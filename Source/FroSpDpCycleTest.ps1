# Repeated SP<->DP-only stress test (NoPulse is never sent).
#
# Today's testing found that NoPulse->SP/DP recovery crashes PowerLab
# intermittently no matter what mitigation was tried (Preload+Bounce, real
# TTL triggers, slower cadence, StartSampling(0, False, 0) args). But pure
# SP<->DP toggling -- Output2 (Testing) always stays enabled, only Output1
# (Conditioning) flips -- has not crashed in any of today's tests
# (FroSpDpToggleTest.vbs: 6 toggles untriggered; FroIsolationTest.ps1: SP
# survived twice after NoPulse). This script repeats SP<->DP toggling many
# more times, with real TTL triggers at a realistic cadence, to build real
# confidence that THIS specific transition (unlike NoPulse recovery) is
# actually reliable over a full session's worth of trials.
#
# Same Preload+Bounce init as FroTtlCycleTest.ps1, including the
# StartSampling(0, False, 0) call. NoPulse is never armed at any point.
#
# SAFETY: run with TMS output cables disconnected from the coil/participant.
# Requires: LabChart open (PowerLab connected, fresh restart), NOT already
# sampling, TriggerBox on COM5.
#
# Usage: powershell -ExecutionPolicy Bypass -File "Source\FroSpDpCycleTest.ps1" [-Toggles 30] [-ArmToTriggerMs 2500] [-InterTrialMs 4000]

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
    Log "Calling StartSampling(0, False, 0) ..."
    try {
        $Doc.StartSampling(0, $false, 0) | Out-Null
        Log "  -> OK"
    } catch {
        Log ("  -> ERROR: " + $_.Exception.Message)
    }
}

function Invoke-StopSampling {
    Log "Calling StopSampling ..."
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

# ── Main ──

Log "=== PHASE 1: Preload + Auto-bounce init ==="
Send-Msg $hexSP "SP (preload #1)"; Start-Sleep -Milliseconds 300
Send-Msg $hexDP "DP (preload #1)"; Start-Sleep -Milliseconds 300
Invoke-StartSampling; Start-Sleep -Milliseconds 500
Invoke-StopSampling; Start-Sleep -Milliseconds 300
Send-Msg $hexSP "SP (preload #2)"; Start-Sleep -Milliseconds 300
Send-Msg $hexDP "DP (preload #2)"; Start-Sleep -Milliseconds 300
Invoke-StartSampling

Log "=== PHASE 1 complete. Waiting 2s before toggles... ==="
Start-Sleep -Seconds 2

Log "=== PHASE 2: SP <-> DP toggle only, $Toggles toggles, NoPulse never sent ==="

for ($i = 1; $i -le $Toggles; $i++) {
    if ($i % 2 -eq 1) {
        Arm-And-Fire $hexDP "DP (toggle $i / $Toggles) -- Output1 OFF->ON"
    } else {
        Arm-And-Fire $hexSP "SP (toggle $i / $Toggles) -- Output1 ON->OFF"
    }
}

Log "=== ALL TOGGLES DONE. Check LabChart for continuous recording with no crash. ==="
Log "Recording is still running -- Stop it manually when done reviewing."

$ttlPort.Close()
Log "TTL port COM5 closed."
