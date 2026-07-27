using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Runtime-built "Game Settings" panel for RWR mode (same UI-at-runtime pattern as
/// PauseOverlay.cs / GameInfoOverlay.cs — no manual scene wiring needed). RWR has no
/// dedicated settings scene like RG/R do (MainMenu.SceneSet.settingSceneName is empty
/// for RWR), so MainMenu.GameSet() shows this overlay in place instead of loading a scene.
///
/// Laid out as one setting per row so more settings can be added below later without
/// redesigning the panel. Row 1 (the only one so far) is the display refresh-rate picker
/// (see RwrDisplaySettings) — different labs use different monitors/TVs, and TTL/Go-cue
/// jitter measurement only means anything if Update() is capped to the real display
/// refresh rate rather than running uncapped.
/// </summary>
public class RwrGameSettingPanel : MonoBehaviour
{
    GameObject     backdrop;
    TMP_InputField hzInputField;

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
            Debug.LogWarning("[RwrGameSettingPanel] No Canvas found.");
            return;
        }

        backdrop = new GameObject("RwrGameSettingPanel_Backdrop");
        backdrop.transform.SetParent(canvas.transform, false);

        var backdropRT = backdrop.AddComponent<RectTransform>();
        backdropRT.anchorMin = Vector2.zero;
        backdropRT.anchorMax = Vector2.one;
        backdropRT.offsetMin = Vector2.zero;
        backdropRT.offsetMax = Vector2.zero;

        var backdropImg = backdrop.AddComponent<Image>();
        backdropImg.color = new Color(0f, 0f, 0f, 0.75f);

        var box = new GameObject("RwrGameSettingPanel_Box");
        box.transform.SetParent(backdrop.transform, false);

        var boxRT = box.AddComponent<RectTransform>();
        boxRT.anchorMin        = new Vector2(0.5f, 0.5f);
        boxRT.anchorMax        = new Vector2(0.5f, 0.5f);
        boxRT.pivot            = new Vector2(0.5f, 0.5f);
        boxRT.anchoredPosition = Vector2.zero;
        boxRT.sizeDelta        = new Vector2(940f, 680f); // ~30% bigger than the original 720x520

        var boxImg = box.AddComponent<Image>();
        boxImg.color = new Color(0.08f, 0.08f, 0.08f, 0.97f);

        AddLabel(box.transform, "Game Settings", 32f, Color.white,
            new Vector2(0f, 280f), new Vector2(900f, 50f));

        // Row 1: display refresh rate — label, editable Hz field, auto-detect button.
        // Everything below this row is intentionally left empty for future settings.
        const float rowY = 190f, rowH = 50f, spacing = 15f;
        const float labelW = 280f, inputW = 120f, btnW = 220f;
        float totalW  = labelW + spacing + inputW + spacing + btnW;
        float startX  = -totalW / 2f;
        float labelX  = startX + labelW / 2f;
        float inputX  = startX + labelW + spacing + inputW / 2f;
        float btnX    = startX + labelW + spacing + inputW + spacing + btnW / 2f;

        AddLabel(box.transform, "Refresh rate (Hz):", 24f, Color.white,
            new Vector2(labelX, rowY), new Vector2(labelW, rowH), TextAlignmentOptions.MidlineRight);

        hzInputField = AddInputField(box.transform, new Vector2(inputX, rowY), new Vector2(inputW, rowH));
        hzInputField.onEndEdit.AddListener(OnHzInputEndEdit);

        AddButton(box.transform, "Auto-detect", new Vector2(btnX, rowY), new Vector2(btnW, rowH), OnAutoDetectClicked);

        AddButton(box.transform, "Close", new Vector2(0f, -300f), new Vector2(200f, 50f), Hide);
    }

    void OnHzInputEndEdit(string value)
    {
        if (int.TryParse(value, out int hz) && hz > 0)
            Apply(hz);
        else
            hzInputField.SetTextWithoutNotify(RwrDisplaySettings.GetTargetRefreshRateHz().ToString());
    }

    void OnAutoDetectClicked()
    {
        int hz = Screen.currentResolution.refreshRate;
        if (hz <= 0) hz = RwrDisplaySettings.DefaultHz;
        Apply(hz);
        hzInputField.SetTextWithoutNotify(hz.ToString());
    }

    void Apply(int hz)
    {
        RwrDisplaySettings.SetTargetRefreshRateHz(hz);
        QualitySettings.vSyncCount  = 0;
        Application.targetFrameRate = hz;
    }

    public void Show()
    {
        if (backdrop == null) return;
        if (hzInputField != null)
            hzInputField.SetTextWithoutNotify(RwrDisplaySettings.GetTargetRefreshRateHz().ToString());
        backdrop.SetActive(true);
    }

    public void Hide()
    {
        if (backdrop != null) backdrop.SetActive(false);
    }

    TMP_Text AddLabel(Transform parent, string text, float fontSize, Color color, Vector2 anchoredPos, Vector2 size,
        TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = size;

        var t = go.AddComponent<TextMeshProUGUI>();
        t.text                = text;
        t.fontSize            = fontSize;
        t.color               = color;
        t.alignment           = align;
        t.enableWordWrapping  = true;
        return t;
    }

    TMP_InputField AddInputField(Transform parent, Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject("HzInputField");
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = size;

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.18f, 0.18f, 0.2f, 1f);

        var inputField = go.AddComponent<TMP_InputField>();

        var textAreaGo = new GameObject("Text Area");
        textAreaGo.transform.SetParent(go.transform, false);
        var textAreaRT = textAreaGo.AddComponent<RectTransform>();
        textAreaRT.anchorMin = Vector2.zero;
        textAreaRT.anchorMax = Vector2.one;
        textAreaRT.offsetMin = new Vector2(10f, 4f);
        textAreaRT.offsetMax = new Vector2(-10f, -4f);
        textAreaGo.AddComponent<RectMask2D>();

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(textAreaGo.transform, false);
        var textRT = textGo.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;
        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.fontSize           = 24f;
        text.color              = Color.white;
        text.alignment          = TextAlignmentOptions.MidlineLeft;
        text.enableWordWrapping = false;

        var placeholderGo = new GameObject("Placeholder");
        placeholderGo.transform.SetParent(textAreaGo.transform, false);
        var placeholderRT = placeholderGo.AddComponent<RectTransform>();
        placeholderRT.anchorMin = Vector2.zero;
        placeholderRT.anchorMax = Vector2.one;
        placeholderRT.offsetMin = Vector2.zero;
        placeholderRT.offsetMax = Vector2.zero;
        var placeholder = placeholderGo.AddComponent<TextMeshProUGUI>();
        placeholder.text       = "60";
        placeholder.fontSize   = 24f;
        placeholder.fontStyle  = FontStyles.Italic;
        placeholder.color      = new Color(1f, 1f, 1f, 0.4f);
        placeholder.alignment  = TextAlignmentOptions.MidlineLeft;

        inputField.textViewport   = textAreaRT;
        inputField.textComponent  = text;
        inputField.placeholder    = placeholder;
        inputField.contentType    = TMP_InputField.ContentType.IntegerNumber;
        inputField.characterLimit = 4;

        return inputField;
    }

    void AddButton(Transform parent, string label, Vector2 anchoredPos, Vector2 size, System.Action onClick)
    {
        var go = new GameObject($"Button_{label}");
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = size;

        var img = go.AddComponent<Image>();
        img.color = new Color(0.25f, 0.25f, 0.28f, 1f);

        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.35f, 0.35f, 0.4f, 1f);
        colors.pressedColor     = new Color(0.15f, 0.6f, 0.15f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(() => onClick());

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);

        var textRT = textGo.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        var t = textGo.AddComponent<TextMeshProUGUI>();
        t.text      = label;
        t.fontSize  = 20f;
        t.color     = Color.white;
        t.alignment = TextAlignmentOptions.Center;
    }
}
