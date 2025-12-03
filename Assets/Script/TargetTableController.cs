using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Collections;                 // for IEnumerator coroutines
using System.Globalization;
using SimpleFileBrowser;                  // for runtime file dialogs

public class TargetTableController : MonoBehaviour
{
    [Header("Table Wiring")]
    [SerializeField] Transform content;        // TargetTable/Viewport/Content
    [SerializeField] GameObject rowPrefab;     // TargetRow prefab

    [Header("Status (optional)")]
    [SerializeField] TMP_Text statusText;

    // Internal state
    private readonly List<TargetRow> rows = new();
    private TargetRow selected;
    private int nextId = 1;

    void Awake()
    {
        if (!content)   Debug.LogError("[TargetTableController] Content not assigned!", this);
        if (!rowPrefab) Debug.LogError("[TargetTableController] Row Prefab not assigned!", this);
    }

    void Start()
    {
        // Restore from runtime cache if available
        var store = RuntimeConfigStore.Instance;
        if (store != null && store.Targets.Count > 0)
        {
            RestoreFromCache(store.Targets);
            SetStatus("Restored targets from memory");
        }
    }

    void OnDisable()
    {
        SnapshotToCache(); // save current table into memory before scene unloads
    }

    // -------- UI Hooks --------

    public void AddTarget()
    {
        var go  = Instantiate(rowPrefab, content);
        var row = go.GetComponent<TargetRow>();
        row.Init(this);
        row.SetId(nextId++);
        rows.Add(row);
        SelectRow(row);
    }

    public void DeleteSelected()
{
    // 🔹 Shift 누르고 Delete → 전체 삭제
    bool shift =
        Input.GetKey(KeyCode.LeftShift) ||
        Input.GetKey(KeyCode.RightShift);

    if (shift)
    {
        ClearAll();
        SnapshotToCache();
        SetStatus("All trials cleared.");
        return;
    }

    // 🔹 기존 단일 삭제 로직
    if (rows.Count == 0)
    {
        SetStatus("No rows to delete.");
        return;
    }

    var victim = selected ?? rows[^1];
    rows.Remove(victim);
    Destroy(victim.gameObject);
    Renumber();
    selected = null;
    SnapshotToCache();
}

    public void SelectRow(TargetRow row)
    {
        selected = row;
        foreach (var r in rows) r.SetSelected(r == selected);
    }

    public void SaveCsv()
{
    if (rows.Count == 0)
    {
        SetStatus("No targets to save.");
        return;
    }

    if (DataPathManager.Instance == null ||
        string.IsNullOrEmpty(DataPathManager.Instance.ParticipantFolder))
    {
        SetStatus("No participant folder set. (Main Menu → Choose folder)");
        return;
    }

    var sb = new StringBuilder();

    // *** GameSessionController 요구 포맷 ***
    sb.AppendLine("ID,cm,x,y,z");

    for (int i = 0; i < rows.Count; i++)
    {
        var r = rows[i];

        string id = (i + 1).ToString();

        string cm = (r.diameter.text ?? "").Trim().Replace(',', '.');
        string x  = (r.posX.text     ?? "").Trim().Replace(',', '.');
        string y  = (r.posY.text     ?? "").Trim().Replace(',', '.');
        string z  = (r.posZ.text     ?? "").Trim().Replace(',', '.');

        if (string.IsNullOrWhiteSpace(cm)) cm = "0";
        if (string.IsNullOrWhiteSpace(x))  x  = "0";
        if (string.IsNullOrWhiteSpace(y))  y  = "0";
        if (string.IsNullOrWhiteSpace(z))  z  = "0";

        sb.AppendLine($"{id},{cm},{x},{y},{z}");
    }

    string file = $"targets_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv";
    string path = DataPathManager.Instance.PathInParticipantFolder(file);
    Directory.CreateDirectory(Path.GetDirectoryName(path));
    File.WriteAllText(path, sb.ToString(), Encoding.UTF8);

    SnapshotToCache();
    SetStatus($"Saved: {path}");
    Debug.Log($"[TargetTable] Saved CSV → {path}");
}


    public void LoadCsv()
    {
        StartCoroutine(LoadCsvRoutine());
    }

    // -------- Coroutines --------

    IEnumerator LoadCsvRoutine()
    {
        FileBrowser.SetFilters(true, new FileBrowser.Filter("CSV", ".csv"));
        FileBrowser.SetDefaultFilter(".csv");

        string initial = (DataPathManager.Instance != null &&
                          !string.IsNullOrEmpty(DataPathManager.Instance.ParticipantFolder))
                         ? DataPathManager.Instance.ParticipantFolder
                         : null;

        yield return FileBrowser.WaitForLoadDialog(
            FileBrowser.PickMode.Files, false, initial, "Load targets CSV", "Load");

        if (!FileBrowser.Success)
        {
            SetStatus("Load canceled.");
            yield break;
        }

        string path = FileBrowser.Result[0];
        if (!File.Exists(path))
        {
            SetStatus("File not found.");
            yield break;
        }

        var lines = File.ReadAllLines(path, Encoding.UTF8);
        ClearAll();

        // Target header: ID, cm, x,y,z
        int start = (lines.Length > 0 && lines[0].TrimStart().StartsWith("ID"))
                    ? 1 : 0;

        int idCounter = 1;
        for (int i = start; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var c = line.Split(',');
            if (c.Length < 5) continue; // ID,cm,x,y,z

            var go  = Instantiate(rowPrefab, content);
            var row = go.GetComponent<TargetRow>();
            row.Init(this);
            row.SetId(idCounter++);
            rows.Add(row);

            // c[0] is ID in file; we ignore and use our own numbering
            row.diameter.text = c[1].Trim().Replace(',', '.');
            row.posX.text     = c[2].Trim().Replace(',', '.');
            row.posY.text     = c[3].Trim().Replace(',', '.');
            row.posZ.text     = c[4].Trim().Replace(',', '.');
        }

        nextId = rows.Count + 1;
        SelectRow(null);

        SnapshotToCache();
        SetStatus($"Loaded: {path}");
        Debug.Log($"[TargetTable] Loaded CSV ← {path}");
    }

    // -------- Helpers --------

    void Renumber()
    {
        for (int i = 0; i < rows.Count; i++)
            rows[i].SetId(i + 1);
        nextId = rows.Count + 1;
    }

    void ClearAll()
    {
        foreach (var r in rows) if (r) Destroy(r.gameObject);
        rows.Clear();
        nextId  = 1;
        selected = null;
    }

    void SetStatus(string msg)
    {
        if (statusText) statusText.text = msg;
        Debug.Log($"[TargetTable] {msg}");
    }

    void SnapshotToCache()
    {
        var store = RuntimeConfigStore.Instance;
        if (store == null) return;

        var list = new List<RuntimeConfigStore.TargetSpec>();
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            var spec = new RuntimeConfigStore.TargetSpec
            {
                id = i + 1,
                cm = ParseFloat(r.diameter.text),
                x  = ParseFloat(r.posX.text),
                y  = ParseFloat(r.posY.text),
                z  = ParseFloat(r.posZ.text)
            };
            list.Add(spec);
        }
        store.SetTargets(list);
    }

    void RestoreFromCache(List<RuntimeConfigStore.TargetSpec> list)
    {
        ClearAll();
        int idCounter = 1;
        foreach (var t in list)
        {
            var go  = Instantiate(rowPrefab, content);
            var row = go.GetComponent<TargetRow>();
            row.Init(this);
            row.SetId(idCounter++);
            rows.Add(row);

            row.diameter.text = t.cm.ToString(CultureInfo.InvariantCulture);
            row.posX.text     = t.x.ToString(CultureInfo.InvariantCulture);
            row.posY.text     = t.y.ToString(CultureInfo.InvariantCulture);
            row.posZ.text     = t.z.ToString(CultureInfo.InvariantCulture);
        }
        nextId = rows.Count + 1;
        SelectRow(null);
    }

    static float ParseFloat(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0f;
        s = s.Replace(',', '.');
        float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v);
        return v;
    }
}
