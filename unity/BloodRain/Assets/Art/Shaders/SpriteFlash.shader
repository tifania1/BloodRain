// 플레이어 몸 그림(BD-01U, 기획서 13장 '피격: 흰색 번쩍임 셰이더').
//   SpriteRenderer 가 넣는 _MainTex × 꼭짓점 색(SpriteRenderer.color)에 _Flash(0~1)만큼 흰색을 섞는다. 알파는 그대로라 그림 모양대로 하얘진다.
//   PlayerLook 이 MaterialPropertyBlock 으로 _Flash 를 넣는다. 빛을 안 받는(unlit) 스프라이트.
Shader "BloodRain/SpriteFlash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Flash ("Flash", Range(0, 1)) = 0
        _FlashColor ("Flash color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "CanUseSpriteAtlas" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _FlashColor;
                float _Flash;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                c.rgb = lerp(c.rgb, _FlashColor.rgb, saturate(_Flash));
                return c;
            }
            ENDHLSL
        }
    }
}
