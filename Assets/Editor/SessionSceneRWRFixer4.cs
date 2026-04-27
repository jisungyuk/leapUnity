using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class SessionSceneRWRFixer4
{
    [MenuItem("Tools/Setup Header Tooltips")]
    public static void Setup()
    {
        // ── 1. Find Canvas ───────────────────────────────────────────
        var canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null) { Debug.LogError("[Fixer4] Canvas not found"); return; }

        // ── 2. Create Tooltip panel as Canvas child ──────────────────
        var tooltipRoot = new GameObject("TooltipUI");
        tooltipRoot.transform.SetParent(canvas.transform, false);

        // Attach TooltipUI script to canvas-level object
        var tooltipUI = tooltipRoot.AddComponent<TooltipUI>();

        // Inner panel (background + text)
        var panel = new GameObject("Panel");
        panel.transform.SetParent(tooltipRoot.transform, false);

        var panelRT = panel.AddComponent<RectTransform>();
        panelRT.sizeDelta = new Vector2(220f, 60f);
        panelRT.pivot     = new Vector2(0f, 1f);

        var img = panel.AddComponent<Image>();
        img.color = new Color(0.1f, 0.1f, 0.1f, 0.88f);

        // Rounded look via sprite (Unity default)
        img.type = Image.Type.Sliced;

        // Text inside panel
        var textGo = new GameObject("Text");
        textGo.transform.SetParent(panel.transform, false);

        var textRT = textGo.AddComponent<RectTransform>();
        textRT.anchorMin  = Vector2.zero;
        textRT.anchorMax  = Vector2.one;
        textRT.offsetMin  = new Vector2(8f, 6f);
        textRT.offsetMax  = new Vector2(-8f, -6f);

        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.fontSize      = 13f;
        tmp.color         = Color.white;
        tmp.enableWordWrapping = true;

        // Wire TooltipUI serialized fields
        var so = new SerializedObject(tooltipUI);
        so.FindProperty("panel").objectReferenceValue = panelRT;
        so.FindProperty("label").objectReferenceValue = tmp;
        so.ApplyModifiedPropertiesWithoutUndo();

        // ── 3. Find HeaderRow and wire triggers ──────────────────────
        var headerRowGo = GameObject.Find("HeaderRow");
        if (headerRowGo == null) { Debug.LogError("[Fixer4] HeaderRow not found"); return; }

        // Tooltip text per column header (matched by TMP_Text.text content)
        var tooltips = new Dictionary<string, string>
        {
            { "#",       "Trial number" },
            { "Hand",    "Hand: 0=Left  1=Right  2=Both" },
            { "Target",  "Target ID (must match Target Table)" },
            { "Start_r", "Start zone radius (cm)" },
            { "Hold",    "Time to hold inside start zone before direction cue (s)" },
            { "Wait",    "Wait-for-Go delay after direction cue (s)" },
            { "Move",    "Movement execution window (s)" },
            { "TS",      "Testing Stimulus delay from Go cue (ms)\n'.' or empty = No pulse" },
            { "CS",      "Conditioning Stimulus offset from TS (ms)\n'.' or empty = No pulse" },
            { "Inst",    "Instruction: 0=REST  1=REACH  2=REACH+GRASP" },
        };

        var headerRoot = headerRowGo.transform;
        for (int i = 0; i < headerRoot.childCount; i++)
        {
            var child = headerRoot.GetChild(i);

            // Need a Raycast target so EventSystem detects hover
            var img2 = child.GetComponent<Image>();
            if (img2 == null)
            {
                img2 = child.gameObject.AddComponent<Image>();
                img2.color        = Color.clear; // invisible but raycast-able
                img2.raycastTarget = true;
            }
            else
            {
                img2.raycastTarget = true;
            }

            // Find matching tooltip text
            var txt = child.GetComponent<TMP_Text>()
                   ?? child.GetComponentInChildren<TMP_Text>();
            string headerText = txt != null ? txt.text : "";

            if (!tooltips.TryGetValue(headerText, out string tip))
                tip = headerText;

            // Add / overwrite HeaderTooltipTrigger
            var trigger = child.GetComponent<HeaderTooltipTrigger>();
            if (trigger == null)
                trigger = child.gameObject.AddComponent<HeaderTooltipTrigger>();

            var trigSo = new SerializedObject(trigger);
            trigSo.FindProperty("tooltipText").stringValue = tip;
            trigSo.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(child.gameObject);
        }

        // ── 4. Save ──────────────────────────────────────────────────
        EditorUtility.SetDirty(canvas.gameObject);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

        Debug.Log("[Fixer4] Tooltip system set up successfully.");
        EditorUtility.DisplayDialog("Done", "Header tooltips set up!", "OK");
    }
}
