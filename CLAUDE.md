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
- TriggerBox receives one TTL pulse **500 ms** before the planned Go cue (`TtlLeadS = 0.5`), every trial
  (also NoPulse trials, as a LabChart event marker) unless the session runs without LabChart (`skipLabChart`).
  The trigger time does not depend on ts/cs.
- Go is timed from the ACTUAL trigger time: the frame nearest to trigger + 500 ms (half-frame tolerance, so
  vsync frame-boundary wobble cannot push Go one frame late).
- FRO fires Output1 (Conditioning Stimulus) and Output2 (Testing Stimulus) at absolute delays from that pulse
  (PowerLab hardware timing).
- `LabChartFro.PrepareOutputs(out1AbsoluteMs, out2AbsoluteMs, doublePulse)` is called during `ShowDirectionCue()` before each trial.
- Three VBS templates, read from next to the running app (project root in the Editor, the .exe folder in a build;
  `Assets/Editor/CopyLabChartFilesPostBuild.cs` copies them, and the `.adiset`, into the build folder):
  - `DoublePulse.vbs` — Output1 placeholder `0.0501`, Output2 placeholder `0.0525`, both enabled
  - `SinglePulse.vbs` — Output1 disabled (On=0), Output2 placeholder `0.0525` enabled
  - `NoPulse.vbs` — both outputs disabled; sent as-is when ttlEnabled=false
- Binary message patched in-place (same-length UTF-16LE replacement + checksum at bytes 20–23).
- COM interop done via `GetObject(,"ADIChart.Application")` in a temp VBS run by `cscript.exe`.
- Calling convention (`TrialGameController_RWR.ShowDirectionCue`):
  - `ttlOffsetMs = ts`, `ttl2OffsetMs = -|cs|` (cs is forced negative: Conditioning always fires before Testing)
  - `out1AbsoluteMs = 500 + ts - |cs|` (Conditioning, ms from TTL trigger)
  - `out2AbsoluteMs = 500 + ts` (Testing, ms from TTL trigger)
  - `doublePulse = (cs != 0)`; otherwise SinglePulse (Testing only)
  - Both delays must be within 0–9900 ms, so ts ≥ -500 (and ts - |cs| ≥ -500)
- Per-trial timing fields in the trial CSV header (Unity frame clock; excludes USB-serial and display latency):
  - `jitter_from_scheduled_ms = (ttlFiredTime - goTime) + 500` — TTL→Go deviation from 500 ms
  - `ts_minus_go_ms = jitter + ts`, `cs_minus_go_ms = jitter + ts - |cs|`
  - Blank when ttlEnabled=false (NoPulse) or the port is not open
- Photodiode patch (`PhotodiodePatch.cs`, optional in MainMenu → Game Settings): a corner square, black, white for
  100 ms from the Go frame. A photodiode on it recorded in LabChart gives the real trigger → Go-on-screen time;
  use the constant part to compensate ts. Trial header field `photodiode_patch`.

## Session CSV Format
```
#,hand,target,start_r,hold,wait,move,ts,cs,inst
```
- `ts` = Testing Stimulus offset (ms from Go cue; 0 = at Go, -50 = 50 ms before Go); empty or `.` = NoPulse (no stimulation)
- `cs` = Conditioning Stimulus offset (ms before Testing; sign ignored, always applied as -|cs|); empty or 0 = SinglePulse
- `inst` = 0 REST / 1 REACH / 2 REACH+GRASP

## Worklog
- Log file: `WORKLOG.md`
- Add an entry at the end of each session summarizing what was done and what comes next.
- Use the `Edit` tool to append — never shell commands.
