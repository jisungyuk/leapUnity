using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Leap;
using UnityEngine;

// Records per-trial data and writes two CSVs: 01 (Right), 02 (Left)
// Sampling is driven by LeapProvider.OnUpdateFrame to align with device timestamps.
public interface ITrialStateProvider
{
    int GetStateCode();
}

public class TrialDataLogger : MonoBehaviour
{
    [Header("Sources")]
    [SerializeField] LeapProvider leapProvider;
    [SerializeField] Transform indexTip;
    [SerializeField] Transform thumbTip;
    [SerializeField] Transform indexMcp;

    ITrialStateProvider owner; // to read current state code

    struct Sample
    {
        public long tUsFromReady;
        public int  stateCode;
        public Vector3 idx, thb, mcp;
    }

    readonly List<Sample> samples = new();
    bool recording = false;
    long readyTimestampUs = 0;
    long ttlFiredUsFromReady = -1;

    // Meta
    int trialIndex = 0;
    int handMode = 1; // 0=L,1=R,2=EITHER
    bool usedLeft = false;
    int targetId = 0;
    Vector3 startPos, targetPos;
    float startRadius, targetRadius;
    float holdDuration, goDelay, moveTimeout, feedbackDuration;
    float ttlOffsetMs;
    float readyTime_s, goTime_s;

    CultureInfo ic = CultureInfo.InvariantCulture;

    public void Setup(LeapProvider provider, Transform idx, Transform thb, Transform mcp, ITrialStateProvider owner)
    {
        this.leapProvider = provider;
        this.indexTip    = idx;
        this.thumbTip    = thb;
        this.indexMcp    = mcp;
        this.owner       = owner;

        if (leapProvider != null)
        {
            leapProvider.OnUpdateFrame -= OnUpdateFrame; // avoid double
            leapProvider.OnUpdateFrame += OnUpdateFrame;
        }
    }

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
        this.trialIndex = trialIndex;
        this.targetId = targetId;
        this.handMode = handMode;
        this.usedLeft = usedLeft;
        this.readyTimestampUs = readyTimestampUs;
        this.startPos = startPos;
        this.startRadius = startRadius;
        this.targetPos = targetPos;
        this.targetRadius = targetRadius;
        this.holdDuration = holdDuration;
        this.goDelay = goDelay;
        this.moveTimeout = moveTimeout;
        this.feedbackDuration = feedbackDuration;
        this.ttlOffsetMs = ttlOffsetMs;
        this.readyTime_s = readyTime_s;
        this.goTime_s = goTime_s;

        samples.Clear();
        ttlFiredUsFromReady = -1;
        recording = true;
    }

    public void SetGoTime(float goTime_s)
    {
        this.goTime_s = goTime_s;
    }

    public void NoteTtlFired(long deviceTimestampUs)
    {
        if (!recording || readyTimestampUs == 0) return;
        ttlFiredUsFromReady = Mathf.Max(0, (int)(deviceTimestampUs - readyTimestampUs));
    }

    public void EndAndSave()
    {
        recording = false;
        WriteFiles();
    }

    void OnDestroy()
    {
        if (leapProvider != null)
            leapProvider.OnUpdateFrame -= OnUpdateFrame;
    }

    void OnUpdateFrame(Frame frame)
    {
        if (!recording || frame == null || readyTimestampUs == 0) return;

        long tUs = frame.Timestamp - readyTimestampUs;
        if (tUs < 0) return; // before ready

        var s = new Sample
        {
            tUsFromReady = tUs,
            stateCode = (owner != null ? owner.GetStateCode() : 0),
            idx = indexTip ? indexTip.position : Vector3.zero,
            thb = thumbTip ? thumbTip.position : Vector3.zero,
            mcp = indexMcp ? indexMcp.position : Vector3.zero
        };
        samples.Add(s);
    }

    void WriteFiles()
    {
        string root = DataPathManager.Instance != null ? DataPathManager.Instance.ParticipantFolder : Application.persistentDataPath;
        if (string.IsNullOrEmpty(root)) root = Application.persistentDataPath;

        // Decide session_NNN folder per trial overwrite rule
        int sessionIdx = FindLatestSessionIndex(root);
        if (sessionIdx <= 0) sessionIdx = 1;

        string trialFile = TrialFilename(trialIndex);
        while (SessionContainsTrial(root, sessionIdx, trialFile))
        {
            sessionIdx++;
        }

        string sessionDir = Path.Combine(root, SessionFolder(sessionIdx));
        string dir01 = Path.Combine(sessionDir, "01");
        string dir02 = Path.Combine(sessionDir, "02");
        Directory.CreateDirectory(dir01);
        Directory.CreateDirectory(dir02);

        // Write right(01) and left(02)
        string pathRight = Path.Combine(dir01, trialFile);
        string pathLeft  = Path.Combine(dir02, trialFile);

        WriteOne(pathRight, false); // right file
        WriteOne(pathLeft,  true);  // left file
    }

    void WriteOne(string path, bool isLeftFile)
    {
        // Build header
        var sb = new StringBuilder();
        sb.AppendLine($"# trial_index: {trialIndex}");
        sb.AppendLine($"# hand_mode: {handMode}");
        sb.AppendLine($"# used_hand: {(usedLeft ? "left" : "right")}");
        sb.AppendLine($"# this_file_hand: {(isLeftFile ? "left" : "right")}");
        sb.AppendLine($"# target_id: {targetId}");
        sb.AppendLine($"# target_pos_m: {targetPos.x.ToString(ic)},{targetPos.y.ToString(ic)},{targetPos.z.ToString(ic)}");
        sb.AppendLine($"# target_radius_m: {targetRadius.ToString(ic)}");
        sb.AppendLine($"# start_pos_m: {startPos.x.ToString(ic)},{startPos.y.ToString(ic)},{startPos.z.ToString(ic)}");
        sb.AppendLine($"# start_radius_m: {startRadius.ToString(ic)}");
        sb.AppendLine($"# timing_s: hold={holdDuration.ToString(ic)}, goDelay={goDelay.ToString(ic)}, moveTimeout={moveTimeout.ToString(ic)}, feedback={feedbackDuration.ToString(ic)}");
        sb.AppendLine($"# times_s: ready={readyTime_s.ToString(ic)}, go={goTime_s.ToString(ic)}");
        // TTL meta
        float ttlFired_s_fromReady = (ttlFiredUsFromReady >= 0) ? (ttlFiredUsFromReady / 1_000_000f) : -1f;
        sb.AppendLine($"# ttl_offset_ms: {ttlOffsetMs.ToString(ic)}");
        sb.AppendLine($"# ttl_fired_s_fromReady: {ttlFired_s_fromReady.ToString(ic)}");

        // Columns
        sb.AppendLine("t_ms_from_ready,state_code,ttl,idx_x,idx_y,idx_z,thb_x,thb_y,thb_z,mcp_x,mcp_y,mcp_z");

        bool zero = (handMode != 2) && // unimanual
                    ((isLeftFile && !usedLeft) || (!isLeftFile && usedLeft));

        // Write rows
        foreach (var s in samples)
        {
            // ttl flag = 1 on the first row whose time passes ttlFiredUsFromReady
            int ttl = 0;
            if (ttlFiredUsFromReady >= 0 && s.tUsFromReady >= ttlFiredUsFromReady)
            {
                ttl = 1;
                // ensure only first gets 1
                ttlFiredUsFromReady = long.MaxValue;
            }

            Vector3 idx = zero ? Vector3.zero : s.idx;
            Vector3 thb = zero ? Vector3.zero : s.thb;
            Vector3 mcp = zero ? Vector3.zero : s.mcp;

            sb.Append(
                ((s.tUsFromReady / 1000f).ToString(ic)) + "," +
                s.stateCode.ToString(ic) + "," +
                ttl.ToString(ic) + "," +
                idx.x.ToString(ic) + "," + idx.y.ToString(ic) + "," + idx.z.ToString(ic) + "," +
                thb.x.ToString(ic) + "," + thb.y.ToString(ic) + "," + thb.z.ToString(ic) + "," +
                mcp.x.ToString(ic) + "," + mcp.y.ToString(ic) + "," + mcp.z.ToString(ic)
            );
            sb.Append('\n');
        }

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        Debug.Log($"[TrialDataLogger] Wrote {path}");
    }

    static string SessionFolder(int idx) => $"session_{idx:000}";
    static string TrialFilename(int trialIndex) => $"{trialIndex:0000}.csv";

    static int FindLatestSessionIndex(string root)
    {
        int latest = 0;
        if (!Directory.Exists(root)) return 0;
        foreach (var d in Directory.GetDirectories(root, "session_*") )
        {
            var name = Path.GetFileName(d);
            if (name.StartsWith("session_"))
            {
                string n = name.Substring(8);
                if (int.TryParse(n, out int v)) latest = Mathf.Max(latest, v);
            }
        }
        return latest;
    }

    static bool SessionContainsTrial(string root, int sessionIdx, string trialFilename)
    {
        string dir = Path.Combine(root, SessionFolder(sessionIdx));
        if (!Directory.Exists(dir)) return false;
        string p1 = Path.Combine(dir, "01", trialFilename);
        string p2 = Path.Combine(dir, "02", trialFilename);
        return File.Exists(p1) || File.Exists(p2);
    }
}
