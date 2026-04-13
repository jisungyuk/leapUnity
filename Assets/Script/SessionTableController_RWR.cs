using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Collections;
using System.Globalization;
using SimpleFileBrowser;

public class SessionTableController_RWR : MonoBehaviour
{
    [Header("Table Wiring")]
    [SerializeField] Transform content;
    [SerializeField] GameObject rowPrefab;
    [SerializeField] TMP_InputField duplicateCountInput;

    [Header("Status (optional)")]
    [SerializeField] TMP_Text statusText;

    private readonly List<SessionRow_RWR> rows = new();
    private SessionRow_RWR selected;
    private SessionRow_RWR lastClicked;
    private int nextIndex = 1;

    void Awake()
    {
        if (!content)   Debug.LogError("[SessionTableController_RWR] Content not assigned!", this);
        if (!rowPrefab) Debug.LogError("[SessionTableController_RWR] Row Prefab not assigned!", this);
    }

    void Start()
    {
        var store = RuntimeConfigStore.Instance;
        if (store != null && store.Trials.Count > 0)
        {
            RestoreFromCache(store.Trials);
            SetStatus("Restored session from memory");
        }
    }

    void OnDisable()
    {
        SnapshotToCache();
    }

    // -------- UI Hooks --------

    public void AddTrial()
    {
        var go  = Instantiate(rowPrefab, content);
        var row = go.GetComponent<SessionRow_RWR>();
        row.Init(this);
        row.SetIndex(nextIndex++);
        rows.Add(row);
        SelectRow(row);
    }

    public void DuplicateSelected()
    {
        if (rows.Count == 0) { SetStatus("No rows to duplicate."); return; }

        int count = 1;
        if (duplicateCountInput != null)
        {
            int.TryParse(duplicateCountInput.text, out count);
            if (count <= 0) count = 1;
        }

        var src = selected ?? rows[^1];
        int insertIndex = rows.IndexOf(src) + 1;

        for (int k = 0; k < count; k++)
        {
            var go  = Instantiate(rowPrefab, content);
            var row = go.GetComponent<SessionRow_RWR>();
            row.Init(this);

            row.targetId.text    = src.targetId.text;
            row.startX.text      = src.startX.text;
            row.startY.text      = src.startY.text;
            row.startZ.text      = src.startZ.text;
            row.hand.text        = src.hand.text;
            row.ttl1.text        = src.ttl1.text;
            if (row.ttl2Offset) row.ttl2Offset.text = src.ttl2Offset ? src.ttl2Offset.text : "";
            row.instruction.text = src.instruction.text;

            rows.Insert(insertIndex + k, row);
        }

        Renumber();
        SnapshotToCache();
        SetStatus($"Duplicated trial x{count}");
    }

    public void DeleteSelected()
    {
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (shift)
        {
            ClearAll();
            SnapshotToCache();
            SetStatus("All trials cleared.");
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

    public void SelectRow(SessionRow_RWR row)
    {
        SelectRow(row, false);
    }

    public void SelectRow(SessionRow_RWR row, bool shift)
    {
        if (!shift || lastClicked == null || !rows.Contains(lastClicked))
        {
            selected    = row;
            lastClicked = row;
            foreach (var r in rows) r.SetSelected(r == row);
            return;
        }

        int i1 = rows.IndexOf(lastClicked);
        int i2 = rows.IndexOf(row);
        if (i1 > i2) (i1, i2) = (i2, i1);

        for (int i = 0; i < rows.Count; i++)
            rows[i].SetSelected(i >= i1 && i <= i2);

        selected    = row;
        lastClicked = row;
    }

    public void SaveCsv()
    {
        if (rows.Count == 0) { SetStatus("No trials to save."); return; }

        if (DataPathManager.Instance == null ||
            string.IsNullOrEmpty(DataPathManager.Instance.ParticipantFolder))
        {
            SetStatus("No participant folder set. (Main Menu → Choose folder)");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("#,target,startx,starty,startz,hand,ttl1,ttl2_offset,instruction");

        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            string trial  = (i + 1).ToString();
            string target = (r.targetId.text    ?? "").Trim();
            string sx     = (r.startX.text      ?? "").Trim().Replace(',', '.');
            string sy     = (r.startY.text      ?? "").Trim().Replace(',', '.');
            string sz     = (r.startZ.text      ?? "").Trim().Replace(',', '.');
            string hnd    = (r.hand.text         ?? "").Trim();
            string ttl    = (r.ttl1.text        ?? "").Trim().Replace(',', '.');
            string ttl2   = (r.ttl2Offset != null ? r.ttl2Offset.text : "").Trim().Replace(',', '.');
            string inst   = (r.instruction.text ?? "").Trim();

            sb.AppendLine($"{trial},{target},{sx},{sy},{sz},{hnd},{ttl},{ttl2},{inst}");
        }

        string file = $"session_rwr_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv";
        string path = DataPathManager.Instance.PathInParticipantFolder(file);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);

        SnapshotToCache();
        SetStatus($"Saved: {path}");
        Debug.Log($"[SessionTableRWR] Saved CSV → {path}");
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
                         ? DataPathManager.Instance.ParticipantFolder
                         : null;

        yield return FileBrowser.WaitForLoadDialog(
            FileBrowser.PickMode.Files, false, initial, "Load RWR session CSV", "Load");

        if (!FileBrowser.Success) { SetStatus("Load canceled."); yield break; }

        string path = FileBrowser.Result[0];
        if (!File.Exists(path)) { SetStatus("File not found."); yield break; }

        var lines = File.ReadAllLines(path, Encoding.UTF8);
        ClearAll();

        // header: #,target,startx,starty,startz,hand,ttl,instruction
        int start = 0;
        if (lines.Length > 0)
        {
            var h = lines[0].TrimStart().ToLower();
            if (h.StartsWith("#") || h.StartsWith("trial")) start = 1;
        }

        int idx = 1;
        for (int i = start; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var c = line.Split(',');
            if (c.Length < 8) continue;

            var go  = Instantiate(rowPrefab, content);
            var row = go.GetComponent<SessionRow_RWR>();
            row.Init(this);
            row.SetIndex(idx++);
            rows.Add(row);

            row.targetId.text    = c[1].Trim();
            row.startX.text      = c[2].Trim().Replace(',', '.');
            row.startY.text      = c[3].Trim().Replace(',', '.');
            row.startZ.text      = c[4].Trim().Replace(',', '.');
            row.hand.text        = c[5].Trim();
            row.ttl1.text        = c[6].Trim().Replace(',', '.');
            if (row.ttl2Offset != null) row.ttl2Offset.text = c.Length > 7 ? c[7].Trim().Replace(',', '.') : "";
            row.instruction.text = c.Length > 8 ? c[8].Trim() : "";
        }

        nextIndex = rows.Count + 1;
        SelectRow(null);
        SnapshotToCache();
        SetStatus($"Loaded: {path}");
        Debug.Log($"[SessionTableRWR] Loaded CSV ← {path}");
    }

    // -------- Helpers --------

    void Renumber()
    {
        for (int i = 0; i < rows.Count; i++)
            rows[i].SetIndex(i + 1);
        nextIndex = rows.Count + 1;
    }

    void ClearAll()
    {
        foreach (var r in rows) if (r) Destroy(r.gameObject);
        rows.Clear();
        nextIndex = 1;
        selected  = null;
    }

    void SetStatus(string msg)
    {
        if (statusText) statusText.text = msg;
        Debug.Log($"[SessionTableRWR] {msg}");
    }

    void SnapshotToCache()
    {
        var store = RuntimeConfigStore.Instance;
        if (store == null) return;

        var list = new List<RuntimeConfigStore.TrialSpec>();
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            list.Add(new RuntimeConfigStore.TrialSpec
            {
                trial       = i + 1,
                targetId    = (r.targetId.text    ?? "").Trim(),
                startX      = ParseFloat(r.startX.text),
                startY      = ParseFloat(r.startY.text),
                startZ      = ParseFloat(r.startZ.text),
                ttl1        = (r.ttl1.text ?? "").Trim(),
                ttl2Offset  = (r.ttl2Offset != null ? r.ttl2Offset.text : "").Trim(),
                hand        = (r.hand.text         ?? "").Trim(),
                instruction = (r.instruction.text ?? "").Trim(),
                vf          = ""
            });
        }
        store.SetTrials(list);
    }

    void RestoreFromCache(List<RuntimeConfigStore.TrialSpec> list)
    {
        ClearAll();
        int idx = 1;
        foreach (var t in list)
        {
            var go  = Instantiate(rowPrefab, content);
            var row = go.GetComponent<SessionRow_RWR>();
            row.Init(this);
            row.SetIndex(idx++);
            rows.Add(row);

            row.targetId.text    = t.targetId;
            row.startX.text      = t.startX.ToString(CultureInfo.InvariantCulture);
            row.startY.text      = t.startY.ToString(CultureInfo.InvariantCulture);
            row.startZ.text      = t.startZ.ToString(CultureInfo.InvariantCulture);
            row.ttl1.text        = t.ttl1 ?? string.Empty;
            if (row.ttl2Offset != null) row.ttl2Offset.text = t.ttl2Offset ?? string.Empty;
            row.hand.text        = t.hand        ?? string.Empty;
            row.instruction.text = t.instruction ?? string.Empty;
        }
        nextIndex = rows.Count + 1;
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
