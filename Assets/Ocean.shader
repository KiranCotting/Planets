Shader "Custom/Ocean"
{
    Properties
    {
        [HideInInspector]_MainTex("Texture", 2D) = "white" {}
        _OceanCenter("Ocean Center", Vector) = (0.0,0.0,0.0,0.0)
        _OceanRadius("Ocean Radius", Float) = 99.0
        _OceanColor1("Ocean Color 1", Color) = (1.0,1.0,1.0,1.0)
        _OceanColor2("Ocean Color 2", Color) = (0,0,0,0)
        _OceanColorBlend("Ocean Color Blend", Float) = 0.25
        _OceanAlpha("Ocean Alpha", Float) = 0.5
        _SpecularStrength("Specular Strength", Float) = 1.0
        _SpecularExponent("Specular Exponent", Int) = 200
        _DiffuseStrength("Diffuse Strength", Float) = 1.5
        _NormalMap1("Normal Map 1", 2D) = "bump" {}
        _NormalMap2("Normal Map 2", 2D) = "bump" {}
        _NormalScale("Normal Map Scale", Float) = 0.05
        _NormalStrength("Normal Map Strength", Range(0, 1)) = 0.5
        _NormalBlendSharpness("Normal Map Blend Sharpness", Range(1, 10)) = 2
        _NormalMapScrollSpeed("Normal Map Scroll Speed", Float) = 10
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
                float3 viewVector : TEXCOORD1;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.viewVector = mul(unity_CameraInvProjection, float4(v.uv.xy * 2 - 1, 0, -1));
                o.viewVector = mul(unity_CameraToWorld, float4(o.viewVector.xyz, 0));
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;
            float4 _OceanCenter;
            float _OceanRadius;
            float4 _OceanColor1;
            float4 _OceanColor2;
            float _OceanColorBlend;
            float _OceanAlpha;
            float _SpecularStrength;
            int _SpecularExponent;
            float _DiffuseStrength;
            sampler2D _NormalMap1;
            sampler2D _NormalMap2;
            float _NormalScale;
            float _NormalStrength;
            float _NormalBlendSharpness;
            float _NormalMapScrollSpeed;
            sampler2D _CameraDepthTexture;
            fixed4 _LightColor0;

            fixed4 frag (v2f i) : SV_Target
            {
                float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv).r;
                depth = LinearEyeDepth(depth) * length(i.viewVector);

                float2 result = SphereDepth(_OceanCenter, _OceanRadius, _WorldSpaceCameraPos, normalize(i.viewVector), 0.0);
                float oceanDepth = max(min(result.y, depth - result.x), 0.0);
                float colorBlend = 1 - exp(-oceanDepth * _OceanColorBlend);
                float alpha = 1 - exp(-oceanDepth * _OceanAlpha);
                float4 oceanCol = lerp(_OceanColor1, _OceanColor2, colorBlend);

                float3 surfacePos = _WorldSpaceCameraPos + normalize(i.viewVector) * result.x;
                float3 surfaceNormal = normalize(surfacePos);
                float3 surfacePosScrollX = surfacePos;
                float3 surfacePosScrollY = surfacePos;
                surfacePosScrollX.x += _Time * _NormalMapScrollSpeed;
                surfacePosScrollY.x -= _Time * _NormalMapScrollSpeed;
                float3 waveNormal1 = TriplanarNormal(surfacePosScrollX, surfaceNormal, _NormalScale, _NormalMap1, _NormalBlendSharpness);
                float3 waveNormal2 = TriplanarNormal(surfacePosScrollY, surfaceNormal, _NormalScale, _NormalMap2, _NormalBlendSharpness);
                float3 waveNormal = normalize(waveNormal1 + waveNormal2);
                surfaceNormal = normalize(lerp(surfaceNormal, waveNormal, _NormalStrength));

                // diffuse lighting
                float diffuseLight = _DiffuseStrength * dot(surfaceNormal, _WorldSpaceLightPos0.xyz);

                // reflection vector
                float3 reflection = 2 * dot(surfaceNormal, _WorldSpaceLightPos0.xyz) * surfaceNormal - _WorldSpaceLightPos0.xyz;
                // specular light
                float specularLight = 0.0;
                // only draw specular when above water
                if (depth > oceanDepth) {
                    specularLight = _SpecularStrength * pow(max(dot(normalize(-i.viewVector), reflection), 0.0), _SpecularExponent);
                }

                oceanCol *= fixed4(diffuseLight * _LightColor0.xyz, 1.0);
                oceanCol += fixed4(specularLight * _LightColor0.xyz, 0.0);

                fixed4 col = tex2D(_MainTex, i.uv);
                col = lerp(col, oceanCol, alpha);

                return col;
            }
            ENDCG
        }
    }
}
