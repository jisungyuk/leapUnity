# Work Log

## 2026-03-06

### ???? ??

- ? ????? Unity 2021.3.45f1 ??? Ultraleap ? ?? ?? ???.
- `Reach`? `Reach-to-Grasp` ? ??? ????.
- ?? ???? ??? ????, ??/?? ???? ??? ?, ?? trial? ??? ? ??? CSV? ???? ???.
- ?? ??? `Packages/manifest.json`, `ProjectSettings/ProjectVersion.txt`, `ProjectSettings/EditorBuildSettings.asset`? ???? ??.

### ?? ??

- ?? ??? `MainMenu -> (Target/Session/GameSetting) -> Game -> EndScene`??.
- `MainMenu.cs`? ??? ?? ??? ?? `R_*` ?? `RG_*` ??? ?????.
- ?? ? ??:
  - participant folder? ???? ??? ??.
  - target ??? ???? ???? ??.
  - session trial ??? ???? ???? ??.
- `TargetTableController.cs`, `SessionTableController.cs`? ??? UI ??? `RuntimeConfigStore`? ????.
- `GameSessionController.cs`, `GameSessionController_RG.cs`? ??? ??/?? ???? ?? trial ??? ??? ?? ????.
- `LeapFingerInput.cs`? Leap/Ultraleap ????? ?? tip, ?? tip, MCP? ????.
- `TrialGameController.cs`, `TrialGameController_RG.cs`? ?? ?? ?? trial ?? ??? ????.

### ?? ?? ??

- ???? participant folder? ????.
- ?? ????? ?? ??? ??? ????.
- ?? ????? target ID, ?? ??, hand, TTL, VF? ????.
- ?? ?? ? ? trial? ???? ????.
- ?? ?? ??? ?? ?? ???? `Ready`, ??? ? `Go`, ?? ??/?? ?? ?????? trial? ???.
- ?? ? ? ?? ???, ?? ??, TTL ??? CSV? ????.

### ??? ?? ??

- `TrialDataLogger.cs`? trial? ??? ????.
- ?? ??? participant folder ?? `session_NNN/01/0001.csv`, `session_NNN/02/0001.csv` ???.
- ? trial?? ???? ??? ??? ??? ? ? ???.
- ???? ?? ? ??? 0 ??? ??? ????.

### ??? ???

- `TrialDataLogger.cs`?? TTL ?? ???? ?? ?? ? ????.
- ? ?? ?? trial? ? ?? ?? ???? TTL=1 ??? ???? ?? ???? ??.
- `R`/`RG`? ?? ? trial ????? ?? ?? ??? ?? ? ??? ??? ??.
- CSV ?? ?? ?? ??? ?? ?? ?? ?? ??? ?? ??? ???? ??.
- ???/?? ??? ??? ?? ??? ??.

### ?? ?? ??

- ? ?? `WORKLOG.md`? ?? ???? ????.
- ?? ??? ??? ?? ??? ????, ?? ? ?? ??? ??? ??? ????.
- ?? ??? ?? ? ???? ?? ?? ??? ??? ???? ??.
- ? ???? ?? ?? ? ?? ??? ???? ??.

## 2026-04-01

### TMS / LabChart / TTL ?? ??

- ?? ?? ??? ???? ??:
  - `Computer <-> TMS`
  - `Computer -> triggerbox -> PowerLab -> TMS`
- ?? MATLAB ???? `LabChart` ?? ???? ? `F6` ?? `F7` ?? ?? TMS stimulation? ?????.
- ??? Unity reaching ???? `FireTtlPulse()` ??? ??? stimulation? ???? ?? ???.
- ?? ????? TTL? `Go` ?? offset ??? ??? ???, `AppActivate + SendKeys` ??? ??? ?? ??? ???? ???? ??? ????.

### LabChart ?? ?? ??

- `F6` ??:
  - TMS ??
  - LabChart? comment/trigger ??? ??
- `F7` ??:
  - TMS ??
  - LabChart?? comment ?? ??
- ?? ????? ???? ??/??? ??? ???.
- ?? LabChart? ?? ??? ?? ??? trigger ???? ???, ????? ???? ???? ??.

### COM5 direct control ??

- ????? ?? ??? ??? ??? `COM3`, `COM4`, `COM5`??.
- `COM5`? Windows `mode` ?? `115200`, `8N1`, handshake ???? ?? ??.
- .NET `System.IO.Ports.SerialPort`? `COM5`? ?? ? ? ?? ?? ????.
- ??? Unity/C#?? COM5? ?? ???? ??? ????? ???? ??.

### ?? ??

- ?? ? ??:
  - `Unity -> COM5 -> triggerbox -> PowerLab -> TMS`
- `Unity -> LabChart -> TMS` ??? ??? ??? ??? ? ??? ?? ?????.
- ?? ?? ??? ??:
  - COM5 ??? ?? ??/??? ??? ?? pulse? ?????
  - LabChart marker? ??? ??? ???, ?? PowerLab ??? ?? ??? ??? ? ???

### reference/App_design ?? ??

- `reference/App_design.mlapp`? ??? ??, MATLAB App Designer ? ?? ??? ????.
- ?? ??:
  - ?? ?? stimulation ??? `LabChart` ? ??? ? `SendKeys(F6/F7)`? ?? ??? ??.
  - ??? reaching/task ?? ??? ??? ??? ??? ?? ?? pulse? ??? ??? ????.
- reaching/task ? ???? ??? ??:
  - `SerialPortObj=serial('COM3', 'TimeOut', 1)`? ??? ??.
  - ?/? Go ??? `gLCDoc.AppendComment('Trigger')`? ????.
  - ??? `fwrite(SerialPortObj, 255, 'sync'); pause(0.01); fwrite(SerialPortObj, 0, 'sync');`? ????.
- ??:
  - ?? reaching ?? MATLAB ??? ?? "LabChart comment ??"? "?? serial trigger pulse"? ???? ??? ?? ??.
  - ? Unity??? ??? ??? ?? ?? ?????.
- ??:
  - MATLAB reference??? `COM3`? ????, ?? ??/?? ?? ???? `COM4`? ???.
  - ?? ?? ????? ?? ??? `COM5`??? ??? ???? ? ??, ?? ?? ???? ??? ??.

### COM5 direct pulse ??? ??

- PowerShell/.NET `SerialPort`? `COM5`? ?? `255 -> 10ms -> 0` pulse? ? ? ????.
- ??:
  - TMS ?? ??
  - LabChart ?? ??
- ??:
  - ?? `COM5`? MATLAB reference? ?? serial trigger ??(`COM3`/`COM4`)? ?? ??? ?? ?? ???? ??.
  - ?? ?? ??? ?? ??? `255 -> 0`? ??? ?? ????, ?? ??, ?? DTR/RTS ?? ??? ??? ? ??.

### Brain Products ?? ??/?? ??

- ???? Brain Products ?? ??? ????:
  - `C:\Program Files\BrainVision\TriggerBox`
- ??? ?? ??:
  - `TriggerBoxConfiguration.exe`
  - `TriggerBoxTestIO.exe`
  - `Drivers\TriggerSerialDriver\...\TrgVirtualSerial.dll`
- ?? ??? ??? `TriggerBox VirtualSerial Port (COM5)`? ?? Brain Products ??? ????, ?? ??? `TriggerBox Plus`??? ?? `TriggerBox rev. 02` ??? ???? ??.
- ??? Brain Products ?? ??:
  - `TriggerBox VirtualSerial Port`?? ??? ?? rev. 02 ? ??? ????.
  - rev. 02? ?? ??? ?? ???? baud rate? ????? ???? ??? ???? ??.
  - Brain Products? `TriggerBox Test IO`? ??? USB trigger ??? ?? ????? ????.
- ?? ?? ?? ??:
  - `TriggerBoxTestIO.exe`? ??? ??? ?? ?/???? ????? ??
  - `TriggerBoxConfiguration.exe`?? ?? ???/?? ??? ??

### ?? ?? ?? ??

- ?? ?? ??, LabChart? `F6` ???? ??? ?? ????:
  - `Doc.SetStimulatorOn(0, False)`
  - `Doc.SetStimulatorOn(1, True)`
  - `Doc.StimulateNow()`
- ??:
  - `F6/F7`? TriggerBox? ???? ???? ???, LabChart `Doc` ??? ?? stimulator? ?? ???? ???.
  - ??? `F6` ?? ? TriggerBox ?? ?? ?? ?? TMS? ??? ??? ????.
  - LabChart comment? TriggerBox ?? ??? ???, TMS/LabChart stimulation ??? feedback? ??? ??? ???? ??.
- direct COM5 ??? ???:
  - `COM5 -> TriggerBox` direct write? TriggerBox pulse? ?? ? ??.
  - ??? ?? ?? ????? ? pulse? ??? TMS stimulation ??? ???.
- ?? ?? ??:
  - `??? timing`? ???? ?? ??? ??? ?? ???? ??.
  - `?? ??? ?? TMS? ??? ?? ????? ??`? LabChart macro(`StimulateNow`) ???.

## 2026-04-03

### ??? ??: FireTtlPulse() ?? ???? ??

- ?? `TrialGameController.FireTtlPulse()`? Debug.Log? ?? ?? ?? ?? (TODO ??).
- ?? ??: COM5 ? TriggerBox ? PowerLab ? TMS
- ?? ?? ??:
  - TriggerBox? ?? ??/???? ??? ?? pulse? ???? (TriggerBoxTestIO.exe? ?? ??? ??)
  - `255 ? 0` ??? ??? ?? ? ???? ?? ???? ?? DTR/RTS ?? ?? ??? ??
  - LabChart marker ??? ??? ??? ?? (????? ??? vs ???? ??)
- ? ??? ?? ??? ?? ?? ? Unity ?? ??.
- RWR ??? TTL? ??? Go + offset_ms ?? ?? ?? ?? ??.

### RWR (Real World Reaching) ?? ?? ??

- ?? Reach/RG ??? ?? ?? ?? ?? ?? ??? ??? ?.
- Leap Motion ???? ?? ?? ??? ?? ???????? ??.
- ?? ? ??? ?? ?? ?? ? Unity ? ?? start zone?? ??.
- ??: ?? start zone ?? ? hold ? direction ? (Reach / Rest) ? go ? ?? zone ??? ? N? ? trial ??.
- kinematic ???? ??? ???? CSV ??.
- ?? hit check ?? (?? ??? ?? ? ???? ??).
- TTL: ??? ??? Go + offset_ms ?? ??.
- ?? ?? ? ?? ?? ?.

### RWR Calibration System (??)

**??:**
- ?? ?? ? ???? ?? ?? ??? ?? ???? SPACE? ??? MCP ??? ??
- ? ??? ?? trial? origin? ?
- ???? ? ???? ??? ???? ? ? ?? ???????? ?? ??

**RWR_Target ??? ?? ??:**
- ??: id, cm, x, y, z (????)
- ??: id, cm, angle_deg, distance_cm (???, origin ?? ????)

**?? ?? (??? ??):**
```
              90� (?, ??? ??)
               |
  135�(??)   |   45�(??)
               |
180� (?) -----+------ 0� (?)
               |
  225�(??)   |   315�(??)
               |
             270� (?)
```
- 0? = ??? ??? ??, ??? ???? ??
- 0~90: ??? ? / 90~180: ?? ? / 180~270: ?? ? / 270~360: ??? ?

**Unity ?? ??:**
```
targetX = originX + (distance_cm / 100f) * cos(angle_rad)
targetZ = originZ + (distance_cm / 100f) * sin(angle_rad)
targetY = originY  (?????? ?? ???)
```

**Session ???:** startX/Y/Z ??, calibration origin? ???

### RWR Trial Timeline (finalized)

```
[MoveToStart]
  Screen: "Place your hand at the start position"
  Wait until MCP enters start zone.

[HoldInStart]
  MCP must remain inside start zone for holdDuration (e.g. 0.5 s).
  ? Zone exit ? reset to MoveToStart.

[ShowDirection]  ? kinematic recording begins here
  Screen: direction cue text (large) + audio cue.
    - "REST"                (rest trial)
    - "REACH"               (reaching trial)
    - "REACH & GRASP"       (reaching+grasping trial)
  For REST: also shows "Please stay at the start position"
  Wait goDelay seconds.
  ? Zone exit ? reset to MoveToStart.

[Go]
  Audio: go cue.
  Screen: "GO"
  TTL scheduled: fires at Go + offset_ms.
  Executing phase begins.
  False start reset no longer applies after this point.

[Executing]  (fixed duration N seconds, configured in game settings)
  REST trial:        subject keeps hand still. Zone exit = BAD result at end.
  REACH trial:       subject reaches toward physical target.
  REACH+GRASP trial: subject reaches and pinch-grasps physical target.
  Kinematics recorded (Leap CSV).
  TTL fires at scheduled time.
  At t = N seconds: evaluate outcome, then show feedback for 1 s.

  Outcome evaluation at N seconds:
    REST:         MCP inside start zone ? GOOD, else ? BAD
    REACH:        MCP inside target zone ? GOOD, else ? BAD
    REACH+GRASP:  index tip AND thumb tip both inside target zone ? GOOD, else ? BAD

[Feedback]  (1 second)
  Screen: "GOOD" or "BAD"

[Done]
  Kinematics saved.
  Screen returns to MoveToStart.
  Next trial begins only when subject re-enters start zone (user-paced ITI).
  ? No fixed inter-trial interval. Supports patient populations who need to rest.
```

### RWR UI / Debug Requirements

- **Debug overlay** (troubleshooting panel):
  - Same info as other task modes (state, hand position, TTL status, trial index, etc.)
  - Toggled on/off via keyboard shortcut (e.g. F1 or D key).
  - Positioned on one side of the screen, not interfering with main cues.
- **Leap Motion connection indicator**:
  - Always visible, top-right corner.
  - Shows connected / disconnected / tracking status in real time.

### RWR Scene Set

Separate scenes required (same pattern as R / RG):
- `RWR_Target`  � virtual target position & radius per target ID (same concept as R/RG)
- `RWR_Session` � trial list (see fields below)
- `RWR_Game`    � main game scene

### RWR Session Table Fields

| Field | Notes |
|---|---|
| trial | trial index |
| targetId | links to RWR_Target entry (supports multiple targets / start positions) |
| startX, startY, startZ | start zone position (set per trial, not in Target scene) |
| instruction | 0=REST / 1=REACH / 2=REACH+GRASP |
| hand | 0 / 1 / 2 (same as R/RG) |
| ttl1 | TTL offset in ms from Go |

Fields removed vs R/RG: `vf` (not needed in real-world task)

### Key Design Decisions (finalized)

| Item | Decision |
|---|---|
| Trial order | Pre-defined in session table |
| Direction types | REST, REACH, REACH+GRASP |
| Direction cue | Large text + audio |
| MCP reference joint | Same as R/RG |
| False start rule | Zone exit before Go ? full reset to MoveToStart |
| False start after Go | No reset; trial continues for full N seconds |
| REST outcome | MCP inside start zone at t=N ? GOOD |
| REACH outcome | MCP inside target zone at t=N ? GOOD |
| REACH+GRASP outcome | Index tip AND thumb tip inside target zone at t=N ? GOOD |
| Feedback duration | 1 second |
| ITI | User-paced; next trial starts when subject re-enters start zone |
| Kinematic recording start | ShowDirection phase |
| TTL timing | Go + offset_ms (same as R/RG) |
| Execution duration N | Single value in game settings (not per-trial) |

## 2026-04-03 (Session 2)

### ?? ??? ?? ??

**RWR ?? ?? 1? ?? (?/????/??)**

1. **RuntimeConfigStore** � `RwrTargetSpec`, `RwrTargets`, `rwrCalibrationOrigin`, `rwrCalibrated`, `TrialSpec.instruction` ??
2. **TargetRow_RWR.cs / TargetRow_RWR.prefab** � angle_deg, distance_cm ??, PosZ ??
3. **TargetTableController_RWR.cs** � RWR? target ??? ???? (CSV ??/??, RuntimeConfigStore ??)
4. **SessionRow_RWR.cs / SessionRow_RWR.prefab** � instruction ?? ?? (VF ? instruction)
5. **SessionTableController_RWR.cs** � RWR? session ??? ????
6. **GameSessionController_RWR.cs** � ?????? ?? ?? (SPACE ? MCP ?? ? origin), polar ? world ?? ??
7. **TrialGameController_RWR.cs** � ?? ?? ?? trial ?? (MoveToStart ? HoldInStart ? ShowDirection ? Go ? Executing ? Feedback ? Done)
8. **RWR_Target / RWR_Session / RWR_Game ?** ?? ? ??
9. **MainMenu** � RealWorldReaching ?? ??, `CanStartGame()` ?? ?? (RWR? ? RwrTargets ??)
10. **RWR_Target ? ?? ??** � `WireRwrTargetButtons.cs` ??? ????? AddRow/DeleteRow/Save/Load ?? ? TargetTableController_RWR ??
11. **RWR_Game ?** � `CalibrationText` UI ???? ?? ? GameSessionController_RWR? ??

### ?? ??

- MainMenu?? Real World Reaching ?? ? Target/Session ?? ? ?? ???? ?? ??
- ?????? ?? ?? ?? (SPACE ???)
- ?? ??? ? ??? ?????? ? ?? ??/?? ?? ?? ?? ?? ??

### ?? ??: RWR ?????? ??? (??)

**????:**
- ?????? ? (SPACE ??? ?): ?? ??? ??? ?
- SPACE ?? ?????? ?? ? (trial ?? ?):
  - ?? ??(start zone) ??? ??
  - ??? ???? **?? ?? ??** ??? ?? (?? ??? ?? ? ???)
  - ?? ?? ??? ?
  - ???? ?? ? **?? ??? ???** trial ??
- Trial ?? ?: ?, ????, ?? ??? ?? ??
- **?? ??**: trial ??? ? ???? ?? ? ? ??? ? (F? ?? ??)
- Instruction? text box? ?? ?? ?? ??

**?? ?? (?? ????):**
1. `GameSessionController_RWR`? calibration ?? ? preview ?? ?? (`SessionState.Preview`)
2. Preview ????: ? ??? ON, start zone ?(sphere) ??, ?? ?(sphere)? ??
3. "Start Trials" ?? (UI) ??? preview ???? ??? trial ??
4. F? (?? ?? ??)?? preview ???? ??
5. `TrialGameController_RWR`? `showMcpCursor` ? ?? cursor ??? ?? ??

**??:**
- ? ???? Leap Motion XR ????? hand renderer? ?? ?? ?? ? ?? � ?? ??
- ?? ??/??? ??? sphere prefab?? ??? ???? ? ??? ?
- Start zone ???? target ???? `TrialGameController_RWR`? ?? ? ???

## 2026-04-06

### ??? ??

**RWR_Game ? ?? ? experimenting ?? ??**

1. **Capsule Hands ???** � ?????? ? ? ?? ?? ??, F?? ??
2. **StartSphere ????** � ? ?? ? ???? ??? SPACE ? calibration origin? ??
3. **TargetSphere preview** � SPACE ? Inspector ???(angle, distance)?? ?? ?? ??
4. **StartSphere ?? ??** � `GameSessionController_RWR.startSphereRadius` ??, `TrialGameController_RWR.startRadius` ??? ?? (`public StartRadius` property ??)
5. **??? sphere** � `TrialGameController_RWR`? ?? ??? alpha 0.3?? ??, `SetTransparentColor()` ?? ??
6. **Experimenting ??** � `forceExperimentingMode` ????? store ??? ???? Inspector ???? ?? ?? trial
7. **Experimenting ?? Inspector ??**: instruction (Range 0-2), hand mode (Range 0-2), TTL offset, angle, distance (?? ??)
8. **Stage indicator ???** � ???? "CALIBRATION" / "TRIAL" ??
9. **? ??? ? SPACE ??** � `leapInput.hasIndexJointData` ??, "Hand not detected!" ??? ??
10. **SHIFT+SPACE ???????** � MoveToStart ??? ?? ??, origin/sphere/trial ?? ????
11. **Experimenting ??? ??** � `experimentLogging` ????, `experimentDataPath` Inspector ??, `ExperimentData` ?? ?? ??
12. **RWR_Game ?? DataPathManager, RuntimeConfigStore ???? ??** � ?? ? ?? ??? ?????
13. **Trial ??? ?? ??** � ?: "Place your hand at the start position" ? ?: "+" ? holdDuration ?: "Instruction: Reach/Rest/Reach & Grasp" ? "GO" ? "GOOD/BAD"
14. **GOOD/BAD ?? ???** � targetGoodColor(??), targetBadColor(??) Inspector ?? ??
15. **Index Mcp ?? ??** � TrialGameController Inspector?? MCPVisual ? MCP? ?? (zone ?? ???)

### ?? ??
- RWR_Game ?? ?? ? experimenting ??? ?? ?? trial ??
- MainMenu ?? ? session/target ?? ?? trial ??
- ??? ?? ?? ?? (ExperimentData ??? session_NNN/01,02/*.csv ??)
- Capsule Hands? ? ???, ??? sphere? zone ??

### ?? ?? (?? ??, ??)

16. **F? 3?? visual ??** � all ? no hands ? none ? all ??, visualMode 0/1/2
17. **?? ?? ?? ?? ?? ??** � zone ??/?? ?? trial ?? ? MCP/sphere? ?? ???? ?? ??
    - `cursorsOverrideHidden` / `spheresOverrideHidden` ??? ??, `SetCursors()` / `InitTrial()`?? ??? ??
    - `ApplyVisualMode()`?? sphere ?? ?? ?? `trialController.SetSpheresVisible()` ??
18. **?? ?? ?? ? instruction ??? ??** � visualMode=2? ? font size 75? (Inspector?? normal/hidden size ?? ??)
19. **TTL ?? ??** � ??? t= ?? Go ?? ?? ms ??
20. **RWR2 ?/???? ?? ??** � RWR? ??? ??? ????? ?? ?? ??

### ?? ?? (?? ??, ?? � MainMenu troubleshooting)

21. **MainMenu ?? ? store ??? ?? ?? ??**
    - `RuntimeConfigStore`? `launchedFromMainMenu` ??? ??
    - `MainMenu.cs`?? ?? ? ?? ?? ??? `true`? ??
    - `GameSessionController_RWR/RWR2`: ???? true? ??? store ??, ?? ? ?? ??(false? ???)
    - ?? ? ?? ??? `forceExperimentingMode` ?? store ?? ? experimenting ??
    - ?? ??? store ???? ???? ?? ?? ? `forceExperimentingMode` ??? experimenting ?? ??

### ?? ?? (2026-04-06 ?? ??)
- MainMenu ?? ?? ?? ?? ?? ??
- RWR_Game ?? ?? (experimenting ??) ?? ?? ??
- F? 3?? ??, ???? ?? sphere/cursor/?? ?? ??

### ?? ?? ??
- RWR ?? (Virtual Reaching Task) � MCP cursor? ??, instruction ???
- FireTtlPulse() ???? ?? (COM5 ? TriggerBox, ?? ?? ?? ??)
- Debug overlay / Leap ?? ?? indicator (Phase 5)

## 2026-04-09

### ??? ??

**Experimenting ?? ??? ?? ??**

1. **exp_session ?? ??** � experimenting ??? `exp_session_NNN/`, MainMenu ??? `session_NNN/`?? ?? ??
2. **exp_session ???** � 24?? ??? ?? exp_session? ??? ??? ??, ?? ??? ? ?? ??
3. **MainMenu session? ?? ?? ??** � trial ?? ??? ? session ??? ?? (??? ??)
4. **experimenting trial index ?? ??** � ?? index=0 ?? ?? ??, `experimentTrialCounter`? 1?? ?? ??
5. **trialCounterText** � experimenting ???? "EXP 1", "EXP 2"... ? ??

**TTL "none" ?? ??**

6. **RuntimeConfigStore.ttl1 ?? ??** � `float` ? `string`
7. **RwrTrialConfig? ttlEnabled ??? ??** � `bool ttlEnabled` + `float ttlOffsetMs` ??
8. **?? ??** � `ParseTtlEnabled()` / `ParseTtlOffset()` ??
   - ?? ? `ttlEnabled=true`, ?? ?? offset?? ??
   - `"none"` ?? ?? ? `ttlEnabled=false`, fire ? ?
9. **TTL scheduling** � `ttlPending = ttlEnabled`? ??, none?? ShowDirection/EnterGo ???? ??? ???
10. **Debug overlay** � `ttlEnabled=false`? ? "TTL: none" ??
11. **RWR, RWR2 ?? ??**

### ??? � Session ??? ttl1 ?

| ? | ?? |
|---|---|
| `150` | Go ?? +150 ms ? fire |
| `-50` | Go ?? -50 ms ? fire |
| `none` | TTL ?? (no-TMS trial) |

### LabChart / EMG ?? ?? ?? (2026-04-09)

- Unity `FireTtlPulse()` ? COM5 ? TriggerBox ? PowerLab input ?? ? LabChart ?? ??
- TMS ?? ???? EMG? ?? ??? (MEP ?? ??)
- PowerLab? TriggerBox ??? hardware input ??? ?? ????, **????? marker ?? ???**
- Unity ?? ? ?: `SerialPort`? COM5? hardware pulse ? ?? ?? ?
- ?? ???: TriggerBox? ???? ??? ???? (TriggerBoxTestIO.exe? ?? ??)

### ?? ?? ??
- FireTtlPulse() ???? ?? ?? (COM5 ? TriggerBox, TriggerBoxTestIO.exe? ???? ?? ??)
- RWR ?? (Virtual Reaching Task)
- Debug overlay / Leap ?? ?? indicator

### TTL ???? ?? ?? (2026-04-09)

- `Precise_Game_VP.py` (Python, ?? ?? ????) TTL ???? ??
  - COM3, baud 115200, channel 1 = `bytes([1])`, pulse 100ms, reset = `bytes([0])`, ?? flush
- Unity ??: `System.IO.Ports.SerialPort` via `.NET Framework` API level
  - `TrialGameController_RWR` / `RWR2` ? `OpenTtlPort()` / `CloseTtlPort()` / `ResetTtlAfterDelay()` ??
  - `FireTtlPulse()`: channel byte high ? `BaseStream.Flush()` ? 100ms ? coroutine?? reset ? flush
  - Inspector ??: `Ttl Com Port` (COM5), `Ttl Channel` (1), `Ttl Pulse Duration Ms` (100)
- `.NET Standard 2.1` ? `.NET Framework` ?? ?? (Project Settings ? Player ? Api Compatibility Level)
- **?? TriggerBox?? ?? ??? ? ??? ?**

---

## ?? ?? ???? (2026-04-06)

### RWR2 � Real World Reaching (?? ?? ??)

**??:** ????? ?? physical object(????, ??)? ??? ??, Unity ??? ?? object? ??? ??. ?? reaching task ?? ? kinematic + TMS? ??? ??? ??.

**?? ??:**
- ????? ?? ??? ??
- ??? ?? zone? ??? ??? ??
- instruction ??? (REST / REACH / REACH+GRASP) ??
- ?? ?? ?? ?? � ?? visual ?? instruction? ?? ? ??
- ?? RWR2 ?/????? ?? ??

---

### RWR (??, ?? ??) � Virtual Reaching Task

**??:** ????? ?? ?? ?? ??? ??? visual feedback?? ???? reaching? ??.

**?? ??:**
- ? ?? ??? ?? (?? ? ? ??)
- MCP cursor? ??? ?? ? ????? ? dot? ?? ??? ??
- instruction ?? � ??? reaching task
- hold duration ?? ?? reaching (?? cue ??? ??)
- ?? ?? ??: MCP? target zone ?? = ??
- ?? RWR ?/????? ? ???? ?? ??

---

### ?? ???? � Virtual Reach-to-Grasp / Interactive Object Task

**??:** VR ???? ?? object? ??? interact?? task.

**?? task:** "??? A?? B? ???"

**?? ?? (?? ???):**
- MCP zone-in ?? ??, ???(pinch/grasp)?? object? ??? ?? ?? ??
- ??: index tip + thumb tip? object ???? pinch gesture ??
- ??: object? hand position? ?? ??
- ??: pinch ?? ? object ??
- ?? ??: object? B zone ?? ??? ?
- Ultraleap? pinch strength API ?? tip distance ???? ?? ??

**??:** Leap Motion? `PinchStrength`, `GrabStrength` API ?? � ?? controller ?? ??


---

## 2026-04-09

### LabChart FRO Per-Trial Parameter Update â€” Completed

**Goal:** Automatically set LabChart Fast Response Output (FRO) Output1 and Output2 delays from Unity before each trial's Go cue.

**Architecture:**
- LabChart's TriggerBox receives one TTL pulse at the Go cue.
- FRO fires Output1 and Output2 at configurable delays after that pulse.
- Unity sets those delays by calling LabChart's PlayMessage() COM function with a modified binary message before each trial.

**Files changed:**
- Assets/Script/LabChartFro.cs â€” new component; loads VBS template, patches binary message in-place, fixes checksum, sends via cscript.exe
- Assets/Script/TrialGameController_RWR.cs / TrialGameController_RWR2.cs â€” added [SerializeField] LabChartFro froController and StartCoroutine(froController.PrepareOutputs(...)) in ShowDirectionCue()
- Assets/Script/SessionTableController_RWR.cs â€” added 	tl2_offset column to CSV save/load, SnapshotToCache, and RestoreFromCache
- Assets/Prefab/SessionRow_RWR.prefab â€” added TTL2Offset InputField between TTL and Instruction columns
- Assets/Scenes/RWR_Game.unity / RWR2_Game.unity â€” added LabChartFro GameObject, wired to TrialGameController

**Key design decisions:**
- COM interop via VBScript process (cscript.exe) â€” Unity/Mono's COM support is incomplete; Marshal.GetActiveObject throws native exceptions that bypass managed catch blocks.
- VBS uses GetObject(,"ADIChart.Application") (not CreateObject) â€” connects to running LabChart, does not launch a new instance.
- Binary message patched in-place (same-length byte replacement + checksum at bytes 20-23). LabChart rejects messages with wrong checksums.
- Output1 is fixed at 50ms in the template (never replaced per trial). Only Output2 changes, using a 6-char placeholder "0.0525".
- FormatDelay() outputs 6-char strings in "0.0000" format (e.g. "0.0525"), matching the placeholder length exactly.

**Template file (Source/Firstt.vbs):**
- Recorded with LabChart macro: Output1 = 0.05s (50ms), Output2 = 0.0525s (52.5ms placeholder).
- LabChart encodes these as PulseDelay = 0.05 and PulseDelay = 0.0525 in UTF-16LE inside the PlayMessage hex blob.
- Total message: 6706 bytes. Checksum at bytes 20-23: FC 7E 0A 00.

**Flow per trial:**
1. ShowDirectionCue() calls StartCoroutine(froController.PrepareOutputs(ttlOffsetMs, ttl2OffsetMs))
2. Template message sent -> LabChart FRO restored to known-good state
3. Wait 50ms
4. Modified message sent -> Output2 delay updated to ttl1 + ttl2_offset
5. At Go cue: TriggerBox fires TTL -> LabChart FRO outputs both pulses at configured delays

**Status: Working as of 2026-04-09.**

---

### Next Session â€” Test Automation of Output Delays

**Goal:** Verify that per-trial Output2 delay changes in Unity actually affect LabChart FRO hardware output timing.

**What to test:**
1. Build a session with varied 	tl1 and 	tl2_offset values across trials (e.g. 50ms/2.5ms, 50ms/5ms, 50ms/10ms).
2. In LabChart, confirm the FRO Output2 pulse arrives at the correct delay after the TTL trigger.
3. Check Unity console for [LabChartFro] FRO armed logs to confirm correct ms values are being sent.

**If Output1 also needs to vary per trial (currently it does NOT):**
- Re-record Source/Firstt.vbs with Output1 set to a 6-char encoded value (e.g. 0.0501s â€” LabChart encodes 0.05 as "0.05" not "0.0500", so use a value with 4 non-zero decimal places).
- Add a TEMPLATE_DELAY_OUT1 constant and a second ReplaceOccurrence call in BuildModifiedMessage for Output1.
- Update PrepareOutputs to replace both outputs instead of just Output2.

---

## 2026-04-13

### RWR UI Overhaul + In-Game Info Overlay

**Changes made:**

**Session/Target Table UI:**
- Session table columns redesigned: #, hand, target, start_r, hold, wait, move, ts, cs, inst
- AddTrial() defaults: hand=1, target=1, start_r=15cm, hold=0.5s, wait=3s, move=3s, inst=1
- AddTarget() defaults: angle=90deg, dist=20cm, diameter=15cm
- Fixed duplicate trial # ordering bug (SetSiblingIndex sync after insert)
- Added Randomize button (Fisher-Yates shuffle)
- Added Reset button (clear all trials)
- Tooltip system on column headers for both Session and Target scenes

**GameSessionController_RWR.cs:**
- Per-trial timing fields added: startRadiusCm, holdDuration, waitForGo, executingDuration
- After SHIFT+SPACE recalibration: calibrationText GO hidden, Recalibrated. status shown 2s, trial restarts from #1
- StatusMessage public getter added for overlay

**LabChartStatusChecker.cs (new):**
- Polls LabChart8 process every 2s on background thread
- Exposes IsOpen (recording detection abandoned — COM/window title both inaccessible)

**GameInfoOverlay.cs (new):**
- Tab key toggle (configurable)
- Shows: Leap Motion tracking status, LabChart open/closed, current status message, trial info
- Font size 24pt, panel 420x480
- LabChartStatusChecker GO + GameInfoOverlay GO added and wired in RWR_Game scene

**Calibration screen fix:**
- calibrationText GO disabled after SPACE calibration (was text=empty but panel still visible)

**Next session:**
- Verify recalibration flow in-game (SHIFT+SPACE during MoveToStart)
- Test Tab overlay during actual trial run
- Consider RWR2 equivalent wiring (LabChartStatusChecker, GameInfoOverlay)

---

## 2026-04-27

### LabChartFro.cs 전면 개편 — DoublePulse / SinglePulse / NoPulse 3-모드 지원

**배경:**
- 이전 구현은 VBS 템플릿이 하나(Firstt.vbs)이고 Output1이 고정(0.05s)이었음.
- TMS 실험에서 단일 자극(SinglePulse)과 이중 자극(DoublePulse, conditioning + test) 구분이 필요해짐.
- TTL이 비활성화된 trial에서 이전 trial의 FRO 설정이 남아 있는 문제도 해결 필요.

**변경 사항 (LabChartFro.cs):**
- VBS 템플릿 3종으로 분리:
  - `DoublePulse.vbs` — Output1 `0.0501` placeholder, Output2 `0.0525` placeholder, 둘 다 활성
  - `SinglePulse.vbs` — Output1 `0.0501` placeholder, Output2 비활성(On=0)
  - `NoPulse.vbs` — Output1/Output2 모두 비활성 (TTL disabled trial용 FRO 초기화)
- Output1도 가변 처리: 플레이스홀더 `0.0501`을 per-trial 값으로 패치
- `BuildModifiedDouble()` / `BuildModifiedSingle()` 메서드 분리
- `PrepareNoPulse()` 추가 — ttlEnabled=false일 때 FRO 상태 초기화
- `CancelPrepare()` 추가 — trial 중간에 다음 trial이 시작될 경우 진행 중인 코루틴 취소
- TTL 타이밍 모델 명시: TTL 트리거는 Go 큐 1초 전에 발사; FRO 딜레이는 해당 트리거 기준 절대값(ms)

**호출 규약 (PrepareOutputs):**
```
out1AbsoluteMs = 1000 + ttl1       (ms, TTL trigger → Output1)
out2AbsoluteMs = 1000 + ttl1 + ttl2  (ms, TTL trigger → Output2)
doublePulse = (ttl2 != 0)
```

### SessionTableController_RWR.cs — CSV 컬럼명 변경

- 이전: `#,hand,target,start_r,hold,wait,move,ttl1,ttl2_offset,inst`
- 이후: `#,hand,target,start_r,hold,wait,move,ts,cs,inst`
- `ts` = Testing Stimulus offset (ms), `cs` = Conditioning Stimulus offset (ms)
- SnapshotToCache / RestoreFromCache / RowToCsv / LoadCsv 모두 새 컬럼명으로 업데이트

### Editor 도구 추가 (Assets/Editor/)

Scene 컬럼명 변경 이후 기존 씬 헤더를 일괄 수정하고 툴팁을 점검하기 위한 Editor 전용 스크립트:
- `SessionSceneRWRFixer.cs` (1~6) — RWR Session 씬 HeaderRow 컬럼명 일괄 수정 (TTL→TS, startX/Y/Z→Hold/Wait 등), Move/CS 열 추가, 순서 정렬
- `TargetSceneRWRFixer.cs` — RWR Target 씬 HeaderRow에 TooltipUI + HeaderTooltipTrigger 자동 연결
- `TooltipDebugCheck.cs` — 현재 씬의 TooltipUI, HeaderRow, GraphicRaycaster, EventSystem 연결 상태 콘솔 출력

### 다음 세션 할 일

- [x] DoublePulse / SinglePulse / NoPulse VBS 템플릿 파일 완료 (`Source/DoublePulse.vbs`, `Source/SinglePulse.vbs`, `Source/NoPulse.vbs`) — 플레이스홀더 검증 완료
- [ ] PrepareOutputs → TrialGameController_RWR/RWR2에서 새 시그니처(out1Abs, out2Abs, doublePulse)로 호출부 업데이트
- [ ] SessionRow_RWR prefab에서 TTL1/TTL2Offset 필드를 TS/CS 필드로 교체 확인
- [ ] RWR_Game 씬 LabChartFro 컴포넌트에 3개 VBS 경로 연결
- [ ] Tab 오버레이 실제 실험 환경에서 동작 검증
- [ ] SHIFT+SPACE 재보정 플로우 in-game 검증

---

## 2026-04-27 (Session 2)

### Inspector UI 개선 — ExperimentTtlEntry 라벨 변경

**변경 내용 (Assets/Editor/ExperimentTtlEntryDrawer.cs 신규):**
- TTL 목록 Inspector 레이블 전면 변경:
  - `TTL Enabled` 체크박스 → `No Pulse` (로직 반전: 체크 = 비활성)
  - `TS` 필드 → `TS (Output2)` (Testing Stimulus)
  - `CS delay (-)` 필드 → `CS delay (-) (Output1)` (Conditioning Stimulus)
- `No Pulse` 체크 시 TS/CS 입력 필드 자동 비활성화 (`EditorGUI.DisabledScope`)

**Out1/Out2 라벨 오류 수정 (GameSessionController_RWR.cs):**
- 코드 주석 및 로그에서 Output1/Output2가 반대로 표기되어 있던 것 수정
  - Output1 = Conditioning Stimulus (CS), Output2 = Testing Stimulus (TS)

---

### PowerLab FRO 크래시 원인 규명 및 해결

**크래시 재현 조건:**
- SinglePulse 또는 NoPulse trial 이후 DoublePulse trial 실행 시 즉시 PowerLab 크래시
- 방향: ON→OFF 전환은 안전 / OFF→ON 전환은 크래시

**근본 원인 (하드웨어 펌웨어 제약):**
- PowerLab FRO 채널을 LabChart 샘플링 **도중** 에 OFF→ON 전환하면 하드웨어 타이밍 회로를 재초기화해야 하는데,
  이 과정이 이미 실행 중인 샘플링 DMA/인터럽트 시스템과 충돌 → 펌웨어 예외 발생
- ON→OFF는 타이밍 회로를 건드릴 필요 없이 단순 비활성화 플래그이므로 안전
- 이것은 LabChart PowerLab **펌웨어의 근본적 제약**으로 소프트웨어로 우회 불가

**시도한 WarmUp 접근법 (실패):**
- `EnterFeedback()`에서 매 trial 끝마다 DoublePulse 템플릿을 전송해 Output1을 ON 상태로 복원하는 방식
- 결론: WarmUp 자체도 OFF→ON COM 호출이므로 동일한 크래시 유발

**채택한 해결책 — Block Design:**
- TTL 목록을 단방향 전환(ON→OFF)만 발생하도록 순서 구성:
  ```
  DoublePulse block (Output1=ON, Output2=ON)
      ↓ (ON→OFF 안전)
  SinglePulse block (Output1=OFF, Output2=ON)
      ↓ (ON→OFF 안전)
  NoPulse block (Output1=OFF, Output2=OFF)
  ```
- **8 trial 제한:** wrap-around 시 NoPulse→DoublePulse (OFF→ON) 크래시 발생
- **8 trial 초과 시 운영 절차:**
  1. LabChart 녹화 중지
  2. FRO 설정에서 Output1 수동 On
  3. 녹화 재시작 → 하드웨어 재초기화로 이후 DoublePulse 안전하게 시작 가능

**코드 정리:**
- `TrialGameController_RWR.cs` `EnterFeedback()` 에서 WarmUp 블록 제거
- `LabChartFro.cs` 에서 `WarmUpOutputs()` 메서드 완전 제거

---

## 2026-04-27 (Session 3)

### Session CSV ts/cs 파싱 버그 수정

**버그:** `ts=0, cs=.` (SinglePulse 의도) 인 trial이 NoPulse로 동작하는 문제
- 원인: `ttlEnabled = ParseTtlEnabled(tr.ts) && ParseTtlEnabled(tr.cs)` — cs가 비어있으면 false가 되어 ttlEnabled=false
- 수정: `ttlEnabled = ParseTtlEnabled(tr.ts)` — ts만으로 결정. cs 비어있음 = SinglePulse (doublePulse=false)

**cs 양수 자동 음수 처리:**
- cs에 양수가 입력되면 자동으로 음수로 변환 (`-Mathf.Abs(...)`)
- Conditioning Stimulus는 항상 Testing Stimulus 이전에 발사되어야 하므로 반드시 0 이하

### LabChart 페이지 매칭 문제 조사 및 결론

**문제:** NoPulse trial에서 LabChart FRO output이 없으니 Comment 페이지가 생성되지 않아 trial 번호와 페이지 번호 불일치

**시도한 해결책 — AppendComment:**
- `LabChartFro.AppendCommentCoroutine()` 추가: NoPulse trial에서 TTL 발사 시점에 LabChart COM으로 텍스트 comment 기록
- `Task.Run` (백그라운드 스레드) → coroutine (`yield return null` 후 동기 실행)으로 교체 — 백그라운드 스레드에서 `Debug.Log` 미출력 문제 해결
- NoPulse trial에서만 호출 (`!ttlEnabled`) — FRO 작동 trial에는 FRO comment가 이미 자동으로 남으므로 중복 방지

**결론:**
- `AppendComment`는 텍스트 주석을 기록하지만 LabChart Comment mode의 페이지는 생성하지 않음 (페이지는 FRO 발사 이벤트로만 생성됨)
- **Event mode 사용으로 결론:** TriggerBox TTL이 모든 trial에서 발사되므로 Event mode 페이지 수 = 전체 trial 수로 항상 일치
- AppendComment는 NoPulse trial에 텍스트 라벨만 남기는 용도로 유지

### 실제 TMS 하드웨어 연결 계획 확정

```
Output1 (Conditioning) → Conditioning TMS 입력
Output2 (Testing)      → Testing TMS 입력
Testing TMS feedback   → Trigger 채널 (LabChart Event 페이지 트리거)
Conditioning TMS feedback → Channel 6
```

- SinglePulse trial: Output1 비활성 → Conditioning TMS 신호 없음 → Channel 6 flat (정상)
- NoPulse trial: 두 TMS 모두 신호 없음 → Event mode 페이지는 TriggerBox로 생성

---

## 2026-04-27 (Session 4)

### In-game UI 개선

**Instruction 텍스트 변경:**
- `ShowDirectionCue()`: `+` (fixation cross) + 아래 회색 이탈릭 direction cue (`<color=#888888><size=65%><i>Reach</i></size></color>`)
- Go cue (non-REST): `+`와 instruction 사라지고 초록색 "GO" 표시 (`<color=#00CC00>GO</color>`)
- Go cue (REST): `+`와 "Rest" 그대로 유지 (Go 신호 없음)
- GOOD → 초록색, BAD → 빨간색 TMP rich text 적용
- "Place your hand..." → "Put your hand on home position"으로 변경
- REST 이탈 경고 → "REST — please return to home position"으로 변경

**P키 일시정지 기능 추가 (`TrialGameController_RWR.cs`):**
- P 누르면 노란색 "PAUSE" 표시, 상태머신 freeze (손 감지 무관)
- P 다시 누르면 일시정지 전 텍스트 복원
- `readyTime`, `goTime`, `ttlPlannedTime` 모두 일시정지 시간만큼 shift → 타이밍 보정

**Tab overlay 기본 숨김 (`GameInfoOverlay.cs`):**
- `showOnStart` 필드 제거, 항상 숨긴 상태로 시작
- Tab 눌러 열고 닫기

### Experiment TTL List 확장 (`GameSessionController_RWR.cs`)

**`ExperimentTtlEntry`에 per-trial 전체 설정 추가:**
- `instruction` (0=REST / 1=REACH / 2=REACH+GRASP) — 드롭다운
- `handMode` (0=Left / 1=Right / 2=Either) — 드롭다운
- `angleDeg` — 타겟 방향 (도)
- `distanceCm` — 타겟 거리 (cm)

**전역 `experimentInstruction`, `experimentHandMode` 제거** → 모든 설정이 entry별로 독립
**`ExperimentTtlEntryDrawer.cs`** 7행으로 확장 (Instruction/Hand/Angle/Distance 추가)

### Calibration 양손 지원

- `ShowCalibrationScreen()`에서 `leapInput.allowEitherHand = true` 설정
- 왼손/오른손 모두 calibration 가능
- Calibration 안내 텍스트 "either hand" 반영
- Trial 시작 후 각 trial의 hand mode 설정으로 정상 리셋

### MCP 커서 손 감지 연동

- `McpInStart()`, `McpInTarget()`에 `leapInput.hasIndexJointData` 조건 추가
- 손이 사라지면 zone 판정 false → ghost 커서로 인한 hold 오작동 방지
- HoldInStart 중 트래킹 끊기면 hold 리셋 (false start는 발생 안 함)

### 데이터 저장 좌표계 변경 (`TrialDataLogger.cs`)

- **Global → 상대좌표 (start 기준 +1m offset)**
- 공식: `saved = global - startPos + (1,1,1)`
- 시작지점 = 항상 `(1,1,1)`, 가동범위(1m 이내) 내에서 항상 양수 보장
- 타겟 위치도 동일 변환하여 헤더에 기록
- 헤더에 `coordinate_system: relative_to_start_plus_1m` 명시

## 2026-04-29

### GRIP 게임 모드 구현

#### 신규 스크립트
- `Assets/Script/TrialGameController_GRIP.cs` — RWR 기반으로 재작성. zone 진입 감지 대신 Physical Hands 물리 인터랙션으로 cylinder를 실제로 잡는 방식. 상태머신: MoveToStart → HoldInStart → ShowDirection → WaitForGo → Executing → Feedback → TrialDone.
- `Assets/Script/GameSessionController_GRIP.cs` — RWR 구조 유지, 타겟 radius 전달 및 targetSphere 숨김 처리 추가.
- `Assets/Script/SessionTableController_GRIP.cs`, `TargetTableController_GRIP.cs`, `SessionRow_GRIP.cs`, `TargetRow_GRIP.cs` — 세션/타겟 CSV UI.
- `Assets/Script/GripTargetListener.cs` — `IPhysicalHandGrab` 직접 구현. `PhysicalHandEvents`의 UnityEvent가 runtime AddComponent 시 null인 문제를 우회하기 위해 C# Action 델리게이트 사용. `isGrabbing` 플래그로 매 프레임 호출되는 `OnHandGrab`에서 grab enter 이벤트 합성.
- `Assets/Editor/GripGameSceneFixer.cs` — Tools 메뉴 Editor 스크립트. GRIP_Game 씬에서 RWR 컴포넌트를 GRIP 컴포넌트로 교체하고 직렬화 필드 복사.

#### 핵심 설계
- 타겟: Inspector 할당 sphere 제거 → 코드로 Cylinder 프리미티브 런타임 spawn (`SpawnCylinder`)
- Cylinder: `Rigidbody(isKinematic=false, useGravity=false)` — GrabHelper가 non-kinematic만 감지하므로
- 성공 판정: cylinder를 잡으면 즉시 GOOD (early exit), 타이머 만료 시 BAD
- REST 조건: `AllFingersInStart()` (MCP + 검지끝 + 엄지끝 셋 모두 시작지점 내)

#### 버그 수정
- `PhysicalHandEvents.onGrabEnter` NullReferenceException → `GripTargetListener`로 교체
- Cylinder kinematic 설정으로 grab 감지 안 되던 문제 → `isKinematic=false`로 수정
- Camera 각도 top-down이라 cylinder가 원처럼 보이던 문제 → 45도로 조정 (사용자 직접 수정)
- RWR 시대 targetSphere가 trial 중에도 보이던 문제 → `ConfirmCalibration()`에서 숨김 처리

#### 시작지점 조건 강화 (이번 세션)
- `McpInStart()` → `AllFingersInStart()`로 교체: MCP 하나만이 아닌 MCP + 검지끝 + 엄지끝 셋 모두 `startRadius` 내에 있어야 진입 인정
- 시작지점 스케일 → `Vector3.one * (startRadius * 2f)` (구 형태, 균등 스케일)

#### MCP for Unity 세팅
- `com.coplaydev.unity-mcp` v9.3.1 → v9.6.8 업그레이드 (Python 서버 v9.6.8과 버전 맞춤)
- `~/.claude/settings.json`에 `mcp-for-unity.exe` 서버 등록

### 다음 작업
- ~~GRIP_Game 씬의 `startSphere` 오브젝트를 Sphere 프리미티브로 교체~~ ✓ 완료
- `startRadius` Inspector 값 조정 (세 포인트 동시 진입 조건에 맞게 넉넉하게, 약 5~6cm 권장)
- 실제 플레이 테스트로 AllFingersInStart 조건 체감 확인 및 튜닝

## 2026-07-24

### RWR — 4/27 이후 미검증 체크리스트 점검 + 재보정 버그 수정

3개월 공백 후 RWR 진행 상황 점검. 4/27 Session 1에 남겨뒀던 TODO 5개 중 코드/에셋으로 확인 가능한 3개는 이미 반영되어 있었음을 확인:
- `PrepareOutputs(out1Abs, out2Abs, doublePulse)` 새 시그니처로 RWR/RWR2/GRIP 모두 정상 호출 중
- `SessionRow_RWR` prefab에 TS/CS 필드 정상 반영 (TTL1/TTL2Offset 잔재 없음)
- `RWR_Game` 씬 LabChartFro에 DoublePulse/SinglePulse/NoPulse 3개 VBS 경로 정상 연결, 파일도 `Source/`에 존재

남은 2개는 실제 플레이로 검증:
- Tab 오버레이 — 정상 동작 확인
- SHIFT+SPACE 재보정 — **버그 발견**: experimentingMode에서 재보정 시 현재 trial(예: 6)에 머물러야 하는데 다음 trial(7)로 건너뛰는 문제. 원인은 `RunExperimentTrial()`이 호출될 때마다 `experimentTrialCounter`를 무조건 증가시키는데, `Recalibrate()`가 이 카운터를 보정하지 않고 그대로 재호출했기 때문. `GameSessionController_RWR.cs` `Recalibrate()`에서 `RunExperimentTrial()` 호출 직전 `experimentTrialCounter--`를 추가해 증가분을 상쇄하도록 수정. 실제 플레이로 trial 6 유지 확인 완료.
- `RWR2.cs`도 같은 구조를 갖고 있지만 (experimentingMode 브랜치가 아예 trial을 재시작하지 않는 등) 별도 문제가 있어 보임 — 이번 세션 범위 아님, RWR2 사용 시 별도 점검 필요.

### PowerLab FRO OFF→ON 크래시 — Preload+Bounce 검증 스크립트 작성

**배경:** 2026-04-27에 근본 원인을 규명해둔 크래시(SP/NoPulse → DoublePulse, 즉 Output1 OFF→ON 전환 시 샘플링 도중 PowerLab 펌웨어 크래시)에 대해, 사용자가 별도 프로젝트(TMSviewer)에서 발견한 "Preload + Auto-bounce" 해법(`reference/FROmodeissue.md`)이 적용 가능한지 검토.

- FROmodeissue.md의 해법은 `StartSampling` 직전에 SP+DP `PlayMessage`를 미리 보내고, Stop/Start bounce 전후로 두 번 반복하는 방식. 단, 그 앱은 프로그램이 `StartSampling`도 직접 호출하는 구조였고, 이 프로젝트는 LabChart 녹화를 실험자가 수동으로 시작하는 구조라 그대로 이식 불가.
- 방향 결정: Unity가 COM으로 `StartSampling`/`StopSampling`까지 직접 제어하는 쪽으로 간다 (나중에 `GameSessionController_RWR.cs`의 "LabChart open — confirm recording manually" 수동 단계를 대체할 계획).
- `reference/App_design_extracted`, `TMSDataCollection_RT_extracted`(같은 연구실의 과거 MATLAB 앱) 소스에서 실제 COM 메서드명이 `Doc.StartSampling` / `Doc.StopSampling` (인자 없음)임을 확인.
- **RWR 프로덕션 코드에 반영하기 전에** 실제 하드웨어로 먼저 검증하기로 함 — FROmodeissue.md의 버그(최초 Start 직후 미초기화)와 이 프로젝트의 버그(샘플링 도중 OFF→ON 전환)가 같은 메커니즘인지 확신이 없기 때문.

**작성한 파일 — `Source/FroInitTest.vbs` (독립 실행, Unity 미관여):**
- `cscript //Nologo "Source\FroInitTest.vbs"`로 직접 실행하는 순수 VBS. `Source/SinglePulse.vbs`/`DoublePulse.vbs`/`NoPulse.vbs`에서 `PlayMessage` hex를 정규식으로 직접 추출해서 재사용 (하드코딩 안 함).
- Phase 1: Preload+Bounce 초기화 시퀀스 (SP→DP→Start→500ms→Stop→SP→DP→Start)
- Phase 2: 이 프로젝트의 정확한 크래시 조건 재현 (DP→SP→NoPulse→DP, 마지막이 OFF→ON)
- Phase 3: Bounce 추가 없이 사이클 2회 더 반복 — 첫 OFF→ON만 우연히 통과하는지, 반복해도 안정적인지 확인
- 각 호출 전/후로 로그를 찍어서, 스크립트가 멈추거나 에러가 나면 정확히 어느 호출에서 크래시했는지 특정 가능하게 설계.
- LabChart 미실행 상태에서 문법 오류 없이 정상 동작(연결 실패 에러까지 정상 출력) 확인 완료. 실제 하드웨어 테스트는 미실시.

**추가 아이디어 (통합 단계에서 반영):** `LabChartStatusChecker.IsOpen`이 false일 때(LabChart 자체가 안 켜져 있을 때) 같은 키로 LabChart 실행파일을 `Process.Start()`로 띄워주는 것도 같이 추가하면 좋겠음 — 지금은 LabChart가 실행 중이어야만 COM 연결이 되므로.

### PowerLab FRO 실제 하드웨어 테스트 — 결과 정리 (같은 세션, 실시간 진행)

**테스트 1 — `FroInitTest.vbs` (Preload+Bounce, 트리거 없음, `DP→SP→NoPulse→DP`):**
- 결과: **크래시.** Phase 1 완료 5초 후(트리거 없이 순수 `PlayMessage` 재구성만으로) `DP (trial D)` — 즉 NoPulse(둘 다 off)→DoublePulse(둘 다 on) 전환에서 크래시.
- 증거: 콘솔 로그는 전부 `-> OK`로 찍혔지만(COM 호출 자체는 동기 응답을 바로 줌), LabChart 창 캡처에서 차트 데이터가 딱 5초 지점에서 멈춰있고 창 제목이 `Idle`, `▶ Start` 버튼 상태였음 — 즉 실제 하드웨어 크래시는 COM 호출 이후 비동기로 발생하며 스크립트의 `Err.Number` 체크로는 감지 불가.
- 사용자 확인: 실제로 LabChart에 팝업 다이얼로그가 떴었음 (프로그램 자체는 살아있는 상태).

**테스트 2 — `FroSpDpToggleTest.vbs` (Preload+Bounce, NoPulse 없이 SP↔DP만 6회 토글, Output2는 항상 유지):**
- 결과: **크래시 없음.** 에러 없이 끝까지 정상 동작.
- 시사점: 크래시는 "Output1만의 OFF→ON"이 아니라, **Output2까지 포함해 둘 다 꺼진 상태(NoPulse)에서 둘 다 켜진 상태(DP)로 가는 전환**에서만 발생하는 것으로 보임.

**테스트 3 — `FroTtlTest.ps1` (Preload+Bounce + 매 arm마다 실제 TTL 트리거 발사, `DP→SP→NoPulse→DP` 1사이클):**
- COM5 시리얼로 `TrialGameController_RWR.cs`의 `FireTtlPulse()`와 동일한 프로토콜(115200bps, channel 1 = 0x01, 100ms 유지 후 리셋)을 재현.
- 결과: **크래시 없음.** NoPulse→DP 전환도 실제 트리거 포함 시 통과.
- 시사점: 트리거 없이 `PlayMessage`만 반복 재구성하는 것(테스트 1)과, 매번 실제로 발화까지 시키는 것(테스트 3)이 다른 결과를 냄 — "실제 트리거 발화가 껴 있으면 안전하다"는 가설이 유력해짐. (실제 프로덕션은 `ttlEnabled` 무관하게 매 trial TTL이 항상 발사되므로, 이 조건과 일치.)

**테스트 4 — `FroTtlCycleTest.ps1` (동일 조건으로 5사이클 반복, 총 20회 arm+발화):**
- 1차 실행: **크래시 없음.** 5사이클 전부 정상 완료, LabChart 반응 정상. (부작용: 사이클마다 COM5 포트를 새로 열고/닫아서 Windows USB 연결/해제 알림음이 반복됨 — 프로덕션 코드는 포트를 세션 시작 시 한 번만 열어서 유지하므로 실제로는 발생 안 할 현상.)
- 2차 실행 (LabChart를 새로 켜거나 녹화를 재시작하지 않고, 1차 실행이 끝난 바로 그 상태에서 동일 스크립트를 다시 실행): **크래시.** `"LabChart Action Failed — There is a problem communicating with the PowerLab 8/35. The USB cable may have been unplugged..."` 팝업 + LabChart 상태 표시 깜빡임. OK를 눌러도 정상 복귀 안 되고 **재시작(USB 재연결 등) 필요한 수준의 통신 단절.**

**추가 발견 — `reference/FROmodeissue2.md` 검토:**
- 사용자가 TMSviewer 프로젝트의 실제 동작 코드(`realtime_viewer.py`, `labchart_client.py`)를 정리한 문서를 추가로 확인.
- 핵심: 그 레퍼런스 앱에는 **NoPulse 개념이 아예 없음.** SP도 Output2(Testing)는 항상 켜진 채 Output1(Conditioning)만 토글하는 구조 — Output2를 꺼본 적이 자체가 없어서, 이 프로젝트만의 "둘 다 꺼짐(NoPulse)" 상태에서 복귀하는 시나리오는 애초에 검증된 적 없는 케이스였음.
- 또한 그 코드는 `doc.StartSampling(0, False, 0)`처럼 명시적 인자를 넘김 (이 프로젝트는 그동안 인자 없이 `StartSampling()`만 호출).

**테스트 5 — `FroIsolationTest.ps1` (`StartSampling(0, False, 0)`로 변경, `NoPulse→SP`만 단독 테스트 + `NoPulse→DP`):**
- 1차 실행: 크래시 없음 (5단계 전부 통과, `NoPulse→SP`와 `NoPulse→DP` 둘 다 성공).
- 같은 LabChart 세션에 재실행(2차): 크래시 없음. (이전엔 세션당 Bounce 2회면 USB 통신이 끊어졌었는데, 이번엔 안 끊어짐 — 인자 변경이 도움 됐을 가능성 시사.)
- 다만 이 시점에선 반복 횟수가 적어(각 1~2회) 우연일 가능성을 배제 못함.

**테스트 6 — `FroTtlCycleTest.ps1`에 `StartSampling(0, False, 0)` 반영 후 15사이클 재실행:**
- 결과: **여전히 크래시.** 이번엔 cycle 2의 `DP (trial D) *** OFF→ON ***`에서 (콘솔 로그는 `-> OK`로 찍힌 후 비동기로) 크래시.
- 결론: `StartSampling` 인자 변경은 근본 해결책이 아니었음 — 테스트 5의 성공은 우연이었던 것으로 판단.

**테스트 7 — `FroSpDpCycleTest.ps1` (NoPulse는 아예 안 쓰고 `SP↔DP`만 30회 반복 토글, 동일한 Preload+Bounce + 실제 트리거 + 2.5s/4s 간격):**
- 결과: **크래시 없음. 30/30 토글 전부 통과.**
- 사용자 확인: 오리지널 프로덕션 버그(Preload 없이 수동 재생하던 시절)는 사실 `SP→DP`와 `NoPulse→DP` **둘 다** 크래시났었음 (WORKLOG 2026-04-27: "SinglePulse 또는 NoPulse 이후 DoublePulse 크래시"). 즉 이번 30회 성공은 우연이 아니라 **원래 크래시의 절반(SP→DP)을 Preload+Bounce가 실제로 고쳤다**는 의미가 큼.

**최종 결론 (이번 세션 종료 시점):**
- **Preload+Bounce(세션 시작 시 1회) + 매 trial 실제 TTL 발사** 조합은 **SP↔DP 전환(Output2/Testing 상시 유지)을 확실히 안정화시킴** — 30회 반복 무크래시로 검증됨, 원래 프로덕션 크래시의 절반을 실질적으로 해결.
- 그러나 **NoPulse가 낀 전환(NoPulse↔SP, NoPulse↔DP)은 여전히 간헐적으로 크래시함** — Preload+Bounce, 실제 트리거, 느린 타이밍(2.5s/4s), `StartSampling(0, False, 0)` 인자 변경 등 시도한 어떤 조합으로도 확실히 못 막음. 어떤 실행은 1사이클 만에, 어떤 실행은 5사이클을 버티다 나기도 해서 재현 조건이 확률적인 것으로 보임.
- 세션당 Bounce를 2회 이상 실행하면(재시작 없이 스크립트 재실행 등) USB 통신 자체가 끊기는 더 심각한 크래시가 날 수 있다는 것도 확인됨 (`StartSampling` 인자 변경 후엔 재현 안 됐지만 우연일 수 있어 신뢰하지 말 것).

**다음 세션 — Unity 통합 방향 (결정됨):**

NoPulse 관련 크래시는 회피 가능하다고 판단 — **NoPulse trial들을 세션 앞/뒤로 따로 묶어서 하나의 독립된 블록으로 배치**하면 (SP/DP 블록과 섞이지 않게), NoPulse↔SP/DP 간 위험한 전환 자체가 거의 발생하지 않음. 기존 Block Design과 비슷하지만, SP/DP끼리는 이제 자유 순서 가능하므로 제약이 훨씬 완화됨.

**TODO (다음 세션):**
1. `LabChartFro.cs`에 `StartSampling`/`StopSampling` COM 래퍼 추가 (`Source/FroTtlCycleTest.ps1`의 `Invoke-StartSampling`/`Invoke-StopSampling` 패턴 참고 — `SendPlayMessage()`와 동일하게 cscript.exe+임시 VBS 방식으로).
2. Preload+Bounce 코루틴 추가 (SP→300ms→DP→300ms→StartSampling→500ms→StopSampling→300ms→SP→300ms→DP→300ms→StartSampling, 세션당 **정확히 1회만** 실행되도록 플래그로 가드 — 실수로 중복 실행되면 USB 통신이 끊어질 수 있음, 오늘 확인함).
3. `GameSessionController_RWR.cs`의 calibration 화면 — 지금 `"LabChart open — confirm recording manually"`([GameSessionController_RWR.cs:217](Assets/Script/GameSessionController_RWR.cs#L217))로 되어 있는 부분을, 키 입력 시 위 Preload+Bounce를 실행해서 녹화까지 자동으로 시작하도록 교체.
4. `LabChartStatusChecker.IsOpen`이 false일 때(LabChart 자체가 꺼져 있을 때) 같은 키로 LabChart 실행파일을 `Process.Start()`로 띄워주는 것도 함께 추가.
5. 세션/타겟 테이블 쪽에서 **NoPulse(TTL 비활성) trial들을 SP/DP trial과 섞이지 않게 블록으로 묶어 배치**하는 규칙 반영 (UI 경고나 자동 정렬 등 — 구체적 방식은 다음 세션에 논의).
6. 통합 후 실제 하드웨어로 재검증: SP/DP 자유 순서 + NoPulse 블록 분리 조건에서 크래시 없는지 확인.

**참고용으로 남겨둔 테스트 스크립트** (`Source/Fro*.vbs`, `Source/Fro*.ps1`) — 통합 후 회귀 확인이나 재검증에 재사용 가능.

## 2026-07-27

### LabChart 녹화 자동 시작(Preload+Bounce) Unity 통합 + 재시작 안전성 검증

지난 세션 TODO 1~4 반영. 상세 설계는 `C:\Users\Jisung Yuk\.claude\plans\cached-riding-lighthouse.md` 참고.

**`LabChartFro.cs`:**
- `SendStartSampling()`/`SendStopSampling()` 추가 — 기존 `SendPlayMessage()`와 동일한 cscript.exe+임시 VBS 패턴(`RunLabChartCommand()`로 공통화).
- `ArmSessionRecording(bool force = false)` 코루틴 추가 — Preload+Bounce 전체 시퀀스(SP→300ms→DP→300ms→Start→500ms→Stop→300ms→SP→300ms→DP→300ms→Start). `hasArmed` 가드로 세션당 1회만 자동 실행되고, `force=true`로 재실행 가능.
- `IsArming`/`IsRecording` public 상태 노출. `IsRecording`은 **Unity가 명령을 성공적으로 보냈다는 로컬 플래그**일 뿐 LabChart 실시간 상태를 읽어오는 게 아님(폴링 방식은 크래시 위험 대비 이번엔 도입 안 하기로 결정).

**`GameSessionController_RWR.cs`:**
- `froController` 필드 추가. **R키** = 녹화 시작(`TryArmRecording()`→`ArmSessionRecording()`), **T키** = 녹화 중지(`TryStopRecording()`→`StopRecording()`, `StopSampling`만 호출·재구성 없음). 둘 다 세션 상태 무관(Calibrating/Running 둘 다) 동작 — "R = 녹화 중인지 확인, T = 멈췄는지 확인"으로 통일.
- calibration 화면에 LabChart 상태 4단계 표시: OFF / ON—idling(press R) / Arming / ON—Recording(T to stop). **SPACE는 Recording 전까지 차단.**
- **가드**: `IsRecording`이 true인 동안은 R을 또 눌러도 무시됨(활성 세션에 Bounce 중복 실행 방지, 아래 재시작 검증 참고) — T로 먼저 멈춰야 R이 다시 먹힘. `LabChartStatusChecker.IsOpen`이 false로 바뀌면(LabChart 자체가 꺼짐) `IsArming`/`IsRecording`도 자동으로 리셋됨(`ResetIfLabChartClosed()`, 매 프레임 체크) — 나중에 LabChart를 다시 켜면 깨끗한 idling 상태부터 시작.
- 처음엔 "SHIFT+R = 강제 재시작"으로 R과 분리해서 설계했었는데, 사용자 피드백으로 R 하나로 통일 — T로 명시적으로 멈추면 `IsRecording=false`가 정확히 반영되니, 그 다음 R을 누르는 게 이미 안전한 시나리오(아래 Test3와 동일)라 굳이 별도 키가 필요 없었음.

**재시작 안전성 검증 — 왜 SHIFT+R이 "재구성 없이 StartSampling만"이 아니라 "전체 Preload+Bounce 재실행"인지:**

배경: "LabChart가 멈추면 R로 다시 시작할 수 있어야 한다"는 요구사항이 나와서, 어떤 재시작 방식이 안전한지 3가지를 실제 하드웨어로 비교 검증함 (모두 Preload+Bounce 최초 1회 → SP/DP 5회 정상 동작 확인 → `StopSampling()`으로 정지 재현 → 5초 대기 → 재시작 → SP/DP 재토글 순서):

| 테스트 | 재시작 방식 | 결과 |
|---|---|---|
| `FroRestartTest.ps1` | `StartSampling()`만 단독 재호출 (프리로드 없음) | **크래시** — 재시작 후 SP/DP 재토글 3회차(SP→DP)에서 에러 |
| `FroRestartTest2.ps1` | SP+DP 프리로드 1회(반복 없음) 후 `StartSampling()` | 크래시 없음 |
| `FroRestartTest3.ps1` | 전체 Preload+Bounce 재실행 (최초 arm과 동일한 풀 시퀀스) | 크래시 없음 |

결론: **재시작 시에는 최소한 프리로드(SP+DP 재전송)가 필요** — `StartSampling`만 단독으로는 이전에 안전했던 SP↔DP까지 다시 깨짐. 전체 Preload+Bounce 재실행(Test3)도 안전한 것으로 나와, 최초 arm과 완전히 같은 `ArmSessionRecording()`을 재시작에도 그대로 재사용하기로 함 — 가드 조건을 `hasArmed`(1회성 플래그) 대신 `IsRecording`으로 바꿔서, "지금 recording 중이 아니면 언제든 다시 arm 가능"하게 만듦.

**중요한 전제 조건**: 이 안전성은 **"명시적으로 Stop한 뒤 재실행"**한 경우에만 확인된 것. 오늘 초반에 확인했던 "이미 샘플링 중인 세션에 대고 또 Bounce"(Stop 없이 곧바로 재실행)는 여전히 USB 통신이 끊기는 크래시 조건 그대로임. 그래서 `IsRecording`이 true인 동안은 R을 눌러도 무시되도록 가드가 남아있음 — T로 명시적으로 멈춰서 `IsRecording=false`가 된 뒤에만 R이 다시 동작. LabChart가 사용자도 모르게 멈추거나 크래시난 경우엔 Unity가 자동으로 알아채지 못하므로(폴링 도입 안 하기로 결정), 그럴 땐 T를 먼저 눌러 상태를 강제로 동기화한 뒤 R을 누르면 됨.

**미해결 — 다음 세션:**
- Unity Editor Play 모드로 R/T/4단계 상태 텍스트/SPACE 차단 동작 실제 확인 (배치모드 컴파일 체크는 이미 열려있는 Editor와 충돌해서 못 함 — 코드 리뷰로 대체함, 컴파일 에러는 없어 보임).
- `RWR_Game.unity`에서 `GameSessionController_RWR`의 `Fro Controller` 슬롯에 씬의 `LabChartFro` 오브젝트 수동 연결 필요.
- NoPulse trial을 SP/DP와 안 섞이게 블록으로 배치하는 것(TODO 5)은 사용자가 세션 테이블 구성 시 직접 지키기로 함 — 코드 강제는 안 하기로.
- LabChart 자동 실행(TODO 4)은 아직 미착수.

### ESC 통합 일시정지 시스템 (같은 세션, 이어서)

**배경:** 지금까지 RWR_Game에서 ESC를 누르면 `EscapeToMenu.cs`가 확인 없이 즉시 MainMenu로 나가버렸음 — 실수로 누르면 되돌릴 수 없고, LabChart는 별도 프로그램이라 녹화가 그대로 방치되고, 진행 중이던 trial 데이터도 제대로 안 저장될 수 있었음. 이걸 진짜 일시정지로 바꿈.

**변경 사항:**
- `TrialGameController_RWR.cs`: 기존 P키 일시정지(`TogglePause()` — trial 내부 타이머만 freeze)를 제거하고, `public void SetPaused(bool value)`로 교체 — 외부(GameSessionController_RWR)에서 호출하는 형태로 전환. 타이밍 시프트 로직(`readyTime`/`goTime`/`ttlPlannedTime`)은 그대로 유지, `instructionText`에 "PAUSE" 직접 쓰던 부분은 제거(오버레이가 대신함).
- 신규 `Assets/Script/PauseOverlay.cs`: `GameInfoOverlay.cs`와 동일한 런타임 UI 생성 패턴(`FindObjectOfType<Canvas>()`) — 화면 전체를 덮는 반투명 검은 패널 + 중앙 텍스트. 씬 수동 연결 불필요, `GameSessionController_RWR.Start()`에서 `gameObject.AddComponent<PauseOverlay>()`로 생성.
- `GameSessionController_RWR.cs`: **ESC** = 일시정지 진입(트라이얼 타이머 freeze + Leap 트래킹(`LeapFingerInput.enabled = false`, Ultraleap 서비스 자체는 안 건드림) + LabChart 정지(`StopRecording()`) + 오버레이 표시) → 일시정지 중 **ESC 재입력** = 복귀 시작(`ResumeSequence()` 코루틴: `ArmSessionRecording()`으로 LabChart 재초기화 대기 후에야 트래킹/트라이얼 타이머/오버레이 동시 복귀 — 약 2.5초 소요) → 일시정지 중 **Q** = MainMenu로 이동 (기존 `EscapeToMenu.cs`와 동일 동작 인라인).
- 일시정지 중엔 R/T/F/SPACE/SHIFT+SPACE 등 나머지 입력 전부 무시.

**미해결 — 다음 세션:**
- `RWR_Game.unity`에서 기존 `EscapeToMenu` 컴포넌트를 비활성화(또는 제거) 필요 — 지금 그대로 두면 ESC를 누르는 순간 새 일시정지 로직보다 먼저(또는 같이) 즉시 메뉴 이동이 발생해 충돌함. `EscapeToMenu.cs` 스크립트 자체는 `GRIP_Game.unity`에서도 쓰므로 스크립트는 안 건드리고 RWR_Game 씬의 컴포넌트 인스턴스만 끄면 됨.
- Unity Editor Play 모드로 ESC 일시정지/복귀/Q 종료, 트래킹 정지·재개, LabChart 정지·재시작 전체 플로우 실제 확인 필요.

**같은 세션 추가 개선 — 오버레이 박스화 + 실시간 상태:** 실제 플레이 테스트로 ESC/Q 플로우 자체는 정상 확인됨. 이어서 `PauseOverlay.cs`를 전체화면 딤 배경(alpha 0.6) + 중앙 고정 박스(680x460, 어두운 배경 패널)로 개선하고, `GameSessionController_RWR.cs`에 `RefreshPauseOverlayText()` 추가 — calibration 화면(`UpdateCalibrationStatus()`)과 같은 형식의 Leap Motion/LabChart 상태 줄을 매 프레임 갱신해서 오버레이 안에 표시. 일시정지 중엔 "Leap Motion: OFF"/"LabChart: idling"(우리가 의도적으로 끈 상태 그대로), 복귀(Resuming) 중엔 LabChart의 Arming→Recording 전환이 실시간으로 보임.
- Leap Motion 손 감지 표시 문구를 calibration 화면과 동일하게 "Hand detected/No hand detected"로 통일. 단, 일시정지 중엔 `LeapFingerInput`이 비활성화돼 있어 `hasIndexJointData`가 그 순간 값에 고정되는 문제 발견 → `leapInput.leapProvider.CurrentFrame.Hands.Count`를 직접 읽어서(우리 코드 비활성화와 무관하게) 실시간으로 반영하도록 수정.

### 단일 손 trial에서 반대쪽 손 모델 강제 숨김

**배경:** Leap Motion이 가끔 오른손을 왼손으로 오인해서, 실제 진행 중인 손(오른손) 모델에 반대쪽(왼손) 모델이 겹쳐 보이는 시각적 혼란이 있었음. 실제 interaction은 이미 `handMode`로 선택된 손(`LeapFingerInput.cs`가 `indexMcp`/`finger1`/`finger2`를 그 손 데이터로만 갱신 — `TrialGameController_RWR.cs`의 `McpInStart()`/`McpInTarget()`은 이 값만 봄)에만 국한되어 있어 게임 진행엔 문제 없었지만, 사용자 입장에서 헷갈릴 수 있어 시각적으로도 정리.

**확인한 구조:** 씬의 `capsuleHands`는 Ultraleap "Capsule Hands" 프리팹(`Library/PackageCache/com.ultraleap.tracking@7.2.0/.../Capsule Hands.prefab`)이며, 그 아래 `Capsule Hand Left`/`Capsule Hand Right` 두 개의 자식 오브젝트로 구성됨.

**구현 (`GameSessionController_RWR.cs`):** `ApplyHandVisualRestriction(int handMode)` 추가 — 처음엔 `capsuleHands.transform.Find("Capsule Hand Left"/"Capsule Hand Right")`로 이름 매칭 시도했으나, 실제 테스트에서 반대쪽 손 모델이 trial 경계에서야 숨겨지고 그마저도 손이 트래킹 범위를 벗어났다 돌아오면 다시 나타나는 문제 발견. `Leap.HandModelBase.Handedness`(Chirality) 속성으로 찾도록 바꿨지만(이름 매칭보다 안정적) 같은 증상 재현 — Ultraleap 패키지 전체(`Library/PackageCache/com.ultraleap.tracking@7.2.0`)를 검색해도 `SetActive(true)`를 호출하는 코드가 전혀 없어서 정확한 재활성화 원인은 못 찾음.

**최종 채택 — 매 프레임 강제 재적용:** 원인을 못 찾았으므로, `LateUpdate()`(다른 모든 스크립트의 Update 이후 실행)에서 매 프레임 `EnforceHandVisualRestriction()`을 호출해 선택 안 된 손을 계속 다시 `SetActive(false)` — 무엇이 언제 다시 켜든 그 프레임 안에서 우리가 마지막에 덮어씀. `activeSelf`가 이미 원하는 상태와 같으면 `SetActive` 호출 자체를 스킵하도록 가드 추가(상태 변화 없는 대부분의 프레임에서는 사실상 공짜). `currentHandModeRestriction` 필드에 현재 trial의 handMode를 저장해두고 매 프레임 그 값 기준으로 판단하므로, trial마다 손이 바뀌어도(Right→Left 등) 다음 프레임부터 바로 정확히 반영됨.

**실제 테스트 결과 (사용자 확인):** 잘 동작함 — 반대쪽 손이 트래킹 범위를 벗어났다 돌아와도 아주 잠깐(한 프레임 수준) 깜빡였다가 바로 사라짐. 근본 원인 재활성화 지점은 못 찾았지만 육안상 문제없는 수준으로 해결됨.

### 곁가지 (RWR 아님, 미착수)
GRIP 양손 협력 과제 확장, Leap Motion Polhemus 기반 공간 캘리브레이션, 게임 모드 범용 블루프린트 문서 — `bimanualplan.md`, `CALIBRATION_PLAN.md`, `GAME_MODE_BLUEPRINT.md` 참고. 코드 구현은 아직 없음.
