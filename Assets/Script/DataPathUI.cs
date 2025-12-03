using UnityEngine;
using TMPro;
using SimpleFileBrowser;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor; // for Editor-only Browse
#endif

public class DataPathUI : MonoBehaviour
{
    [SerializeField] TMP_InputField folderInput;
    [SerializeField] TMP_Text statusText; // small label to show “OK” / errors

    void OnEnable()
    {
        if (DataPathManager.Instance != null && !string.IsNullOrEmpty(DataPathManager.Instance.ParticipantFolder))
            folderInput.text = DataPathManager.Instance.ParticipantFolder;
    }

    public void ApplyFolderFromInput()
    {
        var ok = DataPathManager.Instance.TrySetFolder(folderInput.text, out string err);
        statusText.text = ok ? "Folder set ✔" : $"Error: {err}";
    }

#if UNITY_EDITOR
    public void BrowseInEditor()
    {
        string path = EditorUtility.OpenFolderPanel("Select participant folder", "", "");
        if (!string.IsNullOrEmpty(path))
        {
            folderInput.text = path;
            ApplyFolderFromInput();
        }
    }
#endif

    public void BrowseRuntime()
{
    StartCoroutine(PickFolderCoroutine());
}

IEnumerator PickFolderCoroutine()
{
    FileBrowser.SetDefaultFilter(null);

    // pickMode, allowMultiSelection, initialPath, title, loadButtonText
    yield return FileBrowser.WaitForLoadDialog(
        FileBrowser.PickMode.Folders, false, null,
        "Select participant folder", "Select"
    );

    if (FileBrowser.Success)
    {
        string selectedFolder = FileBrowser.Result[0];
        if (DataPathManager.Instance.TrySetFolder(selectedFolder, out string err))
        {
            if (folderInput) folderInput.text = selectedFolder;
            if (statusText)  statusText.text  = "Folder set o";
        }
        else if (statusText) statusText.text = $"Error: {err}";
    }
}


}
