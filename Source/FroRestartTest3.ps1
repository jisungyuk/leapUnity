# Third variant in the restart-safety series (see FroRestartTest.ps1 and
# FroRestartTest2.ps1):
#   - FroRestartTest.ps1  : restart via bare StartSampling() alone -> CRASHED
#     (SP<->DP toggling failed on the 3rd toggle after this kind of restart)
#   - FroRestartTest2.ps1 : restart via a SINGLE preload pass (SP->DP->Start,
#     no repeat, no extra bounce) -> result pending
#   - This script         : restart via the FULL Preload+Bounce dance again
#     (SP->DP->Start->Stop->SP->DP->Start, the same recipe as the initial
#     Phase 1 arm), to see whether redoing the complete double-bounce at
#     restart time is safe when there's an actual Stop + delay in between
#     (as opposed to today's earlier back-to-back double-arm test, which ran
#     the whole script twice immediately and broke USB communication).
#
# Sequence:
#   Phase 1: Preload+Bounce init
#   Phase 2: a few SP<->DP toggles to confirm the baseline is working
#   Phase 3: StopSampling() -- simulates "recording stopped for some reason"
#   Phase 4: wait a few seconds
#   Phase 5: FULL Preload+Bounce again (SP->300ms->DP->300ms->Start->500ms->
#            Stop->300ms->SP->300ms->DP->300ms->Start) -- the complete recipe,
#            not just a single preload pass
#   Phase 6: SP<->DP toggling again, same count as before
#
# SAFETY: run with TMS output cables disconnected from the coil/participant.
# Requires: LabChart open (PowerLab connected, fresh restart), NOT already
# sampling, TriggerBox on COM5.
#
# Usage: powershell -ExecutionPolicy Bypass -File "Source\FroRestartTest3.ps1" [-Toggles 30] [-ArmToTriggerMs 2500] [-InterTrialMs 4000]

param(
    [int]$Toggles       = 30,
    [int]$ArmToTriggerMs = 2500,
    [int]$InterTrialMs   = 4000
)

function Log {
    param([string]$msg)
    Write-Host ("{0}  {1}" -f (Get-Date -Format "HH:mm:ss.fff"), $msg)
}

function Get-HexFromVbs {
    param([string]$path)
    if (-not (Test-Path $path)) {
        Log "ERROR: template file not found: $path"
        exit 1
    }
    $txt = Get-Content -Raw $path
    if ($txt -match 'PlayMessage\s*\(\s*"(0x[0-9A-Fa-f]+)"\s*\)') {
        return $Matches[1]
    } else {
        Log "ERROR: could not find PlayMessage hex in $path"
        exit 1
    }
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$hexSP = Get-HexFromVbs (Join-Path $scriptDir "SinglePulse.vbs")
$hexDP = Get-HexFromVbs (Join-Path $scriptDir "DoublePulse.vbs")

try {
    $App = [Runtime.InteropServices.Marshal]::GetActiveObject("ADIChart.Application")
    $Doc = $App.ActiveDocument
    Log "Connected to LabChart. ActiveDocument OK."
} catch {
    Log ("ERROR: could not attach to a running LabChart instance (" + $_.Exception.Message + ")")
    exit 1
}

function Send-Msg {
    param([string]$hex, [string]$label)
    Log "Sending $label ..."
    try {
        $Doc.PlayMessage($hex) | Out-Null
        Log "  -> OK"
    } catch {
        Log ("  -> ERROR: " + $_.Exception.Message)
    }
}

function Invoke-StartSampling {
    param([string]$label = "StartSampling")
    Log "Calling $label ..."
    try {
        $Doc.StartSampling() | Out-Null
        Log "  -> OK"
    } catch {
        Log ("  -> ERROR: " + $_.Exception.Message)
    }
}

function Invoke-StopSampling {
    param([string]$label = "StopSampling")
    Log "Calling $label ..."
    try {
        $Doc.StopSampling() | Out-Null
        Log "  -> OK"
    } catch {
        Log ("  -> ERROR: " + $_.Exception.Message)
    }
}

$ttlPort = New-Object System.IO.Ports.SerialPort "COM5", 115200
try {
    $ttlPort.Open()
    Log "TTL port COM5 opened."
} catch {
    Log ("ERROR: could not open TTL port COM5 (" + $_.Exception.Message + ")")
    exit 1
}

function Fire-Ttl {
    Log "Firing TTL pulse (COM5, channel 1, 100ms) ..."
    try {
        $ttlPort.Write([byte[]]@(1), 0, 1)
        $ttlPort.BaseStream.Flush()
        Start-Sleep -Milliseconds 100
        $ttlPort.Write([byte[]]@(0), 0, 1)
        $ttlPort.BaseStream.Flush()
        Log "  -> OK"
    } catch {
        Log ("  -> ERROR: " + $_.Exception.Message)
    }
}

function Arm-And-Fire {
    param([string]$hex, [string]$label)
    Send-Msg $hex $label
    Start-Sleep -Milliseconds $ArmToTriggerMs
    Fire-Ttl
    Start-Sleep -Milliseconds $InterTrialMs
}

function Toggle-Block {
    param([int]$count, [string]$blockLabel)
    for ($i = 1; $i -le $count; $i++) {
        if ($i % 2 -eq 1) {
            Arm-And-Fire $hexDP "$blockLabel DP (toggle $i / $count)"
        } else {
            Arm-And-Fire $hexSP "$blockLabel SP (toggle $i / $count)"
        }
    }
}

function Run-FullPreloadBounce {
    param([string]$tag)
    Send-Msg $hexSP "SP ($tag preload #1)"; Start-Sleep -Milliseconds 300
    Send-Msg $hexDP "DP ($tag preload #1)"; Start-Sleep -Milliseconds 300
    Invoke-StartSampling "StartSampling ($tag #1)"; Start-Sleep -Milliseconds 500
    Invoke-StopSampling "StopSampling ($tag bounce)"; Start-Sleep -Milliseconds 300
    Send-Msg $hexSP "SP ($tag preload #2)"; Start-Sleep -Milliseconds 300
    Send-Msg $hexDP "DP ($tag preload #2)"; Start-Sleep -Milliseconds 300
    Invoke-StartSampling "StartSampling ($tag #2, final)"
}

# ── Main ──

Log "=== PHASE 1: Preload + Auto-bounce init ==="
Run-FullPreloadBounce "initial"

Log "=== PHASE 1 complete. Waiting 2s before baseline toggles... ==="
Start-Sleep -Seconds 2

Log "=== PHASE 2: baseline SP<->DP toggles (confirm normal operation before the stop/restart test) ==="
Toggle-Block 5 "[baseline]"

Log "=== PHASE 3: StopSampling() -- simulating recording stopping for some reason ==="
Invoke-StopSampling "StopSampling (simulated stop)"

Log "=== PHASE 4: waiting 5s (simulating the gap before pressing R again) ==="
Start-Sleep -Seconds 5

Log "=== PHASE 5: FULL Preload+Bounce again (complete recipe, not just a single preload pass) ==="
Run-FullPreloadBounce "restart"

Log "=== PHASE 5 complete. Waiting 2s before post-restart toggles... ==="
Start-Sleep -Seconds 2

Log "=== PHASE 6: SP<->DP toggles again post-restart, $Toggles toggles ==="
Toggle-Block $Toggles "[post-restart]"

Log "=== ALL PHASES DONE. Compare against FroRestartTest.ps1 (bare StartSampling, crashed) and FroRestartTest2.ps1 (single preload) to see which restart approach is actually safe. ==="
Log "Recording is still running -- Stop it manually when done reviewing."

$ttlPort.Close()
Log "TTL port COM5 closed."
