using System.Collections.Generic;
using BloodRain.Core.Data;
using BloodRain.Core.World;
using BloodRain.Sim;
using UnityEngine;

namespace BloodRain.Presentation.Fx
{
    // R4-13c ③ 곤륜 빙판(2026-11-29 PC). 자리는 Core GimmickLayout 굽이 띠(InIce·BandCenterY·BandRows) — 장강 물길(WaterLayer)과 같은 꼴,
    //   값은 gimmicks.json layout ice… × 그 경지 축척. 그림은 웹 drawIce 처럼 푸른 얼음 띠(반폭 전체, 옅은 하늘빛) 위에 가운데 흰 빛 한 줄.
    //   미끄러짐: 빙판 위면 PlayerController.Ice 에 iceFloor 를 넣어 걸음 속도가 Core IceFloorGimmick.Step(inertiaSeconds 0.6)으로 바뀐다.
    //   걷는 적의 미끄러짐은 아직(적 Sim 에 지난 속도 칸이 없음) — unity_tasks R4-13c 줄.
    public sealed class IceLayer : MonoBehaviour
    {
        public LifeRunner runner;

        Material _mat;
        Mesh _band, _shine;
        MaterialPropertyBlock _mpbA, _mpbB;
        static readonly int IdTint = Shader.PropertyToID("_Tint");
        static readonly int IdDissolve = Shader.PropertyToID("_Dissolve");

        int _realm = -1;
        Vector2 _laidAt;
        float _check;
        bool _any;
        PlayerController _pc;

        /// <summary>시험용: 플레이어가 지금 빙판 위인가, 빙판 위에 있던 초.</summary>
        public bool PlayerOnIce { get; private set; }
        public float IceSeconds { get; private set; }

        void Start()
        {
            var map = FindFirstObjectByType<MapLayer>();
            if (map == null || map.material == null) { enabled = false; return; }
            _mat = new Material(map.material) { name = "곤륜 빙판", renderQueue = 2987, mainTexture = Texture2D.whiteTexture };
            _band = new Mesh { name = "빙판" }; _band.MarkDynamic();
            _shine = new Mesh { name = "빙판 빛" }; _shine.MarkDynamic();
            _mpbA = new MaterialPropertyBlock(); _mpbB = new MaterialPropertyBlock();
            _pc = runner != null && runner.player != null ? runner.player.GetComponent<PlayerController>() : null;
        }

        void OnDestroy()
        {
            if (_pc != null) _pc.Ice = null;
        }

        void Update()
        {
            if (runner == null || runner.Life == null || runner.Life.Record != null || runner.player == null || _band == null) return;
            var ws = WorldScale.Current;
            if (ws == null) return;
            var life = runner.Life;
            var g = DataBoot.Data.Gimmicks;
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
            PlayerOnIce = _any && GimmickLayout.InIce(life.Regions, g, k, WorldCoords.FromUnity(p), life.SealStones);
            if (PlayerOnIce) IceSeconds += Time.deltaTime;
            if (_pc != null) _pc.Ice = PlayerOnIce ? g.IceFloor : null;
        }

        void LateUpdate()
        {
            if (_band == null || !_any) return;
            Draw(_mpbA, _band, new Color(0.69f, 0.81f, 0.88f, 0.42f), 0.55f);
            Draw(_mpbB, _shine, new Color(0.94f, 0.97f, 0.99f, 0.35f), 0.54f);
        }

        void Draw(MaterialPropertyBlock mpb, Mesh mesh, Color tint, float z)
        {
            mpb.Clear();
            mpb.SetVector(IdTint, new Vector4(tint.r, tint.g, tint.b, tint.a));
            mpb.SetFloat(IdDissolve, 0f);
            var rp = new RenderParams(_mat) { matProps = mpb, layer = gameObject.layer, worldBounds = new Bounds(Vector3.zero, new Vector3(1e7f, 1e7f, 10f)) };
            Graphics.RenderMesh(rp, mesh, 0, Matrix4x4.Translate(new Vector3(0f, 0f, z)));
        }

        // WaterLayer.Build 와 같은 길: 빙판이 있는 지역마다 띠 j 를 BandRows 로 고르고 x 를 잘게 훑어 가운데 줄 위아래로 사각을 잇는다.
        void Build(BloodRain.Core.Run.Life life, GimmicksDef g, float k, Vector2 p, float radius)
        {
            var L = g.Layout;
            var regions = life.Regions;
            var around = WorldCoords.FromUnity(p);
            float sp = L.IceSpacing * k, half = L.IceHalf * k, W = L.IceWave * k, WL = L.IceWaveLen * k;
            var bv = new List<Vector3>(); var bt = new List<int>();
            var sv = new List<Vector3>(); var st = new List<int>();
            _any = false;
            if (sp > 0f && WL > 0f && half > 0f)
            {
                float step = Mathf.Max(half * 0.35f, radius / 160f);
                foreach (var reg in regions.All)
                {
                    if (reg.Gimmicks == null || !reg.Gimmicks.Contains(g.IceFloor.Id)) continue;
                    var o = regions.Center(reg);
                    var (j0, j1) = GimmickLayout.BandRows(o, sp, around, radius + W + half);
                    if (j1 - j0 > 400) continue;
                    for (int j = j0; j <= j1; j++)
                    for (float x = around.X - radius; x < around.X + radius; x += step)
                    {
                        float xm = x + step * 0.5f;
                        float ym = GimmickLayout.BandCenterY(o, j, sp, W, WL, xm);
                        if (Mathf.Abs(ym - around.Y) > radius + half) continue;
                        if (!GimmickLayout.InIce(regions, g, k, new System.Numerics.Vector2(xm, ym), life.SealStones)) continue;
                        float y0 = GimmickLayout.BandCenterY(o, j, sp, W, WL, x), y1 = GimmickLayout.BandCenterY(o, j, sp, W, WL, x + step);
                        Quad(bv, bt, x, y0, x + step * 1.02f, y1, half);
                        Quad(sv, st, x, y0, x + step * 1.02f, y1, half * 0.25f);
                        _any = true;
                    }
                }
            }
            Fill(_band, bv, bt);
            Fill(_shine, sv, st);
        }

        static void Quad(List<Vector3> v, List<int> t, float xa, float ya, float xb, float yb, float half)
        {
            int i = v.Count;
            v.Add(WorldCoords.ToUnity(new System.Numerics.Vector2(xa, ya - half)));
            v.Add(WorldCoords.ToUnity(new System.Numerics.Vector2(xa, ya + half)));
            v.Add(WorldCoords.ToUnity(new System.Numerics.Vector2(xb, yb + half)));
            v.Add(WorldCoords.ToUnity(new System.Numerics.Vector2(xb, yb - half)));
            t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }

        static void Fill(Mesh m, List<Vector3> v, List<int> t)
        {
            m.Clear();
            m.indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            var uv = new List<Vector2>(v.Count);
            for (int i = 0; i < v.Count; i++) uv.Add(Vector2.zero);
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0, false);
            m.bounds = new Bounds(Vector3.zero, new Vector3(1e7f, 1e7f, 1f));
        }
    }
}
