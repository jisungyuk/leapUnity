using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using System.Collections.Generic;

public class SessionSceneRWRFixer6
{
    static readonly Dictionary<string, string> TooltipTexts = new Dictionary<string, string>
    {
        { "#",       "Trial number" },
        { "Hand",    "Hand: 0=Left  1=Right  2=Both" },
        { "Target",  "Target ID (must match Target Table)" },
        { "Start_r", "Start zone radius (cm)" },
        { "Hold",    "Time to hold inside start zone before instruction cue (s)" },
        { "Wait",    "Time between instruction cue and Go cue (s)" },
        { "Move",    "Movement time window after Go cue, before results (s)" },
        { "TS",      "Testing Stimulus delay from Go cue (ms)\n'.' or empty = No pulse" },
        { "CS",      "Conditioning Stimulus offset from TS (ms)\n'.' or empty = No pulse" },
        { "Inst",    "Instruction: 0=REST  1=REACH  2=REACH+GRASP" },
    };

    [MenuItem("Tools/Attach Tooltip Triggers to Headers")]
    public static void AttachTriggers()
    {
        var headerRowGo = GameObject.Find("HeaderRow");
        if (headerRowGo == null) { Debug.LogError("[Fixer6] HeaderRow not found"); return; }

        var headerRoot = headerRowGo.transform;

        for (int i = 0; i < headerRoot.childCount; i++)
        {
            var child = headerRoot.GetChild(i);

            // TMP_Text is already a Graphic — just enable raycastTarget on it
            var tmp = child.GetComponent<TMP_Text>()
                   ?? child.GetComponentInChildren<TMP_Text>();
            if (tmp != null)
            {
                tmp.raycastTarget = true;
                EditorUtility.SetDirty(tmp);
            }

            // Add or get HeaderTooltipTrigger on the same GameObject
            var trigger = child.GetComponent<HeaderTooltipTrigger>();
            if (trigger == null)
                trigger = child.gameObject.AddComponent<HeaderTooltipTrigger>();

            // Set tooltip text via SerializedObject so it persists
            string headerText = tmp != null ? tmp.text : child.name;
            if (!TooltipTexts.TryGetValue(headerText, out string tip))
                tip = headerText;

            var trigSo = new SerializedObject(trigger);
            trigSo.FindProperty("tooltipText").stringValue = tip;
            trigSo.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(trigger);
            EditorUtility.SetDirty(child.gameObject);

            Debug.Log($"[Fixer6] '{child.name}' → trigger attached, tip='{tip}'");
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        bool saved = EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log($"[Fixer6] Scene saved: {saved}");
        EditorUtility.DisplayDialog("Done", "Triggers attached and scene saved.", "OK");
    }
}
