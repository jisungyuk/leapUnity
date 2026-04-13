using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SessionRow_RWR : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text       trialIndex;    // #
    public TMP_InputField hand;          // 0=Left, 1=Right, 2=Both
    public TMP_InputField targetId;      // target ID
    public TMP_InputField startRadiusCm; // start zone radius (cm)
    public TMP_InputField holdDuration;  // HoldInStart (s)
    public TMP_InputField waitForGo;     // WaitForGo (s)
    public TMP_InputField executing;     // Executing (s)
    public TMP_InputField ts;            // Testing Stimulus delay from Go (ms), "." = NoPulse
    public TMP_InputField cs;            // Conditioning Stimulus offset from TS (ms), "." = NoPulse
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
