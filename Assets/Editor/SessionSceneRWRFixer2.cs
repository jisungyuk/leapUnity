using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class SessionSceneRWRFixer2
{
    [MenuItem("Tools/Revert HeaderRow Width")]
    public static void RevertHeaderWidth()
    {
        var headerRowGo = GameObject.Find("HeaderRow");
        if (headerRowGo == null) { Debug.LogError("[SceneFixer2] HeaderRow not found"); return; }

        var rt = headerRowGo.GetComponent<RectTransform>();
        if (rt == null) { Debug.LogError("[SceneFixer2] RectTransform not found"); return; }

        // Restore original center-anchored fixed-size layout
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 215f);
        rt.sizeDelta        = new Vector2(983f, 30f);

        EditorUtility.SetDirty(headerRowGo);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

        Debug.Log("[SceneFixer2] HeaderRow reverted to original 983px width.");
        EditorUtility.DisplayDialog("Done", "HeaderRow reverted to original size.", "OK");
    }
}
