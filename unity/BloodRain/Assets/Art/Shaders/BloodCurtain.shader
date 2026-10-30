// 혈우 장막(TU-05, 기획서 7장). 태어난 자리를 가운데 두고 반경 _Radius(세계 m)에 붉은 빗줄기 벽을 그린다.
// 사각형 하나를 장막보다 조금 크게 깔고, 조각마다 가운데까지 거리로 벽 모양을 정한다.
//   - 빗줄기: 세계 좌표를 반경의 1/100 단위로 나눈 칸마다 떨어지는 줄(두 겹, 살짝 기울임). 화면에서 굵기가 경지마다 비슷하다.
//   - 벽: 반경에서 가장 진하고, 안쪽으로 갈수록 옅게 번진다(_InnerSoft). 바깥은 _Thick 만큼 진하다가 사라진다.
//   - 갈라짐: 돌파 때 _Split(0~1)만큼 벽이 부채꼴로 갈라진다. BloodCurtain 이 돌파 줌 진행값으로 넣는다.
// 색은 주홍 #B3261E 하나.
Shader "BloodRain/BloodCurtain"
{
    Properties
    {
        _Color ("Color", Color) = (0.702, 0.149, 0.118, 1)
        _InnerSoft ("Inner Soft", Range(0.01, 0.6)) = 0.22
        _Thick ("Thick", Range(0, 0.5)) = 0.06
        _OuterSoft ("Outer Soft", Range(0.01, 0.6)) = 0.18
        _Haze ("Haze", Range(0, 1)) = 0.28
        _StreakAlpha ("Streak Alpha", Range(0, 1)) = 0.85
        _FallSpeed ("Fall Speed", Float) = 2.2
        _SplitCount ("Split Count", Float) = 11
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _InnerSoft, _Thick, _OuterSoft, _Haze, _StreakAlpha, _FallSpeed, _SplitCount;
            CBUFFER_END
            // 돌파 때마다 바뀌는 값은 MaterialPropertyBlock 으로
            float4 _Center;   // xy 세계 m
            float _Radius;    // 세계 m
            float _Split;     // 0~1
            float _Alpha;     // 0~1 (현경으로 걷힐 때 줄어든다)
            float _Clock;     // 언스케일 시간(돌파 중 세상 시간이 느려도 비는 흐르게)

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 world : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 w = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(w);
                o.world = w.xy;
                return o;
            }

            float Hash1(float x) { return frac(sin(x * 127.1) * 43758.5453); }

            // 한 겹 빗줄기. p 는 반경/100 단위 좌표, colW 칸 너비, segL 줄 길이(칸 단위)
            float Streaks(float2 p, float colW, float segL, float speed, float seed)
            {
                p.x += p.y * 0.12;                         // 살짝 기울인 비
                float col = floor(p.x / colW);
                float h = Hash1(col + seed);
                float fx = frac(p.x / colW) - 0.5;
                float thin = 1.0 - smoothstep(0.06, 0.18 + 0.1 * h, abs(fx));
                float s = frac(p.y / (segL * (0.7 + 0.6 * h)) + _Clock * speed * (0.7 + 0.6 * h) + h * 7.3);
                float seg = smoothstep(0.0, 0.08, s) * (1.0 - smoothstep(0.25 + 0.2 * h, 0.5 + 0.2 * h, s));
                return thin * seg * step(0.25, h);          // 칸의 1/4 은 비워 성기게
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float r = max(_Radius, 0.0001);
                float2 off = i.world - _Center.xy;
                float d = length(off) / r;

                float inner = smoothstep(1.0 - _InnerSoft, 1.0, d);
                float innerHaze = smoothstep(1.0 - _InnerSoft * 2.0, 1.0, d);
                float outer = 1.0 - smoothstep(1.0 + _Thick, 1.0 + _Thick + _OuterSoft, d);
                float wall = inner * outer;
                if (innerHaze * outer <= 0.0001) discard;

                float2 p = i.world / (r * 0.01);
                // 시간이 갈수록 무늬가 y 아래로 내려간다(비가 화면 아래로 떨어진다)
                float rain = max(Streaks(p, 0.55, 5.0, _FallSpeed, 0.0),
                                 Streaks(p * 1.7 + 13.0, 0.55, 5.0, _FallSpeed * 1.3, 31.0) * 0.6);

                // 부채꼴 갈라짐
                float a = atan2(off.y, off.x) / 6.2831853 + 0.5;
                float cell = floor(a * _SplitCount);
                float local = frac(a * _SplitCount);
                float edge = min(local, 1.0 - local);
                float gw = _Split * 0.16 * (0.6 + 0.8 * Hash1(cell + 3.0));
                float gap = _Split > 0.001 ? smoothstep(gw, gw + 0.06, edge) : 1.0;

                float alpha = (rain * _StreakAlpha * wall + innerHaze * innerHaze * outer * _Haze) * gap * _Alpha;
                return half4(_Color.rgb, saturate(alpha) * _Color.a);
            }
            ENDHLSL
        }
    }
}
