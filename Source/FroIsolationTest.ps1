# Isolation test: is the PowerLab crash about BOTH outputs going OFF->ON at
# once (NoPulse -> DoublePulse), or about Output2 (Testing) specifically going
# OFF->ON regardless of Output1 (NoPulse -> SinglePulse alone)?
#
# reference/FROmodeissue2.md documents the actual working code from the
# TMSviewer reference app (realtime_viewer.py / labchart_client.py). Two
# things there differ from what we've tried so far:
#   1. That app has NO NoPulse-equivalent state at all -- Output2 (Testing)
#      is NEVER disabled; only Output1 (Conditioning) toggles between
#      SinglePulse (off) and DoublePulse (on). It never turns Output2 off,
#      so it never had to recover from that. This is the leading suspect for
#      why our repeated NoPulse->DoublePulse crash doesn't show up there.
#   2. Its start_sampling() calls doc.StartSampling(0, False, 0) with
#      explicit arguments, not a bare StartSampling() like we've been using.
#
# This script tests both isolated in one run, using the same Preload+Bounce
# init, but now with the (0, False, 0) StartSampling arguments:
#   Step 1: SP -> trigger                          (baseline, Output2 on)
#   Step 2: NoPulse -> trigger                     (ON->OFF, known-safe direction)
#   Step 3: SP -> trigger   *** NoPulse->SP: Output2 ONLY OFF->ON ***
#   Step 4: NoPulse -> trigger                     (back off again)
#   Step 5: DP -> trigger   *** NoPulse->DP: BOTH outputs OFF->ON ***
#
# If step 3 survives but step 5 crashes: the problem is specifically both
# channels flipping at once. If step 3 ALSO crashes: the problem is Output2
# itself being re-enabled from OFF, regardless of Output1.
#
# SAFETY: run with TMS output cables disconnected from the coil/participant.
# Requires: LabChart open (PowerLab connected, fresh restart), NOT already
# sampling, TriggerBox on COM5.
#
# Usage: powershell -ExecutionPolicy Bypass -File "Source\FroIsolationTest.ps1" [-ArmToTriggerMs 2500] [-InterTrialMs 4000]

param(
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
$hexNP = Get-HexFromVbs (Join-Path $scriptDir "NoPulse.vbs")

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

# Using explicit args (0, False, 0), matching reference/FROmodeissue2.md's
# labchart_client.py: self.doc.StartSampling(0, False, 0)
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

Log "=== PHASE 1: Preload + Auto-bounce init (StartSampling now called with (0, False, 0)) ==="
Send-Msg $hexSP "SP (preload #1)"; Start-Sleep -Milliseconds 300
Send-Msg $hexDP "DP (preload #1)"; Start-Sleep -Milliseconds 300
Invoke-StartSampling; Start-Sleep -Milliseconds 500
Invoke-StopSampling; Start-Sleep -Milliseconds 300
Send-Msg $hexSP "SP (preload #2)"; Start-Sleep -Milliseconds 300
Send-Msg $hexDP "DP (preload #2)"; Start-Sleep -Milliseconds 300
Invoke-StartSampling

Log "=== PHASE 1 complete. Waiting 2s before isolation steps... ==="
Start-Sleep -Seconds 2

Log "=== STEP 1: SP (baseline, Output2 on) ==="
Arm-And-Fire $hexSP "SP (step 1, baseline)"

Log "=== STEP 2: NoPulse (ON->OFF, known-safe direction) ==="
Arm-And-Fire $hexNP "NoPulse (step 2)"

Log "=== STEP 3: SP  *** NoPulse->SP: Output2 ONLY OFF->ON, Output1 stays off *** ==="
Arm-And-Fire $hexSP "SP (step 3) *** ISOLATION TEST A ***"

Log "=== STEP 4: NoPulse (back off again) ==="
Arm-And-Fire $hexNP "NoPulse (step 4)"

Log "=== STEP 5: DP  *** NoPulse->DP: BOTH outputs OFF->ON *** ==="
Arm-And-Fire $hexDP "DP (step 5) *** ISOLATION TEST B ***"

Log "=== ALL STEPS DONE. Note which step (if any) crashed: step 3 alone = Output2-specific; only step 5 = simultaneous-both-channels-specific. ==="
Log "Recording is still running -- Stop it manually when done reviewing."

$ttlPort.Close()
Log "TTL port COM5 closed."
