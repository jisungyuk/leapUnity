# RWR TTL/FRO Timing Chain — Where Jitter Lives, and How the Displayed Number Is Computed

> Written up after a session spent tracing exactly why the debug overlay's TTL number
> looked confusing, and where in the pipeline timing precision actually comes from.
> Covers: the full signal chain, which stages are jittery vs. hardware-precise, and how
> `TrialGameController_RWR.cs` / `TrialDataLogger.cs` compute and log the number that
> actually matters.

## 1. The full chain, in order

```
Unity ShowDirectionCue()          Unity Update()              TriggerBox        PowerLab FRO
────────────────────────          ─────────────────           ──────────        ────────────
compute ttlPlannedTime      →     detect Time.time         →  receives TTL  →   detects trigger
= goPlanned - 500ms                >= ttlPlannedTime            byte over        (level crossing
(goPlanned = readyTime            → FireTtlPulse()              COM5 serial      on its Input
 + goDelay, a FUTURE                 → SerialPort.Write()                        channel)
 estimate made well                  → ttlFiredTime =                                │
 before Go)                            Time.time                                     │
                                                                                       ▼
Unity Update_WaitForGo()                                                    waits (500+ts) ms
detect Time.time - readyTime                                                for Testing (TS,
  >= goDelay                                                                 Output2), and
  → goTime = Time.time                                                      (500+ts+cs) ms
  → EnterGo() (visual/audio                                                 for Conditioning
    "GO" cue for the subject                                                (CS, Output1) —
    — nothing sent to                                                       cs can be negative
    PowerLab here)                                                          (CS before TS) or
                                                                              positive (CS after)
```

Key point: **there is exactly one hand-off from Unity to the hardware** — the TTL trigger
pulse, sent over COM5 ~500ms before Go. The "Go cue" itself never talks to PowerLab; it's
a Unity-only visual/audio event for the subject. Everything PowerLab does after the
trigger (waiting the configured TS/CS delays, firing Output1/Output2) happens on its own
internal hardware timer, with no further communication from Unity.

Relevant code:
- `TrialGameController_RWR.cs` `ShowDirectionCue()` — computes `ttlPlannedTime = goPlanned - 0.5f`,
  arms the FRO delays via `LabChartFro.PrepareOutputs()/PrepareNoPulse()`.
- `TrialGameController_RWR.cs` `Update()` — `if (ttlPending && !ttlFired && Time.time >= ttlPlannedTime) FireTtlPulse();`
- `TrialGameController_RWR.cs` `FireTtlPulse()` — sets `ttlFiredTime = Time.time`, then writes to the COM5 `SerialPort`.
- `TrialGameController_RWR.cs` `Update_WaitForGo()` — `if (Time.time - readyTime >= goDelay) { goTime = Time.time; ...; EnterGo(); }`
- `LabChartFro.cs` `PrepareOutputs()` — arms Output1 (Conditioning) at `500 + ts + cs` ms and Output2 (Testing) at `500 + ts` ms from the trigger, via `PlayMessage` COM calls to LabChart.

## 2. Where jitter actually is (and isn't)

| Stage | Jittery? | Why |
|---|---|---|
| Unity detects "Go time reached" (`Update_WaitForGo`) | **Yes** | Checked once per `Update()` frame; can only be detected up to ~1 frame *after* the ideal instant. |
| Unity detects "fire TTL now" (`Update()`) | **Yes** | Same reason — independent frame-polling check, its own up-to-~1-frame delay. |
| Serial write latency (`SerialPort.Write`/`Flush`) | Small, untracked | `ttlFiredTime` is stamped in C# *before* the write call executes, so the actual electrical pulse goes out slightly later than the logged timestamp. Not part of the debug number at all — a hidden, presumably-small extra offset. |
| TriggerBox reaction (serial byte → TTL output) | Negligible | Purpose-built trigger hardware, fast and consistent. |
| PowerLab FRO delay (trigger detection → Output1/Output2 firing) | **No — hardware timer** | Once PowerLab's input detects the trigger's level crossing, the `500+ts` / `500+ts+cs` ms wait is counted by its own internal clock, independent of Unity's frame rate entirely. This is the "TS vs CS spacing is accurate" the whole discussion was trying to confirm — it is. |

So: the two Unity `Update()`-loop polling checks are the dominant, *combined* source of
jitter; everything downstream of the trigger being detected is hardware-timed and precise.

### Why "TTL: fired (X ms from Go)" was misleading

The trigger is *designed* to always fire 500ms before Go, regardless of the configured
`ts`/`cs`. So `(ttlFiredTime - goTime) * 1000` always reads ~-500ms no matter what `ts` is
set to — it only reflects the fixed 500ms offset (plus jitter), not anything about the
actual configured stimulation timing. Setting `ts = 0` does **not** make this number read
`0` — it never did, and it isn't supposed to.

## 3. What actually matters, and how it's computed now

A first attempt measured "when did the first real stimulus (TS or CS, whichever fires
first) actually land, relative to Go" — trigger-to-Go time (measured, jitter included)
plus that stimulus's fixed hardware delay from the trigger. That works, but it's
ambiguous to read on its own: a trial designed for CS to fire 50ms before Go and a trial
designed to fire exactly at Go both produce numbers that mix the *designed* offset
together with the *actual* jitter — reading "55ms" doesn't tell you whether that's 5ms
of jitter on a 50ms-offset trial, or 55ms of jitter on a 0ms-offset trial, unless you
already know that trial's ts/cs design.

The fix: measure deviation from **this trial's own scheduled stimulus time** instead of
from Go cue. Since PowerLab's hardware delay (`500 + ts`, or `500 + ts + cs`) is fixed
and precise (§2 — no jitter there), the designed-offset term cancels out algebraically:

```
first-stim-vs-Go        = (ttlFiredTime - goTime)*1000 + (500 + designedOffset)
first-stim-vs-scheduled = first-stim-vs-Go - designedOffset
                         = (ttlFiredTime - goTime)*1000 + 500
```

So the useful number never needs `ttlOffsetMs`/`ttl2OffsetMs` at all — whatever a trial's
ts/cs design is, this is always pure jitter, and it still includes both jittery
Unity-side timestamps in full (nothing is lost by dropping the offset terms; they were
always going to cancel):

```csharp
// TrialGameController_RWR.cs — ComputeJitterFromScheduledMs()
if (!ttlEnabled || !ttlFired) return float.NaN;
return (ttlFiredTime - goTime) * 1000f + 500f;
```

Sanity check: `ttlFiredTime - goTime` is ideally exactly `-0.5s` (the trigger fires
500ms before Go by design, §1), so the ideal result is `0 + jitter` — "this trial's
stimulus landed exactly where it was scheduled, give or take the measured jitter" —
regardless of what ts/cs happened to be configured to.

This is shown on the debug overlay as `TTL: fired — first fire {X:F1} ms from scheduled`,
and logged per-trial to the kinematic CSV header as `# jitter_from_scheduled_ms: {X}`
(see `TrialDataLogger.NoteJitterFromScheduled()`), called right before `EndAndSave()` in
`Update_Feedback()`. The trial's actual ts/cs design is already logged separately as
`# ttl_offset_ms: {...}`, so nothing about the design is lost by switching to this framing.

One guard worth knowing about: there's a window — between the trigger firing and Go
actually happening (~500ms wide) — where `ttlFired` is already `true` but `goTime` isn't
set yet. The debug text special-cases this as `TTL: fired (awaiting Go)` rather than
computing a nonsense number from an unset `goTime`.

## 4. Rough magnitude of the jitter

Per-frame jitter scales with frame time, i.e. with whatever refresh rate Unity is
actually running at. No `Application.targetFrameRate` is set anywhere in this project,
and `QualitySettings` (currently "Ultra", `vSyncCount: 1`) syncs to the display's refresh
rate — so frame time = 1 / (monitor refresh rate).

For a single trial, the two independent per-check overshoots (`e1` for the TTL-fire
check, `e2` for the Go-detection check, each in `[0, frameTime)`) combine as
`(e1 - e2)`, so a single trial's deviation from the "ideal" value is bounded by
**± one frame time** (not two). Across many trials, `e1` and `e2` land independently
each time, so the observed spread *between* trials (worst vs. best) can be up to
**~2 frame times** peak-to-peak.

| Refresh rate | Frame time | Max single-trial deviation | Max spread across trials |
|---|---|---|---|
| 60 Hz | ≈16.7 ms | ±16.7 ms | ≈33.3 ms |
| 50 Hz | 20 ms | ±20 ms | ≈40 ms |

(These are worst-case bounds — actual observed spread in testing was much tighter, on
the order of a few ms, since both `e1` and `e2` rarely land at opposite extremes on the
same trial.)

## 5. Suggested wording for a Methods / Limitations section

Don't just quote the theoretical worst-case bound from §4 — it's a conservative upper
limit, not what actually happened in the dataset. Before writing this up, pull the
`jitter_from_scheduled_ms` value out of every trial's CSV header (`TrialDataLogger.cs`)
and report the empirical mean and SD (or range) instead — it's already pure jitter with
each trial's ts/cs design canceled out (§3), so no further adjustment is needed before
averaging across trials with different designs. Something like:

```
# quick pass, one project you already have the file layout for
grep "jitter_from_scheduled_ms" session_*/01/*.csv | cut -d: -f2
```

then compute mean/SD (or median/IQR if the distribution looks skewed) over those numbers
and use that instead of, or alongside, the theoretical bound.

**Short version (if timing precision is not a focal point of the study):**

> The Testing (TS) and Conditioning (CS) stimuli were triggered via a hardware TTL pulse
> sent by the presentation software at a fixed 500 ms interval before the Go cue; the
> PowerLab system then delivered TS and CS at fixed hardware-timed delays from that
> trigger. Because the trigger itself was scheduled by the presentation software's
> per-frame update loop (synchronized to the display refresh rate, `<F> Hz`), the
> realized trigger timing carried a small, per-trial timing jitter, measured relative to
> each trial's own scheduled time (`<MEAN> ± <SD> ms` across all trials, software-side
> only; hardware-side delay from trigger detection to stimulus delivery was not
> software-timed and negligible).

**Longer version (if precise timing is a focal point / a reviewer might push on this):**

> Stimulus timing was controlled by a two-stage pipeline. A TTL trigger pulse was sent
> from the presentation software (Unity, [version]) to the stimulator (ADInstruments
> PowerLab [model] / [FRO hardware]) 500 ms before the nominal Go cue. From that
> trigger, PowerLab's own internal timer — independent of the presentation software —
> delivered the Testing stimulus at a fixed programmed delay and the Conditioning
> stimulus at a second fixed programmed delay (which could precede or follow the Testing
> stimulus depending on condition), configured per trial. This hardware-timed interval
> is not subject to software timing error.
>
> The trigger's own onset time, however, was determined by the presentation software's
> per-frame polling loop, synchronized to the display refresh rate (`<F> Hz`, frame
> duration ≈ `<1000/F>` ms). Because both (a) the detection of the Go-cue transition and
> (b) the detection of the scheduled trigger time were each independently subject to this
> per-frame polling delay, the realized trigger time deviated from that trial's own
> scheduled time by an amount bounded by one frame duration per trial (≈`<1000/F>` ms),
> with the two independent polling delays combining to produce a trial-to-trial range of
> up to approximately two frame durations. This deviation — independent of each trial's
> stimulation design — was logged on every trial, and the empirical distribution across
> all `<N>` trials was `<MEAN> ± <SD> ms` (range: `<MIN>` to `<MAX>` ms). Delay from
> trigger detection to stimulus delivery is governed by the stimulator's internal
> hardware clock and was not subject to this software-side timing variability.

Fill in `<F>` (refresh rate actually used during data collection — confirm via Windows
display settings or `Screen.currentResolution.refreshRateRatio`, not assumed), `<N>`
(trial count), and the `<MEAN>/<SD>/<MIN>/<MAX>` from the logged
`jitter_from_scheduled_ms` values, not the theoretical bound.
