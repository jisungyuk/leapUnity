# CLAUDE.md — Project Guidelines for Claude Code

## Communication
- Communicate with the user in **Korean**.
- All code **comments must be in English**.
- Code identifiers (variable names, function names, class names) stay in English.

## File Editing Rules
- **Never use PowerShell `Set-Content`, `Add-Content`, or here-strings to write to files** — these cause encoding corruption on UTF-8 files with Korean text.
- Always use the `Edit` or `Write` tools to modify files.
- Before appending to any `.md` file, read the last ~20 lines first to confirm encoding is intact.
- Never run background shell commands that write to source files.

## Project Overview
- Unity 2021.3.45f1, Ultraleap hand-tracking experiment app.
- Main game mode: **RWR** (Reaching with Rotation). Focus is on RWR only.
- Flow: MainMenu → Target/Session Table → Game Scene → End.
- Config is held in `RuntimeConfigStore` (singleton) and passed between scenes in memory.
- Trial data (hand coordinates, state codes, TTL markers) saved as CSV per trial.

## Key Scripts
- `TrialGameController_RWR.cs` — state machine for each trial
- `GameSessionController_RWR.cs` — sequences trials
- `SessionTableController_RWR.cs` — session table UI, CSV save/load
- `TargetTableController_RWR.cs` — target table UI
- `RuntimeConfigStore.cs` — singleton config store (TrialSpec list, participant folder)
- `LabChartFro.cs` — LabChart FRO per-trial automation via VBScript + cscript.exe

## LabChart FRO System
- TriggerBox receives one TTL pulse 1 second before the Go cue.
- FRO fires Output1 (Testing Stimulus) and Output2 (Conditioning Stimulus) at absolute delays from that pulse.
- `LabChartFro.PrepareOutputs(out1AbsoluteMs, out2AbsoluteMs, doublePulse)` is called during `ShowDirectionCue()` before each trial.
- Three VBS templates in `Source/`:
  - `DoublePulse.vbs` — Output1 placeholder `0.0501`, Output2 placeholder `0.0525`, both enabled
  - `SinglePulse.vbs` — Output1 placeholder `0.0501`, Output2 disabled (On=0)
  - `NoPulse.vbs` — both outputs disabled; sent as-is when ttlEnabled=false
- Binary message patched in-place (same-length UTF-16LE replacement + checksum at bytes 20–23).
- COM interop done via `GetObject(,"ADIChart.Application")` in a temp VBS run by `cscript.exe`.
- Calling convention:
  - `out1AbsoluteMs = 1000 + ts` (ms from TTL trigger)
  - `out2AbsoluteMs = 1000 + ts + cs` (ms from TTL trigger)
  - `doublePulse = (cs != 0)`

## Session CSV Format
```
#,hand,target,start_r,hold,wait,move,ts,cs,inst
```
- `ts` = Testing Stimulus offset (ms from Go cue)
- `cs` = Conditioning Stimulus offset (ms from Go cue); empty or 0 = SinglePulse
- `inst` = 0 REST / 1 REACH / 2 REACH+GRASP

## Worklog
- Log file: `WORKLOG.md`
- Add an entry at the end of each session summarizing what was done and what comes next.
- Use the `Edit` tool to append — never shell commands.
