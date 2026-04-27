using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(GameSessionController_RWR.ExperimentTtlEntry))]
public class ExperimentTtlEntryDrawer : PropertyDrawer
{
    const float Pad = 2f;

    static readonly GUIContent[] instrOptions =
    {
        new GUIContent("REST"),
        new GUIContent("REACH"),
        new GUIContent("REACH+GRASP"),
    };
    static readonly int[] instrValues = { 0, 1, 2 };

    static readonly GUIContent[] handOptions =
    {
        new GUIContent("Left"),
        new GUIContent("Right"),
        new GUIContent("Either"),
    };
    static readonly int[] handValues = { 0, 1, 2 };

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return (EditorGUIUtility.singleLineHeight + Pad) * 7;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var ttlEnabledProp = property.FindPropertyRelative("ttlEnabled");
        var ttlOffsetProp  = property.FindPropertyRelative("ttlOffsetMs");
        var ttl2OffsetProp = property.FindPropertyRelative("ttl2OffsetMs");
        var instrProp      = property.FindPropertyRelative("instruction");
        var handProp       = property.FindPropertyRelative("handMode");
        var angleProp      = property.FindPropertyRelative("angleDeg");
        var distProp       = property.FindPropertyRelative("distanceCm");

        float h    = EditorGUIUtility.singleLineHeight;
        float step = h + Pad;
        Rect  row  = new Rect(position.x, position.y, position.width, h);

        // "No Pulse" is the inverse of ttlEnabled
        bool noPulse = !ttlEnabledProp.boolValue;
        EditorGUI.BeginChangeCheck();
        bool newNoPulse = EditorGUI.Toggle(row, "No Pulse", noPulse);
        if (EditorGUI.EndChangeCheck())
            ttlEnabledProp.boolValue = !newNoPulse;

        // TS / CS fields — grayed out when No Pulse is checked
        using (new EditorGUI.DisabledScope(newNoPulse))
        {
            row.y += step;
            EditorGUI.PropertyField(row, ttlOffsetProp,
                new GUIContent("TS (Output2)",
                               "Testing Stimulus: ms from Go cue. 0 = fires at Go cue."));

            row.y += step;
            EditorGUI.PropertyField(row, ttl2OffsetProp,
                new GUIContent("CS delay (-) (Output1)",
                               "Conditioning Stimulus: ms from Testing Stimulus. Must be 0 (SinglePulse) or negative (fires before Testing)."));
        }

        row.y += step;
        instrProp.intValue = EditorGUI.IntPopup(row,
            new GUIContent("Instruction"), instrProp.intValue, instrOptions, instrValues);

        row.y += step;
        handProp.intValue = EditorGUI.IntPopup(row,
            new GUIContent("Hand"), handProp.intValue, handOptions, handValues);

        row.y += step;
        EditorGUI.PropertyField(row, angleProp,
            new GUIContent("Angle (°)", "Target angle from home position. 0=right, 90=forward."));

        row.y += step;
        EditorGUI.PropertyField(row, distProp,
            new GUIContent("Distance (cm)", "Target distance from home position in centimetres."));

        EditorGUI.EndProperty();
    }
}
