using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Leap;
using UnityEngine;

// Records per-trial data and writes two CSVs: 01 (Right), 02 (Left)
// Sampling is driven by Controller.FrameReady, which fires on the tracking service's
// own background thread at the device's native rate (~115Hz) — independent of Unity's
// Update() cadence (measured at only ~48-60Hz, well below what the Leap device can
// deliver). Positions are transformed from raw device space into world space by hand,
// mirroring LeapServiceProvider.transformFrame(), since the background thread cannot
// touch UnityEngine.Transform.
// If the provider is not a LeapServiceProvider (e.g. GRIP's HandOffsetProvider /
// FreezeProvider chain), raw frames would miss that post-processing, so sampling falls
// back to LeapProvider.OnUpdateFrame and reads the finger Transforms instead.
//
// Output follows the shared LeapDex/ReachLab format (KinematicToolBox DATA_FORMAT.md,
// format_version 1): session_NNN_YYYYMMDD/01|02/0001.csv, t_ms from trial start
// (MoveToStart), state names, cm, x=right/y=up/z=forward, fixed 33 columns with blanks
// for joints this system does not track. Internal names are unchanged; the mapping to
// output names happens only in WriteOne().
public interface ITrialStateProvider
{
    int GetStateCode();               // called on the Leap background thread
    string GetStateName(int code);    // called on the main thread when writing
}

public class TrialDataLogger : MonoBehaviour
{
    [Header("Sources")]
    [SerializeField] LeapProvider leapProvider;
    [SerializeField] Transform indexTip;
    [SerializeField] Transform thumbTip;
    [SerializeField] Transform indexMcp;

    ITrialStateProvider owner; // to read current state code
    bool isExperimenting = false; // when true, uses exp_session_NNN folders

    Controller leapController;
    LeapTransform deviceToWorld; // cached once (main thread) — plain struct, safe to read from the background thread
    LeapProvider fallbackProvider; // subscribed to OnUpdateFrame when leapController is not available

    struct Sample
    {
        public long tUsFromStart;
        public int  stateCode;
        public Vector3 idx, thb, mcp;
    }

    // Shared output format
    const int    FormatVersion = 1;
    const string SystemName    = "leapunity";
    const string Axes          = "x=right, y=up, z=forward";
    const string Origin        = "relative_to_start_plus_100cm";
    const float  MToCm         = 100f;
    static readonly string[] OutputJoints =
    {
        "idx", "thb", "mcp",
        "wrist", "wrist_lateral", "wrist_medial",
        "elbow", "elbow_lateral", "elbow_medial",
        "shoulder"
    };

    readonly List<Sample> samples = new();
    readonly object samplesLock = new();
    bool recording = false;
    bool paused = false;
    long trialStartTimestampUs = 0; // device time of MoveToStart; 0 = take it from the first frame
    long readyUsFromStart = -1;     // last ShowDirection entry
    long goUsFromStart = -1;        // last Executing entry
    long ttlFiredUsFromStart = -1;

    // Legacy callers (see the old-signature BeginTrial below) report go as Unity time
    float legacyReadyTime_s = -1f;

    // Session folder is chosen on the first write of this run and reused afterwards
    string sessionDir = null;
    int sessionIndex = 0;

    // Meta
    int trialIndex = 0;
    int handMode = 1; // 0=L,1=R,2=EITHER
    int instruction = 0; // 0=REST, 1=REACH, 2=REACH+GRASP, -1=unknown (written blank)
    bool usedLeft = false;
    int targetId = 0;
    Vector3 startPos, targetPos;
    float startRadius, targetRadius;
    float holdDuration, goDelay, moveTimeout, feedbackDuration;
    float ttlOffsetMs;
    bool outcomeGood = false;
    float jitterFromScheduledMs = float.NaN; // see TrialGameController_RWR.ComputeJitterFromScheduledMs()
    float tsMinusGoMs = float.NaN;           // see TrialGameController_RWR.ComputeStimMinusGoMs()
    float csMinusGoMs = float.NaN;
    string ttlPortName = "";                 // blank = caller did not report it (non-RWR controllers)

    CultureInfo ic = CultureInfo.InvariantCulture;

    public void SetExperimentingMode(bool value) { isExperimenting = value; }

    public void Setup(LeapProvider provider, Transform idx, Transform thb, Transform mcp, ITrialStateProvider owner)
    {
        this.leapProvider = provider;
        this.indexTip    = idx;
        this.thumbTip    = thb;
        this.indexMcp    = mcp;
        this.owner       = owner;

        Unsubscribe();

        if (leapProvider is LeapServiceProvider serviceProvider)
        {
            deviceToWorld  = new LeapTransform(serviceProvider.transform);
            leapController = serviceProvider.GetLeapController();
        }

        if (leapController != null)
        {
            leapController.FrameReady += OnLeapFrameReady;
        }
        else if (leapProvider != null)
        {
            fallbackProvider = leapProvider;
            fallbackProvider.OnUpdateFrame += OnUpdateFrame;
        }
    }

    void Unsubscribe()
    {
        if (leapController != null)
            leapController.FrameReady -= OnLeapFrameReady;
        if (fallbackProvider != null)
            fallbackProvider.OnUpdateFrame -= OnUpdateFrame;
        leapController   = null;
        fallbackProvider = null;
    }

    /// <summary>
    /// Starts recording at trial start (MoveToStart). trialStartTimestampUs is the Leap
    /// device time of that moment; pass 0 if no frame has arrived yet and the first
    /// recorded frame becomes t=0.
    /// </summary>
    public void BeginTrial(
        int trialIndex,
        int targetId,
        int handMode,
        int instruction,
        bool usedLeft,
        long trialStartTimestampUs,
        Vector3 startPos, float startRadius,
        Vector3 targetPos, float targetRadius,
        float holdDuration, float goDelay, float moveTimeout, float feedbackDuration,
        float ttlOffsetMs
    )
    {
        this.trialIndex = trialIndex;
        this.targetId = targetId;
        this.handMode = handMode;
        this.instruction = instruction;
        this.usedLeft = usedLeft;
        this.startPos = startPos;
        this.startRadius = startRadius;
        this.targetPos = targetPos;
        this.targetRadius = targetRadius;
        this.holdDuration = holdDuration;
        this.goDelay = goDelay;
        this.moveTimeout = moveTimeout;
        this.feedbackDuration = feedbackDuration;
        this.ttlOffsetMs = ttlOffsetMs;

        lock (samplesLock)
        {
            samples.Clear();
            this.trialStartTimestampUs = trialStartTimestampUs;
            readyUsFromStart = -1;
            goUsFromStart = -1;
            ttlFiredUsFromStart = -1;
        }
        legacyReadyTime_s = -1f;
        outcomeGood = false;
        jitterFromScheduledMs = float.NaN;
        tsMinusGoMs = float.NaN;
        csMinusGoMs = float.NaN;
        ttlPortName = "";
        paused = false;
        recording = true;
    }

    /// <summary>
    /// Legacy entry point for the controllers that still begin recording at ShowDirection
    /// (GRIP and the archived R / RG / RWR2 modes). Files use the shared format, but t=0 is
    /// the ready moment (ready_ms = 0) and instruction is blank. go_ms comes from
    /// SetGoTime() in Unity time.
    /// </summary>
    public void BeginTrial(
        int trialIndex,
        int targetId,
        int handMode,
        bool usedLeft,
        long readyTimestampUs,
        Vector3 startPos, float startRadius,
        Vector3 targetPos, float targetRadius,
        float holdDuration, float goDelay, float moveTimeout, float feedbackDuration,
        float ttlOffsetMs,
        float readyTime_s,
        float goTime_s
    )
    {
        BeginTrial(
            trialIndex, targetId, handMode, -1,
            usedLeft, readyTimestampUs,
            startPos, startRadius,
            targetPos, targetRadius,
            holdDuration, goDelay, moveTimeout, feedbackDuration,
            ttlOffsetMs
        );
        readyUsFromStart  = 0;
        legacyReadyTime_s = readyTime_s;
        if (goTime_s >= 0f) SetGoTime(goTime_s);
    }

    /// <summary>Legacy: go time in Unity seconds, paired with the old-signature BeginTrial.</summary>
    public void SetGoTime(float goTime_s)
    {
        if (!recording || legacyReadyTime_s < 0f) return;
        goUsFromStart = System.Math.Max(0L, (long)((goTime_s - legacyReadyTime_s) * 1_000_000f));
    }

    long UsFromStart(long deviceTimestampUs)
    {
        lock (samplesLock)
        {
            if (trialStartTimestampUs == 0) return -1;
            return System.Math.Max(0, deviceTimestampUs - trialStartTimestampUs);
        }
    }

    // Each call overwrites the previous one, so after a false start the header keeps
    // the last ShowDirection / Executing entry.
    public void NoteReady(long deviceTimestampUs)
    {
        if (!recording) return;
        readyUsFromStart = UsFromStart(deviceTimestampUs);
    }

    public void NoteGo(long deviceTimestampUs)
    {
        if (!recording) return;
        goUsFromStart = UsFromStart(deviceTimestampUs);
    }

    public void NoteTtlFired(long deviceTimestampUs)
    {
        if (!recording) return;
        ttlFiredUsFromStart = UsFromStart(deviceTimestampUs);
    }

    public void NoteOutcome(bool good)
    {
        outcomeGood = good;
    }

    /// <summary>Samples are skipped while the session is paused (ESC).</summary>
    public void SetPaused(bool value)
    {
        paused = value;
    }

    /// <summary>
    /// Records the measured timing jitter for this trial, in ms — how far the trigger
    /// actually landed from where it was scheduled, independent of that trial's ts/cs design
    /// (see TrialGameController_RWR.ComputeJitterFromScheduledMs() for why the designed
    /// offset cancels out algebraically). NaN if not applicable (e.g. a NoPulse/TTL-disabled
    /// trial).
    /// </summary>
    public void NoteJitterFromScheduled(float jitterMs)
    {
        jitterFromScheduledMs = jitterMs;
    }

    /// <summary>
    /// When Testing (TS) and Conditioning (CS) actually fired relative to Go, in ms
    /// (positive = after Go). NaN if that stimulus was not sent.
    /// </summary>
    public void NoteStimTiming(float tsMinusGoMs, float csMinusGoMs)
    {
        this.tsMinusGoMs = tsMinusGoMs;
        this.csMinusGoMs = csMinusGoMs;
    }

    /// <summary>COM port the trigger pulses went out on, or "none".</summary>
    public void NoteTtlPort(string portName)
    {
        ttlPortName = portName;
    }

    public void EndAndSave()
    {
        recording = false;
        WriteFiles();
    }

    void OnDestroy()
    {
        Unsubscribe();
    }

    // Runs on the tracking service Connection's background polling thread — do not touch
    // UnityEngine APIs here beyond reading the cached deviceToWorld struct (plain value type).
    void OnLeapFrameReady(object sender, FrameEventArgs args)
    {
        if (!recording || paused) return;

        Frame frame = args.frame;
        if (frame == null) return;

        foreach (var hand in frame.Hands)
        {
            if (hand.IsLeft != usedLeft) continue;

            AddSample(frame.Timestamp,
                deviceToWorld.TransformPoint(hand.Index.TipPosition),
                deviceToWorld.TransformPoint(hand.Thumb.TipPosition),
                deviceToWorld.TransformPoint(hand.Index.Metacarpal.NextJoint));
            break;
        }
    }

    // Fallback (main thread): provider is a post-process chain, so read the finger
    // Transforms it drives, as the original logger did.
    void OnUpdateFrame(Frame frame)
    {
        if (!recording || paused || frame == null) return;

        AddSample(frame.Timestamp,
            indexTip ? indexTip.position : Vector3.zero,
            thumbTip ? thumbTip.position : Vector3.zero,
            indexMcp ? indexMcp.position : Vector3.zero);
    }

    void AddSample(long timestampUs, Vector3 idx, Vector3 thb, Vector3 mcp)
    {
        var s = new Sample
        {
            stateCode = (owner != null ? owner.GetStateCode() : 0),
            idx = idx,
            thb = thb,
            mcp = mcp
        };
        lock (samplesLock)
        {
            if (trialStartTimestampUs == 0) trialStartTimestampUs = timestampUs;
            long tUs = timestampUs - trialStartTimestampUs;
            if (tUs < 0) return; // before trial start
            s.tUsFromStart = tUs;
            samples.Add(s);
        }
    }

    void WriteFiles()
    {
        string root = DataPathManager.Instance != null ? DataPathManager.Instance.ParticipantFolder : Application.persistentDataPath;
        if (string.IsNullOrEmpty(root)) root = Application.persistentDataPath;

        string prefix    = isExperimenting ? "exp_session_" : "session_";
        string trialFile = TrialFilename(trialIndex);

        // One session folder per run: session_NNN_YYYYMMDD. If this trial number was
        // already written in it (e.g. trials restarted after recalibration), open a new
        // session instead of overwriting.
        if (sessionDir == null || !Directory.Exists(sessionDir) || SessionContainsTrial(sessionDir, trialFile))
        {
            sessionIndex = FindLatestSessionIndex(root, prefix) + 1;
            sessionDir   = Path.Combine(root, prefix + $"{sessionIndex:000}_" + System.DateTime.Now.ToString("yyyyMMdd", ic));
        }

        string dir01 = Path.Combine(sessionDir, "01");
        string dir02 = Path.Combine(sessionDir, "02");
        Directory.CreateDirectory(dir01);
        Directory.CreateDirectory(dir02);

        // Always write both files
        string pathRight = Path.Combine(dir01, trialFile);
        string pathLeft  = Path.Combine(dir02, trialFile);

        string participantId = isExperimenting ? "" : Path.GetFileName(root.TrimEnd('/', '\\'));
        WriteOne(pathRight, false, participantId); // right file
        WriteOne(pathLeft,  true,  participantId); // left file
    }

    string F3(float v) => v.ToString("0.000", ic);
    string Ms(long us) => us < 0 ? "" : (us / 1000.0).ToString("0.000", ic);
    string Vec(Vector3 v) => $"{F3(v.x)},{F3(v.y)},{F3(v.z)}";

    // relative = (global - startPos) + 1 m, in cm  →  start position always appears as (100,100,100).
    Vector3 ToOutput(Vector3 world) => (world - startPos + Vector3.one) * MToCm;

    void WriteOne(string path, bool isLeftFile, string participantId)
    {
        System.DateTime now = System.DateTime.Now;
        string usedHand = usedLeft ? "left" : "right";
        string fileHand = isLeftFile ? "left" : "right";

        // Build header — common keys (DATA_FORMAT.md section 5), then leapUnity extras
        var sb = new StringBuilder();
        void Header(string key, string value) => sb.Append("# ").Append(key).Append(": ").Append(value).Append('\n');
        Header("format_version",   FormatVersion.ToString(ic));
        Header("system",           SystemName);
        Header("date",             now.ToString("yyyy-MM-dd", ic));
        Header("time",             now.ToString("HH:mm:ss", ic));
        Header("participant_id",   participantId);
        Header("session_index",    sessionIndex.ToString(ic));
        Header("trial_index",      trialIndex.ToString(ic));
        Header("used_hand",        usedHand);
        Header("this_file_hand",   fileHand);
        Header("instruction",      instruction < 0 ? "" : instruction.ToString(ic));
        Header("target_id",        targetId.ToString(ic));
        Header("start_pos_cm",     Vec(ToOutput(startPos)));
        Header("start_radius_cm",  F3(startRadius * MToCm));
        Header("target_pos_cm",    Vec(ToOutput(targetPos)));
        Header("target_radius_cm", F3(targetRadius * MToCm));
        Header("hold_s",           holdDuration.ToString(ic));
        Header("wait_s",           goDelay.ToString(ic));
        Header("move_s",           moveTimeout.ToString(ic));
        Header("feedback_s",       feedbackDuration.ToString(ic));
        Header("ready_ms",         Ms(readyUsFromStart));
        Header("go_ms",            Ms(goUsFromStart));
        Header("outcome",          outcomeGood ? "GOOD" : "BAD");
        Header("axes",             Axes);
        Header("origin",           Origin);
        Header("hand_mode",        handMode.ToString(ic));
        Header("ttl_offset_ms",    ttlOffsetMs.ToString(ic));
        Header("ttl_ms",           Ms(ttlFiredUsFromStart));
        // Measured timing jitter for this trial, in ms — deviation from this trial's own
        // scheduled trigger time, independent of its ts/cs design (see
        // TrialGameController_RWR.ComputeJitterFromScheduledMs()). Blank if not applicable.
        Header("jitter_from_scheduled_ms", float.IsNaN(jitterFromScheduledMs) ? "" : jitterFromScheduledMs.ToString(ic));
        // Actual stimulus time relative to Go, in ms (positive = after Go), on Unity's frame
        // clock — excludes USB-serial and display latency. Blank if that pulse was not sent.
        Header("ts_minus_go_ms", float.IsNaN(tsMinusGoMs) ? "" : tsMinusGoMs.ToString(ic));
        Header("cs_minus_go_ms", float.IsNaN(csMinusGoMs) ? "" : csMinusGoMs.ToString(ic));
        Header("ttl_port",       ttlPortName);

        // Columns: t_ms,state,ttl + xyz for every joint in OutputJoints
        sb.Append("t_ms,state,ttl");
        foreach (var j in OutputJoints)
            sb.Append(',').Append(j).Append("_x,").Append(j).Append("_y,").Append(j).Append("_z");
        sb.Append('\n');

        // The hand that was not used keeps t_ms/state/ttl with blank coordinates
        bool blank = fileHand != usedHand;
        const string blankJoint = ",,,";
        // wrist .. shoulder: not tracked by leapUnity
        string untrackedJoints = string.Concat(System.Linq.Enumerable.Repeat(blankJoint, OutputJoints.Length - 3));

        // Write rows
        // ttlMarked is local to this call so that the right and left files each
        // independently mark their own first-crossing row.
        bool ttlMarked = false;
        lock (samplesLock)
        {
            foreach (var s in samples)
            {
                // ttl flag = 1 on the first row whose time passes ttlFiredUsFromStart
                int ttl = 0;
                if (!ttlMarked && ttlFiredUsFromStart >= 0 && s.tUsFromStart >= ttlFiredUsFromStart)
                {
                    ttl = 1;
                    ttlMarked = true;
                }

                sb.Append(Ms(s.tUsFromStart)).Append(',')
                  .Append(owner != null ? owner.GetStateName(s.stateCode) : "").Append(',')
                  .Append(ttl.ToString(ic));

                if (blank)
                {
                    sb.Append(blankJoint).Append(blankJoint).Append(blankJoint);
                }
                else
                {
                    sb.Append(',').Append(Vec(ToOutput(s.idx)))
                      .Append(',').Append(Vec(ToOutput(s.thb)))
                      .Append(',').Append(Vec(ToOutput(s.mcp)));
                }
                sb.Append(untrackedJoints);
                sb.Append('\n');
            }
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false)); // no BOM
        Debug.Log($"[TrialDataLogger] Wrote {path}");
    }

    static string TrialFilename(int trialIndex) => $"{trialIndex:0000}.csv";

    // Highest NNN among <prefix>NNN_YYYYMMDD folders (also counts old <prefix>NNN folders)
    static int FindLatestSessionIndex(string root, string prefix)
    {
        int latest = 0;
        if (!Directory.Exists(root)) return 0;
        foreach (var d in Directory.GetDirectories(root, prefix + "*"))
        {
            string name = Path.GetFileName(d);
            if (name.StartsWith(prefix))
            {
                string n = name.Substring(prefix.Length);
                int us = n.IndexOf('_');
                if (us >= 0) n = n.Substring(0, us);
                if (int.TryParse(n, out int v)) latest = Mathf.Max(latest, v);
            }
        }
        return latest;
    }

    static bool SessionContainsTrial(string sessionDir, string trialFilename)
    {
        string p1 = Path.Combine(sessionDir, "01", trialFilename);
        string p2 = Path.Combine(sessionDir, "02", trialFilename);
        return File.Exists(p1) || File.Exists(p2);
    }
}
