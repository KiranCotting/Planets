using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[System.Serializable]
public struct MeshSettings
{
    public int chunkRecursionLevel;
	public AnimationCurve detailCurve;
	public AnimationCurve falloffCurve;
	public float maxDetailSqrDistance;
	public float minDetailSqrDistance;
	public float meshColliderCutoff;
}

[CustomPropertyDrawer(typeof(MeshSettingsDrawer))]
public class MeshSettingsDrawer : PropertyDrawer {
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        EditorGUILayout.CurveField("Detail Curve", property.animationCurveValue);
        property.Next(false);
        EditorGUILayout.CurveField("Falloff Curve", property.animationCurveValue);
        property.Next(false);
        EditorGUILayout.FloatField("Max Detail Squared Distance", property.floatValue);
        property.Next(false);
        EditorGUILayout.FloatField("Min Detail Squared Distance", property.floatValue);
        property.Next(false);
        EditorGUILayout.FloatField("Mesh Collider Cutoff", property.floatValue);
        EditorGUI.EndProperty();
    }
}
