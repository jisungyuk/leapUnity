using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.IO;
using System.Globalization;

public class GameSessionController_RG : MonoBehaviour
{
    [System.Serializable]
    public class SimpleTrialConfig
    {
        public Vector3 startPos   = new Vector3(0, 0, 0);
        public Vector3 targetPos  = new Vector3(0.2f, 0, 0);
        public float   targetRadius    = 0.03f;   // meters
        public bool    visualFeedback  = true;    // VF
        public bool    useLeftHand     = false;   // false = right hand
        public int     trialIndex      = 0;       // from '#' column
        public int     targetId        = 0;       // from 'target' column

        public float   ttlOffsetMs     = 0f;      // ★ from 'ttl' column (ms)

        // ★ 추가: 0=left, 1=right, 2=either
        public int handMode = 1;
    }

    [Header("References")]
    [SerializeField] TrialGameController_RG trialController;
    [SerializeField] LeapFingerInput leapInput;

    [Header("UI (optional)")]
    [SerializeField] TMPro.TMP_Text statusText;        // error/info messages
    [SerializeField] TMPro.TMP_Text trialCounterText;  // shows e.g., "12/30"

    [Header("Trial Sequence")]
    [SerializeField] SimpleTrialConfig[] trials;  // filled from CSV when possible

    [Header("Completion")]
    [Tooltip("Scene to load when all trials finish.")]
    [SerializeField] string endSceneName = "EndScene";
    [SerializeField] bool loadEndSceneOnComplete = true;


    int currentIndex = -1;

    void OnEnable()
    {
        if (trialController != null)
            trialController.OnTrialFinished += HandleTrialFinished;
    }

    void OnDisable()
    {
        if (trialController != null)
            trialController.OnTrialFinished -= HandleTrialFinished;
    }

    void Start()
    {
        // Prefer in-memory data from setup screens; if absent, fall back to Inspector-defined trials.
        bool usingStoreData = TryBuildTrialsFromStore();

        if ((!usingStoreData || trials == null || trials.Length == 0))
        {
            // Fallback: use whatever is already set in the Inspector (for direct play in LeapScene)
            if (trials == null || trials.Length == 0)
            {
                ShowStatus("No in-memory session/targets. Setup first or define trials in Inspector.");
                Debug.LogWarning("[GameSessionController] No trials available from store or Inspector.");
                return;
            }
            else
            {
                Debug.Log("[GameSessionController] Using Inspector-defined trials (store empty).");
                usingStoreData = false;
            }
        }

        // Apply starting trial index (1-based)
        int startAt = 1;
        if (usingStoreData && RuntimeConfigStore.Instance != null)
            startAt = Mathf.Clamp(RuntimeConfigStore.Instance.startTrialIndex, 1, trials.Length);
        // If not using store, default = 1 (matches prior workflow when testing LeapScene directly)
        currentIndex = startAt - 2; // StartNextTrial() will ++ to the desired 0-based index

        StartNextTrial();
    }

    void ShowStatus(string msg)
    {
        if (statusText) statusText.text = msg;
    }

    void StartNextTrial()
    {
        currentIndex++;

        if (trials == null || trials.Length == 0)
        {
            Debug.LogWarning("[GameSessionController] No trials configured.");
            return;
        }

        if (currentIndex >= trials.Length)
        {
            Debug.Log("[GameSessionController] All trials complete.");
            if (loadEndSceneOnComplete && !string.IsNullOrEmpty(endSceneName))
            {
                SceneManager.LoadScene(endSceneName);
            }
            else
            {
                ShowStatus("Session complete.");
            }
            return;
        }

        var cfg = trials[currentIndex];

        // Update trial counter UI (1-based index)
        if (trialCounterText)
            trialCounterText.text = ($"{currentIndex + 1}/{trials.Length}");

        // Set which hand to track this trial
        // Set which hand to track this trial
        if (leapInput != null)
        {
            int mode = cfg.handMode;  // 0=left,1=right,2=either

            // (선택 사항) forceHandOverride 있을 경우 여기에 덮어씌우면 됨
            // if (forceHandOverride) mode = forceHandMode;

            bool useLeft   = (mode == 0);
            bool allowBoth = (mode == 2);

            leapInput.allowEitherHand = allowBoth;
            leapInput.useLeftHand     = useLeft;   // either 모드에서는 "기본 손" 정도로만 의미

            Debug.Log($"[GameSessionController] Hand mode: {mode} " +
                    $"(useLeft={useLeft}, allowEither={allowBoth})");
        }


        // Configure & start the trial
        if (trialController != null)
        {
            // Pass meta for logging (trial index, target id, hand mode)
            trialController.ConfigureAndBegin(
                cfg.startPos,
                cfg.targetPos,
                cfg.targetRadius,
                cfg.visualFeedback,
                cfg.ttlOffsetMs,
                cfg.trialIndex,
                cfg.targetId,
                cfg.handMode
            );
        }

        Debug.Log($"[GameSessionController] Starting trial {currentIndex + 1}/{trials.Length} (TrialIndex={cfg.trialIndex}, TargetID={cfg.targetId}, TTL={cfg.ttlOffsetMs} ms)");
    }

    void HandleTrialFinished()
    {
        Debug.Log($"[GameSessionController] Trial {currentIndex + 1} finished.");
        StartNextTrial();
    }

    // ----------------------------------------------------------
    // CSV LOADING SECTION
    // ----------------------------------------------------------

    class TargetSpec
    {
        public int id;
        public float diameterMeters;
        public Vector3 pos;
    }

    class SessionTrialSpec
    {
        public int trialIndex;   // '#'
        public Vector3 startPos; // startx,starty,startz
        public int targetId;     // 'target'
        public int hand;         // 0=left,1=right,2=both
        public int vf;           // 0/1
        public float ttl1;       // 'ttl' (ms)
    }

    bool TryBuildTrialsFromCsv()
    {
        // 1) Check if participant folder is set
        if (DataPathManager.Instance == null ||
            string.IsNullOrEmpty(DataPathManager.Instance.ParticipantFolder))
        {
            Debug.LogWarning("[GameSessionController] Participant folder not set. Cannot load session/targets CSV.");
            return false;
        }

        string folder = DataPathManager.Instance.ParticipantFolder;

        if (!Directory.Exists(folder))
        {
            Debug.LogWarning($"[GameSessionController] Participant folder does not exist: {folder}");
            return false;
        }

        // 2) Find latest targets_*.csv and session_*.csv
        string targetsFile = FindLatestCsv(folder, "targets_*.csv");
        string sessionFile = FindLatestCsv(folder, "session_*.csv");

        if (string.IsNullOrEmpty(targetsFile) || string.IsNullOrEmpty(sessionFile))
        {
            Debug.LogWarning($"[GameSessionController] Could not find both targets_*.csv and session_*.csv in {folder}");
            return false;
        }

        Debug.Log($"[GameSessionController] Using targets CSV: {targetsFile}");
        Debug.Log($"[GameSessionController] Using session CSV: {sessionFile}");

        // 3) Parse targets
        var targets = LoadTargetsCsv(targetsFile);
        if (targets == null || targets.Count == 0)
        {
            Debug.LogWarning("[GameSessionController] No targets parsed from CSV.");
            return false;
        }

        // 4) Parse session trials
        var sessionTrials = LoadSessionCsv(sessionFile);
        if (sessionTrials == null || sessionTrials.Count == 0)
        {
            Debug.LogWarning("[GameSessionController] No session trials parsed from CSV.");
            return false;
        }

        // 5) Merge session trials + targets into SimpleTrialConfig[]
        List<SimpleTrialConfig> built = new List<SimpleTrialConfig>();
        foreach (var st in sessionTrials)
        {
            // 0=left, 1=right, 2=either; 그 외 값만 스킵
            if (st.hand < 0 || st.hand > 2)
            {
                Debug.LogWarning($"[GameSessionController] Skipping trial {st.trialIndex}: Hand={st.hand} (allowed: 0=left,1=right,2=either).");
                continue;
            }


            if (!targets.TryGetValue(st.targetId, out TargetSpec ts))
            {
                Debug.LogWarning($"[GameSessionController] Skipping trial {st.trialIndex}: TargetID {st.targetId} not found.");
                continue;
            }

            var cfg = new SimpleTrialConfig();
            cfg.trialIndex      = st.trialIndex;
            cfg.targetId        = st.targetId;
            cfg.startPos        = st.startPos;
            cfg.targetPos       = ts.pos;
            cfg.targetRadius    = ts.diameterMeters * 0.5f; // radius = diameter/2
            cfg.visualFeedback  = (st.vf == 1);
            cfg.useLeftHand     = (st.hand == 0);           // 0=left → true, 1=right → false
            cfg.ttlOffsetMs     = st.ttl1;                  // ★ TTL(ms) 복사

            // ★ handMode 저장 (0/1/2)
            cfg.handMode        = st.hand;
            // 기존 useLeftHand는 0/1 기준으로만 채워두자 (디버깅용)
            cfg.useLeftHand     = (st.hand == 0);

            built.Add(cfg);
        }

        if (built.Count == 0)
        {
            Debug.LogWarning("[GameSessionController] After merging, no valid trials remained.");
            return false;
        }

        trials = built.ToArray();
        Debug.Log($"[GameSessionController] Built {trials.Length} trial configs from CSV.");
        return true;
    }

    // ----------------------------------------------------------
    // STORE LOADING SECTION (use values already loaded in the app)
    // ----------------------------------------------------------
    bool TryBuildTrialsFromStore()
    {
        var store = RuntimeConfigStore.Instance;
        if (store == null)
        {
            Debug.LogWarning("[GameSessionController] RuntimeConfigStore not present.");
            return false;
        }

        if (store.Targets == null || store.Targets.Count == 0)
        {
            Debug.LogWarning("[GameSessionController] No targets in store.");
            return false;
        }
        if (store.Trials == null || store.Trials.Count == 0)
        {
            Debug.LogWarning("[GameSessionController] No trials in store.");
            return false;
        }

        // Build dictionary of targets by ID
        var targets = new Dictionary<int, TargetSpec>();
        foreach (var t in store.Targets)
        {
            var ts = new TargetSpec
            {
                id = t.id,
                diameterMeters = t.cm / 100f,
                pos = new Vector3(t.x, t.y, t.z)
            };
            targets[t.id] = ts;
        }

        var built = new List<SimpleTrialConfig>();
        foreach (var tr in store.Trials)
        {
            if (!int.TryParse((tr.targetId ?? string.Empty).Trim(), out int targetId))
            {
                Debug.LogWarning($"[GameSessionController] Skipping trial {tr.trial}: targetId '{tr.targetId}' invalid.");
                continue;
            }

            if (!targets.TryGetValue(targetId, out TargetSpec ts))
            {
                Debug.LogWarning($"[GameSessionController] Skipping trial {tr.trial}: TargetID {targetId} not found in store targets.");
                continue;
            }

            int handMode = ParseHand(tr.hand);
            if (handMode < 0)
            {
                Debug.LogWarning($"[GameSessionController] Skipping trial {tr.trial}: hand '{tr.hand}' invalid.");
                continue;
            }

            bool vf = ParseVf(tr.vf, defaultValue: true);

            var cfg = new SimpleTrialConfig
            {
                trialIndex     = tr.trial,
                targetId       = targetId,
                startPos       = new Vector3(tr.startX, tr.startY, tr.startZ),
                targetPos      = ts.pos,
                targetRadius   = ts.diameterMeters * 0.5f,
                visualFeedback = vf,
                ttlOffsetMs    = tr.ttl1,
                handMode       = handMode,
                useLeftHand    = (handMode == 0)
            };

            built.Add(cfg);
        }

        if (built.Count == 0)
        {
            Debug.LogWarning("[GameSessionController] No valid trials built from store.");
            return false;
        }

        trials = built.ToArray();
        Debug.Log($"[GameSessionController] Built {trials.Length} trial configs from Runtime store.");
        return true;
    }

    int ParseHand(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 1; // default right
        var t = s.Trim().ToLowerInvariant();
        if (int.TryParse(t, out int v))
        {
            if (v >= 0 && v <= 2) return v;
            return -1;
        }
        if (t == "left" || t == "l") return 0;
        if (t == "right" || t == "r") return 1;
        if (t == "either" || t == "both" || t == "e" || t == "b") return 2;
        return -1;
    }

    bool ParseVf(string s, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(s)) return defaultValue;
        var t = s.Trim().ToLowerInvariant();
        if (int.TryParse(t, out int v)) return v != 0;
        if (t == "true" || t == "yes" || t == "y" || t == "on") return true;
        if (t == "false" || t == "no" || t == "n" || t == "off") return false;
        return defaultValue;
    }

    string FindLatestCsv(string folder, string pattern)
    {
        var files = Directory.GetFiles(folder, pattern);
        if (files == null || files.Length == 0)
            return null;

        // Pick the most recently written file
        string latest = null;
        System.DateTime latestTime = System.DateTime.MinValue;
        foreach (var f in files)
        {
            var t = File.GetLastWriteTime(f);
            if (t > latestTime)
            {
                latestTime = t;
                latest = f;
            }
        }
        return latest;
    }

    // Targets header: ID, cm, x,y,z
    Dictionary<int, TargetSpec> LoadTargetsCsv(string path)
    {
        var dict = new Dictionary<int, TargetSpec>();
        var lines = File.ReadAllLines(path);
        if (lines.Length == 0) return dict;

        var ic = CultureInfo.InvariantCulture;

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var cols = line.Split(',');
            if (cols.Length < 5) continue;

            // ID
            if (!int.TryParse(cols[0].Trim(), NumberStyles.Integer, ic, out int id)) continue;
            // diameter in cm → convert to meters
            if (!float.TryParse(cols[1].Trim(), NumberStyles.Float, ic, out float cm)) continue;
            float diameterMeters = cm / 100f;

            if (!float.TryParse(cols[2].Trim(), NumberStyles.Float, ic, out float px)) continue;
            if (!float.TryParse(cols[3].Trim(), NumberStyles.Float, ic, out float py)) continue;
            if (!float.TryParse(cols[4].Trim(), NumberStyles.Float, ic, out float pz)) continue;

            var ts = new TargetSpec
            {
                id = id,
                diameterMeters = diameterMeters,
                pos = new Vector3(px, py, pz)
            };

            dict[id] = ts;
        }

        return dict;
    }

    // Session header: #, target, hand, startx , starty,startz, ttl, vf
    List<SessionTrialSpec> LoadSessionCsv(string path)
    {
        var list = new List<SessionTrialSpec>();
        var lines = File.ReadAllLines(path);
        if (lines.Length == 0) return list;

        var ic = CultureInfo.InvariantCulture;

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var cols = line.Split(',');
            if (cols.Length < 8) continue;

            // # (trial index)
            if (!int.TryParse(cols[0].Trim(), NumberStyles.Integer, ic, out int trialIndex)) continue;
            // target
            if (!int.TryParse(cols[1].Trim(), NumberStyles.Integer, ic, out int targetId)) continue;
            // hand
            if (!int.TryParse(cols[2].Trim(), NumberStyles.Integer, ic, out int hand)) continue;
            // startx, starty, startz
            if (!float.TryParse(cols[3].Trim(), NumberStyles.Float, ic, out float sx)) continue;
            if (!float.TryParse(cols[4].Trim(), NumberStyles.Float, ic, out float sy)) continue;
            if (!float.TryParse(cols[5].Trim(), NumberStyles.Float, ic, out float sz)) continue;
            // ttl
            if (!float.TryParse(cols[6].Trim(), NumberStyles.Float, ic, out float ttl1))
                ttl1 = 0f;
            // vf
            if (!int.TryParse(cols[7].Trim(), NumberStyles.Integer, ic, out int vf))
                vf = 1;

            var st = new SessionTrialSpec
            {
                trialIndex = trialIndex,
                startPos   = new Vector3(sx, sy, sz),
                targetId   = targetId,
                hand       = hand,
                vf         = vf,
                ttl1       = ttl1
            };

            list.Add(st);
        }

        return list;
    }
}
