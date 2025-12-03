using UnityEngine;
using System.IO;

public class DataPathManager : MonoBehaviour
{
    public static DataPathManager Instance { get; private set; }

    [SerializeField] string participantFolder = "";  // shown in Inspector
    public string ParticipantFolder => participantFolder;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Call this when user picks/pastes a folder path
    public bool TrySetFolder(string path, out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(path)) { error = "Path is empty."; return false; }
        if (!Directory.Exists(path)) { error = "Folder does not exist."; return false; }

        // Optional: quick write permission test
        try
        {
            string probe = Path.Combine(path, ".write_test.tmp");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
        }
        catch (System.Exception e) { error = $"No write permission: {e.Message}"; return false; }

        participantFolder = path.Trim();
        return true;
    }

    // Helper to build a file path inside the participant folder; falls back if unset.
    public string PathInParticipantFolder(string filename)
    {
        if (!string.IsNullOrEmpty(participantFolder) && Directory.Exists(participantFolder))
            return Path.Combine(participantFolder, filename);

        // Fallback (safe on all platforms)
        return Path.Combine(Application.persistentDataPath, filename);
    }
   
}
