using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PlanetChunky))]
public class PlanetInspector : Editor
{
    PlanetChunky planet;

    SerializedProperty _meshSettings;
    SerializedProperty _terrainSettings;
    SerializedProperty _postProcessSettings;

    private void OnEnable()
    {
        planet = target as PlanetChunky;
        _meshSettings = serializedObject.FindProperty("meshSettings");
        _terrainSettings = serializedObject.FindProperty("terrainSettings");
        _postProcessSettings = serializedObject.FindProperty("postProcessSettings");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        bool chunksChanged = false;
        bool terrainChanged = false;
        bool meshChanged = false;
        bool postProcessChanged = false;

        EditorGUI.BeginChangeCheck();
        planet.meshSettings.chunkRecursionLevel = EditorGUILayout.IntField("Chunk Recursion Level", planet.meshSettings.chunkRecursionLevel);
        chunksChanged = EditorGUI.EndChangeCheck();

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(_meshSettings, true);
        meshChanged = EditorGUI.EndChangeCheck();

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(_terrainSettings, true);
        terrainChanged = EditorGUI.EndChangeCheck();

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(_postProcessSettings, true);
        postProcessChanged = EditorGUI.EndChangeCheck();

        serializedObject.ApplyModifiedProperties();

        if(GUILayout.Button("Test"))
        {
            Chunk.GenerateChunks(4);
        }

        if(chunksChanged)
        {
            planet.GetTerrain().InitializeChunks(planet.meshSettings.chunkRecursionLevel);
        }

        if (terrainChanged)
        {
            planet.GetTerrain().BindBuffers(planet.terrainSettings);
        }

        if (meshChanged || terrainChanged || chunksChanged)
        {
            planet.UpdateMesh(terrainChanged);
        }

    }
}
