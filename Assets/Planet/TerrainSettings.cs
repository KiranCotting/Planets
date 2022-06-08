using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[System.Serializable]
public struct TerrainSettings
{
	public int randomSeed;

	[Header("Noise Settings")]
	public LayeredNoiseSettings shapeSettings;
	public LayeredNoiseSettings detailSettings;
	public LayeredNoiseSettings ridgeSettings;

	[Header("Ocean Settings")]
	public LayeredNoiseSettings oceanSettings;
	[Range(0f, 1f)]
	public float oceanFloor;

	[Header("Colors")]
	public LayeredNoiseSettings colorSettings;
	public Color color1;
	public Color color2;
	[Range(0f, 5f)]
	public float blendWidth;
	[Range(-1f, 1f)]
	public float blendHeight;

	[Header("Craters")]
	public int numCraters;
	public AnimationCurve craterSizeCurve;
	[Range(0f, 1f)]
	public float maxRadius;
	[Range(0f, 1f)]
	public float minRadius;
	[Range(0f, 2.5f)]
	public float rimSteepness;
	[Range(0f, 1f)]
	public float rimWidth;
	[Range(-1f, 1f)]
	public float floorHeight;
	[Range(0f, 1f)]
	public float craterSmoothness;
}
