// 무공 이펙트 통합 셰이더(VF-01U, 2026-11-13). 시안 web/lookdev/sects.html 의 FS 를 그대로 옮긴 것.
// 텍스처 한 장을 띄우는 대신 모양을 식으로 그리고, 흐르는 잡음으로 가장자리를 침식하고,
// 색 램프(가장자리 → 가운데 → 심)로 칠하고, 긋기(_Prog)와 흩어짐(_Diss)으로 움직인다.
// 모양(_Shape): 0 호(검·봉 궤적) · 1 줄기(찌르기·낙하 검기·섬광) · 2 충격 고리 · 3 터짐·얼룩 · 4 번개 가지 · 5 방울·그림자 · 7 권영
// 밝은 종이 위에선 더하기만 하면 하얗게 날아간다. 빛은 '색 있는 몸(_Add 0) + 약한 더하기(_Add 1, 알파 0.4) + 먹 받침' 세 겹으로 그린다(SectFx.Glow).
Shader "BloodRain/FxInk"
{
    Properties
    {
        _NoiseTex ("Noise (R·G fbm, B grain)", 2D) = "gray" {}
        _Shape ("Shape", Float) = 0
        _P ("Shape params", Vector) = (2.3, 0.4, 1, 0.5)
        _Prog ("Progress", Range(0, 1)) = 1
        _Diss ("Dissolve", Range(0, 1)) = 0
        _Seed ("Seed", Float) = 0
        _FxTime ("Time", Float) = 0
        _Alpha ("Alpha", Range(0, 1)) = 1
        _Add ("Additive (0 보통, 1 더하기)", Range(0, 1)) = 0
        _C0 ("Edge", Color) = (0.13, 0.12, 0.11, 1)
        _C1 ("Mid", Color) = (0.06, 0.06, 0.06, 1)
        _C2 ("Core", Color) = (0.02, 0.02, 0.02, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend One OneMinusSrcAlpha   // 미리 곱한 알파: _Add 1 이면 알파를 0 으로 내보내 더하기가 된다
        ZWrite Off Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _NoiseTex_ST;
                float4 _P;
                float4 _C0, _C1, _C2;
                float _Shape, _Prog, _Diss, _Seed, _FxTime, _Alpha, _Add;
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

            float n1(float2 p) { return SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, p).r; }
            float n2(float2 p) { return SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, p).g; }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 v = i.uv;
                float2 p = (v - 0.5) * 2.0;
                float r = length(p);
                float ang = atan2(p.y, p.x);
                float a = 0.0, heat = 0.0;

                if (_Shape < 0.5)
                {
                    // 호. _P: 펼침(rad), 굵기, 꼬리(온전히 남는 길이 0~1), 거칠기. 펼침이 한 바퀴(> 6)면 머리에서 꼬리로 가늘어진다(도는 검기).
                    float R = 0.78;
                    float t = ang / _P.x + 0.5;
                    float h = _Prog * 1.12;
                    float inArc = step(0.0, t) * step(t, 1.0);
                    float bh0 = max(h - t, 0.0);
                    float tap = _P.x > 6.0
                        ? (0.3 + 0.7 * exp(-bh0 / max(_P.z, 0.02) * 1.1)) * smoothstep(0.0, 0.04, _Prog)
                        : pow(max(sin(PI * saturate(t)), 0.0), 0.55);
                    float w = _P.y * tap * R + 1e-4;
                    float x = (R - r) / w + (n1(float2(t * 1.3 + _Seed, 0.37)) - 0.5) * 0.3 * _P.w;
                    float body = smoothstep(0.0, 0.07, x) * (1.0 - smoothstep(0.62, 1.0, x));
                    float bh = h - t;
                    float sweep = smoothstep(0.0, 0.03, bh);
                    float streak = n2(float2(t * 0.9 + _Seed * 1.7, x * 2.2 + _Seed));
                    float grain = n1(float2(ang * 0.8 + _Seed, r * 3.0 - _FxTime * 0.6));
                    float th = _Diss * 1.2 + max(bh - _P.z, 0.0) * (_P.x > 6.0 ? 3.2 : 1.5) + _P.w * (0.3 * (1.0 - streak) + 0.55 * x * x) - 0.16;
                    a = inArc * body * sweep * smoothstep(th, th + 0.18, lerp(streak, grain, 0.4));
                    heat = saturate((1.0 - smoothstep(0.05, 0.45, x)) * 0.7 + exp(-max(bh, 0.0) * 9.0) * 0.6);
                }
                else if (_Shape < 1.5)
                {
                    // 줄기. _P: 굵기, 거칠기, 흐름 속도, 꼬리
                    float x = v.x;
                    float h = _Prog * 1.1;
                    float w = _P.x * pow(max(x, 0.0), 0.6) * (1.0 - smoothstep(0.8, 1.0, x)) + 1e-4;
                    float e = abs(p.y) / w;
                    float body = 1.0 - smoothstep(0.5, 1.0, e);
                    float bh = h - x;
                    float streak = n2(float2(x * 0.8 - _FxTime * _P.z + _Seed, p.y * 1.5 + _Seed));
                    float th = _Diss * 1.2 + _P.y * 0.4 * (1.0 - streak) - 0.12 + max(bh - _P.w, 0.0) * 1.4;
                    a = body * smoothstep(0.0, 0.04, bh) * smoothstep(th, th + 0.2, streak);
                    heat = saturate((1.0 - e) * 0.8 + exp(-max(bh, 0.0) * 8.0) * 0.5);
                }
                else if (_Shape < 2.5)
                {
                    // 충격 고리. _P: 반지름, 두께, 잡음
                    float d = (r - _P.x) / _P.y;
                    float band = exp(-d * d);
                    float nz = n1(float2(ang * 0.5 + _Seed, r * 1.5 - _FxTime * 0.3));
                    a = band * lerp(1.0, smoothstep(0.25, 0.75, nz), _P.z) * (1.0 - _Diss);
                    heat = band;
                }
                else if (_Shape < 3.5)
                {
                    // 터짐·얼룩. _P: 가시 수, 가시 세기, 반지름
                    float sp = n1(float2(ang * _P.x * 0.159 + _Seed, _Seed * 0.3));
                    float len = _P.z * (1.0 + _P.y * (sp - 0.5) * 2.0);
                    float body = 1.0 - smoothstep(len * 0.55, len, r);
                    float er = n2(p * 0.8 + _Seed);
                    a = body * smoothstep(_Diss, _Diss + 0.15, er + 0.02) * step(_Diss, 0.999);
                    heat = saturate(1.0 - r / len);
                }
                else if (_Shape < 4.5)
                {
                    // 번개 가지. _P: 꺾임, 굵기, 가지
                    float x = v.x;
                    float env = sin(PI * x);
                    float tq = floor(_FxTime * 24.0) * 0.37;
                    float off = ((n1(float2(x * 1.7 + _Seed + tq, _Seed)) - 0.5) * 2.0 + (n2(float2(x * 6.0 + tq, _Seed * 2.0)) - 0.5) * 0.6) * _P.x * env;
                    float d = abs(p.y - off);
                    float core = _P.y / (d + _P.y * 0.5);
                    float bx = saturate((x - 0.35) / 0.5);
                    float d2 = abs(p.y - off - bx * (n2(float2(x * 2.3 + _Seed * 3.0, tq)) - 0.3) * _P.z);
                    float c2 = step(0.35, x) * step(x, 0.85) * (_P.y * 0.6 / (d2 + _P.y * 0.5));
                    a = saturate(core * core * 0.25 + c2 * c2 * 0.2) * step(x, _Prog * 1.2) * (1.0 - _Diss);
                    heat = saturate(core * 0.5);
                }
                else if (_Shape < 5.5)
                {
                    // 방울·불씨·그림자. _P.x: 단단함
                    a = 1.0 - smoothstep(_P.x, 1.0, r);
                    heat = 1.0 - r;
                }
                else
                {
                    // 권영: 마디 있는 둥근 머리 + 뒤로 가늘어지는 결
                    float xf = p.x - 0.07 * abs(cos(p.y * 7.5));
                    float w = xf < 0.25
                        ? 0.62 * smoothstep(-1.05, 0.25, xf)
                        : 0.62 * sqrt(max(0.0, 1.0 - pow((xf - 0.25) / 0.55, 2.0)));
                    float e = abs(p.y) / (w + 1e-4);
                    float body = (1.0 - smoothstep(0.72, 1.0, e)) * step(xf, 0.8);
                    float st = n2(float2(p.x * 0.3 - _FxTime * 2.5 + _Seed, p.y * 3.2 + _Seed));
                    float head = smoothstep(-0.25, 0.2, p.x);
                    float er = n1(p * 0.9 + _Seed);
                    a = body * lerp(smoothstep(0.38, 0.62, st) * 0.85, 1.0, head) * smoothstep(_Diss, _Diss + 0.2, er + 0.02) * step(_Diss, 0.999);
                    heat = saturate(head * (1.0 - e * e) * (0.55 + 0.45 * smoothstep(0.0, 0.7, p.x)));
                }

                float3 col = heat < 0.5 ? lerp(_C0.rgb, _C1.rgb, heat * 2.0) : lerp(_C1.rgb, _C2.rgb, (heat - 0.5) * 2.0);
                a *= _Alpha;
                return half4(col * a, a * (1.0 - _Add));
            }
            ENDHLSL
        }
    }
}
