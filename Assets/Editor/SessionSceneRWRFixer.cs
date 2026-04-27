using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class SessionSceneRWRFixer
{
    [MenuItem("Tools/Fix RWR Session Scene")]
    public static void Fix()
    {
        // ── Find HeaderRow ───────────────────────────────────────────
        var headerRowGo = GameObject.Find("HeaderRow");
        if (headerRowGo == null) { Debug.LogError("[SceneFixer] HeaderRow not found"); return; }
        var headerRoot = headerRowGo.transform;

        // ── Collect current header children by text content ──────────
        var headerByText = new Dictionary<string, Transform>();
        for (int i = 0; i < headerRoot.childCount; i++)
        {
            var child = headerRoot.GetChild(i);
            var txt   = child.GetComponent<TMP_Text>();
            if (txt == null) txt = child.GetComponentInChildren<TMP_Text>();
            if (txt != null)
                headerByText[txt.text] = child;
        }

        // ── Rename old header texts to new names ─────────────────────
        Rename(headerByText, "TTL",         "TS");
        Rename(headerByText, "startX",      "Start_r");
        Rename(headerByText, "startY",      "Hold");
        Rename(headerByText, "startZ",      "Wait");
        Rename(headerByText, "Instruction", "Inst");

        // ── Add missing headers (Move and CS) ────────────────────────
        // Use any existing header child as the template
        Transform template = headerRoot.GetChild(0);

        if (!headerByText.ContainsKey("Move"))
        {
            var go  = Object.Instantiate(template.gameObject, headerRoot);
            go.name = "Move";
            var txt = go.GetComponent<TMP_Text>() ?? go.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = "Move";
            headerByText["Move"] = go.transform;
        }

        if (!headerByText.ContainsKey("CS"))
        {
            var go  = Object.Instantiate(template.gameObject, headerRoot);
            go.name = "CS";
            var txt = go.GetComponent<TMP_Text>() ?? go.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = "CS";
            headerByText["CS"] = go.transform;
        }

        // ── Set sibling order to match row column order ───────────────
        // Row order: Trial(#), Hand, target, StartR, Hold, Wait, Move, TS, CS, Instruction
        string[] order = { "#", "Hand", "Target", "Start_r", "Hold", "Wait", "Move", "TS", "CS", "Inst" };
        for (int i = 0; i < order.Length; i++)
        {
            if (headerByText.TryGetValue(order[i], out Transform t))
                t.SetSiblingIndex(i);
            else
                Debug.LogWarning($"[SceneFixer] Header '{order[i]}' not found after rename");
        }

        // ── Make HeaderRow stretch to full parent width ───────────────
        var headerRT = headerRowGo.GetComponent<RectTransform>();
        if (headerRT != null)
        {
            headerRT.anchorMin        = new Vector2(0f, headerRT.anchorMin.y);
            headerRT.anchorMax        = new Vector2(1f, headerRT.anchorMax.y);
            headerRT.offsetMin        = new Vector2(0f,  headerRT.offsetMin.y);
            headerRT.offsetMax        = new Vector2(0f,  headerRT.offsetMax.y);
        }

        // ── Mark dirty and save scene ────────────────────────────────
        EditorUtility.SetDirty(headerRowGo);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

        Debug.Log("[SceneFixer] RWR Session scene updated successfully.");
        EditorUtility.DisplayDialog("Done", "RWR Session scene updated!", "OK");
    }

    // Rename header text and update dictionary key
    static void Rename(Dictionary<string, Transform> dict, string oldText, string newText)
    {
        if (!dict.TryGetValue(oldText, out Transform t)) return;

        var txt = t.GetComponent<TMP_Text>() ?? t.GetComponentInChildren<TMP_Text>();
        if (txt != null) txt.text = newText;
        t.name = newText;

        dict.Remove(oldText);
        dict[newText] = t;
    }
}
