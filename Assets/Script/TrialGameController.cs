using UnityEngine;
using TMPro;
using System;
using System.Collections.Generic;
using System.Collections;   // 코루틴용 (지금은 안 써도 되지만 남겨둠)

public class TrialGameController : MonoBehaviour, ITrialStateProvider
{
    public event Action OnTrialFinished;

    private enum TrialState
    {
        Idle,
        MoveToStart,
        HoldInStart,
        WaitForGo,
        MoveToTarget,
        Feedback,
        TrialDone
    }

    [Header("UI")]
    [SerializeField] TMP_Text instructionText;   // 화면 중앙 텍스트

    [Header("Core References")]
    [SerializeField] Transform startSphere;   // start zone
    [SerializeField] Transform targetSphere;  // target zone

    [Header("Finger Logic Transforms (from LeapFingerInput)")]
    [SerializeField] Transform finger1;       // index fingertip
    [SerializeField] Transform finger2;       // thumb fingertip
    [SerializeField] Transform indexMcp;      // index MCP joint

    [Header("Radii (meters)")]
    [SerializeField] float startRadius  = 0.03f;   // 3 cm
    [SerializeField] float targetRadius = 0.03f;   // 3 cm

    [Header("Timing (seconds)")]
    [SerializeField] float holdDuration     = 0.5f;  // MCP start 안에서 버티는 시간
    [SerializeField] float goDelay          = 2.0f;  // start green 이후 go까지
    [SerializeField] float moveTimeout      = 3.0f;  // go 이후 최대 이동 시간
    [SerializeField] float feedbackDuration = 0.5f;  // end position 보여주는 시간

    [Header("Visual Feedback (VF)")]
    [SerializeField] bool visualFeedback = true;    // VF=1 → true, VF=0 → false

    [Header("Rendering")]
    [SerializeField] Renderer startRenderer;
    [SerializeField] Renderer targetRenderer;
    [SerializeField] Color startIdleColor  = Color.gray;
    [SerializeField] Color startReadyColor = Color.green;
    [SerializeField] Color targetIdleColor = Color.gray;
    [SerializeField] Color targetGoColor   = Color.green;

    [Header("Cursor Objects")]
    [SerializeField] GameObject cursor1;   // index tip
    [SerializeField] GameObject cursor2;   // thumb tip
    [SerializeField] GameObject mcpCursor; // index MCP
    [SerializeField] bool showFinger1Cursor = false; // 리치: fingertip 커서는 기본 숨김
    [SerializeField] bool showFinger2Cursor = false;
    [SerializeField] bool showMcpCursor    = true;

    [Header("Audio")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip readyClip;
    [SerializeField] AudioClip goClip;
    [SerializeField] AudioClip endClip;

    [Header("Debug (optional)")]
    [SerializeField] TMP_Text debugText;

    [Header("Target hold")]
    [SerializeField] float targetHoldDuration = 1.0f; // 타겟 안에서 버텨야 하는 시간(초)

    [Header("TTL")]
    [SerializeField] float ttlOffsetMs = 0f;   // 이 trial의 TTL 오프셋(ms, +면 go 이후, -면 go 이전)
    bool  ttlEnabled   = false;               // TTL 쓸지 여부 (offset ≠ 0)
    bool  ttlPending   = false;               // 아직 쏘지 않았고, 예약만 된 상태
    bool  ttlFired     = false;               // 실제로 TTL이 나갔는지
    float ttlPlannedTime = 0f;                // Time.time 기준 발사 시각
    float ttlFiredTime   = -1f;               // 실제 발사 시각

    // TTL 관련 필드 근처에 같이 두면 보기 좋음
    [Header("TTL Visual Lamp")]
    [SerializeField] Renderer ttlLampRenderer;       // 작은 sphere의 Renderer
    [SerializeField] Color ttlLampOffColor = Color.black;
    [SerializeField] Color ttlLampOnColor  = Color.yellow;
    [SerializeField] float ttlLampOnDuration = 0.1f; // 몇 초 동안 켤지

    [SerializeField] Gettinghanddata handData;
    [SerializeField] LeapFingerInput leapInput;
    [SerializeField] TrialDataLogger dataLogger;


// Reaction time (Go → trial offset)
float movementTime = -1f;
float ttlLampTimer = 0f;


    // Target hold 체크용
    float inTargetTimer     = 0f;
    bool  finger1InsideLast = false;
    bool  finger2InsideLast = false;

    // 경계에 처음 닿았을 때 fingertip 위치
    Vector3 boundaryPos1;
    Vector3 boundaryPos2;

    bool successThisTrial = false;

    [Header("Trajectory (optional)")]
    [SerializeField] bool showTrajectory = true;   // Inspector에서 on/off
    [SerializeField] LineRenderer trailFinger1;
    [SerializeField] LineRenderer trailFinger2;
    [SerializeField] LineRenderer trailMcp;
    [SerializeField] int maxTrailPoints = 500;

    List<Vector3> trailF1 = new();
    List<Vector3> trailF2 = new();
    List<Vector3> trailM  = new();
    bool recordingTrail   = false;
    bool mcpInsideStartLast = false;

    // Internal state
    TrialState state = TrialState.Idle;

    float holdTimer     = 0f;
    float readyTime     = -1f;
    float goTime        = -1f;
    float feedbackTimer = 0f;

    bool freezeCursors    = false;
    bool notifiedFinished = false;

    Vector3 cursorPos1;
    Vector3 cursorPos2;
    Vector3 mcpPos;

    [Header("Start/Target Cylinder")]
    [SerializeField] float startHeight  = 0.05f;
    [SerializeField] float targetHeight = 0.05f;

    void Awake()
    {
        if (!startRenderer && startSphere)
            startRenderer = startSphere.GetComponentInChildren<Renderer>();
        if (!targetRenderer && targetSphere)
            targetRenderer = targetSphere.GetComponentInChildren<Renderer>();

        if (startSphere)
        {
            float d = startRadius * 2f;
            startSphere.localScale = new Vector3(d, startHeight, d);
        }
    }

    // GameSessionController에서 호출
    public void ConfigureAndBegin(
        Vector3 startPos,
        Vector3 targetPos,
        float targetRadiusMeters,
        bool vf,
        float ttlMs,
        int trialIndex,
        int targetId,
        int handMode
    )
    {
        if (startSphere)  startSphere.position  = startPos;
        if (targetSphere) targetSphere.position = targetPos;

        targetRadius   = targetRadiusMeters;
        visualFeedback = vf;

        if (targetSphere)
        {
            float diameter = targetRadiusMeters * 2f;
            targetSphere.localScale = new Vector3(diameter, targetHeight, diameter);
        }

        // TTL 설정
        ttlOffsetMs  = ttlMs;
        ttlEnabled   = true; // 0이면 TTL 없음
        ttlPending   = false;
        ttlFired     = false;
        ttlFiredTime = -1f;
        ttlPlannedTime = 0f;

        // Save meta for logger
        currentTrialIndex = trialIndex;
        currentTargetId   = targetId;
        currentHandMode   = handMode;

        InitializeTrial();
    }

    void InitializeTrial()
    {
        state           = TrialState.MoveToStart;
        holdTimer       = 0f;
        readyTime       = -1f;
        goTime          = -1f;
        feedbackTimer   = 0f;
        freezeCursors   = false;
        notifiedFinished = false;

        inTargetTimer      = 0f;
        finger1InsideLast  = false;
        finger2InsideLast  = false;
        successThisTrial   = false;
        recordingTrail     = false;
        mcpInsideStartLast = false;

        movementTime       = -1f;  


        trailF1.Clear();
        trailF2.Clear();
        trailM.Clear();

        if (trailFinger1) { trailFinger1.positionCount = 0; trailFinger1.enabled = false; }
        if (trailFinger2) { trailFinger2.positionCount = 0; trailFinger2.enabled = false; }
        if (trailMcp)     { trailMcp.positionCount     = 0; trailMcp.enabled     = false; }

        if (startSphere)  startSphere.gameObject.SetActive(true);
        if (targetSphere) targetSphere.gameObject.SetActive(true);

        SetStartColor(startIdleColor);
        SetTargetColor(targetIdleColor);

        SetCursorsVisible(true);

        if (instructionText) instructionText.text = "Ready";

        UpdateDebug();

        // Prepare data logger wiring only if logging is enabled
        if (ShouldLog() && leapInput)
        {
            dataLogger.Setup(leapInput.leapProvider, finger1, finger2, indexMcp, this);
        }
    }

    void Update()
    {
        // 커서 위치 업데이트
        UpdateCursorPositions();

        // --- TTL 램프 타이머 ---
        if (ttlLampTimer > 0f)
        {
            ttlLampTimer -= Time.deltaTime;
            if (ttlLampTimer <= 0f && ttlLampRenderer)
            {
                ttlLampRenderer.material.color = ttlLampOffColor;
            }
        }

        // --- TTL 예약 처리 ---
        if (ttlEnabled && ttlPending && !ttlFired && Time.time >= ttlPlannedTime)
        {
            FireTtlPulse();      // 여기에서 ttlFired, ttlFiredTime, 램프 On 처리
            ttlPending = false;
        }

        // --- 상태 머신 ---
        switch (state)
        {
            case TrialState.MoveToStart:
                Update_MoveToStart();
                break;
            case TrialState.HoldInStart:
                Update_HoldInStart();
                break;
            case TrialState.WaitForGo:
                Update_WaitForGo();
                break;
            case TrialState.MoveToTarget:
                Update_MoveToTarget();
                break;
            case TrialState.Feedback:
                Update_Feedback();
                break;
            case TrialState.TrialDone:
                Update_TrialDone();
                break;
        }

        // 디버그 텍스트
        UpdateDebug();
    }


    // ---------------------- State Updates ---------------------- //

    void Update_MoveToStart()
    {
        if (IsMcpInSphere(startSphere.position, startRadius))
        {
            state     = TrialState.HoldInStart;
            holdTimer = 0f;
        }
    }

    void Update_HoldInStart()
    {
        if (IsMcpInSphere(startSphere.position, startRadius))
        {
            holdTimer += Time.deltaTime;

            if (holdTimer >= holdDuration)
            {
                SetStartColor(startReadyColor);
                PlaySound(readyClip);

                if (instructionText) instructionText.text = "Reach";

                readyTime = Time.time;
                // Begin recording from Ready
                if (ShouldLog() && leapInput)
                {
                    long readyTs = leapInput.lastTimestampUs;
                    bool usedLeft = (leapInput != null && leapInput.useLeftHand);
                    dataLogger.BeginTrial(
                        currentTrialIndex,
                        currentTargetId,
                        currentHandMode,
                        usedLeft,
                        readyTs,
                        startSphere ? startSphere.position : Vector3.zero,
                        startRadius,
                        targetSphere ? targetSphere.position : Vector3.zero,
                        targetRadius,
                        holdDuration, goDelay, moveTimeout, feedbackDuration,
                        ttlOffsetMs,
                        readyTime,
                        -1f
                    );
                }

                // 여기서 Go 예정 시각과 TTL 발사 시각 계산
                if (ttlEnabled)
                {
                    float goPlannedTime = readyTime + goDelay;
                    float offsetSec     = ttlOffsetMs / 1000f;
                    ttlPlannedTime      = goPlannedTime + offsetSec;
                    ttlPending          = true;
                    ttlFired            = false;
                    ttlFiredTime        = -1f;
                }

                state = TrialState.WaitForGo;
            }
        }
        else
        {
            // start 범위 벗어나면 → false start, 전부 리셋
            holdTimer = 0f;
            SetStartColor(startIdleColor);
            SetTargetColor(targetIdleColor);

            ttlPending = false;

            if (instructionText) instructionText.text = "Return to starting position";

            state = TrialState.MoveToStart;
        }
    }

    void Update_WaitForGo()
    {
        // false start 체크
        if (!IsMcpInSphere(startSphere.position, startRadius))
        {
            holdTimer = 0f;
            readyTime = -1f;

            SetStartColor(startIdleColor);
            SetTargetColor(targetIdleColor);

            ttlPending = false;

            if (instructionText) instructionText.text = "Return to starting position";

            state = TrialState.MoveToStart;
            return;
        }

        // Go 시점 도달
        float elapsed = Time.time - readyTime;
        if (elapsed >= goDelay)
        {
            goTime = Time.time;

            SetTargetColor(targetGoColor);
            PlaySound(goClip);

            if (instructionText) instructionText.text = "Go";

            if (!visualFeedback)
                SetCursorsVisible(false);
            // TTL 처리
            ttlFired = false;

            float goPlannedTime = goTime;
            float offsetSec = ttlOffsetMs / 1000f;

            ttlPlannedTime = goPlannedTime + offsetSec;
            ttlPending = true;


            // trajectory 시작 준비
            mcpInsideStartLast = true;
            recordingTrail      = false;

            state = TrialState.MoveToTarget;

            if (ShouldLog())
                dataLogger.SetGoTime(goTime);
        }
    }

    void Update_MoveToTarget()
    {
        float elapsedSinceGo = Time.time - goTime;

        // MCP가 start 영역을 떠나는 순간 → 궤적 기록 시작
        bool mcpInsideNow = IsMcpInSphere(startSphere.position, startRadius);
        if (!recordingTrail && mcpInsideStartLast && !mcpInsideNow)
        {
            recordingTrail = true;
            AddTrailSample(); // 첫 점
        }
        mcpInsideStartLast = mcpInsideNow;

        // fingertip이 타겟 안/밖인지 체크
        bool inside1 = IsTipInTarget(finger1, targetSphere.position, targetRadius);
        bool inside2 = IsTipInTarget(finger2, targetSphere.position, targetRadius);
        bool mcpInsideTarget = IsMcpInSphere(targetSphere.position, targetRadius);

        // 경계 처음 넘을 때 경계 위치 기록
        if (inside1 && !finger1InsideLast)
            boundaryPos1 = ProjectToTargetBoundary(finger1.position, targetSphere.position, targetRadius);
        if (inside2 && !finger2InsideLast)
            boundaryPos2 = ProjectToTargetBoundary(finger2.position, targetSphere.position, targetRadius);

        // MCP가 타겟 안에 있을 때 hold 타이머 증가
        if (mcpInsideTarget)
        {
            inTargetTimer += Time.deltaTime;

            if (inTargetTimer >= targetHoldDuration)
            {
                successThisTrial = true;
                if (recordingTrail)
                    AddTrailSample();
                EnterFeedback(true);
                return;
            }
        }
        else
        {
            inTargetTimer = 0f;
        }

        finger1InsideLast = inside1;
        finger2InsideLast = inside2;

        if (recordingTrail)
            AddTrailSample();

        // 타임아웃 → 실패
        if (elapsedSinceGo >= moveTimeout)
        {
            successThisTrial = false;
            EnterFeedback(false);
        }
    }

    void Update_Feedback()
    {
        feedbackTimer += Time.deltaTime;
        if (feedbackTimer >= feedbackDuration)
        {
            if (startSphere)  startSphere.gameObject.SetActive(false);
            if (targetSphere) targetSphere.gameObject.SetActive(false);

            freezeCursors = false;
            if (!visualFeedback)
                SetCursorsVisible(true);

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

    // ---------------------- Helpers ---------------------- //

    void FireTtlPulse()
    {
        ttlFired     = true;
        ttlPending   = false;
        ttlFiredTime = Time.time;
        if (ShouldLog() && leapInput)
        {
            dataLogger.NoteTtlFired(leapInput.lastTimestampUs);
        }

        // TODO: 실제 TTL 출력 연결 (DAQ, LSL marker 등)
        Debug.Log($"[TrialGameController] TTL pulse fired (offset={ttlOffsetMs} ms) at t={ttlFiredTime:F3}");
        // 시각적 램프 켜기
        if (ttlLampRenderer)
        {
            ttlLampRenderer.material.color = ttlLampOnColor;
            ttlLampTimer = ttlLampOnDuration;
        }
    }

    void AddTrailSample()
    {
        if (!showTrajectory) return;
        if (maxTrailPoints > 0 && trailF1.Count >= maxTrailPoints)
            return;

        if (finger1 && showFinger1Cursor)   trailF1.Add(finger1.position);
        if (finger2 && showFinger2Cursor)   trailF2.Add(finger2.position);
        if (indexMcp && showMcpCursor)      trailM.Add(indexMcp.position);
    }

    bool IsMcpInSphere(Vector3 center, float radius)
    {
        if (!indexMcp) return false;

        float r2 = radius * radius;
        Vector3 p = indexMcp.position;
        float dx = p.x - center.x;
        float dz = p.z - center.z;

        return (dx * dx + dz * dz) <= r2;
    }

    bool IsTipInTarget(Transform tip, Vector3 center, float radius)
    {
        if (!tip) return false;

        Vector3 p = tip.position;
        float dx = p.x - center.x;
        float dz = p.z - center.z;
        return (dx * dx + dz * dz) <= radius * radius;
    }

    void SetStartColor(Color c)
    {
        if (startRenderer)
            startRenderer.material.color = c;
    }

    void SetTargetColor(Color c)
    {
        if (targetRenderer)
            targetRenderer.material.color = c;
    }

    void SetCursorsVisible(bool visible)
    {
        if (cursor1)   cursor1.SetActive(showFinger1Cursor && visible);
        if (cursor2)   cursor2.SetActive(showFinger2Cursor && visible);
        if (mcpCursor) mcpCursor.SetActive(showMcpCursor && visible);
    }

    void PlaySound(AudioClip clip)
    {
        if (!audioSource || !clip) return;
        audioSource.PlayOneShot(clip);
    }

    void UpdateCursorPositions()
    {
        if (!freezeCursors)
        {
            if (finger1)   cursorPos1 = finger1.position;
            if (finger2)   cursorPos2 = finger2.position;
            if (indexMcp)  mcpPos    = indexMcp.position;

            // MoveToTarget 동안, 타겟 안에 있을 때는 경계 위치로 고정
            if (state == TrialState.MoveToTarget && targetSphere)
            {
                if (finger1)
                {
                    bool inside1 = IsTipInTarget(finger1, targetSphere.position, targetRadius);
                    if (inside1 && boundaryPos1 != Vector3.zero)
                        cursorPos1 = boundaryPos1;
                }

                if (finger2)
                {
                    bool inside2 = IsTipInTarget(finger2, targetSphere.position, targetRadius);
                    if (inside2 && boundaryPos2 != Vector3.zero)
                        cursorPos2 = boundaryPos2;
                }
            }
        }

        ApplyCursorVisuals();
    }

    void ApplyCursorVisuals()
    {
        if (cursor1 && showFinger1Cursor)   cursor1.transform.position = cursorPos1;
        if (cursor2 && showFinger2Cursor)   cursor2.transform.position = cursorPos2;
        if (mcpCursor && showMcpCursor) mcpCursor.transform.position = mcpPos;
    }

    void UpdateDebug()
{
    if (!debugText) return;

    // --- Ready phase: 스타트가 초록 → 타겟 초록 전까지 0부터 증가 ---
    float readyPhase = 0f;
    if (readyTime > 0f)
    {
        if (goTime > 0f)
        {
            // 이미 Go가 나갔다면, ready~go 사이의 고정된 값
            readyPhase = Mathf.Max(0f, goTime - readyTime);
        }
        else
        {
            // 아직 Go 전이라면, 지금까지 경과 시간
            readyPhase = Mathf.Max(0f, Time.time - readyTime);
        }
    }

    // --- movementTime: 타겟 초록(Go) → trial offset 까지 ---
    float rtDisplay = 0f;
    if (goTime > 0f)
    {
        if (movementTime >= 0f)
        {
            // 이미 trial이 끝났으면 확정값
            rtDisplay = movementTime;
        }
        else
        {
            // 아직 진행 중이면 실시간으로 증가
            rtDisplay = Mathf.Max(0f, Time.time - goTime);
        }
    }

    // --- Target hold duration: 두 팁이 타겟 안에 머문 시간 ---
    // inTargetTimer 자체가 "현재 진입 후 머무는 시간"이라 이걸 그대로 표시
    float targetHoldDisplay = inTargetTimer;

    // --- TTL 상태 텍스트 (전에 쓰던 로직 유지) ---
    string ttlStatus;
    if (!ttlEnabled)
    {
        ttlStatus = "TTL: disabled";
    }
    else if (!ttlFired && ttlPending)
    {
        ttlStatus = $"TTL: pending (offset {ttlOffsetMs} ms)";
    }
    else if (ttlFired)
    {
        float dtMs = (ttlFiredTime - goTime) * 1000f;
        ttlStatus = $"TTL: fired ({dtMs:F1} ms from Go)";
    }
    else
    {
        ttlStatus = "TTL: waiting";
    }
    
    
    // ----- MCP 위치 텍스트 -----
    string mcpLine = "MCP: (no data)";
    if (handData != null)
    {
        // 이 trial에서 어떤 손을 쓰는지: leapInput.useLeftHand 기준
        bool useLeft = (leapInput != null && leapInput.useLeftHand);

        Vector3 mcpPos = useLeft ? handData.leftMcpPos
                                 : handData.rightMcpPos;

        mcpLine = $"MCP ({(useLeft ? "Left" : "Right")}): {mcpPos}";
    }

    // --- 최종 디버그 텍스트 ---
    debugText.text =
        
        $"State: {state}\n" +
        $"ReadyTime: {readyPhase:F3} s\n" +
        $"MovementTime:   {rtDisplay:F3} s\n" +
        $"TargetHold: {targetHoldDisplay:F3} s\n" +
        $"feedbackTimer: {feedbackTimer:F3} s\n" +
        $"success: {successThisTrial}\n" +
        ttlStatus + "\n" +
        mcpLine + "\n";
}


    Vector3 ProjectToTargetBoundary(Vector3 tipPos, Vector3 center, float radius)
    {
        float dx = tipPos.x - center.x;
        float dz = tipPos.z - center.z;
        float dist = Mathf.Sqrt(dx * dx + dz * dz);
        if (dist < 1e-6f)
        {
            return new Vector3(center.x + radius, tipPos.y, center.z);
        }

        float scale = radius / dist;
        float bx = center.x + dx * scale;
        float bz = center.z + dz * scale;

        return new Vector3(bx, tipPos.y, bz);
    }

    void EnterFeedback(bool success)
    {
        // Go 이후 trial offset까지 시간 저장 (한 번만)
        if (goTime > 0f && movementTime < 0f)
        movementTime = Time.time - goTime;
        
        recordingTrail = false;

        if (instructionText) instructionText.text = "";

        // 궤적 그리기
        if (showTrajectory)
        {
            if (success)
            {
                if (boundaryPos1 != Vector3.zero && showFinger1Cursor)
                    trailF1.Add(boundaryPos1);
                if (boundaryPos2 != Vector3.zero && showFinger2Cursor)
                    trailF2.Add(boundaryPos2);
            }

            if (trailFinger1 && showFinger1Cursor && trailF1.Count > 1)
            {
                trailFinger1.positionCount = trailF1.Count;
                trailFinger1.SetPositions(trailF1.ToArray());
                trailFinger1.enabled = true;
            }
            else if (trailFinger1)
            {
                trailFinger1.positionCount = 0;
                trailFinger1.enabled = false;
            }

            if (trailFinger2 && showFinger2Cursor && trailF2.Count > 1)
            {
                trailFinger2.positionCount = trailF2.Count;
                trailFinger2.SetPositions(trailF2.ToArray());
                trailFinger2.enabled = true;
            }
            else if (trailFinger2)
            {
                trailFinger2.positionCount = 0;
                trailFinger2.enabled = false;
            }

            if (trailMcp && showMcpCursor && trailM.Count > 1)
            {
                trailMcp.positionCount = trailM.Count;
                trailMcp.SetPositions(trailM.ToArray());
                trailMcp.enabled = true;
            }
            else if (trailMcp)
            {
                trailMcp.positionCount = 0;
                trailMcp.enabled = false;
            }
        }

        // cursor 로직
        freezeCursors = true;

        if (success)
        {
            cursorPos1 = boundaryPos1;
            cursorPos2 = boundaryPos2;
        }
        else
        {
            if (finger1) cursorPos1 = finger1.position;
            if (finger2) cursorPos2 = finger2.position;
        }

        if (indexMcp) mcpPos = indexMcp.position;

        SetCursorsVisible(true);
        ApplyCursorVisuals();

        PlaySound(endClip);

        feedbackTimer = 0f;
        state         = TrialState.Feedback;

        if (ShouldLog())
        {
            dataLogger.EndAndSave();
        }
    }

    void EnterFeedback()
    {
        EnterFeedback(false);
    }

    // ---------------------- Logging helpers ---------------------- //
    int currentTrialIndex = 0;
    int currentTargetId   = 0;
    int currentHandMode   = 1;

    public int GetStateCode()
    {
        return (int)state;
    }

    bool ShouldLog()
    {
        return dataLogger != null &&
               RuntimeConfigStore.Instance != null &&
               RuntimeConfigStore.Instance.enableTrialLogging;
    }
}
            // Update go time in logger (for header)
            // We'll re-begin not necessary; we can just ignore
