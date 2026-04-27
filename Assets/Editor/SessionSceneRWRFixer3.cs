using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class SessionSceneRWRFixer3
{
    [MenuItem("Tools/Add Randomize and Reset Buttons")]
    public static void AddButtons()
    {
        // Find the DeleteRow button to use as template and locate its parent
        var deleteBtn = GameObject.Find("DeleteRow");
        if (deleteBtn == null) { Debug.LogError("[Fixer3] DeleteRow button not found"); return; }

        var buttonParent = deleteBtn.transform.parent;
        if (buttonParent == null) { Debug.LogError("[Fixer3] Parent not found"); return; }

        // Find SessionTableController_RWR in the scene
        var controller = Object.FindObjectOfType<SessionTableController_RWR>();
        if (controller == null) { Debug.LogError("[Fixer3] SessionTableController_RWR not found"); return; }

        // Create Reset button (right after DeleteRow)
        var resetBtn = CreateButton(deleteBtn, buttonParent, "ResetAll", "Reset");
        resetBtn.transform.SetSiblingIndex(deleteBtn.transform.GetSiblingIndex() + 1);

        // Create Randomize button (after Reset)
        var randomizeBtn = CreateButton(deleteBtn, buttonParent, "Randomize", "Randomize");
        randomizeBtn.transform.SetSiblingIndex(resetBtn.transform.GetSiblingIndex() + 1);

        // Wire up onClick events
        WireOnClick(resetBtn,     controller, "ResetAll");
        WireOnClick(randomizeBtn, controller, "RandomizeTrials");

        // Mark dirty and save
        EditorUtility.SetDirty(buttonParent.gameObject);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

        Debug.Log("[Fixer3] Randomize and Reset buttons added.");
        EditorUtility.DisplayDialog("Done", "Buttons added successfully!", "OK");
    }

    static GameObject CreateButton(GameObject template, Transform parent, string name, string label)
    {
        var go = Object.Instantiate(template, parent);
        go.name = name;

        // Update button label text
        var txt = go.GetComponentInChildren<TMP_Text>();
        if (txt != null) txt.text = label;

        // Clear existing onClick listeners (they were copied from template)
        var btn = go.GetComponent<Button>();
        if (btn != null) btn.onClick.RemoveAllListeners();

        return go;
    }

    static void WireOnClick(GameObject btnGo, SessionTableController_RWR controller, string methodName)
    {
        var btn = btnGo.GetComponent<Button>();
        if (btn == null) return;

        var so   = new SerializedObject(btn);
        var calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");

        calls.ClearArray();
        calls.InsertArrayElementAtIndex(0);
        var call = calls.GetArrayElementAtIndex(0);

        call.FindPropertyRelative("m_Target").objectReferenceValue = controller;
        call.FindPropertyRelative("m_MethodName").stringValue      = methodName;
        call.FindPropertyRelative("m_Mode").intValue               = 1; // void, no args
        call.FindPropertyRelative("m_CallState").intValue          = 2; // RuntimeOnly

        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
