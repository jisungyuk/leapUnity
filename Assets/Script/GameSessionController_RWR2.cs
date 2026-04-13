using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Globalization;
using TMPro;

/// <summary>
/// Manages the RWR session.
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
public class GameSessionController_RWR2 : MonoBehaviour
{
    [System.Serializable]
    public class RwrTrialConfig
    {
        public Vector3 startPos;
        public Vector3 targetPos;
        public float   targetRadius;
        public int     handMode;
        public bool    ttlEnabled;
        public float   ttlOffsetMs;
        public float   ttl2OffsetMs;
        public int     trialIndex;
        public int     targetId;
        public int     instruction;   // 0=REST, 1=REACH, 2=REACH+GRASP
    }

    [Header("References")]
    [SerializeField] TrialGameController_RWR2 trialController;
    [SerializeField] LeapFingerInput          leapInput;

    [Header("Hand Visualization")]
    [SerializeField] GameObject capsuleHands;   // full hand model — ON during calibration, OFF during trials

    [Header("Zone Visualization")]
    [SerializeField] GameObject startSphere;              // shown at calibration origin after SPACE
    [SerializeField] GameObject targetSphere;             // shown at default target position after SPACE
    [SerializeField] float      previewAngleDeg   = 90f;  // default target angle (0=right, 90=forward)
    [SerializeField] float      previewDistanceCm = 30f;  // default target distance in cm

    [Header("Experimenting Mode (used when no session data from MainMenu)")]
    [Tooltip("Force experimenting mode even if MainMenu session data exists")]
    [SerializeField] bool forceExperimentingMode = false;
    [Tooltip("Enable trial data logging in experimenting mode")]
    [SerializeField] bool experimentLogging = false;
    [Tooltip("Folder to save experimenting data (inside project)")]
    [SerializeField] string experimentDataPath = "C:/Users/Jisung Yuk/Documents/leapUnity/ExperimentData";
    [Tooltip("0=REST  1=REACH  2=REACH+GRASP")]
    [SerializeField] [Range(0,2)] int experimentInstruction = 1;
    [SerializeField] bool  experimentTtlEnabled   = true;
    [SerializeField] float experimentTtlOffsetMs  = 0f;
    [SerializeField] float experimentTtl2OffsetMs = 2.5f;
    [Tooltip("0=Left  1=Right  2=Either")]
    [SerializeField] [Range(0,2)] int experimentHandMode = 1;

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

    RwrTrialConfig[] trials;
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
                    Debug.Log("[GameSessionController_RWR2] Recalibration blocked — trial in progress.");
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

        if (stageText) stageText.text = "CALIBRATION";

        if (calibrationText)
            calibrationText.text =
                "CALIBRATION\n\n" +
                "Place your hand at the start marker\n" +
                "then press  SPACE";

        if (statusText) statusText.text = "";
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
            Debug.LogWarning("[GameSessionController_RWR2] Calibration failed — no hand detected.");
            return;
        }

        Vector3 origin = mcp.position;

        var store = RuntimeConfigStore.Instance;
        if (store != null)
        {
            store.rwrCalibrationOrigin = origin;
            store.rwrCalibrated        = true;
        }

        Debug.Log($"[GameSessionController_RWR2] Calibration origin set: {origin}");

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
            targetSphere.transform.position   = targetPos;
            targetSphere.transform.localScale = new Vector3(diameter, diameter, diameter);
            targetSphere.SetActive(true);
        }

        if (calibrationText) calibrationText.text = "";

        // Try to build trials from store (MainMenu session)
        // If forceExperimentingMode is checked, or no store data → experimenting mode
        // If launched from MainMenu, always use store data.
        // Otherwise (direct scene launch), forceExperimentingMode or no store data → experimenting.
        bool fromMenu = RuntimeConfigStore.Instance != null &&
                        RuntimeConfigStore.Instance.launchedFromMainMenu;
        if (RuntimeConfigStore.Instance != null)
            RuntimeConfigStore.Instance.launchedFromMainMenu = false; // consume the flag

        if (fromMenu)
        {
            if (!TryBuildTrialsFromStore(origin))
                Debug.LogWarning("[GameSessionController_RWR2] Launched from MainMenu but no store data found.");
        }
        else if (forceExperimentingMode || !TryBuildTrialsFromStore(origin))
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
                Debug.Log($"[GameSessionController_RWR2] Experiment data → {experimentDataPath}");
            }

            if (trialController != null)
                trialController.SetExperimentingMode(true);
        }

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
            Debug.LogWarning("[GameSessionController_RWR2] No trials.");
            return;
        }

        if (currentIndex >= trials.Length)
        {
            Debug.Log("[GameSessionController_RWR2] All trials complete.");
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
            cfg.instruction
        );

        Debug.Log($"[GameSessionController_RWR2] Trial {currentIndex + 1}/{trials.Length} " +
                  $"inst={cfg.instruction} target={cfg.targetId} ttl={(cfg.ttlEnabled ? $"{cfg.ttlOffsetMs}ms" : "none")} " +
                  $"startPos={cfg.startPos} targetPos={cfg.targetPos}");
    }

    void HandleTrialFinished()
    {
        Debug.Log($"[GameSessionController_RWR2] Trial {currentIndex + 1} finished.");
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
            Debug.LogWarning("[GameSessionController_RWR2] Recalibration failed — no hand detected.");
            return;
        }

        Vector3 origin = leapInput.indexMcp.position;

        var store = RuntimeConfigStore.Instance;
        if (store != null)
        {
            store.rwrCalibrationOrigin = origin;
            store.rwrCalibrated        = true;
        }

        Debug.Log($"[GameSessionController_RWR2] Recalibrated. New origin: {origin}");

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
                targetSphere.transform.position = experimentTrial.targetPos;
                targetSphere.transform.localScale = new Vector3(diameter, diameter, diameter);
            }
        }
        else
        {
            // Rebuild trials from store with new origin
            if (TryBuildTrialsFromStore(origin))
            {
                currentIndex = -1;
                Debug.Log("[GameSessionController_RWR2] Trials rebuilt with new origin.");
            }
        }
    }

    // ── Experimenting mode ───────────────────────────────────────────

    RwrTrialConfig experimentTrial;

    void BuildExperimentTrial(Vector3 origin)
    {
        float angleRad = previewAngleDeg * Mathf.Deg2Rad;
        float distM    = previewDistanceCm / 100f;
        Vector3 targetPos = new Vector3(
            origin.x + distM * Mathf.Cos(angleRad),
            origin.y,
            origin.z + distM * Mathf.Sin(angleRad)
        );

        experimentTrial = new RwrTrialConfig
        {
            trialIndex   = 0,
            targetId     = 0,
            startPos     = origin,
            targetPos    = targetPos,
            targetRadius = trialController.StartRadius,
            handMode     = experimentHandMode,
            ttlEnabled   = experimentTtlEnabled,
            ttlOffsetMs  = experimentTtlOffsetMs,
            ttl2OffsetMs = experimentTtl2OffsetMs,
            instruction  = experimentInstruction
        };

        Debug.Log($"[GameSessionController_RWR2] Experimenting mode. " +
                  $"Instruction={experimentInstruction} Angle={previewAngleDeg} Dist={previewDistanceCm}cm");
    }

    void RunExperimentTrial()
    {
        if (trialCounterText) trialCounterText.text = "EXP";

        if (leapInput != null)
        {
            leapInput.allowEitherHand = (experimentTrial.handMode == 2);
            leapInput.useLeftHand     = (experimentTrial.handMode == 0);
        }

        trialController?.ConfigureAndBegin(
            experimentTrial.startPos,
            experimentTrial.targetPos,
            experimentTrial.targetRadius,
            experimentTrial.ttlEnabled,
            experimentTrial.ttlOffsetMs,
            experimentTrial.ttl2OffsetMs,
            experimentTrial.trialIndex,
            experimentTrial.targetId,
            experimentTrial.handMode,
            experimentTrial.instruction
        );
    }

    void ShowStatus(string msg)
    {
        if (statusText) statusText.text = msg;
        Debug.LogWarning($"[GameSessionController_RWR2] {msg}");
    }

    // ── Store → trial configs ────────────────────────────────────────

    bool TryBuildTrialsFromStore(Vector3 origin)
    {
        var store = RuntimeConfigStore.Instance;
        if (store == null)                              { Debug.LogWarning("[RWR] No store.");         return false; }
        if (store.RwrTargets == null || store.RwrTargets.Count == 0) { Debug.LogWarning("[RWR] No RWR targets."); return false; }
        if (store.Trials     == null || store.Trials.Count     == 0) { Debug.LogWarning("[RWR] No trials.");      return false; }

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

        var built = new List<RwrTrialConfig>();
        foreach (var tr in store.Trials)
        {
            if (!int.TryParse((tr.targetId ?? "").Trim(), out int targetId))
            {
                Debug.LogWarning($"[RWR] Trial {tr.trial}: invalid targetId '{tr.targetId}', skipping.");
                continue;
            }

            if (!targetMap.TryGetValue(targetId, out var tspec))
            {
                Debug.LogWarning($"[RWR] Trial {tr.trial}: targetId {targetId} not found, skipping.");
                continue;
            }

            int handMode = ParseHand(tr.hand);
            if (handMode < 0)
            {
                Debug.LogWarning($"[RWR] Trial {tr.trial}: invalid hand '{tr.hand}', skipping.");
                continue;
            }

            built.Add(new RwrTrialConfig
            {
                trialIndex   = tr.trial,
                targetId     = targetId,
                startPos     = origin,              // all trials share the calibrated origin
                targetPos    = tspec.pos,
                targetRadius = tspec.radiusM,
                handMode     = handMode,
                ttlEnabled   = ParseTtlEnabled(tr.ttl1),
                ttlOffsetMs  = ParseTtlOffset(tr.ttl1),
                ttl2OffsetMs = ParseTtlOffset(tr.ttl2Offset),
                instruction  = ParseInstruction(tr.instruction)
            });
        }

        if (built.Count == 0) { Debug.LogWarning("[RWR] No valid trials."); return false; }

        trials = built.ToArray();
        Debug.Log($"[GameSessionController_RWR2] Built {trials.Length} trials. Origin={origin}");
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

    static bool ParseTtlEnabled(string s) =>
        !string.IsNullOrWhiteSpace(s) &&
        s.Trim().ToLowerInvariant() != "none" &&
        float.TryParse(s.Trim(), System.Globalization.NumberStyles.Float,
                       CultureInfo.InvariantCulture, out _);

    static float ParseTtlOffset(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0f;
        float.TryParse(s.Trim(), System.Globalization.NumberStyles.Float,
                       CultureInfo.InvariantCulture, out float v);
        return v;
    }
}
