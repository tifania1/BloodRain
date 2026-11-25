using System.Collections.Generic;
using BloodRain.Core.Data;
using BloodRain.Core.World;
using BloodRain.Sim;
using UnityEngine;

namespace BloodRain.Presentation.Fx
{
    // RG-05U 무당산 태극 바닥(2026-11-26 PC). 자리는 Core GimmickLayout.StaggeredNear(16m 엇갈린 격자, 한가운데 하나 — 웹 taijiPads).
    //   칸마다 Core TaijiFloorGimmick 하나(열쇠 = 지역 + 칸 I·J, 경지가 바뀌면 새로 — 쿨다운을 칸으로 셈). 플레이어가 touchRadius 안에 들면 TryStep —
    //   음양 게이지와 반대 속성 무공 피해 +30% 를 30초(TaijiBlessing). WeaponRunner.AspectDamage 가 DamageMultiplier 를 곱한다.
    //   그림은 코드로 그린 태극 문양(먹·선지)을 바닥 차례(물길 다음 2988, 소품·적 아래)에 RenderMesh 로. 쿨다운 동안 옅고 천천히 돎,
    //   밟는 순간 한 번 커졌다 돌아옴.
    public sealed class TaijiLayer : MonoBehaviour
    {
        public LifeRunner runner;

        sealed class Pad { public TaijiFloorGimmick G; public Vector2 At; public float Angle; public float Pulse; }

        readonly Dictionary<long, Pad> _pads = new Dictionary<long, Pad>();
        static TaijiBlessing _blessing;
        Material _mat;
        Mesh _quad;
        MaterialPropertyBlock _mpb;
        static readonly int IdTint = Shader.PropertyToID("_Tint");
        static readonly int IdDissolve = Shader.PropertyToID("_Dissolve");
        int _realm = -1;
        Vector2 _laidAt;
        float _check;

        public int PadCount => _pads.Count;
        public int Steps { get; private set; }
        /// <summary>지금 걸린 기운(없으면 null). 시험·HUD 용.</summary>
        public static TaijiBlessing Blessing => _blessing;

        /// <summary>무공 속성 피해 배율(태극 바닥 기운). 기운이 없으면 1.</summary>
        public static float DamageMultiplier(YinYang aspect) => _blessing != null ? _blessing.DamageMultiplier(aspect) : 1f;

        void Start()
        {
            _blessing = new TaijiBlessing(DataBoot.Data.Gimmicks.TaijiFloor);
            var map = FindFirstObjectByType<MapLayer>();
            if (map == null || map.material == null) { enabled = false; return; }
            _mat = new Material(map.material) { name = "태극 바닥", renderQueue = 2988, mainTexture = MakeTaiji() };
            _quad = MakeQuad();
            _mpb = new MaterialPropertyBlock();
        }

        void OnDestroy() { _blessing = null; _pads.Clear(); }

        void Update()
        {
            if (runner == null || runner.Life == null || runner.Life.Record != null || runner.player == null || _mat == null) return;
            var ws = WorldScale.Current;
            if (ws == null) return;
            var life = runner.Life;
            float dt = Time.deltaTime;
            _blessing.Tick(dt);
            Vector2 p = runner.player.position;
            float k = life.Scale.Factor(life.Realm);
            float view = ws.Scale.ViewDiagonal(ws.Level);
            _check -= Time.unscaledDeltaTime;
            if (life.Realm != _realm || ((p - _laidAt).magnitude > view * 0.3f && _check <= 0f))
            {
                _check = 0.5f;
                Lay(life, k, p, view * 1.2f);
            }
            if (_pads.Count == 0) return;
            float g = life.Loadout.Gauge.Value;
            int sign = g > 0.001f ? 1 : g < -0.001f ? -1 : 0;
            foreach (var pad in _pads.Values)
            {
                pad.G.Tick(dt);
                if (pad.G.TryStep((pad.At - p).magnitude, sign, _blessing)) { pad.Pulse = 1f; Steps++; }
                pad.Pulse = Mathf.Max(0f, pad.Pulse - dt * 2.5f);
                pad.Angle += dt * (pad.G.Ready ? 12f : 3f);
            }
        }

        void LateUpdate()
        {
            if (_mat == null || _pads.Count == 0) return;
            foreach (var pad in _pads.Values)
            {
                float d = pad.G.TouchRadius * 2f * (1f + 0.25f * pad.Pulse);
                float a = pad.G.Ready ? 0.5f : 0.16f + 0.5f * pad.Pulse;
                _mpb.Clear();
                _mpb.SetVector(IdTint, new Vector4(1f, 1f, 1f, a));
                _mpb.SetFloat(IdDissolve, 0f);
                var at = Matrix4x4.TRS(new Vector3(pad.At.x, pad.At.y, 0.54f), Quaternion.Euler(0f, 0f, -pad.Angle), new Vector3(d, d, 1f));
                var rp = new RenderParams(_mat) { matProps = _mpb, layer = gameObject.layer, worldBounds = new Bounds(at.GetColumn(3), new Vector3(d, d, 1f)) };
                Graphics.RenderMesh(rp, _quad, 0, at);
            }
        }

        void Lay(BloodRain.Core.Run.Life life, float k, Vector2 p, float radius)
        {
            if (life.Realm != _realm) _pads.Clear();
            _realm = life.Realm; _laidAt = p;
            var data = DataBoot.Data;
            var def = data.Gimmicks.TaijiFloor;
            var around = WorldCoords.FromUnity(p);
            var want = new HashSet<long>();
            foreach (var reg in life.Regions.All)
            {
                if (reg.Gimmicks == null || !reg.Gimmicks.Contains(def.Id)) continue;
                foreach (var (pos, i, j) in GimmickLayout.StaggeredNear(life.Regions, reg, data.Gimmicks.Layout.TaijiSpacing * k, around, radius, life.SealStones))
                {
                    long key = ((long)(i & 0xFFFFF) << 20 | (long)(j & 0xFFFFF)) ^ ((long)reg.Id.GetHashCode() << 40);
                    want.Add(key);
                    if (_pads.ContainsKey(key)) continue;
                    _pads[key] = new Pad { G = new TaijiFloorGimmick(def, k), At = WorldCoords.ToUnity(pos) };
                }
            }
            // 멀어진 칸은 지운다(쿨다운도 같이 — 화면 밖 칸을 오래 들고 있지 않음)
            var drop = new List<long>();
            foreach (var kv in _pads) if (!want.Contains(kv.Key)) drop.Add(kv.Key);
            foreach (var key in drop) _pads.Remove(key);
        }

        static Mesh MakeQuad()
        {
            var m = new Mesh { name = "태극 판" };
            m.SetVertices(new List<Vector3> { new Vector3(-.5f, -.5f), new Vector3(-.5f, .5f), new Vector3(.5f, .5f), new Vector3(.5f, -.5f) });
            m.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) });
            m.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            return m;
        }

        // 먹·선지 태극 문양(256px). 바깥 테는 먹, 반은 먹·반은 선지, 작은 눈 둘
        static Texture2D MakeTaiji()
        {
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "태극 문양" };
            var px = new Color32[n * n];
            var ink = new Color(0.13f, 0.12f, 0.11f, 1f);
            var paper = new Color(0.93f, 0.9f, 0.83f, 1f);
            float aa = 2f / n;
            for (int yy = 0; yy < n; yy++)
            for (int xx = 0; xx < n; xx++)
            {
                float x = (xx + 0.5f) / n * 2f - 1f, y = (yy + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(x * x + y * y);
                float a = Mathf.Clamp01((1f - r) / aa);
                if (a <= 0f) { px[yy * n + xx] = new Color32(0, 0, 0, 0); continue; }
                bool dark = x < 0f;
                float up = x * x + (y - 0.5f) * (y - 0.5f), dn = x * x + (y + 0.5f) * (y + 0.5f);
                if (up < 0.25f) dark = false;
                if (dn < 0.25f) dark = true;
                if (up < 0.0144f) dark = true;
                if (dn < 0.0144f) dark = false;
                if (r > 0.93f) dark = true; // 테
                var c = dark ? ink : paper;
                c.a = a;
                px[yy * n + xx] = c;
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
