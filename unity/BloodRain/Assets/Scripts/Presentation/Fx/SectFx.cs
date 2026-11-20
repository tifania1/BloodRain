using System;
using System.Collections.Generic;
using UnityEngine;

namespace BloodRain.Presentation.Fx
{
    /// <summary>색 램프: 가장자리 → 가운데 → 심. FxInk 셰이더가 열(heat)에 따라 섞는다.</summary>
    public readonly struct FxRamp
    {
        public readonly Color Edge, Mid, Core;
        public FxRamp(Color edge, Color mid, Color core) { Edge = edge; Mid = mid; Core = core; }
        public static FxRamp Flat(Color c) => new FxRamp(c, c, c);
        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        // 값은 시안(web/lookdev/sects.html)과 같다. 밝은 종이 위에서 읽히게 가장자리를 짙게 잡았다.
        public static readonly FxRamp Ink = new FxRamp(C(.13f, .12f, .11f), C(.06f, .06f, .06f), C(.02f, .02f, .02f));
        public static readonly FxRamp Wash = new FxRamp(C(.3f, .52f, .5f), C(.24f, .46f, .44f), C(.18f, .38f, .37f));
        public static readonly FxRamp Gold = new FxRamp(C(.56f, .4f, .05f), C(.98f, .74f, .16f), C(1f, .96f, .72f));   // 2026-11-17 가장자리를 주황에서 황토로(주홍으로 읽히지 않게)
        public static readonly FxRamp Brown = new FxRamp(C(.42f, .3f, .18f), C(.24f, .15f, .07f), C(.1f, .06f, .03f));
        public static readonly FxRamp Amber = new FxRamp(C(.55f, .2f, .02f), C(.93f, .5f, .08f), C(1f, .85f, .45f));
        public static readonly FxRamp Blue = new FxRamp(C(.05f, .15f, .6f), C(.2f, .5f, .96f), C(.9f, .97f, 1f));
        public static readonly FxRamp BlueInk = new FxRamp(C(.1f, .14f, .25f), C(.04f, .06f, .13f), C(.02f, .03f, .07f));
        public static readonly FxRamp Dust = new FxRamp(C(.55f, .47f, .35f), C(.5f, .42f, .3f), C(.45f, .37f, .26f));
        public static readonly FxRamp White = Flat(Color.white);
        // 2026-11-16 '더 세게': 날빛(4단), 달아오른 심, 흰 푸른 날, 술 담채
        public static readonly FxRamp Blade = new FxRamp(C(.16f, .42f, .46f), C(.62f, .9f, .9f), C(1f, 1f, 1f));
        public static readonly FxRamp Hot = new FxRamp(C(.96f, .6f, .1f), C(1f, .9f, .5f), C(1f, 1f, .95f));
        public static readonly FxRamp BlueHot = new FxRamp(C(.3f, .6f, 1f), C(.8f, .93f, 1f), C(1f, 1f, 1f));
        public static readonly FxRamp Wine = new FxRamp(C(.62f, .42f, .16f), C(.56f, .34f, .1f), C(.46f, .26f, .07f));
        // 2026-11-17 나머지 무공(VF-02U). 값은 시안 SILVER … MISTG 그대로
        public static readonly FxRamp Silver = new FxRamp(C(.42f, .47f, .52f), C(.8f, .85f, .9f), C(1f, 1f, 1f));
        public static readonly FxRamp Poison = new FxRamp(C(.12f, .34f, .12f), C(.36f, .68f, .24f), C(.78f, .96f, .55f));
        public static readonly FxRamp PoisonInk = new FxRamp(C(.16f, .28f, .12f), C(.1f, .2f, .08f), C(.05f, .12f, .04f));
        public static readonly FxRamp Water = new FxRamp(C(.08f, .34f, .38f), C(.3f, .66f, .68f), C(.86f, .98f, .98f));
        public static readonly FxRamp WaterInk = new FxRamp(C(.1f, .26f, .28f), C(.06f, .18f, .2f), C(.03f, .1f, .12f));
        public static readonly FxRamp Taiji = new FxRamp(C(.04f, .04f, .04f), C(.5f, .49f, .46f), C(.97f, .96f, .92f));
        public static readonly FxRamp Pale = new FxRamp(C(.7f, .69f, .65f), C(.93f, .92f, .88f), C(1f, 1f, .98f));
        public static readonly FxRamp Petal = new FxRamp(C(.74f, .47f, .56f), C(.93f, .76f, .81f), C(1f, .95f, .96f));
        public static readonly FxRamp Talis = new FxRamp(C(.12f, .08f, .04f), C(.8f, .64f, .24f), C(.97f, .88f, .52f));
        public static readonly FxRamp Thunder = new FxRamp(C(.45f, .6f, .95f), C(.85f, .93f, 1f), C(1f, 1f, 1f));
        public static readonly FxRamp Violet = new FxRamp(C(.34f, .09f, .22f), C(.21f, .04f, .13f), C(.1f, .02f, .06f));
        public static readonly FxRamp VGlow = new FxRamp(C(.45f, .1f, .3f), C(.66f, .3f, .48f), C(.93f, .74f, .84f));
        public static readonly FxRamp Palm = new FxRamp(C(.3f, .4f, .46f), C(.6f, .7f, .75f), C(.9f, .95f, .96f));
        public static readonly FxRamp Finger = new FxRamp(C(.7f, .56f, .24f), C(.98f, .9f, .6f), C(1f, 1f, .96f));
        public static readonly FxRamp Iron = new FxRamp(C(.2f, .2f, .22f), C(.1f, .1f, .11f), C(.03f, .03f, .04f));
        public static readonly FxRamp Soot = new FxRamp(C(.32f, .22f, .13f), C(.18f, .11f, .06f), C(.07f, .05f, .03f));
        public static readonly FxRamp MistG = new FxRamp(C(.5f, .64f, .46f), C(.64f, .78f, .56f), C(.84f, .92f, .76f));
        public static readonly FxRamp BloodMist = new FxRamp(C(.42f, .16f, .3f), C(.36f, .11f, .24f), C(.28f, .06f, .17f));
        public static readonly FxRamp Fire = new FxRamp(C(1f, .85f, .5f), C(1f, .95f, .75f), C(1f, 1f, .95f));
    }

    /// <summary>
    /// 문파 무공 이펙트(VF-01U, 2026-11-13). 텍스처 한 장을 띄우던 것을 궤적·기·타격·잔흔 겹으로 그린다.
    /// 시안 web/lookdev/sects.html 의 swing·punch·orbit·skySword·ring·bolt·drops 를 그대로 옮겼다(값도 같다).
    /// 2026-11-16 사람이 '더 세게, 단계도 더 나누고': 무공마다 stage(1~5, Core WeaponGrowth.VisualStage)를 받아 겹과 세기를 올린다.
    /// 단계별 값은 시안의 L([다섯 칸]) 그대로. 휘두르는 횟수·범위 같은 규칙은 여전히 데이터가 정하고 여기는 겉만 바꾼다.
    /// 여기는 그림만 그린다. 피해 판정은 WeaponRunner 가 그대로 한다.
    /// 그리기는 WeaponRunner 의 붓 획과 같은 방식(Graphics.RenderMesh + z 순서)이다.
    /// </summary>
    [DefaultExecutionOrder(200)]   // WeaponRunner 가 그 프레임의 탄(Proj)을 넘긴 뒤에 그린다
    public sealed partial class SectFx : MonoBehaviour
    {
        public static SectFx Instance { get; private set; }

        [SerializeField] Material material;   // BloodRain/FxInk. 비어 있으면 Resources/Fx/FxInk 를 읽는다

        sealed class Fx
        {
            public float T, Life, Seed;
            public bool Under;
            public Action<Fx, float> Draw;
        }
        // Shape 가 0 이 아니면 그 모양 조각(11 꽃잎)이 Rot + T × Spin 으로 돌며 흩날린다
        struct Drop { public Vector2 Pos, Vel; public float T, Life, Size, Rot, Spin; public FxRamp C; public bool Glow; public int Shape; }
        // Shape 0 이면 예전 먹 터짐(모양 3). 아니면 그 모양·값·색·알파로(VF-19 처치 바닥 자국)
        struct Stain { public Vector2 Pos; public float T, Seed, Rot, Size, A; public int Shape; public Vector4 P; public FxRamp C; }
        struct Later { public float T; public Action F; }

        readonly List<Fx> _fx = new List<Fx>();
        readonly List<Drop> _drops = new List<Drop>();
        readonly List<Stain> _stains = new List<Stain>();
        readonly List<Later> _later = new List<Later>();
        readonly System.Random _rng = new System.Random(11);
        MaterialPropertyBlock _mpb;
        Mesh _quad;
        Texture2D _noise;
        float _time, _lastFlash = -1f, _lastKillBurst = -1f, _z;
        RenderParams _rp;

        const float StainLife = 6f;
        const float ZUnder = 0.02f;     // 캐릭터(z 0) 뒤
        const float ZOver = -0.3f;      // 붓 획(-0.26~-0.28)보다 카메라 쪽
        const int MaxDrops = 220, MaxStains = 48; // 튐 400 → 220(2026-11-20 끊김: 장마다 그리기 한 번이라 많으면 느려진다)

        static readonly int IdNoise = Shader.PropertyToID("_NoiseTex"), IdShape = Shader.PropertyToID("_Shape"), IdP = Shader.PropertyToID("_P"),
            IdProg = Shader.PropertyToID("_Prog"), IdDiss = Shader.PropertyToID("_Diss"), IdSeed = Shader.PropertyToID("_Seed"),
            IdTime = Shader.PropertyToID("_FxTime"), IdAlpha = Shader.PropertyToID("_Alpha"), IdAdd = Shader.PropertyToID("_Add"), IdHdr = Shader.PropertyToID("_Hdr"),
            IdC0 = Shader.PropertyToID("_C0"), IdC1 = Shader.PropertyToID("_C1"), IdC2 = Shader.PropertyToID("_C2");

        /// <summary>월드 단위로 1m. 경지가 오르면 세상이 작아지니 WeaponRunner 가 프레임마다 넣는다(장식 크기·속도에 곱한다).</summary>
        [NonSerialized] public float Meter = 1f;

        public int LiveCount => _fx.Count;
        public bool Ready => material != null;

        void Awake()
        {
            Instance = this;
            if (material == null) material = Resources.Load<Material>("Fx/FxInk");
            if (material == null)
            {
                var sh = Shader.Find("BloodRain/FxInk");
                if (sh != null) material = new Material(sh);
            }
            _mpb = new MaterialPropertyBlock();
            _quad = MakeQuad();
            _noise = MakeNoise();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_noise != null) Destroy(_noise);
        }

        float Rnd(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

        // ── 시계: WeaponRunner 가 제 dt 로 부른다(히트스톱·일시정지와 같이 멈추게) ─────────────
        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            _time += dt;
            for (int i = _later.Count - 1; i >= 0; i--)
            {
                var l = _later[i];
                l.T -= dt;
                if (l.T <= 0f) { _later.RemoveAt(i); l.F(); }
                else _later[i] = l;
            }
            for (int i = _fx.Count - 1; i >= 0; i--)
            {
                _fx[i].T += dt;
                if (_fx[i].T >= _fx[i].Life) _fx.RemoveAt(i);
            }
            float drag = Mathf.Pow(0.02f, dt);
            for (int i = _drops.Count - 1; i >= 0; i--)
            {
                var d = _drops[i];
                d.T += dt;
                if (d.T >= d.Life) { _drops.RemoveAt(i); continue; }
                d.Pos += d.Vel * dt;
                d.Vel *= drag;
                _drops[i] = d;
            }
            for (int i = _stains.Count - 1; i >= 0; i--)
            {
                var s = _stains[i];
                s.T += dt;
                if (s.T >= StainLife) _stains.RemoveAt(i);
                else _stains[i] = s;
            }
        }

        /// <summary>시험용 성능 기록: 지난 프레임에 그린 장 수(Graphics.RenderMesh 한 번 = 한 장), 살아 있는 효과·튐 수.</summary>
        public static int QuadsLastFrame;
        static int _quadCount;
        public int FxCount => _fx.Count;
        /// <summary>바쁜 프레임: 지난 프레임에 그린 장이 이보다 많으면 빛의 더하기 겹을 빼고 튐을 줄인다(2026-11-20 사람: 갈수록 뚝뚝 끊긴다.
        /// 한 장 = Graphics.RenderMesh 한 번이라 후반 600장 넘게 쌓이면 웹에서 특히 느리다).</summary>
        public const int BusyQuads = 300;
        static bool Busy => QuadsLastFrame > BusyQuads;
        public int DropCount => _drops.Count;

        void LateUpdate()
        {
            QuadsLastFrame = _quadCount; _quadCount = 0;
            if (material == null) return;
            _rp = new RenderParams(material) { matProps = _mpb, layer = gameObject.layer, worldBounds = new Bounds(Vector3.zero, Vector3.one * 1e7f) };
            _z = ZUnder;
            // 큰 타격에 종이가 잠깐 어두워져 빛이 산다(HitFeel.Jolt). 바닥 위·캐릭터 밑에 화면을 덮는 먹 한 장
            float dim = HitFeel.DimAlpha;
            var cam = dim > .002f ? Camera.main : null;
            if (cam != null)
            {
                float span = cam.orthographicSize * 2f * Mathf.Max(1f, cam.aspect) * 3f;
                Quad(5, cam.transform.position, span, span, 0f, new Vector4(.98f, 0f, 0f, 0f), FxRamp.Ink, dim);
            }
            foreach (var s in _stains)
            {
                float k = s.T / StainLife;
                if (s.Shape == 0) Quad(3, s.Pos, s.Size * 2f, s.Size * 1.4f, s.Rot, new Vector4(3f, .35f, .8f, 0f), FxRamp.Ink, .26f * (1f - k) * (1f - k), 1f, .12f + k * .5f, s.Seed);
                else Quad(s.Shape, s.Pos, s.Size * 2f, s.Size * 1.4f, s.Rot, s.P, s.C, s.A * (1f - k) * (1f - k), 1f, .12f + k * .5f, s.Seed);
            }
            foreach (var f in _fx) if (f.Under) f.Draw(f, f.T / f.Life);
            _z = ZOver;
            foreach (var f in _fx) if (!f.Under) f.Draw(f, f.T / f.Life);
            DrawLive();
            foreach (var d in _drops)
            {
                float k = d.T / d.Life, sp = d.Vel.magnitude / Mathf.Max(Meter, 1e-6f);
                if (d.Shape != 0)
                {
                    // 꽃잎처럼 밝은 조각은 먹 그림자를 한 겹(종이 바닥에 묻혔다, 2026-11-20). 바쁜 프레임엔 뺀다
                    if (!Busy && IsLight(d.C)) Quad(d.Shape, d.Pos + new Vector2(.03f, -.03f) * Meter, d.Size * 2.3f, d.Size * 1.4f, d.Rot + d.T * d.Spin, default, FxRamp.Ink, .45f * (1f - k * k));
                    Quad(d.Shape, d.Pos, d.Size * 2f, d.Size * 1.15f, d.Rot + d.T * d.Spin, default, d.C, .95f * (1f - k * k)); continue;
                }
                float rot = Mathf.Atan2(d.Vel.y, d.Vel.x);
                var p = new Vector4(d.Glow ? .3f : .55f, 0f, 0f, 0f);
                float al = .92f * (1f - k * k);
                if (d.Glow) Glow(5, d.Pos, d.Size * (1f + sp * .09f), d.Size * (1f - k * .5f), rot, p, d.C, al);
                else Quad(5, d.Pos, d.Size * (1f + sp * .09f), d.Size * (1f - k * .5f), rot, p, d.C, al);
            }
        }

        // ── 한 장 그리기 ─────────────────────────────────────────────
        /// <summary>FxInk 사각형 한 장. w·h 는 월드 크기(음수면 뒤집힘), rot 은 라디안.</summary>
        public void Quad(int shape, Vector2 pos, float w, float h, float rot, Vector4 p, FxRamp c,
            float alpha = 1f, float prog = 1f, float diss = 0f, float seed = 0f, bool add = false)
        {
            _mpb.Clear();
            _mpb.SetTexture(IdNoise, _noise);
            _mpb.SetFloat(IdShape, shape);
            _mpb.SetVector(IdP, p);
            _mpb.SetFloat(IdProg, prog);
            _mpb.SetFloat(IdDiss, diss);
            _mpb.SetFloat(IdSeed, seed);
            _mpb.SetFloat(IdTime, _time);
            _mpb.SetFloat(IdAlpha, alpha);
            _mpb.SetFloat(IdAdd, add ? 1f : 0f);
            _mpb.SetFloat(IdHdr, add ? GlowHdr : 1f);
            _mpb.SetColor(IdC0, c.Edge);
            _mpb.SetColor(IdC1, c.Mid);
            _mpb.SetColor(IdC2, c.Core);
            _z -= 0.0002f;   // 부른 순서대로 위에 쌓인다
            _quadCount++;
            var m = Matrix4x4.TRS(new Vector3(pos.x, pos.y, _z), Quaternion.Euler(0f, 0f, rot * Mathf.Rad2Deg), new Vector3(w, h, 1f));
            Graphics.RenderMesh(_rp, _quad, 0, m);
        }

        /// <summary>더하기 겹의 HDR 배수(PP-01U). Bloom 문턱 1 을 기·금빛만 넘게 한다. 1 이면 예전과 같다.</summary>
        public static float GlowHdr = 1.8f;

        /// <summary>종이 바닥(밝기 .7 안팎)에 묻히는 밝은 램프인가: 가운데 색의 밝기가 .65 를 넘으면.</summary>
        static bool IsLight(FxRamp c) => .2126f * c.Mid.r + .7152f * c.Mid.g + .0722f * c.Mid.b > .65f;

        /// <summary>빛나는 겹: 색 있는 몸 + 약한 더하기 한 겹. 밝은 종이 위에서 하얗게 날아가지 않는다.</summary>
        public void Glow(int shape, Vector2 pos, float w, float h, float rot, Vector4 p, FxRamp c,
            float alpha = 1f, float prog = 1f, float diss = 0f, float seed = 0f)
        {
            // 밝은 빛(은빛·흰 금빛·담채·꽃잎·번개 …)은 옅은 종이 바닥에 묻힌다(2026-11-20 사람: 지풍·비도 같은 게 너무 안 보인다).
            // 같은 모양을 조금 크게 먹으로 한 겹 먼저 깔아 테두리처럼 읽히게. 아주 작은 튐은 빼서 장 수를 아낀다
            if (IsLight(c) && Mathf.Abs(w * h) > .05f * Meter * Meter)
                Quad(shape, pos, w * 1.18f + .06f * Meter * Mathf.Sign(w), h * 1.35f + .06f * Meter * Mathf.Sign(h), rot, p, FxRamp.Ink, alpha * .42f, prog, diss, seed);
            Quad(shape, pos, w, h, rot, p, c, alpha, prog, diss, seed);
            if (!FxSettings.Simplified && !Busy) Quad(shape, pos, w, h, rot, p, c, alpha * .4f, prog, diss, seed, true);
        }

        void Add(float life, Action<Fx, float> draw, bool under = false)
        {
            _fx.Add(new Fx { Life = life, Draw = draw, Under = under, Seed = Rnd(0f, 7f) });
        }

        /// <summary>t 초 뒤(이펙트 시계)에 부른다.</summary>
        public void After(float t, Action f) => _later.Add(new Later { T = t, F = f });

        static Vector2 Dir(float a) => new Vector2(Mathf.Cos(a), Mathf.Sin(a));

        /// <summary>단계(1~5)마다 값 하나. 시안의 L([..]).</summary>
        static float L(int st, float a, float b, float c, float d, float e)
            => st <= 1 ? a : st == 2 ? b : st == 3 ? c : st == 4 ? d : e;

        // ── 공통 조각 ─────────────────────────────────────────────
        /// <summary>튀는 방울. glow 면 색 있는 불씨.</summary>
        public void Drops(Vector2 at, float dir, int n, float speed, FxRamp c, bool glow, float size, float spread)
        {
            n = FxSettings.Scale(n);
            if (Busy) n = (n + 2) / 3;
            for (int i = 0; i < n && _drops.Count < MaxDrops; i++)
            {
                float a = dir + Rnd(-spread, spread), s = Rnd(speed * .35f, speed) * Meter;
                _drops.Add(new Drop { Pos = at, Vel = Dir(a) * s, Life = Rnd(.22f, .45f), Size = size * Meter * Rnd(.6f, 1.3f), C = c, Glow = glow });
            }
        }

        /// <summary>퍼지는 충격 고리. size 는 미터.</summary>
        public void Ring(Vector2 at, float size, FxRamp c, float life, float alpha = 1f, bool glow = false, bool under = false)
            => RingW(at, size * Meter, c, life, alpha, glow, under);

        /// <summary>퍼지는 충격 고리. size 는 월드 단위.</summary>
        public void RingW(Vector2 at, float size, FxRamp c, float life, float alpha = 1f, bool glow = false, bool under = false)
        {
            Add(life, (f, k) =>
            {
                var p = new Vector4(.15f + .75f * (1f - (1f - k) * (1f - k)), .11f * (1f - k * .6f) + .025f, .45f, 0f);
                if (glow) Glow(2, at, size, size, 0f, p, c, alpha * (1f - k * k), 1f, 0f, f.Seed);
                else Quad(2, at, size, size, 0f, p, c, alpha * (1f - k * k), 1f, 0f, f.Seed);
            }, under);
        }

        /// <summary>바닥에 가는 금(먹 가시 얼룩). 0.8초에 걸쳐 옅어진다. size 는 미터.</summary>
        public void Crack(Vector2 at, float size, FxRamp c) => CrackW(at, size * Meter, c);

        /// <summary>바닥에 가는 금. size 는 월드 단위.</summary>
        public void CrackW(Vector2 at, float size, FxRamp c)
        {
            if (FxSettings.Simplified) return;
            Add(.8f, (f, k) => Quad(3, at, size, size * .72f, f.Seed, new Vector4(12f, 1.3f, .8f, 0f), c, .55f * (1f - k * k), 1f, .05f + k * .35f, f.Seed), true);
        }

        /// <summary>사방으로 뻗는 빛살 n 줄. len 은 월드 단위.</summary>
        public void RaysW(Vector2 at, int n, float len, FxRamp c)
        {
            if (FxSettings.Simplified) return;
            float wd = .5f * Meter;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * 6.283f + Rnd(-.2f, .2f), sd = Rnd(0f, 7f);
                var pos = at + Dir(a) * len * .55f;
                Add(.17f, (f, k) => Glow(1, pos, len, wd, a, new Vector4(.3f, .2f, 2f, .4f), c, 1f, Mathf.Min(1f, k * 2.4f), Mathf.Max(0f, k - .35f) * 1.3f, sd));
            }
        }

        /// <summary>
        /// 맞은 자리의 흰 섬광. 0.02초에 한 번까지만 뜬다(한꺼번에 여럿 맞아도 화면이 하얗게 안 됨).
        /// 단계가 오르면 길어지고(1.5 → 3.1m), 2단부터 밑에 먹이 터지고, 3단부터 열십자.
        /// </summary>
        public void HitFlash(Vector2 at, float dir, int stage = 1, bool heavy = false)
        {
            if (_time - _lastFlash <= .02f) return;
            _lastFlash = _time;
            float fa = dir + Rnd(-.5f, .5f), m = Meter;
            float len = L(stage, 1.5f, 1.9f, 2.3f, 2.7f, 3.1f) * (heavy ? 1.35f : 1f) * m, wd = (.4f + .07f * stage) * m;
            // 2026-11-17 사람: 번쩍임이 많다 → 흰 섬광 대신 먹으로 그은 금 하나(시안의 '절제')
            Add(.16f, (f, k) => Quad(1, at, len * .85f, wd * .55f, fa, new Vector4(.3f, .7f, 0f, 1f), FxRamp.Ink, .9f * (1f - k * k), Mathf.Min(1f, f.T / .035f), k * .5f, f.Seed));
            if (stage >= 2 && !FxSettings.Simplified)
            {
                float bs = (1.2f + .28f * stage) * m;
                var ba = at + Vector2.down * .1f * m;
                Add(.16f, (f, k) => Quad(3, ba, bs * (1f + k * .45f), bs * (1f + k * .45f), f.Seed, new Vector4(8f, .85f, .8f, 0f), FxRamp.Ink, .5f * (1f - k), 1f, k * .5f, f.Seed), true);
            }
        }

        /// <summary>
        /// 맞는 쪽 반응의 그림 부분(섬광 + 종류별로 튀는 것). kind: ink · gold · amber · blue.
        /// stage 가 오르면 튀는 양과 속도가 는다. cut 은 임팩트 컷(4단부터, 한 번 휘두를 때 처음 맞은 적 하나).
        /// </summary>
        public void Hit(Vector2 at, float dir, string kind, bool heavy = false, int stage = 1, bool cut = false)
        {
            HitFlash(at, dir, stage, heavy);
            float dn = L(stage, .8f, 1f, 1.2f, 1.45f, 1.7f);
            switch (kind)
            {
                case "gold":
                    Drops(at, dir, Mathf.RoundToInt((heavy ? 12 : 6) * dn), 12f + stage, FxRamp.Gold, true, .12f, .9f);
                    Drops(at, dir, 3, 7f, FxRamp.Ink, false, .11f, .6f);
                    break;
                case "amber":
                    Drops(at, dir, Mathf.RoundToInt(7 * dn), 9f + stage, FxRamp.Amber, true, .11f, 1.3f);
                    Drops(at, dir, 4, 6f, FxRamp.Brown, false, .13f, .9f);
                    break;
                case "blue": Drops(at, dir, Mathf.RoundToInt(7 * dn), 13f + stage, FxRamp.Blue, true, .09f, .7f); break;
                case "silver": HitSpark(at, dir, FxRamp.Silver, stage); Drops(at, dir, Mathf.RoundToInt(4 * dn), 11f + stage, FxRamp.Ink, false, .1f, .5f); break;
                case "poison": Drops(at, dir, Mathf.RoundToInt(4 * dn), 7f + stage, FxRamp.Poison, true, .1f, 1.2f); Drops(at, dir, 2, 6f, FxRamp.PoisonInk, false, .11f, .8f); break;
                case "needle": HitSpark(at, dir, FxRamp.Finger, stage); break;
                case "fire": Drops(at, dir, Mathf.RoundToInt(6 * dn), 10f + stage, FxRamp.Amber, true, .11f, 1.1f); Drops(at, dir, 3, 6f, FxRamp.Ink, false, .12f, .8f); break;
                case "water": Drops(at, dir, Mathf.RoundToInt(7 * dn), 10f + stage, FxRamp.Water, true, .1f, 1.1f); break;
                case "petal": Drops(at, dir, Mathf.RoundToInt(5 * dn), 7f + stage, FxRamp.Petal, true, .13f, 1.4f); Drops(at, dir, 3, 8f, FxRamp.Ink, false, .11f, .5f); break;
                case "thunder": Drops(at, dir, Mathf.RoundToInt(7 * dn), 13f + stage, FxRamp.Thunder, true, .09f, 1.6f); break;
                case "violet": Drops(at, dir, Mathf.RoundToInt(6 * dn), 9f + stage, FxRamp.Violet, false, .13f, .9f); Drops(at, dir, 2, 7f, FxRamp.VGlow, true, .09f, .7f); break;
                case "pale": Drops(at, dir, Mathf.RoundToInt(5 * dn), 8f + stage, FxRamp.Pale, true, .1f, 1.2f); Drops(at, dir, 3, 7f, FxRamp.Ink, false, .11f, .6f); break;
                default: Drops(at, dir, Mathf.RoundToInt((heavy ? 10 : 6) * dn), 11f + stage, FxRamp.Ink, false, .13f, .5f); break;
            }
            if (cut && stage >= 4) ImpactCut(at);
        }

        /// <summary>임팩트 컷: 검은 먹 터짐 위에 흰 터짐 두 프레임, 화면이 한 번 눌린다.</summary>
        public void ImpactCut(Vector2 at)
        {
            float m = Meter;
            Add(.07f, (f, k) => Quad(3, at, 3.3f * m, 3.3f * m, f.Seed, new Vector4(9f, 1f, .8f, 0f), FxRamp.Ink, .85f * (1f - k * k), 1f, 0f, f.Seed), true);
            HitFeel.Jolt(.35f, .35f);
        }

        /// <summary>쓰러진 자리에서 튀는 먹. 3단부터 먹이 크게 터진다(0.03초에 한 번까지 — 무리를 한꺼번에 베어도 검게 뭉치지 않게).</summary>
        public void Kill(Vector2 at, float dir, int stage = 1)
        {
            // 바닥 먹 자국은 InkMarks 가 이미 남긴다. 여기서 또 남기면 몸 둘레가 잿빛으로 뭉친다(11-13 Unity 에서 확인)
            Drops(at, dir, Mathf.RoundToInt(8 * L(stage, .8f, 1f, 1.2f, 1.45f, 1.7f)), 9f + stage, FxRamp.Ink, false, .15f, 1.1f);
            if (stage >= 3 && !FxSettings.Simplified && _time - _lastKillBurst > .03f)
            {
                _lastKillBurst = _time;
                float m = Meter;
                Add(.22f, (f, k) => Quad(3, at, 2.3f * (1f + k * .6f) * m, 2.3f * (1f + k * .6f) * m, f.Seed, new Vector4(7f, .9f, .8f, 0f), FxRamp.Ink, .6f * (1f - k), 1f, k * .6f, f.Seed), true);
            }
        }

        // ── 무공 ─────────────────────────────────────────────
        /// <summary>
        /// 붓 호 한 번(삼재검법·취선봉). center 는 그리는 동안 몸을 따라간다. dir -1 이면 반대쪽에서 긋는다.
        /// wash(담채)·gold(금빛 날)·edge(흰 날빛)·under(색 있는 획 밑 먹 받침)는 null 이면 없음.
        /// stage 2부터 마른 붓 메아리, 4부터 벤 자리에서 밀려 나가는 검풍. fin 은 마무리 획(검풍이 더 멀리).
        /// </summary>
        public void Swing(Func<Vector2> center, float ang, int dir, FxRamp ink, float rough, FxRamp? wash, FxRamp? gold, float radius, float span,
            int stage = 1, FxRamp? edge = null, FxRamp? under = null, bool fin = false)
        {
            float S = 2f * radius / .78f, th = L(stage, .42f, .46f, .5f, .53f, .56f);
            bool simple = FxSettings.Simplified;
            Add(.46f, (f, k) =>
            {
                var c = center();
                float t = f.T, pg = Mathf.Min(1f, t / .09f), ds = Mathf.Max(0f, (t - .16f) / .3f);
                if (wash.HasValue && !simple)
                {
                    float g = S * 1.14f * (1f + .12f * k);
                    Quad(0, c, g, g * dir, ang, new Vector4(span, .66f, 1f, .25f), wash.Value, .6f * (1f - k), pg, ds * .5f, f.Seed);
                }
                if (stage >= 2 && !simple)   // 마른 붓 메아리
                    Quad(0, c, S * 1.08f, S * 1.08f * dir, ang - dir * .14f, new Vector4(span * 1.06f, th * .85f, 1f, 1f), ink, .4f * (1f - k), Mathf.Min(1f, t / .14f), .22f + ds * .7f, f.Seed + 5f);
                if (under.HasValue) Quad(0, c, S * 1.03f, S * 1.03f * dir, ang, new Vector4(span, th * 1.12f, 1f, rough * .7f), under.Value, .6f, pg, ds * 1.1f, f.Seed + 7f);
                Quad(0, c, S, S * dir, ang, new Vector4(span, th, 1f, rough), ink, 1f, pg, ds, f.Seed);
                if (edge.HasValue) Glow(0, c, S * 1.012f, S * 1.012f * dir, ang, new Vector4(span, .09f, 1f, .2f), edge.Value, 1f, pg, ds * 1.3f, f.Seed + 2f);
                if (gold.HasValue) Glow(0, c, S * 1.035f, S * 1.035f * dir, ang, new Vector4(span, .15f, 1f, .3f), gold.Value, 1f, pg, ds * 1.1f, f.Seed + 3f);
            });
            if (gold.HasValue)   // 금빛 날이면 호를 따라 불씨
                After(.06f, () =>
                {
                    var c = center();
                    for (int i = 0; i < 6; i++)
                    {
                        float a = ang + (i / 5f - .5f) * Mathf.Min(span, 3f) * .8f * dir;
                        Drops(c + Dir(a) * radius, a, 2, 6f, gold.Value, true, .11f, .9f);
                    }
                });
            if (stage >= 4 && !simple)   // 검풍: 벤 자리에서 밀려 나가는 초승달
            {
                float grow = fin ? .5f : .36f;
                FxRamp? lit = gold ?? edge;
                After(.08f, () => Add(.24f, (f, k) =>
                {
                    var c = center();
                    float g = S * (1f + grow * (1f - (1f - k) * (1f - k)));
                    Quad(0, c, g, g * dir, ang, new Vector4(span * .82f, .15f * (1f - k * .5f), 1f, .7f), ink, .65f * (1f - k), 1f, k * .6f, f.Seed);
                    if (lit.HasValue) Glow(0, c, g * 1.01f, g * 1.01f * dir, ang, new Vector4(span * .82f, .06f, 1f, .3f), lit.Value, 1f - k, 1f, k * .7f, f.Seed + 1f);
                }));
            }
        }

        /// <summary>취선봉의 겹쳐 보이는 잔상 호(본 호보다 먼저 부른다).</summary>
        public void GhostSwing(Func<Vector2> center, float ang, int dir, FxRamp ink, float radius)
        {
            if (FxSettings.Simplified) return;
            float S = 2f * radius / .78f * 1.06f;
            Add(.34f, (f, k) => Quad(0, center(), S, S * dir, ang + dir * .3f, new Vector4(2.3f, .5f, 1f, 1f), ink, .3f * (1f - k), Mathf.Min(1f, f.T / .13f), .25f + k * .6f, f.Seed));
        }

        /// <summary>
        /// 취선봉 봉 끝이 한 박자 늦게 터지는 먼지 고리와 술 방울. 2단부터 찍힌 자리가 패이고, 3단부터 술이 위로 솟는다.
        /// last(마지막 봉 끝, 4단부터)·big(크게 쓸어 마무리)이면 고리가 두 번.
        /// </summary>
        public void Thump(Vector2 at, int stage = 1, bool last = false, bool big = false)
        {
            float m = (big ? 1.7f : 1f) * L(stage, 1f, 1.08f, 1.16f, 1.25f, 1.35f), w = Meter;
            Ring(at, 3.6f * m, FxRamp.Dust, .32f, 1f);
            Ring(at, 3.4f * m, FxRamp.Brown, .3f, .7f);
            Drops(at, 0f, Mathf.RoundToInt(6f * m), 8f + stage, FxRamp.Ink, false, .12f, Mathf.PI);
            if (stage >= 2 && !FxSettings.Simplified)
            {
                var pit = new FxRamp(FxRamp.Brown.Mid, FxRamp.Brown.Core, FxRamp.Ink.Core);
                var pa = at + Vector2.down * .1f * w;
                Add(.6f, (f, k) => Quad(3, pa, 2.7f * m * w, 2.7f * m * .7f * w, f.Seed, new Vector4(9f, 1f, .8f, 0f), pit, .62f * (1f - k) * (1f - k), 1f, k * .4f, f.Seed), true);
            }
            Drops(at, 0f, Mathf.RoundToInt(9f * m), 6f + stage, FxRamp.Amber, true, .13f, Mathf.PI);
            if (stage >= 3) Drops(at, Mathf.PI * .5f, Mathf.RoundToInt(6f * m), 10f, FxRamp.Wine, false, .14f, .9f);
            if (last || big)
            {
                After(.09f, () => { Ring(at, 5.2f * m, FxRamp.Dust, .36f, .6f); Ring(at, 5f * m, FxRamp.Brown, .34f, .3f); });
                float b = 2.4f * m * w;
                Add(.12f, (g, k) => Glow(3, at, b * (1f + k * .5f), b * (1f + k * .5f), g.Seed, new Vector4(7f, .6f, .8f, 0f), FxRamp.Amber, .9f * (1f - k), 1f, 0f, g.Seed));
            }
        }

        /// <summary>
        /// 소림 나한권: 금빛 권영이 from 에서 ang 쪽으로 reach(월드 단위)만큼 박힌다. 닿는 때(0.075초)에 onLand 를 부른다.
        /// last 는 마지막 주먹(크고 고리 둘). 2단부터 바닥에 금, 3단부터 잔상 주먹 둘, 4단부터 달아오른 심과 빛살,
        /// 5단의 마지막 주먹은 1.3배에 고리 셋·화면 눌림.
        /// </summary>
        public void Punch(Vector2 from, float ang, bool last, float reach, Action<Vector2> onLand = null, int stage = 1)
        {
            bool big = last && stage >= 5, simple = FxSettings.Simplified;
            float m = L(stage, 1f, 1.06f, 1.12f, 1.2f, 1.3f);
            float sz = reach * (last ? 1.5f : 1.3f) * m * (big ? 1.3f : 1f);
            var d0 = Dir(ang);
            var target = from + d0 * reach;
            Add(.4f, (f, k) =>
            {
                float t = f.T, go = Mathf.Min(1f, t / .075f), ez = 1f - (1f - go) * (1f - go), ds = Mathf.Max(0f, (t - .2f) / .2f);
                float d = reach * (.18f + .66f * ez);
                var pos = from + d0 * d;
                if (stage >= 3 && t < .24f && !simple)   // 잔상 주먹
                    for (int j = 2; j >= 1; j--)
                    {
                        float dj = Mathf.Max(reach * .09f, d - j * reach * .25f * ez);
                        Glow(7, from + d0 * dj, sz * (1f - .1f * j), sz * .74f * (1f - .1f * j), ang, Vector4.zero, FxRamp.Gold, .36f / j, 1f, Mathf.Min(.98f, ds + .25f), f.Seed + j);
                    }
                Quad(7, pos - d0 * reach * .045f + Vector2.down * reach * .03f, sz * 1.12f, sz * .86f, ang, Vector4.zero, FxRamp.Ink, .66f, 1f, ds, f.Seed);
                Glow(7, pos, sz, sz * .74f, ang, Vector4.zero, FxRamp.Gold, 1f, 1f, ds, f.Seed);
                if (stage >= 4) Glow(7, pos + d0 * sz * .08f, sz * .62f, sz * .46f, ang, Vector4.zero, FxRamp.Hot, .9f * (1f - k), 1f, ds, f.Seed + 4f);   // 달아오른 심
            });
            After(.075f, () =>
            {
                onLand?.Invoke(target);
                float rs = reach * (last ? 1.85f : 1.45f) * m * (big ? 1.25f : 1f);
                RingW(target, rs, FxRamp.Ink, .26f, .6f);
                RingW(target, rs * .96f, FxRamp.Gold, .26f, 1f, true);
                float b = rs * .68f;
                Add(.12f, (g, k) => Glow(3, target, b * (1f + k * .5f), b * (1f + k * .5f), g.Seed, new Vector4(5f, .28f, .8f, 0f), FxRamp.Gold, 1f - k, 1f, 0f, g.Seed));
                if (stage >= 2) CrackW(target + Vector2.down * reach * .2f, rs * .8f, FxRamp.Ink);
                if (stage >= 4) RaysW(target, last ? 10 : 6, rs * .75f, FxRamp.Gold);
                if (!last) return;
                After(.1f, () => { RingW(target, rs * 1.48f, FxRamp.Ink, .36f, .4f); RingW(target, rs * 1.44f, FxRamp.Gold, .36f, .85f, true); });   // 종 울림
                if (!big) return;
                After(.2f, () => { RingW(target, rs * 2f, FxRamp.Ink, .42f, .3f); RingW(target, rs * 1.96f, FxRamp.Gold, .42f, .7f, true); });
                Drops(target, ang, 16, 14f, FxRamp.Gold, true, .14f, Mathf.PI);
                HitFeel.Jolt(.9f, .8f);
            });
        }

        /// <summary>
        /// 남궁 창천검기: 몸을 한 바퀴(dur 초) 도는 푸른 초승달. 머리 각도는 a0 + 2π × 진행.
        /// 돌고 나면 0.3초에 걸쳐 흩어진다. 단계가 오르면 굵어지고 꼬리가 길어진다.
        /// 2단부터 머리 빛, 3단부터 후광, 4단부터 흰 날과 궤도를 따라 기는 번개(그림만).
        /// </summary>
        public void Orbit(Func<Vector2> center, float a0, float radius, float dur = .5f, int stage = 1)
        {
            float S = 2f * radius / .78f, rot = a0 + Mathf.PI;
            float m = L(stage, 1f, 1.1f, 1.2f, 1.3f, 1.4f), tl = L(stage, .14f, .17f, .2f, .23f, .26f);
            bool simple = FxSettings.Simplified;
            if (stage >= 4 && !simple)
                for (float q = .1f * dur; q < dur; q += .14f * dur)
                {
                    float ha = a0 + 6.283f * q / dur, hb = ha + Rnd(.3f, .7f), r2 = radius * Rnd(1.1f, 1.45f);
                    After(q, () => { var c = center(); Bolt(c + Dir(ha) * radius, c + Dir(hb) * r2, stage); });
                }
            Add(dur + .3f, (f, k) =>
            {
                var c = center();
                float t = f.T, pg = Mathf.Min(1f, t / dur) / 1.12f, ds = Mathf.Max(0f, (t - dur) / .3f);
                Quad(0, c, S * 1.04f, S * 1.04f, rot, new Vector4(6.283f, .34f * m, tl * .8f, .5f), FxRamp.BlueInk, .78f, pg, ds, f.Seed);
                if (stage >= 3 && !simple) Quad(0, c, S * 1.1f, S * 1.1f, rot, new Vector4(6.283f, .56f, .08f, .3f), FxRamp.Flat(FxRamp.Blue.Mid), .28f, pg, ds, f.Seed + 1f);
                Glow(0, c, S, S, rot, new Vector4(6.283f, .28f * m, tl, .45f), FxRamp.Blue, 1f, pg, ds, f.Seed + 3f);
                if (stage >= 4) Glow(0, c, S * .995f, S * .995f, rot, new Vector4(6.283f, .09f, tl * 1.2f, .2f), FxRamp.BlueHot, 1f, pg, ds * 1.3f, f.Seed + 4f);   // 흰 날
                if (stage >= 2 && t < dur)   // 머리 빛
                {
                    float ha = a0 + 6.283f * (t / dur), hs = .8f * m * Meter;
                    Glow(5, c + Dir(ha) * radius, hs, hs, 0f, new Vector4(.2f, 0f, 0f, 0f), FxRamp.BlueHot, .85f);
                }
            });
        }

        /// <summary>두 점 사이 번개 가지(창천검기 궤도를 따라 기거나 옆 적으로 튈 때). 단계가 오르면 굵어진다.</summary>
        public void Bolt(Vector2 a, Vector2 b, int stage = 1)
        {
            var d = b - a;
            float len = d.magnitude, ang = Mathf.Atan2(d.y, d.x), w = L(stage, .04f, .04f, .046f, .052f, .06f);
            var mid = (a + b) * .5f;
            Add(.16f, (f, k) =>
            {
                Quad(4, mid, len, 1.6f * Meter, ang, new Vector4(.35f, w * 2f, .5f, 0f), FxRamp.BlueInk, .8f * (1f - k * k), 1f, 0f, f.Seed);
                Glow(4, mid, len, 1.6f * Meter, ang, new Vector4(.35f, w, .5f, 0f), FxRamp.Blue, 1f - k * k, 1f, 0f, f.Seed);
            });
        }

        /// <summary>하늘에서 내리꽂히는 검기(창궁 진전 3단). 0.3초 예고 고리 뒤에 떨어지고, 닿는 때 onLand. many 는 여럿이 차례로 꽂힐 때(1.2배).</summary>
        public void SkySword(Vector2 at, Action onLand = null, bool many = false)
        {
            float m = Meter * (many ? 1.2f : 1f);
            Add(.3f, (f, k) => Quad(2, at + Vector2.up * .2f * m, 3f * m, 1.8f * m, 0f, new Vector4(.7f - .3f * k, .05f, .3f, 0f), FxRamp.Blue, .7f * k), true);
            After(.3f, () => SkyFall(at, onLand, many));
        }

        /// <summary>낙하 검기만(예고 없이). 부른 뒤 0.07초에 땅에 닿는다. 닿으면 고리·바닥 금·빛살이 터지고 화면이 눌린다.</summary>
        public void SkyFall(Vector2 at, Action onLand = null, bool many = false)
        {
            float m = Meter, s = many ? 1.2f : 1f;
            Add(.34f, (f, k) =>
            {
                float t = f.T, pg = Mathf.Min(1f, t / .07f), ds = Mathf.Max(0f, (t - .12f) / .22f);
                var top = at + Vector2.up * 5.2f * m;
                Quad(1, top, 10f * m, 1.9f * s * m, -Mathf.PI / 2f, new Vector4(.42f, .3f, 2f, 1f), FxRamp.BlueInk, .6f, pg, ds, f.Seed);
                Glow(1, top, 10f * m, 1.4f * s * m, -Mathf.PI / 2f, new Vector4(.42f, .4f, 3f, 1f), FxRamp.Blue, 1f, pg, ds, f.Seed + 1f);
                Glow(1, top, 10f * m, .6f * s * m, -Mathf.PI / 2f, new Vector4(.4f, .2f, 3f, 1f), FxRamp.BlueHot, 1f, pg, ds * 1.2f, f.Seed + 2f);
            });
            After(.07f, () =>
            {
                onLand?.Invoke();
                var g0 = at + Vector2.up * .3f * m;
                Ring(g0, 4.6f * s, FxRamp.BlueInk, .28f, .55f);
                Ring(g0, 4.4f * s, FxRamp.Blue, .28f, 1f, true);
                Crack(at, 3.2f * s, FxRamp.BlueInk);
                RaysW(g0, many ? 8 : 6, 3f * s * m, FxRamp.Blue);
                Add(.14f, (g, k) => Glow(3, at + Vector2.up * .4f * m, 3.4f * s * m, 3.4f * s * m, g.Seed, new Vector4(11f, .9f, .8f, 0f), FxRamp.Blue, 1f - k, 1f, 0f, g.Seed));
                HitFeel.Jolt(many ? .6f : .4f, many ? .6f : .4f);
            });
        }

        // ── 재료 ─────────────────────────────────────────────
        static Mesh MakeQuad()
        {
            var m = new Mesh { name = "FxQuad" };
            m.vertices = new[] { new Vector3(-.5f, -.5f), new Vector3(.5f, -.5f), new Vector3(-.5f, .5f), new Vector3(.5f, .5f) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            m.bounds = new Bounds(Vector3.zero, Vector3.one * 1e4f);
            return m;
        }

        /// <summary>이어 붙여도 이음매 없는 잡음 256². R·G 는 결이 다른 fbm, B 는 고운 알갱이(시안의 noiseTex 와 같은 식).</summary>
        static Texture2D MakeNoise()
        {
            const int N = 256;
            Func<int, uint, Func<int, int, float>> mk = (per, sd) =>
            {
                var g = new float[per * per];
                uint s = sd;
                for (int i = 0; i < g.Length; i++) { s = unchecked(s * 1664525u + 1013904223u); g[i] = s / 4294967296f; }
                return (x, y) =>
                {
                    float fx = x * per / (float)N, fy = y * per / (float)N;
                    int x0 = (int)fx, y0 = (int)fy;
                    float tx = fx - x0, ty = fy - y0, sx = tx * tx * (3 - 2 * tx), sy = ty * ty * (3 - 2 * ty);
                    float A = g[(y0 % per) * per + x0 % per], B = g[(y0 % per) * per + (x0 + 1) % per];
                    float C = g[((y0 + 1) % per) * per + x0 % per], D = g[((y0 + 1) % per) * per + (x0 + 1) % per];
                    return (A + (B - A) * sx) * (1 - sy) + (C + (D - C) * sx) * sy;
                };
            };
            var oct = new[]
            {
                new[] { mk(4, 1), mk(8, 2), mk(16, 3), mk(32, 4) },
                new[] { mk(6, 5), mk(12, 6), mk(24, 7), mk(48, 8) },
            };
            var grain = mk(128, 9);
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float r = 0f, g = 0f, am = .5f;
                    for (int o = 0; o < 4; o++) { r += oct[0][o](x, y) * am; g += oct[1][o](x, y) * am; am *= .5f; }
                    px[y * N + x] = new Color32(
                        (byte)Mathf.Clamp(((r / .9375f - .5f) * 1.7f + .5f) * 255f, 0f, 255f),
                        (byte)Mathf.Clamp(((g / .9375f - .5f) * 1.7f + .5f) * 255f, 0f, 255f),
                        (byte)(grain(x, y) * 255f), 255);
                }
            var t = new Texture2D(N, N, TextureFormat.RGBA32, true, true) { name = "FxNoise", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear };
            t.SetPixels32(px);
            t.Apply(true, true);
            return t;
        }
    }
}
