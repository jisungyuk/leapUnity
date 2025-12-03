using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class MainMenu : MonoBehaviour
{
    [System.Serializable]
    public class SceneSet
    {
        public string targetSceneName;
        public string sessionSceneName;
        public string settingSceneName;
        public string gameSceneName;
    }

    [Header("Scenes - Reach to Grasp (RG)")]
    [SerializeField] SceneSet rgScenes = new SceneSet
    {
        targetSceneName  = "RG_Target",
        sessionSceneName = "RG_Session",
        settingSceneName = "RG_GameSetting",
        gameSceneName    = "RG_Game"
    };

    [Header("Scenes - Reach (R)")]
    [SerializeField] SceneSet rScenes = new SceneSet
    {
        targetSceneName  = "R_Target",
        sessionSceneName = "R_Session",
        settingSceneName = "R_GameSetting",
        gameSceneName    = "R_Game"
    };


    [Header("UI")]
    [SerializeField] TMP_Text warningText;        // Small label under Start button (optional)
    [SerializeField] TMP_InputField startTrialInput; // Optional: start trial index (1-based)
    [SerializeField] TMP_Dropdown gameModeDropdown;  // Select Reach vs Reachtograsp

    // Internal
    bool settingDropdownSilently = false;

    void Start()
    {
        if (warningText) warningText.text = "";
        // Disable logging by default when returning to Main Menu
        if (RuntimeConfigStore.Instance != null)
        {
            RuntimeConfigStore.Instance.enableTrialLogging = false;

            // Initialize dropdown from stored mode if present
            if (gameModeDropdown)
            {
                settingDropdownSilently = true;
                gameModeDropdown.SetValueWithoutNotify((int)RuntimeConfigStore.Instance.currentGameMode);
                gameModeDropdown.RefreshShownValue();
                settingDropdownSilently = false;
            }
        }

        // No automatic mode warning; user can select freely
        UpdateBlankWarning();
    }

    public void StartGame()
    {
        EnsureModeFromDropdown();
        if (!IsModeSelected(out string modeMsg))
        {
            ShowModeWarning(modeMsg);
            return;
        }
        ClearModeWarning();

        if (!CanStartGame(out string message))
        {
            if (warningText) warningText.text = message;
            Debug.LogWarning("[MainMenu] Cannot start: " + message);
            return;
        }
        if (!TryApplyStartTrial(out string startMsg))
        {
            if (warningText) warningText.text = startMsg;
            Debug.LogWarning("[MainMenu] Invalid start trial: " + startMsg);
            return;
        }
        if (RuntimeConfigStore.Instance != null)
            RuntimeConfigStore.Instance.enableTrialLogging = true;
        SceneManager.LoadScene(GetSceneSet().gameSceneName);
    }

    public void OnGameModeChanged(int optionIndex)
    {
        if (settingDropdownSilently) return;
        ApplyGameMode(optionIndex);
    }

    public void TargetSet()
    {
        EnsureModeFromDropdown();
        if (!IsModeSelected(out string modeMsg))
        {
            ShowModeWarning(modeMsg);
            return;
        }
        ClearModeWarning();

        SceneManager.LoadScene(GetSceneSet().targetSceneName);
    }

    public void SessionSet()
    {
        EnsureModeFromDropdown();
        if (!IsModeSelected(out string modeMsg))
        {
            ShowModeWarning(modeMsg);
            return;
        }
        ClearModeWarning();

        SceneManager.LoadScene(GetSceneSet().sessionSceneName);
    }

    public void GameSet()
    {
        EnsureModeFromDropdown();
        if (!IsModeSelected(out string modeMsg))
        {
            ShowModeWarning(modeMsg);
            return;
        }
        ClearModeWarning();

        SceneManager.LoadScene(GetSceneSet().settingSceneName);
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    // -----------------------------------------------------------
    // GAME START VALIDATION
    // -----------------------------------------------------------
    bool CanStartGame(out string problem)
    {
        var store = RuntimeConfigStore.Instance;

        // 1) Participant folder still required (for data saves, logs, etc.)
        if (DataPathManager.Instance == null ||
            string.IsNullOrEmpty(DataPathManager.Instance.ParticipantFolder))
        {
            problem = "❗ Please set participant folder first.";
            return false;
        }

        // 2) Require in-memory Targets and Trials (we no longer support CSV mode)
        if (store == null || store.Targets.Count == 0)
        {
            problem = "❗ No targets defined. Go to Target Settings.";
            return false;
        }
        if (store.Trials.Count == 0)
        {
            problem = "❗ No session trials defined. Go to Session Settings.";
            return false;
        }

        problem = "";
        return true;
    }

    // Parse and validate the start trial from input. If empty/invalid (<=0, non-integer), defaults to 1.
    // If greater than available trials, show error and block.
    bool TryApplyStartTrial(out string message)
    {
        message = "";
        var store = RuntimeConfigStore.Instance;
        if (store == null)
        {
            message = "Internal error: no runtime store.";
            return false;
        }

        int total = store.Trials != null ? store.Trials.Count : 0;

        // Default behavior when input field is missing or empty → start at 1
        if (startTrialInput == null || string.IsNullOrWhiteSpace(startTrialInput.text))
        {
            store.startTrialIndex = 1;
            return true;
        }

        var raw = startTrialInput.text.Trim();
        // Only accept plain integers; reject decimals and <= 0
        if (!int.TryParse(raw, out int idx) || idx <= 0)
        {
            store.startTrialIndex = 1;
            return true;
        }

        if (idx > total && total > 0)
        {
            message = $"Total trial # < {idx}";
            return false;
        }

        store.startTrialIndex = idx;
        return true;
    }

    SceneSet GetSceneSet()
    {
        var store = RuntimeConfigStore.Instance;
        if (!store) return rgScenes;

        return store.currentGameMode == RuntimeConfigStore.GameMode.Reach ? rScenes : rgScenes;
    }

    void EnsureModeFromDropdown()
    {
        if (!gameModeDropdown) return;
        var store = RuntimeConfigStore.Instance;
        if (!store) return;

        // Avoid unnecessary processing when value matches current mode
        if ((int)store.currentGameMode != gameModeDropdown.value)
            ApplyGameMode(gameModeDropdown.value);
    }

    void ApplyGameMode(int optionIndex)
    {
        var store = RuntimeConfigStore.Instance;
        if (!store) return;

        var clamped = Mathf.Clamp(optionIndex, 0, 2);
        var newMode = (RuntimeConfigStore.GameMode)clamped;

        if (store.currentGameMode != newMode)
        {
            // 모드가 달라질 때마다 캐시 리셋
            store.ClearAllCachedData();
            store.currentGameMode = newMode;
        }

        UpdateBlankWarning();
    }

    bool IsModeSelected(out string message)
    {
        var store = RuntimeConfigStore.Instance;
        if (!store)
        {
            message = "Internal error: no runtime store.";
            return false;
        }

        if (store.currentGameMode == RuntimeConfigStore.GameMode.Blank)
        {
            message = "Select a game mode.";
            return false;
        }

        message = "";
        return true;
    }

    void UpdateBlankWarning()
    {
        // No mode warning text is shown
        ClearModeWarning();
    }

    void ShowModeWarning(string message)
    {
        // Intentionally no-op: warnings suppressed
    }

    void ClearModeWarning()
    {
        if (warningText && warningText.text == "Select a game mode.")
            warningText.text = "";
    }
}
