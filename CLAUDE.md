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
- Two main game modes: **RWR** (Reaching with Rotation) and **RWR2**.
- Flow: MainMenu → Target/Session Table → Game Scene → End.
- Config is held in `RuntimeConfigStore` (singleton) and passed between scenes in memory.
- Trial data (hand coordinates, state codes, TTL markers) saved as CSV per trial.

## Key Scripts
- `TrialGameController_RWR.cs` / `TrialGameController_RWR2.cs` — state machine for each trial
- `GameSessionController_RWR.cs` / `GameSessionController_RWR2.cs` — sequences trials
- `SessionTableController_RWR.cs` — session table UI, CSV save/load
- `TargetTableController_RWR.cs` — target table UI
- `RuntimeConfigStore.cs` — singleton config store (TrialSpec list, participant folder)
- `LabChartFro.cs` — LabChart FRO per-trial automation via VBScript + cscript.exe

## LabChart FRO System
- TriggerBox receives one TTL pulse at the Go cue.
- FRO fires Output1 (fixed 50ms) and Output2 (per-trial) after that pulse.
- `LabChartFro.PrepareOutputs()` is called during `ShowDirectionCue()` before each trial.
- Template: `Source/Firstt.vbs` — Output1 = 0.05s, Output2 = 0.0525s (6-char placeholder).
- Binary message patched in-place (same-length UTF-16LE replacement + checksum at bytes 20–23).
- COM interop done via `GetObject(,"ADIChart.Application")` in a temp VBS run by `cscript.exe`.

## Session CSV Format
```
#,target,startx,starty,startz,hand,ttl1,ttl2_offset,instruction
```

## Worklog
- Log file: `WORKLOG.md`
- Add an entry at the end of each session summarizing what was done and what comes next.
- Use the `Edit` tool to append — never shell commands.
