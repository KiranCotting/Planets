Shader "Custom/Atmosphere"
{
    Properties
    {
        [HideInInspector]_MainTex ("Texture", 2D) = "white" {}
        [HideInInspector]_ScatteringCoefficients("Atmosphere Scattering Coefficients", Vector) = (0.10662224, 0.32444156, 0.68301346, 0.0)
        _NumInScatterPoints("In Scatter Resolution", Int) = 10
        _NumOpticalDepthPoints("Optical Depth Resolution", Int) = 10
        _PlanetCenter("Planet Center", Vector) = (0.0,0.0,0.0,0.0)
        _PlanetRadius("Planet Radius", Float) = 100.0
        _AtmosphereRadius("Atmosphere Radius", Float) = 125.0
        _OceanRadius("Ocean Radius", Float) = 99.0
        _DensityFalloff("Atmosphere Density Falloff", Float) = 1.0
        _ScatteringStrength("Light Scatter Strength", Float) = 1.0
    }
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "Assets/ShaderHelper.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 viewVector : TEXCOORD1;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.viewVector = mul(unity_CameraInvProjection, float4(v.uv.xy * 2 - 1, 0, -1));
                o.viewVector = mul(unity_CameraToWorld, float4(o.viewVector.xyz, 0.0));
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;
            sampler2D _CameraDepthTexture;
            fixed4 _LightColor0;

            int _NumInScatterPoints;
            int _NumOpticalDepthPoints;
            float3 _PlanetCenter;
            float _PlanetRadius;
            float _AtmosphereRadius;
            float _OceanRadius;
            float _DensityFalloff;
            float3 _ScatteringCoefficients;
            float _ScatteringStrength;

            float DensityAtPoint(float3 densitySamplePoint) {
                float heightAboveSurface = length(densitySamplePoint - _PlanetCenter) - _PlanetRadius;
                float height01 = heightAboveSurface / (_AtmosphereRadius - _PlanetRadius);
                float localDensity = exp(-height01 * _DensityFalloff) * (1 - height01);
                return localDensity;
            }

            float OpticalDepth(float3 origin, float3 direction, float length) {
                float3 densitySamplePoint = origin;
                float stepSize = length / (_NumOpticalDepthPoints - 1);
                float opticalDepth = 0;

                for (int i = 0; i < _NumOpticalDepthPoints; i++) {
                    float localDensity = DensityAtPoint(densitySamplePoint);
                    opticalDepth += localDensity * stepSize;
                    densitySamplePoint += direction * stepSize;
                }
                return opticalDepth;
            }

            float3 Light(float3 origin, float3 direction, float length, float3 originalCol) {
                float3 inScatterPoint = origin;
                float stepSize = length / (_NumInScatterPoints - 1);
                float3 inScatteredLight = 0;
                float viewRayOpticalDepth = 0;

                for (int i = 0; i < _NumInScatterPoints; i++) {
                    float sunRayLength = SphereDepth(_PlanetCenter, _AtmosphereRadius, inScatterPoint, _WorldSpaceLightPos0.xyz, 0.0).y;
                    float sunRayOpticalDepth = OpticalDepth(inScatterPoint, _WorldSpaceLightPos0.xyz, sunRayLength);
                    viewRayOpticalDepth = OpticalDepth(inScatterPoint, -direction, stepSize * i);
                    float3 transmittance = exp(-(sunRayOpticalDepth + viewRayOpticalDepth) * _ScatteringCoefficients * _ScatteringStrength);
                    float localDensity = DensityAtPoint(inScatterPoint);
                    inScatterPoint += direction * stepSize;

                    inScatteredLight += localDensity * _ScatteringCoefficients * _ScatteringStrength * transmittance * stepSize;
                    inScatterPoint += direction * stepSize;
                }
                float originalColTransmittance = exp(-viewRayOpticalDepth * _ScatteringCoefficients * _ScatteringStrength * 0.01);
                return originalCol * originalColTransmittance + inScatteredLight * _LightColor0.xyz;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
                depth = LinearEyeDepth(depth) * length(i.viewVector);

                float3 viewVector = normalize(i.viewVector);

                float oceanDistance = SphereDepth(_PlanetCenter, _OceanRadius, _WorldSpaceCameraPos, viewVector, depth).x;
                float distanceToSurface = min(depth, oceanDistance);
                
                float2 result = SphereDepth(_PlanetCenter, _AtmosphereRadius, _WorldSpaceCameraPos, viewVector, 0);
                float atmosphereDepth = min(result.y, distanceToSurface - result.x);

                fixed4 col = tex2D(_MainTex, i.uv);
                
                if (atmosphereDepth > 0.0) {
                    float3 pointInAtmosphere = _WorldSpaceCameraPos + viewVector * result.x;
                    float3 light = Light(pointInAtmosphere, viewVector, atmosphereDepth, col);
                    col = float4(light, 0);
                }

                return col;
            }
            ENDCG
        }
    }
}
