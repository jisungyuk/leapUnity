using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SessionRow : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text trialIndex;
    public TMP_InputField Hand;
    public TMP_InputField targetId;
    public TMP_InputField startX, startY, startZ;
    public TMP_InputField ttl1;
    public TMP_InputField VF;
    public Image background;

    private SessionTableController controller;

    public void Init(SessionTableController owner) { controller = owner; }

    public void SetIndex(int idx)
    {
        if (!trialIndex)
        {
            Debug.LogError("SessionRow: 'trialIndex' not assigned.", this);
            return;
        }
        trialIndex.text = idx.ToString();
    }

    // 버튼 OnClick 에 연결할 함수
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
