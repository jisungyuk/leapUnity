 // DisplayPrefs.cs
using UnityEngine;

    public class DisplayPrefs : MonoBehaviour
    {
        public static DisplayPrefs Instance { get; private set; }
        [SerializeField] bool launchOnSecondMonitor = false;
        public bool LaunchOnSecondMonitor => launchOnSecondMonitor;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // Hook this to the Toggle's OnValueChanged(bool)
        public void SetLaunchOnSecondMonitor(bool on) => launchOnSecondMonitor = on;
    }
