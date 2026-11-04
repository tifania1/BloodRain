// 붓 획 이펙트(V-04, vfx.md '만드는 법'). tools/bake_brush.py 가 구운 흰 획 + 알파 텍스처에 색을 입혀 그린다.
//   _Progress 0~1: 획이 그어지는 정도. 호(_Kind 0)는 텍스처 가운데를 중심으로 한 각도 마스크(-_Span/2 → +_Span/2, 가운데가 +x),
//                 찌르기(_Kind 1)는 +x 방향으로 진행(uv.x 0 → 1).
//                 AI 재료(Art/VFX/AI/ai.json progress)는 2 radial·3 down·4 up·1 right·5 left (VfxBootstrap.KindOf).
//   _Dissolve 0~1: Common/noise_1 이 문턱보다 작은 곳부터 구멍이 나며 흩어진다. 가장자리는 조금 진하게.
//   _Color: 층 색(stroke 먹 #141414, wash 청회 #4F7F7A, gold #C9A227). _Alpha 로 전체를 옅게(담채가 커지며 옅어질 때).
//   _Rect: 텍스처가 원래 512 칸에서 잘라 낸 자리(uv, 왼쪽 아래 x·y, 너비·높이). bake_brush.py 가 획 있는 쪽만 잘라 json crop 에 적는다(V-10).
//          그어짐·흩어짐은 원래 칸 좌표(fu)로 셈하니 잘라도 모양이 같다. 자르지 않은 텍스처(AI·Common)는 기본값 (0,0,1,1).
// 머티리얼 하나를 MaterialPropertyBlock 으로 돌려 쓴다(WeaponRunner).
Shader "BloodRain/BrushStroke"
{
    Properties
    {
        _MainTex ("Stroke", 2D) = "white" {}
        _NoiseTex ("Noise", 2D) = "gray" {}
        _Color ("Color", Color) = (0.078, 0.078, 0.078, 1)
        _Progress ("Progress", Range(0, 1)) = 1
        _Dissolve ("Dissolve", Range(0, 1)) = 0
        _Span ("Arc span (rad)", Float) = 2.3
        _Kind ("0 arc, 1 line(right), 2 radial, 3 down, 4 up, 5 left", Float) = 0
        _Alpha ("Alpha", Range(0, 1)) = 1
        _Feather ("Mask feather", Range(0.001, 0.3)) = 0.06
        _Rect ("Crop rect in full square uv (x, y, w, h)", Vector) = (0, 0, 1, 1)
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst blend (10 보통, 1 더하기)", Float) = 10
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        // 먹(stroke·wash)은 보통 섞기, AI 빛 재료(ai_glow 의 gold·core 층)는 _DstBlend = One 으로 더하기(BrushStrokeAdd.mat)
        Blend SrcAlpha [_DstBlend]
        ZWrite Off Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _Rect;
                float _Progress, _Dissolve, _Span, _Kind, _Alpha, _Feather;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a;
                float2 fu = _Rect.xy + i.uv * _Rect.zw;   // 원래 512 칸 좌표(가운데 0.5 가 몸)

                // 그어지는 정도: 0(획 시작) ~ 1(획 끝) 위치 값 t 가 _Progress 보다 앞이면 보인다
                float t;
                if (_Kind < 0.5)
                {
                    float2 d = fu - 0.5;
                    float ang = atan2(d.y, d.x);                // -pi ~ pi, 가운데가 +x
                    t = saturate(ang / max(_Span, 1e-3) + 0.5);
                }
                else if (_Kind < 1.5) t = fu.x;                         // 찌르기·AI right: 왼 → 오른
                else if (_Kind < 2.5) t = saturate(length(fu - 0.5) * 2.0); // AI radial: 가운데 → 바깥
                else if (_Kind < 3.5) t = 1.0 - fu.y;                   // AI down: 위 → 아래
                else if (_Kind < 4.5) t = fu.y;                         // AI up
                else t = 1.0 - fu.x;                                    // AI left
                float reveal = _Progress * (1.0 + _Feather);
                a *= 1.0 - smoothstep(reveal - _Feather, reveal, t);

                // 흩어짐: 잡음이 문턱보다 낮은 곳부터 사라진다
                half n = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, fu * 1.7).r;
                half keep = smoothstep(_Dissolve, _Dissolve + 0.08, n + 0.02 * (1 - _Dissolve));
                half edge = 1.0 - smoothstep(_Dissolve, _Dissolve + 0.12, n);
                a *= _Dissolve > 0 ? keep : 1;

                half3 rgb = _Color.rgb * (1.0 - 0.35 * edge * step(0.001, _Dissolve));
                return half4(rgb, a * _Color.a * _Alpha);
            }
            ENDHLSL
        }
    }
}
