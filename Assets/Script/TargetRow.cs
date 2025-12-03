// TargetRow.cs
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TargetRow : MonoBehaviour
{
    public TMP_Text idText;
    public TMP_InputField diameter, posX, posY, posZ;
    public Image background;

    TargetTableController controller;

    public void Init(TargetTableController owner) { controller = owner; }

    public void SetId(int id)
    {
        if (!idText) { Debug.LogError("TargetRow: idText not assigned.", this); return; }
        idText.text = id.ToString();
    }

    public void SelectMe() { controller?.SelectRow(this); }

    public void SetSelected(bool on)
    {
        if (background) background.color = on ? new Color(0.85f,0.92f,1f) : Color.white;
    }
}
