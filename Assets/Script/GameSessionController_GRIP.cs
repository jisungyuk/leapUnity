using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Globalization;
using TMPro;

/// <summary>
/// Manages the GRIP session.
/// On start, shows a calibration screen: subject places hand at the physical
/// start marker and presses SPACE. The captured MCP position becomes the
/// origin for all trial coordinates.
///
/// Target positions are stored as polar coords (angle_deg, distance_cm)
/// and converted to world-space using the calibration origin:
///   X = originX + distance_m * cos(angle_rad)
///   Z = originZ + distance_m * sin(angle_rad)
///   Y = originY
/// </summary>
public class GameSessionController_GRIP : MonoBehaviour
{
    [System.Serializable]
    public class GripTrialConfig
    {
        public Vector3 startPos;
        public Vector3 targetPos;
        public float   targetRadius;
        public float   startRadiusCm;     // start zone radius in cm (0 = use Inspector default)
        public int     handMode;
        public bool    ttlEnabled;
        public float   ttlOffsetMs;       // Testing Stimulus (Output2) delay from Go (ms)
        public float   ttl2OffsetMs;      // Conditioning Stimulus (Output1) offset from Testing (ms)
        public int     trialIndex;
        public int     targetId;
        public int     instruction;       // 0=REST, 1=REACH, 2=REACH+GRASP
        public float   holdDuration;      // hold in start before direction cue (s); 0 = Inspector default
        public float   waitForGo;         // direction cue → go delay (s); 0 = Inspector default
        public float   executingDuration; // execution window (s); 0 = Inspector default
    }

    [Header("References")]
    [SerializeField] TrialGameController_GRIP trialController;
    [SerializeField] LeapFingerInput           leapInput;
    [SerializeField] LabChartStatusChecker     labChartStatus;

    [Header("Hand Visualization")]
    [SerializeField] GameObject capsuleHands;   // full hand model — ON during calibration, OFF during trials

    [Header("Zone Visualization")]
    [SerializeField] GameObject startSphere;              // shown at calibration origin after SPACE
    [SerializeField] GameObject targetSphere;             // shown at default target position after SPACE
    [SerializeField] float      previewAngleDeg      = 90f;  // default target angle (0=right, 90=forward)
    [SerializeField] float      previewDistanceCm    = 30f;  // default target distance in cm
    [SerializeField] float      previewCylinderRadiusCm = 3f; // calibration preview target sphere radius (cm)

    [Header("Experimenting Mode (used when launched directly, not from MainMenu)")]
    [Tooltip("Enable trial data logging in experimenting mode")]
    [SerializeField] bool experimentLogging = false;
    [Tooltip("Folder to save experimenting data (inside project)")]
    [SerializeField] string experimentDataPath = "C:/Users/Jisung Yuk/Documents/leapUnity/ExperimentData";

    [Tooltip("Trial conditions cycled in experimenting mode. Each entry is a fully independent trial: TTL, instruction, hand, and target position.")]
    [SerializeField] List<ExperimentTtlEntry> experimentTtlList = new List<ExperimentTtlEntry>
    {
        new ExperimentTtlEntry { ttlEnabled = true,  ttlOffsetMs = 0f, ttl2OffsetMs =  -5f, instruction = 1, handMode = 1, angleDeg = 90f, distanceCm = 30f },
        new ExperimentTtlEntry { ttlEnabled = true,  ttlOffsetMs = 0f, ttl2OffsetMs = -15f, instruction = 1, handMode = 1, angleDeg = 90f, distanceCm = 30f },
        new ExperimentTtlEntry { ttlEnabled = true,  ttlOffsetMs = 0f, ttl2OffsetMs =   0f, instruction = 1, handMode = 1, angleDeg = 90f, distanceCm = 30f },
        new ExperimentTtlEntry { ttlEnabled = false, ttlOffsetMs = 0f, ttl2OffsetMs =   0f, instruction = 1, handMode = 1, angleDeg = 90f, distanceCm = 30f },
    };

    /// <summary>
    /// One TTL configuration for experimenting mode.
    /// ttlEnabled=false  → no TTL at all (no FRO call, no TMS).
    /// ttlOffsetMs       → Testing Stimulus (Output2) delay relative to Go cue (ms). 0 = at Go cue.
    /// ttl2OffsetMs      → Conditioning Stimulus (Output1) delay relative to Testing Stimulus (ms).
    ///                      0 = SinglePulse (Output1 disabled). Negative = Conditioning fires before Testing.
    /// </summary>
    [System.Serializable]
    public class ExperimentTtlEntry
    {
        public bool  ttlEnabled      = true;
        public float ttlOffsetMs     = 0f;    // Testing Stimulus: ms relative to Go cue
        public float ttl2OffsetMs    = 0f;    // Conditioning Stimulus: ms relative to Testing; 0 = SinglePulse
        public int   instruction     = 1;     // 0=REST  1=REACH  2=REACH+GRASP
        public int   handMode        = 1;     // 0=Left  1=Right  2=Either
        public float angleDeg        = 90f;   // target angle from home (0=right, 90=forward)
        public float distanceCm      = 30f;   // target distance from home in cm
        public float cylinderRadiusCm = 3f;   // cylinder radius in cm (0 = use Inspector default)
    }

    [Header("UI")]
    [SerializeField] TMP_Text statusText;
    [SerializeField] TMP_Text trialCounterText;
    [SerializeField] TMP_Text calibrationText;   // shown during calibration screen
    [SerializeField] TMP_Text stageText;         // top-left: shows CALIBRATION or TRIAL
    [SerializeField] float instructionFontSizeNormal = 36f;
    [SerializeField] float instructionFontSizeHidden = 75f;

    [Header("Completion")]
    [SerializeField] string endSceneName          = "EndScene";
    [SerializeField] bool   loadEndSceneOnComplete = true;

    // ── State ───────────────────────────────────────────────────────
    enum SessionState { Calibrating, Running }
    SessionState sessionState = SessionState.Calibrating;
    bool experimentingMode = false;
    int visualMode = 0; // 0=all, 1=no hands, 2=none

    GripTrialConfig[] trials;

    // ── Public info for overlay ──────────────────────────────────────
    public GripTrialConfig CurrentTrial =>
        trials != null && currentIndex >= 0 && currentIndex < trials.Length
        ? trials[currentIndex] : null;
    public int TrialCount    => trials?.Length ?? 0;
    public int CurrentIndex  => currentIndex + 1;   // 1-based
    public bool IsCalibrating => sessionState == SessionState.Calibrating;
    public bool IsExperimenting => experimentingMode;
    public int ExperimentCounter => experimentTrialCounter;
    public string StatusMessage => statusText ? statusText.text : "";
    int currentIndex = -1;

    // ── Unity lifecycle ─────────────────────────────────────────────

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
        // Hide trial UI until calibration done
        if (trialController != null)
            trialController.gameObject.SetActive(false);

        // Show hand model during calibration
        if (capsuleHands != null) capsuleHands.SetActive(true);

        ShowCalibrationScreen();
    }

    void Update()
    {
        if (sessionState == SessionState.Calibrating)
        {
            if (Input.GetKeyDown(KeyCode.Space))
                ConfirmCalibration();

            UpdateCalibrationStatus();
        }

        // SHIFT+SPACE: recalibrate, only allowed when trial is in MoveToStart state
        if (sessionState == SessionState.Running &&
            Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                bool inMoveToStart = trialController != null &&
                                     trialController.gameObject.activeSelf &&
                                     trialController.GetStateCode() == 1; // MoveToStart

                if (inMoveToStart)
                    Recalibrate();
                else
                    Debug.Log("[GameSessionController_GRIP] Recalibration blocked — trial in progress.");
            }
        }

        // F key cycles visual mode: all → no hands → none → all
        if (Input.GetKeyDown(KeyCode.F) && sessionState == SessionState.Running)
        {
            visualMode = (visualMode + 1) % 3;
            ApplyVisualMode();
        }
    }

    // ── Calibration ─────────────────────────────────────────────────

    void ShowCalibrationScreen()
    {
        sessionState = SessionState.Calibrating;

        // Accept either hand for calibration
        if (leapInput != null)
            leapInput.allowEitherHand = true;

        if (stageText) stageText.text = "CALIBRATION";
        if (statusText) statusText.text = "";
    }

    void UpdateCalibrationStatus()
    {
        if (!calibrationText) return;

        // Leap Motion status
        bool handDetected = leapInput != null && leapInput.hasIndexJointData;
        string leapLine = handDetected
            ? "<color=#44FF44>● Hand detected</color>"
            : "<color=#FF4444>○ No hand detected</color>";

        // LabChart status
        string labChartLine;
        if (labChartStatus == null)
        {
            labChartLine = "<color=#888888>○ LabChart checker not assigned</color>";
        }
        else if (!labChartStatus.IsOpen)
        {
            labChartLine = "<color=#FF4444>✗ LabChart not running</color>";
        }
        else
        {
            labChartLine = "<color=#44FF44>● LabChart open</color>  <color=#FFFF44>— confirm recording manually</color>";
        }

        calibrationText.text =
            "CALIBRATION\n\n" +
            "Place either hand at the home position\n" +
            "then press  SPACE\n\n" +
            $"Leap Motion:  {leapLine}\n" +
            $"LabChart:     {labChartLine}";
    }

    void ConfirmCalibration()
    {
        if (leapInput == null)
        {
            ShowStatus("LeapFingerInput not assigned.");
            return;
        }

        // Read current MCP world position as calibration origin
        Transform mcp = leapInput.indexMcp;
        if (mcp == null || !leapInput.hasIndexJointData)
        {
            if (calibrationText)
                calibrationText.text =
                    "Hand not detected!\n\n" +
                    "Place your hand at the start marker\n" +
                    "then press  SPACE";
            Debug.LogWarning("[GameSessionController_GRIP] Calibration failed — no hand detected.");
            return;
        }

        Vector3 origin = mcp.position;

        var store = RuntimeConfigStore.Instance;
        if (store != null)
        {
            store.rwrCalibrationOrigin = origin;
            store.rwrCalibrated        = true;
        }

        Debug.Log($"[GameSessionController_GRIP] Calibration origin set: {origin}");

        // Show start sphere at calibration origin
        float diameter = trialController.StartRadius * 2f;
        if (startSphere != null)
        {
            startSphere.transform.position   = origin;
            startSphere.transform.localScale = new Vector3(diameter, diameter, diameter);
            startSphere.SetActive(true);
        }

        // Show default target sphere at (previewAngleDeg, previewDistanceCm) from origin
        if (targetSphere != null)
        {
            float angleRad = previewAngleDeg * Mathf.Deg2Rad;
            float distM    = previewDistanceCm / 100f;
            Vector3 targetPos = new Vector3(
                origin.x + distM * Mathf.Cos(angleRad),
                origin.y,
                origin.z + distM * Mathf.Sin(angleRad)
            );
            float targetDiameter = previewCylinderRadiusCm / 100f * 2f;
            targetSphere.transform.position   = targetPos;
            targetSphere.transform.localScale = new Vector3(targetDiameter, targetDiameter, targetDiameter);
            targetSphere.SetActive(true);
        }

        if (calibrationText)
        {
            calibrationText.text = "";
            calibrationText.gameObject.SetActive(false);   // hide calibration panel after cal
        }

        // Try to build trials from store (MainMenu session)
        // If forceExperimentingMode is checked, or no store data → experimenting mode
        // If launched from MainMenu, always use store data.
        // Otherwise (direct scene launch), forceExperimentingMode or no store data → experimenting.
        bool fromMenu = RuntimeConfigStore.Instance != null &&
                        RuntimeConfigStore.Instance.launchedFromMainMenu;
        if (RuntimeConfigStore.Instance != null)
            RuntimeConfigStore.Instance.launchedFromMainMenu = false; // consume the flag

        // ── Startup diagnostics ──────────────────────────────────────
        int storeTrialCount = RuntimeConfigStore.Instance?.Trials?.Count ?? 0;
        Debug.Log($"[GameSessionController_GRIP] === SESSION START DIAGNOSTICS ===\n" +
                  $"  launchedFromMainMenu : {fromMenu}\n" +
                  $"  Store trial count : {storeTrialCount}\n" +
                  $"  → Will use : {(fromMenu ? "STORE (MainMenu session)" : "EXPERIMENT TTL LIST")}");

        if (fromMenu)
        {
            if (!TryBuildTrialsFromStore(origin))
                Debug.LogWarning("[GameSessionController_GRIP] Launched from MainMenu but no store data found.");
        }
        else
        {
            experimentingMode = true;
            BuildExperimentTrial(origin);
        }

        // Hand model stays visible — F key can toggle it at any time

        sessionState = SessionState.Running;
        if (stageText) stageText.text = "TRIAL";

        if (experimentingMode)
        {
            // Set up logging before enabling trial controller
            var store2 = RuntimeConfigStore.Instance;
            if (store2 != null) store2.enableTrialLogging = experimentLogging;

            if (experimentLogging && DataPathManager.Instance != null)
            {
                System.IO.Directory.CreateDirectory(experimentDataPath);
                DataPathManager.Instance.TrySetFolder(experimentDataPath, out _);
                Debug.Log($"[GameSessionController_GRIP] Experiment data → {experimentDataPath}");
            }

            if (trialController != null)
                trialController.SetExperimentingMode(true);

        }

        // GRIP spawns its own cylinder target — hide the scene preview sphere
        if (targetSphere != null) targetSphere.SetActive(false);

        // Enable trial controller and start
        if (trialController != null)
            trialController.gameObject.SetActive(true);

        if (experimentingMode)
        {
            currentIndex = 0;
            RunExperimentTrial();
        }
        else
        {
            int startAt = 1;
            if (store != null)
                startAt = Mathf.Clamp(store.startTrialIndex, 1, trials.Length);

            currentIndex = startAt - 2;
            StartNextTrial();
        }
    }

    // ── Trial sequencing ─────────────────────────────────────────────

    void StartNextTrial()
    {
        currentIndex++;

        if (trials == null || trials.Length == 0)
        {
            Debug.LogWarning("[GameSessionController_GRIP] No trials.");
            return;
        }

        if (currentIndex >= trials.Length)
        {
            Debug.Log("[GameSessionController_GRIP] All trials complete.");
            if (loadEndSceneOnComplete && !string.IsNullOrEmpty(endSceneName))
                SceneManager.LoadScene(endSceneName);
            else
                ShowStatus("Session complete.");
            return;
        }

        var cfg = trials[currentIndex];

        if (trialCounterText)
            trialCounterText.text = $"{currentIndex + 1}/{trials.Length}";

        if (leapInput != null)
        {
            leapInput.allowEitherHand = (cfg.handMode == 2);
            leapInput.useLeftHand     = (cfg.handMode == 0);
        }

        trialController?.ConfigureAndBegin(
            cfg.startPos,
            cfg.targetPos,
            cfg.targetRadius,
            cfg.ttlEnabled,
            cfg.ttlOffsetMs,
            cfg.ttl2OffsetMs,
            cfg.trialIndex,
            cfg.targetId,
            cfg.handMode,
            cfg.instruction,
            cfg.holdDuration,
            cfg.waitForGo,
            cfg.executingDuration,
            cfg.startRadiusCm
        );

        Debug.Log($"[GameSessionController_GRIP] Trial {currentIndex + 1}/{trials.Length} " +
                  $"inst={cfg.instruction} target={cfg.targetId} ttl={(cfg.ttlEnabled ? $"{cfg.ttlOffsetMs}ms" : "none")} " +
                  $"startPos={cfg.startPos} targetPos={cfg.targetPos}");
    }

    void HandleTrialFinished()
    {
        Debug.Log($"[GameSessionController_GRIP] Trial {currentIndex + 1} finished.");
        if (experimentingMode)
            RunExperimentTrial();
        else
            StartNextTrial();
    }

    // ── Visual mode ──────────────────────────────────────────────────

    void ApplyVisualMode()
    {
        bool showHands = (visualMode == 0);
        bool showAll   = (visualMode <= 1);

        if (capsuleHands != null) capsuleHands.SetActive(showHands);

        if (trialController != null && trialController.gameObject.activeSelf)
        {
            trialController.SetCursorsVisible(showAll);
            trialController.SetSpheresVisible(showAll);
            trialController.SetInstructionFontSize(showAll ? instructionFontSizeNormal : instructionFontSizeHidden);
        }
        else
        {
            // During calibration, control spheres directly
            if (startSphere  != null) startSphere.SetActive(showAll);
            if (targetSphere != null) targetSphere.SetActive(showAll);
        }
    }

    // ── Recalibration ────────────────────────────────────────────────

    void Recalibrate()
    {
        if (leapInput == null || leapInput.indexMcp == null)
        {
            Debug.LogWarning("[GameSessionController_GRIP] Recalibration failed — no hand detected.");
            return;
        }

        Vector3 origin = leapInput.indexMcp.position;

        var store = RuntimeConfigStore.Instance;
        if (store != null)
        {
            store.rwrCalibrationOrigin = origin;
            store.rwrCalibrated        = true;
        }

        Debug.Log($"[GameSessionController_GRIP] Recalibrated. New origin: {origin}");

        // Ensure calibration screen text stays hidden
        if (calibrationText) calibrationText.gameObject.SetActive(false);

        // Update start sphere
        float diameter = trialController.StartRadius * 2f;
        if (startSphere != null)
        {
            startSphere.transform.position   = origin;
            startSphere.transform.localScale = new Vector3(diameter, diameter, diameter);
        }

        // Update target sphere and all trial configs with new origin
        if (experimentingMode)
        {
            BuildExperimentTrial(origin);
            if (targetSphere != null)
            {
                float targetDiameter = previewCylinderRadiusCm / 100f * 2f;
                targetSphere.transform.position   = experimentTrial.targetPos;
                targetSphere.transform.localScale = new Vector3(targetDiameter, targetDiameter, targetDiameter);
            }
            RunExperimentTrial();
        }
        else
        {
            // Rebuild trials from store with new origin and restart from trial 1
            if (TryBuildTrialsFromStore(origin))
            {
                currentIndex = -1;
                Debug.Log("[GameSessionController_GRIP] Trials rebuilt with new origin.");
                StartNextTrial();
            }
        }

        StartCoroutine(ShowStatusThenClear("Recalibrated.", 2f));
    }

    System.Collections.IEnumerator ShowStatusThenClear(string msg, float delay)
    {
        if (statusText) statusText.text = msg;
        yield return new WaitForSeconds(delay);
        if (statusText) statusText.text = "";
    }

    // ── Experimenting mode ───────────────────────────────────────────

    GripTrialConfig experimentTrial;
    int experimentTrialCounter = 0;

    void BuildExperimentTrial(Vector3 origin)
    {
        // Base config stores only the start position; target geometry is per-entry.
        // Preview sphere uses global preview values (calibration reference only).
        experimentTrial = new GripTrialConfig
        {
            trialIndex   = 0,
            targetId     = 0,
            startPos     = origin,
            targetPos    = ComputeTargetPos(origin, previewAngleDeg, previewDistanceCm),
            targetRadius = trialController.StartRadius,
            handMode     = 1,
            ttlEnabled   = false,
            ttlOffsetMs  = 0f,
            ttl2OffsetMs = 0f,
            instruction  = 1
        };

        Debug.Log($"[GameSessionController_GRIP] Experimenting mode. Origin={origin}");
    }

    Vector3 ComputeTargetPos(Vector3 origin, float angleDeg, float distanceCm)
    {
        float angleRad = angleDeg * Mathf.Deg2Rad;
        float distM    = distanceCm / 100f;
        return new Vector3(
            origin.x + distM * Mathf.Cos(angleRad),
            origin.y,
            origin.z + distM * Mathf.Sin(angleRad)
        );
    }

    void RunExperimentTrial()
    {
        experimentTrialCounter++;
        if (trialCounterText) trialCounterText.text = $"EXP {experimentTrialCounter}";

        // All trial parameters come from the cycled entry
        bool    ttlEnabled   = false;
        float   ttlOffsetMs  = 0f;
        float   ttl2OffsetMs = 0f;
        int     instruction  = 1;
        int     handMode     = 1;
        Vector3 targetPos    = experimentTrial.targetPos;

        float targetRadius = experimentTrial.targetRadius;

        if (experimentTtlList != null && experimentTtlList.Count > 0)
        {
            var entry  = experimentTtlList[(experimentTrialCounter - 1) % experimentTtlList.Count];
            ttlEnabled   = entry.ttlEnabled;
            ttlOffsetMs  = entry.ttlOffsetMs;
            ttl2OffsetMs = entry.ttl2OffsetMs;
            instruction  = entry.instruction;
            handMode     = entry.handMode;
            targetPos    = ComputeTargetPos(experimentTrial.startPos, entry.angleDeg, entry.distanceCm);
            if (entry.cylinderRadiusCm > 0f) targetRadius = entry.cylinderRadiusCm / 100f;
        }

        if (leapInput != null)
        {
            leapInput.allowEitherHand = (handMode == 2);
            leapInput.useLeftHand     = (handMode == 0);
        }

        string ttl2Desc = !ttlEnabled ? "none" : (ttl2OffsetMs == 0f ? "SinglePulse" : $"{ttl2OffsetMs:F1}ms from Testing");
        Debug.Log($"[GameSessionController_GRIP] EXP {experimentTrialCounter} — " +
                  $"inst={instruction} hand={handMode} angle={experimentTtlList[(experimentTrialCounter-1)%experimentTtlList.Count].angleDeg}° dist={experimentTtlList[(experimentTrialCounter-1)%experimentTtlList.Count].distanceCm}cm " +
                  $"cylinderRadius={targetRadius*100f:F1}cm " +
                  $"ttlEnabled={ttlEnabled} Testing(Out2)={ttlOffsetMs:F1}ms Conditioning(Out1)={ttl2Desc}");

        trialController?.ConfigureAndBegin(
            experimentTrial.startPos,
            targetPos,
            targetRadius,
            ttlEnabled,
            ttlOffsetMs,
            ttl2OffsetMs,
            experimentTrialCounter,
            experimentTrial.targetId,
            handMode,
            instruction
        );
    }

    void ShowStatus(string msg)
    {
        if (statusText) statusText.text = msg;
        Debug.LogWarning($"[GameSessionController_GRIP] {msg}");
    }

    // ── Store → trial configs ────────────────────────────────────────

    bool TryBuildTrialsFromStore(Vector3 origin)
    {
        var store = RuntimeConfigStore.Instance;
        if (store == null)                              { Debug.LogWarning("[GRIP] No store.");         return false; }
        if (store.RwrTargets == null || store.RwrTargets.Count == 0) { Debug.LogWarning("[GRIP] No targets."); return false; }
        if (store.Trials     == null || store.Trials.Count     == 0) { Debug.LogWarning("[GRIP] No trials.");  return false; }

        // Build target lookup (polar → world)
        var targetMap = new Dictionary<int, (Vector3 pos, float radiusM)>();
        foreach (var t in store.RwrTargets)
        {
            float angleRad = t.angle_deg * Mathf.Deg2Rad;
            float distM    = t.distance_cm / 100f;
            float radiusM  = (t.cm / 100f) * 0.5f;

            Vector3 pos = new Vector3(
                origin.x + distM * Mathf.Cos(angleRad),
                origin.y,
                origin.z + distM * Mathf.Sin(angleRad)
            );

            targetMap[t.id] = (pos, radiusM);
        }

        var built = new List<GripTrialConfig>();
        foreach (var tr in store.Trials)
        {
            if (!int.TryParse((tr.targetId ?? "").Trim(), out int targetId))
            {
                Debug.LogWarning($"[GRIP] Trial {tr.trial}: invalid targetId '{tr.targetId}', skipping.");
                continue;
            }

            if (!targetMap.TryGetValue(targetId, out var tspec))
            {
                Debug.LogWarning($"[GRIP] Trial {tr.trial}: targetId {targetId} not found, skipping.");
                continue;
            }

            int handMode = ParseHand(tr.hand);
            if (handMode < 0)
            {
                Debug.LogWarning($"[GRIP] Trial {tr.trial}: invalid hand '{tr.hand}', skipping.");
                continue;
            }

            // NoPulse only if ts is "." or empty; cs empty/dot = SinglePulse (not NoPulse)
            bool ttlEnabled = ParseTtlEnabled(tr.ts);

            built.Add(new GripTrialConfig
            {
                trialIndex        = tr.trial,
                targetId          = targetId,
                startPos          = origin,              // all trials share the calibrated origin
                targetPos         = tspec.pos,
                targetRadius      = tspec.radiusM,
                startRadiusCm     = ParseFloat(tr.startRadiusCm),
                handMode          = handMode,
                ttlEnabled        = ttlEnabled,
                ttlOffsetMs       = ParseTtlOffset(tr.ts),
                ttl2OffsetMs      = -Mathf.Abs(ParseTtlOffset(tr.cs)),  // cs always non-positive (fires before Testing)
                instruction       = ParseInstruction(tr.instruction),
                holdDuration      = ParseFloat(tr.holdDuration),
                waitForGo         = ParseFloat(tr.waitForGo),
                executingDuration = ParseFloat(tr.executing)
            });
        }

        if (built.Count == 0) { Debug.LogWarning("[GRIP] No valid trials."); return false; }

        trials = built.ToArray();
        Debug.Log($"[GameSessionController_GRIP] Built {trials.Length} trials. Origin={origin}");
        return true;
    }

    // ── Parsers ──────────────────────────────────────────────────────

    int ParseHand(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 1;
        var t = s.Trim().ToLowerInvariant();
        if (int.TryParse(t, out int v) && v >= 0 && v <= 2) return v;
        if (t == "left"  || t == "l") return 0;
        if (t == "right" || t == "r") return 1;
        if (t == "either" || t == "both") return 2;
        return -1;
    }

    int ParseInstruction(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        if (int.TryParse(s.Trim(), out int v) && v >= 0 && v <= 2) return v;
        var t = s.Trim().ToLowerInvariant();
        if (t == "rest")  return 0;
        if (t == "reach") return 1;
        if (t == "reach+grasp" || t == "rg") return 2;
        return 0;
    }

    // "." or empty → TTL disabled (NoPulse); any number → enabled
    static bool ParseTtlEnabled(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        var t = s.Trim();
        if (t == ".") return false;
        return float.TryParse(t, System.Globalization.NumberStyles.Float,
                              CultureInfo.InvariantCulture, out _);
    }

    static float ParseTtlOffset(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0f;
        float.TryParse(s.Trim(), System.Globalization.NumberStyles.Float,
                       CultureInfo.InvariantCulture, out float v);
        return v;
    }

    static float ParseFloat(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0f;
        s = s.Trim().Replace(',', '.');
        float.TryParse(s, System.Globalization.NumberStyles.Float,
                       CultureInfo.InvariantCulture, out float v);
        return v;
    }
}
