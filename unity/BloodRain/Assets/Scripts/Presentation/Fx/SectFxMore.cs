using System;
using System.Collections.Generic;
using UnityEngine;

namespace BloodRain.Presentation.Fx
{
    // VF-02U 나머지 무공(2026-11-17). 시안 web/lookdev/sects.html 의 palm·finger·bomb·mist·dagger 를 옮겼다.
    // 날아가는 것(장풍·지풍·비도)은 WeaponRunner 가 탄을 굴리니, 여기선 그 프레임의 자리·각만 받아 그린다(Proj → DrawLive).
    // 값의 길이 단위는 시안 m. 탄 크기처럼 WeaponRunner 가 이미 월드 길이로 준 것은 그대로 쓰고, 시안 숫자는 Meter 를 곱한다.
    public sealed partial class SectFx
    {
        public enum ProjLook { Palm, Finger, Dagger, Petal }

        struct LiveProj { public ProjLook Look; public Vector2 Pos; public float Ang, Size, Age, Left, Seed, Run; public int Stage; public bool Evo; }
        readonly List<LiveProj> _live = new List<LiveProj>();

        static float Ez(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

        /// <summary>이번 프레임의 탄 하나. size 는 탄 반경(월드), age 는 난 지 초, left 는 남은 초, run 은 날아온 거리(월드).</summary>
        public void Proj(ProjLook look, Vector2 pos, float ang, float size, float age, float left, float run, int stage, bool evo = false)
        {
            if (_live.Count >= 256) return;
            // 난 때는 탄마다 다르고 프레임이 가도 같다 → 씨앗으로 쓴다(잡음이 프레임마다 튀지 않게)
            float seed = Mathf.Repeat((_time - age) * 7.13f, 7f);
            _live.Add(new LiveProj { Look = look, Pos = pos, Ang = ang, Size = size, Age = age, Left = left, Run = run, Seed = seed, Stage = stage, Evo = evo });
        }

        // 멈칫 표시(음공 기절·뇌부 감전·천설부 등 EnemySim Stun, 시안 sonic 의 머리 위 먹 점 셋). EnemyField 가 프레임마다 넘긴다
        readonly List<Vector3> _stuns = new List<Vector3>();

        /// <summary>이번 프레임에 멈춰 선 적 하나. head 는 머리 위 자리, size 는 사람 하나 크기(월드).</summary>
        public void Stunned(Vector2 head, float size)
        {
            if (_stuns.Count < 48) _stuns.Add(new Vector3(head.x, head.y, size));
        }

        void DrawLive()
        {
            foreach (var s in _stuns)
                for (int i = 0; i < 3; i++)
                {
                    float aa = _time * 5f + i * 2.09f + s.x * .7f;
                    Quad(5, new Vector2(s.x + Mathf.Cos(aa) * .3f * s.z, s.y + Mathf.Sin(aa) * .1f * s.z), .13f * s.z, .13f * s.z, 0f, new Vector4(.3f, 0f, 0f, 0f), FxRamp.Ink, .8f);
                }
            _stuns.Clear();
            foreach (var p in _live)
            {
                switch (p.Look)
                {
                    case ProjLook.Palm: DrawPalm(p); break;
                    case ProjLook.Finger: DrawFinger(p); break;
                    case ProjLook.Petal: DrawPetalShot(p); break;
                    default: DrawDagger(p); break;
                }
            }
            _live.Clear();
        }

        // 장풍: 회청 바람 초승달. 2단 마른 메아리 둘, 3단 담채, 5단 앞날 흰 빛과 바람결 줄기
        void DrawPalm(LiveProj p)
        {
            int st = p.Stage;
            float S = p.Size / .45f, k = p.Age / Mathf.Max(.01f, p.Age + p.Left);
            float al = 1f - Mathf.Max(0f, (k - .75f) / .25f);
            var d = Dir(p.Ang);
            var c0 = p.Pos - d * .39f * S;
            var body = p.Evo ? FxRamp.Gold : FxRamp.Palm;
            if (st >= 3) Glow(0, c0, S * 1.15f, S * 1.15f, p.Ang, new Vector4(1.9f, .5f, 1f, .6f), FxRamp.Wash, .18f * al, 1f, 0f, p.Seed + 3f);
            if (st >= 2 && !FxSettings.Simplified)
                for (int j = 1; j <= 2; j++)
                    Quad(0, c0 - d * .15f * S * j, S * (1f - .08f * j), S * (1f - .08f * j), p.Ang, new Vector4(1.9f, .26f, 1f, .9f), body, .22f * al / j, 1f, .3f + .15f * j, p.Seed + j);
            // 회청 바람이 종이 바닥에 묻혀서 먹 받침을 먼저(2026-11-20)
            Quad(0, c0 - d * .03f * S, S * 1.08f, S * 1.08f, p.Ang, new Vector4(2f, st >= 2 ? .56f : .48f, 1f, .55f), FxRamp.Ink, .45f * al, Mathf.Min(1f, p.Age / .08f), k > .75f ? (k - .75f) * 3f : 0f, p.Seed);
            Quad(0, c0, S, S, p.Ang, new Vector4(2f, st >= 2 ? .46f : .38f, 1f, .55f), body, .95f * al, Mathf.Min(1f, p.Age / .08f), k > .75f ? (k - .75f) * 3f : 0f, p.Seed);
            // 3단 흙먼지 자국(시안 palm: 0.3초마다 지나간 바닥에 옅은 흙빛, 6초에 걸쳐 마름)
            if (st >= 3 && !p.Evo && !FxSettings.Simplified && k < .8f && p.Age >= .3f && Mathf.Floor(p.Age / .3f) != Mathf.Floor((p.Age - Time.deltaTime) / .3f))
            {
                if (_stains.Count >= MaxStains) _stains.RemoveAt(0);
                _stains.Add(new Stain { Pos = c0 - Vector2.up * .1f * S, Seed = p.Seed + p.Age, Rot = p.Ang, Size = .27f * S, Shape = 3, P = new Vector4(3f, .35f, .8f, 0f), C = FxRamp.Dust, A = .22f });
            }
            if (st >= 5)
            {
                Glow(0, c0 + d * .013f * S, S, S, p.Ang, new Vector4(1.8f, .07f, 1f, .3f), FxRamp.White, .7f * al, 1f, 0f, p.Seed + 5f);
                var n = new Vector2(-d.y, d.x);
                for (int j = -1; j <= 1; j += 2)
                    Quad(1, p.Pos - d * .37f * S + n * (j * .117f * S), .53f * S, .073f * S, p.Ang + j * .15f * Mathf.Sin(p.Age * 9f), new Vector4(.3f, .8f, 3f, 1f), body, .4f * al, 1f, 0f, p.Seed + j);
            }
        }

        // 섬광지: 흰 금빛 바늘. 3단 꼬리가 길어지고 4단부터 흰 심
        void DrawFinger(LiveProj p)
        {
            int st = p.Stage; float m = Meter;
            // 2026-11-20 사람: 지풍이 너무 안 보인다 — 굵기 약 1.8배, 끝에 빛 머리
            float tl = Mathf.Min(L(st, .9f, 1f, 1.3f, 1.4f, 1.6f) * m, p.Run + .25f * m), w = L(st, .18f, .2f, .22f, .24f, .26f) * m * (p.Evo ? 1.3f : 1f);
            float al = Mathf.Clamp01(p.Left / .06f);
            var d = Dir(p.Ang);
            Glow(1, p.Pos - d * tl * .5f, tl, w, p.Ang, new Vector4(.3f, .2f, 3f, 1f), FxRamp.Finger, .95f * al, 1f, 0f, p.Seed); // 먹 받침은 Glow 가 깐다
            Glow(5, p.Pos, w * 2.2f, w * 1.4f, p.Ang, new Vector4(.3f, 0f, 0f, 0f), FxRamp.Finger, al);
            if (st >= 4 || p.Evo) Quad(1, p.Pos - d * tl * .45f, tl * .8f, w * .45f, p.Ang, new Vector4(.3f, .1f, 3f, 1f), FxRamp.White, .9f * al, 1f, 0f, p.Seed + 1f);
        }

        // 비도: 아주 가는 은빛 줄. 먹 받침 한 줄, 그 위 은빛(4단부터 흰 날빛), 앞에 날 하나
        void DrawDagger(LiveProj p)
        {
            int st = p.Stage; float m = Meter;
            float tl = Mathf.Min(L(st, .9f, 1.3f, 1.5f, 1.8f, 2f) * m, p.Run + .3f * m);
            float al = Mathf.Clamp01(p.Left / .12f);
            var d = Dir(p.Ang);
            var mid = p.Pos - d * tl * .5f;
            // 2026-11-20 사람: 비도가 너무 안 보인다 — 은빛만으로는 옅은 종이 바닥에 묻혀서, 먹 받침을 굵고 짙게, 날도 키우고 먹 그림자를 깐다
            Quad(1, mid, tl, .26f * m, p.Ang, new Vector4(.22f, .3f, 2f, 1f), p.Evo ? FxRamp.BlueInk : FxRamp.Ink, .62f * al, 1f, 0f, p.Seed);
            Glow(1, mid, tl, .15f * m, p.Ang, new Vector4(.2f, .2f, 2f, 1f), st >= 4 ? FxRamp.Blade : FxRamp.Silver, (st >= 2 ? .95f : .8f) * al, 1f, 0f, p.Seed + 1f);
            Glow(5, p.Pos, .52f * m, .17f * m, p.Ang, new Vector4(.3f, 0f, 0f, 0f), FxRamp.Silver, al); // 먹 그림자는 Glow 가 깐다
        }

        /// <summary>맞는 순간 가는 빛 한 줄(비도·지풍). 3단부터 열십자.</summary>
        public void HitSpark(Vector2 at, float dir, FxRamp c, int stage)
        {
            float m = Meter;
            Add(.09f, (f, k) =>
            {
                Quad(1, at, 1.3f * m, .5f * m, dir + 1.57f, new Vector4(.3f, 0f, 0f, 1f), FxRamp.Ink, .5f * (1f - k)); // 먹 받침(2026-11-20)
                Glow(1, at, 1.2f * m, .34f * m, dir + 1.57f, new Vector4(.3f, 0f, 0f, 1f), c, 1f - k);
                if (stage >= 3) Glow(1, at, .9f * m, .26f * m, dir, new Vector4(.3f, 0f, 0f, 1f), c, .8f * (1f - k));
            });
        }

        /// <summary>독 꽃 얼룩(당가 3단부터 맞은 바닥, 독무 4단부터 쓰러진 자리). size 는 미터.</summary>
        public void PoisonBloom(Vector2 at, float size, bool big = false)
        {
            if (FxSettings.Simplified) return;
            if (_stains.Count >= MaxStains) _stains.RemoveAt(0);
            _stains.Add(new Stain { Pos = at, Seed = Rnd(0f, 9f), Rot = Rnd(0f, 6f), Size = size * Meter, Shape = 13, P = big ? new Vector4(7f, .5f, .85f, .9f) : new Vector4(6f, .45f, .85f, .8f), C = FxRamp.PoisonInk, A = big ? .45f : .42f });
            if (!big) return;
            float m = Meter;
            Add(.4f, (g, k) => { float e = Ez(Mathf.Min(1f, k * 3f)); Glow(3, at, 3.4f * e * m, 3.4f * e * m, g.Seed, new Vector4(7f, .8f, .8f, 0f), FxRamp.Poison, .8f * (1f - k), 1f, k * .6f, g.Seed); });
            Drops(at, 0f, 14, 9f, FxRamp.Poison, true, .14f, 3.14f);
        }

        // ── 벽력탄 ─────────────────────────────────────────────
        /// <summary>쇠구슬이 포물선으로 떨어져 심지가 탄다. fuse 초 뒤에 WeaponRunner 가 BombBurst 를 부른다.</summary>
        public void BombThrow(Vector2 from, Vector2 at, float fuse, int stage)
        {
            float m = Meter, fly = fuse * .45f;
            var a0 = from + Vector2.up * 1.1f * m;
            Add(fuse, (f, k) =>
            {
                float q = Mathf.Min(1f, f.T / fly);
                var b = Vector2.Lerp(a0, at + Vector2.up * .15f * m, q) + Vector2.up * (Mathf.Sin(q * Mathf.PI) * 2.2f * m);
                Quad(5, at, .7f * q * m, .3f * q * m, 0f, new Vector4(.3f, 0f, 0f, 0f), FxRamp.Ink, .25f * q);            // 떨어질 자리 그림자
                Quad(5, b, .42f * m, .42f * m, 0f, new Vector4(.75f, 0f, 0f, 0f), FxRamp.Iron, 1f);
                Glow(5, b + new Vector2(-.07f, .08f) * m, .12f * m, .12f * m, 0f, new Vector4(.3f, 0f, 0f, 0f), FxRamp.Silver, .6f);
                if (f.T > fly)
                {
                    float burn = (f.T - fly) / Mathf.Max(.01f, fuse - fly);
                    var tip = b + new Vector2(.12f, .2f) * m;
                    if (Time.deltaTime > 0f && Rnd(0f, 1f) < .5f) Drops(tip, 1.4f, 1, 3f, FxRamp.Amber, true, .07f, .9f);
                    Glow(5, tip, (.22f + .1f * Mathf.Sin(f.T * 60f)) * m, .22f * m, 0f, new Vector4(.2f, 0f, 0f, 0f), FxRamp.Amber, .6f + .4f * burn);
                }
            });
        }

        /// <summary>호박색 불꽃으로 터진다. R 은 폭발 반경(월드). 2단 불씨, 3단 먹 연기, 4단·마지막은 고리 한 겹 더.</summary>
        public void BombBurst(Vector2 at, float R, int stage, bool final = false)
        {
            float m = Meter;
            Add(.32f, (g, k) =>
            {
                float e = Ez(Mathf.Min(1f, k * 3f));
                Glow(3, at, R * 1.9f * e, R * 1.5f * e, g.Seed, new Vector4(9f, .6f, .8f, 0f), FxRamp.Amber, .9f * (1f - k), 1f, k * .5f, g.Seed);
                if (k < .35f) Glow(5, at, R * 1.1f, R * .9f, 0f, new Vector4(.3f, 0f, 0f, 0f), FxRamp.Fire, (1f - k / .35f) * .8f);
            });
            if (stage >= 4 || final) RingW(at, R * 2.4f, FxRamp.Amber, .4f, .6f, true);
            RingW(at + Vector2.up * .1f * m, R * 1.8f, FxRamp.Dust, .45f, .6f);
            Drops(at, 0f, 8 + 4 * stage, 8f + stage, FxRamp.Amber, true, .12f, 3.14f);
            Drops(at, 0f, 6, 6f, FxRamp.Ink, false, .13f, 3.14f);
            if (FxSettings.Simplified) return;
            if (stage >= 3)
                Add(1.4f, (g, k) =>
                {
                    for (int j = 0; j < 3; j++)
                        Quad(3, at + new Vector2((j - 1) * .2f * R, (.6f + k * 1.8f + j * .2f) * m), R * (.6f + .5f * k), R * (.5f + .4f * k), g.Seed + j, new Vector4(6f, .5f, .8f, 0f), FxRamp.Ink, .25f * (1f - k), 1f, .2f + k * .7f, g.Seed + j);
                });
            if (stage >= 2)
            {
                int n = 4 + stage;
                for (int j = 0; j < n; j++)
                {
                    var ep = at + new Vector2(Rnd(-R * .6f, R * .6f), Rnd(-R * .4f, R * .4f));
                    float ph = j;
                    Add(2f, (g, k) => Glow(5, ep, .18f * m, .14f * m, 0f, new Vector4(.2f, 0f, 0f, 0f), FxRamp.Amber, (.5f + .5f * Mathf.Sin(g.T * 20f + ph)) * (1f - k)), true);
                }
            }
            if (_stains.Count >= MaxStains) _stains.RemoveAt(0);
            _stains.Add(new Stain { Pos = at - Vector2.up * .3f * m, Seed = Rnd(0f, 9f), Rot = Rnd(0f, 6f), Size = R * .9f, Shape = 14, P = new Vector4(9f, .45f, .34f, 1.2f), C = FxRamp.Soot, A = .32f });
        }

        // ── 독무 ─────────────────────────────────────────────
        /// <summary>몸 둘레에 감도는 옅은 녹색 안개. 틱마다 부른다. R 은 독무 반경(월드). 3단 가장자리 짙은 띠와 위로 오르는 김.</summary>
        public void Mist(Func<Vector2> center, float R, int stage)
        {
            int n = FxSettings.Scale((int)L(stage, 3f, 4f, 5f, 6f, 7f));
            for (int i = 0; i < n; i++)
            {
                float a0 = Rnd(0f, 6.283f), r0 = R * Rnd(.15f, .8f), sz = R * Rnd(.7f, 1.1f), sp = Rnd(.25f, .6f) * (Rnd(0f, 1f) < .5f ? -1f : 1f);
                Add(1.6f, (f, k) =>
                {
                    float a = a0 + sp * f.T, al = Mathf.Sin(k * Mathf.PI);
                    var pos = center() + new Vector2(Mathf.Cos(a) * r0, Mathf.Sin(a) * r0 * .8f);
                    Quad(3, pos, sz * (1f + .3f * k), sz * .7f * (1f + .3f * k), a, new Vector4(5f, .35f, .85f, 0f), FxRamp.Poison, .3f * al, 1f, .25f + .5f * k, f.Seed); // MistG(옅은 연두) .26 → 독 녹색 .3(2026-11-20: 종이 바닥에 묻혔다)
                }, true);
            }
            // 독무가 닿는 둘레: 1단부터 옅은 독 먹 고리(전엔 3단부터만 — 범위가 안 보였다, 2026-11-20)
            if (!FxSettings.Simplified)
                Add(1.2f, (f, k) => Quad(2, center(), R * 2.1f, R * 1.7f, f.Seed + f.T * .4f, new Vector4(.86f, .08f, .8f, 0f), FxRamp.PoisonInk, (stage >= 3 ? .3f : .2f) * Mathf.Sin(k * Mathf.PI)), true);
            if (stage < 3 || FxSettings.Simplified) return;
            float m = Meter;
            for (int j = 0; j < (stage >= 5 ? 3 : 1) && _drops.Count < MaxDrops; j++)
            {
                float a = Rnd(0f, 6.283f);
                var pos = center() + new Vector2(Mathf.Cos(a) * R * .9f, Mathf.Sin(a) * R * .7f);
                _drops.Add(new Drop { Pos = pos, Vel = new Vector2(Rnd(-.2f, .2f), Rnd(1.6f, 2.6f)) * m, Life = 1.1f, Size = .35f * m, C = FxRamp.MistG, Glow = false });
            }
        }

        // ── 화산 낙화검우 ─────────────────────────────────────────────
        /// <summary>꽃잎·배꽃 조각이 흩날린다(모양 11). 크기·속도는 미터.</summary>
        public void Petals(Vector2 at, float dir, int n, float speed, float spread, float size, FxRamp c)
        {
            n = FxSettings.Scale(n);
            for (int i = 0; i < n && _drops.Count < MaxDrops; i++)
            {
                float a = dir + Rnd(-spread, spread), v = Rnd(speed * .3f, speed) * Meter;
                _drops.Add(new Drop { Pos = at, Vel = Dir(a) * v, Life = Rnd(.6f, 1.1f), Size = size * Meter * Rnd(.7f, 1.3f), C = c, Shape = 11, Rot = Rnd(0f, 6.28f), Spin = Rnd(-6f, 6f) });
            }
        }

        // 가는 먹 검선. 2단 꽃잎이 따라 나부끼고, 3단 연분홍 담채, 4단 흰 날빛
        void DrawPetalShot(LiveProj p)
        {
            int st = p.Stage; float m = Meter;
            float tl = Mathf.Min(L(st, .8f, 1f, 1.2f, 1.3f, 1.4f) * m, p.Run + .3f * m), al = Mathf.Clamp01(p.Left / .1f);
            var d = Dir(p.Ang);
            if (st >= 3) Glow(1, p.Pos - d * tl * .6f, tl * 1.2f, .36f * m, p.Ang, new Vector4(.4f, .4f, 2f, 1f), FxRamp.Petal, .45f * al, 1f, 0f, p.Seed);
            Quad(1, p.Pos - d * tl * .5f, tl, .18f * m, p.Ang, new Vector4(.25f, .2f, 2f, 1f), FxRamp.Ink, .9f * al, 1f, 0f, p.Seed + 1f); // 굵기 .1 → .18(2026-11-20 잘 안 보였다)
            if (st >= 4) Glow(1, p.Pos - d * tl * .5f, tl, .06f * m, p.Ang, new Vector4(.2f, .1f, 2f, 1f), FxRamp.White, .8f * al, 1f, 0f, p.Seed + 2f);
            float dt = Time.deltaTime;
            if (st >= 2 && dt > 0f && Rnd(0f, 1f) < dt / (st >= 4 ? .03f : .05f)) Petals(p.Pos - d * .3f * m, p.Ang + Mathf.PI, 1, 1.5f, 1f, .21f, FxRamp.Petal);   // VIS-01U: 꽃잎 .16 → .21
        }

        // ── 곤륜 뇌부 ─────────────────────────────────────────────
        /// <summary>누런 부적이 날아가 붙어 fuse 초 동안 깜빡인다(2단부터 부적 자리에 먹 고리). size 는 낙뢰 반경에 따른 배(1 = 기본).</summary>
        public void Talisman(Vector2 from, Vector2 at, float fuse, int stage, float size = 1f)
        {
            float m = Meter, fl = Mathf.Min(.22f, fuse * .55f);
            var a0 = from + Vector2.up * m;
            Add(fuse + .05f, (f, k) =>
            {
                float q = Mathf.Min(1f, f.T / fl), qq = Ez(q), flick = q < 1f ? 1f : .7f + .3f * Mathf.Sin(f.T * 70f);
                var pp = Vector2.Lerp(a0, at + Vector2.up * .9f * m, qq) + Vector2.up * (Mathf.Sin(q * Mathf.PI) * 1.2f * m);
                Quad(12, pp, .5f * size * m, .9f * size * m, q < 1f ? f.T * 14f : 0f, default, FxRamp.Talis, flick, 1f, 0f, f.Seed);
                if (q >= 1f && stage >= 2) Quad(2, at + Vector2.up * .15f * m, 1.6f * size * m, m, 0f, new Vector4(.55f + .1f * Mathf.Sin(f.T * 30f), .1f, .5f, 0f), FxRamp.Ink, .5f, 1f, 0f, f.Seed);
            });
        }

        /// <summary>낙뢰. R 은 맞는 반경(월드). 번개 줄기(먹 받침 + 빛) · 터짐 · 남색 고리 · 그을음. 4단부터 가지 번개 둘, final 은 번개 셋.</summary>
        public void Strike(Vector2 at, float R, int stage, bool final = false)
        {
            float M = Meter, m = R / Mathf.Max(1e-6f, 1.2f * M), w = L(stage, .05f, .07f, .08f, .09f, .1f) * m, H = 9f * M;
            void One(Vector2 o, float ww, float al) => Add(.2f, (e, k) =>
            {
                Quad(4, o + Vector2.up * (H / 2f), H, 2f * M, -Mathf.PI / 2f, new Vector4(.25f, ww * 2.2f, .6f, 0f), FxRamp.BlueInk, .7f * al * (1f - k * k), 1f, 0f, e.Seed);
                Glow(4, o + Vector2.up * (H / 2f), H, 2f * M, -Mathf.PI / 2f, new Vector4(.25f, ww, .6f, 0f), FxRamp.Thunder, al * (1f - k * k), 1f, 0f, e.Seed);
            });
            One(at, w, 1f);
            if (final) for (int s = -1; s <= 1; s += 2) One(at + Vector2.right * (s * .6f * M), w * .8f, .9f);
            Add(.12f, (g, k) => Glow(3, at + Vector2.up * .4f * M, 2.6f * m * M * (1f + k * .5f), 2.6f * m * M * (1f + k * .5f), g.Seed, new Vector4(10f, .9f, .8f, 0f), FxRamp.Thunder, 1f - k, 1f, 0f, g.Seed));
            RingW(at + Vector2.up * .2f * M, 3.2f * m * M, FxRamp.BlueInk, .26f, .5f);
            if (FxSettings.Simplified) return;
            if (_stains.Count >= MaxStains) _stains.RemoveAt(0);
            _stains.Add(new Stain { Pos = at, Seed = Rnd(0f, 9f), Rot = Rnd(0f, 6f), Size = 1.4f * m * M, Shape = 14, P = new Vector4(8f, .4f, .3f, 1f), C = FxRamp.BlueInk, A = .5f });
            if (stage >= 4)
                for (int s = -1; s <= 1; s += 2)
                {
                    float a = -Mathf.PI / 2f + s * Rnd(.6f, 1.1f), ln = Rnd(1.6f, 2.4f) * m * M;
                    var from = at + Vector2.up * .5f * M;
                    var to = from + new Vector2(Mathf.Cos(a) * ln, -Mathf.Sin(a) * ln * .6f);
                    After(.02f, () => Bolt(from, to, stage));
                }
        }

        // ── 무당 태극검 ─────────────────────────────────────────────
        /// <summary>흑백 검이 느리게 돈다. angle(t, i) 는 t 초에 i 번째 검의 각(WeaponShapes.BladeAngle), R 은 궤도 반경(월드).
        /// 짝수 번째는 먹, 홀수 번째는 흰 검(먹 받침 + 빛). 2단부터 바닥에 옅은 태극, 4단 흰 검에 날빛.</summary>
        public void Taiji(Func<Vector2> center, Func<float, int, float> angle, int n, float R, float life, int stage, bool evo = false)
        {
            float M = Meter, S = 2f * R / .78f, tl = L(stage, .18f, .26f, .3f, .34f, .38f), span = tl * 6.283f;
            if (stage >= 2 || evo)
                Add(life + .4f, (f, k) =>
                {
                    float sz = R * 2.4f * (stage >= 4 ? 1.25f : 1f), on = Mathf.Min(1f, f.T / .4f) * (1f - Mathf.Max(0f, (f.T - life) / .4f));
                    Quad(10, center() + Vector2.up * .1f * M, sz, sz * .8f, -f.T * .7f, new Vector4(.3f, 0f, 0f, 0f), FxRamp.Taiji, (stage >= 4 ? .24f : .14f) * on, 1f, 0f, f.Seed);
                }, true);
            Add(life + .3f, (f, k) =>
            {
                float t = f.T, fade = Mathf.Max(0f, (t - life) / .3f), al = Mathf.Min(1f, t / .2f) * (1f - fade);
                var c = center();
                for (int i = 0; i < n; i++)
                {
                    float a = angle(Mathf.Min(t, life), i);
                    bool wh = i % 2 == 1;
                    Quad(0, c, S, S, a - span / 2f, new Vector4(span, .24f, .6f, .4f), wh ? FxRamp.Pale : FxRamp.Ink, (wh ? .55f : .7f) * al, 1f, 0f, f.Seed + i);
                    if (wh)
                    {
                        Quad(0, c, S * 1.03f, S * 1.03f, a - span / 2f, new Vector4(span, .3f, .6f, .4f), FxRamp.Ink, .22f * al, 1f, 0f, f.Seed + i + 9f);
                        Glow(0, c, S, S, a - span / 2f, new Vector4(span, .14f, .6f, .3f), stage >= 4 ? FxRamp.White : FxRamp.Pale, .6f * al, 1f, 0f, f.Seed + i + 3f);
                    }
                    var tan = Dir(a + 1.57f);
                    var b = c + Dir(a) * R - tan * .45f * M;
                    Quad(1, b, .9f * M, .2f * M, a + 1.57f, new Vector4(.3f, 0f, 0f, 1f), wh ? FxRamp.Pale : FxRamp.Ink, al);
                    if (wh) Quad(1, b, .95f * M, .3f * M, a + 1.57f, new Vector4(.3f, 0f, 0f, 1f), FxRamp.Ink, .35f * al);
                }
            });
        }

        // ── 수로채 쇄겸술 ─────────────────────────────────────────────
        /// <summary>먹 사슬이 hook(걸린 자리)까지 뻗어 낫이 박혔다 돌아온다. 2단 당기는 길의 물결, 3단 청록 물결 호, 4단 낫에 물빛과 끌려온 자리(to)의 소용돌이.</summary>
        public void Chain(Func<Vector2> origin, Vector2 hook, Vector2 to, int stage, bool first)
        {
            float M = Meter, dir = first ? 1f : -1f;
            var o0 = origin();
            float an0 = Mathf.Atan2(hook.y - o0.y, hook.x - o0.x);
            Add(.52f, (f, k) =>
            {
                float t = f.T, outK = t < .14f ? Ez(t / .14f) : t < .22f ? 1f : Mathf.Max(0f, 1f - Ez((t - .22f) / .2f));
                var o = origin();
                var v = hook - o;
                float an = Mathf.Atan2(v.y, v.x), Ln = v.magnitude * outK;
                var u = Dir(an);
                var tip = o + u * Ln; var mid = o + u * (Ln * .5f);
                if (stage >= 2 && t > .18f) Glow(1, mid, Ln + .6f * M, .7f * M, an + Mathf.PI, new Vector4(.4f, .6f, 3f, 1f), FxRamp.Water, .45f * (1f - k), 1f, 0f, f.Seed);
                Quad(1, mid, Ln, .08f * M, an, new Vector4(.3f, 0f, 0f, 1f), FxRamp.Ink, .45f);
                int links = 0;
                for (float d = .3f * M; d < Ln && links < 40; d += .36f * M, links++)
                {
                    bool odd = links % 2 == 1;
                    Quad(5, o + u * d, (odd ? .3f : .22f) * M, (odd ? .16f : .22f) * M, an, new Vector4(.55f, 0f, 0f, 0f), FxRamp.Ink, .95f);
                }
                Quad(0, tip, 1.3f * M, 1.3f * M * dir, an - dir * .6f, new Vector4(2.2f, .42f, 1f, .5f), FxRamp.Ink, 1f, 1f, 0f, f.Seed + 2f);
                if (stage >= 4) Glow(0, tip, 1.33f * M, 1.33f * M * dir, an - dir * .6f, new Vector4(2.2f, .12f, 1f, .2f), FxRamp.Water, 1f, 1f, 0f, f.Seed + 3f);
                if (stage >= 3 && t > .2f)
                {
                    float S = 2f * Ln / .78f + .1f * M, pg = Mathf.Min(1f, (t - .2f) / .14f), ds = Mathf.Max(0f, k - .6f) * 2f;
                    Quad(0, o - Vector2.up * .2f * M, S, S * dir, an, new Vector4(1.3f, .5f, 1f, .6f), FxRamp.WaterInk, .4f * (1f - k), pg, ds, f.Seed + 4f);
                    Glow(0, o - Vector2.up * .2f * M, S * .98f, S * .98f * dir, an, new Vector4(1.3f, .3f, 1f, .5f), FxRamp.Water, .7f * (1f - k), pg, ds, f.Seed + 5f);
                }
            });
            After(.14f, () =>
            {
                Drops(hook, an0 + Mathf.PI, 10, 9f + stage, FxRamp.Water, false, .13f, 1.2f);
                RingW(hook - Vector2.up * .2f * M, 2.2f * M, FxRamp.WaterInk, .25f, .5f);
                if (stage < 2 || FxSettings.Simplified) return;
                if (_stains.Count >= MaxStains) _stains.RemoveAt(0);
                _stains.Add(new Stain { Pos = hook, Seed = Rnd(0f, 9f), Rot = an0, Size = 1.3f * M, Shape = 16, P = new Vector4(.8f, 1.4f, .35f, 0f), C = FxRamp.WaterInk, A = .3f });
            });
            if (stage >= 4 && !FxSettings.Simplified)
                After(.22f, () => Add(.5f, (g, k) =>
                {
                    Quad(2, to, 2.6f * M * (1f - k * .4f), 1.6f * M * (1f - k * .4f), 0f, new Vector4(.6f, .12f, .6f, 0f), FxRamp.WaterInk, .5f * (1f - k), 1f, 0f, g.Seed);
                    Glow(2, to, 2.4f * M * (1f - k * .4f), 1.5f * M * (1f - k * .4f), 0f, new Vector4(.5f, .08f, .6f, 0f), FxRamp.Water, .8f * (1f - k), 1f, 0f, g.Seed + 1f);
                }, true));
        }

        // ── 마교 혈조 ─────────────────────────────────────────────
        /// <summary>어두운 자주 할퀸 자국 한 갈래. len 은 갈래 길이(월드). 2단 마른 메아리, 3단 바닥에 잠깐 남는 자국, 4단 연분홍 빛.
        /// VIS-01U(2026-11-20 21:50 PC 에서 봄): 시안 굵기(.55m, 획 몫 .3)는 게임 카메라(대각선 36m)에선 2~3px 실금이라 거의 안 보였다 — 굵기 2배, 획 몫 .4.</summary>
        public void Claw(Vector2 from, float ra, float len, int stage, float delay)
        {
            float M = Meter;
            var u = Dir(ra); var nrm = new Vector2(-u.y, u.x);
            var c = from + u * (len * .5f + .25f * M);
            After(delay, () =>
            {
                Add(.34f, (f, k) =>
                {
                    float pg = Mathf.Min(1f, f.T / .07f), ds = Mathf.Max(0f, (f.T - .12f) / .22f);
                    if (stage >= 2) Quad(1, c + nrm * .1f * M, len * 1.02f, 1.24f * M, ra, new Vector4(.4f, 1f, 1.5f, 1f), FxRamp.Violet, .4f * (1f - k), pg, .2f + ds * .7f, f.Seed + 5f);
                    Quad(1, c, len, 1.1f * M, ra, new Vector4(.4f, .9f, 1.5f, 1f), FxRamp.Violet, 1f, pg, ds, f.Seed);
                    if (stage >= 4) Glow(1, c, len, .36f * M, ra, new Vector4(.26f, .4f, 1.5f, 1f), FxRamp.VGlow, .8f, pg, ds * 1.2f, f.Seed + 2f);
                });
                if (stage >= 3 && !FxSettings.Simplified)
                    Add(.9f, (f, k) => Quad(1, c - Vector2.up * .3f * M, len * .95f, .8f * M, ra, new Vector4(.36f, 1f, 1.5f, 1f), FxRamp.Violet, .55f * (1f - k), 1f, .15f + k * .6f, f.Seed), true);
            });
        }

        /// <summary>할퀸 끝에서 피어나는 옅은 피안개. size 는 미터.</summary>
        public void BloodMistAt(Vector2 at, float size, int stage)
        {
            float s = size * Meter;
            Add(.7f, (g, k) => Quad(3, at, s * (.6f + .6f * Ez(k)), s * .7f * (.6f + .6f * Ez(k)), g.Seed, new Vector4(6f, .5f, .8f, 0f), FxRamp.BloodMist, (stage >= 2 ? .55f : .42f) * (1f - k), 1f, .15f + k * .7f, g.Seed), true);
        }

        /// <summary>흡혈: 맞은 적에서 몸으로 들어오는 가는 실(3단부터).</summary>
        public void BloodThread(Vector2 from, Func<Vector2> to)
        {
            float M = Meter;
            Add(.36f, (g, k) =>
            {
                float q = Ez(k);
                var t = to() + Vector2.up * .5f * M;
                var h = Vector2.Lerp(from, t, q); var tail = Vector2.Lerp(from, t, Mathf.Max(0f, q - .35f));
                var v = h - tail;
                Glow(1, (h + tail) * .5f, v.magnitude + .05f * M, .12f * M, Mathf.Atan2(v.y, v.x), new Vector4(.25f, .2f, 2f, 1f), FxRamp.VGlow, .9f * (1f - k * k), 1f, 0f, g.Seed);
            });
        }

        // ── 음공 ─────────────────────────────────────────────
        /// <summary>현이 떨리고 먹 파문이 R(월드)까지 번진다. 2단 마른 메아리 고리, 3단 안쪽 담채, 5단 현에 흰 빛. gold 는 절기·금강후.</summary>
        public void Sonic(Func<Vector2> center, float R, int stage, bool gold = false)
        {
            float M = Meter;
            var c0 = center();
            Add(.35f, (f, k) =>
            {
                var c = center();
                for (int j = -1; j <= 1; j++)
                {
                    float wob = Mathf.Sin(f.T * 70f + j) * .06f * M * (1f - k);
                    var at = c + Vector2.up * (.35f * M + j * .12f * M + wob);
                    Quad(1, at, 1.5f * M, .05f * M, 0f, new Vector4(.5f, .2f, 0f, 1f), FxRamp.Ink, .7f * (1f - k));
                    if (stage >= 5 || gold) Glow(1, at, 1.5f * M, .04f * M, 0f, new Vector4(.5f, 0f, 0f, 1f), gold ? FxRamp.Gold : FxRamp.White, .6f * (1f - k));
                }
            });
            RingW(c0, R * 2.2f, FxRamp.Ink, .75f, L(stage, .7f, .8f, .85f, .85f, .9f));
            if (gold) RingW(c0, R * 2.15f, FxRamp.Gold, .75f, .8f, true);
            if (stage >= 2) After(.07f, () => RingW(c0, R * 2f, FxRamp.Ink, .7f, .35f));
            if (stage >= 3 && !FxSettings.Simplified)
                Add(.8f, (f, k) =>
                {
                    float q = Mathf.Min(1f, f.T / .6f), r = R * Ez(q), al = 1f - Mathf.Max(0f, (f.T - .6f) / .2f);
                    Quad(5, c0, r * 2f, r * 1.6f, 0f, new Vector4(.85f, 0f, 0f, 0f), FxRamp.Wash, .08f * al * (1f - q));
                }, true);
        }

        // ── 이화창 ─────────────────────────────────────────────
        /// <summary>곧은 먹 찌르기가 len(월드) 앞까지. 2단 마른 메아리, 3단 창끝에서 흰 배꽃과 옅은 담채, 4단 창날에 흰 빛. spin 은 회전 찌르기(배꽃이 더 많이).</summary>
        public void Thrust(Func<Vector2> origin, float a, float len, int stage, bool spin = false, bool evo = false)
        {
            float M = Meter, tl = Mathf.Min(L(stage, 1.6f, 1.9f, 2.1f, 2.3f, 2.5f) * M, len * .7f);
            const float dur = .1f;
            var d = Dir(a);
            var body = evo ? FxRamp.Gold : FxRamp.Ink;
            Add(dur + .22f, (f, k) =>
            {
                float q = Mathf.Min(1f, f.T / dur), dist = .4f * M + (len - .4f * M) * Ez(q), al = 1f - Mathf.Max(0f, (f.T - dur) / .22f);
                var h = origin() + d * dist;
                if (stage >= 3) Glow(1, h - d * len * .45f, len * .95f, .5f * M, a, new Vector4(.5f, .5f, 3f, 1f), FxRamp.Pale, .3f * al, 1f, 0f, f.Seed + 2f);
                if (stage >= 2) Quad(1, h - d * (tl * .5f + .25f * M), tl, .3f * M, a, new Vector4(.4f, .9f, 3f, 1f), FxRamp.Ink, .3f * al, 1f, .3f, f.Seed + 3f);
                Quad(1, h - d * tl * .5f, tl, L(stage, .26f, .36f, .5f, .66f, .85f) * M, a, new Vector4(.5f, .42f, 3f, .8f), body, .95f * al, 1f, (1f - al) * .6f, f.Seed);   // VF-25: 4·5단이 삼재검법 5단 세기에 닿게 굵힘
                if (stage >= 4) Glow(1, h - d * tl * .4f, tl * .8f, L(stage, .08f, .08f, .08f, .1f, .13f) * M, a, new Vector4(.3f, .1f, 3f, 1f), FxRamp.White, .85f * al, 1f, 0f, f.Seed + 1f);
                Glow(5, h, .3f * M, .3f * M, 0f, new Vector4(.2f, 0f, 0f, 0f), FxRamp.White, .8f * al);
            });
            if (stage >= 3) After(dur, () => Petals(origin() + d * len, a, spin ? 10 : 6, 3f + stage * .4f, 1.2f, .28f, FxRamp.Pale));   // VIS-01U: 배꽃 조각 .22 → .28
            // VF-25: 찌른 길이 먹 한 획으로 잠깐 남는다(삼재검법 반원이 남듯)
            if (stage >= 2)
                After(dur, () =>
                {
                    float ml = len + .2f * M;
                    var mid = origin() + d * (.3f * M + ml * .5f);
                    float tw = L(stage, .3f, .3f, .4f, .5f, .64f) * M, ta = L(stage, .4f, .4f, .5f, .55f, .62f);
                    Add(L(stage, .3f, .3f, .36f, .4f, .46f), (f, k) =>
                    {
                        Quad(1, mid, ml, tw, a, new Vector4(.5f, .7f, 1.5f, 1f), body, ta * (1f - k), 1f, .15f + k * .75f, f.Seed);
                        if (stage >= 5) Glow(1, mid + d * ml * .1f, ml * .8f, .12f * M, a, new Vector4(.3f, .2f, 1.5f, 1f), FxRamp.White, .5f * (1f - k) * (1f - k), 1f, 0f, f.Seed + 1f);
                    });
                });
        }
    }
}
