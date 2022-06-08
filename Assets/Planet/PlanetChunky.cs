using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[ExecuteAlways]
public class PlanetChunky : MonoBehaviour
{
    public MeshFilter meshFilter;

    public ComputeShader cs;

    private LODGroup lodGroup;

    public MeshSettings meshSettings = new MeshSettings(); 

    public TerrainSettings terrainSettings = new TerrainSettings();

    public PostProcessSettings postProcessSettings = new PostProcessSettings();

    private Terrain terrain;

    void Update()
    {
        UpdateMesh();
    }

    public void UpdateMesh(bool forceRebuild = false)
    {
        if(!lodGroup)
        {
            lodGroup = GetComponent<LODGroup>();
        }

        Camera cam = null;

        if (Application.isEditor)
        {
            cam = SceneView.lastActiveSceneView.camera;
        }
        
        if(Application.isPlaying)
        {
            cam = Camera.main;
        }

        int lod = LODExtendedUtility.GetVisibleLOD(lodGroup, cam);

        // LOD is at maximum, should be rendering chunks
        if (lod == 0)
        {
            meshFilter.sharedMesh = GetTerrain().Mesh(cam.transform.position, meshSettings, terrainSettings, forceRebuild);
            GetComponent<MeshCollider>().sharedMesh = GetTerrain().PhysicsMesh(cam.transform.position, meshSettings);
        }
    }

    public Terrain GetTerrain()
    {
        if(terrain == null)
        {
            terrain = new Terrain(4, cs);
        }
        return terrain;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        // Ensure continuous Update calls.
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            UnityEditor.SceneView.RepaintAll();
        }
    }
#endif
}
