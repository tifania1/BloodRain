using System.Collections.Generic;
using BloodRain.Core.Data;
using BloodRain.Core.World;
using BloodRain.Sim;
using UnityEngine;

namespace BloodRain.Presentation.Fx
{
    // R4-13b 자리가 필요 없는 지역 기믹 셋(2026-11-28 PC, gimmicks fallingRocks·blizzard·redMoon — 웹 regionGimmicksStep·drawWeather 본보기).
    //   낙석(화산): 한 생 시계로 FallingRocksGimmick.FallsBetween 이 차고 화산에 서 있으면 플레이어 둘레 Places 세 곳에 그림자,
    //     telegraphSeconds 뒤 떨어져 반경 안 플레이어는 최대 기혈 10%(경공 무적이면 피함), 잡몹은 쓰러지고 정예·보스는 5%.
    //   눈보라(곤륜 설원)·붉은 달(십만대산): WeatherGimmick.ActiveAt 동안 그 땅에 서 있으면. 눈보라는 화면 가장자리 흰 덮개
    //     (화면 반대각선 × viewScale 밖이 가려짐, 웹과 같은 꼴) + 나·적 걸음, 붉은 달은 화면을 옅게 붉히고 적 걸음.
    //   걸음: 플레이어는 PlayerController.WeatherMultiplier, 적은 EnemyField 가 Step 에 EnemySpeed 를 곱한다.
    public sealed class WeatherLayer : MonoBehaviour
    {
        public LifeRunner runner;

        sealed class Rock { public System.Numerics.Vector2 At; public float Left; public float Boom = -1f; }
        readonly List<Rock> _rocks = new List<Rock>();
        readonly System.Random _rng = new System.Random(7713);
        float _lastSec = -1f;
        Material _discMat, _rockMat, _fogMat, _tintMat;
        Mesh _quad;
        MaterialPropertyBlock _mpb;
        PlayerController _pc;
        static readonly int IdTint = Shader.PropertyToID("_Tint");
        static readonly int IdDissolve = Shader.PropertyToID("_Dissolve");
        bool _forceRocks, _forceBlizzard, _forceRedMoon;

        /// <summary>적 걸음 배율(눈보라 × 붉은 달). EnemyField 가 Step 에 곱한다.</summary>
        public static float EnemySpeed { get; private set; } = 1f;
        public bool BlizzardOn { get; private set; }
        public bool RedMoonOn { get; private set; }
        public int RockCount => _rocks.Count;
        public int RockKills { get; private set; }
        /// <summary>시험: 낙석을 지금 떨군다 / 눈보라·붉은 달을 켜 둔다(땅·시각과 상관없이).</summary>
        public void ForceRocks() => _forceRocks = true;
        public void ForceBlizzard(bool on) => _forceBlizzard = on;
        public void ForceRedMoon(bool on) => _forceRedMoon = on;

        void Start()
        {
            var map = FindFirstObjectByType<MapLayer>();
            if (map == null || map.material == null) { enabled = false; return; }
            _discMat = new Material(map.material) { name = "낙석", renderQueue = 2995, mainTexture = MakeDisc(64, 0.55f) };
            _rockMat = new Material(map.material) { name = "바위", renderQueue = 2996, mainTexture = MakeDisc(64, 0.82f) };
            _fogMat = new Material(map.material) { name = "눈보라 덮개", renderQueue = 3300, mainTexture = MakeVignette(128) };
            _tintMat = new Material(map.material) { name = "붉은 달", renderQueue = 3290, mainTexture = Texture2D.whiteTexture };
            _quad = MakeQuad();
            _mpb = new MaterialPropertyBlock();
            _pc = runner != null && runner.player != null ? runner.player.GetComponent<PlayerController>() : null;
        }

        void OnDestroy()
        {
            EnemySpeed = 1f;
            if (_pc != null) _pc.WeatherMultiplier = 1f;
        }

        void Update()
        {
            if (runner == null || runner.Life == null || runner.Life.Record != null || runner.player == null || _quad == null)
            {
                EnemySpeed = 1f; BlizzardOn = RedMoonOn = false;
                if (_pc != null) _pc.WeatherMultiplier = 1f;
                return;
            }
            var life = runner.Life;
            var g = DataBoot.Data.Gimmicks;
            float now = life.Minutes * 60f, k = life.Scale.Factor(life.Realm);
            var wp = WorldCoords.FromUnity(runner.player.position);

            // 날씨
            BlizzardOn = _forceBlizzard || (HereHas(life, g.Blizzard.Id) && WeatherGimmick.ActiveAt(g.Blizzard, now));
            RedMoonOn = _forceRedMoon || (HereHas(life, g.RedMoon.Id) && WeatherGimmick.ActiveAt(g.RedMoon, now));
            float playerMul = BlizzardOn ? Or1(WeatherGimmick.PlayerMoveSpeed(g.Blizzard, ActiveTime(g.Blizzard, now))) : 1f;
            EnemySpeed = (BlizzardOn ? Or1(WeatherGimmick.EnemyMoveSpeed(g.Blizzard, ActiveTime(g.Blizzard, now))) : 1f)
                       * (RedMoonOn ? Or1(WeatherGimmick.EnemyMoveSpeed(g.RedMoon, ActiveTime(g.RedMoon, now))) : 1f);
            if (_pc != null) _pc.WeatherMultiplier = playerMul;

            // 낙석
            var fr = g.FallingRocks;
            if (_forceRocks || (_lastSec >= 0f && FallingRocksGimmick.FallsBetween(fr, _lastSec, now) > 0 && HereHas(life, fr.Id)))
            {
                _forceRocks = false;
                foreach (var at in FallingRocksGimmick.Places(fr, wp, _rng, k))
                    _rocks.Add(new Rock { At = at, Left = fr.TelegraphSeconds });
            }
            _lastSec = now;
            float dt = Time.deltaTime;
            for (int r = _rocks.Count - 1; r >= 0; r--)
            {
                var rock = _rocks[r];
                if (rock.Boom >= 0f)
                {
                    rock.Boom += dt;
                    if (rock.Boom >= 0.45f) _rocks.RemoveAt(r);
                    continue;
                }
                rock.Left -= dt;
                if (rock.Left > 0f) continue;
                rock.Boom = 0f;
                Fall(fr, rock.At, wp, k);
            }
        }

        // 바위가 떨어진 자리: 플레이어 한 번, 그 반경 안 적 전부
        void Fall(FallingRocksGimmickDef fr, System.Numerics.Vector2 at, System.Numerics.Vector2 wp, float k)
        {
            var vitals = runner.vitals;
            if (vitals != null && FallingRocksGimmick.Hits(fr, at, wp, k) && !(_pc != null && _pc.Invulnerable))
                vitals.Drain(FallingRocksGimmick.Damage(fr, GimmickTargetKind.Player, vitals.MaxHealth));
            var en = runner.enemies;
            var sim = en != null ? en.Sim : null;
            if (sim == null) return;
            for (int i = sim.Count - 1; i >= 0; i--)
            {
                var ag = sim.Get(i);
                if (ag.Health <= 0f) continue;
                if (!FallingRocksGimmick.Hits(fr, at, WorldCoords.FromUnity(new Vector2(ag.Pos.x, ag.Pos.y)), k)) continue;
                var ed = en.DefOf(ag.Kind);
                var kind = ed != null && ed.Family == "boss" ? GimmickTargetKind.Boss : ed != null && ed.Family == "elite" ? GimmickTargetKind.Elite : GimmickTargetKind.Normal;
                if (kind == GimmickTargetKind.Normal) { en.Kill(i); RockKills++; continue; }
                ag.Health -= FallingRocksGimmick.Damage(fr, kind, ag.MaxHealth);
                if (ag.Health <= 0f) { en.Kill(i); continue; }
                sim.Set(i, ag);
            }
        }

        void LateUpdate()
        {
            if (_quad == null || runner == null || runner.Life == null) return;
            var life = runner.Life;
            var fr = DataBoot.Data.Gimmicks.FallingRocks;
            float k = life.Scale.Factor(life.Realm), d = fr.Radius * k * 2f;
            float tel = Mathf.Max(0.01f, fr.TelegraphSeconds);
            foreach (var rock in _rocks)
            {
                var u = WorldCoords.ToUnity(rock.At);
                if (rock.Boom < 0f)
                {
                    // 예고: 그림자가 짙어지며 바위(작은 먹 덩이)가 위에서 줄어들며 내려옴
                    float t = 1f - Mathf.Clamp01(rock.Left / tel);
                    Draw(_discMat, new Color(0.18f, 0.14f, 0.12f, 0.22f + 0.33f * t), u, d, 0f, -0.05f);
                    Draw(_discMat, new Color(0.7f, 0.15f, 0.12f, 0.22f * t), u, d * (0.35f + 0.65f * t), 0f, -0.06f);
                    float rs = d * Mathf.Lerp(0.6f, 0.34f, t);
                    Draw(_rockMat, new Color(0.2f, 0.18f, 0.16f, 0.2f + 0.75f * t), u + Vector2.up * d * 1.6f * (1f - t), rs, t * 90f, -0.3f);
                }
                else
                {
                    // 떨어짐: 흙먼지 고리가 퍼지며 옅어짐 + 바위 자국
                    float b = Mathf.Clamp01(rock.Boom / 0.45f);
                    Draw(_discMat, new Color(0.35f, 0.31f, 0.28f, 0.6f * (1f - b)), u, d * (1f + 0.6f * b), 0f, -0.05f);
                    Draw(_rockMat, new Color(0.2f, 0.18f, 0.16f, 0.9f * (1f - b * b)), u, d * 0.34f, 45f, -0.06f);
                }
            }

            var cam = Camera.main;
            if (cam == null || !cam.orthographic) return;
            float h = cam.orthographicSize * 2f, w = h * cam.aspect;
            Vector2 c = cam.transform.position;
            if (RedMoonOn)
                DrawRect(_tintMat, new Color(0.59f, 0.08f, 0.08f, 0.12f), c, w * 1.05f, h * 1.05f);
            if (BlizzardOn)
            {
                // 웹 drawWeather 와 같은 꼴: 화면 반대각선 R × viewScale 을 바깥 끝으로, 그 0.7배부터 흰 덮개가 짙어짐
                float vs = Or1(DataBoot.Data.Gimmicks.Blizzard.ViewScale);
                float R = Mathf.Sqrt(w * w + h * h) * 0.5f * vs;
                // 덮개 텍스처는 반지름 1 = 바깥 끝(R), 0.7 부터 짙어지게 구웠다. 그 밖은 사각 판 넷으로 채운다
                DrawRect(_fogMat, new Color(0.957f, 0.965f, 0.973f, 0.94f), c, R * 2f, R * 2f);
                var full = new Color(0.957f, 0.965f, 0.973f, 0.94f);
                float ex = Mathf.Max(0f, w * 0.5f - R) + 0.5f, ey = Mathf.Max(0f, h * 0.5f - R) + 0.5f;
                if (w * 0.5f > R) { DrawRectWhite(full, c + new Vector2(R + ex * 0.5f, 0f), ex, h * 1.05f); DrawRectWhite(full, c - new Vector2(R + ex * 0.5f, 0f), ex, h * 1.05f); }
                if (h * 0.5f > R) { DrawRectWhite(full, c + new Vector2(0f, R + ey * 0.5f), R * 2f, ey); DrawRectWhite(full, c - new Vector2(0f, R + ey * 0.5f), R * 2f, ey); }
            }
        }

        void DrawRectWhite(Color tint, Vector2 at, float w, float h) => DrawRect(_tintMat, tint, at, w, h);

        void Draw(Material m, Color tint, Vector2 at, float size, float angle, float z)
        {
            _mpb.Clear();
            _mpb.SetVector(IdTint, new Vector4(tint.r, tint.g, tint.b, tint.a));
            _mpb.SetFloat(IdDissolve, 0f);
            var mat = Matrix4x4.TRS(new Vector3(at.x, at.y, z), Quaternion.Euler(0f, 0f, angle), new Vector3(size, size, 1f));
            var rp = new RenderParams(m) { matProps = _mpb, layer = gameObject.layer, worldBounds = new Bounds(mat.GetColumn(3), new Vector3(size, size, 1f)) };
            Graphics.RenderMesh(rp, _quad, 0, mat);
        }

        void DrawRect(Material m, Color tint, Vector2 at, float w, float h)
        {
            _mpb.Clear();
            _mpb.SetVector(IdTint, new Vector4(tint.r, tint.g, tint.b, tint.a));
            _mpb.SetFloat(IdDissolve, 0f);
            var mat = Matrix4x4.TRS(new Vector3(at.x, at.y, -0.5f), Quaternion.identity, new Vector3(w, h, 1f));
            var rp = new RenderParams(m) { matProps = _mpb, layer = gameObject.layer, worldBounds = new Bounds(mat.GetColumn(3), new Vector3(w, h, 1f)) };
            Graphics.RenderMesh(rp, _quad, 0, mat);
        }

        // 날씨가 켜진 동안의 셈은 시각 하나로 충분하지만, 강제로 켠 시험 때도 '켜짐' 값이 나오게 켜진 시각을 준다
        static float ActiveTime(WeatherGimmickDef def, float now) => WeatherGimmick.ActiveAt(def, now) ? now : def.FirstSeconds;
        static float Or1(float v) => v > 0f ? v : 1f;

        static bool HereHas(BloodRain.Core.Run.Life life, string gimmick)
        {
            foreach (var r in life.Regions.All)
                if (r.Id == life.RegionId) return r.Gimmicks != null && r.Gimmicks.Contains(gimmick);
            return false;
        }

        static Mesh MakeQuad()
        {
            var m = new Mesh { name = "날씨 판" };
            m.SetVertices(new List<Vector3> { new Vector3(-.5f, -.5f), new Vector3(-.5f, .5f), new Vector3(.5f, .5f), new Vector3(.5f, -.5f) });
            m.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) });
            m.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            return m;
        }

        // 부드러운 원(그림자·흙먼지·바위): soft 부터 가장자리까지 옅어지고 잡음으로 가장자리가 들쭉날쭉
        static Texture2D MakeDisc(int n, float soft)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "낙석 원" };
            var px = new Color32[n * n];
            for (int yy = 0; yy < n; yy++)
            for (int xx = 0; xx < n; xx++)
            {
                float x = (xx + 0.5f) / n * 2f - 1f, y = (yy + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(x * x + y * y) + (Mathf.PerlinNoise(xx * 0.15f, yy * 0.15f) - 0.5f) * 0.18f;
                float a = 1f - Mathf.Clamp01((r - soft) / (1f - soft));
                px[yy * n + xx] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        // 눈보라 덮개: 반지름 0.7 안은 비고 1 에서 꽉 참(웹 createRadialGradient(inner×0.7, inner)), 눈발 잡음을 조금
        static Texture2D MakeVignette(int n)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "눈보라 덮개" };
            var px = new Color32[n * n];
            for (int yy = 0; yy < n; yy++)
            for (int xx = 0; xx < n; xx++)
            {
                float x = (xx + 0.5f) / n * 2f - 1f, y = (yy + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(x * x + y * y);
                float a = Mathf.Clamp01((r - 0.7f) / 0.3f);
                a = Mathf.Clamp01(a + (Mathf.PerlinNoise(xx * 0.2f, yy * 0.2f) - 0.5f) * 0.25f * a);
                px[yy * n + xx] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
