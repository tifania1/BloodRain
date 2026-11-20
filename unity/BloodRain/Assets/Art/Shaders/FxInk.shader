// 무공 이펙트 통합 셰이더(VF-01U, 2026-11-13). 시안 web/lookdev/sects.html 의 FS 를 그대로 옮긴 것.
// 텍스처 한 장을 띄우는 대신 모양을 식으로 그리고, 흐르는 잡음으로 가장자리를 침식하고,
// 색 램프(가장자리 → 가운데 → 심)로 칠하고, 긋기(_Prog)와 흩어짐(_Diss)으로 움직인다.
// 모양(_Shape): 0 호(검·봉 궤적) · 1 줄기(찌르기·낙하 검기·섬광) · 2 충격 고리 · 3 터짐·얼룩 · 4 번개 가지 · 5 방울·그림자 · 7 권영 · 10 태극 · 11 꽃잎 · 12 부적 · 13 꽃잎 로브 얼룩 · 14 그을음 · 15 쏠린 방울 튐 · 16 젖은 자국 · 17 말리는 물보라(VF-19)
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
        _Hdr ("더하기 겹 HDR 배수(PP-01U Bloom)", Float) = 1
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
            #pragma multi_compile_instancing   // PERF-01U: SectFx 가 장마다 따로 그리지 않고 한 번에(RenderMeshInstanced)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _NoiseTex_ST;
                float _FxTime;
            CBUFFER_END
            // 장마다 다른 값(PERF-01U). 인스턴싱이 꺼진 변형에서는 그냥 상수 버퍼라 예전처럼 MaterialPropertyBlock 한 값으로 읽힌다
            UNITY_INSTANCING_BUFFER_START(FxProps)
                UNITY_DEFINE_INSTANCED_PROP(float4, _P)
                UNITY_DEFINE_INSTANCED_PROP(float4, _C0)
                UNITY_DEFINE_INSTANCED_PROP(float4, _C1)
                UNITY_DEFINE_INSTANCED_PROP(float4, _C2)
                UNITY_DEFINE_INSTANCED_PROP(float, _Shape)
                UNITY_DEFINE_INSTANCED_PROP(float, _Prog)
                UNITY_DEFINE_INSTANCED_PROP(float, _Diss)
                UNITY_DEFINE_INSTANCED_PROP(float, _Seed)
                UNITY_DEFINE_INSTANCED_PROP(float, _Alpha)
                UNITY_DEFINE_INSTANCED_PROP(float, _Add)
                UNITY_DEFINE_INSTANCED_PROP(float, _Hdr)
            UNITY_INSTANCING_BUFFER_END(FxProps)
            #define I_P UNITY_ACCESS_INSTANCED_PROP(FxProps, _P)
            #define I_C0 UNITY_ACCESS_INSTANCED_PROP(FxProps, _C0)
            #define I_C1 UNITY_ACCESS_INSTANCED_PROP(FxProps, _C1)
            #define I_C2 UNITY_ACCESS_INSTANCED_PROP(FxProps, _C2)
            #define I_Shape UNITY_ACCESS_INSTANCED_PROP(FxProps, _Shape)
            #define I_Prog UNITY_ACCESS_INSTANCED_PROP(FxProps, _Prog)
            #define I_Diss UNITY_ACCESS_INSTANCED_PROP(FxProps, _Diss)
            #define I_Seed UNITY_ACCESS_INSTANCED_PROP(FxProps, _Seed)
            #define I_Alpha UNITY_ACCESS_INSTANCED_PROP(FxProps, _Alpha)
            #define I_Add UNITY_ACCESS_INSTANCED_PROP(FxProps, _Add)
            #define I_Hdr UNITY_ACCESS_INSTANCED_PROP(FxProps, _Hdr)

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            float n1(float2 p) { return SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, p).r; }
            float n2(float2 p) { return SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, p).g; }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float2 v = i.uv;
                float2 p = (v - 0.5) * 2.0;
                float r = length(p);
                float ang = atan2(p.y, p.x);
                float a = 0.0, heat = 0.0;

                if (I_Shape < 0.5)
                {
                    // 호. I_P: 펼침(rad), 굵기, 꼬리(온전히 남는 길이 0~1), 거칠기. 펼침이 한 바퀴(> 6)면 머리에서 꼬리로 가늘어진다(도는 검기).
                    float R = 0.78;
                    float t = ang / I_P.x + 0.5;
                    float h = I_Prog * 1.12;
                    float inArc = step(0.0, t) * step(t, 1.0);
                    float bh0 = max(h - t, 0.0);
                    float tap = I_P.x > 6.0
                        ? (0.3 + 0.7 * exp(-bh0 / max(I_P.z, 0.02) * 1.1)) * smoothstep(0.0, 0.04, I_Prog)
                        : pow(max(sin(PI * saturate(t)), 0.0), 0.55);
                    float w = I_P.y * tap * R + 1e-4;
                    float x = (R - r) / w + (n1(float2(t * 1.3 + I_Seed, 0.37)) - 0.5) * 0.3 * I_P.w;
                    float body = smoothstep(0.0, 0.07, x) * (1.0 - smoothstep(0.62, 1.0, x));
                    float bh = h - t;
                    float sweep = smoothstep(0.0, 0.03, bh);
                    float streak = n2(float2(t * 0.9 + I_Seed * 1.7, x * 2.2 + I_Seed));
                    float grain = n1(float2(ang * 0.8 + I_Seed, r * 3.0 - _FxTime * 0.6));
                    float th = I_Diss * 1.2 + max(bh - I_P.z, 0.0) * (I_P.x > 6.0 ? 3.2 : 1.5) + I_P.w * (0.3 * (1.0 - streak) + 0.55 * x * x) - 0.16;
                    a = inArc * body * sweep * smoothstep(th, th + 0.18, lerp(streak, grain, 0.4));
                    heat = saturate((1.0 - smoothstep(0.05, 0.45, x)) * 0.7 + exp(-max(bh, 0.0) * 9.0) * 0.6);
                }
                else if (I_Shape < 1.5)
                {
                    // 줄기. I_P: 굵기, 거칠기, 흐름 속도, 꼬리
                    float x = v.x;
                    float h = I_Prog * 1.1;
                    float w = I_P.x * pow(max(x, 0.0), 0.6) * (1.0 - smoothstep(0.8, 1.0, x)) + 1e-4;
                    float e = abs(p.y) / w;
                    float body = 1.0 - smoothstep(0.5, 1.0, e);
                    float bh = h - x;
                    float streak = n2(float2(x * 0.8 - _FxTime * I_P.z + I_Seed, p.y * 1.5 + I_Seed));
                    float th = I_Diss * 1.2 + I_P.y * 0.4 * (1.0 - streak) - 0.12 + max(bh - I_P.w, 0.0) * 1.4;
                    a = body * smoothstep(0.0, 0.04, bh) * smoothstep(th, th + 0.2, streak);
                    heat = saturate((1.0 - e) * 0.8 + exp(-max(bh, 0.0) * 8.0) * 0.5);
                }
                else if (I_Shape < 2.5)
                {
                    // 충격 고리. I_P: 반지름, 두께, 잡음
                    float d = (r - I_P.x) / I_P.y;
                    float band = exp(-d * d);
                    float nz = n1(float2(ang * 0.5 + I_Seed, r * 1.5 - _FxTime * 0.3));
                    a = band * lerp(1.0, smoothstep(0.25, 0.75, nz), I_P.z) * (1.0 - I_Diss);
                    heat = band;
                }
                else if (I_Shape < 3.5)
                {
                    // 터짐·얼룩. I_P: 가시 수, 가시 세기, 반지름
                    float sp = n1(float2(ang * I_P.x * 0.159 + I_Seed, I_Seed * 0.3));
                    float len = I_P.z * (1.0 + I_P.y * (sp - 0.5) * 2.0);
                    float body = 1.0 - smoothstep(len * 0.55, len, r);
                    float er = n2(p * 0.8 + I_Seed);
                    a = body * smoothstep(I_Diss, I_Diss + 0.15, er + 0.02) * step(I_Diss, 0.999);
                    heat = saturate(1.0 - r / len);
                }
                else if (I_Shape < 4.5)
                {
                    // 번개 가지. I_P: 꺾임, 굵기, 가지
                    float x = v.x;
                    float env = sin(PI * x);
                    float tq = floor(_FxTime * 24.0) * 0.37;
                    float off = ((n1(float2(x * 1.7 + I_Seed + tq, I_Seed)) - 0.5) * 2.0 + (n2(float2(x * 6.0 + tq, I_Seed * 2.0)) - 0.5) * 0.6) * I_P.x * env;
                    float d = abs(p.y - off);
                    float core = I_P.y / (d + I_P.y * 0.5);
                    float bx = saturate((x - 0.35) / 0.5);
                    float d2 = abs(p.y - off - bx * (n2(float2(x * 2.3 + I_Seed * 3.0, tq)) - 0.3) * I_P.z);
                    float c2 = step(0.35, x) * step(x, 0.85) * (I_P.y * 0.6 / (d2 + I_P.y * 0.5));
                    a = saturate(core * core * 0.25 + c2 * c2 * 0.2) * step(x, I_Prog * 1.2) * (1.0 - I_Diss);
                    heat = saturate(core * 0.5);
                }
                else if (I_Shape < 5.5)
                {
                    // 방울·불씨·그림자. I_P.x: 단단함
                    a = 1.0 - smoothstep(I_P.x, 1.0, r);
                    heat = 1.0 - r;
                }
                else if (I_Shape < 7.5)
                {
                    // 권영: 마디 있는 둥근 머리 + 뒤로 가늘어지는 결
                    float xf = p.x - 0.07 * abs(cos(p.y * 7.5));
                    float w = xf < 0.25
                        ? 0.62 * smoothstep(-1.05, 0.25, xf)
                        : 0.62 * sqrt(max(0.0, 1.0 - pow((xf - 0.25) / 0.55, 2.0)));
                    float e = abs(p.y) / (w + 1e-4);
                    float body = (1.0 - smoothstep(0.72, 1.0, e)) * step(xf, 0.8);
                    float st = n2(float2(p.x * 0.3 - _FxTime * 2.5 + I_Seed, p.y * 3.2 + I_Seed));
                    float head = smoothstep(-0.25, 0.2, p.x);
                    float er = n1(p * 0.9 + I_Seed);
                    a = body * lerp(smoothstep(0.38, 0.62, st) * 0.85, 1.0, head) * smoothstep(I_Diss, I_Diss + 0.2, er + 0.02) * step(I_Diss, 0.999);
                    heat = saturate(head * (1.0 - e * e) * (0.55 + 0.45 * smoothstep(0.0, 0.7, p.x)));
                }
                else if (I_Shape < 10.5)
                {
                    // 태극(VF-09): 흑백 두 색. heat 0 → I_C0(검), 1 → I_C2(흰). I_P.x: 가장자리 잡음
                    float d1 = length(p - float2(0.0, 0.5)), d2 = length(p + float2(0.0, 0.5));
                    float s = p.x > 0.0 ? 1.0 : 0.0;
                    if (d1 < 0.5) s = 0.0;
                    if (d2 < 0.5) s = 1.0;
                    if (d1 < 0.13) s = 1.0;
                    if (d2 < 0.13) s = 0.0;
                    float er = n1(float2(ang * 0.6 + I_Seed, r * 2.0 - _FxTime * 0.2));
                    a = (1.0 - smoothstep(0.86, 1.0, r + (er - 0.5) * I_P.x)) * (1.0 - I_Diss);
                    heat = s;
                }
                else if (I_Shape < 11.5)
                {
                    // 꽃잎(VF-09): 끝이 살짝 파인 물방울
                    float qx = p.x + 0.1;
                    float w = 0.62 * sqrt(max(0.0, 1.0 - qx * qx)) * (1.0 - 0.3 * smoothstep(0.4, 1.0, qx)) + 1e-4;
                    float e = abs(p.y) / w;
                    float notch = 1.0 - step(0.78, qx) * (1.0 - smoothstep(0.03, 0.14, abs(p.y)));
                    a = (1.0 - smoothstep(0.7, 1.0, e)) * step(abs(qx), 1.0) * notch;
                    heat = saturate(1.0 - e * 0.7 - max(qx, 0.0) * 0.25);
                }
                else if (I_Shape < 12.5)
                {
                    // 부적(VF-09): 누런 종이 띠, 가장자리 침식, 가운데 세로 먹 획과 위 먹 점. 먹은 heat 0(I_C0)
                    float er = n1(p * 0.7 + I_Seed);
                    float m = max(abs(p.x), abs(p.y));
                    a = (1.0 - smoothstep(0.84, 0.98, m + (er - 0.5) * 0.14)) * smoothstep(I_Diss, I_Diss + 0.15, er + 0.02) * step(I_Diss, 0.999);
                    float ink = max((1.0 - smoothstep(0.07, 0.15, abs(p.x + 0.12 * sin(p.y * 8.0 + I_Seed)))) * step(abs(p.y), 0.62),
                                    1.0 - smoothstep(0.1, 0.17, length(p - float2(0.0, 0.74))));
                    heat = ink > 0.5 ? 0.0 : 0.55 + 0.35 * er;
                }
                else if (I_Shape < 13.5)
                {
                    // 꽃잎 로브 얼룩(VF-19, 당가 독 꽃). I_P: 로브 수, 로브 세기, 반지름, 심 어둡기 — 가장자리에 물감이 고이고 심이 짙다
                    float lob = 0.5 + 0.5 * cos(ang * I_P.x + I_Seed * 2.0);
                    float jag = n1(float2(ang * 0.6 + I_Seed, I_Seed * 0.7));
                    float len = I_P.z * (1.0 - I_P.y + I_P.y * pow(lob, 0.7)) * (0.9 + 0.2 * jag);
                    float e = r / len;
                    float er = n2(p * 0.9 + I_Seed);
                    a = min(1.0, (1.0 - smoothstep(0.82, 1.0, e)) * (0.55 + 0.45 * smoothstep(0.55, 0.95, e) + I_P.w * (1.0 - smoothstep(0.0, 0.3, e))))
                        * smoothstep(I_Diss, I_Diss + 0.15, er + 0.02) * step(I_Diss, 0.999);
                    heat = saturate(I_P.w * (1.0 - smoothstep(0.0, 0.35, e)) + 0.35 * smoothstep(0.6, 0.95, e));
                }
                else if (I_Shape < 14.5)
                {
                    // 그을음(VF-19, 곤륜·남궁): 가운데 검고 가는 금이 방사로 뻗음. I_P: 금 수, 금 길이, 심 반지름, 거칠기
                    float er = n1(float2(ang * 0.8 + I_Seed, r * 2.0));
                    float core = 1.0 - smoothstep(I_P.z * 0.6, I_P.z * (1.0 + 0.35 * (er - 0.5) * I_P.w * 2.0), r);
                    float seg = ang / (2.0 * PI) * I_P.x + I_Seed;
                    float id = floor(seg);
                    float fr = frac(seg) - 0.5;
                    float jit = n2(float2(id * 0.173 + I_Seed, 0.5));
                    float wob = (n1(float2(r * 2.5 + id * 0.31, id * 0.7 + I_Seed)) - 0.5) * 0.5;
                    float cl = I_P.z + I_P.y * (0.4 + 0.6 * jit);
                    float dA = abs(fr + wob * 0.3 - (jit - 0.5) * 0.4) * r * 2.0 * PI / I_P.x;
                    float w = 0.035 * (1.0 - smoothstep(I_P.z * 0.5, cl, r)) + 0.004;
                    float crack = (1.0 - smoothstep(w * 0.5, w, dA)) * step(r, cl) * step(I_P.z * 0.5, r);
                    float er2 = n2(p * 0.8 + I_Seed);
                    a = max(core * 0.9, crack) * smoothstep(I_Diss, I_Diss + 0.15, er2 + 0.02) * step(I_Diss, 0.999);
                    heat = saturate(core * (1.0 - 0.5 * r / I_P.z) + crack * 0.7);
                }
                else if (I_Shape < 15.5)
                {
                    // 한쪽(+x)으로 쏠린 방울 튐(VF-19, 개방 술 방울): 큰 방울 몇에 잔 방울, 뒤로 꼬리. I_P: 방울 수(16까지), 퍼짐, 꼬리, 큰 방울 수
                    float acc = 0.0, hh = 0.0;
                    [loop] for (int k = 0; k < 16; k++)
                    {
                        float fi = (float)k;
                        if (fi >= I_P.x) break;
                        float h1 = n1(float2(fi * 0.381 + I_Seed * 0.13, fi * 0.727 + 0.23));
                        float h2 = n2(float2(fi * 0.553 + I_Seed * 0.7, fi * 0.297 + 0.61));
                        float h3 = n1(float2(fi * 0.619 + 0.77, fi * 0.443 + I_Seed * 0.3));
                        float big = step(fi, I_P.w - 0.5);
                        float dx = lerp(-0.45, 0.85, pow(h1, 0.8));
                        float2 d = p - float2(dx, (h2 - 0.5) * 2.0 * I_P.y * (0.3 + 0.7 * (dx + 0.35) / 1.15));
                        float rad = big > 0.0 ? lerp(0.15, 0.22, h3) : lerp(0.04, 0.09, h3);
                        float Lt = rad + I_P.z * rad * 4.0;
                        float ex = d.x < 0.0 ? -d.x / Lt : 0.0;
                        float wd = rad * (1.0 - ex * 0.9);
                        float dd = d.x < 0.0 ? (ex > 1.0 ? 9.0 : abs(d.y) / max(wd, 1e-4)) : length(d) / rad;
                        float m = 1.0 - smoothstep(0.75, 1.0, dd);
                        acc = max(acc, m);
                        hh = max(hh, m * (big > 0.0 ? 1.0 : 0.6) * (1.0 - ex * 0.6));
                    }
                    float er = n2(p * 0.9 + I_Seed);
                    a = acc * smoothstep(I_Diss, I_Diss + 0.15, er + 0.02) * step(I_Diss, 0.999);
                    heat = hh;
                }
                else if (I_Shape < 16.5)
                {
                    // 젖은 자국(VF-19, 수로채): 고른 담묵에 가장자리만 조금 진함. I_P: 반지름, 가장자리 진하기, 잡음
                    float er = n1(float2(ang * 0.7 + I_Seed, I_Seed * 0.5));
                    float er3 = n2(p * 1.6 + I_Seed);
                    float len = I_P.x * (1.0 + (er - 0.5) * I_P.z + (er3 - 0.5) * I_P.z * 0.4);
                    float e = r / len;
                    float rim = smoothstep(0.7, 0.97, e) * (1.0 - smoothstep(0.97, 1.02, e));
                    float er2 = n2(p * 0.8 + I_Seed + 3.1);
                    a = (1.0 - smoothstep(0.9, 1.0, e)) * (0.45 + I_P.y * rim) * smoothstep(I_Diss, I_Diss + 0.15, er2 + 0.02) * step(I_Diss, 0.999);
                    heat = 0.25 + 0.6 * rim;
                }
                else
                {
                    // 위로 말리는 물보라(VF-19, 수로채 처치): 왼쪽 아래에서 솟아 오른쪽으로 말려 끝이 거품으로 부서짐. I_P: 굵기, 말림, 거품
                    float2 q = p - float2(0.08, -0.15);
                    float th = atan2(q.y, q.x);
                    if (th < -0.5 * PI) th += 2.0 * PI;
                    float t = (1.05 * PI - th) / (1.15 * PI);
                    float Rt = 0.66 * (1.0 - I_P.y * t);
                    float d = abs(length(q) - Rt);
                    float w = I_P.x * (1.0 - t * 0.7) * smoothstep(0.0, 0.12, t) + 1e-4;
                    float body = (1.0 - smoothstep(0.55, 1.0, d / w)) * step(0.0, t) * step(t, 1.0);
                    float fz = n1(float2(t * 3.0 + I_Seed, length(q) * 4.0 - _FxTime * 0.8));
                    float er = n2(p * 0.9 + I_Seed);
                    a = body * lerp(1.0, smoothstep(0.42, 0.6, fz), smoothstep(0.55, 0.95, t) * I_P.z) * step(t, I_Prog * 1.1)
                        * smoothstep(I_Diss, I_Diss + 0.15, er + 0.02) * step(I_Diss, 0.999);
                    heat = saturate(smoothstep(0.45, 1.0, t) * 0.8 + (1.0 - d / w) * 0.3);
                }

                float3 col = heat < 0.5 ? lerp(I_C0.rgb, I_C1.rgb, heat * 2.0) : lerp(I_C1.rgb, I_C2.rgb, (heat - 0.5) * 2.0);
                a *= I_Alpha;
                // 더하기 겹만 HDR 로 밀어 Bloom 문턱(1)을 넘긴다(PP-01U). 몸 겹·먹은 1 이하 그대로
                return half4(col * a * lerp(1.0, max(I_Hdr, 1.0), I_Add), a * (1.0 - I_Add));
            }
            ENDHLSL
        }
    }
}
