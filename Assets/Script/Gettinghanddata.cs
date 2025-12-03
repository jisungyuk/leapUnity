using Leap;
using UnityEngine;

public class Gettinghanddata : MonoBehaviour
{
    public LeapProvider leapProvider;

    // 현재 프레임에서 얻은 MCP 좌표
    public Vector3 leftMcpPos { get; private set; }
    public Vector3 rightMcpPos { get; private set; }

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
            leftMcpPos = ExtractIndexMcp(left);

        Hand right = frame.GetHand(Chirality.Right);
        if (right != null)
            rightMcpPos = ExtractIndexMcp(right);
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
