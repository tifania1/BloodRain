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
        public static readonly FxRamp Gold = new FxRamp(C(.6f, .27f, .02f), C(.97f, .66f, .1f), C(1f, .96f, .72f));
        public static readonly FxRamp Brown = new FxRamp(C(.42f, .3f, .18f), C(.24f, .15f, .07f), C(.1f, .06f, .03f));
        public static readonly FxRamp Amber = new FxRamp(C(.55f, .2f, .02f), C(.93f, .5f, .08f), C(1f, .85f, .45f));
        public static readonly FxRamp Blue = new FxRamp(C(.05f, .15f, .6f), C(.2f, .5f, .96f), C(.9f, .97f, 1f));
        public static readonly FxRamp BlueInk = new FxRamp(C(.1f, .14f, .25f), C(.04f, .06f, .13f), C(.02f, .03f, .07f));
        public static readonly FxRamp Dust = new FxRamp(C(.55f, .47f, .35f), C(.5f, .42f, .3f), C(.45f, .37f, .26f));
        public static readonly FxRamp White = Flat(Color.white);
    }

    /// <summary>
    /// 문파 무공 이펙트(VF-01U, 2026-11-13). 텍스처 한 장을 띄우던 것을 궤적·기·타격·잔흔 겹으로 그린다.
    /// 시안 web/lookdev/sects.html 의 swing·punch·orbit·skySword·ring·bolt·drops 를 그대로 옮겼다(값도 같다).
    /// 여기는 그림만 그린다. 피해 판정은 WeaponRunner 가 그대로 한다.
    /// 그리기는 WeaponRunner 의 붓 획과 같은 방식(Graphics.RenderMesh + z 순서)이다.
    /// </summary>
    public sealed class SectFx : MonoBehaviour
    {
        public static SectFx Instance { get; private set; }

        [SerializeField] Material material;   // BloodRain/FxInk. 비어 있으면 Resources/Fx/FxInk 를 읽는다

        sealed class Fx
        {
            public float T, Life, Seed;
            public bool Under;
            public Action<Fx, float> Draw;
        }
        struct Drop { public Vector2 Pos, Vel; public float T, Life, Size; public FxRamp C; public bool Glow; }
        struct Stain { public Vector2 Pos; public float T, Seed, Rot, Size; }
        struct Later { public float T; public Action F; }

        readonly List<Fx> _fx = new List<Fx>();
        readonly List<Drop> _drops = new List<Drop>();
        readonly List<Stain> _stains = new List<Stain>();
        readonly List<Later> _later = new List<Later>();
        readonly System.Random _rng = new System.Random(11);
        MaterialPropertyBlock _mpb;
        Mesh _quad;
        Texture2D _noise;
        float _time, _lastFlash = -1f, _z;
        RenderParams _rp;

        const float StainLife = 6f;
        const float ZUnder = 0.02f;     // 캐릭터(z 0) 뒤
        const float ZOver = -0.3f;      // 붓 획(-0.26~-0.28)보다 카메라 쪽
        const int MaxDrops = 400, MaxStains = 48;

        static readonly int IdNoise = Shader.PropertyToID("_NoiseTex"), IdShape = Shader.PropertyToID("_Shape"), IdP = Shader.PropertyToID("_P"),
            IdProg = Shader.PropertyToID("_Prog"), IdDiss = Shader.PropertyToID("_Diss"), IdSeed = Shader.PropertyToID("_Seed"),
            IdTime = Shader.PropertyToID("_FxTime"), IdAlpha = Shader.PropertyToID("_Alpha"), IdAdd = Shader.PropertyToID("_Add"),
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

        void LateUpdate()
        {
            if (material == null) return;
            _rp = new RenderParams(material) { matProps = _mpb, layer = gameObject.layer, worldBounds = new Bounds(Vector3.zero, Vector3.one * 1e7f) };
            _z = ZUnder;
            foreach (var s in _stains)
            {
                float k = s.T / StainLife;
                Quad(3, s.Pos, s.Size * 2f, s.Size * 1.4f, s.Rot, new Vector4(3f, .35f, .8f, 0f), FxRamp.Ink, .26f * (1f - k) * (1f - k), 1f, .12f + k * .5f, s.Seed);
            }
            foreach (var f in _fx) if (f.Under) f.Draw(f, f.T / f.Life);
            _z = ZOver;
            foreach (var f in _fx) if (!f.Under) f.Draw(f, f.T / f.Life);
            foreach (var d in _drops)
            {
                float k = d.T / d.Life, sp = d.Vel.magnitude / Mathf.Max(Meter, 1e-6f);
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
            _mpb.SetColor(IdC0, c.Edge);
            _mpb.SetColor(IdC1, c.Mid);
            _mpb.SetColor(IdC2, c.Core);
            _z -= 0.0002f;   // 부른 순서대로 위에 쌓인다
            var m = Matrix4x4.TRS(new Vector3(pos.x, pos.y, _z), Quaternion.Euler(0f, 0f, rot * Mathf.Rad2Deg), new Vector3(w, h, 1f));
            Graphics.RenderMesh(_rp, _quad, 0, m);
        }

        /// <summary>빛나는 겹: 색 있는 몸 + 약한 더하기 한 겹. 밝은 종이 위에서 하얗게 날아가지 않는다.</summary>
        public void Glow(int shape, Vector2 pos, float w, float h, float rot, Vector4 p, FxRamp c,
            float alpha = 1f, float prog = 1f, float diss = 0f, float seed = 0f)
        {
            Quad(shape, pos, w, h, rot, p, c, alpha, prog, diss, seed);
            if (!FxSettings.Simplified) Quad(shape, pos, w, h, rot, p, c, alpha * .4f, prog, diss, seed, true);
        }

        void Add(float life, Action<Fx, float> draw, bool under = false)
        {
            _fx.Add(new Fx { Life = life, Draw = draw, Under = under, Seed = Rnd(0f, 7f) });
        }

        /// <summary>t 초 뒤(이펙트 시계)에 부른다.</summary>
        public void After(float t, Action f) => _later.Add(new Later { T = t, F = f });

        static Vector2 Dir(float a) => new Vector2(Mathf.Cos(a), Mathf.Sin(a));

        // ── 공통 조각 ─────────────────────────────────────────────
        /// <summary>튀는 방울. glow 면 색 있는 불씨.</summary>
        public void Drops(Vector2 at, float dir, int n, float speed, FxRamp c, bool glow, float size, float spread)
        {
            n = FxSettings.Scale(n);
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

        /// <summary>맞은 자리의 가는 흰 섬광. 0.02초에 한 번까지만 뜬다(한꺼번에 여럿 맞아도 화면이 하얗게 안 됨).</summary>
        public void HitFlash(Vector2 at, float dir)
        {
            if (_time - _lastFlash <= .02f) return;
            _lastFlash = _time;
            float fa = dir + Rnd(-.5f, .5f);
            float m = Meter;
            Add(.07f, (f, k) => Quad(1, at, (1.3f + k * .6f) * m, .45f * m, fa, new Vector4(.38f, 0f, 0f, 1f), FxRamp.White, .8f * (1f - k)));
        }

        /// <summary>맞는 쪽 반응의 그림 부분(섬광 + 종류별로 튀는 것). kind: ink · gold · amber · blue</summary>
        public void Hit(Vector2 at, float dir, string kind, bool heavy = false)
        {
            HitFlash(at, dir);
            switch (kind)
            {
                case "gold": Drops(at, dir, heavy ? 12 : 6, 12f, FxRamp.Gold, true, .12f, .9f); Drops(at, dir, 3, 7f, FxRamp.Ink, false, .11f, .6f); break;
                case "amber": Drops(at, dir, 7, 9f, FxRamp.Amber, true, .11f, 1.3f); Drops(at, dir, 4, 6f, FxRamp.Brown, false, .13f, .9f); break;
                case "blue": Drops(at, dir, 7, 13f, FxRamp.Blue, true, .09f, .7f); break;
                default: Drops(at, dir, heavy ? 10 : 6, 11f, FxRamp.Ink, false, .13f, .5f); break;
            }
        }

        /// <summary>쓰러진 자리의 먹 얼룩과 튀는 먹. 6초에 걸쳐 마른다.</summary>
        public void Kill(Vector2 at, float dir)
        {
            if (_stains.Count >= MaxStains) _stains.RemoveAt(0);
            _stains.Add(new Stain { Pos = at + Dir(dir) * .5f * Meter, Seed = Rnd(0f, 9f), Rot = dir, Size = Rnd(.9f, 1.4f) * Meter });
            Drops(at, dir, 8, 9f, FxRamp.Ink, false, .15f, 1.1f);
        }

        // ── 무공 ─────────────────────────────────────────────
        /// <summary>
        /// 붓 호 한 번(삼재검법·취선봉). center 는 그리는 동안 몸을 따라간다. dir -1 이면 반대쪽에서 긋는다.
        /// wash(2단 담채)·gold(3단 금빛 날)는 null 이면 없음.
        /// </summary>
        public void Swing(Func<Vector2> center, float ang, int dir, FxRamp ink, float rough, FxRamp? wash, FxRamp? gold, float radius, float span)
        {
            float S = 2f * radius / .78f;
            Add(.44f, (f, k) =>
            {
                var c = center();
                float t = f.T, pg = Mathf.Min(1f, t / .11f), ds = Mathf.Max(0f, (t - .17f) / .27f);
                if (wash.HasValue && !FxSettings.Simplified)
                {
                    float g = S * 1.12f * (1f + .1f * k);
                    Quad(0, c, g, g * dir, ang, new Vector4(span, .6f, 1f, .25f), wash.Value, .55f * (1f - k), pg, ds * .5f, f.Seed);
                }
                Quad(0, c, S, S * dir, ang, new Vector4(span, .46f, 1f, rough), ink, 1f, pg, ds, f.Seed);
                if (gold.HasValue) Glow(0, c, S * 1.03f, S * 1.03f * dir, ang, new Vector4(span, .11f, 1f, .3f), gold.Value, 1f, pg, ds * 1.2f, f.Seed + 2f);
            });
        }

        /// <summary>취선봉의 겹쳐 보이는 잔상 호(본 호보다 먼저 부른다).</summary>
        public void GhostSwing(Func<Vector2> center, float ang, int dir, FxRamp ink, float radius)
        {
            if (FxSettings.Simplified) return;
            float S = 2f * radius / .78f * 1.06f;
            Add(.34f, (f, k) => Quad(0, center(), S, S * dir, ang + dir * .3f, new Vector4(2.3f, .5f, 1f, 1f), ink, .3f * (1f - k), Mathf.Min(1f, f.T / .13f), .25f + k * .6f, f.Seed));
        }

        /// <summary>취선봉 봉 끝이 한 박자 늦게 터지는 먼지 고리와 술 방울.</summary>
        public void Thump(Vector2 at)
        {
            Ring(at, 3.6f, FxRamp.Dust, .32f, .9f);
            Ring(at, 3.4f, FxRamp.Brown, .3f, .4f);
            Drops(at, 0f, 9, 6f, FxRamp.Amber, true, .13f, Mathf.PI);
        }

        /// <summary>소림 나한권: 금빛 권영이 from 에서 ang 쪽으로 reach(월드 단위)만큼 박힌다. 닿는 때(0.08초)에 onLand 를 부른다. last 는 마지막 주먹(크고 고리 둘).</summary>
        public void Punch(Vector2 from, float ang, bool last, float reach, Action<Vector2> onLand = null)
        {
            float sz = reach * (last ? 1.5f : 1.3f);
            var d0 = Dir(ang);
            var target = from + d0 * reach;
            Add(.38f, (f, k) =>
            {
                float t = f.T, go = Mathf.Min(1f, t / .08f), ez = 1f - (1f - go) * (1f - go), ds = Mathf.Max(0f, (t - .2f) / .18f);
                var pos = from + d0 * reach * (.18f + .66f * ez);
                Quad(7, pos - d0 * reach * .045f + Vector2.down * reach * .03f, sz * 1.12f, sz * .86f, ang, Vector4.zero, FxRamp.Ink, .62f, 1f, ds, f.Seed);
                Glow(7, pos, sz, sz * .74f, ang, Vector4.zero, FxRamp.Gold, 1f, 1f, ds, f.Seed);
            });
            After(.08f, () =>
            {
                onLand?.Invoke(target);
                RingW(target, reach * (last ? 1.85f : 1.45f), FxRamp.Ink, .26f, .55f);
                RingW(target, reach * (last ? 1.78f : 1.36f), FxRamp.Gold, .26f, 1f, true);
                float b = reach * (last ? 1.26f : 1f);
                Add(.11f, (g, k) => Glow(3, target, b * (1f + k * .5f), b * (1f + k * .5f), g.Seed, new Vector4(5f, .28f, .8f, 0f), FxRamp.Gold, 1f - k, 1f, 0f, g.Seed));
                if (last) After(.1f, () => { RingW(target, reach * 2.74f, FxRamp.Ink, .36f, .35f); RingW(target, reach * 2.67f, FxRamp.Gold, .36f, .8f, true); });   // 종 울림
            });
        }

        /// <summary>
        /// 남궁 창천검기: 몸을 한 바퀴(dur 초) 도는 푸른 초승달. 머리 각도는 a0 + 2π × 진행.
        /// 돌고 나면 0.3초에 걸쳐 흩어진다.
        /// </summary>
        public void Orbit(Func<Vector2> center, float a0, float radius, float dur = .5f)
        {
            float S = 2f * radius / .78f, rot = a0 + Mathf.PI;
            Add(dur + .3f, (f, k) =>
            {
                var c = center();
                float t = f.T, pg = Mathf.Min(1f, t / dur) / 1.12f, ds = Mathf.Max(0f, (t - dur) / .3f);
                Quad(0, c, S * 1.04f, S * 1.04f, rot, new Vector4(6.283f, .3f, .1f, .5f), FxRamp.BlueInk, .6f, pg, ds, f.Seed);
                if (!FxSettings.Simplified) Quad(0, c, S * 1.08f, S * 1.08f, rot, new Vector4(6.283f, .5f, .08f, .3f), FxRamp.Flat(FxRamp.Blue.Mid), .25f, pg, ds, f.Seed + 1f);
                Glow(0, c, S, S, rot, new Vector4(6.283f, .24f, .14f, .45f), FxRamp.Blue, 1f, pg, ds, f.Seed + 3f);
            });
        }

        /// <summary>두 점 사이 번개 가지(창천검기가 옆 적으로 튈 때).</summary>
        public void Bolt(Vector2 a, Vector2 b)
        {
            var d = b - a;
            float len = d.magnitude, ang = Mathf.Atan2(d.y, d.x);
            var mid = (a + b) * .5f;
            Add(.16f, (f, k) =>
            {
                Quad(4, mid, len, 1.6f * Meter, ang, new Vector4(.35f, .06f, .5f, 0f), FxRamp.BlueInk, .5f * (1f - k * k), 1f, 0f, f.Seed);
                Glow(4, mid, len, 1.6f * Meter, ang, new Vector4(.35f, .035f, .5f, 0f), FxRamp.Blue, 1f - k * k, 1f, 0f, f.Seed);
            });
        }

        /// <summary>하늘에서 내리꽂히는 검기(창궁 진전 3단). 0.3초 예고 고리 뒤에 떨어지고, 닿는 때 onLand.</summary>
        public void SkySword(Vector2 at, Action onLand = null)
        {
            float m = Meter;
            Add(.3f, (f, k) => Quad(2, at + Vector2.up * .2f * m, 3f * m, 1.8f * m, 0f, new Vector4(.7f - .3f * k, .05f, .3f, 0f), FxRamp.Blue, .7f * k), true);
            After(.3f, () => SkyFall(at, onLand));
        }

        /// <summary>낙하 검기만(예고 없이). 부른 뒤 0.07초에 땅에 닿는다.</summary>
        public void SkyFall(Vector2 at, Action onLand = null)
        {
            float m = Meter;
            Add(.34f, (f, k) =>
            {
                float t = f.T, pg = Mathf.Min(1f, t / .07f), ds = Mathf.Max(0f, (t - .12f) / .22f);
                var top = at + Vector2.up * 5.2f * m;
                Quad(1, top, 10f * m, 1.7f * m, -Mathf.PI / 2f, new Vector4(.42f, .3f, 2f, 1f), FxRamp.BlueInk, .55f, pg, ds, f.Seed);
                Glow(1, top, 10f * m, 1.3f * m, -Mathf.PI / 2f, new Vector4(.42f, .4f, 3f, 1f), FxRamp.Blue, 1f, pg, ds, f.Seed + 1f);
            });
            After(.07f, () =>
            {
                onLand?.Invoke();
                var g0 = at + Vector2.up * .3f * m;
                Ring(g0, 4.6f, FxRamp.BlueInk, .28f, .5f);
                Ring(g0, 4.4f, FxRamp.Blue, .28f, 1f, true);
                Add(.14f, (g, k) => Glow(3, at + Vector2.up * .4f * m, 3.4f * m, 3.4f * m, g.Seed, new Vector4(11f, .9f, .8f, 0f), FxRamp.Blue, 1f - k, 1f, 0f, g.Seed));
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
