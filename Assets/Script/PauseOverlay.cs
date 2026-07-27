using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Full-screen dim backdrop + a smaller centered announcement box for RWR's pause
/// system. Built at runtime (same pattern as GameInfoOverlay.cs) so no manual scene
/// wiring is needed — just gameObject.AddComponent&lt;PauseOverlay&gt;() from
/// GameSessionController_RWR.
/// </summary>
public class PauseOverlay : MonoBehaviour
{
    GameObject backdrop;
    GameObject box;
    TMP_Text   messageText;

    void Awake()
    {
        BuildUI();
        Hide();
    }

    void BuildUI()
    {
        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[PauseOverlay] No Canvas found.");
            return;
        }

        // Full-screen dim backdrop
        backdrop = new GameObject("PauseOverlay_Backdrop");
        backdrop.transform.SetParent(canvas.transform, false);

        var backdropRT = backdrop.AddComponent<RectTransform>();
        backdropRT.anchorMin = Vector2.zero;
        backdropRT.anchorMax = Vector2.one;
        backdropRT.offsetMin = Vector2.zero;
        backdropRT.offsetMax = Vector2.zero;

        var backdropImg = backdrop.AddComponent<Image>();
        backdropImg.color = new Color(0f, 0f, 0f, 0.6f);

        // Smaller centered announcement box on top of the backdrop
        box = new GameObject("PauseOverlay_Box");
        box.transform.SetParent(backdrop.transform, false);

        var boxRT = box.AddComponent<RectTransform>();
        boxRT.anchorMin        = new Vector2(0.5f, 0.5f);
        boxRT.anchorMax        = new Vector2(0.5f, 0.5f);
        boxRT.pivot            = new Vector2(0.5f, 0.5f);
        boxRT.anchoredPosition = Vector2.zero;
        boxRT.sizeDelta        = new Vector2(680f, 460f);

        var boxImg = box.AddComponent<Image>();
        boxImg.color = new Color(0.08f, 0.08f, 0.08f, 0.96f);

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(box.transform, false);

        var textRT = textGo.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(30f, 30f);
        textRT.offsetMax = new Vector2(-30f, -30f);

        messageText = textGo.AddComponent<TextMeshProUGUI>();
        messageText.fontSize    = 40f;
        messageText.color       = new Color(1f, 1f, 0.3f);
        messageText.alignment   = TextAlignmentOptions.Center;
        messageText.richText    = true;
        messageText.enableWordWrapping = true;
    }

    public void Show(string message)
    {
        if (backdrop == null) return;
        backdrop.SetActive(true);
        if (messageText != null) messageText.text = message;
    }

    public void Hide()
    {
        if (backdrop != null) backdrop.SetActive(false);
    }
}
