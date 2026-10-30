// 절기 각성 화면 효과(vfx.md '절기 각성 연출'). URP Full Screen Pass 에 물려 쓴다.
// 세상은 흑백, 깨어나는 사람 둘레(_AwakenCenter, _AwakenRadius)만 색이 남고 가장자리는 어둡게.
// 값은 전역 셰이더 변수로 받는다(AwakeningFx 가 매 프레임 넣는다). _AwakenAmount 가 0이면 원래 화면 그대로.
Shader "BloodRain/AwakenHole"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Blend Off Cull Off

        Pass
        {
            Name "AwakenHole"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _AwakenCenter;   // xy: 뷰포트 좌표(0~1)
            float _AwakenRadius;    // 화면 세로 기준 반지름(1 = 화면 높이)
            float _AwakenAmount;    // 0 꺼짐 ~ 1 다 켜짐
            float _AwakenSoft;      // 구멍 가장자리 번짐 폭
            float _AwakenVignette;  // 가장자리 어둡게 세기
            float _AwakenInvert;    // 1이면 탁본처럼 뒤집힘(낙관 순간 0.08초)

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                if (_AwakenAmount <= 0.0001) return col;

                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 d = (uv - _AwakenCenter.xy) * float2(aspect, 1.0);
                float dist = length(d);
                // 구멍 안 1(색), 밖 0(흑백)
                float hole = 1.0 - smoothstep(_AwakenRadius, _AwakenRadius + _AwakenSoft, dist);

                half gray = dot(col.rgb, half3(0.299, 0.587, 0.114));
                half3 mono = gray.xxx * half3(1.01, 1.0, 0.97); // 먹빛으로 아주 조금 따뜻하게
                half3 rgb = lerp(mono, col.rgb, hole);

                float2 v = (uv - 0.5) * float2(aspect, 1.0);
                float vig = 1.0 - _AwakenVignette * smoothstep(0.35, 1.0, length(v)) * (1.0 - hole);
                rgb *= vig;

                rgb = lerp(col.rgb, rgb, _AwakenAmount);
                rgb = lerp(rgb, 1.0 - rgb, _AwakenInvert);
                return half4(rgb, col.a);
            }
            ENDHLSL
        }
    }
}
