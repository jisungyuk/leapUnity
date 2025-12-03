using Leap;
using UnityEngine;

public class LeapFingerInput : MonoBehaviour
{
    [Header("Leap")]
    public LeapProvider leapProvider;   // LeapServiceProvider in your scene
    public bool useLeftHand = false;    // false = right hand, true = left hand
    public bool allowEitherHand = false;  // ★ 새 필드

    // ★ 추가: 이번 trial 동안 어느 손을 쓰는지 기억
    bool hasActiveHand = false;
    bool activeIsLeft = false;


    [Header("Finger Transforms (driven by Leap)")]
    public Transform finger1;           // index tip (for TrialGameController)
    public Transform finger2;           // thumb tip (for TrialGameController)
    public Transform indexMcp;          // index metacarpophalangeal joint (MCP)

    // Expose last device timestamp (microseconds) for logging/time alignment
    public long lastTimestampUs { get; private set; } = 0;

    private void OnEnable()
    {
        if (leapProvider != null)
            leapProvider.OnUpdateFrame += OnUpdateFrame;
    }

    private void OnDisable()
    {
        if (leapProvider != null)
            leapProvider.OnUpdateFrame -= OnUpdateFrame;
    }

        void OnUpdateFrame(Frame frame)
    {
        if (frame == null) return;
        lastTimestampUs = frame.Timestamp;
        if (finger1 == null || finger2 == null) return;

        Hand hand = null;

        // -------------------------------
        // ★ 1) either-hand 모드일 때
        // -------------------------------
        if (allowEitherHand)
        {
            Hand left  = frame.GetHand(Chirality.Left);
            Hand right = frame.GetHand(Chirality.Right);

            // 아직 이번 trial에서 쓸 손을 결정 안 했으면 → 첫 손을 고정
            if (!hasActiveHand)
            {
                if (left != null && right == null)
                {
                    activeIsLeft  = true;
                    hasActiveHand = true;
                }
                else if (right != null && left == null)
                {
                    activeIsLeft  = false;
                    hasActiveHand = true;
                }
                else if (left != null && right != null)
                {
                    // 둘 다 있으면 useLeftHand 설정에 따라 선택
                    activeIsLeft  = useLeftHand;
                    hasActiveHand = true;
                }
            }

            if (hasActiveHand)
            {
                // 선택된 손 기준으로 계속 읽기
                hand = frame.GetHand(activeIsLeft ? Chirality.Left : Chirality.Right);

                // 선택된 손이 안 보이면 → 손 사라진 것. 다른 손도 없으면 꺼두기.
                if (hand == null)
                {
                    Hand other = frame.GetHand(activeIsLeft ? Chirality.Right : Chirality.Left);
                    if (other != null)
                    {
                        // 필요하면 여기서 다른 손으로 스위치할 수도 있음.
                        // 지금은 "사라지면 멈춘다" 쪽이 더 안전해서 스위치 안 함.
                        SetFingerObjectsActive(false);
                        hasActiveHand = false;
                        return;
                    }
                    else
                    {
                        SetFingerObjectsActive(false);
                        hasActiveHand = false;
                        return;
                    }
                }
            }
            else
            {
                // 아직 어느 손도 안 보임
                SetFingerObjectsActive(false);
                return;
            }
        }
        // -------------------------------
        // ★ 2) 기존 한 손 모드 (left / right 고정)
        // -------------------------------
        else
        {
            hasActiveHand = false;  // 매 trial마다 새로 정하게
            hand = frame.GetHand(useLeftHand ? Chirality.Left : Chirality.Right);
            if (hand == null)
            {
                SetFingerObjectsActive(false);
                return;
            }
        }

        // -------------------------------
        // 아래는 기존 코드 그대로
        // -------------------------------
        Finger[] fingers = hand.fingers;
        if (fingers == null || fingers.Length < 2)
        {
            SetFingerObjectsActive(false);
            return;
        }

        Finger thumbFinger = null;
        Finger indexFinger = null;

        // prefer type-based assignment
        foreach (Finger f in fingers)
        {
            if (f.Type == Finger.FingerType.THUMB)
                thumbFinger = f;
            else if (f.Type == Finger.FingerType.INDEX)
                indexFinger = f;
        }

        // fallback if types not set
        if (thumbFinger == null && fingers.Length > 0)
            thumbFinger = fingers[0];
        if (indexFinger == null && fingers.Length > 1)
            indexFinger = fingers[1];

        if (thumbFinger == null || indexFinger == null)
        {
            SetFingerObjectsActive(false);
            return;
        }

        // Distal bones → fingertips
        Bone indexDistal = indexFinger.bones[(int)Bone.BoneType.DISTAL];
        Bone thumbDistal = thumbFinger.bones[(int)Bone.BoneType.DISTAL];

        var indexTip = indexDistal.NextJoint; // Leap.Vector
        var thumbTip = thumbDistal.NextJoint;

        Vector3 indexTipPos = new Vector3(indexTip.x, indexTip.y, indexTip.z);
        Vector3 thumbTipPos = new Vector3(thumbTip.x, thumbTip.y, thumbTip.z);

        finger1.position = indexTipPos;
        finger2.position = thumbTipPos;

        // Metacarpal.NextJoint ~ MCP (knuckle) position for index finger
        if (indexMcp != null)
        {
            Bone indexMetacarpal = indexFinger.bones[(int)Bone.BoneType.METACARPAL];
            var mcp = indexMetacarpal.NextJoint;
            Vector3 mcpPos = new Vector3(mcp.x, mcp.y, mcp.z);
            indexMcp.position = mcpPos;
        }
    }



    void SetFingerObjectsActive(bool active)
    {
        if (finger1 && finger1.gameObject.activeSelf != active)
            finger1.gameObject.SetActive(active);

        if (finger2 && finger2.gameObject.activeSelf != active)
            finger2.gameObject.SetActive(active);

        // MCP cursor visibility is controlled by TrialGameController via its own cursor,
        // so we usually don't toggle indexMcp GameObject here.
    }
}
