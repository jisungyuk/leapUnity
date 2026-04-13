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
        public string ttl1;           // TTL1 = Output1 delay from Go in ms, or "none" to skip TMS
        public string ttl2Offset;     // Output2 = Output1 + this offset in ms (e.g. "2.5")
        // Also persist UI text fields that aren't numeric-parsed
        public string hand;       // e.g., "0/1/2"
        public string vf;         // e.g., "0/1" (R/RG only)
        public string instruction; // RWR only: "0"=REST, "1"=REACH, "2"=REACH+GRASP
    }

    // RWR-specific target spec (polar coordinates relative to calibration origin)
    [System.Serializable]
    public class RwrTargetSpec
    {
        public int   id;
        public float cm;           // diameter in cm (radius = cm/2/100 metres)
        public float angle_deg;    // 0=right, 90=forward, 180=left, 270=back
        public float distance_cm;  // distance from origin in cm
    }

    public readonly List<TargetSpec>    Targets    = new List<TargetSpec>();
    public readonly List<RwrTargetSpec> RwrTargets = new List<RwrTargetSpec>();
    public readonly List<TrialSpec>     Trials     = new List<TrialSpec>();

    // Calibration origin set at game start (RWR only)
    public Vector3 rwrCalibrationOrigin = Vector3.zero;
    public bool    rwrCalibrated        = false;

    public enum GameMode
    {
        Blank = 0,
        Reachtograsp = 1,
        Reach = 2,
        RealWorldReaching = 3
    }

    // Selected game mode from Main Menu (default: current game)
    public GameMode currentGameMode = GameMode.Blank;

    // Desired starting trial index (1-based). MainMenu sets this.
    public int startTrialIndex = 1;

    // When true, trials save data to disk.
    // Set to true only when launching game via MainMenu.
    public bool enableTrialLogging = false;

    // Set to true by MainMenu just before loading the game scene.
    // GameSessionController reads this to know whether to use store data.
    // Automatically cleared after reading.
    public bool launchedFromMainMenu = false;

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

    public void SetRwrTargets(List<RwrTargetSpec> list)
    {
        RwrTargets.Clear();
        if (list != null) RwrTargets.AddRange(list);
    }

    public void ClearAllCachedData()
    {
        Targets.Clear();
        RwrTargets.Clear();
        Trials.Clear();
        startTrialIndex     = 1;
        rwrCalibrated       = false;
        rwrCalibrationOrigin = Vector3.zero;
    }
}
