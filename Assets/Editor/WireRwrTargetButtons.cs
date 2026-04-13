using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine.Events;

public static class WireRwrTargetButtons
{
    [MenuItem("Tools/Wire RWR Target Buttons")]
    public static void Wire()
    {
        var controller = Object.FindObjectOfType<TargetTableController_RWR>();
        if (controller == null) { Debug.LogError("TargetTableController_RWR not found!"); return; }

        WireButton("AddRow",    controller, "AddTarget");
        WireButton("DeleteRow", controller, "DeleteSelected");
        WireButton("Save",      controller, "SaveCsv");
        WireButton("Load",      controller, "LoadCsv");

        EditorUtility.SetDirty(controller.gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        Debug.Log("[WireRwrTargetButtons] All buttons wired.");
    }

    static void WireButton(string goName, TargetTableController_RWR controller, string methodName)
    {
        var go = GameObject.Find(goName);
        if (go == null) { Debug.LogWarning($"Button GameObject '{goName}' not found."); return; }

        var btn = go.GetComponent<Button>();
        if (btn == null) { Debug.LogWarning($"No Button on '{goName}'."); return; }

        btn.onClick.RemoveAllListeners();

        var method = typeof(TargetTableController_RWR).GetMethod(methodName);
        if (method == null) { Debug.LogWarning($"Method '{methodName}' not found."); return; }

        var action = (UnityAction)System.Delegate.CreateDelegate(typeof(UnityAction), controller, method);
        UnityEventTools.AddPersistentListener(btn.onClick, action);

        EditorUtility.SetDirty(btn);
        Debug.Log($"  Wired {goName} -> {methodName}");
    }
}
