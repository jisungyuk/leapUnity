using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class TargetSceneRWRFixer
{
    static readonly Dictionary<string, string> TooltipTexts = new Dictionary<string, string>
    {
        { "ID",           "Target ID number (referenced by session trials)" },
        { "angle(deg)",   "Target angle from starting position (degrees)\n0=Right  90=Forward  180=Left  270=Back" },
        { "Distance(cm)", "Target distance from starting position (cm)" },
        { "cm",           "Target sphere diameter (cm)" },
    };

    [MenuItem("Tools/Setup Target Scene Tooltips")]
    public static void Setup()
    {
        // ── 1. Ensure TooltipUI exists in this scene ─────────────────
        var tooltipUI = Object.FindObjectOfType<TooltipUI>();
        if (tooltipUI == null)
        {
            var canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null) { Debug.LogError("[TargetFixer] Canvas not found"); return; }

            var tooltipRoot = new GameObject("TooltipUI");
            tooltipRoot.transform.SetParent(canvas.transform, false);
            tooltipUI = tooltipRoot.AddComponent<TooltipUI>();

            // Panel
            var panel = new GameObject("Panel");
            panel.transform.SetParent(tooltipRoot.transform, false);
            var panelRT  = panel.AddComponent<RectTransform>();
            panelRT.sizeDelta = new Vector2(240f, 64f);
            panelRT.pivot     = new Vector2(0f, 1f);
            var img = panel.AddComponent<Image>();
            img.color = new Color(0.1f, 0.1f, 0.1f, 0.88f);

            // Text
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(panel.transform, false);
            var textRT    = textGo.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(8f, 6f);
            textRT.offsetMax = new Vector2(-8f, -6f);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.fontSize          = 13f;
            tmp.color             = Color.white;
            tmp.enableWordWrapping = true;

            // Wire fields
            var so = new SerializedObject(tooltipUI);
            so.FindProperty("panel").objectReferenceValue = panelRT;
            so.FindProperty("label").objectReferenceValue = tmp;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(tooltipRoot);
            Debug.Log("[TargetFixer] TooltipUI created.");
        }
        else
        {
            Debug.Log("[TargetFixer] TooltipUI already exists.");
        }

        // ── 2. Find HeaderRow and attach triggers ────────────────────
        var headerRowGo = GameObject.Find("HeaderRow");
        if (headerRowGo == null) { Debug.LogError("[TargetFixer] HeaderRow not found"); return; }

        var headerRoot = headerRowGo.transform;

        for (int i = 0; i < headerRoot.childCount; i++)
        {
            var child = headerRoot.GetChild(i);

            var txtComp = child.GetComponent<TMP_Text>()
                       ?? child.GetComponentInChildren<TMP_Text>();
            if (txtComp != null)
            {
                txtComp.raycastTarget = true;
                EditorUtility.SetDirty(txtComp);
            }

            var trigger = child.GetComponent<HeaderTooltipTrigger>();
            if (trigger == null)
                trigger = child.gameObject.AddComponent<HeaderTooltipTrigger>();

            string headerText = txtComp != null ? txtComp.text : child.name;
            if (!TooltipTexts.TryGetValue(headerText, out string tip))
                tip = headerText;

            var trigSo = new SerializedObject(trigger);
            trigSo.FindProperty("tooltipText").stringValue = tip;
            trigSo.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(trigger);
            EditorUtility.SetDirty(child.gameObject);

            Debug.Log($"[TargetFixer] '{child.name}' text='{headerText}' → tip='{tip}'");
        }

        // ── 3. Save ──────────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        bool saved = EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log($"[TargetFixer] Scene saved: {saved}");
        EditorUtility.DisplayDialog("Done", "Target scene tooltips set up!", "OK");
    }
}
