using Leap;
using UnityEngine;

/// <summary>
/// PostProcessProvider that applies a per-hand world-space position offset
/// to all Leap tracking data before it reaches downstream consumers
/// (PhysicalHandsManager, LeapFingerInput, etc.).
///
/// Wiring: ServiceProvider → HandOffsetProvider → PhysicalHandsManager / LeapFingerInput
///
/// Use case: bimanual tasks where real hands would occlude each other from the
/// Leap sensor. Offsetting virtual hands inward lets real hands stay apart while
/// appearing to meet in virtual space.
/// </summary>
public class HandOffsetProvider : PostProcessProvider
{
    [Header("Right Hand Offset (metres)")]
    public Vector3 rightHandOffset = new Vector3(0f, 0f, -0.03f);

    [Header("Left Hand Offset (metres)")]
    public Vector3 leftHandOffset = new Vector3(0f, 0f, 0.03f);

    public override void ProcessFrame(ref Frame inputFrame)
    {
        foreach (var hand in inputFrame.Hands)
        {
            Vector3 off = hand.IsLeft ? leftHandOffset : rightHandOffset;
            if (off == Vector3.zero) continue;
            ShiftHand(hand, off);
        }
    }

    static void ShiftHand(Hand hand, Vector3 off)
    {
        hand.PalmPosition           += off;
        hand.StabilizedPalmPosition += off;

        hand.Arm.PrevJoint += off;
        hand.Arm.NextJoint += off;
        hand.Arm.Center    += off;

        foreach (var finger in hand.fingers)
        {
            for (int b = 0; b < 4; b++)
            {
                finger.bones[b].PrevJoint += off;
                finger.bones[b].NextJoint += off;
                finger.bones[b].Center    += off;
            }
        }
    }
}
