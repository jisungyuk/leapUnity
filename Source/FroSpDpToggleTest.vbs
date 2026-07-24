' Follow-up test for the PowerLab FRO OFF->ON crash investigation.
'
' FroInitTest.vbs showed that after Preload+Bounce warm-up, going
' NoPulse (both outputs OFF) -> DoublePulse (both outputs ON) still crashed
' PowerLab, ~5s into the post-bounce recording.
'
' Hypothesis to test here: the crash may be specific to the "both outputs OFF"
' (NoPulse) -> "both outputs ON" (DoublePulse) transition, not to Output1
' (Conditioning) toggling alone. In DoublePulse/SinglePulse, Output2 (Testing)
' stays ON in both -- only Output1 (Conditioning) toggles. This script never
' sends NoPulse at all: after the same Preload+Bounce warm-up, it just toggles
' SinglePulse <-> DoublePulse repeatedly (Output2 always stays enabled) to see
' if THAT transition is stable, matching what the reference TMSviewer app
' reportedly did successfully.
'
' SAFETY: run with TMS output cables disconnected from the coil/participant.
'
' Usage: cscript //Nologo "Source\FroSpDpToggleTest.vbs"
' Requires: LabChart already open (PowerLab connected), NOT already sampling --
' this script calls StartSampling itself as part of the Preload+Bounce warm-up.

Option Explicit

Dim fso, App, Doc
Dim hexSP, hexDP
Dim scriptDir
Dim toggle

Set fso = CreateObject("Scripting.FileSystemObject")
scriptDir = fso.GetParentFolderName(WScript.ScriptFullName)

hexSP = LoadHexFromVbs(fso.BuildPath(scriptDir, "SinglePulse.vbs"))
hexDP = LoadHexFromVbs(fso.BuildPath(scriptDir, "DoublePulse.vbs"))

Call Connect()

Call Log("=== PHASE 1: Preload + Auto-bounce init (same recipe as FroInitTest.vbs) ===")
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
Call Log("Waiting 2s before toggle phase...")
WScript.Sleep 2000

Call Log("=== PHASE 2: SP <-> DP toggle only -- NoPulse is never sent, Output2 stays enabled throughout ===")

For toggle = 1 To 6
    If toggle Mod 2 = 1 Then
        Call SendMsg(hexDP, "DP (toggle " & toggle & ") -- Output1 OFF->ON")
    Else
        Call SendMsg(hexSP, "SP (toggle " & toggle & ") -- Output1 ON->OFF")
    End If
    WScript.Sleep 1000
Next

Call Log("=== ALL TOGGLES DONE. Manually confirm LabChart is still open/responsive and check the chart for a continuous ~9s recording with no gap. ===")
Call Log("=== Recording is running from this script's StartSampling call -- Stop it manually if this was a scratch test. ===")

' ── Helpers (same as FroInitTest.vbs) ───────────────────────────

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
