using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class EndScreenController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] TMP_Text messageText;
    [TextArea]
    [SerializeField] string defaultMessage = "Session complete. Thank you.";

    [Header("Navigation (optional)")]
    [SerializeField] string mainMenuSceneName = "MainMenu";
    [SerializeField] bool allowAnyKeyToMenu = true;

    void Start()
    {
        if (messageText) messageText.text = defaultMessage;
    }

    void Update()
    {
        if (!allowAnyKeyToMenu) return;
        if (Input.anyKeyDown && !string.IsNullOrEmpty(mainMenuSceneName))
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}

