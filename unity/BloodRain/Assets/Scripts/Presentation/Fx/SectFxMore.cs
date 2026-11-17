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
        public enum ProjLook { Palm, Finger, Dagger }

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

        void DrawLive()
        {
            foreach (var p in _live)
            {
                switch (p.Look)
                {
                    case ProjLook.Palm: DrawPalm(p); break;
                    case ProjLook.Finger: DrawFinger(p); break;
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
            Quad(0, c0, S, S, p.Ang, new Vector4(2f, st >= 2 ? .46f : .38f, 1f, .55f), body, .95f * al, Mathf.Min(1f, p.Age / .08f), k > .75f ? (k - .75f) * 3f : 0f, p.Seed);
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
            float tl = Mathf.Min(L(st, .7f, .8f, 1.2f, 1.3f, 1.5f) * m, p.Run + .25f * m), w = L(st, .1f, .11f, .12f, .13f, .14f) * m * (p.Evo ? 1.3f : 1f);
            float al = Mathf.Clamp01(p.Left / .06f);
            var d = Dir(p.Ang);
            Glow(1, p.Pos - d * tl * .5f, tl, w, p.Ang, new Vector4(.3f, .2f, 3f, 1f), FxRamp.Finger, .85f * al, 1f, 0f, p.Seed);
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
            Quad(1, mid, tl, .14f * m, p.Ang, new Vector4(.22f, .3f, 2f, 1f), p.Evo ? FxRamp.BlueInk : FxRamp.Ink, .35f * al, 1f, 0f, p.Seed);
            Glow(1, mid, tl, .1f * m, p.Ang, new Vector4(.2f, .2f, 2f, 1f), st >= 4 ? FxRamp.Blade : FxRamp.Silver, (st >= 2 ? .8f : .55f) * al, 1f, 0f, p.Seed + 1f);
            Quad(5, p.Pos, .32f * m, .11f * m, p.Ang, new Vector4(.3f, 0f, 0f, 0f), FxRamp.Silver, al);
        }

        /// <summary>맞는 순간 가는 빛 한 줄(비도·지풍). 3단부터 열십자.</summary>
        public void HitSpark(Vector2 at, float dir, FxRamp c, int stage)
        {
            float m = Meter;
            Add(.07f, (f, k) =>
            {
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
                    Quad(3, pos, sz * (1f + .3f * k), sz * .7f * (1f + .3f * k), a, new Vector4(5f, .35f, .85f, 0f), FxRamp.MistG, .26f * al, 1f, .25f + .5f * k, f.Seed);
                }, true);
            }
            if (stage < 3 || FxSettings.Simplified) return;
            Add(1.2f, (f, k) => Quad(2, center(), R * 2.1f, R * 1.7f, f.Seed + f.T * .4f, new Vector4(.86f, .08f, .8f, 0f), FxRamp.PoisonInk, .22f * Mathf.Sin(k * Mathf.PI)), true);
            float m = Meter;
            for (int j = 0; j < (stage >= 5 ? 3 : 1) && _drops.Count < MaxDrops; j++)
            {
                float a = Rnd(0f, 6.283f);
                var pos = center() + new Vector2(Mathf.Cos(a) * R * .9f, Mathf.Sin(a) * R * .7f);
                _drops.Add(new Drop { Pos = pos, Vel = new Vector2(Rnd(-.2f, .2f), Rnd(1.6f, 2.6f)) * m, Life = 1.1f, Size = .35f * m, C = FxRamp.MistG, Glow = false });
            }
        }
    }
}
