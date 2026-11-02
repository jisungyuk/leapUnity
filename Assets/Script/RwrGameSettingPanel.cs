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
/// redesigning the panel. Row 1 lists the connected monitors (name, resolution, refresh rate);
/// the chosen one is where the game goes fullscreen when it starts. Row 2 shows the refresh
/// rate of the monitor the window is on now next to the measured frame rate (vsync is on, so
/// they should match). See RwrDisplaySettings.
/// </summary>
public class RwrGameSettingPanel : MonoBehaviour
{
    GameObject backdrop;
    GameObject displayList;
    TMP_Text   refreshLabel;

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

        // Row 1: monitor for the game. One button per connected monitor (name, resolution,
        // refresh rate); the chosen one is marked and used when the game starts. Rebuilt on Show().
        AddLabel(box.transform, "Game monitor:", 24f, Color.white,
            new Vector2(0f, 215f), new Vector2(860f, 40f));
        displayList = new GameObject("DisplayList");
        displayList.transform.SetParent(box.transform, false);
        displayList.AddComponent<RectTransform>();

        // Row 2: refresh rate of the monitor the window is on now, and the measured frame rate.
        const float rowY = -150f, rowH = 50f;
        AddLabel(box.transform, "Now:", 24f, Color.white,
            new Vector2(-330f, rowY), new Vector2(120f, rowH), TextAlignmentOptions.MidlineRight);
        refreshLabel = AddLabel(box.transform, "", 22f, Color.white,
            new Vector2(60f, rowY), new Vector2(640f, rowH), TextAlignmentOptions.MidlineLeft);
        AddLabel(box.transform, "vsync is on: frames follow the refresh rate of the monitor the game runs on. " +
            "To test another rate, change it in the operating system's display settings.", 18f,
            new Color(1f, 1f, 1f, 0.6f), new Vector2(0f, rowY - 60f), new Vector2(860f, 60f));

        AddButton(box.transform, "Close", new Vector2(0f, -300f), new Vector2(200f, 50f), Hide);
    }

    // measured frame rate while the panel is open (unscaled, so pausing does not matter)
    float fpsAccum; int fpsFrames;

    void Update()
    {
        if (backdrop == null || !backdrop.activeSelf || refreshLabel == null) return;
        fpsAccum += Time.unscaledDeltaTime; fpsFrames++;
        if (fpsAccum < 0.5f) return;
        refreshLabel.text = RwrDisplaySettings.RefreshStatusText(fpsFrames / fpsAccum);
        fpsAccum = 0f; fpsFrames = 0;
    }

    public void Show()
    {
        if (backdrop == null) return;
        RwrDisplaySettings.ApplyVsync();
        BuildDisplayList();
        fpsAccum = 0f; fpsFrames = 0;
        if (refreshLabel != null) refreshLabel.text = $"Monitor {RwrDisplaySettings.MonitorRefreshHz():0.##} Hz (measuring...)";
        backdrop.SetActive(true);
    }

    void BuildDisplayList()
    {
        if (displayList == null) return;
        foreach (Transform child in displayList.transform) Destroy(child.gameObject);

        var displays = RwrDisplaySettings.Displays();
        string chosen = RwrDisplaySettings.SelectedDisplayName;
        if (string.IsNullOrEmpty(chosen)) chosen = RwrDisplaySettings.CurrentDisplay.name;
        const float top = 160f, step = 58f;
        for (int i = 0; i < displays.Count; i++)
        {
            var d = displays[i];
            string mark = d.name == chosen ? "[x] " : "[  ] ";   // plain ASCII: the default TMP font lacks circles
            AddButton(displayList.transform, $"{mark}{i + 1}. {RwrDisplaySettings.Describe(d)}",
                new Vector2(0f, top - i * step), new Vector2(760f, 50f),
                () => { RwrDisplaySettings.SelectDisplay(d); BuildDisplayList(); });
        }
        if (displays.Count == 0)
            AddLabel(displayList.transform, "No monitor reported by the operating system.", 20f,
                new Color(1f, 0.4f, 0.4f), new Vector2(0f, top), new Vector2(760f, 50f));
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
