using UnityEngine;

/// <summary>
/// Persists the lab's actual display refresh rate (Hz), configured from MainMenu's Game
/// Settings panel (see RwrGameSettingPanel.cs). DisplayModeManager.cs reads this to cap
/// Application.targetFrameRate in the game scene — different labs use different
/// monitors/TVs, so this can't be hardcoded, and it must survive across app restarts,
/// hence PlayerPrefs rather than RuntimeConfigStore (which resets each launch).
/// </summary>
public static class RwrDisplaySettings
{
    const string PrefKey = "RWR_TargetRefreshRateHz";
    public const int DefaultHz = 60;

    public static int GetTargetRefreshRateHz() => PlayerPrefs.GetInt(PrefKey, DefaultHz);

    public static void SetTargetRefreshRateHz(int hz)
    {
        PlayerPrefs.SetInt(PrefKey, hz);
        PlayerPrefs.Save();
    }
}
