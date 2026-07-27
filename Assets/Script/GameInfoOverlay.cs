using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// In-game info overlay. Press ToggleKey (default: Tab) to show/hide.
/// Displays: FPS, Leap Motion status, LabChart status, current trial details.
/// Instruction text is intentionally excluded.
/// </summary>
public class GameInfoOverlay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] LeapFingerInput          leapInput;
    [SerializeField] LabChartStatusChecker    labChartStatus;
    [SerializeField] GameSessionController_RWR sessionController;

    [Header("Settings")]
    [SerializeField] KeyCode toggleKey = KeyCode.Tab;

    // Runtime UI (created in Awake)
    GameObject panel;
    TMP_Text   infoText;

    bool visible = false;

    // ── Lifecycle ────────────────────────────────────────────────────

    void Awake()
    {
        BuildUI();
        SetVisible(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            SetVisible(!visible);

        if (visible)
            RefreshText();
    }

    // ── UI Builder ───────────────────────────────────────────────────

    void BuildUI()
    {
        // Find or create Canvas
        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[GameInfoOverlay] No Canvas found.");
            return;
        }

        // Semi-transparent background panel
        panel = new GameObject("GameInfoOverlay_Panel");
        panel.transform.SetParent(canvas.transform, false);

        var rt = panel.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 1f);
        rt.anchorMax        = new Vector2(0f, 1f);
        rt.pivot            = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(10f, -10f);
        rt.sizeDelta        = new Vector2(420f, 480f);

        var img = panel.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.72f);

        // Text
        var textGo = new GameObject("Text");
        textGo.transform.SetParent(panel.transform, false);

        var textRT    = textGo.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(10f, 10f);
        textRT.offsetMax = new Vector2(-10f, -10f);

        infoText = textGo.AddComponent<TextMeshProUGUI>();
        infoText.fontSize          = 24f;
        infoText.color             = Color.white;
        infoText.enableWordWrapping = false;
        infoText.richText          = true;
    }

    void SetVisible(bool on)
    {
        visible = on;
        if (panel != null) panel.SetActive(on);
    }

    // ── Text refresh ─────────────────────────────────────────────────

    void RefreshText()
    {
        if (infoText == null) return;

        var sb = new System.Text.StringBuilder();

        // Header
        sb.AppendLine($"<color=#AAAAAA>[{toggleKey} to hide]</color>");
        sb.AppendLine();


        // Leap Motion
        sb.AppendLine("<color=#FFFF88>LEAP MOTION</color>");
        if (leapInput == null)
        {
            sb.AppendLine("  <color=#888888>not assigned</color>");
        }
        else if (leapInput.hasIndexJointData)
        {
            string hand = leapInput.useLeftHand ? "Left" : (leapInput.allowEitherHand ? "Either" : "Right");
            sb.AppendLine($"  <color=#44FF44>● tracking  ({hand})</color>");
        }
        else
        {
            sb.AppendLine("  <color=#FF4444>○ no hand detected</color>");
        }
        sb.AppendLine();

        // LabChart
        sb.AppendLine("<color=#FFFF88>LABCHART</color>");
        if (sessionController != null && sessionController.LabChartBypassed)
            sb.AppendLine("  <color=#888888>○ kinematic-only (no stimulation)</color>");
        else if (labChartStatus == null)
            sb.AppendLine("  <color=#888888>not assigned</color>");
        else if (labChartStatus.IsOpen)
            sb.AppendLine("  <color=#44FF44>● open</color>");
        else
            sb.AppendLine("  <color=#FF4444>✗ not running</color>");
        sb.AppendLine();

        // Status text
        if (sessionController != null)
        {
            string statusMsg = sessionController.StatusMessage;
            if (!string.IsNullOrEmpty(statusMsg))
            {
                sb.AppendLine($"<color=#FFAA44>{statusMsg}</color>");
                sb.AppendLine();
            }
        }

        // Trial info
        sb.AppendLine("<color=#FFFF88>TRIAL INFO</color>");

        if (sessionController == null)
        {
            sb.AppendLine("  <color=#888888>controller not assigned</color>");
        }
        else if (sessionController.IsCalibrating)
        {
            sb.AppendLine("  Calibrating...");
        }
        else if (sessionController.IsExperimenting)
        {
            sb.AppendLine($"  <color=#AAFFAA>EXP {sessionController.ExperimentCounter}</color>");
        }
        else
        {
            var t     = sessionController.CurrentTrial;
            int n     = sessionController.CurrentIndex;
            int total = sessionController.TrialCount;

            sb.AppendLine($"  <color=#AAFFAA>Trial  {n} / {total}</color>");

            if (t != null)
            {
                string handStr = t.handMode == 0 ? "L" : t.handMode == 1 ? "R" : "Either";
                sb.AppendLine($"  Hand:    {handStr}   Target: {t.targetId}");
                sb.AppendLine($"  Start_r: {t.startRadiusCm:F0} cm");
                sb.AppendLine($"  Hold:    {t.holdDuration:F2} s   Wait: {t.waitForGo:F2} s");
                sb.AppendLine($"  Move:    {t.executingDuration:F2} s");
                sb.AppendLine($"  TS:      {(t.ttlEnabled ? $"{t.ttlOffsetMs:F1} ms" : "—")}");
                sb.AppendLine($"  CS:      {(t.ttlEnabled && t.ttl2OffsetMs != 0 ? $"{t.ttl2OffsetMs:F1} ms" : "—")}");
            }
        }

        infoText.text = sb.ToString();
    }
}
