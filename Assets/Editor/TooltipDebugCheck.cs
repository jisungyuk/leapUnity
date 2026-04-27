using UnityEngine;
using UnityEditor;
using TMPro;
using UnityEngine.UI;

public class TooltipDebugCheck
{
    [MenuItem("Tools/Debug Tooltip Setup")]
    public static void Check()
    {
        // 1. Check TooltipUI
        var tooltipUI = Object.FindObjectOfType<TooltipUI>();
        if (tooltipUI == null)
            Debug.LogError("[TooltipDebug] TooltipUI NOT FOUND in scene.");
        else
            Debug.Log($"[TooltipDebug] TooltipUI found on: {tooltipUI.gameObject.name}");

        // 2. Check HeaderRow
        var headerRowGo = GameObject.Find("HeaderRow");
        if (headerRowGo == null) { Debug.LogError("[TooltipDebug] HeaderRow NOT FOUND"); return; }

        var headerRoot = headerRowGo.transform;
        Debug.Log($"[TooltipDebug] HeaderRow has {headerRoot.childCount} children");

        for (int i = 0; i < headerRoot.childCount; i++)
        {
            var child = headerRoot.GetChild(i);
            var tmp     = child.GetComponent<TMP_Text>() ?? child.GetComponentInChildren<TMP_Text>();
            var img     = child.GetComponent<Image>();
            var trigger = child.GetComponent<HeaderTooltipTrigger>();

            Debug.Log($"[TooltipDebug] Header[{i}] name='{child.name}' " +
                      $"text='{tmp?.text}' " +
                      $"tmp.raycast={tmp?.raycastTarget} " +
                      $"img={img != null} img.raycast={img?.raycastTarget} " +
                      $"trigger={trigger != null}");
        }

        // 3. Check GraphicRaycaster on Canvas
        var raycaster = Object.FindObjectOfType<GraphicRaycaster>();
        if (raycaster == null)
            Debug.LogError("[TooltipDebug] No GraphicRaycaster found on Canvas!");
        else
            Debug.Log($"[TooltipDebug] GraphicRaycaster found on: {raycaster.gameObject.name}");

        // 4. Check EventSystem
        var es = Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
        Debug.Log(es != null
            ? $"[TooltipDebug] EventSystem found: {es.gameObject.name}"
            : "[TooltipDebug] EventSystem NOT FOUND");
    }
}
