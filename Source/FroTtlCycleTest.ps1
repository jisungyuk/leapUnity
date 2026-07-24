# Repeated-cycle version of FroTtlTest.ps1.
#
# The original production crash (WORKLOG 2026-04-27) was found after several
# trials -- the documented workaround allowed up to ~8 trials before a
# NoPulse->DoublePulse "wrap-around" crashed. A single DP->SP->NoPulse->DP
# pass (FroTtlTest.ps1) succeeded once, which isn't enough evidence on its
# own -- this script repeats that same triggered cycle several times in a row
# to check whether it holds up over more OFF->ON transitions, closer to real
# session length.
#
# Same recipe/protocol as FroTtlTest.ps1: Preload+Bounce init once, then per
# cycle: arm DP -> trigger, arm SP -> trigger, arm NoPulse -> trigger, arm DP
# -> trigger (the critical OFF->ON transition), with no extra bounce between
# cycles.
#
# SAFETY: run with TMS output cables disconnected from the coil/participant.
# Requires: LabChart open (PowerLab connected), NOT already sampling,
# TriggerBox on COM5.
#
# Usage: powershell -ExecutionPolicy Bypass -File "Source\FroTtlCycleTest.ps1" [-Cycles 15] [-ArmToTriggerMs 2500] [-InterTrialMs 4000]
#
# IMPORTANT: Preload+Bounce (StartSampling -> StopSampling -> StartSampling) must
# only happen ONCE per LabChart session. Running this script a second time
# against a LabChart instance that already went through a Bounce (from a
# previous run of this script, or FroTtlTest.ps1 / FroInitTest.vbs) has been
# observed to break USB communication with the PowerLab entirely ("LabChart
# Action Failed - problem communicating with the PowerLab 8/35"), needing a
# full LabChart/PowerLab restart to recover. Always restart LabChart fresh
# before (re-)running this script.
#
# A fresh-restart run at the original tight cadence (arm->trigger 200ms,
# trigger->next-arm 800ms) still crashed with the same USB communication
# failure partway through, and slowing the cadence down (this version's
# defaults) still crashed at a NoPulse->X transition too.
#
# reference/FROmodeissue2.md (the actual working code from the TMSviewer
# reference app) calls doc.StartSampling(0, False, 0) with explicit args,
# not a bare StartSampling(). FroIsolationTest.ps1, updated to match that
# signature, survived NoPulse->SinglePulse AND NoPulse->DoublePulse twice in a
# row on the same LabChart session (which previously broke USB entirely) --
# this version now uses the same (0, False, 0) call to see if it holds up
# over a full repeated-cycle stress run too.

param(
    [int]$Cycles = 15,
    [int]$ArmToTriggerMs = 2500,  # delay between PlayMessage arm and the TTL trigger (real: ~goDelay)
    [int]$InterTrialMs   = 4000   # delay between TTL trigger and the next trial's arm (real: Executing+Feedback+ITI)
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

function Invoke-StartSampling {
    # Explicit args (0, False, 0), matching reference/FROmodeissue2.md's
    # labchart_client.py: self.doc.StartSampling(0, False, 0) -- the earlier
    # bare StartSampling() calls may have been missing something PowerLab
    # expects.
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

# TTL port is opened once and kept open for the whole run (matching
# TrialGameController_RWR.cs's OpenTtlPort()/FireTtlPulse() -- it does NOT
# reopen the port per pulse). Repeatedly opening/closing a USB-serial port
# per pulse, as an earlier version of this script did, made Windows play a
# device connect/disconnect chime on every single trigger.
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

Log "=== PHASE 1: Preload + Auto-bounce init (reference/FROmodeissue.md recipe) ==="
Send-Msg $hexSP "SP (preload #1)"; Start-Sleep -Milliseconds 300
Send-Msg $hexDP "DP (preload #1)"; Start-Sleep -Milliseconds 300
Invoke-StartSampling; Start-Sleep -Milliseconds 500
Invoke-StopSampling; Start-Sleep -Milliseconds 300
Send-Msg $hexSP "SP (preload #2)"; Start-Sleep -Milliseconds 300
Send-Msg $hexDP "DP (preload #2)"; Start-Sleep -Milliseconds 300
Invoke-StartSampling

Log "=== PHASE 1 complete. Waiting 2s before cycles... ==="
Start-Sleep -Seconds 2

Log "=== PHASE 2: repeating DP -> SP -> NoPulse -> DP (triggered) for $Cycles cycles, no extra bounce between cycles ==="
Log "    (arm->trigger delay: ${ArmToTriggerMs}ms, trigger->next-arm delay: ${InterTrialMs}ms)"

for ($i = 1; $i -le $Cycles; $i++) {
    Log "--- Cycle $i / $Cycles ---"
    Arm-And-Fire $hexDP "DP (cycle $i, trial A)"
    Arm-And-Fire $hexSP "SP (cycle $i, trial B) -- ON->OFF"
    Arm-And-Fire $hexNP "NoPulse (cycle $i, trial C)"
    Arm-And-Fire $hexDP "DP (cycle $i, trial D) *** OFF->ON ***"
}

Log "=== ALL CYCLES DONE. Check LabChart: continuous recording, no crash popups, and confirm which cycle (if any) it stopped advancing at. ==="
Log "Recording is still running -- Stop it manually when done reviewing."

$ttlPort.Close()
Log "TTL port COM5 closed."
