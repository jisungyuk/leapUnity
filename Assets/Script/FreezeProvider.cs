using Leap;
using UnityEngine;

/// <summary>
/// PostProcessProvider that freezes its output frame at grab moment,
/// then applies position delta + rotation delta each frame so the
/// frozen hand model tracks the hand together with the cylinder.
/// </summary>
public class FreezeProvider : PostProcessProvider
{
    private bool       _frozen      = false;
    private Frame      _frozenFrame = new Frame();
    private Vector3    _pivot       = Vector3.zero;
    private Vector3    _delta       = Vector3.zero;
    private Quaternion _rot         = Quaternion.identity;

    public void Freeze(Vector3 pivot)
    {
        _frozen = true;
        _pivot  = pivot;
        _delta  = Vector3.zero;
        _rot    = Quaternion.identity;
    }

    public void Unfreeze()
    {
        _frozen = false;
        _delta  = Vector3.zero;
        _rot    = Quaternion.identity;
    }

    // Called every frame from UpdateCylinderFollow — same values as cylinder.
    public void UpdateTransform(Vector3 delta, Quaternion rot)
    {
        _delta = delta;
        _rot   = rot;
    }

    public override void ProcessFrame(ref Frame inputFrame)
    {
        if (!_frozen)
        {
            _frozenFrame.CopyFrom(inputFrame);
        }
        else
        {
            inputFrame.CopyFrom(_frozenFrame);
            foreach (var hand in inputFrame.Hands)
                TransformHand(hand);
        }
    }

    private Vector3 TP(Vector3 p) => _pivot + _delta + _rot * (p - _pivot);

    private void TransformHand(Hand hand)
    {
        hand.PalmPosition           = TP(hand.PalmPosition);
        hand.StabilizedPalmPosition = TP(hand.StabilizedPalmPosition);
        hand.WristPosition          = TP(hand.WristPosition);
        hand.PalmNormal             = _rot * hand.PalmNormal;
        hand.Direction              = _rot * hand.Direction;
        hand.Rotation               = _rot * hand.Rotation;

        TransformBone(hand.Arm);

        foreach (var finger in hand.fingers)
        {
            finger.TipPosition = TP(finger.TipPosition);
            finger.Direction   = _rot * finger.Direction;
            foreach (var bone in finger.bones)
                TransformBone(bone);
        }
    }

    private void TransformBone(Bone bone)
    {
        bone.PrevJoint = TP(bone.PrevJoint);
        bone.NextJoint = TP(bone.NextJoint);
        bone.Center    = TP(bone.Center);
        bone.Direction = _rot * bone.Direction;
        bone.Rotation  = _rot * bone.Rotation;
    }
}
