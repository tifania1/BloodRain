// 적 덩이 렌더러(TU-04)용. Graphics.RenderMeshInstanced 로 사람 하나를 사각형 하나로 그린다.
// 사람마다 _Tint(색)와 _Dissolve(0 그대로 ~ 1 다 사라짐)를 인스턴스 값으로 받는다.
// 사라질 때는 먹물이 번지듯: 잡음보다 _Dissolve 가 큰 곳부터 지워지고, 지워지는 가장자리는 먹빛.
Shader "BloodRain/ClusterInk"
{
    Properties
    {
        _MainTex ("Atlas", 2D) = "white" {}
        _InkColor ("Ink", Color) = (0.07, 0.06, 0.06, 1)
        _EdgeWidth ("Edge", Range(0, 0.3)) = 0.12
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _InkColor;
                float _EdgeWidth;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Tint)
                UNITY_DEFINE_INSTANCED_PROP(float, _Dissolve)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float seed : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                // 사람마다 다른 잡음이 되게 월드 원점 위치를 씨앗으로
                float3 origin = TransformObjectToWorld(float3(0, 0, 0));
                o.seed = frac(sin(dot(origin.xy, float2(12.9898, 78.233))) * 43758.5453);
                return o;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 tint = UNITY_ACCESS_INSTANCED_PROP(Props, _Tint);
                float dissolve = UNITY_ACCESS_INSTANCED_PROP(Props, _Dissolve);
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * tint;
                if (dissolve > 0.0001)
                {
                    float n = Noise(i.uv * 6.0 + i.seed * 17.0) * 0.7 + Noise(i.uv * 14.0 + i.seed * 5.0) * 0.3;
                    float cut = dissolve * (1.0 + _EdgeWidth);
                    if (n < cut - _EdgeWidth) col.a = 0;
                    else if (n < cut) col.rgb = _InkColor.rgb;
                }
                clip(col.a - 0.01);
                return col;
            }
            ENDHLSL
        }
    }
}
