# Follow-up test for the PowerLab FRO OFF->ON crash investigation -- this time
# with an actual TTL trigger pulse sent over COM5, so the FRO subsystem behaves
# exactly as it does during a real trial (arm via PlayMessage, then wait for a
# real trigger event to actually discharge Output1/Output2), instead of just
# re-arming with PlayMessage and never triggering anything.
#
# Reuses:
#   - Preload+Bounce init recipe from reference/FROmodeissue.md (same as
#     Source/FroInitTest.vbs Phase 1)
#   - The exact TTL pulse protocol from TrialGameController_RWR.cs's
#     FireTtlPulse(): SerialPort(COM5, 115200), channel 1 -> byte 0x01 high,
#     flush, wait 100ms, byte 0x00, flush (see FireTtlPulse()/OpenTtlPort()).
#
# This is a PowerShell script (not VBS) because classic VBScript has no native
# serial port access; PowerShell can talk to both the LabChart COM object and
# System.IO.Ports.SerialPort directly.
#
# SAFETY: run with TMS output cables disconnected from the coil/participant.
# This sends 4 real TTL triggers, each of which will fire whatever FRO output
# is currently armed.
#
# Requires: LabChart already open (PowerLab connected), NOT already sampling,
# TriggerBox connected on COM5.
#
# Usage: powershell -ExecutionPolicy Bypass -File "Source\FroTtlTest.ps1"

function Log {
    param([string]$msg)
    Write-Host ("{0}  {1}" -f (Get-Date -Format "HH:mm:ss.fff"), $msg)
}

# Extracts the PlayMessage hex blob from a recorded template VBS file, same
# pattern as LabChartFro.cs's EnsureTemplate() and FroInitTest.vbs's
# LoadHexFromVbs().
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

# ── Connect to LabChart (attach to the running instance, like VBS's GetObject(,"ADIChart.Application")) ──
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
    Log "Calling StartSampling ..."
    try {
        $Doc.StartSampling() | Out-Null
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

# Matches TrialGameController_RWR.cs FireTtlPulse(): channel 1 = 0x01, 100ms high, then reset.
function Fire-Ttl {
    Log "Firing TTL pulse (COM5, channel 1, 100ms) ..."
    $ttlPort = New-Object System.IO.Ports.SerialPort "COM5", 115200
    try {
        $ttlPort.Open()
        $ttlPort.Write([byte[]]@(1), 0, 1)
        $ttlPort.BaseStream.Flush()
        Start-Sleep -Milliseconds 100
        $ttlPort.Write([byte[]]@(0), 0, 1)
        $ttlPort.BaseStream.Flush()
        Log "  -> OK"
    } catch {
        Log ("  -> ERROR: " + $_.Exception.Message)
    } finally {
        if ($ttlPort.IsOpen) { $ttlPort.Close() }
    }
}

# ── Main ──

Log "=== PHASE 1: Preload + Auto-bounce init (reference/FROmodeissue.md recipe) ==="
Send-Msg $hexSP "SP (preload #1)"; Start-Sleep -Milliseconds 300
Send-Msg $hexDP "DP (preload #1)"; Start-Sleep -Milliseconds 300
Invoke-StartSampling; Start-Sleep -Milliseconds 500
Invoke-StopSampling; Start-Sleep -Milliseconds 300
Send-Msg $hexSP "SP (preload #2)"; Start-Sleep -Milliseconds 300
Send-Msg $hexDP "DP (preload #2)"; Start-Sleep -Milliseconds 300
Invoke-StartSampling

Log "=== PHASE 1 complete. Check the LabChart window: recording should be running. ==="
Log "Waiting 2s before Phase 2..."
Start-Sleep -Seconds 2

Log "=== PHASE 2: arm + REAL TTL trigger per state, reproducing the known crash order (DP -> SP -> NoPulse -> DP) ==="

Log "--- Trial A: DoublePulse ---"
Send-Msg $hexDP "DP (trial A)"
Start-Sleep -Milliseconds 200
Fire-Ttl
Start-Sleep -Milliseconds 800

Log "--- Trial B: SinglePulse (Output1 ON->OFF, previously safe direction) ---"
Send-Msg $hexSP "SP (trial B)"
Start-Sleep -Milliseconds 200
Fire-Ttl
Start-Sleep -Milliseconds 800

Log "--- Trial C: NoPulse (both already OFF) ---"
Send-Msg $hexNP "NoPulse (trial C)"
Start-Sleep -Milliseconds 200
Fire-Ttl
Start-Sleep -Milliseconds 800

Log "--- Trial D: DoublePulse *** CRITICAL OFF->ON, now WITH a real trigger *** ---"
Send-Msg $hexDP "DP (trial D)"
Start-Sleep -Milliseconds 200
Fire-Ttl
Start-Sleep -Milliseconds 800

Log "=== PHASE 2 complete. ==="
Log "Check LabChart: each trial should show an Events/comment page. SP/DP trials should show an actual FRO output pulse (~50ms after their trigger); NoPulse should show none."
Log "Recording is still running from this script's StartSampling call -- Stop it manually when done reviewing."
