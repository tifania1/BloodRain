using System.Collections.Generic;
using BloodRain.Core.World;
using BloodRain.Sim;
using UnityEngine;

namespace BloodRain.Presentation.Fx
{
    // RG-05U 무당산 운해(2026-11-26 PC, gimmicks cloudSea — 웹 cloudSeaStep 본보기). 한 생 시계로 BloomsBetween 이 차고
    //   무당산에 서 있으면 플레이어 둘레 ±scatter 안 count 곳에 구름이 telegraphSeconds 에 피어 leaveSeconds 머물고 driftDegrees 쪽으로 흐른다.
    //   구름이 덮는 동안(Covering) 그 안에 든 플레이어를 보스 뺀 적이 놓친다: EnemyField 가 Decoy(숨은 순간 플레이어 자리)를 쫓아
    //   그 둘레를 맴돌고, 원거리 적은 쏘지 않는다. 그림은 코드로 구운 흰 구름 덩이 두 겹 + 푸른 잿빛 테(종이 바닥에서 보이게).
    public sealed class CloudSeaLayer : MonoBehaviour
    {
        public LifeRunner runner;

        sealed class Cloud { public System.Numerics.Vector2 At; public float Born; public float Seed; }
        readonly List<Cloud> _clouds = new List<Cloud>();
        readonly System.Random _rng = new System.Random(5501);
        float _lastSec = -1f;
        Material _mat, _rimMat;
        Mesh _quad;
        MaterialPropertyBlock _mpb;
        static readonly int IdTint = Shader.PropertyToID("_Tint");
        static readonly int IdDissolve = Shader.PropertyToID("_Dissolve");
        static Vector2? _decoy;
        bool _forceBloom;

        /// <summary>플레이어가 구름에 숨어 있으면 적이 쫓을 자리(숨은 순간의 플레이어 자리, Unity 좌표). 아니면 null.</summary>
        public static Vector2? Decoy => _decoy;
        public int CloudCount => _clouds.Count;
        public bool Hidden => _decoy.HasValue;
        /// <summary>시험: 다음 프레임에 구름을 피운다.</summary>
        public void ForceBloom() => _forceBloom = true;

        void Start()
        {
            var map = FindFirstObjectByType<MapLayer>();
            if (map == null || map.material == null) { enabled = false; return; }
            var tex = MakeCloud();
            _mat = new Material(map.material) { name = "운해", renderQueue = 3100, mainTexture = tex };
            _rimMat = new Material(map.material) { name = "운해 테", renderQueue = 3099, mainTexture = tex };
            _quad = MakeQuad();
            _mpb = new MaterialPropertyBlock();
        }

        void OnDestroy() => _decoy = null;

        void Update()
        {
            if (runner == null || runner.Life == null || runner.Life.Record != null || runner.player == null || _mat == null) { _decoy = null; return; }
            var life = runner.Life;
            var def = DataBoot.Data.Gimmicks.CloudSea;
            float now = life.Minutes * 60f, k = life.Scale.Factor(life.Realm);
            Vector2 p = runner.player.position;
            var wp = WorldCoords.FromUnity(p);
            if (_forceBloom || (_lastSec >= 0f && CloudSeaGimmick.BloomsBetween(def, _lastSec, now) > 0 && HereHas(life, def.Id)))
            {
                _forceBloom = false;
                foreach (var at in CloudSeaGimmick.Places(def, wp, _rng, k))
                    _clouds.Add(new Cloud { At = at, Born = now, Seed = (float)_rng.NextDouble() * 10f });
            }
            _lastSec = now;
            _clouds.RemoveAll(c => now - c.Born >= def.TelegraphSeconds + def.LeaveSeconds);
            bool inside = false;
            float mb = DataBoot.Data.Balance.Stats.MoveBase;
            foreach (var c in _clouds)
            {
                float since = now - c.Born;
                if (!CloudSeaGimmick.Covering(def, since)) continue;
                if (CloudSeaGimmick.Hides(def, CloudSeaGimmick.Drift(def, c.At, since, mb, k), wp, false, k)) { inside = true; break; }
            }
            if (inside) { if (!_decoy.HasValue) _decoy = p; }
            else _decoy = null;
        }

        void LateUpdate()
        {
            if (_mat == null || _clouds.Count == 0 || runner == null || runner.Life == null) return;
            var life = runner.Life;
            var def = DataBoot.Data.Gimmicks.CloudSea;
            float now = life.Minutes * 60f, k = life.Scale.Factor(life.Realm), mb = DataBoot.Data.Balance.Stats.MoveBase;
            foreach (var c in _clouds)
            {
                float since = now - c.Born;
                // 피어남(예고 동안 0 → 1), 머묾, 끝 1초 동안 걷힘
                float bloom = Mathf.Clamp01(since / Mathf.Max(0.01f, def.TelegraphSeconds));
                float fade = Mathf.Clamp01((def.TelegraphSeconds + def.LeaveSeconds - since) / 1f);
                float a = Mathf.Min(bloom, fade);
                var u = WorldCoords.ToUnity(CloudSeaGimmick.Drift(def, c.At, since, mb, k));
                float d = def.Radius * k * 2f * (0.75f + 0.25f * bloom);
                float wob = Mathf.Sin(Time.time * 0.6f + c.Seed) * 4f;
                Draw(_rimMat, new Color(0.55f, 0.63f, 0.68f, 0.55f * a), u, d * 1.12f, c.Seed * 40f + wob);
                Draw(_mat, new Color(0.97f, 0.98f, 0.99f, (CloudSeaGimmick.Covering(def, since) ? 0.82f : 0.5f) * a), u, d, -c.Seed * 30f - wob);
                Draw(_mat, new Color(1f, 1f, 1f, 0.55f * a), u + new Vector2(Mathf.Cos(c.Seed), Mathf.Sin(c.Seed)) * d * 0.18f, d * 0.7f, c.Seed * 70f + wob * 2f);
            }
        }

        void Draw(Material m, Color tint, Vector2 at, float size, float angle)
        {
            _mpb.Clear();
            _mpb.SetVector(IdTint, new Vector4(tint.r, tint.g, tint.b, tint.a));
            _mpb.SetFloat(IdDissolve, 0f);
            var mat = Matrix4x4.TRS(new Vector3(at.x, at.y, -0.2f), Quaternion.Euler(0f, 0f, angle), new Vector3(size, size, 1f));
            var rp = new RenderParams(m) { matProps = _mpb, layer = gameObject.layer, worldBounds = new Bounds(mat.GetColumn(3), new Vector3(size, size, 1f)) };
            Graphics.RenderMesh(rp, _quad, 0, mat);
        }

        static bool HereHas(BloodRain.Core.Run.Life life, string gimmick)
        {
            foreach (var r in life.Regions.All)
                if (r.Id == life.RegionId) return r.Gimmicks != null && r.Gimmicks.Contains(gimmick);
            return false;
        }

        static Mesh MakeQuad()
        {
            var m = new Mesh { name = "구름 판" };
            m.SetVertices(new List<Vector3> { new Vector3(-.5f, -.5f), new Vector3(-.5f, .5f), new Vector3(.5f, .5f), new Vector3(.5f, -.5f) });
            m.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) });
            m.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            return m;
        }

        // 구름 덩이(128px): 둥근 덩이 일곱을 겹친 부드러운 흰 얼룩, 가장자리는 잡음으로 들쭉날쭉
        static Texture2D MakeCloud()
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "구름 덩이" };
            var px = new Color32[n * n];
            var rng = new System.Random(77);
            var blobs = new List<Vector3>();
            blobs.Add(new Vector3(0f, 0f, 0.55f));
            for (int i = 0; i < 6; i++)
            {
                float ang = i / 6f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.6f, r = 0.3f + (float)rng.NextDouble() * 0.12f;
                blobs.Add(new Vector3(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r, 0.28f + (float)rng.NextDouble() * 0.12f));
            }
            for (int yy = 0; yy < n; yy++)
            for (int xx = 0; xx < n; xx++)
            {
                float x = (xx + 0.5f) / n * 2f - 1f, y = (yy + 0.5f) / n * 2f - 1f;
                float v = 0f;
                foreach (var b in blobs)
                {
                    float dx = x - b.x, dy = y - b.y, q = 1f - (dx * dx + dy * dy) / (b.z * b.z);
                    if (q > 0f) v += q * q;
                }
                float noise = Mathf.PerlinNoise(xx * 0.09f, yy * 0.09f) * 0.35f;
                float a = Mathf.Clamp01((v - 0.15f - noise * 0.4f) * 2.2f);
                px[yy * n + xx] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
