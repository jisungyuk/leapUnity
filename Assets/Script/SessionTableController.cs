using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Collections;                 // for IEnumerator coroutines
using System.Globalization;
using SimpleFileBrowser;                  // for runtime file dialogs

public class SessionTableController : MonoBehaviour
{
    [Header("Table Wiring")]
    [SerializeField] Transform content;        // TrialTable/Viewport/Content
    [SerializeField] GameObject rowPrefab;     // SessionRow prefab (manual TargetID + TTL1)
    [SerializeField] TMP_InputField duplicateCountInput;

    [Header("Status (optional)")]
    [SerializeField] TMP_Text statusText;

    // Internal state
    private readonly List<SessionRow> rows = new();
    private SessionRow selected;
    private SessionRow lastClicked;   
    private int nextIndex = 1;

    void Awake()
    {
        if (!content)   Debug.LogError("[SessionTableController] Content not assigned!", this);
        if (!rowPrefab) Debug.LogError("[SessionTableController] Row Prefab not assigned!", this);
    }

    void Start()
    {
        // Restore from runtime cache if available
        var store = RuntimeConfigStore.Instance;
        if (store != null && store.Trials.Count > 0)
        {
            RestoreFromCache(store.Trials);
            SetStatus("Restored session from memory");
        }
    }

    void OnDisable()
    {
        SnapshotToCache(); // save current table into memory before scene unloads
    }

    // -------- UI Hooks --------
    public void DuplicateSelected()
{
    if (rows.Count == 0)
    {
        SetStatus("No rows to duplicate.");
        return;
    }

    // 몇 번 복제할지 읽기 (빈칸/잘못된 값이면 1회로)
    int count = 1;
    if (duplicateCountInput != null)
    {
        int.TryParse(duplicateCountInput.text, out count);
        if (count <= 0) count = 1;
    }

    // 기준 row: 선택된 게 없으면 마지막 row
    var src = selected ?? rows[^1];

    // 선택된 row 바로 아래부터 끼워 넣기
    int insertIndex = rows.IndexOf(src) + 1;

    for (int k = 0; k < count; k++)
    {
        var go  = Instantiate(rowPrefab, content);
        var row = go.GetComponent<SessionRow>();
        row.Init(this);

        // 내용 복사
        row.targetId.text = src.targetId.text;
        row.Hand.text     = src.Hand.text;
        row.startX.text   = src.startX.text;
        row.startY.text   = src.startY.text;
        row.startZ.text   = src.startZ.text;
        row.ttl1.text     = src.ttl1.text;
        row.VF.text       = src.VF.text;

        // 리스트에 삽입
        rows.Insert(insertIndex + k, row);
    }

    // 인덱스 다시 번호 매기기
    Renumber();
    SnapshotToCache();
    SetStatus($"Duplicated trial x{count}");
}

    
    public void AddTrial()
    {
        var go  = Instantiate(rowPrefab, content);
        var row = go.GetComponent<SessionRow>();
        row.Init(this);
        row.SetIndex(nextIndex++);
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

// 기존에 SelectRow(SessionRow row)만 있던 자리 교체

// 필요하면 코드 다른 곳에서 그냥 이거 호출 가능
public void SelectRow(SessionRow row)
{
    SelectRow(row, false);
}

public void SelectRow(SessionRow row, bool shift)
{
    // Shift 안 눌렸으면 단일 선택
    if (!shift || lastClicked == null || !rows.Contains(lastClicked))
    {
        selected    = row;
        lastClicked = row;

        foreach (var r in rows)
            r.SetSelected(r == row);

        return;
    }

    // Shift 눌림 → 범위 선택
    int i1 = rows.IndexOf(lastClicked);
    int i2 = rows.IndexOf(row);
    if (i1 > i2) (i1, i2) = (i2, i1);

    for (int i = 0; i < rows.Count; i++)
    {
        bool inRange = (i >= i1 && i <= i2);
        rows[i].SetSelected(inRange);
    }

    selected    = row;
    lastClicked = row;
}



    public void SaveCsv()
{
    if (rows.Count == 0)
    {
        SetStatus("No trials to save.");
        return;
    }

    if (DataPathManager.Instance == null ||
        string.IsNullOrEmpty(DataPathManager.Instance.ParticipantFolder))
    {
        SetStatus("No participant folder set. (Main Menu → Choose folder)");
        return;
    }

    var sb = new StringBuilder();

    // *** GameSessionController가 요구하는 8개 컬럼 ***
    sb.AppendLine("#,target,hand,startx,starty,startz,ttl,vf");

    for (int i = 0; i < rows.Count; i++)
    {
        var r = rows[i];

        string trial   = (i + 1).ToString();
        string target  = (r.targetId.text ?? "").Trim();
        string hand    = (r.Hand.text ?? "").Trim().Replace(',', '.');
        string sx      = (r.startX.text ?? "").Trim().Replace(',', '.');
        string sy      = (r.startY.text ?? "").Trim().Replace(',', '.');
        string sz      = (r.startZ.text ?? "").Trim().Replace(',', '.');
        string ttl     = (r.ttl1.text ?? "").Trim().Replace(',', '.');
        string vf      = (r.VF.text ?? "").Trim().Replace(',', '.');

        sb.AppendLine($"{trial},{target},{hand},{sx},{sy},{sz},{ttl},{vf}");
    }

    string file = $"session_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv";
    string path = DataPathManager.Instance.PathInParticipantFolder(file);
    Directory.CreateDirectory(Path.GetDirectoryName(path));
    File.WriteAllText(path, sb.ToString(), Encoding.UTF8);

    SnapshotToCache();
    SetStatus($"Saved: {path}");
    Debug.Log($"[SessionTable] Saved CSV → {path}");
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
        FileBrowser.PickMode.Files, false, initial, "Load session CSV", "Load");

    if (!FileBrowser.Success) { SetStatus("Load canceled."); yield break; }

    string path = FileBrowser.Result[0];
    if (!File.Exists(path)) { SetStatus("File not found."); yield break; }

    var lines = File.ReadAllLines(path, Encoding.UTF8);
    ClearAll();

    // 헤더: "#,target,hand,startx,starty,startz,ttl,vf"
    int start = 0;
    if (lines.Length > 0)
    {
        var h = lines[0].TrimStart().ToLower();
        if (h.StartsWith("#") || h.StartsWith("trial"))
            start = 1;   // 헤더는 건너뛰기
    }

    int idx = 1;

    for (int i = start; i < lines.Length; i++)
    {
        var line = lines[i].Trim();
        if (string.IsNullOrEmpty(line)) continue;

        var c = line.Split(',');
        // #,target,hand,startx,starty,startz,ttl,vf  → 8개
        if (c.Length < 8) continue;

        var go  = Instantiate(rowPrefab, content);
        var row = go.GetComponent<SessionRow>();
        row.Init(this);
        row.SetIndex(idx++);
        rows.Add(row);

        // c[0] = trial 번호는 그냥 버리고, 우리 idx 사용
        row.targetId.text = c[1].Trim();                        // target
        row.Hand.text     = c[2].Trim();                        // hand
        row.startX.text   = c[3].Trim().Replace(',', '.');      // startx
        row.startY.text   = c[4].Trim().Replace(',', '.');      // starty
        row.startZ.text   = c[5].Trim().Replace(',', '.');      // startz
        row.ttl1.text     = c[6].Trim().Replace(',', '.');      // ttl
        row.VF.text       = c[7].Trim().Replace(',', '.');      // vf
    }

    nextIndex = rows.Count + 1;
    SelectRow(null);

    SnapshotToCache();
    SetStatus($"Loaded: {path}");
    Debug.Log($"[SessionTable] Loaded CSV ← {path}");
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
        selected = null;
    }

    void SetStatus(string msg)
    {
        if (statusText) statusText.text = msg;
        Debug.Log($"[SessionTable] {msg}");
    }

    void SnapshotToCache()
    {
        var store = RuntimeConfigStore.Instance;
        if (store == null) return;

        var list = new List<RuntimeConfigStore.TrialSpec>();
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            var spec = new RuntimeConfigStore.TrialSpec
            {
                trial   = i + 1,
                targetId= (r.targetId.text ?? "").Trim(),
                startX  = ParseFloat(r.startX.text),
                startY  = ParseFloat(r.startY.text),
                startZ  = ParseFloat(r.startZ.text),
                ttl1    = (r.ttl1.text ?? "").Trim(),
                hand    = (r.Hand.text ?? "").Trim(),
                vf      = (r.VF.text ?? "").Trim()
            };
            list.Add(spec);
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
            var row = go.GetComponent<SessionRow>();
            row.Init(this);
            row.SetIndex(idx++);
            rows.Add(row);

            row.targetId.text = t.targetId;
            row.startX.text   = t.startX.ToString(CultureInfo.InvariantCulture);
            row.startY.text   = t.startY.ToString(CultureInfo.InvariantCulture);
            row.startZ.text   = t.startZ.ToString(CultureInfo.InvariantCulture);
            row.ttl1.text     = t.ttl1.ToString(CultureInfo.InvariantCulture);
            row.Hand.text     = t.hand ?? string.Empty;
            row.VF.text       = t.vf ?? string.Empty;
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
