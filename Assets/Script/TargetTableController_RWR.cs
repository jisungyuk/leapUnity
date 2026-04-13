using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Collections;
using System.Globalization;
using SimpleFileBrowser;

public class TargetTableController_RWR : MonoBehaviour
{
    [Header("Table Wiring")]
    [SerializeField] Transform  content;
    [SerializeField] GameObject rowPrefab;   // TargetRow_RWR prefab

    [Header("Status (optional)")]
    [SerializeField] TMP_Text statusText;

    private readonly List<TargetRow_RWR> rows = new();
    private TargetRow_RWR selected;
    private int nextId = 1;

    void Awake()
    {
        if (!content)   Debug.LogError("[TargetTableController_RWR] Content not assigned!", this);
        if (!rowPrefab) Debug.LogError("[TargetTableController_RWR] Row Prefab not assigned!", this);
    }

    void Start()
    {
        var store = RuntimeConfigStore.Instance;
        if (store != null && store.RwrTargets.Count > 0)
        {
            RestoreFromCache(store.RwrTargets);
            SetStatus("Restored RWR targets from memory");
        }
    }

    void OnDisable()
    {
        SnapshotToCache();
    }

    // -------- UI Hooks --------

    public void AddTarget()
    {
        var go  = Instantiate(rowPrefab, content);
        var row = go.GetComponent<TargetRow_RWR>();
        row.Init(this);
        row.SetId(nextId++);

        // Default values
        row.angleDeg.text   = "90";
        row.distanceCm.text = "20";
        row.diameter.text   = "15";

        rows.Add(row);
        SelectRow(row);
    }

    public void DeleteSelected()
    {
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (shift)
        {
            ClearAll();
            SnapshotToCache();
            SetStatus("All targets cleared.");
            return;
        }

        if (rows.Count == 0) { SetStatus("No rows to delete."); return; }

        var victim = selected ?? rows[^1];
        rows.Remove(victim);
        Destroy(victim.gameObject);
        Renumber();
        selected = null;
        SnapshotToCache();
    }

    public void SelectRow(TargetRow_RWR row)
    {
        selected = row;
        foreach (var r in rows) r.SetSelected(r == selected);
    }

    public void SaveCsv()
    {
        if (rows.Count == 0) { SetStatus("No targets to save."); return; }

        if (DataPathManager.Instance == null ||
            string.IsNullOrEmpty(DataPathManager.Instance.ParticipantFolder))
        {
            SetStatus("No participant folder set.");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("ID,cm,angle_deg,distance_cm");

        for (int i = 0; i < rows.Count; i++)
        {
            var r  = rows[i];
            string id   = (i + 1).ToString();
            string cm   = (r.diameter.text    ?? "").Trim().Replace(',', '.');
            string ang  = (r.angleDeg.text    ?? "").Trim().Replace(',', '.');
            string dist = (r.distanceCm.text  ?? "").Trim().Replace(',', '.');

            if (string.IsNullOrWhiteSpace(cm))   cm   = "0";
            if (string.IsNullOrWhiteSpace(ang))  ang  = "0";
            if (string.IsNullOrWhiteSpace(dist)) dist = "0";

            sb.AppendLine($"{id},{cm},{ang},{dist}");
        }

        string file = $"targets_rwr_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv";
        string path = DataPathManager.Instance.PathInParticipantFolder(file);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);

        SnapshotToCache();
        SetStatus($"Saved: {path}");
        Debug.Log($"[TargetTableRWR] Saved → {path}");
    }

    public void LoadCsv()
    {
        StartCoroutine(LoadCsvRoutine());
    }

    IEnumerator LoadCsvRoutine()
    {
        FileBrowser.SetFilters(true, new FileBrowser.Filter("CSV", ".csv"));
        FileBrowser.SetDefaultFilter(".csv");

        string initial = (DataPathManager.Instance != null &&
                          !string.IsNullOrEmpty(DataPathManager.Instance.ParticipantFolder))
                         ? DataPathManager.Instance.ParticipantFolder : null;

        yield return FileBrowser.WaitForLoadDialog(
            FileBrowser.PickMode.Files, false, initial, "Load RWR targets CSV", "Load");

        if (!FileBrowser.Success) { SetStatus("Load canceled."); yield break; }

        string path = FileBrowser.Result[0];
        if (!File.Exists(path)) { SetStatus("File not found."); yield break; }

        var lines = File.ReadAllLines(path, Encoding.UTF8);
        ClearAll();

        int start = (lines.Length > 0 && lines[0].TrimStart().StartsWith("ID")) ? 1 : 0;
        int idCounter = 1;

        for (int i = start; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var c = line.Split(',');
            if (c.Length < 4) continue;   // ID, cm, angle_deg, distance_cm

            var go  = Instantiate(rowPrefab, content);
            var row = go.GetComponent<TargetRow_RWR>();
            row.Init(this);
            row.SetId(idCounter++);
            rows.Add(row);

            row.diameter.text   = c[1].Trim().Replace(',', '.');
            row.angleDeg.text   = c[2].Trim().Replace(',', '.');
            row.distanceCm.text = c[3].Trim().Replace(',', '.');
        }

        nextId = rows.Count + 1;
        SelectRow(null);
        SnapshotToCache();
        SetStatus($"Loaded: {path}");
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
        nextId   = 1;
        selected = null;
    }

    void SetStatus(string msg)
    {
        if (statusText) statusText.text = msg;
        Debug.Log($"[TargetTableRWR] {msg}");
    }

    void SnapshotToCache()
    {
        var store = RuntimeConfigStore.Instance;
        if (store == null) return;

        var list = new List<RuntimeConfigStore.RwrTargetSpec>();
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            list.Add(new RuntimeConfigStore.RwrTargetSpec
            {
                id           = i + 1,
                cm           = ParseFloat(r.diameter.text),
                angle_deg    = ParseFloat(r.angleDeg.text),
                distance_cm  = ParseFloat(r.distanceCm.text)
            });
        }
        store.SetRwrTargets(list);
    }

    void RestoreFromCache(List<RuntimeConfigStore.RwrTargetSpec> list)
    {
        ClearAll();
        int idCounter = 1;
        foreach (var t in list)
        {
            var go  = Instantiate(rowPrefab, content);
            var row = go.GetComponent<TargetRow_RWR>();
            row.Init(this);
            row.SetId(idCounter++);
            rows.Add(row);

            row.diameter.text   = t.cm.ToString(CultureInfo.InvariantCulture);
            row.angleDeg.text   = t.angle_deg.ToString(CultureInfo.InvariantCulture);
            row.distanceCm.text = t.distance_cm.ToString(CultureInfo.InvariantCulture);
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
