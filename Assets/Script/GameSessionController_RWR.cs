using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using Leap;   // for reading raw hand-presence while LeapFingerInput is disabled (paused)

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
public class GameSessionController_RWR : MonoBehaviour
{
    [System.Serializable]
    public class RwrTrialConfig
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
    [SerializeField] TrialGameController_RWR trialController;
    [SerializeField] LeapFingerInput          leapInput;
    [SerializeField] LabChartStatusChecker    labChartStatus;
    [SerializeField] LabChartFro              froController;

    [Header("Hand Visualization")]
    [SerializeField] GameObject capsuleHands;   // full hand model — ON during calibration, OFF during trials

    // Ultraleap's Capsule Hands prefab has separate "Capsule Hand Left"/"Capsule Hand Right"
    // children. Leap sometimes mis-identifies which physical hand is which, briefly rendering
    // the wrong-side model overlapping the real one — cosmetic only (interaction is already
    // restricted to the trial's chosen hand via LeapFingerInput), but confusing to look at.
    // For single-hand trials we force-hide the other side's model entirely.
    GameObject capsuleHandLeft;
    GameObject capsuleHandRight;
    bool       capsuleHandChildrenCached = false;
    int        currentHandModeRestriction = 2; // 0=Left, 1=Right, 2=Either (no restriction) — default matches calibration

    [Header("Zone Visualization")]
    [SerializeField] GameObject startSphere;              // shown at calibration origin after SPACE
    [SerializeField] GameObject targetSphere;             // shown at default target position after SPACE
    [SerializeField] float      previewAngleDeg   = 90f;  // default target angle (0=right, 90=forward)
    [SerializeField] float      previewDistanceCm = 30f;  // default target distance in cm

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
        public bool  ttlEnabled   = true;
        public float ttlOffsetMs  = 0f;    // Testing Stimulus: ms relative to Go cue
        public float ttl2OffsetMs = 0f;    // Conditioning Stimulus: ms relative to Testing; 0 = SinglePulse
        public int   instruction  = 1;     // 0=REST  1=REACH  2=REACH+GRASP
        public int   handMode     = 1;     // 0=Left  1=Right  2=Either
        public float angleDeg     = 90f;   // target angle from home (0=right, 90=forward)
        public float distanceCm   = 30f;   // target distance from home in cm
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

    RwrTrialConfig[] trials;

    // SHIFT+SPACE on the calibration screen confirms without LabChart — kinematic-only
    // recording for the rest of the session (no TTL, no FRO/stimulation). Once set, the
    // LabChart gate on SPACE and the R key are both permanently ignored for this session.
    bool labChartBypassed = false;
    public bool LabChartBypassed => labChartBypassed;

    // ── ESC pause ──────────────────────────────────────────────────
    // Absorbs the old P-key pause (TrialGameController_RWR.SetPaused — trial timer
    // freeze) and additionally stops Leap tracking + LabChart recording while paused.
    // Resuming re-arms LabChart (Preload+Bounce) before actually letting play continue.
    PauseOverlay pauseOverlay;
    bool isPaused   = false;
    bool isResuming = false;

    // ── Public info for overlay ──────────────────────────────────────
    public RwrTrialConfig CurrentTrial =>
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

        pauseOverlay = gameObject.AddComponent<PauseOverlay>();

        ShowCalibrationScreen();
    }

    void Update()
    {
        // If LabChart itself has closed, clear any stale Arming/Recording flags so a
        // fresh R press works cleanly once it's reopened, instead of being ignored.
        if (labChartStatus != null && !labChartStatus.IsOpen && froController != null)
            froController.ResetIfLabChartClosed();

        // ESC: enter pause, or (while already paused) begin the resume sequence.
        // Q: quit to menu — only while paused, so it can't be hit by accident mid-trial.
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (!isPaused) EnterPause();
            else if (!isResuming) BeginResume();
        }
        if (isPaused && Input.GetKeyDown(KeyCode.Q))
        {
            QuitToMenu();
            return;
        }

        // While paused/resuming, ignore everything else below (calibration input,
        // recalibration, visual mode, R/T LabChart controls, trial state) and just
        // keep the overlay's Leap/LabChart status lines live.
        if (isPaused)
        {
            RefreshPauseOverlayText();
            return;
        }

        if (sessionState == SessionState.Calibrating)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                ConfirmCalibration(shiftHeld);
            }

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
                    Debug.Log("[GameSessionController_RWR] Recalibration blocked — trial in progress.");
            }
        }

        // F key cycles visual mode: all → no hands → none → all
        if (Input.GetKeyDown(KeyCode.F) && sessionState == SessionState.Running)
        {
            visualMode = (visualMode + 1) % 3;
            ApplyVisualMode();
        }

        // R: (re)start LabChart recording (Preload+Bounce). T: stop it. Both work in any
        // session state — recording could need restarting mid-experiment, not just before
        // trials start. TryArmRecording()/StopRecording() no-op safely if already in that
        // state, so R and T can be pressed freely as a single "make sure it's recording" /
        // "make sure it's stopped" pair.
        if (Input.GetKeyDown(KeyCode.R))
            TryArmRecording();
        if (Input.GetKeyDown(KeyCode.T))
            TryStopRecording();
    }

    // ── ESC pause ────────────────────────────────────────────────────

    /// <summary>
    /// ESC (from not-paused): freezes the trial timers (absorbs the old P-key pause),
    /// stops Leap tracking (our own LeapFingerInput only) and LabChart recording, and
    /// shows the full-screen pause overlay.
    /// </summary>
    void EnterPause()
    {
        isPaused = true;

        if (trialController != null)
            trialController.SetPaused(true);
        if (leapInput != null)
            leapInput.enabled = false;
        if (froController != null)
            froController.StopRecording();

        RefreshPauseOverlayText();

        Debug.Log("[GameSessionController_RWR] Paused — trial timers frozen, Leap tracking off, LabChart stopped.");
    }

    /// <summary>
    /// Rebuilds the pause overlay's text every frame while paused/resuming — same
    /// Leap/LabChart status info as the calibration screen (UpdateCalibrationStatus()),
    /// plus the current instructions/title. Live so LabChart's Arming→Recording
    /// transition during ResumeSequence() actually shows up.
    /// </summary>
    void RefreshPauseOverlayText()
    {
        if (pauseOverlay == null) return;

        string title = isResuming ? "Resuming..." : "PAUSE";
        string body  = isResuming
            ? "<size=60%>Arming LabChart, please wait</size>"
            : "<size=60%>ESC: Resume     Q: Quit to Menu</size>";

        // Same wording as the calibration screen (UpdateCalibrationStatus()) — hand
        // detected or not, not a generic on/off toggle. Read the raw Leap frame
        // directly (not leapInput.hasIndexJointData) since LeapFingerInput itself is
        // disabled while paused, which would otherwise freeze this at a stale value.
        bool handDetected = false;
        if (leapInput != null && leapInput.leapProvider != null)
        {
            Frame frame = leapInput.leapProvider.CurrentFrame;
            handDetected = frame != null && frame.Hands != null && frame.Hands.Count > 0;
        }
        string leapLine = handDetected
            ? "<color=#44FF44>● Hand detected</color>"
            : "<color=#FF4444>○ No hand detected</color>";

        string labChartLine;
        if (labChartBypassed)
            labChartLine = "<color=#888888>LabChart: OFF (kinematic-only)</color>";
        else if (labChartStatus == null)
            labChartLine = "<color=#888888>LabChart: checker not assigned</color>";
        else if (!labChartStatus.IsOpen)
            labChartLine = "<color=#FF4444>✗ LabChart: OFF</color>";
        else if (froController != null && froController.IsRecording)
            labChartLine = "<color=#44FF44>● LabChart: ON — Recording</color>";
        else if (froController != null && froController.IsArming)
            labChartLine = "<color=#FFFF44>… LabChart: Arming</color>";
        else
            labChartLine = "<color=#FFFF44>○ LabChart: ON — idling</color>";

        pauseOverlay.Show($"{title}\n\n{body}\n\n{leapLine}\n{labChartLine}");
    }

    /// <summary>ESC (from paused): starts the resume sequence. See ResumeSequence().</summary>
    void BeginResume()
    {
        StartCoroutine(ResumeSequence());
    }

    /// <summary>
    /// Re-arms LabChart (Preload+Bounce) before actually letting play continue — the ~2.5s
    /// this takes is the "wait a few seconds" the user asked for. Leap tracking and the
    /// trial timers only resume once arming completes, so everything comes back in sync.
    /// </summary>
    IEnumerator ResumeSequence()
    {
        isResuming = true;
        RefreshPauseOverlayText();

        if (froController != null && !labChartBypassed)
            yield return StartCoroutine(froController.ArmSessionRecording());

        if (leapInput != null)
            leapInput.enabled = true;
        if (trialController != null)
            trialController.SetPaused(false);

        if (pauseOverlay != null)
            pauseOverlay.Hide();

        isPaused   = false;
        isResuming = false;
        Debug.Log("[GameSessionController_RWR] Resumed.");
    }

    /// <summary>Q (only while paused) — same behavior EscapeToMenu.cs used to provide directly on ESC.</summary>
    void QuitToMenu()
    {
        Screen.fullScreen = false;
        Screen.SetResolution(1920, 1080, false);
        SceneManager.LoadScene("MainMenu");
    }

    /// <summary>T — stops LabChart recording via LabChartFro.StopRecording(). Press R to resume.</summary>
    void TryStopRecording()
    {
        if (froController == null)
        {
            Debug.LogWarning("[GameSessionController_RWR] T pressed but froController not assigned.");
            return;
        }

        froController.StopRecording();
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

    /// <summary>
    /// Triggers ArmSessionRecording() (Preload+Bounce + StartSampling) on the R key.
    /// No-ops safely if already recording/arming, or if LabChart isn't open — R can be
    /// pressed freely as "make sure it's recording".
    /// </summary>
    void TryArmRecording()
    {
        if (labChartBypassed)
        {
            Debug.Log("[GameSessionController_RWR] R ignored — session is running without LabChart (kinematic-only, confirmed via SHIFT+SPACE).");
            return;
        }
        if (froController == null)
        {
            Debug.LogWarning("[GameSessionController_RWR] R pressed but froController not assigned.");
            return;
        }
        if (labChartStatus != null && !labChartStatus.IsOpen)
        {
            froController.LaunchLabChart();
            Debug.Log("[GameSessionController_RWR] LabChart not open — launching it. Once it's up, press R again to start recording.");
            return;
        }
        if (froController.IsRecording || froController.IsArming)
        {
            Debug.Log("[GameSessionController_RWR] Recording already armed/arming — ignoring repeat R press.");
            return;
        }

        StartCoroutine(froController.ArmSessionRecording());
    }

    void UpdateCalibrationStatus()
    {
        if (!calibrationText) return;

        // Leap Motion status
        bool handDetected = leapInput != null && leapInput.hasIndexJointData;
        string leapLine = handDetected
            ? "<color=#44FF44>● Hand detected</color>"
            : "<color=#FF4444>○ No hand detected</color>";

        // LabChart status — three states: OFF / ON-idling / ON-recording.
        // "Recording" reflects that Unity itself issued StartSampling successfully,
        // not a live read of LabChart's UI (LabChart's COM API has no way to query
        // current sampling state) — if recording stops for some other reason (manual
        // stop in LabChart, a crash), this will keep showing "Recording" until T is
        // pressed to sync it back to idling.
        string labChartLine;
        if (labChartStatus == null)
        {
            labChartLine = "<color=#888888>○ LabChart checker not assigned</color>";
        }
        else if (!labChartStatus.IsOpen)
        {
            labChartLine = "<color=#FF4444>✗ LabChart: OFF</color>  <color=#FFFF44>— press R to launch</color>";
        }
        else if (froController != null && froController.IsRecording)
        {
            labChartLine = "<color=#44FF44>● LabChart: ON — Recording</color>  <color=#888888>(T to stop)</color>";
        }
        else if (froController != null && froController.IsArming)
        {
            labChartLine = "<color=#FFFF44>… LabChart: Arming (please wait ~2.5s)</color>";
        }
        else
        {
            labChartLine = "<color=#FFFF44>○ LabChart: ON — idling</color>  <color=#FFFF44>— press R to start recording</color>";
        }

        calibrationText.text =
            "CALIBRATION\n\n" +
            "Place either hand at the home position\n" +
            "then press  SPACE\n\n" +
            $"Leap Motion:  {leapLine}\n" +
            $"LabChart:     {labChartLine}";
    }

    void ConfirmCalibration(bool bypassLabChart = false)
    {
        if (!labChartBypassed && froController != null && !froController.IsRecording)
        {
            if (bypassLabChart)
            {
                labChartBypassed = true;
                if (trialController != null) trialController.skipLabChart = true;
                Debug.LogWarning("[GameSessionController_RWR] Continuing WITHOUT LabChart (SHIFT+SPACE) — kinematic-only for this session, TTL/stimulation disabled.");
            }
            else
            {
                if (calibrationText)
                    calibrationText.text =
                        "Press  R  to start LabChart recording first\n\n" +
                        "(SPACE will be enabled once recording has started)\n\n" +
                        "<size=70%>or  SHIFT+SPACE  to continue WITHOUT LabChart\n(kinematic-only, no stimulation)</size>";
                Debug.LogWarning("[GameSessionController_RWR] SPACE blocked — LabChart recording not active. Press R first, or SHIFT+SPACE for kinematic-only.");
                return;
            }
        }

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
            Debug.LogWarning("[GameSessionController_RWR] Calibration failed — no hand detected.");
            return;
        }

        Vector3 origin = mcp.position;

        var store = RuntimeConfigStore.Instance;
        if (store != null)
        {
            store.rwrCalibrationOrigin = origin;
            store.rwrCalibrated        = true;
        }

        Debug.Log($"[GameSessionController_RWR] Calibration origin set: {origin}");

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
        Debug.Log($"[GameSessionController_RWR] === SESSION START DIAGNOSTICS ===\n" +
                  $"  launchedFromMainMenu : {fromMenu}\n" +
                  $"  Store trial count : {storeTrialCount}\n" +
                  $"  → Will use : {(fromMenu ? "STORE (MainMenu session)" : "EXPERIMENT TTL LIST")}");

        if (fromMenu)
        {
            if (!TryBuildTrialsFromStore(origin))
                Debug.LogWarning("[GameSessionController_RWR] Launched from MainMenu but no store data found.");
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
                Debug.Log($"[GameSessionController_RWR] Experiment data → {experimentDataPath}");
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
            Debug.LogWarning("[GameSessionController_RWR] No trials.");
            return;
        }

        if (currentIndex >= trials.Length)
        {
            Debug.Log("[GameSessionController_RWR] All trials complete.");
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
        ApplyHandVisualRestriction(cfg.handMode);

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

        Debug.Log($"[GameSessionController_RWR] Trial {currentIndex + 1}/{trials.Length} " +
                  $"inst={cfg.instruction} target={cfg.targetId} ttl={(cfg.ttlEnabled ? $"{cfg.ttlOffsetMs}ms" : "none")} " +
                  $"startPos={cfg.startPos} targetPos={cfg.targetPos}");
    }

    void HandleTrialFinished()
    {
        Debug.Log($"[GameSessionController_RWR] Trial {currentIndex + 1} finished.");
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

    /// <summary>
    /// Records which hand a single-hand trial restricts to (0=Left, 1=Right, 2=Either)
    /// and immediately applies it. LateUpdate() re-applies this every frame — see
    /// EnforceHandVisualRestriction() for why.
    /// </summary>
    void ApplyHandVisualRestriction(int handMode)
    {
        currentHandModeRestriction = handMode;

        if (!capsuleHandChildrenCached)
        {
            capsuleHandChildrenCached = true;
            if (capsuleHands != null)
            {
                // Find by Handedness (Leap.HandModelBase), not GameObject name — robust
                // against any renaming/prefab variation, unlike Transform.Find by string.
                foreach (var hm in capsuleHands.GetComponentsInChildren<HandModelBase>(true))
                {
                    if (hm.Handedness == Chirality.Left)  capsuleHandLeft  = hm.gameObject;
                    if (hm.Handedness == Chirality.Right) capsuleHandRight = hm.gameObject;
                }

                if (capsuleHandLeft == null || capsuleHandRight == null)
                    Debug.LogWarning("[GameSessionController_RWR] Could not find HandModelBase children (Left/Right) under capsuleHands — per-hand visual restriction disabled.");
            }
        }

        EnforceHandVisualRestriction();
    }

    /// <summary>
    /// Re-hides the non-selected hand's Capsule Hand model every frame (called from
    /// LateUpdate, after everything else has had a chance to run) for single-hand trials —
    /// something re-enables it on its own after a tracking loss/reacquisition and we
    /// couldn't pin down the exact cause, so this just wins every frame instead.
    /// SetActive is skipped when the state already matches, so this is effectively free
    /// on the (vast majority of) frames where nothing is trying to re-show it.
    /// </summary>
    void EnforceHandVisualRestriction()
    {
        if (capsuleHandLeft != null)
        {
            bool shouldBeActive = currentHandModeRestriction != 1; // hidden only when Right-only
            if (capsuleHandLeft.activeSelf != shouldBeActive)
                capsuleHandLeft.SetActive(shouldBeActive);
        }
        if (capsuleHandRight != null)
        {
            bool shouldBeActive = currentHandModeRestriction != 0; // hidden only when Left-only
            if (capsuleHandRight.activeSelf != shouldBeActive)
                capsuleHandRight.SetActive(shouldBeActive);
        }
    }

    void LateUpdate()
    {
        EnforceHandVisualRestriction();
    }

    // ── Recalibration ────────────────────────────────────────────────

    void Recalibrate()
    {
        if (leapInput == null || leapInput.indexMcp == null)
        {
            Debug.LogWarning("[GameSessionController_RWR] Recalibration failed — no hand detected.");
            return;
        }

        Vector3 origin = leapInput.indexMcp.position;

        var store = RuntimeConfigStore.Instance;
        if (store != null)
        {
            store.rwrCalibrationOrigin = origin;
            store.rwrCalibrated        = true;
        }

        Debug.Log($"[GameSessionController_RWR] Recalibrated. New origin: {origin}");

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
                targetSphere.transform.position = experimentTrial.targetPos;
                targetSphere.transform.localScale = new Vector3(diameter, diameter, diameter);
            }
            experimentTrialCounter--; // cancel out RunExperimentTrial's increment so we redo the same trial number
            RunExperimentTrial();
        }
        else
        {
            // Rebuild trials from store with new origin and restart from trial 1
            if (TryBuildTrialsFromStore(origin))
            {
                currentIndex = -1;
                Debug.Log("[GameSessionController_RWR] Trials rebuilt with new origin.");
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

    RwrTrialConfig experimentTrial;
    int experimentTrialCounter = 0;

    void BuildExperimentTrial(Vector3 origin)
    {
        // Base config stores only the start position; target geometry is per-entry.
        // Preview sphere uses global preview values (calibration reference only).
        experimentTrial = new RwrTrialConfig
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

        Debug.Log($"[GameSessionController_RWR] Experimenting mode. Origin={origin}");
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

        if (experimentTtlList != null && experimentTtlList.Count > 0)
        {
            var entry  = experimentTtlList[(experimentTrialCounter - 1) % experimentTtlList.Count];
            ttlEnabled   = entry.ttlEnabled;
            ttlOffsetMs  = entry.ttlOffsetMs;
            ttl2OffsetMs = entry.ttl2OffsetMs;
            instruction  = entry.instruction;
            handMode     = entry.handMode;
            targetPos    = ComputeTargetPos(experimentTrial.startPos, entry.angleDeg, entry.distanceCm);
        }

        if (leapInput != null)
        {
            leapInput.allowEitherHand = (handMode == 2);
            leapInput.useLeftHand     = (handMode == 0);
        }
        ApplyHandVisualRestriction(handMode);

        string ttl2Desc = !ttlEnabled ? "none" : (ttl2OffsetMs == 0f ? "SinglePulse" : $"{ttl2OffsetMs:F1}ms from Testing");
        Debug.Log($"[GameSessionController_RWR] EXP {experimentTrialCounter} — " +
                  $"inst={instruction} hand={handMode} angle={experimentTtlList[(experimentTrialCounter-1)%experimentTtlList.Count].angleDeg}° dist={experimentTtlList[(experimentTrialCounter-1)%experimentTtlList.Count].distanceCm}cm " +
                  $"ttlEnabled={ttlEnabled} Testing(Out2)={ttlOffsetMs:F1}ms Conditioning(Out1)={ttl2Desc}");

        trialController?.ConfigureAndBegin(
            experimentTrial.startPos,
            targetPos,
            experimentTrial.targetRadius,
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
        Debug.LogWarning($"[GameSessionController_RWR] {msg}");
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

            // NoPulse only if ts is "." or empty; cs empty/dot = SinglePulse (not NoPulse)
            bool ttlEnabled = ParseTtlEnabled(tr.ts);

            built.Add(new RwrTrialConfig
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

        if (built.Count == 0) { Debug.LogWarning("[RWR] No valid trials."); return false; }

        trials = built.ToArray();
        Debug.Log($"[GameSessionController_RWR] Built {trials.Length} trials. Origin={origin}");
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
