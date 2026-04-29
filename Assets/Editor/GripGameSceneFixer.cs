using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Menu: Tools/Fix GRIP Game Scene
/// Replaces RWR components with GRIP equivalents, copying all matching
/// serialized fields by name. Run once while GRIP_Game.unity is open.
/// </summary>
public static class GripGameSceneFixer
{
    [MenuItem("Tools/Fix GRIP Game Scene")]
    public static void Fix()
    {
        int replaced = 0;

        // ── 1. TrialGameController_RWR → GRIP ───────────────────────
        var rwrTrials = Object.FindObjectsOfType<TrialGameController_RWR>(true);
        TrialGameController_GRIP gripTrial = null;

        foreach (var rwr in rwrTrials)
        {
            var go = rwr.gameObject;

            gripTrial = go.GetComponent<TrialGameController_GRIP>();
            if (gripTrial == null)
                gripTrial = Undo.AddComponent<TrialGameController_GRIP>(go);

            CopyMatchingSerializedFields(rwr, gripTrial);
            Undo.DestroyObjectImmediate(rwr);
            replaced++;
            Debug.Log($"[GripGameFixer] Replaced TrialGameController_RWR → GRIP on '{go.name}'");
        }

        // Fallback: if GRIP component already existed without RWR
        if (gripTrial == null)
            gripTrial = Object.FindObjectOfType<TrialGameController_GRIP>(true);

        // ── 2. GameSessionController_RWR → GRIP ─────────────────────
        var rwrSessions = Object.FindObjectsOfType<GameSessionController_RWR>(true);

        foreach (var rwr in rwrSessions)
        {
            var go = rwr.gameObject;

            var gripSession = go.GetComponent<GameSessionController_GRIP>();
            if (gripSession == null)
                gripSession = Undo.AddComponent<GameSessionController_GRIP>(go);

            CopyMatchingSerializedFields(rwr, gripSession);

            // Fix trialController reference — type changed from _RWR to _GRIP
            if (gripTrial != null)
            {
                var so   = new SerializedObject(gripSession);
                var prop = so.FindProperty("trialController");
                if (prop != null)
                {
                    prop.objectReferenceValue = gripTrial;
                    so.ApplyModifiedProperties();
                    Debug.Log($"[GripGameFixer] trialController → {gripTrial.gameObject.name}");
                }
            }

            Undo.DestroyObjectImmediate(rwr);
            replaced++;
            Debug.Log($"[GripGameFixer] Replaced GameSessionController_RWR → GRIP on '{go.name}'");
        }

        // ── 3. Mark scene dirty ──────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        string msg = replaced > 0
            ? $"교체 완료: {replaced}개 컴포넌트. 씬을 저장하세요 (Ctrl+S)."
            : "교체할 RWR 컴포넌트를 찾지 못했습니다.\nGRIP_Game 씬이 열려 있는지 확인하세요.";

        Debug.Log($"[GripGameFixer] {msg}");
        EditorUtility.DisplayDialog("GRIP Game Scene Fixer", msg, "OK");
    }

    /// <summary>
    /// Copies serialized properties from src to dst where property paths
    /// and types match. Skips m_Script (different classes).
    /// Object references that are type-incompatible are silently skipped;
    /// the trialController cross-reference is fixed separately above.
    /// </summary>
    static void CopyMatchingSerializedFields(Component src, Component dst)
    {
        var srcSO = new SerializedObject(src);
        var dstSO = new SerializedObject(dst);

        var iter = srcSO.GetIterator();
        bool enterChildren = true;
        while (iter.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (iter.propertyPath == "m_Script") continue;

            var dstProp = dstSO.FindProperty(iter.propertyPath);
            if (dstProp == null) continue;
            if (dstProp.propertyType != iter.propertyType) continue;

            // For object references, skip if types are incompatible
            // (avoids "can't assign RWR type to GRIP field" errors)
            if (iter.propertyType == SerializedPropertyType.ObjectReference)
            {
                var srcObj = iter.objectReferenceValue;
                if (srcObj == null) { dstSO.CopyFromSerializedProperty(iter); continue; }

                // Check assignability via the serialized field's actual type
                if (dstProp.objectReferenceValue == null)
                {
                    // Try to assign — Unity will silently drop incompatible types
                    dstProp.objectReferenceValue = srcObj;
                }
                else
                {
                    dstProp.objectReferenceValue = srcObj;
                }
                continue;
            }

            dstSO.CopyFromSerializedProperty(iter);
        }

        dstSO.ApplyModifiedProperties();
    }
}
