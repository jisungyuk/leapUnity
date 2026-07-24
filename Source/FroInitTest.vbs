' Standalone test harness for the PowerLab FRO OFF->ON crash.
'
' Background: LabChartFro.cs sends SinglePulse/DoublePulse/NoPulse PlayMessage blobs
' to LabChart via COM before each RWR trial. Going DoublePulse -> Single/NoPulse (Output1
' ON->OFF) is safe, but Single/NoPulse -> DoublePulse (Output1 OFF->ON) crashes the
' PowerLab firmware while sampling is running (see reference/FROmodeissue.md and
' WORKLOG.md 2026-04-27 for the root-cause writeup).
'
' This script tests whether the "Preload + Auto-bounce" fix described in
' reference/FROmodeissue.md (send SP+DP before StartSampling, twice, with a
' Stop/Start bounce in between) prevents that OFF->ON crash in THIS project's
' exact failure condition. It runs standalone via cscript.exe -- no Unity build
' needed, so it can be re-run quickly against real hardware while iterating.
'
' SAFETY: run this with the TMS output cables disconnected from the coil/participant.
' This script fires many DP/SP/NoPulse messages back to back, which would trigger
' real TMS pulses if the outputs are physically wired up.
'
' Usage: cscript //Nologo "Source\FroInitTest.vbs"
' Requires: LabChart already open (with PowerLab connected), same as during a real
' experiment. Watch the console -- if it stops advancing partway through a
' "Sending ..." line, that line is the crash point. LabChart itself may also
' freeze/stop responding at that point.

Option Explicit

Dim fso, App, Doc
Dim hexSP, hexDP, hexNP
Dim scriptDir
Dim cycle

Set fso = CreateObject("Scripting.FileSystemObject")
scriptDir = fso.GetParentFolderName(WScript.ScriptFullName)

hexSP = LoadHexFromVbs(fso.BuildPath(scriptDir, "SinglePulse.vbs"))
hexDP = LoadHexFromVbs(fso.BuildPath(scriptDir, "DoublePulse.vbs"))
hexNP = LoadHexFromVbs(fso.BuildPath(scriptDir, "NoPulse.vbs"))

Call Connect()

Call Log("=== PHASE 1: Preload + Auto-bounce init (reference/FROmodeissue.md recipe) ===")
Call SendMsg(hexSP, "SP (preload #1)")
WScript.Sleep 300
Call SendMsg(hexDP, "DP (preload #1)")
WScript.Sleep 300
Call DoStartSampling()
WScript.Sleep 500
Call DoStopSampling()
WScript.Sleep 300
Call SendMsg(hexSP, "SP (preload #2)")
WScript.Sleep 300
Call SendMsg(hexDP, "DP (preload #2)")
WScript.Sleep 300
Call DoStartSampling()

Call Log("=== PHASE 1 complete. Check the LabChart window: recording should be running. ===")
Call Log("Waiting 2s before Phase 2...")
WScript.Sleep 2000

Call Log("=== PHASE 2: reproduce this project's exact crash condition (DP -> SP -> NoPulse -> DP) ===")
Call SendMsg(hexDP, "DP (trial A)")
WScript.Sleep 1000
Call SendMsg(hexSP, "SP (trial B) -- ON->OFF, previously safe")
WScript.Sleep 1000
Call SendMsg(hexNP, "NoPulse (trial C) -- already OFF")
WScript.Sleep 1000
Call SendMsg(hexDP, "DP (trial D) *** CRITICAL OFF->ON TRANSITION ***")
WScript.Sleep 1000

Call Log("=== PHASE 2 complete. If no ERROR/hang above and LabChart is still responsive, the OFF->ON transition survived. ===")
Call Log("=== PHASE 3: repeat the cycle twice more with no extra bounce, to check whether only the FIRST OFF->ON survives ===")

For cycle = 1 To 2
    Call Log("--- repeat cycle " & cycle & " ---")
    Call SendMsg(hexSP, "SP (cycle " & cycle & ")")
    WScript.Sleep 1000
    Call SendMsg(hexNP, "NoPulse (cycle " & cycle & ")")
    WScript.Sleep 1000
    Call SendMsg(hexDP, "DP (cycle " & cycle & ") *** OFF->ON ***")
    WScript.Sleep 1000
Next

Call Log("=== ALL PHASES DONE. Manually confirm LabChart is still open/responsive. ===")
Call Log("=== Recording is running from this script's StartSampling call -- Stop it manually if this was a scratch test. ===")

' ── Helpers ──────────────────────────────────────────────────────

Sub Log(msg)
    WScript.Echo Now & "  " & msg
End Sub

Sub Connect()
    On Error Resume Next
    Err.Clear
    Set App = GetObject(, "ADIChart.Application")
    If Err.Number <> 0 Then
        Call Log("ERROR: could not attach to a running LabChart instance (" & Err.Description & ")")
        WScript.Quit 1
    End If
    On Error Goto 0
    Set Doc = App.ActiveDocument
    Call Log("Connected to LabChart. ActiveDocument OK.")
End Sub

' Extracts the PlayMessage hex blob from one of the recorded template VBS files
' (Source/SinglePulse.vbs / DoublePulse.vbs / NoPulse.vbs), mirroring the regex
' LabChartFro.cs.EnsureTemplate() uses in C#, so this test never needs its own
' copy of the (very long) hex strings.
Function LoadHexFromVbs(path)
    Dim ts, txt, re, mc

    If Not fso.FileExists(path) Then
        Call Log("ERROR: template file not found: " & path)
        WScript.Quit 1
    End If

    Set ts = fso.OpenTextFile(path, 1) ' 1 = ForReading
    txt = ts.ReadAll
    ts.Close

    Set re = New RegExp
    re.Pattern = "PlayMessage\s*\(\s*""(0x[0-9A-Fa-f]+)""\s*\)"
    re.IgnoreCase = True
    re.Global = False

    Set mc = re.Execute(txt)
    If mc.Count = 0 Then
        Call Log("ERROR: could not find PlayMessage hex in " & path)
        WScript.Quit 1
    End If

    LoadHexFromVbs = mc(0).SubMatches(0)
End Function

Sub SendMsg(hex, label)
    Call Log("Sending " & label & " ...")
    On Error Resume Next
    Err.Clear
    Call Doc.PlayMessage(hex)
    If Err.Number <> 0 Then
        Call Log("  -> ERROR: " & Err.Description & " (0x" & Hex(Err.Number) & ")")
    Else
        Call Log("  -> OK")
    End If
    On Error Goto 0
End Sub

Sub DoStartSampling()
    Call Log("Calling StartSampling ...")
    On Error Resume Next
    Err.Clear
    Call Doc.StartSampling()
    If Err.Number <> 0 Then
        Call Log("  -> ERROR: " & Err.Description)
    Else
        Call Log("  -> OK")
    End If
    On Error Goto 0
End Sub

Sub DoStopSampling()
    Call Log("Calling StopSampling ...")
    On Error Resume Next
    Err.Clear
    Call Doc.StopSampling()
    If Err.Number <> 0 Then
        Call Log("  -> ERROR: " & Err.Description)
    Else
        Call Log("  -> OK")
    End If
    On Error Goto 0
End Sub
