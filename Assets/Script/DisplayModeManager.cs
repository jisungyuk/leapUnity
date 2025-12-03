using UnityEngine;

public class DisplayModeManager : MonoBehaviour
{
    [SerializeField] bool useFullscreen = false;  // toggle per scene
    [SerializeField] int width = 1920;
    [SerializeField] int height = 1080;

    void Start()
    {
        // Set resolution and fullscreen mode
        Screen.SetResolution(width, height, useFullscreen);
        Debug.Log($"DisplayModeManager: {width}x{height}, Fullscreen={useFullscreen}");
    }
}
