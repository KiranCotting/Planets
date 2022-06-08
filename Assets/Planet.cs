using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

[ExecuteInEditMode]
public class Planet : MonoBehaviour
{
    struct Crater {
		public float radius;
		public Vector3 position;

		public Crater(float radius, Vector3 position)
        {
			this.radius = radius;
			this.position = position;
        }
	}

	[System.Serializable]
	public struct NoiseSettings
	{
		public int layers;
		public float scale;
		public float elevation;
		public float verticalShift;
		public float lacunarity;
		public float gain;
		public Vector3 offset;

		public NoiseSettings(int layers, float scale, float elevation, float verticalShift, float lacunarity, float gain, Vector3 offset)
        {
			this.layers = layers;
			this.scale = scale;
			this.elevation = elevation;
			this.verticalShift = verticalShift;
			this.lacunarity = lacunarity;
			this.gain = gain;
			this.offset = offset;
        }
	};

	public int randomSeed;

	[Header("Mesh Settings")]
	public AnimationCurve detailCurve;
	public AnimationCurve falloffCurve;
	public int maxDetail;
	public int minDetail;
	public float maxDetailDistance;
	public float minDetailDistance;
	public int meshUpdateIntervalMillis = 10;
	public float meshColliderCutoff = 0.9f;

	private Mesh mesh;
	private Mesh collisionMesh;

	[Header("Compute shaders")]
	public ComputeShader computeTerrain;

	[Header("Noise Settings")]
	public NoiseSettings shapeSettings = new NoiseSettings(3, 1, 0, 0, 2, 0.5f, new Vector3(0,0,0));
	public NoiseSettings detailSettings = new NoiseSettings(3, 1, 0, 0, 2, 0.5f, new Vector3(0, 0, 0));
	public NoiseSettings ridgeSettings = new NoiseSettings(3, 1, 0, 0, 2, 0.5f, new Vector3(0, 0, 0));

	[Header("Ocean Settings")]
	public NoiseSettings oceanSettings = new NoiseSettings(3, 1, 0, 0, 2, 0.5f, new Vector3(0, 0, 0));
	[Range(0f, 1f)]
	public float oceanFloor;
	public Color oceanColor1;
	public Color oceanColor2;
	[Range(0, 1)]
	public float oceanColorBlend;
	[Range(0, 1)]
	public float oceanAlpha;
	[Range(0, 1)]
	public float oceanSpecularStrength;
	[Range(0, 1000)]
	public int oceanSpecularExponent;
	[Range(0, 10)]
	public float oceanDiffuseStrength;
	public Material oceanMat;

	[Header("Atmosphere")]
	public Material atmosphereMat;

	[Header("Colors")]
	public NoiseSettings colorSettings = new NoiseSettings(3, 1, 0, 0, 2, 0.5f, new Vector3(0, 0, 0));
	public Color color1;
	public Color color2;
	[Range(0f, 5f)]
	public float blendWidth;
	[Range(-1f, 1f)]
	public float blendHeight;

	[Header("Craters")]
	public int numCraters = 0;
	public AnimationCurve craterSizeCurve;
	[Range(0f, 1f)]
	public float maxRadius = 1f;
	[Range(0f, 1f)]
	public float minRadius = 0f;
	[Range(0f, 2.5f)]
	public float rimSteepness = 0.5f;
	[Range(0f, 1f)]
	public float rimWidth = 0.5f;
	[Range(-1f, 1f)]
	public float floorHeight = -1f;
	[Range(0f, 1f)]
	public float craterSmoothness = 0.5f;

	private ComputeBuffer verticesBuffer;
	private ComputeBuffer heightBuffer;
	private ComputeBuffer colorDataBuffer;
	private ComputeBuffer noiseSettingsBuffer;
	private ComputeBuffer craterBuffer;

	private static readonly int verticesBufferID = Shader.PropertyToID("vertices");
	private static readonly int noiseSettingsBufferID = Shader.PropertyToID("noiseSettings");
	private static readonly int cratersBufferID = Shader.PropertyToID("craters");
	private static readonly int heightBufferID = Shader.PropertyToID("heights");
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

	// Main thread variables
	private Vector3[] vertices;
	private int[] triangleIndices;
	private int[] colliderTriangleIndices;
	Dictionary<System.Tuple<int, int>, int>[] edgeCaseBuckets;

	// Mesh thread variables
	private Vector3 cameraPos;
	private Matrix4x4 worldToLocalMatrix;
	private List<Vector3> threadVertices;
	private List<int> threadTriangleIndices;
	private List<int> threadColliderTriangleIndices;
	private Triangle[] triangles;
	Dictionary<System.Tuple<int, int>, int> verticesMap;
	Dictionary<System.Tuple<int, int>, int>[] threadEdgeCaseBuckets;

	bool newMeshData = false;
	Thread meshThread;
	Object meshDataLock = new Object();

	// Start is called before the first frame update
	void Start()
	{
		GameManager.Instance.planet = this;
		edgeCaseBuckets = new Dictionary<System.Tuple<int, int>, int>[maxDetail];
		mesh = new Mesh();
		mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
		collisionMesh = new Mesh();
		collisionMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
		meshThread = new Thread(MeshThread);
		meshThread.Start();
	}

	public void UpdateTerrain()
	{
		Random.State prevRNGState = Random.state;
		Random.InitState(randomSeed);

		if(verticesBuffer != null)
        {
			verticesBuffer.Release();
		}

		if (heightBuffer != null)
		{
			heightBuffer.Release();
		}

		if (colorDataBuffer != null)
		{
			colorDataBuffer.Release();
		}

		if (noiseSettingsBuffer != null)
		{
			noiseSettingsBuffer.Release();
		}

		if (craterBuffer != null)
		{
			craterBuffer.Release();
		}

		verticesBuffer = new ComputeBuffer(vertices.Length, 12);
		noiseSettingsBuffer = new ComputeBuffer(5, 36);
		heightBuffer = new ComputeBuffer(vertices.Length, 4);
		colorDataBuffer = new ComputeBuffer(vertices.Length, 16);

		int kernelHandle = computeTerrain.FindKernel("ComputeTerrain");

		verticesBuffer.SetData(vertices);
		noiseSettingsBuffer.SetData(new NoiseSettings[] { shapeSettings, detailSettings, colorSettings, ridgeSettings, oceanSettings});

		computeTerrain.SetBuffer(kernelHandle, verticesBufferID, verticesBuffer);
		computeTerrain.SetBuffer(kernelHandle, noiseSettingsBufferID, noiseSettingsBuffer);
		computeTerrain.SetBuffer(kernelHandle, heightBufferID, heightBuffer);
		computeTerrain.SetBuffer(kernelHandle, colorDataBufferID, colorDataBuffer);
		computeTerrain.SetInt(numCratersID, numCraters);
		computeTerrain.SetFloat(rimSteepnessID, rimSteepness);
		computeTerrain.SetFloat(rimWidthID, rimWidth);
		computeTerrain.SetFloat(floorHeightID, floorHeight);
		computeTerrain.SetFloat(craterSmoothnessID, craterSmoothness);
		computeTerrain.SetFloat(oceanFloorID, oceanFloor);
		computeTerrain.SetFloats(color1ID, new float[] { color1.r, color1.g, color1.b, color1.a });
		computeTerrain.SetFloats(color2ID, new float[] { color2.r, color2.g, color2.b, color2.a });
		computeTerrain.SetFloat(blendWidthID, blendWidth);
		computeTerrain.SetFloat(blendHeightID, blendHeight);

		Crater[] craters;
		if (numCraters != 0)
		{
			craterBuffer = new ComputeBuffer(numCraters, 16);
			craters = new Crater[numCraters];
		} else // make default undrawn crater because compute buffers can't be empty
        {
			craterBuffer = new ComputeBuffer(1, 16);
			craters = new Crater[] { new Crater(0, Vector3.zero) };
        }

		for(int i = 0; i < numCraters; i++)
        {
 			craters[i] = new Crater(craterSizeCurve.Evaluate(Random.Range(minRadius, maxRadius)), Random.insideUnitSphere);
        }
		craterBuffer.SetData(craters);
		computeTerrain.SetBuffer(kernelHandle, cratersBufferID, craterBuffer);

		int groupSize = Mathf.CeilToInt(vertices.Length / 256f);

		computeTerrain.Dispatch(kernelHandle, groupSize, 1, 1);

		float[] heights = new float[vertices.Length];
		Color[] colorData = new Color[vertices.Length];

		heightBuffer.GetData(heights);
		colorDataBuffer.GetData(colorData);

		for(int i = 0; i < vertices.Length; i++)
        {
			vertices[i] = vertices[i] * heights[i];
        }

		for(int i = 0; i < edgeCaseBuckets.Length; i++)
        {
			foreach(KeyValuePair<System.Tuple<int, int>, int> index in edgeCaseBuckets[i])
            {
				vertices[index.Value] = (vertices[index.Key.Item1] + vertices[index.Key.Item2]) / 2;
			}
        }

		mesh.Clear();
		mesh.SetVertices(vertices);
		mesh.SetTriangles(triangleIndices, 0);
		mesh.SetColors(colorData);
		mesh.RecalculateNormals();
		mesh.RecalculateBounds();
		mesh.RecalculateTangents();
		GetComponent<MeshFilter>().mesh = mesh;

		collisionMesh.Clear();
		collisionMesh.SetVertices(vertices);
		collisionMesh.SetTriangles(colliderTriangleIndices, 0);
		collisionMesh.RecalculateNormals();
		collisionMesh.RecalculateBounds();
		collisionMesh.RecalculateTangents();
		GetComponent<MeshCollider>().sharedMesh = collisionMesh;

		Random.state = prevRNGState;
	}

	void Update()
    {
		worldToLocalMatrix = transform.worldToLocalMatrix;
		cameraPos = Camera.main.transform.position;
		lock(meshDataLock)
        {
			if (newMeshData)
			{
				UpdateTerrain();
				newMeshData = false;
			}
		}
    }

    private void OnDisable()
    {
		verticesBuffer.Release();
		heightBuffer.Release();
		colorDataBuffer.Release();
		noiseSettingsBuffer.Release();
		craterBuffer.Release();
		verticesBuffer = null;
		heightBuffer = null;
		colorDataBuffer = null;
		noiseSettingsBuffer = null;
		craterBuffer = null;
		meshThread.Abort();
    }

	void MeshThread()
	{
		RebuildMesh();
		int extraTriangles = 0;
		while (true)
		{
			if(extraTriangles > (threadTriangleIndices.Count / 3) * 0.25f)
            {
				RebuildMesh();
				extraTriangles = 0;
			} else
            {
				extraTriangles = UpdateMesh();
			}
			Thread.Sleep(meshUpdateIntervalMillis);
		}
	}

	void RebuildMesh()
    {
		// initialize starting octahedron
		threadVertices = new List<Vector3>() {
			new Vector3(0.0f, 1.0f, 0.0f),
			new Vector3(0.0f, 0.0f, 1.0f),
			new Vector3(1.0f, 0.0f, 0.0f),
			new Vector3(0.0f, 0.0f, -1.0f),
			new Vector3(-1.0f, 0.0f, 0.0f),
			new Vector3(0.0f, -1.0f, 0.0f)
			};

		threadTriangleIndices = new List<int>()
			{
				0, 1, 2,
				0, 2, 3,
				0, 3, 4,
				0, 4, 1,
				5, 1, 4,
				5, 4, 3,
				5, 3, 2,
				5, 2, 1
			};

		threadColliderTriangleIndices = new List<int>();

		verticesMap = new Dictionary<System.Tuple<int, int>, int> {
				{new System.Tuple<int, int>(0, 0), 0},
				{new System.Tuple<int, int>(1, 1), 1},
				{new System.Tuple<int, int>(2, 2), 2},
				{new System.Tuple<int, int>(3, 3), 3},
				{new System.Tuple<int, int>(4, 4), 4},
				{new System.Tuple<int, int>(5, 5), 5}
			};

		threadEdgeCaseBuckets = new Dictionary<System.Tuple<int, int>, int>[maxDetail];

		for (int i = 0; i < maxDetail; i++)
		{
			threadEdgeCaseBuckets[i] = new Dictionary<System.Tuple<int, int>, int>();
		}

		triangles = new Triangle[] {
				new Triangle(0, 1, 2, null),
				new Triangle(0, 2, 3, null),
				new Triangle(0, 3, 4, null),
				new Triangle(0, 4, 1, null),
				new Triangle(5, 1, 4, null),
				new Triangle(5, 4, 3, null),
				new Triangle(5, 3, 2, null),
				new Triangle(5, 2, 1, null),
			};

		UpdateMesh();
	}

	int UpdateMesh()
    {
		int extraTriangles = 0;

		// recurse all triangles until appropriate detail level is reached
		Stack<Triangle> triangleStack = new Stack<Triangle>(triangles);

		List<Triangle> colliderTriangles = new List<Triangle>();

		while (triangleStack.Count != 0)
		{
			Triangle triangle = triangleStack.Pop();

			if (triangle.children != null)
			{
				foreach (Triangle t in triangle.children)
				{
					triangleStack.Push(t);
				}
				continue;
			}

			Vector3 trianglePos = (threadVertices[triangle.a] + threadVertices[triangle.b] + threadVertices[triangle.c]) / 3;
			Vector3 towardTriangle = trianglePos.normalized;
			Vector3 toCamera = worldToLocalMatrix * cameraPos;
			float dot = Vector3.Dot(toCamera.normalized, towardTriangle);
			float directionDetail = detailCurve.Evaluate(dot);

			Vector3 triangleToCamera = toCamera - trianglePos;
			float distanceDetail = falloffCurve.Evaluate(1 - (triangleToCamera.magnitude - maxDetailDistance) / minDetailDistance);
			int detail = Mathf.CeilToInt(Mathf.Min(directionDetail, distanceDetail) * (maxDetail - minDetail) + minDetail);

			if(triangle.detail > detail)
            {
				extraTriangles++;
            }

			if (triangle.detail < detail)
			{
				Subdivide(triangle);
				foreach (Triangle t in triangle.children)
				{
					triangleStack.Push(t);
				}
			}
			else if (dot > meshColliderCutoff)
			{
				colliderTriangles.Add(triangle);
			}
		}

		// dump only the highest detail triangles to the final triangle array
		triangleStack = new Stack<Triangle>(triangles);
		threadTriangleIndices.Clear();

		while (triangleStack.Count != 0)
		{
			Triangle triangle = triangleStack.Pop();

			if (triangle.children == null)
			{
				threadTriangleIndices.AddRange(triangle.Indices());
			}
			else
			{
				foreach (Triangle t in triangle.children)
				{
					triangleStack.Push(t);
				}
			}
		}

		threadColliderTriangleIndices.Clear();

		// build separate mesh data for collision
		foreach (Triangle triangle in colliderTriangles)
		{
			threadColliderTriangleIndices.AddRange(triangle.Indices());
		}

		lock (meshDataLock)
		{
			vertices = threadVertices.ToArray();
			triangleIndices = threadTriangleIndices.ToArray();
			colliderTriangleIndices = threadColliderTriangleIndices.ToArray();
			for (int i = 0; i < threadEdgeCaseBuckets.Length; i++)
			{
				edgeCaseBuckets[i] = new Dictionary<System.Tuple<int, int>, int>(threadEdgeCaseBuckets[i]);
			}
			newMeshData = true;
		}

		return extraTriangles;
	}

	void Subdivide(Triangle triangle)
	{
		int ab = MidPoint(triangle.a, triangle.b, triangle.detail);
		int bc = MidPoint(triangle.b, triangle.c, triangle.detail);
		int ac = MidPoint(triangle.a, triangle.c, triangle.detail);
		Triangle child1 = new Triangle(triangle.a, ab, ac, triangle);
		Triangle child2 = new Triangle(triangle.b, bc, ab, triangle);
		Triangle child3 = new Triangle(triangle.c, ac, bc, triangle);
		Triangle child4 = new Triangle(ab, bc, ac, triangle);
		triangle.children = new Triangle[] { child1, child2, child3, child4 };
	}

	int MidPoint(int a, int b, int lod)
	{
		int lower = a < b ? a : b;
		int higher = a >= b ? a : b;
		System.Tuple<int, int> key = new System.Tuple<int, int>(lower, higher);
		int result;

		if (verticesMap.TryGetValue(key, out result))
		{
			threadEdgeCaseBuckets[lod].Remove(key);
			return result;
		}
		else
		{
			threadVertices.Add((threadVertices[a] + threadVertices[b]).normalized);
			verticesMap.Add(key, threadVertices.Count - 1);
			threadEdgeCaseBuckets[lod].Add(key, threadVertices.Count - 1);
			return threadVertices.Count - 1;
		}
	}


	class Triangle
	{
		public int a, b, c, detail;
		public Triangle parent;
		public Triangle[] children;

		public Triangle(int a, int b, int c, Triangle parent)
		{
			this.a = a;
			this.b = b;
			this.c = c;
			this.parent = parent;

			if (parent != null)
			{
				detail = parent.detail + 1;
			}
			else
			{
				detail = 0;
			}
		}

		public int[] Indices()
		{
			return new int[] { a, b, c };
		}
	}
}