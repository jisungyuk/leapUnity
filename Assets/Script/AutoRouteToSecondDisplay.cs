using UnityEngine;

public class AutoRouteToSecondDisplay : MonoBehaviour
{
    [SerializeField] int width = 1920;
    [SerializeField] int height = 1080;
    [SerializeField] Canvas primaryCoverCanvas; // optional: assign if you have a PrimaryCover on Display 1

    void Start()
    {
        for (int i = 1; i < Display.displays.Length; i++)
            Display.displays[i].Activate();

        int targetDisplayIndex = (Display.displays.Length >= 2) ? 1 : 0;

        foreach (var cam in Camera.allCameras)
            cam.targetDisplay = targetDisplayIndex;

        foreach (var cv in FindObjectsOfType<Canvas>(true))
        {
            if (primaryCoverCanvas != null && cv.rootCanvas == primaryCoverCanvas.rootCanvas)
                continue; // keep cover on Display 1
            cv.targetDisplay = targetDisplayIndex;
        }

        if (primaryCoverCanvas != null)
            primaryCoverCanvas.gameObject.SetActive(targetDisplayIndex == 1);

        Screen.fullScreen = true;
        Screen.SetResolution(width, height, true);

        Debug.Log($"[AutoRoute] Gameplay → Display {targetDisplayIndex+1}, Cover on Display 1");
    }
}
