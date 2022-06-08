using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct LayeredNoiseSettings
{
	public int layers;
	public float scale;
	public float elevation;
	public float verticalShift;
	public float lacunarity;
	public float gain;
	public Vector3 offset;

	public LayeredNoiseSettings(int layers, float scale, float elevation, float verticalShift, float lacunarity, float gain, Vector3 offset)
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

