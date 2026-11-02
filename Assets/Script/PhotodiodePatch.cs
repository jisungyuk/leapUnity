using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Optional photodiode patch for measuring when the Go cue actually appears on the screen.
///
/// A square in one corner of the game screen, black during the trial, white for FlashS from
/// the Go frame (every instruction, REST included). A photodiode taped over it and recorded in
/// LabChart next to the TTL trigger gives the real trigger → Go-on-screen time per trial
/// (rendering, vsync and monitor latency included), which the frame-clock fields in the trial
/// CSV cannot see. Compensate ts by the measured constant part.
///
/// Turned on/off, placed and sized in MainMenu's Game Settings panel (RwrGameSettingPanel);
/// stored in PlayerPrefs. TrialGameController_RWR creates it when the game scene starts
/// (CreateIfEnabled) and calls Flash() in EnterGo, on the same frame as the Go cue.
/// Drawn on its own overlay canvas above everything else, at exact screen pixels.
/// </summary>
public class PhotodiodePatch : MonoBehaviour
{
    public enum Corner { BottomLeft, BottomRight, TopLeft, TopRight }

    const string EnabledKey = "RWR_PhotodiodeEnabled";
    const string CornerKey  = "RWR_PhotodiodeCorner";
    const string SizeKey    = "RWR_PhotodiodeSizePx";

    public static readonly int[] SizesPx = { 50, 100, 150, 200 };
    const int DefaultSizePx = 100;

    // White this long from the Go frame (6 frames at 60 Hz); only the onset is used for timing
    const float FlashS = 0.1f;

    public static bool Enabled
    {
        get => PlayerPrefs.GetInt(EnabledKey, 0) == 1;
        set { PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static Corner PatchCorner
    {
        get => (Corner)Mathf.Clamp(PlayerPrefs.GetInt(CornerKey, (int)Corner.BottomLeft), 0, 3);
        set { PlayerPrefs.SetInt(CornerKey, (int)value); PlayerPrefs.Save(); }
    }

    public static int SizePx
    {
        get => PlayerPrefs.GetInt(SizeKey, DefaultSizePx);
        set { PlayerPrefs.SetInt(SizeKey, value); PlayerPrefs.Save(); }
    }

    static PhotodiodePatch instance;

    /// <summary>For the trial header: e.g. "BottomLeft 100px" if the patch is on screen in this
    /// scene, "off" otherwise (turned off, or a game mode that does not create it).</summary>
    public static string Describe() => instance != null ? $"{PatchCorner} {SizePx}px" : "off";

    Image patch;
    float whiteUntil = -1f;

    /// <summary>Adds the patch to the current scene if it is turned on in Game Settings.</summary>
    public static void CreateIfEnabled()
    {
        if (!Enabled || instance != null) return;
        new GameObject("PhotodiodePatch").AddComponent<PhotodiodePatch>();
    }

    /// <summary>Turns the patch white from this frame for FlashS. No-op when the patch is off.</summary>
    public static void Flash()
    {
        if (instance == null) return;
        instance.patch.color = Color.white;
        instance.whiteUntil  = Time.unscaledTime + FlashS;
    }

    void Awake()
    {
        instance = this;

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;   // above the pause overlay and every other UI

        var go = new GameObject("Patch");
        go.transform.SetParent(transform, false);

        Corner c = PatchCorner;
        var anchor = new Vector2(c == Corner.BottomRight || c == Corner.TopRight ? 1f : 0f,
                                 c == Corner.TopLeft     || c == Corner.TopRight ? 1f : 0f);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = anchor;
        rt.anchorMax        = anchor;
        rt.pivot            = anchor;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(SizePx, SizePx);

        patch = go.AddComponent<Image>();
        patch.color         = Color.black;
        patch.raycastTarget = false;
    }

    void Update()
    {
        if (whiteUntil >= 0f && Time.unscaledTime >= whiteUntil)
        {
            patch.color = Color.black;
            whiteUntil  = -1f;
        }
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
