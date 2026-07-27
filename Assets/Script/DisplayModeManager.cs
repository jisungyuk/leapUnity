using UnityEngine;

public class DisplayModeManager : MonoBehaviour
{
    [SerializeField] bool useFullscreen = false;  // toggle per scene
    [SerializeField] int width = 1920;
    [SerializeField] int height = 1080;

    int lastWidth, lastHeight;

    void Start()
    {
        // Set resolution and fullscreen mode
        Screen.SetResolution(width, height, useFullscreen);
        Debug.Log($"DisplayModeManager: {width}x{height}, Fullscreen={useFullscreen}");

        // Cap Update() to the lab's configured display refresh rate (MainMenu > Game
        // Settings, see RwrDisplaySettings/RwrGameSettingPanel) rather than letting it run
        // uncapped. Running uncapped makes Update()'s frame-polling checks (TTL fire
        // detection, Go-cue detection) land at a nearly constant phase relative to each
        // other every trial (500ms apart, but frame time barely varies run to run), which
        // collapses TrialGameController_RWR's measured jitter to near-zero regardless of
        // real timing precision — confirmed via [JitterDebug] logging showing genuine
        // few-ms jitter in Editor (irregular frame times) vs ~0 in a standalone build
        // (very regular, uncapped frame times). vSyncCount must be 0 or targetFrameRate
        // is ignored.
        QualitySettings.vSyncCount  = 0;
        Application.targetFrameRate = RwrDisplaySettings.GetTargetRefreshRateHz();

        lastWidth  = Screen.width;
        lastHeight = Screen.height;
    }

    // Player Settings' "Resizable Window" lets the user freely drag-resize the window,
    // which Unity does not lock to any aspect ratio on its own. This keeps whatever ratio
    // width/height above represent (default 16:9) by recomputing height from the new
    // width whenever a resize is detected. Windows-drag-resize can look a bit jittery
    // mid-drag since Unity doesn't expose a native aspect-lock hook — it settles once the
    // user releases the mouse.
    void Update()
    {
        if (useFullscreen) return;
        if (Screen.width == lastWidth && Screen.height == lastHeight) return;

        float aspect    = (float)width / height;
        int   newWidth  = Screen.width;
        int   newHeight = Mathf.RoundToInt(newWidth / aspect);

        if (newHeight != Screen.height)
        {
            Screen.SetResolution(newWidth, newHeight, false);
            lastWidth  = newWidth;
            lastHeight = newHeight;
        }
        else
        {
            lastWidth  = Screen.width;
            lastHeight = Screen.height;
        }
    }
}
