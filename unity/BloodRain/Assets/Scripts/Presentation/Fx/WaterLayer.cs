using System.Collections.Generic;
using BloodRain.Core.Data;
using BloodRain.Core.World;
using BloodRain.Sim;
using UnityEngine;

namespace BloodRain.Presentation.Fx
{
    // RG-04U 장강 수로 물길과 떠내려오는 배(2026-11-26 PC). 자리는 Core GimmickLayout 굽이 띠(InWater·BandCenterY·BandRows)·
    //   굽이 배(CurvedBoats·OnCurvedBoat) — 웹 합친 판 inWater·drawWaterways·boatsW 와 같은 셈. 값은 gimmicks.json layout × 그 경지 축척.
    //   그림: 띠 가운데 줄을 x 로 훑어 띠 모양 메시를 만들고 Resources/Art/Tiles/tile_changjiang_water 를 세계 좌표 무늬로 깐다.
    //   그 아래 조금 넓은 먹 띠 한 겹이 물가 테. 그리는 차례는 바닥(2980~2984)과 소품(2990) 사이.
    //   감속: 물 안이면 플레이어 걸음 × water.moveSpeedMultiplier(수로채 성질·수상비·배 위는 1), 걷는 적은 같은 값만큼 Slow 를 계속 건다.
    //   역류 파도·마천패 물기둥은 아직 — unity_tasks RG-04U ③④.
    public sealed class WaterLayer : MonoBehaviour
    {
        public LifeRunner runner;

        const float TileMeters = 10f;     // 물 원화 한 장이 덮는 길이(m)
        const float WaterAlpha = 0.9f;
        static readonly string[] BoatArt = { "prop_covered_boat", "prop_cargo_raft" };

        Material _waterMat, _edgeMat;
        Mesh _water, _edge;
        MaterialPropertyBlock _mpb;
        static readonly int IdTint = Shader.PropertyToID("_Tint");
        static readonly int IdDissolve = Shader.PropertyToID("_Dissolve");

        int _realm = -1;
        Vector2 _laidAt;
        float _check;
        bool _any;            // 둘레에 물길이 있나
        WaterWalk _walk;
        PlayerController _pc;
        readonly List<GameObject> _boats = new List<GameObject>();
        readonly List<(System.Numerics.Vector2 Pos, float Angle, System.Numerics.Vector2 Vel)> _boatList = new List<(System.Numerics.Vector2, float, System.Numerics.Vector2)>();

        /// <summary>플레이어가 지금 물 위인가·배 위인가(시험·HUD 용).</summary>
        public bool PlayerInWater { get; private set; }
        public bool PlayerOnBoat { get; private set; }
        public int BoatCount => _boatList.Count;
        public int SlowedEnemies { get; private set; }

        void Start()
        {
            var map = FindFirstObjectByType<MapLayer>();
            if (map == null || map.material == null) { enabled = false; return; }
            var tex = Resources.Load<Texture2D>("Art/Tiles/tile_changjiang_water");
            if (tex != null) tex.wrapMode = TextureWrapMode.Repeat;
            _waterMat = new Material(map.material) { name = "장강 물길", renderQueue = 2987, mainTexture = tex != null ? tex : Texture2D.whiteTexture };
            _edgeMat = new Material(map.material) { name = "장강 물가 테", renderQueue = 2986, mainTexture = Texture2D.whiteTexture };
            _water = new Mesh { name = "물길" }; _water.MarkDynamic();
            _edge = new Mesh { name = "물가" }; _edge.MarkDynamic();
            _mpb = new MaterialPropertyBlock();
            _walk = new WaterWalk(DataBoot.Data.Gimmicks.Water);
            _pc = runner != null && runner.player != null ? runner.player.GetComponent<PlayerController>() : null;
            if (_pc != null) _pc.Dashed += OnDash;
        }

        void OnDestroy()
        {
            if (_pc != null) { _pc.Dashed -= OnDash; _pc.TerrainMultiplier = 1f; }
            foreach (var b in _boats) if (b != null) Destroy(b);
        }

        void OnDash() => _walk?.OnDash();

        void Update()
        {
            if (runner == null || runner.Life == null || runner.Life.Record != null || runner.player == null || _water == null) return;
            var ws = WorldScale.Current;
            if (ws == null) return;
            var life = runner.Life;
            var data = DataBoot.Data;
            var g = data.Gimmicks;
            Vector2 p = runner.player.position;
            float k = life.Scale.Factor(life.Realm);
            float view = ws.Scale.ViewDiagonal(ws.Level);
            _check -= Time.unscaledDeltaTime;
            if (life.Realm != _realm || ((p - _laidAt).magnitude > view * 0.25f && _check <= 0f))
            {
                _check = 0.3f;
                _realm = life.Realm; _laidAt = p;
                Build(life, g, k, p, view * 0.9f);
            }

            var wp = WorldCoords.FromUnity(p);
            _walk.Tick(Time.deltaTime);
            TickBoats(life, g, k, wp, view * 0.8f);
            PlayerInWater = _any && GimmickLayout.InWater(life.Regions, g, k, wp, life.SealStones);
            PlayerOnBoat = false;
            if (PlayerInWater)
                foreach (var b in _boatList)
                    if (GimmickLayout.OnCurvedBoat(g.DriftingBoats, b.Pos, b.Angle, wp, k)) { PlayerOnBoat = true; break; }
            if (_pc != null)
                _pc.TerrainMultiplier = WaterGimmick.MoveSpeedMultiplier(g.Water, PlayerInWater && !PlayerOnBoat, false, _walk.Active, life.Body.SectTrait.IgnoresWater);
            SlowEnemies(life, g, k);
        }

        void LateUpdate()
        {
            if (!_any || _water == null) return;
            Draw(_edgeMat, _edge, new Color(0.16f, 0.2f, 0.21f, 0.32f));
            Draw(_waterMat, _water, new Color(1f, 1f, 1f, WaterAlpha));
        }

        void Draw(Material m, Mesh mesh, Color tint)
        {
            _mpb.Clear();
            _mpb.SetVector(IdTint, new Vector4(tint.r, tint.g, tint.b, tint.a));
            _mpb.SetFloat(IdDissolve, 0f);
            var rp = new RenderParams(m) { matProps = _mpb, layer = gameObject.layer, worldBounds = new Bounds(Vector3.zero, new Vector3(1e7f, 1e7f, 10f)) };
            Graphics.RenderMesh(rp, mesh, 0, Matrix4x4.Translate(new Vector3(0f, 0f, 0.55f)));
        }

        // 띠 모양 메시를 새로 짓는다. 지역마다 물(gimmicks water)이 있으면 그 원점의 띠 j 를 BandRows 로 고르고,
        // x 를 잘게 훑어 가운데 줄 위아래 half 만큼 사각을 잇는다. 그 칸 가운데가 InWater 가 아니면(다른 지역 땅) 건너뜀.
        void Build(BloodRain.Core.Run.Life life, GimmicksDef g, float k, Vector2 p, float radius)
        {
            var L = g.Layout;
            var regions = life.Regions;
            var around = WorldCoords.FromUnity(p);
            float sp = L.WaterSpacing * k, half = L.WaterHalf * k, W = L.WaterWave * k, WL = L.WaterWaveLen * k;
            var wv = new List<Vector3>(); var wu = new List<Vector2>(); var wt = new List<int>();
            var ev = new List<Vector3>(); var eu = new List<Vector2>(); var et = new List<int>();
            _any = false;
            if (sp > 0f && WL > 0f && half > 0f)
            {
                float step = Mathf.Max(half * 0.35f, radius / 160f);
                foreach (var reg in regions.All)
                {
                    if (reg.Gimmicks == null || !reg.Gimmicks.Contains(g.Water.Id)) continue;
                    var o = regions.Center(reg);
                    var (j0, j1) = GimmickLayout.BandRows(o, sp, around, radius + W + half);
                    if (j1 - j0 > 400) continue;
                    for (int j = j0; j <= j1; j++)
                    for (float x = around.X - radius; x < around.X + radius; x += step)
                    {
                        float xm = x + step * 0.5f;
                        float ym = GimmickLayout.BandCenterY(o, j, sp, W, WL, xm);
                        if (Mathf.Abs(ym - around.Y) > radius + half) continue;
                        if (!GimmickLayout.InWater(regions, g, k, new System.Numerics.Vector2(xm, ym), life.SealStones)) continue;
                        float y0 = GimmickLayout.BandCenterY(o, j, sp, W, WL, x), y1 = GimmickLayout.BandCenterY(o, j, sp, W, WL, x + step);
                        Quad(wv, wu, wt, x, y0, x + step * 1.02f, y1, half, true);
                        Quad(ev, eu, et, x, y0, x + step * 1.02f, y1, half * 1.14f, false);
                        _any = true;
                    }
                }
            }
            Fill(_water, wv, wu, wt);
            Fill(_edge, ev, eu, et);
        }

        static void Quad(List<Vector3> v, List<Vector2> u, List<int> t, float xa, float ya, float xb, float yb, float half, bool tiled)
        {
            int i = v.Count;
            var a0 = WorldCoords.ToUnity(new System.Numerics.Vector2(xa, ya - half));
            var a1 = WorldCoords.ToUnity(new System.Numerics.Vector2(xa, ya + half));
            var b0 = WorldCoords.ToUnity(new System.Numerics.Vector2(xb, yb - half));
            var b1 = WorldCoords.ToUnity(new System.Numerics.Vector2(xb, yb + half));
            v.Add(a0); v.Add(a1); v.Add(b1); v.Add(b0);
            if (tiled) { u.Add(a0 / TileMeters); u.Add(a1 / TileMeters); u.Add(b1 / TileMeters); u.Add(b0 / TileMeters); }
            else { u.Add(Vector2.zero); u.Add(Vector2.zero); u.Add(Vector2.zero); u.Add(Vector2.zero); }
            t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }

        static void Fill(Mesh m, List<Vector3> v, List<Vector2> u, List<int> t)
        {
            m.Clear();
            m.indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            m.SetVertices(v); m.SetUVs(0, u); m.SetTriangles(t, 0, false);
            m.bounds = new Bounds(Vector3.zero, new Vector3(1e7f, 1e7f, 1f));
        }

        // 배: 물이 있는 지역마다 CurvedBoats 로 자리·기울기를 받아 소품 원화를 옮겨 둔다(모자라면 더 만들고 남으면 숨김).
        void TickBoats(BloodRain.Core.Run.Life life, GimmicksDef g, float k, System.Numerics.Vector2 around, float radius)
        {
            _boatList.Clear();
            if (_any)
                foreach (var reg in life.Regions.All)
                {
                    if (reg.Gimmicks == null || !reg.Gimmicks.Contains(g.Water.Id) || !reg.Gimmicks.Contains(g.DriftingBoats.Id)) continue;
                    _boatList.AddRange(GimmickLayout.CurvedBoats(life.Regions, g, reg, k, life.Minutes * 60f, around, radius, life.SealStones));
                }
            float len = g.DriftingBoats.Length * k;
            for (int i = 0; i < _boatList.Count; i++)
            {
                if (i >= _boats.Count)
                {
                    var go = PropArt.Make(transform, "배", BoatArt[i % BoatArt.Length], Vector2.zero, len, 4);
                    if (go == null) break;
                    _boats.Add(go);
                }
                var b = _boats[i];
                var at = WorldCoords.ToUnity(_boatList[i].Pos);
                b.SetActive(true);
                b.transform.position = new Vector3(at.x, at.y, 0f);
                b.transform.rotation = Quaternion.Euler(0f, 0f, -_boatList[i].Angle * Mathf.Rad2Deg); // 세계 y 가 아래로라 기울기 부호가 바뀜
                var sr = b.GetComponent<SpriteRenderer>();
                if (sr != null) PropArt.Fit(b.transform, sr.sprite, len);
            }
            for (int i = _boatList.Count; i < _boats.Count; i++) if (_boats[i].activeSelf) _boats[i].SetActive(false);
        }

        // 걷는 적: 물 안이면 걸음 × moveSpeedMultiplier 만큼 Slow 를 건다(Slow 는 초당 풀리니 프레임마다 다시). 나는 적은 그대로.
        void SlowEnemies(BloodRain.Core.Run.Life life, GimmicksDef g, float k)
        {
            SlowedEnemies = 0;
            var field = runner.enemies;
            if (!_any || field == null || field.Sim == null) return;
            float slow = 1f - g.Water.MoveSpeedMultiplier;
            if (slow <= 0f) return;
            var sim = field.Sim;
            for (int i = 0; i < sim.Count; i++)
            {
                var a = sim.Get(i);
                if (a.Slow >= slow) continue;
                var def = field.DefOf(a.Kind);
                if (def != null && def.IgnoresTerrain && g.Water.FlyingIgnores) continue;
                var at = WorldCoords.FromUnity(new Vector2(a.Pos.x, a.Pos.y));
                if (!GimmickLayout.InWater(life.Regions, g, k, at, life.SealStones)) continue;
                a.Slow = slow;
                sim.Set(i, a);
                SlowedEnemies++;
            }
        }
    }
}
