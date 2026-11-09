using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using BloodRain.Core.World;
using BloodRain.Sim;
using Unity.Mathematics;
using UnityEngine;

namespace BloodRain.Presentation.Combat
{
    // BR-01 땅의 주인 패턴 러너. Core BossPatterns 시간표를 돌려 예고(바닥 붉은 그림자)와 판정을 그린다.
    //   - life.Bosses.Fight 가 생기면 보스를 EnemyField 에 한 덩이(family boss)로 세운다. 기혈은 Fight.MaxHealth,
    //     걸음·닿는 피해는 bosspatterns.json moveSpeed·contactDamage. 무공은 보통 적처럼 이 덩이를 맞힌다.
    //   - 넉백·감전은 보스에 안 먹게 매 프레임 지운다(W-01). 기절은 Stun(초)으로만(팔선과해 bossStunSeconds 등, 이때 패턴 시계가 멈춤).
    //   - 길이·반경은 화면 기준 m 이라 싸움을 건 경지(Fight.Realm) 축척을 곱해 세계에 놓는다.
    //   - 피해 = BossPatterns.ScaleDamage(baseDamage, 싸움을 건 분, 그때 혈운 농도, damagePerMinute). 호신은 PlayerVitals 가 뺀다.
    //   - 지금 그리는 모양: cone(금강장)·summon_ring(십팔나한진)·circles(범종 낙하)·rotating_beams(붉은 빛줄기, 2막).
    //     다른 땅의 주인 모양은 각자 항목(R-14U·R4-02U)에서 이 틀에 더한다. 범종이 남기는 범종 기믹은 아직 안 둔다.
    public sealed class PatternRunner : MonoBehaviour
    {
        public LifeRunner runner;
        public static PatternRunner Instance { get; private set; }

        /// <summary>보스 몸 반경(화면 기준 m). 데이터에 없어 자리 표시 값.</summary>
        public float bossBodyRadius = 1.0f;

        BossFight _fight;
        BossPatterns _patterns;
        EnemyDef _bossDef;
        int _bossId = -1;
        float _factor = 1f;
        float _density = 1f;
        float _stun;
        float _walkSpeed;
        float _towardPlayerAtPhase2;
        float _beamHitLeft;
        readonly System.Random _rng = new System.Random();
        readonly List<Tele> _teles = new List<Tele>();
        readonly List<GameObject> _beamVis = new List<GameObject>();
        Material _mat;
        GameObject _bossMark;

        /// <summary>시험·로그용: 패턴별 판정 수와 맞은 수.</summary>
        public readonly Dictionary<string, int> Resolved = new Dictionary<string, int>();
        public readonly Dictionary<string, int> PlayerHits = new Dictionary<string, int>();
        public string LastEvent { get; private set; } = "";
        public bool InFight => _fight != null;
        public BossPatterns Patterns => _patterns;
        public int BossId => _bossId;
        /// <summary>지금 싸우는 땅의 주인 이름(현지화). 싸움이 없으면 빈 글.</summary>
        public string BossName => _fight != null ? Loc.Boss(_fight.Boss) : "";

        sealed class Tele
        {
            public BossPatternDef P;
            public int Cast;
            public float Facing;
            public Vector2 Center;
            public Vector2[] Circles;
            public float Start;
            public GameObject Vis;
            public float FadeLeft = -1f;
        }

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; Clear(); }

        void OnEnable() { if (runner != null && runner.enemies != null) runner.enemies.Killed += OnKilled; }
        void OnDisable() { if (runner != null && runner.enemies != null) runner.enemies.Killed -= OnKilled; }

        void Start()
        {
            if (runner == null) runner = GetComponent<LifeRunner>();
            if (runner != null && runner.enemies != null) { runner.enemies.Killed -= OnKilled; runner.enemies.Killed += OnKilled; }
        }

        /// <summary>보스를 seconds 동안 세운다(패턴 시계도 멈춤). 팔선과해 bossStunSeconds 같은 곳이 부른다.</summary>
        public void Stun(float seconds) => _stun = Mathf.Max(_stun, seconds);

        void Update()
        {
            var life = runner != null ? runner.Life : null;
            if (life == null || runner.enemies == null || runner.player == null) return;
            var fight = life.Bosses.Fight;
            if (fight != _fight)
            {
                if (_fight != null) EndFight();
                if (fight != null) BeginFight(fight, life);
            }
            if (_fight == null) return;

            float dt = Time.deltaTime;
            int idx = BossIndex();
            if (idx < 0) return; // 쓰러짐은 OnKilled 가 받는다
            var sim = runner.enemies.Sim;
            var a = sim.Get(idx);
            // 넉백·감전은 보스에 안 먹는다(W-01). 우리가 건 기절만
            a.Knock = float2.zero;
            if (_stun > 0f) { _stun -= dt; a.Stun = Mathf.Max(0.05f, _stun); }
            else a.Stun = 0f;
            a.Speed = _patterns.Holding ? 0f : _walkSpeed;
            sim.Set(idx, a);

            Vector2 boss = new Vector2(a.Pos.x, a.Pos.y);
            if (_bossMark != null) { _bossMark.transform.position = new Vector3(boss.x, boss.y, 0f); }

            if (_stun <= 0f)
            {
                float ratio = a.MaxHealth > 0f ? a.Health / a.MaxHealth : 1f;
                foreach (var e in _patterns.Tick(dt, ratio)) Handle(e, boss);
            }
            UpdateTeles(boss);
            UpdateBeams(boss, dt);
        }

        // ---- 싸움 시작·끝 ----

        void BeginFight(BossFight fight, BloodRain.Core.Run.Life life)
        {
            var data = DataBoot.Data;
            var set = data.BossPatternSet(fight.Boss.Id);
            _fight = fight;
            _patterns = new BossPatterns(set);
            var ws = WorldScale.Current;
            _factor = ws != null ? ws.Length(1f, fight.Realm) : 1f;
            _density = life.Density;
            Resolved.Clear(); PlayerHits.Clear();
            _stun = 0f;
            _beamHitLeft = 0f;

            _bossDef = new EnemyDef
            {
                Id = fight.Boss.Id,
                NameKo = fight.Boss.NameKo,
                Family = "boss",
                Health = fight.MaxHealth,
                Speed = set.MoveSpeed,
                ContactDamage = set.ContactDamage,
                Behavior = "chase",
            };
            Vector2 p = runner.player.position;
            Vector2 at = p + Vector2.right * (6f * _factor);
            int id = runner.enemies.SpawnAt(_bossDef, at);
            _bossId = id;
            int idx = BossIndex();
            if (idx >= 0)
            {
                var sim = runner.enemies.Sim;
                var a = sim.Get(idx);
                a.Health = a.MaxHealth = fight.MaxHealth;
                a.ContactDamage = Scaled(set.ContactDamage);
                a.Radius = bossBodyRadius * _factor;
                a.Weight = 0.1f;
                a.BornRealm = fight.Realm;
                a.NoDrop = 1;
                _walkSpeed = a.Speed;
                sim.Set(idx, a);
            }
            _bossMark = Shape("땅의 주인 표시", new Color(0.9f, 0.1f, 0.08f, 0.55f), 6);
            SetRing(_bossMark, Vector2.zero, bossBodyRadius * _factor * 1.15f, 0.12f * _factor);
            LastEvent = $"보스전 시작 {fight.Boss.NameKo} 기혈 {fight.MaxHealth:0} 축척 {_factor:0.###}";
            Debug.Log($"[PatternRunner] {LastEvent}");
            runner.ShowPopup(Loc.Boss(fight.Boss));
            SfxPlayer.Instance?.Play("sfx_boss_appear");
        }

        void EndFight()
        {
            Clear();
            _fight = null;
            _patterns = null;
            _bossId = -1;
        }

        void Clear()
        {
            foreach (var t in _teles) if (t.Vis != null) Destroy(t.Vis);
            _teles.Clear();
            foreach (var b in _beamVis) if (b != null) Destroy(b);
            _beamVis.Clear();
            if (_bossMark != null) Destroy(_bossMark);
            _bossMark = null;
        }

        void OnKilled(Vector2 at, EnemyAgent a)
        {
            if (_fight == null || a.Id != _bossId) return;
            var life = runner.Life;
            Debug.Log($"[PatternRunner] {_fight.Boss.NameKo} 쓰러짐");
            LastEvent = "쓰러짐";
            if (life != null && life.Bosses.InFight && life.Record == null)
            {
                var kill = life.DefeatBoss(_fight.IsFinal ? life.Silver : (int?)null);
                if (kill.Chest) BloodRain.Presentation.Fx.ChestLayer.Instance?.Place(at); // BR-02 처치 보갑(C-09d 보갑 길)
                Debug.Log($"[PatternRunner] 처치 {kill.RegionId} 첫 처치 {kill.First} 봉인석 {kill.SealStone} 윤회의 기 {kill.RebirthQi}");
            }
            EndFight();
        }

        int BossIndex()
        {
            if (_bossId < 0 || runner.enemies.Sim == null) return -1;
            var sim = runner.enemies.Sim;
            var agents = sim.Agents;
            for (int i = 0; i < sim.Count; i++) if (agents[i].Id == _bossId) return i;
            return -1;
        }

        float Scaled(float baseDamage) =>
            BossPatterns.ScaleDamage(baseDamage, _fight.Minute, _density, DataBoot.Data.Balance.EnemyScaling.DamagePerMinute);

        float PlayerRadius => DataBoot.Data.Balance.Stats.PlayerBodyRadius;

        static float Deg(Vector2 v) => Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;

        // ---- 사건 ----

        void Handle(BossPatternEvent e, Vector2 boss)
        {
            Vector2 player = runner.player.position;
            if (e.Kind == BossPatternEventKind.Phase2)
            {
                _towardPlayerAtPhase2 = Deg(player - boss);
                LastEvent = "2막";
                Debug.Log("[PatternRunner] 2막");
                return;
            }
            var p = e.Pattern;
            if (e.Kind == BossPatternEventKind.Telegraph)
            {
                var t = new Tele { P = p, Cast = e.Cast, Start = _patterns.Elapsed, Center = player };
                t.Facing = Deg(player - boss);
                switch (p.Shape)
                {
                    case "cone":
                        t.Center = boss;
                        t.Vis = Shape(p.Id, new Color(0.8f, 0.05f, 0.05f, 0.25f), 2);
                        SetFan(t.Vis, Vector2.zero, p.Radius * _factor, t.Facing, p.AngleDegrees);
                        break;
                    case "summon_ring":
                        t.Vis = Shape(p.Id, new Color(0.8f, 0.05f, 0.05f, 0.35f), 2);
                        SetRing(t.Vis, Vector2.zero, p.Radius * _factor, 0.25f * _factor);
                        break;
                    case "circles":
                        var offs = BossPatterns.ScatterOffsets(p, _rng);
                        t.Circles = offs.Select(o => player + new Vector2(o.x, o.y) * _factor).ToArray();
                        t.Vis = Shape(p.Id, new Color(0.8f, 0.05f, 0.05f, 0.25f), 2);
                        SetCircles(t.Vis, t.Circles.Select(c => c - player).ToArray(), p.Radius * _factor);
                        t.Center = player;
                        break;
                    case "rotating_beams":
                        t.Center = boss;
                        break;
                    default:
                        Debug.LogWarning($"[PatternRunner] 아직 안 그리는 모양 {p.Shape} ({p.Id})");
                        break;
                }
                if (t.Vis != null) t.Vis.transform.position = new Vector3(t.Center.x, t.Center.y, 0f);
                _teles.Add(t);
                LastEvent = $"예고 {p.NameKo}";
                return;
            }

            // Resolve
            Resolved[p.Id] = Resolved.TryGetValue(p.Id, out var n) ? n + 1 : 1;
            LastEvent = $"판정 {p.NameKo}";
            var tele = _teles.FirstOrDefault(x => x.P == p && x.Cast == e.Cast && x.FadeLeft < 0f);
            if (p.Continuous) { if (tele != null) Remove(tele); return; } // 2막 빛줄기는 UpdateBeams 가 맞힌다
            float pr = PlayerRadius;
            bool hit = false;
            switch (p.Shape)
            {
                case "cone":
                {
                    var c = tele != null ? tele.Center : boss;
                    var d = (player - c) / _factor;
                    hit = BossPatterns.InCone(p, d.x, d.y, tele != null ? tele.Facing : Deg(player - boss), pr);
                    break;
                }
                case "summon_ring":
                {
                    var c = tele != null ? tele.Center : player;
                    SummonRing(p, c);
                    break;
                }
                case "circles":
                {
                    if (tele?.Circles != null)
                        foreach (var c in tele.Circles)
                        {
                            var d = (player - c) / _factor;
                            if (BossPatterns.InCircle(p.Radius, d.x, d.y, pr)) { hit = true; break; }
                        }
                    break;
                }
            }
            if (hit && p.BaseDamage > 0f && runner.vitals != null && runner.vitals.Hit(Scaled(p.BaseDamage)))
                PlayerHits[p.Id] = PlayerHits.TryGetValue(p.Id, out var h) ? h + 1 : 1;
            if (tele != null)
            {
                // 판정 순간 밝게 번쩍이고 사라진다
                tele.FadeLeft = 0.25f;
                SetColor(tele.Vis, new Color(1f, 0.25f, 0.15f, 0.6f));
            }
        }

        void SummonRing(BossPatternDef p, Vector2 center)
        {
            var def = DataBoot.Data.Enemy(p.Summon);
            if (def == null) { Debug.LogWarning($"[PatternRunner] 소환 적 없음 {p.Summon}"); return; }
            var sim = runner.enemies.Sim;
            foreach (var o in BossPatterns.RingOffsets(p))
            {
                var at = center + new Vector2(o.x, o.y) * _factor;
                int id = runner.enemies.SpawnAt(def, at);
                if (p.SummonSpeed <= 0f || def.Speed <= 0f) continue;
                for (int i = sim.Count - 1; i >= 0; i--)
                {
                    var a = sim.Get(i);
                    if (a.Id != id) continue;
                    a.Speed *= p.SummonSpeed / def.Speed;
                    sim.Set(i, a);
                    break;
                }
            }
        }

        void UpdateTeles(Vector2 boss)
        {
            Vector2 player = runner.player.position;
            for (int i = _teles.Count - 1; i >= 0; i--)
            {
                var t = _teles[i];
                if (t.FadeLeft >= 0f)
                {
                    t.FadeLeft -= Time.deltaTime;
                    if (t.FadeLeft < 0f) Remove(t);
                    continue;
                }
                if (t.Vis == null) continue;
                // 십팔나한진: 예고 동안 원이 플레이어를 따라온다(판정 순간 그 자리에 선다)
                if (t.P.Center == "player_at_resolve") { t.Center = player; t.Vis.transform.position = new Vector3(player.x, player.y, 0f); }
                // 판정이 다가올수록 짙게
                float k = t.P.TelegraphSeconds > 0f ? Mathf.Clamp01((_patterns.Elapsed - t.Start) / t.P.TelegraphSeconds) : 1f;
                SetColor(t.Vis, new Color(0.8f, 0.05f, 0.05f, Mathf.Lerp(0.15f, 0.45f, k)));
            }
        }

        void Remove(Tele t)
        {
            if (t.Vis != null) Destroy(t.Vis);
            _teles.Remove(t);
        }

        // 2막 붉은 빛줄기: 예고 동안 희미하게, 켜진 뒤 돌며 맞힌다(같은 대상은 hitIntervalSeconds 에 한 번)
        void UpdateBeams(Vector2 boss, float dt)
        {
            if (_patterns == null || !_patterns.InPhase2) return;
            Vector2 player = runner.player.position;
            var d = (player - boss) / _factor;
            int vi = 0;
            _beamHitLeft -= dt;
            foreach (var p in _patterns.Def.Phase2)
            {
                if (p.Shape != "rotating_beams") continue;
                float? on = _patterns.ActiveSeconds(p);
                float active = on ?? 0f;
                bool hitNow = false;
                for (int b = 0; b < Mathf.Max(1, p.Count); b++)
                {
                    float deg = BossPatterns.BeamDegrees(p, _towardPlayerAtPhase2, active, b);
                    while (_beamVis.Count <= vi) _beamVis.Add(Shape("붉은 빛줄기", Color.red, 3));
                    var go = _beamVis[vi++];
                    go.transform.position = new Vector3(boss.x, boss.y, 0f);
                    SetBeam(go, p.Length * _factor, p.Width * _factor, deg);
                    SetColor(go, on == null ? new Color(0.9f, 0.1f, 0.1f, 0.15f) : new Color(0.95f, 0.08f, 0.1f, 0.6f));
                    if (on != null && BossPatterns.InBeam(p, d.x, d.y, deg, PlayerRadius)) hitNow = true;
                }
                if (hitNow && _beamHitLeft <= 0f && runner.vitals != null)
                {
                    _beamHitLeft = p.HitIntervalSeconds;
                    if (runner.vitals.Hit(Scaled(p.BaseDamage))) PlayerHits[p.Id] = PlayerHits.TryGetValue(p.Id, out var h) ? h + 1 : 1;
                }
            }
        }

        // ---- 그리기(바닥 붉은 그림자. 붓 획 예고는 vfx.md 대로 나중에) ----

        GameObject Shape(string name, Color c, int order)
        {
            if (_mat == null) _mat = new Material(Shader.Find("Sprites/Default"));
            var go = new GameObject($"[패턴] {name}");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = new Mesh();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _mat;
            mr.sortingOrder = order;
            SetColor(go, c);
            return go;
        }

        static void SetColor(GameObject go, Color c)
        {
            if (go == null) return;
            _lastColor[go.GetInstanceID()] = c;
            var m = go.GetComponent<MeshFilter>().sharedMesh;
            if (m.vertexCount == 0) return;
            var cols = new Color[m.vertexCount];
            for (int i = 0; i < cols.Length; i++) cols[i] = c;
            m.colors = cols;
        }

        static readonly Dictionary<int, Color> _lastColor = new Dictionary<int, Color>();

        static void Apply(GameObject go, List<Vector3> v, List<int> tri)
        {
            var m = go.GetComponent<MeshFilter>().sharedMesh;
            m.Clear();
            m.SetVertices(v);
            m.SetTriangles(tri, 0);
            var c = _lastColor.TryGetValue(go.GetInstanceID(), out var lc) ? lc : new Color(0.8f, 0.05f, 0.05f, 0.3f);
            var cols = new Color[v.Count];
            for (int i = 0; i < cols.Length; i++) cols[i] = c;
            m.colors = cols;
            m.RecalculateBounds();
        }

        static void SetFan(GameObject go, Vector2 c, float r, float facing, float angle)
        {
            var v = new List<Vector3> { c };
            var tri = new List<int>();
            int seg = Mathf.Max(6, Mathf.CeilToInt(angle / 4f));
            for (int i = 0; i <= seg; i++)
            {
                float a = (facing - angle * 0.5f + angle * i / seg) * Mathf.Deg2Rad;
                v.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                if (i > 0) { tri.Add(0); tri.Add(i + 1); tri.Add(i); }
            }
            Apply(go, v, tri);
        }

        static void SetRing(GameObject go, Vector2 c, float r, float w)
        {
            var v = new List<Vector3>();
            var tri = new List<int>();
            const int seg = 64;
            for (int i = 0; i <= seg; i++)
            {
                float a = 2f * Mathf.PI * i / seg;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                v.Add(c + dir * (r - w * 0.5f));
                v.Add(c + dir * (r + w * 0.5f));
                if (i > 0)
                {
                    int k = i * 2;
                    tri.Add(k - 2); tri.Add(k - 1); tri.Add(k);
                    tri.Add(k - 1); tri.Add(k + 1); tri.Add(k);
                }
            }
            Apply(go, v, tri);
        }

        static void SetCircles(GameObject go, Vector2[] centers, float r)
        {
            var v = new List<Vector3>();
            var tri = new List<int>();
            const int seg = 32;
            foreach (var c in centers)
            {
                int b = v.Count;
                v.Add(c);
                for (int i = 0; i <= seg; i++)
                {
                    float a = 2f * Mathf.PI * i / seg;
                    v.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                    if (i > 0) { tri.Add(b); tri.Add(b + i + 1); tri.Add(b + i); }
                }
            }
            Apply(go, v, tri);
        }

        static void SetBeam(GameObject go, float length, float width, float deg)
        {
            float a = deg * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            var n = new Vector2(-dir.y, dir.x) * (width * 0.5f);
            var v = new List<Vector3> { -n, n, dir * length + n, dir * length - n };
            Apply(go, v, new List<int> { 0, 2, 1, 0, 3, 2 });
        }

        // ---- 시험 ----

        /// <summary>시험: 그 보스가 있는 지역을 깨워(필요하면) 바로 싸움을 건다.</summary>
        public string ForceFight(string bossId)
        {
            var life = runner != null ? runner.Life : null;
            if (life == null) return "한 생 없음";
            if (life.Bosses.InFight) return "이미 보스전 중";
            var region = DataBoot.Data.Cheonha.Regions.FirstOrDefault(r => r.Boss == bossId);
            if (region == null) return $"{bossId} 지역 없음";
            if (life.Bosses.State(region.Id) != BossRegionState.Awake)
            {
                var f = typeof(BossAwakening).GetField("_state", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var dict = (Dictionary<string, BossRegionState>)f.GetValue(life.Bosses);
                dict[region.Id] = BossRegionState.Awake;
            }
            var fight = life.Bosses.Engage(region.Id, life.Level, life.Minutes);
            return $"{fight.Boss.NameKo} 기혈 {fight.MaxHealth:0}";
        }

        /// <summary>시험: 보스 남은 기혈 비율을 바꾼다(2막 확인).</summary>
        public void SetBossHealthRatio(float ratio)
        {
            int idx = BossIndex();
            if (idx < 0) return;
            var sim = runner.enemies.Sim;
            var a = sim.Get(idx);
            a.Health = a.MaxHealth * ratio;
            sim.Set(idx, a);
        }

        public float BossHealthRatio
        {
            get
            {
                int idx = BossIndex();
                if (idx < 0) return 0f;
                var a = runner.enemies.Sim.Get(idx);
                return a.MaxHealth > 0f ? a.Health / a.MaxHealth : 0f;
            }
        }
    }
}
