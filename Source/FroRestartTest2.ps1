# Follow-up to FroRestartTest.ps1: that test showed a "bare StartSampling()"
# restart (no preload messages at all) does NOT preserve the stability a full
# Preload+Bounce gives -- SP<->DP toggling crashed again on the 3rd toggle
# after that kind of restart. So SOME re-priming before the restart
# StartSampling is needed -- the question is whether a single SP+DP preload
# pass (no repeat, no extra internal Stop/Start bounce) is enough, without
# repeating the full double-bounce dance that's known to break USB entirely.
#
# Sequence:
#   Phase 1: Preload+Bounce init (same as FroSpDpCycleTest.ps1 / FroRestartTest.ps1)
#   Phase 2: a few SP<->DP toggles to confirm the baseline is working
#   Phase 3: StopSampling() -- simulates "recording stopped for some reason"
#   Phase 4: wait a few seconds
#   Phase 5: SINGLE preload pass (SP -> 300ms -> DP -> 300ms), THEN
#            StartSampling ONCE -- no second Stop/Start, no repeat
#   Phase 6: SP<->DP toggling again, same count as before, to check whether
#            this lighter (single-preload, no re-bounce) restart holds up
#
# SAFETY: run with TMS output cables disconnected from the coil/participant.
# Requires: LabChart open (PowerLab connected, fresh restart), NOT already
# sampling, TriggerBox on COM5.
#
# Usage: powershell -ExecutionPolicy Bypass -File "Source\FroRestartTest2.ps1" [-Toggles 30] [-ArmToTriggerMs 2500] [-InterTrialMs 4000]

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

# ── Main ──

Log "=== PHASE 1: Preload + Auto-bounce init ==="
Send-Msg $hexSP "SP (preload #1)"; Start-Sleep -Milliseconds 300
Send-Msg $hexDP "DP (preload #1)"; Start-Sleep -Milliseconds 300
Invoke-StartSampling "StartSampling #1 (initial arm)"; Start-Sleep -Milliseconds 500
Invoke-StopSampling "StopSampling (bounce)"; Start-Sleep -Milliseconds 300
Send-Msg $hexSP "SP (preload #2)"; Start-Sleep -Milliseconds 300
Send-Msg $hexDP "DP (preload #2)"; Start-Sleep -Milliseconds 300
Invoke-StartSampling "StartSampling #2 (final arm)"

Log "=== PHASE 1 complete. Waiting 2s before baseline toggles... ==="
Start-Sleep -Seconds 2

Log "=== PHASE 2: baseline SP<->DP toggles (confirm normal operation before the stop/restart test) ==="
Toggle-Block 5 "[baseline]"

Log "=== PHASE 3: StopSampling() -- simulating recording stopping for some reason ==="
Invoke-StopSampling "StopSampling (simulated stop)"

Log "=== PHASE 4: waiting 5s (simulating the gap before pressing R again) ==="
Start-Sleep -Seconds 5

Log "=== PHASE 5: SINGLE preload pass (SP -> 300ms -> DP -> 300ms), then ONE StartSampling -- no repeat, no extra bounce ==="
Send-Msg $hexSP "SP (restart preload)"; Start-Sleep -Milliseconds 300
Send-Msg $hexDP "DP (restart preload)"; Start-Sleep -Milliseconds 300
Invoke-StartSampling "StartSampling (restart, single preload, no re-bounce)"

Log "=== PHASE 5 complete. Waiting 2s before post-restart toggles... ==="
Start-Sleep -Seconds 2

Log "=== PHASE 6: SP<->DP toggles again post-restart, $Toggles toggles -- does stability match the original single-Bounce case? ==="
Toggle-Block $Toggles "[post-restart]"

Log "=== ALL PHASES DONE. Compare: did Phase 6 behave the same as Phase 2 / FroSpDpCycleTest.ps1, or was there a crash? ==="
Log "Recording is still running -- Stop it manually when done reviewing."

$ttlPort.Close()
Log "TTL port COM5 closed."
