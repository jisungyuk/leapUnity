using UnityEngine;
using UnityEngine.SceneManagement;

public class BackToMenu : MonoBehaviour
{
    [SerializeField] string menuSceneName = "MainMenu"; // set this to your Main Menu scene name

    public void GoBack()
    {
        // Optional: confirm the scene exists
        if (Application.CanStreamedLevelBeLoaded(menuSceneName))
        {
            SceneManager.LoadScene(menuSceneName);
        }
        else
        {
            Debug.LogError($"Scene '{menuSceneName}' not found in Build Settings!");
        }
    }
}
