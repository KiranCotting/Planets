Shader "Custom/Surface"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Scale("Texture Scale", Float) = 1
        _NormalMap1("Normal Map 1", 2D) = "bump" {}
        _Normal1Scale("Normal Map 1 Scale", Float) = 1
        _NormalMap2("Normal Map 2", 2D) = "bump" {}
        _Normal2Scale("Normal Map 2 Scale", Float) = 1
        _NormalStrength("Normal Map Strength", Range(0, 1)) = 1
        _BlendTex("Blend Texture", 2D) = "gray" {}
        _BlendScale("Blend Texture Scale", Float) = 1
        _BlendSharpness("Blend Sharpness", Range(1, 10)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

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
                float3 normal : NORMAL;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vec : SV_POSITION;
                float4 pos : TEXCOORD0;
                float3 normal : NORMAL;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float _Scale;
            sampler2D _NormalMap1;
            float _Normal1Scale;
            sampler2D _NormalMap2;
            float _Normal2Scale;
            float _NormalStrength;
            sampler2D _BlendTex;
            float _BlendScale;
            float _BlendSharpness;
            fixed4 _LightColor0;

            v2f vert (appdata v)
            {
                v2f o;
                o.vec = UnityObjectToClipPos(v.vertex);
                o.pos = v.vertex;
                o.normal = v.normal;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float4 col = TriplanarColor(i.pos, i.normal, _Scale, _MainTex, _BlendSharpness);
                float4 blendCol = TriplanarColor(i.pos, i.normal, _BlendScale, _BlendTex, _BlendSharpness);

                float3 normal1 = TriplanarNormal(i.pos, i.normal, _Normal1Scale, _NormalMap1, _BlendSharpness);
                float3 normal2 = TriplanarNormal(i.pos, i.normal, _Normal2Scale, _NormalMap2, _BlendSharpness);
                float3 normal = normalize(lerp(normal1, normal2, tex2D(_BlendTex, length(blendCol))));
                normal = lerp(i.normal, normal, _NormalStrength);
                normal = normalize(mul(unity_ObjectToWorld, normal));
                float diffuseLight = dot(normal, _WorldSpaceLightPos0.xyz);
                col *= i.color;
                col *= fixed4(diffuseLight * _LightColor0.xyz, 1.0);
                return col;
            }
            ENDCG
        }
    }
    Fallback "Standard"
}
