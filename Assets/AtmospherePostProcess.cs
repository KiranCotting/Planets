using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
[ImageEffectAllowedInSceneView]
public class AtmospherePostProcess : MonoBehaviour
{
    void Start()
    {
        Camera cam = GetComponent<Camera>();
        cam.depthTextureMode |= DepthTextureMode.Depth;
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Planet planet = GameManager.Instance.planet;
        if (planet && planet.atmosphereMat)
        {
            Graphics.Blit(source, destination, planet.atmosphereMat);
        }
    }
}
