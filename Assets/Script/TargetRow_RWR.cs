using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TargetRow_RWR : MonoBehaviour
{
    public TMP_Text       idText;
    public TMP_InputField diameter;     // cm (target size)
    public TMP_InputField angleDeg;     // 0~359 degrees
    public TMP_InputField distanceCm;   // distance from origin in cm
    public Image          background;

    TargetTableController_RWR controller;

    public void Init(TargetTableController_RWR owner) { controller = owner; }

    public void SetId(int id)
    {
        if (!idText) { Debug.LogError("TargetRow_RWR: idText not assigned.", this); return; }
        idText.text = id.ToString();
    }

    public void SelectMe() { controller?.SelectRow(this); }

    public void SetSelected(bool on)
    {
        if (background) background.color = on ? new Color(0.85f, 0.92f, 1f) : Color.white;
    }
}
