using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Attach to a header cell. Shows a tooltip on mouse enter, hides on exit.
/// </summary>
public class HeaderTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [TextArea(2, 4)]
    [SerializeField] string tooltipText;

    public void OnPointerEnter(PointerEventData _) => TooltipUI.Instance?.Show(tooltipText);
    public void OnPointerExit(PointerEventData _)  => TooltipUI.Instance?.Hide();

    void OnDisable() => TooltipUI.Instance?.Hide();
}
