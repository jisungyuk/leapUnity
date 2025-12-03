using UnityEngine;
using UnityEngine.SceneManagement;

public class EscapeToMenu : MonoBehaviour
{
    [SerializeField] string menuSceneName = "MainMenu"; // must match your actual main menu scene name
    [SerializeField] bool returnToWindowed = true;       // optional toggle

    void Update()
    {
        // Detect ESC key
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // Optional: switch back to windowed mode when leaving gameplay
            if (returnToWindowed)
            {
                Screen.fullScreen = false;
                Screen.SetResolution(1920, 1080, false);
            }

            // Load Main Menu
            SceneManager.LoadScene(menuSceneName);
        }
    }
}
