using UnityEngine;
using TMPro;

/// <summary>
/// Singleton tooltip panel. Follows the mouse while visible.
/// Call Show(text) / Hide() from any trigger component.
/// </summary>
public class TooltipUI : MonoBehaviour
{
    public static TooltipUI Instance { get; private set; }

    [SerializeField] RectTransform panel;
    [SerializeField] TMP_Text      label;
    [SerializeField] Vector2       offset = new Vector2(12f, -20f); // offset from cursor

    Canvas parentCanvas;

    void Awake()
    {
        Instance = this;
        parentCanvas = GetComponentInParent<Canvas>();
        panel.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!panel.gameObject.activeSelf) return;

        // Use null camera for ScreenSpaceOverlay, worldCamera otherwise
        Camera cam = (parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                     ? null : parentCanvas.worldCamera;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentCanvas.GetComponent<RectTransform>(),
            Input.mousePosition,
            cam,
            out Vector2 localPos);

        panel.localPosition = localPos + offset;
    }

    public void Show(string text)
    {
        label.text = text;
        panel.gameObject.SetActive(true);
        panel.SetAsLastSibling(); // always on top
    }

    public void Hide()
    {
        panel.gameObject.SetActive(false);
    }
}
