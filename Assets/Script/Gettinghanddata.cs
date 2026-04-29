using Leap;
using UnityEngine;

public class Gettinghanddata : MonoBehaviour
{
    public LeapProvider leapProvider;

    public Vector3    leftMcpPos  { get; private set; }
    public Vector3    rightMcpPos { get; private set; }
    public Quaternion leftPalmRot  { get; private set; } = Quaternion.identity;
    public Quaternion rightPalmRot { get; private set; } = Quaternion.identity;

    private void OnEnable()
    {
        leapProvider.OnUpdateFrame += OnUpdateFrame;
    }

    private void OnDisable()
    {
        leapProvider.OnUpdateFrame -= OnUpdateFrame;
    }

    void OnUpdateFrame(Frame frame)
    {
        Hand left = frame.GetHand(Chirality.Left);
        if (left != null)
        {
            leftMcpPos  = ExtractIndexMcp(left);
            leftPalmRot = left.Rotation;
        }

        Hand right = frame.GetHand(Chirality.Right);
        if (right != null)
        {
            rightMcpPos  = ExtractIndexMcp(right);
            rightPalmRot = right.Rotation;
        }
    }

    Vector3 ExtractIndexMcp(Hand hand)
    {
        Finger index = hand.fingers[1];
        Bone metacarpal = index.bones[(int)Bone.BoneType.METACARPAL];
        return new Vector3(metacarpal.NextJoint.x,
                           metacarpal.NextJoint.y,
                           metacarpal.NextJoint.z);
    }
}
