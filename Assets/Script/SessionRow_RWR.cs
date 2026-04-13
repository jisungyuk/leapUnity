using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SessionRow_RWR : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text trialIndex;
    public TMP_InputField targetId;
    public TMP_InputField startX, startY, startZ;
    public TMP_InputField hand;
    public TMP_InputField ttl1;
    public TMP_InputField ttl2Offset;    // Output2 = Output1 + this offset (ms), e.g. "2.5"
    public TMP_InputField instruction;   // 0=REST, 1=REACH, 2=REACH+GRASP
    public Image background;

    private SessionTableController_RWR controller;

    public void Init(SessionTableController_RWR owner) { controller = owner; }

    public void SetIndex(int idx)
    {
        if (!trialIndex)
        {
            Debug.LogError("SessionRow_RWR: 'trialIndex' not assigned.", this);
            return;
        }
        trialIndex.text = idx.ToString();
    }

    public void SelectMe()
    {
        if (controller == null) return;

        bool shift =
            Input.GetKey(KeyCode.LeftShift) ||
            Input.GetKey(KeyCode.RightShift);

        controller.SelectRow(this, shift);
    }

    public void SetSelected(bool on)
    {
        if (background)
            background.color = on ? new Color(0.85f, 0.92f, 1f) : Color.white;
    }
}
