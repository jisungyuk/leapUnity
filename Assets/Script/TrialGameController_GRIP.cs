using UnityEngine;
using TMPro;
using System;
using System.Collections;
using System.IO.Ports;
using Leap.PhysicalHands;

/// <summary>
/// GRIP trial controller.
///
/// State machine:
///   MoveToStart → HoldInStart → ShowDirection → WaitForGo → Executing → Feedback → TrialDone
///
/// Instruction codes:  0 = REST,  1 = REACH,  2 = REACH+GRASP
/// False start rule:   zone exit before Go → full reset to MoveToStart
/// After Go:
///   REST        → MCP stays in start zone                    → GOOD
///   REACH / R+G → participant grabs the cylinder (Physical Hands onGrabEnter) → GOOD (early exit)
///                 timer expires without grab                  → BAD
/// </summary>
public class TrialGameController_GRIP : MonoBehaviour
{
    public event Action OnTrialFinished;

    // ── Instruction constants ───────────────────────────────────────
    const int INST_REST  = 0;
    const int INST_REACH = 1;
    const int INST_RG    = 2;

    // ── State machine ───────────────────────────────────────────────
    enum TrialState
    {
        Idle,
        MoveToStart,
        HoldInStart,
        ShowDirection,
        WaitForGo,
        Executing,
        Feedback,
        TrialDone
    }

    // ── Inspector ───────────────────────────────────────────────────

    [Header("UI")]
    [SerializeField] TMP_Text instructionText;
    [SerializeField] TMP_Text debugText;

    [Header("Core References")]
    [SerializeField] Transform startSphere;

    [Header("Target Cylinder")]
    [SerializeField] float    cylinderHeightM = 0.10f;   // world height of target cylinder (metres)
    [SerializeField] Material cylinderMaterial;           // optional material; white default if null

    [Header("Finger Transforms (from LeapFingerInput)")]
    [SerializeField] Transform indexTip;
    [SerializeField] Transform thumbTip;
    [SerializeField] Transform indexMcp;

    [Header("Radii (metres)")]
    [SerializeField] float startRadius  = 0.03f;
    [SerializeField] float targetRadius = 0.03f;
    public float StartRadius => startRadius;

    [Header("Timing (seconds)")]
    [SerializeField] float holdDuration      = 0.5f;
    [SerializeField] float goDelay           = 2.0f;
    [SerializeField] float executionDuration = 3.0f;
    [SerializeField] float feedbackDuration  = 1.0f;

    [Header("Rendering")]
    [SerializeField] Renderer startRenderer;
    [SerializeField] Color startIdleColor    = new Color(0.5f, 0.5f, 0.5f, 0.3f);
    [SerializeField] Color startReadyColor   = new Color(0f,   1f,   0f,   0.3f);
    [SerializeField] Color targetIdleColor   = new Color(0.5f, 0.5f, 0.5f, 1f);
    [SerializeField] Color targetActiveColor = new Color(1f,   1f,   0f,   1f);
    [SerializeField] Color targetGoodColor   = new Color(0f,   1f,   0f,   1f);
    [SerializeField] Color targetBadColor    = new Color(1f,   0f,   0f,   1f);

    [Header("Cursor Objects")]
    [SerializeField] GameObject mcpCursor;
    [SerializeField] GameObject indexTipCursor;
    [SerializeField] GameObject thumbTipCursor;
    [SerializeField] bool showMcpCursor      = true;
    [SerializeField] bool showIndexTipCursor = false;
    [SerializeField] bool showThumbTipCursor = false;

    [Header("Audio")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip   readyClip;
    [SerializeField] AudioClip   goClip;
    [SerializeField] AudioClip   goodClip;
    [SerializeField] AudioClip   badClip;

    [Header("TTL")]
    [SerializeField] float  ttlOffsetMs        = 0f;
    [SerializeField] float  ttl2OffsetMs       = 2.5f;
    [SerializeField] string ttlComPort         = "COM5";
    [SerializeField] int    ttlChannel         = 1;
    [SerializeField] float  ttlPulseDurationMs = 100f;
    [SerializeField] Renderer ttlLampRenderer;
    [SerializeField] Color    ttlLampOffColor  = Color.black;
    [SerializeField] Color    ttlLampOnColor   = Color.yellow;
    [SerializeField] float    ttlLampDuration  = 0.1f;

    [Header("LabChart FRO")]
    [SerializeField] LabChartFro froController;

    [Header("Start Zone Height")]
    [SerializeField] float startHeight = 0.05f;

    [Header("Hand Visualization")]
    [SerializeField] FreezeProvider freezeProvider;

    [Header("References")]
    [SerializeField] LeapFingerInput leapInput;
    [SerializeField] Gettinghanddata handData;
    [SerializeField] TrialDataLogger dataLogger;

    // ── Private state ───────────────────────────────────────────────
    TrialState state = TrialState.Idle;

    int  currentInstruction = INST_REST;
    int  currentTrialIndex  = 0;
    int  currentTargetId    = 0;
    int  currentHandMode    = 1;

    float holdTimer      = 0f;
    float readyTime      = -1f;
    float goTime         = -1f;
    float execTimer      = 0f;
    float feedbackTimer  = 0f;
    float ttlLampTimer   = 0f;

    bool  ttlEnabled     = true;
    bool  ttlPending     = false;
    bool  ttlFired       = false;
    float ttlFiredTime   = -1f;
    float ttlPlannedTime = 0f;

    bool notifiedFinished = false;
    bool outcomeGood      = false;

    bool   paused          = false;
    float  pauseStartTime  = 0f;
    string textBeforePause = "";

    bool cursorsOverrideHidden = false;
    bool spheresOverrideHidden = false;

    SerialPort ttlPort = null;

    // ── Physical Hands grab tracking ────────────────────────────────
    GameObject        spawnedCylinder = null;
    GripTargetListener gripListener   = null;
    Renderer          targetRenderer  = null;
    bool              isGrabbed       = false;

    // ── Freeze / cylinder-follow state ──────────────────────────────
    bool       handFrozen   = false;
    Vector3    grabMcpPos   = Vector3.zero;
    Vector3    grabCylPos   = Vector3.zero;
    Quaternion grabCylRot   = Quaternion.identity;
    Quaternion grabHandRot  = Quaternion.identity;


    // ── Public entry point ──────────────────────────────────────────
    public void ConfigureAndBegin(
        Vector3 startPos,
        Vector3 targetPos,
        float   targetRadiusMeters,
        bool    ttlEnabledForTrial,
        float   ttlMs,
        float   ttl2Ms,
        int     trialIndex,
        int     targetId,
        int     handMode,
        int     instruction,
        float   perTrialHoldDuration      = 0f,
        float   perTrialWaitForGo         = 0f,
        float   perTrialExecutingDuration = 0f,
        float   perTrialStartRadiusCm     = 0f)
    {
        if (startSphere) startSphere.position = startPos;

        targetRadius       = targetRadiusMeters;
        ttlEnabled         = ttlEnabledForTrial;
        ttlOffsetMs        = ttlMs;
        ttl2OffsetMs       = ttl2Ms;
        currentTrialIndex  = trialIndex;
        currentTargetId    = targetId;
        currentHandMode    = handMode;
        currentInstruction = Mathf.Clamp(instruction, 0, 2);

        if (perTrialHoldDuration      > 0f) holdDuration      = perTrialHoldDuration;
        if (perTrialWaitForGo         > 0f) goDelay           = perTrialWaitForGo;
        if (perTrialExecutingDuration > 0f) executionDuration = perTrialExecutingDuration;
        if (perTrialStartRadiusCm     > 0f) startRadius       = perTrialStartRadiusCm / 100f;

        if (startSphere)
            startSphere.localScale = Vector3.one * (startRadius * 2f);

        SpawnCylinder(targetPos, targetRadiusMeters);

        InitTrial();
    }

    void InitTrial()
    {
        state            = TrialState.MoveToStart;
        holdTimer        = 0f;
        readyTime        = -1f;
        goTime           = -1f;
        execTimer        = 0f;
        feedbackTimer    = 0f;
        ttlPending       = false;
        ttlFired         = false;
        ttlFiredTime     = -1f;
        ttlPlannedTime   = 0f;
        notifiedFinished = false;
        outcomeGood      = false;
        isGrabbed        = false;
        UnfreezeHand();

        if (!spheresOverrideHidden)
        {
            if (startSphere)     startSphere.gameObject.SetActive(true);
            if (spawnedCylinder) spawnedCylinder.SetActive(true);
        }

        SetStartColor(startIdleColor);
        SetTargetColor(targetIdleColor);
        SetCursors(true);

        if (instructionText)
            instructionText.text = "Put your hand on home position";

        if (ShouldLog() && leapInput)
            dataLogger.Setup(leapInput.leapProvider, indexTip, thumbTip, indexMcp, null);
    }

    void Awake()
    {
        if (!startRenderer && startSphere)
            startRenderer = startSphere.GetComponentInChildren<Renderer>();

        OpenTtlPort();
    }

    void OnDestroy()
    {
        CleanupCylinder();
        CloseTtlPort();
    }

    void OpenTtlPort()
    {
        if (string.IsNullOrEmpty(ttlComPort)) return;
        try
        {
            ttlPort = new SerialPort(ttlComPort, 115200);
            ttlPort.Open();
            ttlPort.Write(new byte[] { 0 }, 0, 1);
            Debug.Log($"[TrialGameController_GRIP] TTL port {ttlComPort} opened.");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[TrialGameController_GRIP] Could not open TTL port {ttlComPort}: {e.Message}");
            ttlPort = null;
        }
    }

    void CloseTtlPort()
    {
        if (ttlPort != null && ttlPort.IsOpen)
        {
            try { ttlPort.Write(new byte[] { 0 }, 0, 1); } catch { }
            ttlPort.Close();
        }
        ttlPort = null;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
            TogglePause();

        if (paused)
        {
            UpdateCursors();
            UpdateDebug();
            return;
        }

        if (ttlLampTimer > 0f)
        {
            ttlLampTimer -= Time.deltaTime;
            if (ttlLampTimer <= 0f && ttlLampRenderer)
                ttlLampRenderer.material.color = ttlLampOffColor;
        }

        if (ttlPending && !ttlFired && Time.time >= ttlPlannedTime)
            FireTtlPulse();

        switch (state)
        {
            case TrialState.MoveToStart:   Update_MoveToStart();   break;
            case TrialState.HoldInStart:   Update_HoldInStart();   break;
            case TrialState.ShowDirection: Update_ShowDirection();  break;
            case TrialState.WaitForGo:     Update_WaitForGo();     break;
            case TrialState.Executing:     Update_Executing();     break;
            case TrialState.Feedback:      Update_Feedback();      break;
            case TrialState.TrialDone:     Update_TrialDone();     break;
        }

        if (handFrozen && spawnedCylinder != null)
            UpdateCylinderFollow();

        UpdateCursors();
        UpdateDebug();
    }

    // ── State handlers ──────────────────────────────────────────────

    void Update_MoveToStart()
    {
        if (AllFingersInStart())
        {
            state     = TrialState.HoldInStart;
            holdTimer = 0f;
            if (instructionText) instructionText.text = "+";
        }
    }

    void Update_HoldInStart()
    {
        if (!AllFingersInStart())
        {
            ResetToMoveToStart();
            return;
        }

        holdTimer += Time.deltaTime;
        if (holdTimer >= holdDuration)
        {
            SetStartColor(startReadyColor);
            PlaySound(readyClip);
            readyTime = Time.time;

            if (ShouldLog() && leapInput)
            {
                dataLogger.BeginTrial(
                    currentTrialIndex, currentTargetId, currentHandMode,
                    leapInput.useLeftHand, leapInput.lastTimestampUs,
                    startSphere ? startSphere.position : Vector3.zero, startRadius,
                    spawnedCylinder ? spawnedCylinder.transform.position : Vector3.zero, targetRadius,
                    holdDuration, goDelay, executionDuration, feedbackDuration,
                    ttlOffsetMs, readyTime, -1f
                );
            }

            state = TrialState.ShowDirection;
            ShowDirectionCue();
        }
    }

    void Update_ShowDirection()
    {
        if (!AllFingersInStart())
        {
            ResetToMoveToStart();
            return;
        }

        if (Time.time - readyTime >= goDelay)
        {
            goTime = Time.time;
            EnterGo();
        }
    }

    void Update_WaitForGo()
    {
        // Kept for potential future use
    }

    void Update_Executing()
    {
        execTimer += Time.deltaTime;

        if (currentInstruction == INST_REST)
        {
            if (!AllFingersInStart())
            {
                if (instructionText)
                    instructionText.text = "REST — please return to home position";
            }
            else
            {
                if (instructionText)
                    instructionText.text = "+\n<color=#888888><size=65%><i>Rest</i></size></color>";
            }
        }

        // REACH / R+G: proximity triggers hand freeze + cylinder coupling (no early exit)
        if (currentInstruction != INST_REST)
        {
            if (!isGrabbed && CheckProximity())
            {
                isGrabbed = true;
                FreezeHand();
            }
        }

        if (execTimer >= executionDuration)
        {
            EvaluateOutcome();
            EnterFeedback();
        }
    }

    void Update_Feedback()
    {
        feedbackTimer += Time.deltaTime;
        if (feedbackTimer >= feedbackDuration)
        {
            if (startSphere) startSphere.gameObject.SetActive(false);
            CleanupCylinder();

            if (ShouldLog()) dataLogger.EndAndSave();

            state = TrialState.TrialDone;
        }
    }

    void Update_TrialDone()
    {
        if (!notifiedFinished)
        {
            notifiedFinished = true;
            OnTrialFinished?.Invoke();
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────

    void ShowDirectionCue()
    {
        string instrLabel = currentInstruction switch
        {
            INST_REST  => "<i>Rest</i>",
            INST_REACH => "<i>Reach & Grasp</i>",
            INST_RG    => "<i>Reach & Grasp</i>",
            _          => "<i>Rest</i>"
        };

        if (instructionText)
            instructionText.text = $"+\n<color=#888888><size=65%>{instrLabel}</size></color>";
        PlaySound(readyClip);

        float goPlanned = readyTime + goDelay;
        ttlPlannedTime  = goPlanned - 0.5f;
        ttlPending      = true;
        ttlFired        = false;
        ttlFiredTime    = -1f;

        if (froController != null)
        {
            froController.CancelPrepare();

            if (!ttlEnabled)
            {
                froController.activeCoroutine = StartCoroutine(froController.PrepareNoPulse());
            }
            else
            {
                float out1Abs     = 500f + ttlOffsetMs + ttl2OffsetMs;
                float out2Abs     = 500f + ttlOffsetMs;
                bool  doublePulse = ttl2OffsetMs != 0f;

                if (doublePulse && (out1Abs < 0f || out1Abs >= 9900f))
                    Debug.LogWarning($"[TrialGameController_GRIP] Output1 absolute delay out of range: {out1Abs:F1}ms.");
                else if (out2Abs < 0f || out2Abs >= 9900f)
                    Debug.LogWarning($"[TrialGameController_GRIP] Output2 absolute delay out of range: {out2Abs:F1}ms.");
                else
                    froController.activeCoroutine = StartCoroutine(froController.PrepareOutputs(out1Abs, out2Abs, doublePulse));
            }
        }
    }

    void EnterGo()
    {
        if (currentInstruction != INST_REST)
        {
            if (instructionText) instructionText.text = "<color=#00CC00>GO</color>";
        }
        SetTargetColor(targetActiveColor);
        PlaySound(goClip);

        if (!ttlFired)
        {
            ttlPlannedTime = goTime - 0.5f;
            ttlPending     = true;
        }

        if (ShouldLog()) dataLogger.SetGoTime(goTime);

        execTimer = 0f;
        state     = TrialState.Executing;
    }

    void EvaluateOutcome()
    {
        if (currentInstruction == INST_REST)
        {
            outcomeGood = AllFingersInStart();
            return;
        }
        // Success: grabbed AND pinch gap < cylinder diameter AND both tips inside cylinder
        outcomeGood = isGrabbed && IsGripSuccessful();
    }

    void EnterFeedback()
    {
        if (instructionText)
            instructionText.text = outcomeGood
                ? "<color=#00CC00>GOOD</color>"
                : "<color=#FF3333>BAD</color>";

        SetTargetColor(outcomeGood ? targetGoodColor : targetBadColor);
        PlaySound(outcomeGood ? goodClip : badClip);
        feedbackTimer = 0f;
        state         = TrialState.Feedback;
    }

    void TogglePause()
    {
        paused = !paused;
        if (paused)
        {
            pauseStartTime  = Time.time;
            textBeforePause = instructionText ? instructionText.text : "";
            if (instructionText) instructionText.text = "<color=#FFFF44>PAUSE</color>";
        }
        else
        {
            float elapsed = Time.time - pauseStartTime;
            if (readyTime      > 0f) readyTime      += elapsed;
            if (goTime         > 0f) goTime         += elapsed;
            if (ttlPlannedTime > 0f) ttlPlannedTime += elapsed;

            if (instructionText) instructionText.text = textBeforePause;
        }
    }

    void ResetToMoveToStart()
    {
        state     = TrialState.MoveToStart;
        holdTimer = 0f;
        readyTime = -1f;
        goTime    = -1f;
        execTimer = 0f;
        isGrabbed = false;
        ttlPending = false;
        UnfreezeHand();

        SetStartColor(startIdleColor);
        SetTargetColor(targetIdleColor);
        SetCursors(true);

        if (instructionText)
            instructionText.text = "Put your hand on home position";

        Debug.Log("[TrialGameController_GRIP] False start — reset to MoveToStart.");
    }

    void FireTtlPulse()
    {
        ttlFired     = true;
        ttlPending   = false;
        ttlFiredTime = Time.time;

        if (ShouldLog() && leapInput)
            dataLogger.NoteTtlFired(leapInput.lastTimestampUs);

        if (ttlLampRenderer)
        {
            ttlLampRenderer.material.color = ttlLampOnColor;
            ttlLampTimer = ttlLampDuration;
        }

        Debug.Log($"[TrialGameController_GRIP] TTL fired — {(ttlFiredTime - goTime) * 1000f:F1} ms from Go");

        if (!ttlEnabled && froController != null)
            StartCoroutine(froController.AppendCommentCoroutine($"Trial {currentTrialIndex}"));

        if (ttlPort != null && ttlPort.IsOpen)
        {
            byte channelByte = (byte)(1 << (ttlChannel - 1));
            try
            {
                ttlPort.Write(new byte[] { channelByte }, 0, 1);
                ttlPort.BaseStream.Flush();
                StartCoroutine(ResetTtlAfterDelay(ttlPulseDurationMs / 1000f));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TrialGameController_GRIP] TTL write failed: {e.Message}");
            }
        }
    }

    IEnumerator ResetTtlAfterDelay(float delaySeconds)
    {
        yield return new WaitForSecondsRealtime(delaySeconds);
        if (ttlPort != null && ttlPort.IsOpen)
        {
            try { ttlPort.Write(new byte[] { 0 }, 0, 1); ttlPort.BaseStream.Flush(); }
            catch (Exception e) { Debug.LogWarning($"[TrialGameController_GRIP] TTL reset failed: {e.Message}"); }
        }
    }

    // ── Start zone check ────────────────────────────────────────────

    // Returns true only when MCP, index tip, AND thumb tip are all within startRadius (XZ plane).
    bool AllFingersInStart()
    {
        if (!indexMcp || !indexTip || !thumbTip || !startSphere) return false;
        if (leapInput != null && !leapInput.hasIndexJointData) return false;

        Vector3 center = startSphere.position;
        float   r2     = startRadius * startRadius;
        return PtInStartXZ(indexMcp.position,  center, r2)
            && PtInStartXZ(indexTip.position,  center, r2)
            && PtInStartXZ(thumbTip.position,  center, r2);
    }

    bool PtInStartXZ(Vector3 p, Vector3 center, float r2)
    {
        float dx = p.x - center.x;
        float dz = p.z - center.z;
        return dx * dx + dz * dz <= r2;
    }

    // ── Cylinder spawning and Physical Hands grab detection ─────────

    void SpawnCylinder(Vector3 pos, float radiusM)
    {
        CleanupCylinder();

        spawnedCylinder      = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        spawnedCylinder.name = "GripTarget";
        spawnedCylinder.transform.position = pos;

        // Unity Cylinder default: radius 0.5 at scale.x=1, height 2 at scale.y=1
        // → radiusM desired: scale.x = radiusM * 2
        // → cylinderHeightM desired: scale.y = cylinderHeightM / 2
        spawnedCylinder.transform.localScale =
            new Vector3(radiusM * 2f, cylinderHeightM / 2f, radiusM * 2f);

        targetRenderer = spawnedCylinder.GetComponent<Renderer>();
        if (cylinderMaterial != null)
            targetRenderer.material = cylinderMaterial;

        // Trigger collider: Physical Hands cannot push it, proximity detection is code-based.
        var col = spawnedCylinder.GetComponent<CapsuleCollider>();
        if (col != null) col.isTrigger = true;

        var rb = spawnedCylinder.AddComponent<Rigidbody>();
        rb.isKinematic  = true;
        rb.useGravity   = false;
        rb.constraints  = RigidbodyConstraints.FreezeAll;

        // GripTargetListener implements IPhysicalHandGrab directly —
        // avoids the null UnityEvent fields that PhysicalHandEvents has at runtime.
        gripListener = spawnedCylinder.AddComponent<GripTargetListener>();
        gripListener.OnGrabEnterAction = OnGrabEnter;
        gripListener.OnGrabExitAction  = OnGrabExit;

        SetTargetColor(targetIdleColor);
    }

    // Returns true when any finger tip is within (targetRadius + 3 cm) of the cylinder axis (XZ plane).
    bool CheckProximity()
    {
        if (spawnedCylinder == null) return false;
        Vector3 cyl = spawnedCylinder.transform.position;
        float   thr = targetRadius;
        float   t2  = thr * thr;

        bool IndexClose() {
            if (indexTip == null) return false;
            float dx = indexTip.position.x - cyl.x;
            float dz = indexTip.position.z - cyl.z;
            return dx * dx + dz * dz <= t2;
        }
        bool ThumbClose() {
            if (thumbTip == null) return false;
            float dx = thumbTip.position.x - cyl.x;
            float dz = thumbTip.position.z - cyl.z;
            return dx * dx + dz * dz <= t2;
        }
        return IndexClose() && ThumbClose();
    }

    void FreezeHand()
    {
        if (handFrozen) return;
        handFrozen = true;

        bool useLeft = leapInput != null && leapInput.useLeftHand;

        grabMcpPos  = indexMcp != null ? indexMcp.position : Vector3.zero;
        grabHandRot = handData != null
            ? (useLeft ? handData.leftPalmRot : handData.rightPalmRot)
            : Quaternion.identity;
        grabCylPos  = spawnedCylinder != null ? spawnedCylinder.transform.position : Vector3.zero;
        grabCylRot  = spawnedCylinder != null ? spawnedCylinder.transform.rotation : Quaternion.identity;

        freezeProvider?.Freeze(grabMcpPos);

        Debug.Log($"[TrialGameController_GRIP] Hand frozen at mcp={grabMcpPos:F3}");
    }

    void UnfreezeHand()
    {
        handFrozen = false;
        freezeProvider?.Unfreeze();
    }

    void UpdateCylinderFollow()
    {
        if (indexMcp == null || spawnedCylinder == null) return;

        bool useLeft = leapInput != null && leapInput.useLeftHand;
        Quaternion currentPalmRot = handData != null
            ? (useLeft ? handData.leftPalmRot : handData.rightPalmRot)
            : Quaternion.identity;

        Vector3    mcpDelta = indexMcp.position - grabMcpPos;
        Quaternion rotDelta = currentPalmRot * Quaternion.Inverse(grabHandRot);

        freezeProvider?.UpdateTransform(mcpDelta, rotDelta);

        spawnedCylinder.transform.position = grabCylPos + mcpDelta;
        spawnedCylinder.transform.rotation = rotDelta * grabCylRot;
    }

    // Returns true when pinch gap < cylinder diameter AND both tips are inside the cylinder volume.
    bool IsGripSuccessful()
    {
        if (spawnedCylinder == null || indexTip == null || thumbTip == null) return false;

        float pinchDist = Vector3.Distance(indexTip.position, thumbTip.position);
        if (pinchDist >= targetRadius * 2f) return false;

        Vector3 cyl   = spawnedCylinder.transform.position;
        float   r2    = targetRadius * targetRadius;
        float   halfH = cylinderHeightM / 2f;

        bool TipInside(Vector3 tip)
        {
            float dx = tip.x - cyl.x;
            float dz = tip.z - cyl.z;
            if (dx * dx + dz * dz > r2) return false;
            return Mathf.Abs(tip.y - cyl.y) <= halfH;
        }

        return TipInside(indexTip.position) && TipInside(thumbTip.position);
    }

    void CleanupCylinder()
    {
        UnfreezeHand();

        if (gripListener != null)
        {
            gripListener.OnGrabEnterAction = null;
            gripListener.OnGrabExitAction  = null;
            gripListener = null;
        }

        if (spawnedCylinder != null)
        {
            Destroy(spawnedCylinder);
            spawnedCylinder = null;
        }

        targetRenderer = null;
    }

    void OnGrabEnter(ContactHand hand)
    {
        isGrabbed = true;
        Debug.Log($"[TrialGameController_GRIP] Grab detected — {hand.Handedness} hand, trial {currentTrialIndex}");
    }

    void OnGrabExit(ContactHand hand)
    {
        isGrabbed = false;
    }

    // ── Visuals ──────────────────────────────────────────────────────

    void SetStartColor(Color c)  { if (startRenderer)  SetTransparentColor(startRenderer.material,  c); }
    void SetTargetColor(Color c) { if (targetRenderer) SetOpaqueColor(targetRenderer.material, c); }

    void SetOpaqueColor(Material mat, Color c)
    {
        mat.SetFloat("_Mode", 0);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
        mat.SetInt("_ZWrite", 1);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = -1;
        mat.color = c;
    }

    void SetTransparentColor(Material mat, Color c)
    {
        mat.SetFloat("_Mode", 3);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        mat.color = c;
    }

    void SetCursors(bool visible)
    {
        if (cursorsOverrideHidden) visible = false;
        if (mcpCursor)       mcpCursor.SetActive(showMcpCursor && visible);
        if (indexTipCursor)  indexTipCursor.SetActive(showIndexTipCursor && visible);
        if (thumbTipCursor)  thumbTipCursor.SetActive(showThumbTipCursor && visible);
    }

    public void SetCursorsVisible(bool visible)
    {
        cursorsOverrideHidden = !visible;
        if (mcpCursor)       mcpCursor.SetActive(visible && showMcpCursor);
        if (indexTipCursor)  indexTipCursor.SetActive(visible && showIndexTipCursor);
        if (thumbTipCursor)  thumbTipCursor.SetActive(visible && showThumbTipCursor);
    }

    public void SetSpheresVisible(bool visible)
    {
        spheresOverrideHidden = !visible;
        if (startSphere)     startSphere.gameObject.SetActive(visible);
        if (spawnedCylinder) spawnedCylinder.SetActive(visible);
    }

    public void SetInstructionFontSize(float size)
    {
        if (instructionText) instructionText.fontSize = size;
    }

    public void SetExperimentingMode(bool value)
    {
        if (dataLogger) dataLogger.SetExperimentingMode(value);
    }

    void UpdateCursors()
    {
        if (mcpCursor      && showMcpCursor      && indexMcp) mcpCursor.transform.position      = indexMcp.position;
        if (indexTipCursor && showIndexTipCursor && indexTip) indexTipCursor.transform.position = indexTip.position;
        if (thumbTipCursor && showThumbTipCursor && thumbTip) thumbTipCursor.transform.position = thumbTip.position;
    }

    void PlaySound(AudioClip clip)
    {
        if (!audioSource || !clip) return;
        audioSource.PlayOneShot(clip);
    }

    // ── Debug overlay ────────────────────────────────────────────────

    void UpdateDebug()
    {
        if (!debugText) return;

        string instrName = currentInstruction switch
        {
            INST_REST  => "REST",
            INST_REACH => "REACH",
            INST_RG    => "REACH+GRASP",
            _          => "?"
        };

        string ttlStatus = !ttlEnabled
            ? "TTL: none"
            : ttlFired
                ? $"TTL: fired ({(ttlFiredTime - goTime) * 1000f:F1} ms from Go)"
                : ttlPending
                    ? $"TTL: pending (offset {ttlOffsetMs} ms)"
                    : "TTL: waiting";

        string mcpLine = "MCP: (no data)";
        if (handData != null)
        {
            bool useLeft = leapInput != null && leapInput.useLeftHand;
            Vector3 mcp  = useLeft ? handData.leftMcpPos : handData.rightMcpPos;
            mcpLine = $"MCP ({(useLeft ? "L" : "R")}): {mcp:F3}";
        }

        debugText.text =
            $"State: {state}\n" +
            $"Instruction: {instrName}\n" +
            $"Trial: {currentTrialIndex}  Target: {currentTargetId}\n" +
            $"Exec: {(state == TrialState.Executing ? execTimer : 0f):F2} / {executionDuration:F1} s\n" +
            $"Grabbed: {isGrabbed}\n" +
            $"Outcome: {(state >= TrialState.Feedback ? (outcomeGood ? "GOOD" : "BAD") : "-")}\n" +
            ttlStatus + "\n" +
            mcpLine;
    }

    // ── Logging helpers ───────────────────────────────────────────────

    bool ShouldLog() =>
        dataLogger != null &&
        RuntimeConfigStore.Instance != null &&
        RuntimeConfigStore.Instance.enableTrialLogging;

    public int GetStateCode() => (int)state;
}
