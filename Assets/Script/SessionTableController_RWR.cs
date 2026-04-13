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

    // CSV column header
    // #,hand,target,start_r,hold,wait,move,ts,cs,inst
    const string CSV_HEADER = "#,hand,target,start_r,hold,wait,move,ts,cs,inst";

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

        // Default values
        row.hand.text          = "1";
        row.targetId.text      = "1";
        row.startRadiusCm.text = "15";
        row.holdDuration.text  = "0.5";
        row.waitForGo.text     = "3";
        row.executing.text     = "3";
        row.ts.text            = "";
        row.cs.text            = "";
        row.instruction.text   = "1";

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

            row.hand.text         = src.hand.text;
            row.targetId.text     = src.targetId.text;
            row.startRadiusCm.text = src.startRadiusCm.text;
            row.holdDuration.text  = src.holdDuration.text;
            row.waitForGo.text     = src.waitForGo.text;
            row.executing.text     = src.executing.text;
            row.ts.text            = src.ts.text;
            row.cs.text            = src.cs.text;
            row.instruction.text   = src.instruction.text;

            rows.Insert(insertIndex + k, row);
            go.transform.SetSiblingIndex(insertIndex + k);  // keep visual order in sync with list order
        }

        Renumber();
        SnapshotToCache();
        SetStatus($"Duplicated trial x{count}");
    }

    public void RandomizeTrials()
    {
        if (rows.Count < 2) { SetStatus("Need at least 2 trials to randomize."); return; }

        // Fisher-Yates shuffle
        var rng = new System.Random();
        for (int i = rows.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (rows[i], rows[j]) = (rows[j], rows[i]);
        }

        // Sync GameObject sibling order to match shuffled list
        for (int i = 0; i < rows.Count; i++)
            rows[i].transform.SetSiblingIndex(i);

        Renumber();
        SnapshotToCache();
        SetStatus("Trials randomized.");
    }

    public void ResetAll()
    {
        ClearAll();
        SnapshotToCache();
        SetStatus("All trials cleared.");
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
        sb.AppendLine(CSV_HEADER);

        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            sb.AppendLine(RowToCsv(i + 1, r));
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
            if (c.Length < 10) continue;

            var go  = Instantiate(rowPrefab, content);
            var row = go.GetComponent<SessionRow_RWR>();
            row.Init(this);
            row.SetIndex(idx++);
            rows.Add(row);

            // columns: #, hand, target, start_r, hold, wait, move, ts, cs, inst
            row.hand.text          = c[1].Trim();
            row.targetId.text      = c[2].Trim();
            row.startRadiusCm.text = c[3].Trim().Replace(',', '.');
            row.holdDuration.text  = c[4].Trim().Replace(',', '.');
            row.waitForGo.text     = c[5].Trim().Replace(',', '.');
            row.executing.text     = c[6].Trim().Replace(',', '.');
            row.ts.text            = c[7].Trim().Replace(',', '.');
            row.cs.text            = c[8].Trim().Replace(',', '.');
            row.instruction.text   = c[9].Trim();
        }

        nextIndex = rows.Count + 1;
        SelectRow(null);
        SnapshotToCache();
        SetStatus($"Loaded: {path}");
        Debug.Log($"[SessionTableRWR] Loaded CSV ← {path}");
    }

    // -------- Helpers --------

    string RowToCsv(int trialNum, SessionRow_RWR r)
    {
        string trial   = trialNum.ToString();
        string hand    = (r.hand.text          ?? "").Trim();
        string target  = (r.targetId.text      ?? "").Trim();
        string startR  = (r.startRadiusCm.text ?? "").Trim().Replace(',', '.');
        string hold    = (r.holdDuration.text  ?? "").Trim().Replace(',', '.');
        string wait    = (r.waitForGo.text     ?? "").Trim().Replace(',', '.');
        string move    = (r.executing.text     ?? "").Trim().Replace(',', '.');
        string ts      = (r.ts.text            ?? "").Trim().Replace(',', '.');
        string cs      = (r.cs.text            ?? "").Trim().Replace(',', '.');
        string inst    = (r.instruction.text   ?? "").Trim();

        return $"{trial},{hand},{target},{startR},{hold},{wait},{move},{ts},{cs},{inst}";
    }

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
                trial         = i + 1,
                hand          = (r.hand.text          ?? "").Trim(),
                targetId      = (r.targetId.text      ?? "").Trim(),
                startRadiusCm = (r.startRadiusCm.text ?? "").Trim(),
                holdDuration  = (r.holdDuration.text  ?? "").Trim(),
                waitForGo     = (r.waitForGo.text     ?? "").Trim(),
                executing     = (r.executing.text     ?? "").Trim(),
                ts            = (r.ts.text            ?? "").Trim(),
                cs            = (r.cs.text            ?? "").Trim(),
                instruction   = (r.instruction.text   ?? "").Trim(),
                // Legacy fields unused in RWR
                startX = 0, startY = 0, startZ = 0,
                ttl1 = "", ttl2Offset = "", vf = ""
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

            row.hand.text          = t.hand          ?? "";
            row.targetId.text      = t.targetId      ?? "";
            row.startRadiusCm.text = t.startRadiusCm ?? "";
            row.holdDuration.text  = t.holdDuration  ?? "";
            row.waitForGo.text     = t.waitForGo     ?? "";
            row.executing.text     = t.executing     ?? "";
            row.ts.text            = t.ts            ?? "";
            row.cs.text            = t.cs            ?? "";
            row.instruction.text   = t.instruction   ?? "";
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
