using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class SessionSceneRWRFixer5
{
    [MenuItem("Tools/Fix Header Raycast Targets")]
    public static void Fix()
    {
        var headerRowGo = GameObject.Find("HeaderRow");
        if (headerRowGo == null) { Debug.LogError("[Fixer5] HeaderRow not found"); return; }

        var headerRoot = headerRowGo.transform;
        int fixed_ = 0;

        for (int i = 0; i < headerRoot.childCount; i++)
        {
            var child = headerRoot.GetChild(i);

            // Enable raycastTarget on the TMP_Text so EventSystem can detect hover
            var tmp = child.GetComponent<TMP_Text>()
                   ?? child.GetComponentInChildren<TMP_Text>();
            if (tmp != null)
            {
                tmp.raycastTarget = true;
                EditorUtility.SetDirty(tmp);
            }

            // Also make sure any Image has raycastTarget enabled
            var img = child.GetComponent<Image>();
            if (img != null)
            {
                img.raycastTarget = true;
                EditorUtility.SetDirty(img);
            }

            fixed_++;
            Debug.Log($"[Fixer5] '{child.name}' raycastTarget enabled. TMP='{tmp?.text}'");
        }

        // Verify TooltipUI exists in scene
        var tooltipUI = Object.FindObjectOfType<TooltipUI>();
        if (tooltipUI == null)
            Debug.LogWarning("[Fixer5] TooltipUI not found in scene — run 'Setup Header Tooltips' first.");
        else
            Debug.Log($"[Fixer5] TooltipUI found on '{tooltipUI.gameObject.name}'.");

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

        Debug.Log($"[Fixer5] Done. Fixed {fixed_} header cells.");
        EditorUtility.DisplayDialog("Done", $"raycastTarget enabled on {fixed_} header cells.", "OK");
    }
}
