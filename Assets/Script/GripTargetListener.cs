using System;
using UnityEngine;
using Leap.PhysicalHands;

/// <summary>
/// Attached at runtime to the spawned cylinder target.
/// Implements IPhysicalHandGrab so PhysicalHandsManager calls us directly.
/// OnHandGrab fires every frame while grabbing, so we track the first-frame
/// transition to synthesize an OnGrabEnter action.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class GripTargetListener : MonoBehaviour, IPhysicalHandGrab
{
    public Action<ContactHand> OnGrabEnterAction;
    public Action<ContactHand> OnGrabExitAction;

    bool isGrabbing = false;

    // Called every frame the hand is grabbing this object
    public void OnHandGrab(ContactHand hand)
    {
        if (!isGrabbing)
        {
            isGrabbing = true;
            OnGrabEnterAction?.Invoke(hand);
        }
    }

    // Called once when the grab ends
    public void OnHandGrabExit(ContactHand hand)
    {
        isGrabbing = false;
        OnGrabExitAction?.Invoke(hand);
    }
}
