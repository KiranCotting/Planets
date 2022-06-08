using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

struct Crater
{
    public float radius;
    public Vector3 position;

    public Crater(float radius, Vector3 position)
    {
        this.radius = radius;
        this.position = position;
    }
}

public class Terrain
{
    private int maxChunkLOD;

    public ComputeShader cs;

    private Chunk[] chunks;
    private ChunkBounds[] chunkBounds;

    private List<Chunk>[] chunkLODBuckets;

    private List<ChunkBounds>[] chunkBoundsLODBuckets;

    private ComputeBuffer chunksBuffer;
    private ComputeBuffer[] verticesLODBuffers;
    private ComputeBuffer outputVerticesBuffer;
    private ComputeBuffer colorDataBuffer;
    private ComputeBuffer noiseSettingsBuffer;
    private ComputeBuffer craterBuffer;

    private int[] LODKernelIDs;
    private int[] verticesLODBufferIDs;

    private static readonly int chunksBufferID = Shader.PropertyToID("chunks");
    private static readonly int outputVerticesBufferID = Shader.PropertyToID("outputVertices");
    private static readonly int noiseSettingsBufferID = Shader.PropertyToID("noiseSettings");
    private static readonly int cratersBufferID = Shader.PropertyToID("craters");
    private static readonly int colorDataBufferID = Shader.PropertyToID("colorData");
    private static readonly int numCratersID = Shader.PropertyToID("numCraters");
    private static readonly int rimSteepnessID = Shader.PropertyToID("rimSteepness");
    private static readonly int rimWidthID = Shader.PropertyToID("rimWidth");
    private static readonly int floorHeightID = Shader.PropertyToID("floorHeight");
    private static readonly int craterSmoothnessID = Shader.PropertyToID("craterSmoothness");
    private static readonly int oceanFloorID = Shader.PropertyToID("oceanFloor");
    private static readonly int color1ID = Shader.PropertyToID("color1");
    private static readonly int color2ID = Shader.PropertyToID("color2");
    private static readonly int blendWidthID = Shader.PropertyToID("blendWidth");
    private static readonly int blendHeightID = Shader.PropertyToID("blendHeight");

    private Mesh finalMesh;
    private Mesh physicsMesh;
    private bool chunksInitialized = false;
    private bool buffersBound = false;

    public Terrain(int maxChunkLOD, ComputeShader cs)
    {
        this.cs = cs;
        this.maxChunkLOD = maxChunkLOD;
        Chunk.GeneratePlaneMeshes(maxChunkLOD);
        chunkLODBuckets = new List<Chunk>[maxChunkLOD];
        chunkBoundsLODBuckets = new List<ChunkBounds>[maxChunkLOD];
        verticesLODBuffers = new ComputeBuffer[maxChunkLOD];

        LODKernelIDs = new int[maxChunkLOD];
        verticesLODBufferIDs = new int[maxChunkLOD];

        for (int i = 0; i < maxChunkLOD; i++)
        {
            verticesLODBufferIDs[i] = Shader.PropertyToID("verticesLOD" + i);
            verticesLODBuffers[i] = new ComputeBuffer(Chunk.planeMeshes[i].vertexCount, 12);
            verticesLODBuffers[i].SetData(Chunk.planeMeshes[i].vertices);
            LODKernelIDs[i] = cs.FindKernel("ChunkLOD" + i);
            cs.SetBuffer(LODKernelIDs[i], verticesLODBufferIDs[i], verticesLODBuffers[i]);
        }
    }

    public Mesh Mesh(Vector3 viewPos, MeshSettings meshSettings, TerrainSettings terrainSettings, bool forceRebuild)
    {
        if(!chunksInitialized)
        {
            InitializeChunks(meshSettings.chunkRecursionLevel);
            chunksInitialized = true;
        }

        if(!buffersBound)
        {
            BindBuffers(terrainSettings);
            buffersBound = true;
        }

        UpdateMesh(viewPos, meshSettings, forceRebuild);

        return finalMesh;
    }

    public Mesh PhysicsMesh(Vector3 viewPos, MeshSettings meshSettings)
    {
        List<CombineInstance> physicsCIs = new List<CombineInstance>();

        for (int i = 0; i < chunks.Length; i++)
        {
            Chunk c = chunks[i];
            float dot = Vector3.Dot(viewPos.normalized, c.center.normalized);

            if(dot > meshSettings.meshColliderCutoff)
            {
                CombineInstance ci = new CombineInstance();
                ci.mesh = c.mesh;
                ci.transform = Matrix4x4.identity;
                physicsCIs.Add(ci);
            }
        }

        physicsMesh.Clear();
        physicsMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        physicsMesh.CombineMeshes(physicsCIs.ToArray());
        physicsMesh.RecalculateNormals();

        return physicsMesh;
    }

    public void InitializeChunks(int chunkRecursionLevel)
    {
        chunks = Chunk.GenerateChunks(chunkRecursionLevel);
        chunkBounds = new ChunkBounds[chunks.Length];
        for (int i = 0; i < chunks.Length; i++)
        {
            chunkBounds[i] = chunks[i].bounds;
        }
    }

    public void UpdateMesh(Vector3 viewPos, MeshSettings meshSettings, bool forceRebuild)
    {
        int totalUpdatedChunks = UpdateChunkMeshes(viewPos, meshSettings, forceRebuild);

        if(totalUpdatedChunks == 0)
        {
            return;
        }

        CombineInstance[] ci = new CombineInstance[chunks.Length];

        for (int i = 0; i < chunks.Length; i++)
        {
            Chunk c = chunks[i];
            for (int j = 0; j < 4; j++)
            {
                Chunk otherChunk = chunks[c.borders[j].otherChunk];

                if (c.meshUpdatePending ^ otherChunk.meshUpdatePending)
                {
                    if(c.currentLOD <= otherChunk.currentLOD)
                    {
                        FixChunkBorder(c, otherChunk, (Side)j, c.borders[j].otherSide);
                    } else
                    {
                        FixChunkBorder(otherChunk, c, c.borders[j].otherSide, (Side)j);
                    }
                }
            }

            ci[i].mesh = c.mesh;
            ci[i].transform = Matrix4x4.identity;
            c.meshUpdatePending = false;
        }

        if (finalMesh == null)
        {
            finalMesh = new Mesh();
        }

        if (physicsMesh == null)
        {
            physicsMesh = new Mesh();
        }

        finalMesh.Clear();
        finalMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        finalMesh.CombineMeshes(ci);
        finalMesh.RecalculateNormals();
    }

    private int UpdateChunkMeshes(Vector3 viewPos, MeshSettings meshSettings, bool forceRebuild)
    {
        int chunkUpdateTotal = 0;
        SortChunksByLOD(viewPos, meshSettings, forceRebuild);

        for (int chunkDetail = maxChunkLOD - 1; chunkDetail >= 0; chunkDetail--)
        {
            int lodChunkCount = chunkLODBuckets[chunkDetail].Count;

            chunkUpdateTotal += lodChunkCount;

            if (lodChunkCount == 0)
            {
                continue;
            }

            chunksBuffer = new ComputeBuffer(lodChunkCount, 48);
            chunksBuffer.SetData(chunkBoundsLODBuckets[chunkDetail]);
            cs.SetBuffer(LODKernelIDs[chunkDetail], chunksBufferID, chunksBuffer);

            int outputVerticesCount = lodChunkCount * verticesLODBuffers[chunkDetail].count;

            outputVerticesBuffer = new ComputeBuffer(outputVerticesCount, 12);
            cs.SetBuffer(LODKernelIDs[chunkDetail], outputVerticesBufferID, outputVerticesBuffer);

            colorDataBuffer = new ComputeBuffer(outputVerticesCount, 16);
            cs.SetBuffer(LODKernelIDs[chunkDetail], colorDataBufferID, colorDataBuffer);

            cs.Dispatch(LODKernelIDs[chunkDetail], lodChunkCount, 1, 1);

            int numMeshVertices = verticesLODBuffers[chunkDetail].count;

            for (int i = 0; i < lodChunkCount; i++)
            {
                Chunk c = chunkLODBuckets[chunkDetail][i];
                Vector3[] meshVertices = new Vector3[numMeshVertices];
                Color[] meshColors = new Color[numMeshVertices];

                outputVerticesBuffer.GetData(meshVertices, 0, i * numMeshVertices, numMeshVertices);
                colorDataBuffer.GetData(meshColors, 0, i * numMeshVertices, numMeshVertices);

                c.mesh.Clear();
                c.mesh.vertices = meshVertices;
                c.mesh.triangles = Chunk.planeMeshes[chunkDetail].triangles;
                c.mesh.colors = meshColors;
                c.currentLOD = chunkDetail;
                c.meshUpdatePending = true;
            }

            chunksBuffer.Release();
            outputVerticesBuffer.Release();
            colorDataBuffer.Release();
        }

        return chunkUpdateTotal;
    }

    private void SortChunksByLOD(Vector3 viewPos, MeshSettings meshSettings, bool forceRebuild)
    {
        for (int i = 0; i < maxChunkLOD; i++)
        {
            chunkLODBuckets[i] = new List<Chunk>();
            chunkBoundsLODBuckets[i] = new List<ChunkBounds>();
        }
        for (int i = 0; i < chunks.Length; i++)
        {
            Chunk c = chunks[i];
            int chunkLOD = ChunkLOD(c, viewPos, meshSettings, forceRebuild);
            if (chunkLOD != -1)
            {
                chunkLODBuckets[chunkLOD].Add(c);
                chunkBoundsLODBuckets[chunkLOD].Add(c.bounds);
            }
        }
    }

    private int ChunkLOD(Chunk c, Vector3 viewPos, MeshSettings meshSettings, bool forceRebuild)
    {
        float dot = Vector3.Dot(viewPos.normalized, c.center.normalized);
        int desiredLOD = Mathf.FloorToInt(Mathf.Clamp01(meshSettings.detailCurve.Evaluate(dot)) * (maxChunkLOD - 1));

        if (desiredLOD >= maxChunkLOD || desiredLOD < 0)
        {
            return -1;
        }

        if(forceRebuild)
        {
            return desiredLOD;
        }

        if (c.currentLOD != desiredLOD)
        {
            return desiredLOD;
        }
        else
        {
            return -1;
        }
    }

    private void FixChunkBorder(Chunk c1, Chunk c2, Side c1Side, Side c2Side)
    {
        Vector3[] c1Vertices = c1.mesh.vertices;
        Vector3[] c2Vertices = c2.mesh.vertices;

        int[] c1BorderIndices = Chunk.planeMeshBorders[c1.currentLOD].GetSide(c1Side);
        int[] c2BorderIndicesTemp = Chunk.planeMeshBorders[c2.currentLOD].GetSide(c2Side);
        int[] c2BorderIndices = new int[c2BorderIndicesTemp.Length];
        c2BorderIndicesTemp.CopyTo(c2BorderIndices, 0);
        Array.Reverse(c2BorderIndices);

        if (c1.currentLOD < c2.currentLOD)
        {
            int ratio = (c1BorderIndices.Length - 1) / (c2BorderIndices.Length - 1);

            for (int i = 0; i < c1BorderIndices.Length; i++) 
            {
                if(i % ratio != 0)
                {
                    int otherVertexIndex1 = i / ratio;
                    int otherVertexIndex2 = otherVertexIndex1 + 1;
                    Vector3 otherVertex1 = c2Vertices[c2BorderIndices[otherVertexIndex1]];
                    Vector3 otherVertex2 = c2Vertices[c2BorderIndices[otherVertexIndex2]];
                    float t = i % ratio / (float)ratio;
                    c1Vertices[c1BorderIndices[i]] = Vector3.Lerp(otherVertex1, otherVertex2, t);
                }
            }

            c1.mesh.vertices = c1Vertices;
        }

        if(c1.currentLOD == c2.currentLOD)
        {
            for (int i = 0; i < c1BorderIndices.Length; i++)
            {
                c1Vertices[c1BorderIndices[i]] = c2Vertices[c2BorderIndices[i]];
            }

            c1.mesh.vertices = c1Vertices;
        }
    }

    public void BindBuffers(TerrainSettings terrainSettings)
    {
        UnityEngine.Random.State prevRNGState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(terrainSettings.randomSeed);

        if (noiseSettingsBuffer != null)
        {
            noiseSettingsBuffer.Release();
        }

        if (craterBuffer != null)
        {
            craterBuffer.Release();
        }

        noiseSettingsBuffer = new ComputeBuffer(5, 36);

        noiseSettingsBuffer.SetData(new LayeredNoiseSettings[] {
            terrainSettings.shapeSettings,
            terrainSettings.detailSettings,
            terrainSettings.colorSettings,
            terrainSettings.ridgeSettings,
            terrainSettings.oceanSettings
        });

        Crater[] craters;
        if (terrainSettings.numCraters != 0)
        {
            craterBuffer = new ComputeBuffer(terrainSettings.numCraters, 16);
            craters = new Crater[terrainSettings.numCraters];
        }
        else // make default undrawn crater because compute buffers can't be empty
        {
            craterBuffer = new ComputeBuffer(1, 16);
            craters = new Crater[] { new Crater(0, Vector3.zero) };
        }

        for (int i = 0; i < terrainSettings.numCraters; i++)
        {
            craters[i] = new Crater(terrainSettings.craterSizeCurve.Evaluate(UnityEngine.Random.Range(terrainSettings.minRadius, terrainSettings.maxRadius)), UnityEngine.Random.insideUnitSphere);
        }
        craterBuffer.SetData(craters);

        for(int i = 0; i < LODKernelIDs.Length; i++)
        {
            cs.SetBuffer(LODKernelIDs[i], noiseSettingsBufferID, noiseSettingsBuffer);
            cs.SetBuffer(LODKernelIDs[i], cratersBufferID, craterBuffer);
            cs.SetInt(numCratersID, terrainSettings.numCraters);
            cs.SetFloat(rimSteepnessID, terrainSettings.rimSteepness);
            cs.SetFloat(rimWidthID, terrainSettings.rimWidth);
            cs.SetFloat(floorHeightID, terrainSettings.floorHeight);
            cs.SetFloat(craterSmoothnessID, terrainSettings.craterSmoothness);
            cs.SetFloat(oceanFloorID, terrainSettings.oceanFloor);
            cs.SetFloats(color1ID, new float[] { terrainSettings.color1.r, terrainSettings.color1.g, terrainSettings.color1.b, terrainSettings.color1.a });
            cs.SetFloats(color2ID, new float[] { terrainSettings.color2.r, terrainSettings.color2.g, terrainSettings.color2.b, terrainSettings.color2.a });
            cs.SetFloat(blendWidthID, terrainSettings.blendWidth);
            cs.SetFloat(blendHeightID, terrainSettings.blendHeight);
        }
    }

    void ReleaseBuffers()
    {
        noiseSettingsBuffer.Release();
        craterBuffer.Release();
        noiseSettingsBuffer = null;
        craterBuffer = null;
    }

    ~Terrain()
    {
        ReleaseBuffers();
        for (int i = 0; i < maxChunkLOD; i++)
        {
            verticesLODBuffers[i].Release();
        }
    }
}
