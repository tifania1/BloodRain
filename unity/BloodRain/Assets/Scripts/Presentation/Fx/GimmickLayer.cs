using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.World;
using BloodRain.Sim;
using Unity.Mathematics;
using UnityEngine;

namespace BloodRain.Presentation.Fx
{
    // GM-01U 지역 기믹을 땅에 깐다(2026-11-20, 자리·개수는 R-10 '표대로' gimmicks.json layout, 놓는 함수 Core GimmickLayout).
    //   지금은 숭산 둘: 범종(30m 격자, 한가운데 하나, 닿으면 둘레 적 기절, 60초에 한 번)과 탑림(60m 격자 반 칸 비킴, 돌탑 무더기가 걷는 것을 막음).
    //   축척은 지금 경지(돌파하면 새로 깐다). 플레이어 둘레 화면 대각선 1.2배 안 칸만 만들고, 멀어지면 지운다.
    //   막힘: 플레이어는 PlayerController 가 Resolve 를 부르고, 적은 EnemyField 가 Step 뒤 PushAgents 를 부른다. 나는 적(ignoresTerrain)은 지나간다.
    //   그림은 PropArt(Art/Props/Parts) 의 돌탑·범종 원화, 없으면 먹 동그라미.
    public sealed class GimmickLayer : MonoBehaviour
    {
        public LifeRunner runner;

        static GimmickLayer _instance;
        public static GimmickLayer Instance => _instance;

        static readonly string[] PagodaArt = { "prop_pagoda_seven", "prop_pagoda_three", "prop_pagoda_tall", "prop_pagoda_mossy", "prop_pagoda_broken" };

        sealed class Bell { public BellGimmick G; public Vector2 At; public GameObject Go; public float Flash; }
        // 돌탑·대나무 포기 하나. 대나무는 베이면 Up 이 거짓이 되고(막지 않음) RegrowAt(한 생 초) 에 다시 선다(RG-03U)
        sealed class Stalk { public Vector2 At; public float R; public bool Bamboo; public bool Up = true; public int Hits; public float RegrowAt; public GameObject Go; public Vector3 Scale; public Vector3 Pos; }
        sealed class Patch { public Vector2 At; public bool Bamboo; public readonly List<Stalk> Stones = new List<Stalk>(); public readonly List<GameObject> Gos = new List<GameObject>(); }

        readonly Dictionary<long, Bell> _bells = new Dictionary<long, Bell>();
        readonly Dictionary<long, Patch> _patches = new Dictionary<long, Patch>();
        // 돌탑 공간 칸(막힘 판정이 둘레 칸만 보게)
        readonly Dictionary<long, List<Stalk>> _grid = new Dictionary<long, List<Stalk>>();
        float _cell = 4f;
        int _realm = -1;
        string _region = "";
        Vector2 _laidAt;
        float _check;
        Material _ringMat;

        public int BellCount => _bells.Count;
        public int StoneCount { get; private set; }
        public int Rings { get; private set; }

        void Awake() { _instance = this; }
        void OnDestroy() { if (_instance == this) _instance = null; ClearAll(); }

        void Update()
        {
            if (runner == null || runner.Life == null || runner.Life.Record != null || runner.player == null) return;
            var ws = WorldScale.Current;
            if (ws == null) return;
            var life = runner.Life;
            Vector2 p = runner.player.position;
            _check -= Time.unscaledDeltaTime;
            float view = ws.Scale.ViewDiagonal(ws.Level); // 지금 화면 대각선(세계 m)
            bool moved = (p - _laidAt).magnitude > view * 0.3f;
            if (life.Realm != _realm || life.RegionId != _region || (moved && _check <= 0f))
            {
                _check = 0.5f;
                Lay(life, p, view * 1.2f);
            }
            TickBells(Time.deltaTime, p);
            TickPlums(Time.deltaTime, p);
            TickFires(life, Time.deltaTime, p);
            TickBamboo(life.Minutes * 60f);
            TickFog(life, p, Time.deltaTime);
            TickAltars(life, p, view);
        }

        void Lay(BloodRain.Core.Run.Life life, Vector2 p, float radius)
        {
            if (life.Realm != _realm) ClearAll(); // 축척이 바뀌면 모두 새로
            _realm = life.Realm; _region = life.RegionId; _laidAt = p;
            float k = life.Scale.Factor(life.Realm);
            var data = DataBoot.Data;
            var lay = data.Gimmicks.Layout;
            var regions = life.Regions;
            var around = WorldCoords.FromUnity(p);
            var wantBells = new HashSet<long>();
            var wantPatches = new HashSet<long>();
            var wantPlums = new HashSet<long>();
            var wantFires = new HashSet<long>();
            _cell = Mathf.Max(1f, data.Gimmicks.PagodaForest.Spacing * k * 1.5f);
            foreach (var reg in regions.All)
            {
                if (reg.Gimmicks == null) continue;
                bool bell = reg.Gimmicks.Contains("bell"), pagoda = reg.Gimmicks.Contains("pagoda_forest"), bamboo = reg.Gimmicks.Contains("bamboo_thicket");
                if (bamboo)
                {
                    // RG-03U 당가타 대나무 덤불: 탑림과 같은 60m 반 칸 격자 무더기(gimmick_start_values 2절)
                    float br = data.Gimmicks.BambooThicket.PatchRadius * k;
                    foreach (var c in GimmickLayout.Near(regions, reg, lay.BambooSpacing * k, true, around, radius + br, life.SealStones))
                    {
                        long key = Key(c) ^ 0x5A5A5A5AL;
                        wantPatches.Add(key);
                        if (!_patches.ContainsKey(key)) _patches[key] = MakeBamboo(c, k, key);
                    }
                }
                if (reg.Gimmicks.Contains("plum_heaps"))
                {
                    // R4-13c 화산 매화 더미: 30m 격자(한가운데 포함)에 하나씩, 닿으면 최대 기혈 15% 회복하고 지고 90초 뒤 다시 핌
                    foreach (var c in GimmickLayout.Near(regions, reg, lay.PlumSpacing * k, false, around, radius, life.SealStones))
                    {
                        long key = Key(c) ^ 0x3C3C3C3CL;
                        wantPlums.Add(key);
                        if (!_plums.ContainsKey(key)) _plums[key] = MakePlum(c, k);
                    }
                }
                if (reg.Gimmicks.Contains("campfire"))
                {
                    // R4-13c 곤륜 모닥불: 28m 반 칸 격자(웹 campfires 와 같게 i+0.5), 곁에서 기혈이 차고 빙강시는 느려짐
                    float fr = data.Gimmicks.Campfire.Radius * k;
                    foreach (var c in GimmickLayout.Near(regions, reg, lay.CampfireSpacing * k, true, around, radius + fr, life.SealStones))
                    {
                        long key = Key(c) ^ 0x2D2D2D2DL;
                        wantFires.Add(key);
                        if (!_fires.ContainsKey(key)) _fires[key] = MakeFire(c, k);
                    }
                }
                if (!bell && !pagoda) continue;
                var patchCenters = new List<System.Numerics.Vector2>();
                if (pagoda)
                {
                    float pr = data.Gimmicks.PagodaForest.PatchRadius * k;
                    patchCenters = GimmickLayout.Near(regions, reg, lay.PagodaSpacing * k, true, around, radius / 1f + pr, life.SealStones);
                    foreach (var c in patchCenters)
                    {
                        long key = Key(c);
                        wantPatches.Add(key);
                        if (!_patches.ContainsKey(key)) _patches[key] = MakePatch(c, k, key);
                    }
                }
                if (bell)
                {
                    var cells = GimmickLayout.Near(regions, reg, lay.BellSpacing * k, false, around, radius, life.SealStones);
                    // 탑림 무더기 안 범종은 뺀다(근처 무더기가 화면 밖이어도 빠지게 무더기는 더 넓게 본다)
                    if (pagoda)
                    {
                        var allNear = GimmickLayout.Near(regions, reg, lay.PagodaSpacing * k, true, around, radius + lay.PagodaSpacing * k, life.SealStones);
                        cells = GimmickLayout.Without(cells, allNear, data.Gimmicks.PagodaForest.PatchRadius * k);
                    }
                    foreach (var c in cells)
                    {
                        long key = Key(c);
                        wantBells.Add(key);
                        if (!_bells.ContainsKey(key)) _bells[key] = MakeBell(c, k);
                    }
                }
            }
            // 멀어진 것 지우기
            var drop = new List<long>();
            foreach (var kv in _bells) if (!wantBells.Contains(kv.Key)) drop.Add(kv.Key);
            foreach (var key in drop) { if (_bells[key].Go != null) Destroy(_bells[key].Go); _bells.Remove(key); }
            drop.Clear();
            foreach (var kv in _patches) if (!wantPatches.Contains(kv.Key)) drop.Add(kv.Key);
            foreach (var key in drop) { foreach (var g in _patches[key].Gos) if (g != null) Destroy(g); _patches.Remove(key); }
            drop.Clear();
            foreach (var kv in _plums) if (!wantPlums.Contains(kv.Key)) drop.Add(kv.Key);
            foreach (var key in drop) { if (_plums[key].Go != null) Destroy(_plums[key].Go); _plums.Remove(key); }
            drop.Clear();
            foreach (var kv in _fires) if (!wantFires.Contains(kv.Key)) drop.Add(kv.Key);
            foreach (var key in drop) { foreach (var g in _fires[key].Gos) if (g != null) Destroy(g); _fires.Remove(key); }
            RebuildGrid();
        }

        // ---------------------------------------------------------------- 모닥불(R4-13c, Core CampfireGimmick)

        sealed class Fire { public Vector2 At; public readonly List<GameObject> Gos = new List<GameObject>(); }
        readonly Dictionary<long, Fire> _fires = new Dictionary<long, Fire>();
        public int FireCount => _fires.Count;
        public float FireHealed { get; private set; }
        public int FireSlowed { get; private set; }

        Fire MakeFire(System.Numerics.Vector2 c, float k)
        {
            var f = new Fire { At = WorldCoords.ToUnity(c) };
            float r = DataBoot.Data.Gimmicks.Campfire.Radius * k;
            // 온기 반경을 옅은 주황 원으로(밟으면 차는 자리가 읽히게), 그 위에 불 원화
            f.Gos.Add(Disc("모닥불 온기", f.At, r, new Color(1f, 0.62f, 0.25f, 0.13f), 1));
            var go = PropArt.Make(transform, "모닥불", "prop_campfire_ring", f.At, r * 0.75f, 3)
                  ?? Disc("모닥불", f.At, r * 0.22f, new Color(0.85f, 0.35f, 0.08f, 0.95f), 3);
            f.Gos.Add(go);
            return f;
        }

        void TickFires(BloodRain.Core.Run.Life life, float dt, Vector2 player)
        {
            if (_fires.Count == 0 || dt <= 0f) return;
            var def = DataBoot.Data.Gimmicks.Campfire;
            float k = life.Scale.Factor(life.Realm);
            var vitals = runner.vitals;
            foreach (var f in _fires.Values)
            {
                if (vitals == null) break;
                // 회복 배율(문파 성질)은 vitals.Heal 이 곱하니 1 로 넘긴다
                float h = CampfireGimmick.Heal(def, Vector2.Distance(player, f.At), vitals.MaxHealth, dt, k, 1f);
                if (h > 0f && vitals.Health < vitals.MaxHealth) FireHealed += vitals.Heal(h);
            }
            // 빙강시 계열은 곁에서 걸음 0.5배 — 쇄겸술 둔화(Slow)와 같은 칸에 덮어 두면 곁을 떠난 뒤 저절로 풀린다
            var en = runner.enemies;
            var sim = en != null ? en.Sim : null;
            if (sim == null || string.IsNullOrEmpty(def.SlowedFamily)) return;
            float want = 1f - def.SlowedMoveSpeedMultiplier;
            int n = 0;
            for (int i = 0; i < sim.Count; i++)
            {
                var a = sim.Get(i);
                if (a.Health <= 0f || a.Slow >= want) continue;
                var ed = en.DefOf(a.Kind);
                if (ed == null || ed.Family != def.SlowedFamily) continue;
                var ap = new Vector2(a.Pos.x, a.Pos.y);
                foreach (var f in _fires.Values)
                {
                    if (CampfireGimmick.EnemyMoveSpeed(def, ed.Family, Vector2.Distance(ap, f.At), k) >= 1f) continue;
                    a.Slow = want; sim.Set(i, a); n++;
                    break;
                }
            }
            FireSlowed = n;
        }

        // ---------------------------------------------------------------- 매화 더미(R4-13c, Core PlumHeapGimmick)

        sealed class Plum { public PlumHeapGimmick G; public Vector2 At; public GameObject Go; public bool Art; }
        readonly Dictionary<long, Plum> _plums = new Dictionary<long, Plum>();
        static readonly Color PlumPink = new Color(0.91f, 0.65f, 0.71f, 1f);
        public int PlumCount => _plums.Count;
        public int PlumUses { get; private set; }

        Plum MakePlum(System.Numerics.Vector2 c, float k)
        {
            var g = new PlumHeapGimmick(DataBoot.Data.Gimmicks.PlumHeaps, k);
            Vector2 at = WorldCoords.ToUnity(c);
            float r = DataBoot.Data.Gimmicks.PlumHeaps.TouchRadius * k;
            var go = PropArt.Make(transform, "매화 더미", "prop_plum_petal_heap", at, r * 2.2f, 2);
            bool art = go != null;
            if (!art) go = Disc("매화 더미", at, r * 0.8f, PlumPink, 2);
            return new Plum { G = g, At = at, Go = go, Art = art };
        }

        void TickPlums(float dt, Vector2 player)
        {
            if (_plums.Count == 0) return;
            var vitals = runner.vitals;
            foreach (var h in _plums.Values)
            {
                h.G.Tick(dt);
                if (vitals != null && h.G.Blooming)
                {
                    float heal = h.G.TryUse(Vector2.Distance(player, h.At), vitals.MaxHealth);
                    if (heal > 0f)
                    {
                        PlumUses++;
                        vitals.Heal(heal); // healingMultiplier(문파 성질)는 Heal 이 곱한다
                        var fx = SectFx.Instance;
                        if (fx != null && fx.Ready) fx.RingW(h.At, DataBoot.Data.Gimmicks.PlumHeaps.TouchRadius * h.G.Scale * 2.6f, FxRamp.Flat(PlumPink), 0.6f, 0.7f, true);
                    }
                }
                if (h.Go != null)
                {
                    // 진 뒤엔 옅게, 다시 필 때가 가까울수록 차오른다
                    var sr = h.Go.GetComponent<SpriteRenderer>();
                    if (sr == null) continue;
                    float regrow = DataBoot.Data.Gimmicks.PlumHeaps.RegrowSeconds;
                    float u = h.G.Blooming || regrow <= 0f ? 1f : 1f - h.G.RegrowRemaining / regrow;
                    var baseC = h.Art ? Color.white : PlumPink;
                    sr.color = new Color(baseC.r, baseC.g, baseC.b, h.G.Blooming ? 1f : Mathf.Lerp(0.15f, 0.45f, u));
                }
            }
        }

        static long Key(System.Numerics.Vector2 c) => ((long)Mathf.RoundToInt(c.X * 10f) << 32) ^ (uint)Mathf.RoundToInt(c.Y * 10f);

        Bell MakeBell(System.Numerics.Vector2 c, float k)
        {
            var def = DataBoot.Data.Gimmicks.Bell;
            var g = new BellGimmick(def, k);
            Vector2 at = WorldCoords.ToUnity(c);
            float size = g.TouchRadius * 1.6f;
            // 2026-11-24 사람이 종 모양으로 정함(전엔 탑 모양 prop_stupa_bell). 목록에 아직 없으면 탑으로
            var go = PropArt.Make(transform, "범종", "prop_bell", at, size, 3) ?? PropArt.Make(transform, "범종", "prop_stupa_bell", at, size, 3);
            if (go != null)
            {
                // 2026-11-23: 가운데 pivot 그림이 기믹 자리에 가운데로 앉아, 지역 한가운데에서 태어난 몸과 늘 겹쳐 보였다.
                //   그림 발밑(아래 끝에서 조금 올라온 자리)을 기믹 자리에 맞추고, TickBells 가 몸 발밑과 y 로 앞뒤를 가린다.
                var sp = go.GetComponent<SpriteRenderer>().sprite;
                float h = sp.bounds.size.y * go.transform.localScale.y;
                go.transform.position = new Vector3(at.x, at.y + h * 0.42f, 0f);
            }
            if (go == null) go = Disc("범종", at, g.TouchRadius * 0.6f, new Color(0.62f, 0.48f, 0.20f, 0.95f), 3);
            return new Bell { G = g, At = at, Go = go };
        }

        Patch MakePatch(System.Numerics.Vector2 c, float k, long key)
        {
            var def = DataBoot.Data.Gimmicks.PagodaForest;
            var patch = new Patch { At = WorldCoords.ToUnity(c) };
            var rng = new System.Random(unchecked((int)(key ^ (key >> 32))));
            // 그림만 흔드는 따로 된 난수(배치 난수를 건드리면 막힘 자리가 바뀐다). 2026-11-21: 같은 크기·같은 방향 탑이 격자로 서서 묘지 판처럼 딱딱했다
            var look = new System.Random(unchecked((int)(key ^ (key >> 32)) * 31 + 7));
            int v = 0;
            foreach (var s in PagodaForest.Layout(def, rng, k))
            {
                var at = patch.At + new Vector2(s.X, -s.Y);
                var st = new Stalk { At = at, R = s.Radius };
                patch.Stones.Add(st);
                float sz = 0.86f + 0.3f * (float)look.NextDouble();
                var go = PropArt.Make(transform, "돌탑", PagodaArt[(v++ + (int)(key & 7)) % PagodaArt.Length], at + new Vector2(0f, s.Radius * 0.9f * sz), s.Radius * 3.2f * sz, 3);
                if (go != null)
                {
                    var sr = go.GetComponent<SpriteRenderer>();
                    sr.flipX = look.NextDouble() < 0.5;
                    float g = 0.86f + 0.14f * (float)look.NextDouble();
                    sr.color = new Color(g, g * 0.99f, g * 0.97f, 1f);
                }
                if (go == null) go = Disc("돌탑", at, s.Radius, new Color(0.30f, 0.29f, 0.27f, 0.95f), 3);
                st.Go = go;
                patch.Gos.Add(go);
            }
            return patch;
        }

        // ── RG-03U 당가타 대나무 덤불(2026-11-26) ─────────────────────────
        //   탑림과 같은 무더기·같은 막힘(Resolve·PushAgents). 무공이 친 원(LanternLayer.Strike 가 같이 부름)에 든 선 포기는
        //   hitsToBreak 번에 베이고 dropChance 로 만두 하나, regrowSeconds 뒤 다시 선다. 그림은 당가타 낱장 bamboo_clump·bamboo_brush.
        static readonly string[] BambooArt = { "prop_bamboo_clump", "prop_bamboo_brush", "prop_bamboo_clump" };
        readonly System.Random _bambooRng = new System.Random(4127);
        public int BambooCut { get; private set; }

        Patch MakeBamboo(System.Numerics.Vector2 c, float k, long key)
        {
            var def = DataBoot.Data.Gimmicks.BambooThicket;
            var patch = new Patch { At = WorldCoords.ToUnity(c), Bamboo = true };
            var rng = new System.Random(unchecked((int)(key ^ (key >> 32))));
            var look = new System.Random(unchecked((int)(key ^ (key >> 32)) * 31 + 11));
            int v = 0;
            foreach (var s in BambooThicketGimmick.Layout(def, rng, k))
            {
                var at = patch.At + new Vector2(s.X, -s.Y);
                var st = new Stalk { At = at, R = s.Radius, Bamboo = true };
                patch.Stones.Add(st);
                float sz = 0.85f + 0.35f * (float)look.NextDouble();
                var go = PropArt.Make(transform, "대나무", BambooArt[(v++ + (int)(key & 3)) % BambooArt.Length], at + new Vector2(0f, s.Radius * 1.4f * sz), s.Radius * 4.2f * sz, 3);
                if (go != null)
                {
                    var sr = go.GetComponent<SpriteRenderer>();
                    sr.flipX = look.NextDouble() < 0.5;
                    // 발밑 y 로 앞뒤(몸 정렬 20 근처). 무더기 안 포기끼리도 아래 것이 앞
                    sr.sortingOrder = 3;
                }
                if (go == null) go = Disc("대나무", at, s.Radius, new Color(0.31f, 0.48f, 0.27f, 0.95f), 3);
                st.Go = go; st.Scale = go.transform.localScale; st.Pos = go.transform.position;
                patch.Gos.Add(go);
            }
            return patch;
        }

        /// <summary>무공이 이 원 안을 쳤다(LanternLayer.Strike 가 같이 부른다). 선 대나무 포기를 벤다.</summary>
        public static void Strike(Vector2 center, float radius)
        {
            var me = _instance;
            if (me == null || me._grid.Count == 0 || me.runner == null || me.runner.Life == null) return;
            var def = DataBoot.Data.Gimmicks.BambooThicket;
            float now = me.runner.Life.Minutes * 60f;
            int c0 = Mathf.FloorToInt((center.x - radius) / me._cell) - 1, c1 = Mathf.FloorToInt((center.x + radius) / me._cell) + 1;
            int r0 = Mathf.FloorToInt((center.y - radius) / me._cell) - 1, r1 = Mathf.FloorToInt((center.y + radius) / me._cell) + 1;
            if ((c1 - c0) * (r1 - r0) > 400) return; // 화면을 덮는 큰 원은 셈이 커서 건너뛴다(지역 기믹은 가까운 무공 몫)
            for (int y = r0; y <= r1; y++)
            for (int x = c0; x <= c1; x++)
            {
                if (!me._grid.TryGetValue(CellKey(x, y), out var list)) continue;
                foreach (var s in list)
                {
                    if (!s.Bamboo || !s.Up || (s.At - center).magnitude > s.R + radius) continue;
                    s.Hits++;
                    if (!BambooThicketGimmick.Breaks(def, s.Hits)) continue;
                    me.Cut(s, now, def);
                }
            }
        }

        void Cut(Stalk s, float now, BambooThicketGimmickDef def)
        {
            s.Up = false; s.Hits = 0;
            s.RegrowAt = BambooThicketGimmick.RegrowAt(def, now);
            BambooCut++;
            if (s.Go != null)
            {
                // 밑동만 남는다: 아래 끝을 붙잡고 키를 1/4 로, 마른 빛
                var sr = s.Go.GetComponent<SpriteRenderer>();
                float h = sr != null && sr.sprite != null ? sr.sprite.bounds.size.y * s.Scale.y : 0f;
                s.Go.transform.localScale = new Vector3(s.Scale.x, s.Scale.y * 0.22f, s.Scale.z);
                s.Go.transform.position = s.Pos - new Vector3(0f, h * 0.39f, 0f);
                if (sr != null) sr.color = new Color(0.62f, 0.64f, 0.5f, 1f);
            }
            var fx = SectFx.Instance;
            if (fx != null && fx.Ready) fx.Drops(s.At + new Vector2(0f, s.R), Mathf.PI * 0.5f, 5, 5f, FxRamp.Poison, false, .1f, 1.3f);
            if (BambooThicketGimmick.DropsFood(def, _bambooRng)) LanternLayer.DropFood(s.At);
        }

        void TickBamboo(float now)
        {
            foreach (var patch in _patches.Values)
            {
                if (!patch.Bamboo) continue;
                foreach (var s in patch.Stones)
                {
                    if (s.Up || now < s.RegrowAt) continue;
                    s.Up = true;
                    if (s.Go != null)
                    {
                        s.Go.transform.localScale = s.Scale; s.Go.transform.position = s.Pos;
                        var sr = s.Go.GetComponent<SpriteRenderer>();
                        if (sr != null) sr.color = Color.white;
                    }
                }
            }
        }

        // ── RG-03U 당가타 독안개(2026-11-26) ─────────────────────────────
        //   한 생 시계로 퍼짐이 시작될 때 당가타에 서 있으면 내 둘레 fogOffset(6m × 축척) 안 한 곳에 샘(웹 WP-13b 와 같음).
        //   반경은 Core PoisonFogGimmick.RadiusAt, 안에선 플레이어·적 모두 HealthLoss(당가 몸은 독 면역, 보스 면역). 그림은 독 담채 + 먹 테.
        PoisonFogGimmick _fog;
        Vector2 _fogAt;
        float _fogSeed;
        readonly System.Random _fogRng = new System.Random(911);
        public bool FogActive => _fog != null;
        public float FogRadius { get; private set; }
        public Vector2 FogAt => _fogAt;

        void TickFog(BloodRain.Core.Run.Life life, Vector2 p, float dt)
        {
            var def = DataBoot.Data.Gimmicks.PoisonFog;
            float now = life.Minutes * 60f;
            var phase = PoisonFogGimmick.PhaseAt(def, now);
            if (phase == FogPhase.Clear) { _fog = null; FogRadius = 0f; return; }
            if (_fog == null)
            {
                // 퍼짐이 막 시작됐고 지금 당가타(독안개가 있는 땅)에 서 있을 때만
                if (phase != FogPhase.Spreading || !HereHas(life, "poison_fog")) return;
                float k = life.Scale.Factor(life.Realm);
                float off = DataBoot.Data.Gimmicks.Layout.FogOffset * k;
                float a = (float)(_fogRng.NextDouble() * Mathf.PI * 2f), d = off * Mathf.Sqrt((float)_fogRng.NextDouble());
                _fogAt = p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                _fog = new PoisonFogGimmick(def, k);
                _fogSeed = (float)_fogRng.NextDouble() * 7f;
            }
            FogRadius = _fog.RadiusAt(now);
            if (FogRadius <= 0f) return;
            var vitals = runner.vitals;
            if (vitals != null && (p - _fogAt).magnitude <= FogRadius)
                vitals.Drain(_fog.HealthLoss(GimmickTargetKind.Player, vitals.MaxHealth, dt, life.Body.SectTrait.PoisonImmune));
            var en = runner.enemies;
            var sim = en != null ? en.Sim : null;
            if (sim == null) return;
            float r2 = FogRadius * FogRadius;
            for (int i = sim.Count - 1; i >= 0; i--)
            {
                var ag = sim.Get(i);
                if (ag.Health <= 0f) continue;
                var dv = new Vector2(ag.Pos.x, ag.Pos.y) - _fogAt;
                if (dv.sqrMagnitude > r2) continue;
                var ed = en.DefOf(ag.Kind);
                var kind = ed != null && ed.Family == "boss" ? GimmickTargetKind.Boss : ed != null && ed.Family == "elite" ? GimmickTargetKind.Elite : GimmickTargetKind.Normal;
                float loss = _fog.HealthLoss(kind, ag.MaxHealth, dt);
                if (loss <= 0f) continue;
                ag.Health -= loss;
                if (ag.Health <= 0f) { en.Kill(i); continue; }
                sim.Set(i, ag);
            }
        }

        static bool HereHas(BloodRain.Core.Run.Life life, string gimmick)
        {
            foreach (var r in life.Regions.All)
                if (r.Id == life.RegionId) return r.Gimmicks != null && r.Gimmicks.Contains(gimmick);
            return false;
        }

        void LateUpdate()
        {
            if (_fog == null || FogRadius <= 0f) return;
            var fx = SectFx.Instance;
            if (fx == null || !fx.Ready) return;
            // 독 담채 안개 두 겹(천천히 돈다) + 먹·독 테 — 옅은 종이 바닥에서도 경계가 읽히게
            float t = Time.time * 0.12f, D = FogRadius * 2f;
            fx.Quad(3, _fogAt, D * 1.08f, D * 0.96f, t + _fogSeed, new Vector4(5f, .35f, .85f, 0f), FxRamp.Poison, .30f, 1f, .2f, _fogSeed);
            fx.Quad(3, _fogAt, D * 0.86f, D * 0.92f, -t * 1.3f + _fogSeed * 2f, new Vector4(5f, .3f, .8f, 0f), FxRamp.MistG, .22f, 1f, .25f, _fogSeed + 3f);
            fx.Quad(2, _fogAt, D, D, 0f, new Vector4(.97f, .03f, .45f, 0f), FxRamp.PoisonInk, .45f, 1f, .15f, _fogSeed);
        }
        static Sprite _disc;
        GameObject Disc(string name, Vector2 at, float r, Color c, int order)
        {
            if (_disc == null)
            {
                const int n = 64;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "기믹 동그라미" };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01((1f - d) * 8f) * 255));
                }
                tex.SetPixels32(px); tex.Apply();
                _disc = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            }
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(at.x, at.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _disc; sr.color = c; sr.sortingOrder = order;
            go.transform.localScale = Vector3.one * r * 2f;
            return go;
        }

        // ---------------------------------------------------------------- 혈제단(R4-13c, Core BloodAltarGimmick)
        // 십만대산 한가운데 둘레 layout.altarRing m(× 축척) 고리의 네 귀퉁이에 넷, 켜지는 차례는 북동(altarOrderFromDegrees 45°)부터 시계 방향(웹 bloodAltarsStep).
        // 제단은 적 목록에 '움직이지 않는 적'으로 넣어 무공이 그대로 깎게 한다(속도 0·닿는 피해 0·구슬 없음, 적 걸음 앞에서 제자리로 되돌림).
        // 덩이 그림은 투명 한 장으로 가리고 제단 원화를 따로 세운다. 화면에서 멀어지면 거두고(깎인 비율은 남김) 가까워지면 다시 놓고,
        // 경지가 바뀌면 새 축척으로 다시 놓는다. 켜지기 전에도 깎을 수 있고 미리 부순 제단은 안 켜진다(웹 WP-13b 와 같게).
        // 켜진 제단 buffRadius 안 적: 피해(1+Buff) = enemyDamageMultiplier, 걸음은 Buff 와 Slow 를 함께 걸어 enemyMoveSpeedMultiplier 가 되게.

        sealed class Altar { public bool Broken; public float Ratio = 1f; public int Id = -1; public bool Lit; public Vector2 At; public GameObject Go, Glow, Ring; }
        readonly List<Altar> _altars = new List<Altar>();
        BloodRain.Core.Run.Life _altarLife;
        int _altarRealm = -1;
        bool _altarHooked;
        EnemyDef _altarDef;
        static Texture2D _clearTex;
        static readonly Color AltarRed = new Color(0.70f, 0.15f, 0.12f, 1f);
        public int AltarPlaced { get { int n = 0; foreach (var a in _altars) if (a.Id >= 0) n++; return n; } }
        public int AltarLit { get { int n = 0; foreach (var a in _altars) if (a.Lit) n++; return n; } }
        public int AltarsBroken { get { int n = 0; foreach (var a in _altars) if (a.Broken) n++; return n; } }
        public int AltarBuffed { get; private set; }
        public int AltarChests { get; private set; }

        void TickAltars(BloodRain.Core.Run.Life life, Vector2 player, float view)
        {
            var def = DataBoot.Data.Gimmicks.BloodAltars;
            var en = runner.enemies;
            if (def == null || def.Count <= 0 || en == null || en.Sim == null) return;
            if (!_altarHooked) { en.Killed += OnAltarKilled; _altarHooked = true; }
            if (_altarLife != life) { RemoveAltars(); foreach (var st in _altars) DestroyAltarArt(st); _altars.Clear(); _altarLife = life; _altarRealm = -1; }
            if (_altarRealm != life.Realm) { RemoveAltars(); foreach (var st in _altars) DestroyAltarArt(st); _altarRealm = life.Realm; }

            BloodRain.Core.Data.RegionDef reg = null;
            foreach (var r in life.Regions.All) if (r.Gimmicks != null && r.Gimmicks.Contains(def.Id)) { reg = r; break; }
            if (reg == null) return;
            if (_altars.Count == 0) for (int i = 0; i < def.Count; i++) _altars.Add(new Altar());

            float k = life.Scale.Factor(life.Realm);
            var lay = DataBoot.Data.Gimmicks.Layout;
            var o = life.Regions.Center(reg);
            float ring = lay.AltarRing * k;
            int lit = BloodAltarGimmick.LitCount(def, life.Minutes * 60f);
            var sim = en.Sim;
            for (int i = 0; i < _altars.Count; i++)
            {
                var st = _altars[i];
                float ang = (lay.AltarOrderFromDegrees - 90f * i) * Mathf.Deg2Rad; // 북동 → 남동 → 남서 → 북서
                st.At = WorldCoords.ToUnity(new System.Numerics.Vector2(o.X + Mathf.Cos(ang) * ring, o.Y - Mathf.Sin(ang) * ring));
                float d = Vector2.Distance(player, st.At);
                bool near = d <= view * 1.0f, far = d > view * 1.25f;
                if (st.Go == null && d <= view * 1.3f) MakeAltarArt(st, k);
                else if (st.Go != null && d > view * 1.4f) DestroyAltarArt(st);
                if (!st.Broken)
                {
                    int idx = st.Id >= 0 ? IndexOf(sim, st.Id) : -1;
                    if (st.Id >= 0 && idx < 0) st.Id = -1;                 // 수 줄이기로 풀에서 빠짐 — 깎인 비율 그대로 다시 놓는다
                    if (st.Id < 0 && near) st.Id = SpawnAltar(st, def, k);
                    else if (idx >= 0 && far)
                    {
                        var a = sim.Get(idx);
                        st.Ratio = a.MaxHealth > 0f ? a.Health / a.MaxHealth : 1f;
                        sim.RemoveAt(idx); st.Id = -1;
                    }
                }
                bool wasLit = st.Lit;
                st.Lit = i < lit && !st.Broken;
                if (st.Lit && !wasLit) Debug.Log($"[GimmickLayer] 혈제단 {i} 켜짐 ({life.Minutes * 60f:0}초)");
                if (st.Go != null) UpdateAltarArt(st, def, k);
            }
        }

        static int IndexOf(EnemySim sim, int id)
        {
            var agents = sim.Agents;
            for (int i = 0; i < sim.Count; i++) if (agents[i].Id == id) return i;
            return -1;
        }

        int SpawnAltar(Altar st, BloodAltarGimmickDef def, float k)
        {
            var en = runner.enemies;
            if (_altarDef == null)
                _altarDef = new EnemyDef { Id = "blood_altar", NameKo = def.NameKo, Family = "altar", Health = def.Health, Speed = 0f, ContactDamage = 0f, Behavior = "chase" };
            int id = en.SpawnAt(_altarDef, st.At);
            int idx = IndexOf(en.Sim, id);
            if (idx < 0) return -1;
            var a = en.Sim.Get(idx);
            a.Health = Mathf.Max(1f, a.MaxHealth * st.Ratio); // 기혈 1,500 은 적 기혈 식(SpawnHealth)을 거친 값
            a.Speed = 0f; a.ContactDamage = 0f;
            a.Radius = 0.9f * k;
            a.Weight = 0f;
            a.NoDrop = 1;
            en.Sim.Set(idx, a);
            if (_clearTex == null)
            {
                _clearTex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "혈제단 덩이 가림" };
                _clearTex.SetPixel(0, 0, new Color(1f, 1f, 1f, 0f)); _clearTex.Apply();
            }
            en.SetViewArt(id, _clearTex);
            return id;
        }

        void RemoveAltars()
        {
            var sim = runner != null && runner.enemies != null ? runner.enemies.Sim : null;
            foreach (var st in _altars)
            {
                if (st.Id < 0) continue;
                int idx = sim != null ? IndexOf(sim, st.Id) : -1;
                if (idx >= 0)
                {
                    var a = sim.Get(idx);
                    st.Ratio = a.MaxHealth > 0f ? a.Health / a.MaxHealth : 1f;
                    sim.RemoveAt(idx);
                }
                st.Id = -1;
            }
        }

        void OnAltarKilled(Vector2 at, EnemyAgent a)
        {
            foreach (var st in _altars)
            {
                if (st.Id != a.Id) continue;
                st.Broken = true; st.Id = -1; st.Lit = false;
                DestroyAltarArt(st);
                if (_altarLife != null) MakeAltarArt(st, _altarLife.Scale.Factor(_altarLife.Realm));
                var def = DataBoot.Data.Gimmicks.BloodAltars;
                if (def.ChestOnBreak && ChestLayer.Instance != null) { ChestLayer.Instance.Place(st.At); AltarChests++; }
                var fx = SectFx.Instance;
                if (fx != null && fx.Ready && _altarLife != null) fx.RingW(st.At, 2.4f * _altarLife.Scale.Factor(_altarLife.Realm), FxRamp.Flat(new Color(0.35f, 0.1f, 0.1f, 1f)), 0.7f, 0.8f, true);
                Debug.Log($"[GimmickLayer] 혈제단 무너짐 · 보갑");
                return;
            }
        }

        void MakeAltarArt(Altar st, float k)
        {
            var def = DataBoot.Data.Gimmicks.BloodAltars;
            st.Ring = Disc("혈제단 기운", st.At, def.BuffRadius * k, new Color(AltarRed.r, AltarRed.g, AltarRed.b, 0f), 1);
            st.Glow = Disc("혈제단 불", st.At + Vector2.up * 0.9f * k, 1.4f * k, new Color(AltarRed.r, AltarRed.g, AltarRed.b, 0f), 4);
            st.Go = PropArt.Make(transform, "혈제단", st.Broken ? "prop_blood_altar_broken" : "prop_blood_altar", st.At + Vector2.up * 0.6f * k, 2.6f * k, 3)
                 ?? Disc("혈제단", st.At, 0.9f * k, st.Broken ? new Color(0.35f, 0.32f, 0.3f, 0.9f) : new Color(0.23f, 0.2f, 0.19f, 1f), 3);
        }

        void DestroyAltarArt(Altar st)
        {
            if (st.Go != null) Destroy(st.Go);
            if (st.Glow != null) Destroy(st.Glow);
            if (st.Ring != null) Destroy(st.Ring);
            st.Go = st.Glow = st.Ring = null;
        }

        void UpdateAltarArt(Altar st, BloodAltarGimmickDef def, float k)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);
            var ring = st.Ring != null ? st.Ring.GetComponent<SpriteRenderer>() : null;
            var glow = st.Glow != null ? st.Glow.GetComponent<SpriteRenderer>() : null;
            if (ring != null) ring.color = new Color(AltarRed.r, AltarRed.g, AltarRed.b, st.Lit ? 0.07f + 0.04f * pulse : 0f);
            if (glow != null) glow.color = new Color(AltarRed.r, AltarRed.g, AltarRed.b, st.Lit ? 0.35f + 0.25f * pulse : 0f);
            var sr = st.Go.GetComponent<SpriteRenderer>();
            if (sr == null || st.Broken) return;
            // 깎일수록 옅어지고, 켜지기 전엔 잿빛
            float hp = 1f;
            var en = runner.enemies;
            if (st.Id >= 0 && en != null && en.Sim != null) { int idx = IndexOf(en.Sim, st.Id); if (idx >= 0) { var a = en.Sim.Get(idx); hp = a.MaxHealth > 0f ? a.Health / a.MaxHealth : 1f; } }
            else hp = st.Ratio;
            Color c = st.Lit ? Color.white : new Color(0.72f, 0.7f, 0.68f, 1f);
            sr.color = Color.Lerp(new Color(c.r, c.g, c.b, 0.55f), c, hp);
        }

        /// <summary>EnemyField 가 적 걸음 앞(ApplyAuras 뒤)에 부른다: 제단을 제자리로, 켜진 제단 둘레 적에 Buff·Slow.</summary>
        public static void AltarStep(EnemySim sim) { if (_instance != null && sim != null) _instance.ApplyAltars(sim); }

        void ApplyAltars(EnemySim sim)
        {
            AltarBuffed = 0;
            if (_altars.Count == 0 || _altarLife == null) return;
            var def = DataBoot.Data.Gimmicks.BloodAltars;
            float k = _altarLife.Scale.Factor(_altarLife.Realm);
            var agents = sim.Agents;
            bool anyLit = false;
            foreach (var st in _altars)
            {
                if (st.Lit) anyLit = true;
                if (st.Id < 0) continue;
                int idx = IndexOf(sim, st.Id);
                if (idx < 0) continue;
                var a = agents[idx];
                a.Pos = new float2(st.At.x, st.At.y); a.Knock = float2.zero; a.Speed = 0f; a.Buff = 0f;
                agents[idx] = a;
            }
            if (!anyLit) return;
            float buff = def.EnemyDamageMultiplier - 1f;
            float slow = def.EnemyDamageMultiplier > 0f ? Mathf.Clamp01(1f - def.EnemyMoveSpeedMultiplier / def.EnemyDamageMultiplier) : 0f;
            float r2 = def.BuffRadius * k; r2 *= r2;
            int n = 0;
            for (int i = 0; i < sim.Count; i++)
            {
                var a = agents[i];
                if (a.Health <= 0f || IsAltarId(a.Id)) continue;
                foreach (var st in _altars)
                {
                    if (!st.Lit) continue;
                    float dx = a.Pos.x - st.At.x, dy = a.Pos.y - st.At.y;
                    if (dx * dx + dy * dy > r2) continue;
                    if (a.Buff < buff) a.Buff = buff;
                    if (a.Slow < slow) a.Slow = slow;
                    agents[i] = a; n++;
                    break;
                }
            }
            AltarBuffed = n;
        }

        bool IsAltarId(int id) { foreach (var st in _altars) if (st.Id == id) return true; return false; }

        /// <summary>시험: 혈제단 하나를 지금 켜진 것처럼(한 생 시계는 안 건드림) 보려면 i 번째 제단 앞으로 플레이어를 옮긴다.</summary>
        public Vector2 AltarAt(int i) => i >= 0 && i < _altars.Count ? _altars[i].At : Vector2.zero;

        void RebuildGrid()
        {
            _grid.Clear();
            int n = 0;
            foreach (var patch in _patches.Values)
                foreach (var s in patch.Stones)
                {
                    long key = CellKey(s.At);
                    if (!_grid.TryGetValue(key, out var list)) _grid[key] = list = new List<Stalk>();
                    list.Add(s);
                    n++;
                }
            StoneCount = n;
        }

        long CellKey(Vector2 p) => CellKey(Mathf.FloorToInt(p.x / _cell), Mathf.FloorToInt(p.y / _cell));
        static long CellKey(int x, int y) => ((long)x << 32) ^ (uint)y;

        /// <summary>몸(반경 r)이 돌탑과 겹치면 밖으로 밀어낸 자리. 돌탑이 없으면 그대로.</summary>
        public static Vector2 Resolve(Vector2 pos, float r)
        {
            var me = _instance;
            if (me == null || me._grid.Count == 0) return pos;
            int cx = Mathf.FloorToInt(pos.x / me._cell), cy = Mathf.FloorToInt(pos.y / me._cell);
            for (int pass = 0; pass < 2; pass++)
            for (int y = cy - 1; y <= cy + 1; y++)
            for (int x = cx - 1; x <= cx + 1; x++)
            {
                if (!me._grid.TryGetValue(CellKey(x, y), out var list)) continue;
                foreach (var s in list)
                {
                    if (!s.Up) continue; // 베인 대나무는 지나간다
                    var d = pos - s.At;
                    float min = s.R + r, m = d.magnitude;
                    if (m >= min) continue;
                    pos = m < 1e-4f ? s.At + new Vector2(min, 0f) : s.At + d / m * min;
                }
            }
            return pos;
        }

        /// <summary>걷는 적을 돌탑 밖으로(EnemyField 가 Step 뒤 부른다). 나는 적은 지나간다.</summary>
        public static void PushAgents(EnemySim sim, System.Func<int, bool> flying)
        {
            var me = _instance;
            if (me == null || me._grid.Count == 0 || sim == null) return;
            for (int i = 0; i < sim.Count; i++)
            {
                var a = sim.Get(i);
                if (a.Health <= 0f || flying(a.Kind)) continue;
                var p = new Vector2(a.Pos.x, a.Pos.y);
                var q = Resolve(p, a.Radius);
                if ((q - p).sqrMagnitude < 1e-8f) continue;
                a.Pos = new float2(q.x, q.y);
                sim.Set(i, a);
            }
        }

        void TickBells(float dt, Vector2 player)
        {
            foreach (var b in _bells.Values)
            {
                b.G.Tick(dt);
                if (b.G.TryRing(Vector2.Distance(player, b.At))) Ring(b);
                if (b.Go != null)
                {
                    // 몸(정렬 20) 발밑이 범종 발밑보다 위(뒤)면 범종이 몸을 가리고, 아래(앞)면 몸이 범종 앞에 선다
                    var so = b.Go.GetComponent<SpriteRenderer>();
                    if (so != null) so.sortingOrder = player.y > b.At.y ? 21 : 3;
                }
                if (b.Flash > 0f && b.Go != null)
                {
                    b.Flash -= dt;
                    var sr = b.Go.GetComponent<SpriteRenderer>();
                    if (sr != null) sr.color = Color.Lerp(Color.white, new Color(1f, 0.85f, 0.4f, 1f), Mathf.Clamp01(b.Flash / 0.6f));
                }
                else if (b.Go != null)
                {
                    // 쿨다운 중엔 흐리게(다시 울릴 수 있을 때까지)
                    var sr = b.Go.GetComponent<SpriteRenderer>();
                    if (sr != null) sr.color = b.G.Ready ? Color.white : new Color(0.75f, 0.75f, 0.75f, 0.8f);
                }
            }
        }

        void Ring(Bell b)
        {
            Rings++;
            b.Flash = 0.6f;
            var sim = runner.enemies != null ? runner.enemies.Sim : null;
            int stunned = 0;
            if (sim != null)
                for (int i = 0; i < sim.Count; i++)
                {
                    var a = sim.Get(i);
                    if (a.Health <= 0f) continue;
                    var def = runner.enemies.DefOf(a.Kind);
                    var kind = def != null && def.Family == "boss" ? GimmickTargetKind.Boss : def != null && def.Family == "elite" ? GimmickTargetKind.Elite : GimmickTargetKind.Normal;
                    float s = b.G.StunSeconds(kind, Vector2.Distance(b.At, new Vector2(a.Pos.x, a.Pos.y)));
                    if (s <= 0f) continue;
                    a.Stun = Mathf.Max(a.Stun, s);
                    sim.Set(i, a);
                    stunned++;
                }
            // 울림(2026-11-21): 먹 고리가 세우는 반경까지 + 종 둘레 금빛 소리 고리(떨어진 범종과 같게). SectFx 가 없으면 예전 선 고리
            var fx = SectFx.Instance;
            if (fx != null && fx.Ready)
            {
                fx.RingW(b.At, b.G.StunRadius * 2.2f, FxRamp.Ink, 0.55f, 0.32f);
                fx.RingW(b.At, b.G.TouchRadius * 3f, FxRamp.Gold, 0.45f, 0.8f, true);
            }
            else StartCoroutine(RingWave(b.At, b.G.StunRadius));
            Debug.Log($"[GimmickLayer] 범종 울림 — 세운 적 {stunned}");
        }

        System.Collections.IEnumerator RingWave(Vector2 at, float radius)
        {
            // 먹 고리가 기절 반경까지 퍼진다(0.5초)
            var go = new GameObject("범종 울림");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(at.x, at.y, 0f);
            var lr = go.AddComponent<LineRenderer>();
            if (_ringMat == null) _ringMat = new Material(Shader.Find("Sprites/Default"));
            lr.material = _ringMat;
            lr.useWorldSpace = false; lr.loop = true; lr.positionCount = 48; lr.sortingOrder = 6;
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                float u = t / 0.5f, r = Mathf.Lerp(radius * 0.15f, radius, 1f - (1f - u) * (1f - u));
                lr.widthMultiplier = radius * 0.04f * (1f - u * 0.6f);
                var c = new Color(0.12f, 0.10f, 0.08f, 0.8f * (1f - u));
                lr.startColor = lr.endColor = c;
                for (int i = 0; i < 48; i++) { float a = i / 48f * Mathf.PI * 2f; lr.SetPosition(i, new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f)); }
                yield return null;
            }
            Destroy(go);
        }

        void ClearAll()
        {
            foreach (var b in _bells.Values) if (b.Go != null) Destroy(b.Go);
            foreach (var p in _patches.Values) foreach (var g in p.Gos) if (g != null) Destroy(g);
            foreach (var h in _plums.Values) if (h.Go != null) Destroy(h.Go);
            _plums.Clear();
            foreach (var f in _fires.Values) foreach (var g in f.Gos) if (g != null) Destroy(g);
            _fires.Clear();
            _bells.Clear(); _patches.Clear(); _grid.Clear(); StoneCount = 0;
            _fog = null;
        }
    }
}
