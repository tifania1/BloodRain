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
    //     예고는 옅은 핏빛 물 + 짙은 테두리 + 판정까지 차오르는 안쪽(DrawTele, 2026-11-21).
    //     다른 땅의 주인 모양은 각자 항목(R-14U·R4-02U)에서 이 틀에 더한다.
    //   - BR-03 범종 낙하(leaveGimmick bell)는 원 자리마다 Core BellGimmick 을 남긴다. 닿으면 울려 둘레 적을 세우고(보스는 Stun),
    //     한 번 울렸거나 수명이 다하면 치운다. 싸움이 끝나도 남은 수명만큼은 둔다(웹 G.bells).
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
        readonly List<(BellGimmick bell, Vector2 at, GameObject vis)> _bells = new List<(BellGimmick, Vector2, GameObject)>();
        /// <summary>시험·로그용: 남은 종 수, 울린 수.</summary>
        public int BellCount => _bells.Count;
        public int BellRings { get; private set; }
        Material _mat;
        GameObject _bossMark;
        // C-10a 서장 장삼: 땅의 주인 규칙(BossAwakening) 밖의 싸움. 기혈은 Prologue 바닥(10%)을 거쳐야 해서
        // 덩이 기혈은 넉넉한 완충(_buffer)으로 두고, 깎인 만큼을 life.DamagePrologueBoss 로 진짜 기혈(_realHealth)에 옮긴다.
        bool _prologue, _prologueDone;
        float _realHealth, _buffer;
        // 한 시전 안에서 여러 번 일어나는 일(jump_slam 세 번 뛰기). 패턴 시계(Elapsed) 기준
        readonly List<(float at, System.Action act)> _later = new List<(float at, System.Action act)>();

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
            // 그림 다시 그리기용(2026-11-21): 반경·길이·폭은 세계 길이, Offs 는 Center 기준 원 자리
            public float R, Len, Wid;
            public Vector2[] Offs;
        }

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; Clear(); ClearBells(); }

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
            UpdateBells(Time.deltaTime);
            var fight = life.Bosses.Fight;
            if (!_prologue && fight != _fight)
            {
                if (_fight != null) EndFight();
                if (fight != null) BeginFight(fight, life);
            }
            if (_fight == null && !_prologueDone && life.IsPrologue && life.Prologue.Step == BloodRain.Core.Run.PrologueStep.Boss)
                BeginPrologueFight(life);
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
            if (_prologue)
            {
                float dmg = _buffer - a.Health;
                if (dmg > 0f) { _realHealth = life.DamagePrologueBoss(_realHealth, _fight.MaxHealth, dmg); a.Health = _buffer; }
            }
            sim.Set(idx, a);
            if (_prologue && _realHealth <= 0f) { runner.enemies.Kill(idx); return; } // 각성 뒤에만 0 이 된다(Prologue 바닥)

            Vector2 boss = new Vector2(a.Pos.x, a.Pos.y);
            if (_bossMark != null) { _bossMark.transform.position = new Vector3(boss.x, boss.y, 0f); }

            if (_stun <= 0f)
            {
                float ratio = _prologue ? _realHealth / _fight.MaxHealth : a.MaxHealth > 0f ? a.Health / a.MaxHealth : 1f;
                foreach (var e in _patterns.Tick(dt, ratio)) Handle(e, boss);
                for (int i = 0; i < _later.Count;)
                {
                    if (_later[i].at > _patterns.Elapsed) { i++; continue; }
                    var act = _later[i].act;
                    _later.RemoveAt(i);
                    act();
                    if (_fight == null) return;
                }
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
                if (_prologue) { _realHealth = fight.MaxHealth; _buffer = a.Health = a.MaxHealth = fight.MaxHealth * 10f; }
                sim.Set(idx, a);
            }
            _bossMark = Shape("땅의 주인 표시", new Color(0.9f, 0.1f, 0.08f, 0.55f), 6);
            SetRing(_bossMark, Vector2.zero, bossBodyRadius * _factor * 1.15f, 0.12f * _factor);
            LastEvent = $"보스전 시작 {fight.Boss.NameKo} 기혈 {fight.MaxHealth:0} 축척 {_factor:0.###}";
            Debug.Log($"[PatternRunner] {LastEvent}");
            runner.ShowPopup(Loc.Boss(fight.Boss));
            SfxPlayer.Instance?.Play("sfx_boss_appear");
        }

        /// <summary>C-10a 서장 장삼전. 기혈은 Prologue.BossMaxHealth(enemies.json 그대로), 2막 없음.</summary>
        void BeginPrologueFight(BloodRain.Core.Run.Life life)
        {
            var data = DataBoot.Data;
            var pb = data.Cheonha.PrologueBody;
            var boss = data.Bosses.First(b => b.Id == pb.Boss);
            var region = data.Cheonha.Regions.FirstOrDefault(r => r.Id == pb.Region) ?? data.Cheonha.Regions[0];
            var ws = WorldScale.Current;
            _prologue = true;
            BeginFight(new BossFight(region, boss, ws != null ? ws.Realm : 0, life.Minutes, BloodRain.Core.Run.Prologue.BossMaxHealth(boss), false), life);
        }

        void EndFight()
        {
            if (_prologue) _prologueDone = true;
            _prologue = false;
            _later.Clear();
            Clear();
            _fight = null;
            _patterns = null;
            _bossId = -1;
        }

        void Clear()
        {
            foreach (var t in _teles) if (t.Vis != null) Kill(t.Vis);
            _teles.Clear();
            foreach (var b in _beamVis) if (b != null) Kill(b);
            _beamVis.Clear();
            if (_bossMark != null) Kill(_bossMark);
            _bossMark = null;
        }

        void OnKilled(Vector2 at, EnemyAgent a)
        {
            if (_fight == null || a.Id != _bossId) return;
            var life = runner.Life;
            if (_prologue)
            {
                Debug.Log($"[PatternRunner] 서장 {_fight.Boss.NameKo} 쓰러짐");
                LastEvent = "쓰러짐";
                EndFight();
                runner.EndPrologue(); // C-10b KillPrologueBoss → 컷 → 객잔 침방 첫 환혼
                return;
            }
            Debug.Log($"[PatternRunner] {_fight.Boss.NameKo} 쓰러짐");
            LastEvent = "쓰러짐";
            if (life != null && life.Bosses.InFight && life.Record == null)
            {
                var kill = life.DefeatBoss(_fight.IsFinal ? life.Silver : (int?)null);
                if (kill.Chest) BloodRain.Presentation.Fx.ChestLayer.Instance?.Place(at); // BR-02 처치 보갑(C-09d 보갑 길)
                var notice = BossKillNotice.Of(DataBoot.Data, Loc.Current, kill); // BR-02 짧은 붓글씨(종장이면 null)
                var banner = runner.GetComponent<BloodRain.Presentation.Hud.BossKillBanner>();
                if (notice != null && banner != null) banner.Show(notice);
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
                // 2막 모습(boss_<id>_p2 원화가 있으면 — 원각은 핏빛 각성 모습)
                if (_fight != null) runner.enemies.SetViewArt(_bossId, runner.enemies.ArtNamed("boss_" + _fight.Boss.Id + "_p2"));
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
                        t.Vis = Shape(p.Id, TeleWash, 2);
                        t.R = p.Radius * _factor;
                        break;
                    case "summon_ring":
                        t.Vis = Shape(p.Id, TeleWash, 2);
                        t.R = p.Radius * _factor; t.Wid = 0.25f * _factor;
                        break;
                    case "circles":
                        var offs = BossPatterns.ScatterOffsets(p, _rng);
                        t.Circles = offs.Select(o => player + new Vector2(o.x, o.y) * _factor).ToArray();
                        t.Vis = Shape(p.Id, TeleWash, 2);
                        t.Offs = t.Circles.Select(c => c - player).ToArray();
                        t.R = p.Radius * _factor;
                        t.Center = player;
                        break;
                    case "rotating_beams":
                        t.Center = boss;
                        break;
                    case "bind_line":
                        t.Center = boss;
                        t.Vis = Shape(p.Id, TeleWash, 2);
                        t.Len = p.Length * _factor; t.Wid = p.Width * _factor;
                        break;
                    case "summon":
                        t.Vis = Shape(p.Id, TeleWash, 2);
                        t.R = p.Scatter * _factor; t.Wid = 0.15f * _factor;
                        break;
                    case "jump_slam":
                        t.Cast = -100; // 판정은 _later 가 뛸 때마다 한다(Resolve 가 지우지 않게)
                        t.Vis = Shape(p.Id, TeleWash, 2);
                        t.Offs = new[] { Vector2.zero }; t.R = p.Radius * _factor;
                        ScheduleJumps(p, e.At, t);
                        break;
                    default:
                        Debug.LogWarning($"[PatternRunner] 아직 안 그리는 모양 {p.Shape} ({p.Id})");
                        break;
                }
                if (t.Vis != null) { t.Vis.transform.position = new Vector3(t.Center.x, t.Center.y, 0f); DrawTele(t, 0f, 0f); }
                _teles.Add(t);
                LastEvent = $"예고 {p.NameKo}";
                return;
            }

            // Resolve
            Resolved[p.Id] = Resolved.TryGetValue(p.Id, out var n) ? n + 1 : 1;
            LastEvent = $"판정 {p.NameKo}";
            var tele = _teles.FirstOrDefault(x => x.P == p && x.Cast == e.Cast && x.FadeLeft < 0f);
            if (p.Continuous) { if (tele != null) Remove(tele); return; } // 2막 빛줄기는 UpdateBeams 가 맞힌다
            if (p.Shape == "jump_slam") return; // ScheduleJumps 가 뛸 때마다
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
                case "summon":
                {
                    var c = tele != null ? tele.Center : player;
                    Summon(p, c, BloodRain.Core.Combat.PatternShapes.SummonOffsets(p, _rng));
                    break;
                }
                case "bind_line":
                {
                    var c = tele != null ? tele.Center : boss;
                    var d = (player - c) / _factor;
                    hit = BossPatterns.InLine(p.Length, p.Width, d.x, d.y, tele != null ? tele.Facing : Deg(player - boss), pr);
                    if (hit && p.RootSeconds > 0f && !(runner.vitals != null && runner.vitals.Invulnerable))
                    {
                        var pc = runner.player.GetComponent<PlayerController>();
                        if (pc != null) pc.Bind(p.RootSeconds); // 경공으로 끊는 것은 PlayerController 가
                    }
                    break;
                }
                case "circles":
                {
                    if (tele?.Circles != null)
                        foreach (var c in tele.Circles)
                        {
                            var d = (player - c) / _factor;
                            if (BossPatterns.InCircle(p.Radius, d.x, d.y, pr)) hit = true;
                            if (p.LeaveGimmick == "bell") LeaveBell(p, c);
                        }
                    break;
                }
            }
            if (hit && p.BaseDamage > 0f && runner.vitals != null && runner.vitals.Hit(Scaled(p.BaseDamage)))
                PlayerHits[p.Id] = PlayerHits.TryGetValue(p.Id, out var h) ? h + 1 : 1;
            if (tele != null)
            {
                // 판정 순간 밝게 번쩍이고 사라진다
                tele.FadeLeft = TeleFlashSeconds;
                DrawTele(tele, 1f, 1f);
            }
        }

        // jump_slam: Count 번 뛴다. k 번째는 JumpTakeoffSeconds(k) 에 그 순간 플레이어 자리에 그림자, JumpLandSeconds(k) 에 내려찍음
        void ScheduleJumps(BossPatternDef p, float castAt, Tele first)
        {
            int n = Mathf.Max(1, p.Count);
            for (int k = 0; k < n; k++)
            {
                int kk = k;
                Tele[] slot = { kk == 0 ? first : null };
                if (kk > 0)
                    _later.Add((castAt + BloodRain.Core.Combat.PatternShapes.JumpTakeoffSeconds(p, kk), () =>
                    {
                        Vector2 pl = runner.player.position;
                        var t = new Tele { P = p, Cast = -100 - kk, Start = _patterns.Elapsed, Center = pl };
                        t.Vis = Shape(p.Id, TeleWash, 2);
                        t.Offs = new[] { Vector2.zero }; t.R = p.Radius * _factor;
                        t.Vis.transform.position = new Vector3(pl.x, pl.y, 0f);
                        DrawTele(t, 0f, 0f);
                        _teles.Add(t);
                        slot[0] = t;
                    }));
                _later.Add((castAt + BloodRain.Core.Combat.PatternShapes.JumpLandSeconds(p, kk), () =>
                {
                    var t = slot[0];
                    if (t == null) return;
                    Vector2 pl = runner.player.position;
                    var d = (pl - t.Center) / _factor;
                    int idx = BossIndex();
                    if (idx >= 0) { var a = runner.enemies.Sim.Get(idx); a.Pos = new float2(t.Center.x, t.Center.y); runner.enemies.Sim.Set(idx, a); }
                    if (BossPatterns.InCircle(p.Radius, d.x, d.y, PlayerRadius) && runner.vitals != null && runner.vitals.Hit(Scaled(p.BaseDamage)))
                        PlayerHits[p.Id] = PlayerHits.TryGetValue(p.Id, out var h) ? h + 1 : 1;
                    JumpLands++;
                    t.FadeLeft = TeleFlashSeconds;
                    DrawTele(t, 1f, 1f);
                }));
            }
        }

        /// <summary>시험용: jump_slam 이 내려찍은 수.</summary>
        public int JumpLands { get; private set; }

        void Summon(BossPatternDef p, Vector2 center, (float x, float y)[] offsets)
        {
            var def = DataBoot.Data.Enemy(p.Summon);
            if (def == null) { Debug.LogWarning($"[PatternRunner] 소환 적 없음 {p.Summon}"); return; }
            foreach (var o in offsets) runner.enemies.SpawnAt(def, center + new Vector2(o.x, o.y) * _factor);
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
                    else DrawTele(t, 1f, t.FadeLeft / TeleFlashSeconds);
                    continue;
                }
                if (t.Vis == null) continue;
                // 십팔나한진: 예고 동안 원이 플레이어를 따라온다(판정 순간 그 자리에 선다)
                if (t.P.Center == "player_at_resolve") { t.Center = player; t.Vis.transform.position = new Vector3(player.x, player.y, 0f); }
                // 판정이 다가올수록 안쪽이 차오르고 테두리가 짙어진다
                float k = t.P.TelegraphSeconds > 0f ? Mathf.Clamp01((_patterns.Elapsed - t.Start) / t.P.TelegraphSeconds) : 1f;
                DrawTele(t, k, 0f);
            }
        }

        void Remove(Tele t)
        {
            if (t.Vis != null) Kill(t.Vis);
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
                    DrawBeam(go, p.Length * _factor, p.Width * _factor, deg, on != null);
                    if (on != null && BossPatterns.InBeam(p, d.x, d.y, deg, PlayerRadius)) hitNow = true;
                }
                if (hitNow && _beamHitLeft <= 0f && runner.vitals != null)
                {
                    _beamHitLeft = p.HitIntervalSeconds;
                    if (runner.vitals.Hit(Scaled(p.BaseDamage))) PlayerHits[p.Id] = PlayerHits.TryGetValue(p.Id, out var h) ? h + 1 : 1;
                }
            }
        }

        // ---- BR-03 범종 낙하가 남기는 범종 ----

        void LeaveBell(BossPatternDef p, Vector2 at)
        {
            var bell = BellGimmick.FromBossDrop(DataBoot.Data.Gimmicks.Bell, p, _factor);
            // 지역 범종(GimmickLayer)과 같은 그림·크기(2026-11-21 — 전엔 먹 원 + 金 테두리 자리 표시). 떨어질 때 먹 고리가 퍼진다
            var vis = Fx.PropArt.Make(transform, "범종", "prop_stupa_bell", at, bell.TouchRadius * 1.6f, 4);
            if (vis == null)
            {
                vis = Shape("범종", new Color(0.08f, 0.07f, 0.06f, 0.85f), 4);
                SetCircles(vis, new[] { Vector2.zero }, bell.TouchRadius * 0.6f);
                var rim = Shape("범종 테", new Color(0.86f, 0.68f, 0.22f, 0.95f), 5);
                rim.transform.SetParent(vis.transform, false);
                SetRing(rim, Vector2.zero, bell.TouchRadius * 0.6f, 0.12f * _factor);
                vis.transform.position = new Vector3(at.x, at.y, 0f);
            }
            var fx = Fx.SectFx.Instance;
            if (fx != null && fx.Ready && !Fx.FxSettings.Simplified) { fx.RingW(at, bell.TouchRadius * 2.4f, Fx.FxRamp.Ink, 0.35f, 0.7f, false, true); fx.CrackW(at, bell.TouchRadius * 1.6f, Fx.FxRamp.Ink); }
            _bells.Add((bell, at, vis));
        }

        void UpdateBells(float dt)
        {
            if (_bells.Count == 0) return;
            Vector2 player = runner.player.position;
            for (int i = _bells.Count - 1; i >= 0; i--)
            {
                var (bell, at, vis) = _bells[i];
                bell.Tick(dt);
                if (bell.TryRing(Vector2.Distance(player, at))) Ring(bell, at);
                if (bell.Gone)
                {
                    if (vis != null) Kill(vis);
                    _bells.RemoveAt(i);
                }
            }
        }

        void Ring(BellGimmick bell, Vector2 at)
        {
            BellRings++;
            SfxPlayer.Instance?.Play("sfx_gimmick_bell");
            // 울림: 종 둘레 금빛 소리 고리 + 먹 고리가 적을 세우는 반경까지(지역 범종 GimmickLayer 와 같게)
            var fx = Fx.SectFx.Instance;
            if (fx != null && fx.Ready) { fx.RingW(at, bell.TouchRadius * 3f, Fx.FxRamp.Gold, 0.5f, 0.8f, true); fx.RingW(at, bell.StunRadius * 2.2f, Fx.FxRamp.Ink, 0.55f, 0.32f); }
            var sim = runner.enemies.Sim;
            if (sim == null) return;
            int stunned = 0;
            for (int i = 0; i < sim.Count; i++)
            {
                var a = sim.Get(i);
                if (a.Health <= 0f) continue;
                float dist = Vector2.Distance(at, new Vector2(a.Pos.x, a.Pos.y));
                bool isBoss = a.Id == _bossId && _fight != null;
                var def = runner.enemies.DefOf(a.Kind);
                var kind = isBoss || (def != null && def.Family == "boss") ? GimmickTargetKind.Boss
                    : def != null && def.Family == "elite" ? GimmickTargetKind.Elite : GimmickTargetKind.Normal;
                float s = bell.StunSeconds(kind, dist);
                if (s <= 0f) continue;
                stunned++;
                if (isBoss) { Stun(s); continue; } // 보스는 패턴 시계도 멈춘다
                a.Stun = Mathf.Max(a.Stun, s);
                sim.Set(i, a);
            }
            LastEvent = $"범종 울림 {stunned}";
            Debug.Log($"[PatternRunner] 범종 울림 — 세운 적 {stunned}");
        }

        void ClearBells()
        {
            foreach (var b in _bells) if (b.vis != null) Kill(b.vis);
            _bells.Clear();
        }

        // ---- 그리기(범종·땅의 주인 표시는 단색 메쉬. 예고는 아래 '예고 그림') ----

        /// <summary>예고 그림을 지운다. Shape 가 그림마다 새로 만든 Mesh 도 같이(GameObject 만 지우면 Mesh 가 남아 땅의 주인 싸움 내내 쌓였다, 2026-11-20).</summary>
        static void Kill(GameObject go)
        {
            var mf = go.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) Destroy(mf.sharedMesh);
            Destroy(go);
        }

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
            c = Hud.DangerColors.Of(c); // UI-03 색각 보정: 붉은 예고만
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

        // ---- 예고 그림(2026-11-21) ----
        // 예전엔 납작한 붉은 판 한 장이 짙어지기만 했다. 이제 옅은 핏빛 물(가운데 묽고 가장자리 짙게) + 짙은 테두리(= 맞는 경계)
        // + 판정까지 안쪽에서 차오르는 물(차오름이 테두리에 닿는 순간이 판정)로 그린다. 판정 순간 밝은 핏빛으로 번쩍이고 스러진다.
        // 모양은 판정 모양 그대로(원·부채꼴·띠·고리). 색은 모두 DangerColors(색각 보정)를 거친다. 메쉬는 예고마다 하나(Kill 이 지움).
        static readonly Color TeleWash = new Color(0.62f, 0.04f, 0.05f, 0.25f);
        static readonly Color TeleRim = new Color(0.36f, 0.02f, 0.03f, 1f);
        static readonly Color TeleHot = new Color(1f, 0.25f, 0.15f, 1f);
        const float TeleFlashSeconds = 0.25f;

        sealed class MeshBuf
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Color> C = new List<Color>();
            public readonly List<int> T = new List<int>();
            public void Clear() { V.Clear(); C.Clear(); T.Clear(); }
        }
        static readonly MeshBuf _buf = new MeshBuf();

        static Color A(Color c, float a) { c.a = Mathf.Clamp01(a); return c; }
        static Vector2 Dir(float deg) { float a = deg * Mathf.Deg2Rad; return new Vector2(Mathf.Cos(a), Mathf.Sin(a)); }
        static void Push(MeshBuf b, Vector2 p, Color c) { b.V.Add(p); b.C.Add(c); }

        static void BufDisc(MeshBuf b, Vector2 c, float r, Color cIn, Color cOut, int seg = 40)
        {
            if (r <= 1e-4f) return;
            int o = b.V.Count;
            Push(b, c, cIn);
            for (int i = 0; i <= seg; i++)
            {
                Push(b, c + Dir(360f * i / seg) * r, cOut);
                if (i > 0) { b.T.Add(o); b.T.Add(o + i + 1); b.T.Add(o + i); }
            }
        }

        static void BufRing(MeshBuf b, Vector2 c, float r, float w, Color col, float from = 0f, float span = 360f)
        {
            if (r <= 1e-4f) return;
            int o = b.V.Count, seg = Mathf.Max(8, Mathf.CeilToInt(span / 6f));
            for (int i = 0; i <= seg; i++)
            {
                var d = Dir(from + span * i / seg);
                Push(b, c + d * Mathf.Max(0f, r - w * 0.5f), col);
                Push(b, c + d * (r + w * 0.5f), col);
                if (i > 0) { int k = o + i * 2; b.T.Add(k - 2); b.T.Add(k - 1); b.T.Add(k); b.T.Add(k - 1); b.T.Add(k + 1); b.T.Add(k); }
            }
        }

        static void BufFan(MeshBuf b, Vector2 c, float r, float facing, float angle, Color cIn, Color cOut)
        {
            if (r <= 1e-4f) return;
            int o = b.V.Count, seg = Mathf.Max(6, Mathf.CeilToInt(angle / 4f));
            Push(b, c, cIn);
            for (int i = 0; i <= seg; i++)
            {
                Push(b, c + Dir(facing - angle * 0.5f + angle * i / seg) * r, cOut);
                if (i > 0) { b.T.Add(o); b.T.Add(o + i + 1); b.T.Add(o + i); }
            }
        }

        static void BufLine(MeshBuf b, Vector2 p0, Vector2 p1, float w, Color col)
        {
            var d = p1 - p0;
            if (d.sqrMagnitude < 1e-8f) return;
            var n = new Vector2(-d.y, d.x).normalized * (w * 0.5f);
            int o = b.V.Count;
            Push(b, p0 - n, col); Push(b, p0 + n, col); Push(b, p1 + n, col); Push(b, p1 - n, col);
            b.T.Add(o); b.T.Add(o + 2); b.T.Add(o + 1); b.T.Add(o); b.T.Add(o + 3); b.T.Add(o + 2);
        }

        // c 에서 deg 쪽으로 length 뻗는 폭 width 띠. 가운데 줄 cMid, 양 가장자리 cEdge
        static void BufBeam(MeshBuf b, Vector2 c, float length, float width, float deg, Color cMid, Color cEdge)
        {
            if (length <= 1e-4f) return;
            var dir = Dir(deg);
            var n = new Vector2(-dir.y, dir.x) * (width * 0.5f);
            var e = c + dir * length;
            int o = b.V.Count;
            Push(b, c - n, cEdge); Push(b, c, cMid); Push(b, c + n, cEdge);
            Push(b, e - n, cEdge); Push(b, e, cMid); Push(b, e + n, cEdge);
            b.T.Add(o); b.T.Add(o + 4); b.T.Add(o + 1); b.T.Add(o); b.T.Add(o + 3); b.T.Add(o + 4);
            b.T.Add(o + 1); b.T.Add(o + 5); b.T.Add(o + 2); b.T.Add(o + 1); b.T.Add(o + 4); b.T.Add(o + 5);
        }

        static void BufBeamRim(MeshBuf b, Vector2 c, float length, float width, float deg, float w, Color col)
        {
            var dir = Dir(deg);
            var n = new Vector2(-dir.y, dir.x) * (width * 0.5f);
            var e = c + dir * length;
            BufLine(b, c - n, e - n, w, col);
            BufLine(b, c + n, e + n, w, col);
            BufLine(b, e - n, e + n, w, col);
        }

        static void ApplyBuf(GameObject go, MeshBuf b)
        {
            if (go == null) return;
            var m = go.GetComponent<MeshFilter>().sharedMesh;
            m.Clear();
            m.SetVertices(b.V);
            m.SetColors(b.C);
            m.SetTriangles(b.T, 0);
            m.RecalculateBounds();
        }

        /// <summary>예고 하나를 다시 그린다. k = 판정까지 진행(0~1), flash = 판정 번쩍임(1 에서 0 으로).</summary>
        void DrawTele(Tele t, float k, float flash)
        {
            if (t.Vis == null) return;
            var b = _buf;
            b.Clear();
            Color wash = Hud.DangerColors.Of(TeleWash), rim = Hud.DangerColors.Of(TeleRim), hot = Hud.DangerColors.Of(TeleHot);
            bool fl = flash > 0f;
            if (fl) { wash = Color.Lerp(wash, hot, 0.55f); rim = Color.Lerp(rim, hot, 0.6f); }
            float aIn = fl ? 0.4f * flash : 0.05f, aOut = fl ? 0.6f * flash : 0.2f + 0.1f * k;
            float aFill = fl ? 0f : 0.14f + 0.14f * k, aRim = fl ? flash : 0.5f + 0.45f * k;
            float rw = 0.07f * _factor;                 // 테두리 굵기(화면 m 0.07)
            var p = t.P;
            switch (p.Shape)
            {
                case "circles":
                case "jump_slam":
                    if (t.Offs == null) break;
                    foreach (var o in t.Offs)
                    {
                        BufDisc(b, o, t.R, A(wash, aIn), A(wash, aOut));
                        if (aFill > 0f) BufDisc(b, o, t.R * k, A(wash, aFill), A(wash, aFill * 1.5f));
                        BufRing(b, o, t.R, rw, A(rim, aRim));
                    }
                    break;
                case "cone":
                {
                    float ang = p.AngleDegrees;
                    BufFan(b, Vector2.zero, t.R, t.Facing, ang, A(wash, aIn), A(wash, aOut));
                    if (aFill > 0f) BufFan(b, Vector2.zero, t.R * k, t.Facing, ang, A(wash, aFill), A(wash, aFill * 1.5f));
                    BufRing(b, Vector2.zero, t.R, rw, A(rim, aRim), t.Facing - ang * 0.5f, ang);
                    if (ang < 359f)
                    {
                        BufLine(b, Vector2.zero, Dir(t.Facing - ang * 0.5f) * t.R, rw, A(rim, aRim));
                        BufLine(b, Vector2.zero, Dir(t.Facing + ang * 0.5f) * t.R, rw, A(rim, aRim));
                    }
                    break;
                }
                case "bind_line":
                    BufBeam(b, Vector2.zero, t.Len, t.Wid, t.Facing, A(wash, aIn), A(wash, aOut));
                    if (aFill > 0f) BufBeam(b, Vector2.zero, t.Len * k, t.Wid, t.Facing, A(wash, aFill), A(wash, aFill * 1.5f));
                    BufBeamRim(b, Vector2.zero, t.Len, t.Wid, t.Facing, rw, A(rim, aRim));
                    break;
                default: // summon_ring·summon: 적이 솟을 고리 띠. 차오름 대신 띠가 짙어지고 안팎 테두리
                    BufRing(b, Vector2.zero, t.R, t.Wid, A(wash, fl ? 0.6f * flash : 0.18f + 0.3f * k));
                    BufRing(b, Vector2.zero, t.R - t.Wid * 0.5f, rw * 0.7f, A(rim, aRim * 0.8f));
                    BufRing(b, Vector2.zero, t.R + t.Wid * 0.5f, rw * 0.7f, A(rim, aRim * 0.8f));
                    break;
            }
            ApplyBuf(t.Vis, b);
        }

        /// <summary>2막 붉은 빛줄기. 켜지기 전엔 옅은 띠와 테두리, 켜지면 가운데가 밝은 핏빛 빛줄기.</summary>
        void DrawBeam(GameObject go, float length, float width, float deg, bool on)
        {
            var b = _buf;
            b.Clear();
            Color wash = Hud.DangerColors.Of(TeleWash), rim = Hud.DangerColors.Of(TeleRim), hot = Hud.DangerColors.Of(TeleHot);
            float rw = 0.06f * _factor;
            if (on)
            {
                BufBeam(b, Vector2.zero, length, width * 1.25f, deg, A(wash, 0.35f), A(wash, 0f));   // 번짐
                BufBeam(b, Vector2.zero, length, width, deg, A(hot, 0.8f), A(wash, 0.5f));
                BufBeamRim(b, Vector2.zero, length, width, deg, rw, A(rim, 0.75f));
            }
            else
            {
                BufBeam(b, Vector2.zero, length, width, deg, A(wash, 0.06f), A(wash, 0.2f));
                BufBeamRim(b, Vector2.zero, length, width, deg, rw, A(rim, 0.45f));
            }
            ApplyBuf(go, b);
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
            if (_prologue) { _realHealth = _fight.MaxHealth * ratio; return; }
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
                if (_prologue) return _realHealth / _fight.MaxHealth;
                var a = runner.enemies.Sim.Get(idx);
                return a.MaxHealth > 0f ? a.Health / a.MaxHealth : 0f;
            }
        }
    }
}
