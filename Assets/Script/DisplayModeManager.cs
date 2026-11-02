using UnityEngine;

public class DisplayModeManager : MonoBehaviour
{
    [SerializeField] bool useFullscreen = false;  // toggle per scene
    [SerializeField] int width = 1920;
    [SerializeField] int height = 1080;

    int lastWidth, lastHeight;

    void Start()
    {
        // Fullscreen scenes (the game) go to the monitor chosen in Game Settings, fullscreen at
        // that monitor's own resolution; the other scenes are windowed at width x height, back on
        // the monitor the app opened on when returning from the game.
        if (useFullscreen)
            StartCoroutine(RwrDisplaySettings.MoveToSelectedDisplay());
        else
            StartCoroutine(RwrDisplaySettings.ReturnToHomeDisplay(width, height));
        Debug.Log($"DisplayModeManager: {(useFullscreen ? "fullscreen on the selected monitor" : $"{width}x{height} windowed")}");

        // vsync ON (2026-11): one Update() per refresh of the monitor the window is on, so the
        // Go cue is drawn on the monitor's own frame (60 Hz monitor -> 60 fps, 120 Hz -> 120 fps).
        // This replaces the earlier vsync-off cap at a per-lab Hz setting (targetFrameRate),
        // which set how often the app polled but not when the monitor actually showed the cue.
        // Check the measured rate against the monitor in FpsDisplay / Game Settings, and in the
        // display_refresh_hz / frame_interval_*_ms header fields of each trial file.
        RwrDisplaySettings.ApplyVsync();

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
