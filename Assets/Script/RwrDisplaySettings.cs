using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Display selection and timing.
///
/// Monitor: the Game Settings panel lists the connected monitors (name, resolution, refresh
/// rate) and stores the chosen one (PlayerPrefs, by name). When a game scene starts,
/// DisplayModeManager moves the window to that monitor and makes it fullscreen there
/// (MoveToSelectedDisplay). If the stored monitor is not connected, the game stays on the
/// monitor the window is already on.
///
/// Timing: the app runs with vsync ON, so Update() (and therefore the Go cue, which is drawn
/// on the next frame) follows the refresh of the monitor the window is on: 60 Hz monitor ->
/// 60 fps, 120 Hz -> 120 fps. The Game Settings panel and FpsDisplay show that monitor's
/// refresh rate next to the measured frame rate, so a mismatch (vsync overridden by the
/// driver, dropped frames) is visible during the session; TrialDataLogger writes both into
/// each trial header.
/// </summary>
public static class RwrDisplaySettings
{
    /// <summary>vsync ON: one Update() per monitor refresh. targetFrameRate is ignored
    /// by Unity while vsync is on, and is reset so it cannot cap below the monitor.</summary>
    public static void ApplyVsync()
    {
        QualitySettings.vSyncCount  = 1;
        Application.targetFrameRate = -1;
    }

    // ── monitors ──────────────────────────────────────────────────────────

    const string DisplayPrefKey = "RWR_DisplayName";

    /// <summary>Monitors connected now, in the order the OS reports them.</summary>
    public static List<DisplayInfo> Displays()
    {
        var list = new List<DisplayInfo>();
        Screen.GetDisplayLayout(list);
        return list;
    }

    public static float RefreshHz(DisplayInfo d) => (float)d.refreshRate.value;

    public static string Describe(DisplayInfo d) =>
        $"{d.name}  {d.width}×{d.height}  {RefreshHz(d):0.##} Hz";

    public static string SelectedDisplayName => PlayerPrefs.GetString(DisplayPrefKey, "");

    public static void SelectDisplay(DisplayInfo d)
    {
        PlayerPrefs.SetString(DisplayPrefKey, d.name);
        PlayerPrefs.Save();
    }

    /// <summary>The monitor the game window is on now.</summary>
    public static DisplayInfo CurrentDisplay => Screen.mainWindowDisplayInfo;

    /// <summary>Refresh rate (Hz) of the monitor the game window is on; 0 if unknown.</summary>
    public static float MonitorRefreshHz() => RefreshHz(CurrentDisplay);

    /// <summary>Moves the window to the selected monitor (if connected and not already there)
    /// and makes it fullscreen at that monitor's own resolution. Run as a coroutine.</summary>
    public static IEnumerator MoveToSelectedDisplay()
    {
        string want = SelectedDisplayName;
        DisplayInfo target = CurrentDisplay;
        if (!string.IsNullOrEmpty(want))
            foreach (var d in Displays())
                if (d.name == want) { target = d; break; }

        if (target.name != CurrentDisplay.name)
        {
            var op = Screen.MoveMainWindowTo(target, Vector2Int.zero);
            while (!op.isDone) yield return null;
        }
        Screen.SetResolution(target.width, target.height, FullScreenMode.FullScreenWindow);
        returnHomePending = true;
        Debug.Log($"[RwrDisplaySettings] Game on {Describe(CurrentDisplay)}");
    }

    // ── home monitor (menus, tables) ──────────────────────────────────────

    const string HomeDisplayPrefKey = "RWR_HomeDisplayName";

    // Monitor the app window opened on; the home monitor when none is chosen in Game Settings.
    static DisplayInfo launchDisplay;

    // Set when the game has taken the window; the next windowed scene brings it back home.
    // A flag rather than the screen mode, because the game's exit paths (GameSessionController_RWR,
    // EscapeToMenu) already switch to windowed before loading the menu.
    static bool returnHomePending;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RememberLaunchDisplay() => launchDisplay = Screen.mainWindowDisplayInfo;

    /// <summary>Chosen home monitor's name, or the monitor the app opened on if none is chosen.</summary>
    public static string HomeDisplayName
    {
        get
        {
            string name = PlayerPrefs.GetString(HomeDisplayPrefKey, "");
            return string.IsNullOrEmpty(name) ? launchDisplay.name : name;
        }
    }

    public static void SelectHomeDisplay(DisplayInfo d)
    {
        PlayerPrefs.SetString(HomeDisplayPrefKey, d.name);
        PlayerPrefs.Save();
    }

    /// <summary>Makes the window width x height windowed. Coming back from the game, also moves it,
    /// centered, to the home monitor (if connected); otherwise the window stays where it is
    /// (e.g. where the user dragged it). Run as a coroutine.</summary>
    public static IEnumerator ReturnToHomeDisplay(int width, int height)
    {
        Screen.SetResolution(width, height, false);
        if (!returnHomePending) yield break;
        returnHomePending = false;

        string want = HomeDisplayName;
        DisplayInfo home = default;
        bool found = false;
        foreach (var d in Displays())
            if (d.name == want) { home = d; found = true; break; }
        if (!found || home.name == CurrentDisplay.name) yield break;

        yield return null;   // let the switch to windowed mode take effect before moving
        var pos = new Vector2Int(Mathf.Max(0, (home.width - width) / 2),
                                 Mathf.Max(0, (home.height - height) / 2));
        var op = Screen.MoveMainWindowTo(home, pos);
        while (!op.isDone) yield return null;
        Debug.Log($"[RwrDisplaySettings] Menu back on {Describe(CurrentDisplay)}");
    }

    /// <summary>"Monitor 120 Hz | Unity 119.9 fps | vsync ON", red when the measured frame
    /// rate is more than 5% off the monitor (vsync not in effect or frames being dropped).</summary>
    public static string RefreshStatusText(float measuredFps)
    {
        float hz = MonitorRefreshHz();
        bool vsync = QualitySettings.vSyncCount > 0;
        string text = $"Monitor {hz:0.##} Hz | Unity {measuredFps:0.0} fps | vsync {(vsync ? "ON" : "OFF")}";
        bool ok = vsync && hz > 0 && Mathf.Abs(measuredFps - hz) <= 0.05f * hz;
        return ok ? text : $"<color=#FF4444>{text}</color>";
    }
}
