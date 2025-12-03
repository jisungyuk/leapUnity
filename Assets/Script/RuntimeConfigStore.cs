using System.Collections.Generic;
using UnityEngine;

// Keep settings alive across scene loads
public class RuntimeConfigStore : MonoBehaviour
{
    public static RuntimeConfigStore Instance { get; private set; }


    [System.Serializable]
    public class TargetSpec
    {
        public int id;
        public float cm;
        public float x, y, z;
    }

    [System.Serializable]
    public class TrialSpec
    {
        public int trial;
        public string targetId;   // manual text (e.g., "3")
        public float startX, startY, startZ;
        public float ttl1;        // NEW: TTL1 per trial
        // Also persist UI text fields that aren't numeric-parsed
        public string hand;       // e.g., "0/1/2"
        public string vf;         // e.g., "0/1"
    }

    public readonly List<TargetSpec> Targets = new List<TargetSpec>();
    public readonly List<TrialSpec>  Trials  = new List<TrialSpec>();

    public enum GameMode
    {
        Blank = 0,
        Reachtograsp = 1,
        Reach = 2
    }

    // Selected game mode from Main Menu (default: current game)
    public GameMode currentGameMode = GameMode.Blank;

    // Desired starting trial index (1-based). MainMenu sets this.
    public int startTrialIndex = 1;

    // When true, trials save data to disk.
    // Set to true only when launching game via MainMenu.
    public bool enableTrialLogging = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Helpers to reset/replace data
    public void SetTargets(List<TargetSpec> list)
    {
        Targets.Clear();
        if (list != null) Targets.AddRange(list);
    }

    public void SetTrials(List<TrialSpec> list)
    {
        Trials.Clear();
        if (list != null) Trials.AddRange(list);
    }

    public void ClearAllCachedData()
    {
        Targets.Clear();
        Trials.Clear();
        startTrialIndex = 1;
    }
}
