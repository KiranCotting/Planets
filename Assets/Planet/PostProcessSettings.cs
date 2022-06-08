using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[System.Serializable]
public struct PostProcessSettings
{
	public Material atmosphereMat;

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
}
