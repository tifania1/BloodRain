using System.Collections.Generic;
using System.Diagnostics;
using BloodRain.Core.Data;
using BloodRain.Core.Run;
using BloodRain.Presentation.Fx;
using BloodRain.Sim;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BloodRain.Presentation
{
    // 적 무리(C-02). EnemySim(Burst 잡)을 돌리고 결과를 덩이 렌더러(ClusterRenderer)에 넘긴다.
    //   속도 = enemies.json speed / 100 × 기본 걸음(balance.json stats.moveBase) → WorldScale 로 세계 m/s, 혈월이면 Life.EnemySpeedMultiplier.
    //   기혈 = enemies.json health × Life.EnemyHealthMultiplier(시간·혈운 농도 배율은 생성 디렉터 C-10 에서).
    //   생성 고리·되돌리기 거리는 balance.json spawnTarget(화면 대각선 배수).
    // 지금은 목표 수만큼 채워 두는 시험용 생성만 있다. 진짜 생성 흐름은 C-10(LifeDirector).
    public sealed class EnemyField : MonoBehaviour
    {
        [Tooltip("채워 둘 적 수. [ ] 키로 반·두 배")]
        public int targetCount = 300;
        public int capacity = 4096;
        [Tooltip("한 프레임에 새로 넣는 최대 수")]
        public int spawnPerFrame = 60;
        public Transform player;
        public ClusterRenderer clusters;
        [Tooltip("접촉 피해를 받을 쪽. 비우면 player 에서 찾는다")]
        public PlayerVitals vitals;
        public bool showStats = true;
        [Tooltip("플레이어에게 들어가는 모든 적 피해 배율. 혈마의 그림자(C-12)가 EnemyDamageMultiplier 를 넣는다.")]
        public float damageMultiplier = 1f;

        bool HitPlayer(float damage) => vitals != null && vitals.Hit(damage * damageMultiplier);

        float _playerBody = 0.8f;
        PlayerController _playerMove;
        static readonly Color TelegraphTint = new Color(0.95f, 0.15f, 0.1f);
        static readonly Color BuffTint = new Color(0.75f, 0.12f, 0.10f);
        readonly List<EnemySim.ContactHit> _hits = new List<EnemySim.ContactHit>();

        // 시험용 생성표: (적 id, 가중치)
        static readonly (string id, float w)[] Mix =
        {
            ("sigang", 50f), ("hyeol_gangsi", 22f), ("hyeolin", 10f), ("hyeol_musa", 10f), ("donggap", 8f), ("gisu", 1f), ("hyeol_gungsu", 4f), ("ice_gangsi", 4f), ("hyeol_sulsa", 3f), ("mogin_nahan", 4f), ("poison_toad", 4f), ("hyeolyeong_assassin", 2f), ("bigang", 4f), ("twin_gangsi", 2f), ("water_bandit", 3f), ("dogin", 3f), ("blood_lion", 2f), ("snow_wolf", 1f), ("golden_centipede", 1f), ("demon_cultist", 1f),
        };

        struct Kind
        {
            public EnemyDef Def;
            public EnemyMove Move;
            public Color Tint;
            public float Weight;
        }

        EnemySim _sim;
        readonly List<Kind> _kinds = new List<Kind>();
        float _totalWeight;
        readonly Dictionary<int, ClusterView> _views = new Dictionary<int, ClusterView>();
        System.Random _rng = new System.Random(20261101);
        float _moveBase = 4.6f;
        float _healthMul = 1f, _speedMul = 1f;
        Vector2 _lastPlayer;
        Vector2 _heading;
        readonly Stopwatch _watch = new Stopwatch();
        float _simMs, _fps;

        /// <summary>적이 쓰러질 때(위치 세계 m, 그 적 상태). 진기 구슬(OrbLayer)이 여기서 떨어진다.</summary>
        public event System.Action<Vector2, EnemyAgent> Killed;

        /// <summary>index 자리 적을 쓰러뜨린다(무공 C-04 가 기혈을 0으로 만들면 이걸 부른다).</summary>
        public void Kill(int index)
        {
            var a = _sim.Get(index);
            _sim.RemoveAt(index);
            var def = DefOf(a.Kind);
            if (def?.Death != null) _deaths.Add((new Vector2(a.Pos.x, a.Pos.y), def, a.BornRealm));
            if (def?.Twin != null) OnTwinKilled(a, def);
            Killed?.Invoke(new Vector2(a.Pos.x, a.Pos.y), a);
        }

        readonly List<int> _near = new List<int>();

        /// <summary>시험용: center 둘레 radius(세계 m) 안 적을 모두 쓰러뜨린다. 자리 번호가 바뀌니 뒤에서부터.</summary>
        public int KillInside(Vector2 center, float radius)
        {
            _sim.QueryCircle(new float2(center.x, center.y), radius, _near);
            _near.Sort();
            int n = 0, last = -1;
            for (int k = _near.Count - 1; k >= 0; k--)
            {
                if (_near[k] == last) continue; // 해시가 겹친 칸에서 같은 적이 두 번 나올 수 있다
                last = _near[k];
                Kill(last);
                n++;
            }
            _sim.RebuildGrid(Mathf.Max(0.01f, RadiusOf(World.Realm) * 2.2f));
            return n;
        }

        readonly List<int> _dead = new List<int>();

        /// <summary>무공 한 번(C-04). indices 자리 적에게 dmg 를 주고, from 에서 바깥쪽으로 knock(세계 m/s) 넉백.
        /// filter 가 있으면 그 적만. 기혈이 0 아래로 가면 쓰러뜨린다(자리 번호가 바뀌니 맨 끝에 뒤에서부터). 맞은 수를 돌려준다.</summary>
        public int Damage(List<int> indices, float dmg, Vector2 from, float knock, System.Func<EnemyAgent, bool> filter = null)
        {
            _dead.Clear();
            int hit = 0, last = -1;
            indices.Sort();
            foreach (int i in indices)
            {
                if (i == last) continue;
                last = i;
                var a = _sim.Get(i);
                if (a.Health <= 0f) continue;
                if (filter != null && !filter(a)) continue;
                a.Health -= dmg;
                if (knock > 0f)
                {
                    var d = new Vector2(a.Pos.x - from.x, a.Pos.y - from.y);
                    float m = d.magnitude;
                    if (m > 1e-5f) a.Knock += new float2(d.x / m, d.y / m) * knock;
                }
                _sim.Set(i, a);
                hit++;
                if (a.Health <= 0f) _dead.Add(i);
            }
            for (int k = _dead.Count - 1; k >= 0; k--) Kill(_dead[k]);
            if (_dead.Count > 0) _sim.RebuildGrid(Mathf.Max(0.01f, RadiusOf(World.Realm) * 2.2f));
            return hit;
        }

        /// <summary>EnemyAgent.Kind 순번의 적 정의. 표에 없으면 null.</summary>
        public EnemyDef DefOf(int kind) => kind >= 0 && kind < _kinds.Count ? _kinds[kind].Def : null;

        public EnemySim Sim => _sim;
        public int Count => _sim?.Count ?? 0;
        public float LastSimMs => _simMs;

        /// <summary>혈월 천하(회귀)면 적 기혈·속도에 곱할 값을 Life 에서 받는다. 이미 나온 적에는 안 붙는다.</summary>
        public void UseLife(Life life)
        {
            _healthMul = life.EnemyHealthMultiplier;
            _speedMul = life.EnemySpeedMultiplier;
        }

        void Awake()
        {
            var data = DataBoot.Data;
            _moveBase = data.Balance.Stats.MoveBase;
            _playerBody = data.Balance.Stats.PlayerBodyRadius * 2f; // 지름(경지 m)
            foreach (var (id, w) in Mix)
            {
                var def = data.Enemy(id);
                _kinds.Add(new Kind { Def = def, Move = MoveOf(def.Behavior), Tint = TintOf(def.Family), Weight = w });
                _totalWeight += w;
            }
            _sim = new EnemySim(capacity);
            if (player != null)
            {
                if (vitals == null) vitals = player.GetComponent<PlayerVitals>();
                _playerMove = player.GetComponent<PlayerController>();
            }
            if (clusters != null)
            {
                clusters.hopPeriod = EnemySim.HopPeriod;
                if (clusters.atlas == null) clusters.atlas = ClusterRenderer.MakePlaceholderAtlas(); // 적 아틀라스가 오기 전 임시
            }
        }

        void OnDestroy() => _sim?.Dispose();

        public static EnemyMove MoveOf(string behavior)
        {
            switch (behavior)
            {
                case "hop_chase": return EnemyMove.Hop;
                case "flee": return EnemyMove.Flee;
                case "line_charge": return EnemyMove.Line;
                case "telegraph_dash": return EnemyMove.Dash; // EnemyDef.Dash 가 없으면 SpawnAt 이 Chase 로 돌린다
                case "charge_roar": return EnemyMove.Dash;    // 혈사자: 같은 돌진, 쉬기로 들어서면 포효(ApplyAuras)
                case "kite_shoot": return EnemyMove.Keep;     // EnemyDef.Shoot 의 KeepMax 가 0 이면 SpawnAt 이 Chase 로 돌린다
                case "fly_curve": return EnemyMove.Curve;     // EnemyDef.Curve 가 없으면 SpawnAt 이 Chase 로 돌린다
                case "flank_pack": return EnemyMove.Flank;    // EnemyDef.Group 이 없으면 SpawnAt 이 Chase 로 돌린다
                default: return EnemyMove.Chase; // 나머지 갈래는 붙이기 전까지 곧장 쫓는다
            }
        }

        static Color TintOf(string family)
        {
            switch (family)
            {
                case "bloodman": return new Color(0.55f, 0.10f, 0.08f);
                case "bloodshadow": return new Color(0.30f, 0.06f, 0.12f);
                case "event": return new Color(0.85f, 0.65f, 0.15f);
                default: return new Color(0.16f, 0.15f, 0.17f); // 강시 먹빛
            }
        }

        WorldScale World => WorldScale.Current;

        /// <summary>size(경지 m) 사람이 n 명인 덩이의 밀어내기 반경(세계 m).</summary>
        float RadiusOf(int bornRealm)
        {
            var ws = World;
            float member = clusters != null ? clusters.memberRealmMeters : 1f;
            float spacing = clusters != null ? clusters.spacing : 0.9f;
            int n = ws.Scale.ClusterSize(bornRealm);
            float size = ws.Length(member, bornRealm);
            return size * (n <= 1 ? 0.38f : 0.75f * spacing * Mathf.Sqrt(n));
        }

        public int SpawnAt(EnemyDef def, Vector2 pos)
        {
            var ws = World;
            int realm = ws.Realm;
            var move = MoveOf(def.Behavior);
            if (move == EnemyMove.Dash && def.Dash == null) move = EnemyMove.Chase;
            if (move == EnemyMove.Keep && (def.Shoot == null || def.Shoot.KeepMax <= 0f)) move = EnemyMove.Chase;
            if (move == EnemyMove.Curve && (def.Curve == null || def.Curve.Amplitude <= 0f)) move = EnemyMove.Chase;
            if (move == EnemyMove.Flank && (def.Group == null || def.Group.FlankAngle <= 0f)) move = EnemyMove.Chase;
            var to = (Vector2)player.position - pos;
            var a = new EnemyAgent
            {
                Pos = new float2(pos.x, pos.y),
                LineDir = to.sqrMagnitude > 1e-6f ? (float2)(to.normalized) : new float2(1, 0),
                Speed = ws.Speed(def.Speed / 100f * _moveBase, realm) * _speedMul,
                Radius = RadiusOf(realm),
                Weight = 1f,
                Hop = (float)_rng.NextDouble(),
                Health = def.Health * _healthMul,
                MaxHealth = def.Health * _healthMul,
                MaxLife = def.LifeSeconds, // 혈인 14·금두꺼비 25(enemies.json lifeSeconds), 0 이면 끝까지
                ContactDamage = def.ContactDamage, // 시간에 따른 배율(enemyScaling.damagePerMinute)은 C-10 디렉터가 곱한다
                BornRealm = realm,
                Move = (byte)move,
            };
            if (move == EnemyMove.Dash)
            {
                // 혈무사 돌진(enemies.json telegraph·dash, 기획서 6장): 경지 m → 세계 m
                var dash = def.Dash;
                a.DashRange = ws.Length(dash.Range, realm);
                a.DashSpeed = ws.Speed(dash.Speed, realm) * _speedMul;
                a.DashSeconds = dash.Seconds;
                a.RestMin = dash.RestMin;
                a.RestMax = dash.RestMax;
                a.Telegraph = def.Telegraph;
            }
            if (move == EnemyMove.Curve)
            {
                // 비강 곡선 비행(enemies.json curve, 기획서 6장 '옆 1.5m, 1.6초'): 경지 m → 세계 m. 위상은 Hop 의 무작위 값
                a.CurveAmp = ws.Length(def.Curve.Amplitude, realm);
                a.CurvePeriod = def.Curve.Period;
            }
            if (move == EnemyMove.Flank)
            {
                // 설랑 무리(enemies.json group, 기획서 6장 '지역별 고유 적'): 무리 안에서 번갈아 왼쪽·오른쪽으로 비껴 든다
                a.FlankAngle = def.Group.FlankAngle * Mathf.Deg2Rad * (_flankSide ? 1f : -1f);
                _flankSide = !_flankSide;
                a.CommitRange = ws.Length(def.Group.CommitRange, realm);
            }
            if (def.AuraRadius > 0f)
            {
                // 기수 깃발(enemies.json auraRadius·auraBonus, 기획서 6장 '반경 5m 적 이동속도·피해 +20%'): 경지 m → 세계 m
                a.AuraRadius = ws.Length(def.AuraRadius, realm);
                a.AuraBonus = def.AuraBonus;
            }
            if (def.Shoot != null && def.Shoot.Interval > 0f)
            {
                // 쏘는 적(enemies.json shoot, 기획서 6장 표 아래): 경지 m → 세계 m. 첫 발은 나온 뒤 0~Interval 사이
                var sh = def.Shoot;
                a.KeepMin = ws.Length(sh.KeepMin, realm);
                a.KeepMax = ws.Length(sh.KeepMax, realm);
                a.ShootRange = ws.Length(sh.Range, realm);
                a.ShootInterval = sh.Interval;
                a.ShotLeft = sh.Interval * (float)_rng.NextDouble();
            }
            int kindIndex = _kinds.FindIndex(k => k.Def == def);
            if (kindIndex < 0)
            {
                // 생성표 밖 적(디렉터·시험이 직접 넣은 것)도 종류 순번을 갖게 가중치 0 으로 표 끝에 붙인다(DefOf·탄·보상이 읽는다)
                _kinds.Add(new Kind { Def = def, Move = move, Tint = TintOf(def.Family), Weight = 0f });
                kindIndex = _kinds.Count - 1;
            }
            a.Kind = kindIndex;
            int id = _sim.Spawn(a);
            if (id < 0) return -1;
            var tint = kindIndex >= 0 ? _kinds[kindIndex].Tint : TintOf(def.Family);
            _views[id] = new ClusterView { BornRealm = realm, Seed = id, Tint = tint, Position = pos, HopDuty = EnemySim.HopDuty, HopBeat = move == EnemyMove.Hop ? a.Hop : -2f };
            if (def.Twin != null && !_spawningTwin)
            {
                // 흑백 쌍강시(enemies.json twin, 기획서 6장): 플레이어 쪽 방향에 직각으로 Gap m 옆에 짝을 하나 더 세운다
                var side = to.sqrMagnitude > 1e-6f ? new Vector2(-to.y, to.x).normalized : Vector2.up;
                _spawningTwin = true;
                int pid = SpawnAt(def, pos + side * ws.Length(def.Twin.Gap, realm));
                _spawningTwin = false;
                if (pid >= 0) { _twinOf[id] = pid; _twinOf[pid] = id; }
            }
            if (move == EnemyMove.Flank && def.Group.Size > 1 && !_spawningGroup)
            {
                // 설랑은 Size 마리가 한 번에 나온다. 첫 마리 둘레 1m 안에 흩어 세우고, 왼쪽·오른쪽은 _flankSide 가 번갈아 준다
                _spawningGroup = true;
                float spread = ws.Length(1f, realm);
                for (int i = 1; i < def.Group.Size; i++)
                {
                    float ang = (float)_rng.NextDouble() * Mathf.PI * 2f, r = Mathf.Sqrt((float)_rng.NextDouble()) * spread;
                    var off = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                    SpawnAt(def, pos + off);
                }
                _spawningGroup = false;
            }
            if (def.Behavior == "snake_line" && def.Group != null && def.Group.Size > 1 && !_spawningGroup)
            {
                // 금선지네(enemies.json group, 기획서 6장): 머리는 곧장 쫓고, 뒤로 Spacing m 간격 마디 Size-1 개가 앞 마디를 따라간다
                _spawningGroup = true;
                float gap = ws.Length(def.Group.Spacing, realm);
                var back = to.sqrMagnitude > 1e-6f ? -to.normalized : Vector2.left;
                int leader = id;
                for (int i = 1; i < def.Group.Size; i++)
                {
                    int sid = SpawnAt(def, pos + back * gap * i);
                    if (sid < 0) break;
                    int si = _sim.Count - 1; // Spawn 은 맨 뒤에 넣는다
                    var s = _sim.Get(si);
                    s.Move = (byte)EnemyMove.Follow;
                    s.FollowGap = gap;
                    s.FollowTarget = _sim.Get(si - 1).Pos;
                    _sim.Set(si, s);
                    _follow[sid] = new FollowLink { Leader = leader, Head = id };
                    leader = sid;
                }
                _spawningGroup = false;
            }
            if (def.Behavior == "spear_line" && def.Group != null && def.Group.Size > 1 && !_spawningGroup)
            {
                // 마교도 창진(enemies.json group, 기획서 6장): 머리를 가운데 두고 플레이어 쪽에 수직으로 Spacing m 간격 한 줄.
                // 나머지는 머리 옆 +1·-1·+2·-2… 칸 자리를 지키며 줄째 전진한다(줄은 늘 플레이어를 마주 보게 돈다)
                _spawningGroup = true;
                float gap = ws.Length(def.Group.Spacing, realm);
                var side = to.sqrMagnitude > 1e-6f ? new Vector2(-to.y, to.x).normalized : Vector2.up;
                for (int i = 1; i < def.Group.Size; i++)
                {
                    float off = ((i + 1) / 2) * (i % 2 == 1 ? 1f : -1f) * gap;
                    int sid = SpawnAt(def, pos + side * off);
                    if (sid < 0) break;
                    int si = _sim.Count - 1;
                    var s = _sim.Get(si);
                    s.Move = (byte)EnemyMove.Follow;
                    s.FollowGap = gap * 0.1f;
                    s.FollowTarget = s.Pos;
                    _sim.Set(si, s);
                    _follow[sid] = new FollowLink { Leader = id, Head = id, Offset = off };
                }
                _spawningGroup = false;
            }
            return id;
        }

        // ── 금선지네 마디(snake_line)·마교도 창진(spear_line) ─────────
        // 마디마다 바로 앞 마디(Leader)와 머리(Head)를 기억한다. 앞 마디가 쓰러지면 그 앞 마디를 따라가 줄을 잇고,
        // 머리가 쓰러지면 남은 마디가 저마다 흩어져 곧장 쫓는다(Chase).
        // 창진은 Offset(세계 m)이 0 이 아니어서 머리 자리에서 플레이어 쪽에 수직으로 Offset 만큼 옆 자리를 지킨다.
        struct FollowLink { public int Leader, Head; public float Offset; }
        readonly Dictionary<int, FollowLink> _follow = new Dictionary<int, FollowLink>();
        readonly Dictionary<int, int> _indexOf = new Dictionary<int, int>();
        readonly List<int> _followDone = new List<int>();
        public int SnakeScattered { get; private set; }

        void StepFollow(float2 player)
        {
            if (_follow.Count == 0) return;
            _indexOf.Clear();
            var agents = _sim.Agents;
            for (int i = 0; i < _sim.Count; i++) _indexOf[agents[i].Id] = i;
            _followDone.Clear();
            var keys = new List<int>(_follow.Keys);
            foreach (int fid in keys)
            {
                if (!_indexOf.TryGetValue(fid, out int fi)) continue; // 쓰러진 마디: 뒤 마디가 지나갈 길로 남겨 둔다
                var link = _follow[fid];
                var e = agents[fi];
                if (!_indexOf.ContainsKey(link.Head))
                {
                    e.Move = (byte)EnemyMove.Chase;
                    agents[fi] = e;
                    _followDone.Add(fid);
                    SnakeScattered++;
                    continue;
                }
                int leader = link.Leader;
                while (!_indexOf.ContainsKey(leader) && leader != link.Head && _follow.TryGetValue(leader, out var up)) leader = up.Leader;
                if (!_indexOf.TryGetValue(leader, out int li)) continue;
                if (leader != link.Leader) { link.Leader = leader; _follow[fid] = link; }
                e.FollowTarget = agents[li].Pos;
                if (link.Offset != 0f)
                {
                    float2 toP = player - agents[li].Pos;
                    float2 sd = math.lengthsq(toP) > 1e-6f ? math.normalize(new float2(-toP.y, toP.x)) : new float2(0f, 1f);
                    e.FollowTarget += sd * link.Offset;
                }
                agents[fi] = e;
            }
            foreach (int fid in _followDone) _follow.Remove(fid);
            // 산 마디가 하나도 안 가리키는 쓰러진 마디는 지운다
            if (_follow.Count > 0)
            {
                _followDone.Clear();
                foreach (var kv in _follow)
                {
                    if (_indexOf.ContainsKey(kv.Key)) continue;
                    bool referenced = false;
                    foreach (var kv2 in _follow) if (kv2.Value.Leader == kv.Key && _indexOf.ContainsKey(kv2.Key)) { referenced = true; break; }
                    if (!referenced) _followDone.Add(kv.Key);
                }
                foreach (int fid in _followDone) _follow.Remove(fid);
            }
        }

        bool _flankSide = true;   // 설랑 다음 마리가 비껴 드는 쪽
        bool _spawningGroup;      // 무리 나머지를 세우는 중(그 안에서 다시 무리를 부르지 않게)

        // ── 흑백 쌍강시 되살림(twin_revive) ─────────────────────────
        // 한쪽이 쓰러지면 그 자리에 눕고(Fallen), 짝이 ReviveSeconds 버티면 최대 기혈 × ReviveHealth 로 일어난다.
        // 둘 다 누우면 끝. 되살아난 몸은 NoDrop 이라 구슬을 다시 떨구지 않는다. 정예 한 쌍 보갑 하나는 보갑이 붙을 때(C-09).
        struct Fallen { public Vector2 Pos; public EnemyDef Def; public int Realm; public int Partner; public float Left; }
        readonly Dictionary<int, int> _twinOf = new Dictionary<int, int>();
        readonly List<Fallen> _fallen = new List<Fallen>();
        readonly List<SpriteRenderer> _fallenViews = new List<SpriteRenderer>();
        bool _spawningTwin;
        static readonly Color FallenColor = new Color(0.25f, 0.25f, 0.25f, 0.55f);

        /// <summary>시험용: 지금 누워 있는 쌍강시 수와 지금까지 되살아난 수.</summary>
        public int FallenCount => _fallen.Count;
        public int RevivedCount { get; private set; }

        int IndexOfId(int id)
        {
            var agents = _sim.Agents;
            for (int i = 0; i < _sim.Count; i++) if (agents[i].Id == id) return i;
            return -1;
        }

        void OnTwinKilled(EnemyAgent a, EnemyDef def)
        {
            if (!_twinOf.TryGetValue(a.Id, out int partner)) return;
            _twinOf.Remove(a.Id);
            int lying = _fallen.FindIndex(f => f.Partner == a.Id);
            if (lying >= 0) { _fallen.RemoveAt(lying); _twinOf.Remove(partner); return; } // 짝이 이미 누워 있다: 둘 다 누우면 끝
            if (IndexOfId(partner) < 0) return;
            _fallen.Add(new Fallen { Pos = new Vector2(a.Pos.x, a.Pos.y), Def = def, Realm = a.BornRealm, Partner = partner, Left = def.ReviveSeconds });
        }

        void StepTwins(float dt)
        {
            for (int i = _fallen.Count - 1; i >= 0; i--)
            {
                var f = _fallen[i];
                if (IndexOfId(f.Partner) < 0) { _fallen.RemoveAt(i); continue; } // 짝이 다른 길로 사라짐(비우기 등)
                f.Left -= dt;
                if (f.Left > 0f) { _fallen[i] = f; continue; }
                _fallen.RemoveAt(i);
                _spawningTwin = true;
                int nid = SpawnAt(f.Def, f.Pos);
                _spawningTwin = false;
                if (nid < 0) continue;
                int ni = _sim.Count - 1; // Spawn 은 맨 뒤에 넣는다
                var na = _sim.Get(ni);
                na.Health = na.MaxHealth * f.Def.Twin.ReviveHealth;
                na.NoDrop = 1;
                _sim.Set(ni, na);
                _twinOf[nid] = f.Partner;
                _twinOf[f.Partner] = nid;
                RevivedCount++;
            }
            // 누운 몸: 잿빛 원, 일어나기 직전 1초에 깜빡인다(그림 오기 전 임시)
            while (_fallenViews.Count < _fallen.Count)
            {
                var go = new GameObject("누운 쌍강시");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 1;
                _fallenViews.Add(sr);
            }
            for (int i = 0; i < _fallenViews.Count; i++)
            {
                var sr = _fallenViews[i];
                bool on = i < _fallen.Count && _dot != null;
                if (sr.enabled != on) sr.enabled = on;
                if (!on) continue;
                var f = _fallen[i];
                sr.sprite = _dot;
                sr.transform.position = new Vector3(f.Pos.x, f.Pos.y, 0f);
                sr.transform.localScale = Vector3.one * (RadiusOf(f.Realm) * 2f);
                var c = FallenColor;
                if (f.Left < 1f) c.a *= 0.4f + 0.6f * Mathf.PingPong(Time.time * 6f, 1f);
                sr.color = c;
            }
        }

        EnemyDef PickDef()
        {
            double r = _rng.NextDouble() * _totalWeight;
            foreach (var k in _kinds) { r -= k.Weight; if (r <= 0) return k.Def; }
            return _kinds[0].Def;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.rightBracketKey.wasPressedThisFrame) targetCount = Mathf.Min(capacity, targetCount * 2);
                if (kb.leftBracketKey.wasPressedThisFrame) targetCount = Mathf.Max(1, targetCount / 2);
                if (kb.cKey.wasPressedThisFrame) _sim.Clear();
                if (kb.kKey.wasPressedThisFrame && player != null && World != null)
                    KillInside(player.position, World.ViewDiagonal * 0.15f); // 시험용 일격(무공이 오기 전)
            }
            if (player == null || World == null) return;

            var ws = World;
            var st = DataBoot.Data.Balance.SpawnTarget;
            float diag = ws.ViewDiagonal;
            Vector2 p = player.position;
            float dt = Time.deltaTime;
            if (dt > 0f)
            {
                var v = (p - _lastPlayer) / dt;
                _heading = Vector2.Lerp(_heading, v, 1f - Mathf.Exp(-dt * 4f));
            }
            _lastPlayer = p;

            // 모자라면 생성 고리(대각선 0.6~0.75배)에 채운다. 넘치면 뒤에서부터 걷는다.
            int spawn = Mathf.Min(spawnPerFrame, targetCount - _sim.Count);
            for (int i = 0; i < spawn; i++)
            {
                float ang = (float)(_rng.NextDouble() * Mathf.PI * 2);
                float rad = diag * Mathf.Lerp(st.RingMinDiagonal, st.RingMaxDiagonal, (float)_rng.NextDouble());
                SpawnAt(PickDef(), p + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad);
            }
            while (_sim.Count > targetCount) _sim.RemoveAt(_sim.Count - 1);

            _watch.Restart();
            ApplyAuras(dt);
            StepFollow(new float2(p.x, p.y));
            float cell = Mathf.Max(0.01f, RadiusOf(ws.Realm) * 2.2f);
            _sim.Step(dt, new float2(p.x, p.y), new float2(_heading.x, _heading.y), cell,
                      diag * st.RecycleBeyondDiagonal, diag * st.RecycleToDiagonal,
                      1f, DataBoot.Data.Balance.EnemyStatus.KnockbackKeepPerSecond);
            // 접촉 피해(C-03): 플레이어 원에 닿은 적, 같은 적은 contact.sameEnemyIntervalSeconds 마다 한 번
            if (vitals != null)
            {
                float pr = ws.Length(_playerBody) * 0.5f;
                var scale = ws.Scale;
                _sim.Contacts(new float2(p.x, p.y), pr, DataBoot.Data.Balance.Contact.SameEnemyIntervalSeconds, ws.Realm,
                              (born, pr2) => scale.DealsContactDamage(born, pr2), _hits);
                var agents = _sim.Agents;
                foreach (var h in _hits)
                {
                    if (!HitPlayer(h.Damage)) continue;
                    // 빙강시: 닿으면 걸음 둔화(enemies.json onHitSlowSeconds 2·onHitSlowMultiplier 0.65)
                    int kind = h.Index >= 0 && h.Index < _sim.Count ? agents[h.Index].Kind : -1;
                    if (kind >= 0 && kind < _kinds.Count && _kinds[kind].Def.OnHitSlowSeconds > 0f && _playerMove != null)
                        _playerMove.Slow(_kinds[kind].Def.OnHitSlowSeconds, _kinds[kind].Def.OnHitSlowMultiplier);
                }
            }
            FireShots();
            StepShots(dt, p);
            ResolveDeaths(p);
            StepMarks(dt, p);
            StepPools(dt, p);
            StepTwins(dt);
            StepHooks(dt, p);
            _watch.Stop();
            _simMs = Mathf.Lerp(_simMs, (float)_watch.Elapsed.TotalMilliseconds, 0.1f);
            _fps = Mathf.Lerp(_fps, dt > 0 ? 1f / Time.unscaledDeltaTime : 0f, 0.05f);

            Sync();
        }

        // ── 적의 탄(혈궁수 화살·매화검귀 꽃잎·설인 얼음) ─────────────────
        //   EnemySim 이 Fire 를 세운 적마다 EnemyDef.Shoot 대로 플레이어 쪽으로 Count 발(Spread 도 사이에 고르게).
        //   탄은 초속 Speed, Range 날아가면 사라지고, 플레이어 몸 원에 닿으면 ProjectileDamage(무적이면 그냥 지나간다). 탄은 세계 m.
        //   그림은 이펙트가 오기 전 임시 점(종류 색).
        struct Shot { public Vector2 Pos, Vel; public float Left, Radius, Damage; public Color Color; }
        readonly List<Shot> _shots = new List<Shot>();
        readonly List<SpriteRenderer> _shotViews = new List<SpriteRenderer>();
        Sprite _dot;

        /// <summary>시험용: 날아가는 적 탄 수.</summary>
        public int ShotCount => _shots.Count;

        void FireShots()
        {
            var agents = _sim.Agents;
            var ws = World;
            for (int i = 0; i < _sim.Count; i++)
            {
                var a = agents[i];
                if (a.Fire == 0) continue;
                a.Fire = 0;
                agents[i] = a;
                if (a.Kind < 0 || a.Kind >= _kinds.Count) continue;
                var def = _kinds[a.Kind].Def;
                var sh = def.Shoot;
                if (sh == null) continue;
                var from = new Vector2(a.Pos.x, a.Pos.y);
                var to = (Vector2)player.position - from;
                float baseAng = Mathf.Atan2(to.y, to.x);
                int n = Mathf.Max(1, sh.Count);
                float spread = sh.Spread * Mathf.Deg2Rad;
                float speed = ws.Speed(sh.Speed, a.BornRealm);
                for (int k = 0; k < n; k++)
                {
                    float ang = n == 1 ? baseAng : baseAng - spread * 0.5f + spread * k / (n - 1);
                    if (_shots.Count >= DataBoot.Data.Balance.Limits.Projectiles) return;
                    _shots.Add(new Shot
                    {
                        Pos = from, Vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * speed,
                        Left = a.ShootRange, Radius = ws.Length(sh.Radius, a.BornRealm), Damage = def.ProjectileDamage * (1f + a.Buff),
                        Color = _kinds[a.Kind].Tint,
                    });
                }
            }
        }

        void StepShots(float dt, Vector2 p)
        {
            float pr = World.Length(_playerBody) * 0.5f;
            for (int i = _shots.Count - 1; i >= 0; i--)
            {
                var s = _shots[i];
                var step = s.Vel * dt;
                s.Pos += step;
                s.Left -= step.magnitude;
                bool gone = s.Left <= 0f;
                if (!gone && vitals != null && (s.Pos - p).sqrMagnitude <= (pr + s.Radius) * (pr + s.Radius) && !vitals.Invulnerable)
                {
                    HitPlayer(s.Damage);
                    gone = true;
                }
                if (gone) _shots.RemoveAt(i);
                else _shots[i] = s;
            }
            DrawShots();
        }

        void DrawShots()
        {
            if (_dot == null)
            {
                const int N = 32;
                var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { name = "적 탄 점" };
                var px = new Color32[N * N];
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                        byte al = (byte)(Mathf.Clamp01((1f - Mathf.Sqrt(dx * dx + dy * dy)) * 6f) * 255);
                        px[y * N + x] = new Color32(255, 255, 255, al);
                    }
                tex.SetPixels32(px);
                tex.Apply();
                _dot = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
            }
            while (_shotViews.Count < _shots.Count)
            {
                var go = new GameObject("적 탄");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _dot;
                sr.sortingOrder = 30;
                _shotViews.Add(sr);
            }
            for (int i = 0; i < _shotViews.Count; i++)
            {
                var sr = _shotViews[i];
                bool on = i < _shots.Count;
                if (sr.enabled != on) sr.enabled = on;
                if (!on) continue;
                var s = _shots[i];
                sr.transform.position = new Vector3(s.Pos.x, s.Pos.y, 0f);
                sr.transform.localScale = Vector3.one * (s.Radius * 2f);
                sr.color = Color.Lerp(s.Color, Color.black, 0.3f);
            }
        }

        // ── 피 웅덩이(혈술사 blood_pool) ─────────────────
        //   EnemyDef.Pool 이 있는 적은 Interval 초마다 그때 플레이어 발밑에 반경 Radius(경지 m) 웅덩이를 깔고 Seconds 초 남긴다.
        //   한 적이 깐 것은 Max 개까지(넘으면 오래된 것부터). 플레이어가 하나라도 밟고 있으면 1초마다 ZoneDamagePerSecond 를 한 번
        //   (겹쳐 밟아도 한 번, 무적이면 Hit 이 걸러 냄). 화면 밖(대각선 절반 너머) 혈술사는 깔지 않는다. 깐 적이 쓰러져도 웅덩이는 남는다.
        //   첫 웅덩이는 나온 뒤 0~Interval 사이. 그림은 이펙트가 오기 전 임시 핏빛 원.
        struct Pool { public Vector2 Pos; public float Radius, Left, Damage; public int Owner; public Color Color; }
        readonly List<Pool> _pools = new List<Pool>();
        readonly List<SpriteRenderer> _poolViews = new List<SpriteRenderer>();
        readonly Dictionary<int, float> _poolTimers = new Dictionary<int, float>();
        float _poolTick;
        static readonly Color PoolColor = new Color(0.55f, 0.04f, 0.04f, 0.45f);

        /// <summary>시험용: 깔려 있는 피 웅덩이 수.</summary>
        public int PoolCount => _pools.Count;

        void StepPools(float dt, Vector2 p)
        {
            var ws = World;
            var agents = _sim.Agents;
            float reach = ws.ViewDiagonal * 0.5f;
            for (int i = 0; i < _sim.Count; i++)
            {
                var a = agents[i];
                if (a.Kind < 0 || a.Kind >= _kinds.Count) continue;
                var def = _kinds[a.Kind].Def;
                var pool = def.Pool;
                if (pool == null || pool.Interval <= 0f) continue;
                if (!_poolTimers.TryGetValue(a.Id, out float t)) t = pool.Interval * (float)_rng.NextDouble();
                t -= dt;
                if (t <= 0f)
                {
                    if ((new Vector2(a.Pos.x, a.Pos.y) - p).sqrMagnitude <= reach * reach)
                    {
                        int mine = 0, oldest = -1;
                        for (int k = 0; k < _pools.Count; k++)
                            if (_pools[k].Owner == a.Id) { mine++; if (oldest < 0) oldest = k; }
                        if (pool.Max > 0 && mine >= pool.Max && oldest >= 0) _pools.RemoveAt(oldest);
                        _pools.Add(new Pool
                        {
                            Pos = p, Radius = ws.Length(pool.Radius, a.BornRealm), Left = pool.Seconds,
                            Damage = def.ZoneDamagePerSecond * (1f + a.Buff), Owner = a.Id, Color = PoolColor,
                        });
                    }
                    t += pool.Interval;
                }
                _poolTimers[a.Id] = t;
            }

            float pr = ws.Length(_playerBody) * 0.5f;
            float hurt = 0f;
            for (int k = _pools.Count - 1; k >= 0; k--)
            {
                var q = _pools[k];
                q.Left -= dt;
                if (q.Left <= 0f) { _pools.RemoveAt(k); continue; }
                _pools[k] = q;
                float r = q.Radius + pr * 0.5f; // 몸 가운데가 웅덩이 가장자리를 넘어서면 밟은 것
                if ((q.Pos - p).sqrMagnitude <= r * r) hurt = Mathf.Max(hurt, q.Damage);
            }
            hurt = Mathf.Max(hurt, StepZones(p, pr));
            if (hurt > 0f)
            {
                _poolTick -= dt;
                if (_poolTick <= 0f)
                {
                    HitPlayer(hurt);
                    _poolTick = 1f;
                }
            }
            else _poolTick = 0f; // 밟자마자 첫 피해
            DrawPools();
        }

        // ── 표식 뒤 순간이동(혈영자객 mark_teleport) ─────────────────
        //   EnemyDef.Mark 가 있는 적은 처음 나타나면 바로, 그 뒤 Interval 초마다 그때 플레이어 발밑에 반경 Radius(경지 m) 붉은 표식을 찍고
        //   Telegraph 초 뒤 그 자리로 옮긴다. 그 순간 플레이어가 표식 안이면 접촉 피해 한 번(기수 깃발 배율, 무적이면 Hit 이 거름).
        //   걸음(speed 0)은 시뮬이 그대로 돌린다. 표식 그림은 웅덩이 줄에 피해 0 으로 얹는다(이펙트 오기 전 임시).
        struct MarkState { public float Next, Left; public Vector2 At; }
        readonly Dictionary<int, MarkState> _marks = new Dictionary<int, MarkState>();
        static readonly Color MarkColor = new Color(0.85f, 0.08f, 0.06f, 0.35f);

        /// <summary>시험용: 지금까지 순간이동한 수.</summary>
        public int TeleportCount { get; private set; }

        void StepMarks(float dt, Vector2 p)
        {
            var ws = World;
            var agents = _sim.Agents;
            float pr = ws.Length(_playerBody) * 0.5f;
            for (int i = 0; i < _sim.Count; i++)
            {
                var a = agents[i];
                var def = DefOf(a.Kind);
                var mk = def?.Mark;
                if (mk == null || mk.Interval <= 0f) continue;
                _marks.TryGetValue(a.Id, out var s); // 처음이면 Next 0 → 바로 첫 표식
                if (s.Left > 0f)
                {
                    s.Left -= dt;
                    if (s.Left <= 0f)
                    {
                        a.Pos = new float2(s.At.x, s.At.y);
                        a.Knock = float2.zero;
                        _sim.Set(i, a);
                        TeleportCount++;
                        float r = ws.Length(mk.Radius, a.BornRealm) + pr;
                        if (vitals != null && (p - s.At).sqrMagnitude <= r * r) HitPlayer(def.ContactDamage * (1f + a.Buff));
                    }
                }
                else
                {
                    s.Next -= dt;
                    if (s.Next <= 0f)
                    {
                        float tele = Mathf.Max(0.05f, def.Telegraph);
                        s.At = p;
                        s.Left = tele;
                        s.Next = Mathf.Max(0f, mk.Interval - tele); // 표식부터 다음 표식까지 Interval(예고 동안은 Next 가 줄지 않는다)
                        _pools.Add(new Pool { Pos = p, Radius = ws.Length(mk.Radius, a.BornRealm), Left = tele, Damage = 0f, Owner = -2, Color = MarkColor });
                    }
                }
                _marks[a.Id] = s;
            }
        }

        // ── 갈고리 묶기(수적 hook_bind) ─────────────────────────────
        //   EnemyDef.Hook 이 있는 적은 플레이어가 Range(경지 m) 안이고 쉬는 시간이 끝났으면 그 순간 플레이어 쪽으로 겨눠
        //   Telegraph 초 동안 서서 예고선을 긋고(겨눈 방향은 굳힘 — 비켜서면 피한다), 초속 Speed 갈고리를 Range 까지 던진다.
        //   플레이어 몸에 닿으면 BindSeconds 동안 걷기·경공을 못 한다(PlayerController.Bind, 무적이면 지나감). 피해는 없다.
        //   던진 뒤 다음까지 Interval 초. 처음 나온 적은 0~Interval 사이 아무 때나. 그림은 임시 선(이펙트 오기 전).
        struct HookState { public float Cool, Tele; public Vector2 Dir; public bool Seen; }
        struct HookShot { public int Owner; public Vector2 From, Pos, Dir; public float Speed, Left, Bind, Radius; }
        readonly Dictionary<int, HookState> _hookStates = new Dictionary<int, HookState>();
        readonly List<HookShot> _hookShots = new List<HookShot>();
        readonly List<LineRenderer> _hookLines = new List<LineRenderer>();
        Material _lineMat;
        static readonly Color HookAimColor = new Color(0.85f, 0.10f, 0.06f, 0.45f);
        static readonly Color HookRopeColor = new Color(0.12f, 0.10f, 0.08f, 0.9f);

        /// <summary>시험용: 지금까지 갈고리에 묶인 수.</summary>
        public int HookBindCount { get; private set; }

        void StepHooks(float dt, Vector2 p)
        {
            var ws = World;
            var agents = _sim.Agents;
            float pr = ws.Length(_playerBody) * 0.5f;
            int line = 0;
            for (int i = 0; i < _sim.Count; i++)
            {
                var a = agents[i];
                var def = DefOf(a.Kind);
                var hk = def?.Hook;
                if (hk == null || hk.Interval <= 0f) continue;
                _hookStates.TryGetValue(a.Id, out var s);
                if (!s.Seen) { s.Seen = true; s.Cool = hk.Interval * (float)_rng.NextDouble(); }
                var at = new Vector2(a.Pos.x, a.Pos.y);
                float range = ws.Length(hk.Range, a.BornRealm);
                if (s.Tele > 0f)
                {
                    s.Tele -= dt;
                    if (a.Stun < dt * 2f) { a.Stun = dt * 2f; _sim.Set(i, a); } // 예고 동안 선다
                    if (s.Tele <= 0f)
                    {
                        _hookShots.Add(new HookShot
                        {
                            Owner = a.Id, From = at, Pos = at, Dir = s.Dir, Speed = ws.Speed(hk.Speed, a.BornRealm),
                            Left = range, Bind = hk.BindSeconds, Radius = ws.Length(hk.Radius > 0f ? hk.Radius : 0.25f, a.BornRealm),
                        });
                        s.Cool = hk.Interval;
                    }
                    else DrawHookLine(line++, at, at + s.Dir * range, HookAimColor, ws.Length(0.06f));
                }
                else
                {
                    s.Cool -= dt;
                    if (s.Cool <= 0f && (p - at).sqrMagnitude <= range * range)
                    {
                        var d = p - at;
                        s.Dir = d.sqrMagnitude > 1e-6f ? d.normalized : Vector2.right;
                        s.Tele = Mathf.Max(0.05f, def.Telegraph);
                    }
                }
                _hookStates[a.Id] = s;
            }
            for (int k = _hookShots.Count - 1; k >= 0; k--)
            {
                var h = _hookShots[k];
                float step = h.Speed * dt;
                h.Pos += h.Dir * step;
                h.Left -= step;
                bool gone = h.Left <= 0f;
                float rr = h.Radius + pr;
                if (!gone && (h.Pos - p).sqrMagnitude <= rr * rr && (_playerMove == null || !_playerMove.Invulnerable))
                {
                    _playerMove?.Bind(h.Bind);
                    HookBindCount++;
                    gone = true;
                }
                if (gone) { _hookShots.RemoveAt(k); continue; }
                _hookShots[k] = h;
                DrawHookLine(line++, h.From, h.Pos, HookRopeColor, ws.Length(0.05f));
            }
            for (int k = line; k < _hookLines.Count; k++) if (_hookLines[k].enabled) _hookLines[k].enabled = false;
        }

        void DrawHookLine(int index, Vector2 from, Vector2 to, Color color, float width)
        {
            if (_lineMat == null) _lineMat = new Material(Shader.Find("Sprites/Default"));
            while (_hookLines.Count <= index)
            {
                var go = new GameObject("갈고리 선");
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.sharedMaterial = _lineMat;
                lr.positionCount = 2;
                lr.useWorldSpace = true;
                lr.sortingOrder = 3;
                _hookLines.Add(lr);
            }
            var l = _hookLines[index];
            l.enabled = true;
            l.SetPosition(0, new Vector3(from.x, from.y, 0f));
            l.SetPosition(1, new Vector3(to.x, to.y, 0f));
            l.startColor = l.endColor = color;
            l.startWidth = l.endWidth = width;
        }

        // ── 쓰러질 때 남기는 것(EnemyDef.Death, 목인나한 splinter_burst·독두꺼비 poison_pool) ─────────────────
        //   Kill 은 Damage 고리 안에서 불리니 바로 터뜨리지 않고 모아 두었다가 프레임마다 여기서 푼다(자리 번호가 꼬이지 않게).
        //   splinter_burst: 반경 Radius(경지 m) 안 다른 적에게 EnemyDamage, 플레이어가 안이면 PlayerDamage 한 번.
        //     나뭇조각에 쓰러진 목인나한이 또 터지면 다음 프레임에 이어서 터진다.
        //   poison_pool: 그 자리에 반경 Radius 독 장판 Seconds 초, 밟으면 초당 PerSecond(피 웅덩이와 같은 줄: 겹쳐도 1초에 한 번, 큰 값).
        readonly List<(Vector2 pos, EnemyDef def, int born)> _deaths = new List<(Vector2, EnemyDef, int)>();
        readonly List<(Vector2 pos, EnemyDef def, int born)> _deathsNow = new List<(Vector2, EnemyDef, int)>();
        readonly List<int> _splinterHits = new List<int>();
        static readonly Color PoisonColor = new Color(0.30f, 0.55f, 0.12f, 0.45f);
        static readonly Color SplinterColor = new Color(0.45f, 0.30f, 0.15f, 0.6f);

        /// <summary>시험용: 지금까지 터진 나뭇조각 수.</summary>
        public int SplinterCount { get; private set; }

        void ResolveDeaths(Vector2 p)
        {
            if (_deaths.Count == 0) return;
            _deathsNow.Clear();
            _deathsNow.AddRange(_deaths);
            _deaths.Clear();
            var ws = World;
            float pr = ws.Length(_playerBody) * 0.5f;
            foreach (var (pos, def, born) in _deathsNow)
            {
                var d = def.Death;
                float r = ws.Length(d.Radius, born);
                if (def.OnDeath == "splinter_burst")
                {
                    SplinterCount++;
                    _sim.QueryCircle(new float2(pos.x, pos.y), r, _splinterHits);
                    _splinterHits.RemoveAll(i => i >= _sim.Count); // 격자에 남은 걷어 낸 자리
                    if (d.EnemyDamage > 0f && _splinterHits.Count > 0) Damage(_splinterHits, d.EnemyDamage, pos, 0f);
                    if (d.PlayerDamage > 0f && vitals != null && (pos - p).sqrMagnitude <= (r + pr) * (r + pr)) HitPlayer(d.PlayerDamage);
                    // 터진 자리를 0.3초 비춘다(이펙트 오기 전 임시)
                    _pools.Add(new Pool { Pos = pos, Radius = r, Left = 0.3f, Damage = 0f, Owner = -1, Color = SplinterColor });
                }
                else if (def.OnDeath == "poison_pool" && d.Seconds > 0f)
                {
                    _pools.Add(new Pool { Pos = pos, Radius = r, Left = d.Seconds, Damage = d.PerSecond, Owner = -1, Color = PoisonColor });
                }
            }
        }

        void DrawPools()
        {
            while (_poolViews.Count < _pools.Count)
            {
                var go = new GameObject("피 웅덩이");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _dot;
                sr.sortingOrder = 2;
                _poolViews.Add(sr);
            }
            for (int i = 0; i < _poolViews.Count; i++)
            {
                var sr = _poolViews[i];
                bool on = i < _pools.Count && _dot != null;
                if (sr.enabled != on) sr.enabled = on;
                if (!on) continue;
                var q = _pools[i];
                sr.sprite = _dot;
                sr.transform.position = new Vector3(q.Pos.x, q.Pos.y, 0f);
                sr.transform.localScale = Vector3.one * (q.Radius * 2f);
                var c = q.Color;
                c.a *= Mathf.Clamp01(q.Left / 0.5f); // 마르기 직전 0.5초에 옅어짐
                sr.color = c;
            }
        }

        // 독인 poison_aura(enemies.json zoneRadius·zoneDamagePerSecond, 기획서 6장 '제 몸 둘레 반경 1.5m 독안개, 초당 4'):
        // 웅덩이와 같은 줄에서 큰 값 하나를 1초에 한 번. 안개 그림은 몸을 따라다니는 옅은 초록 원(임시).
        readonly List<SpriteRenderer> _zoneViews = new List<SpriteRenderer>();

        float StepZones(Vector2 p, float pr)
        {
            var ws = World;
            var agents = _sim.Agents;
            float hurt = 0f;
            int shown = 0;
            for (int i = 0; i < _sim.Count; i++)
            {
                var a = agents[i];
                var def = DefOf(a.Kind);
                if (def == null || def.ZoneRadius <= 0f || def.ZoneDamagePerSecond <= 0f) continue;
                var at = new Vector2(a.Pos.x, a.Pos.y);
                float zr = ws.Length(def.ZoneRadius, a.BornRealm);
                float r = zr + pr * 0.5f;
                if ((at - p).sqrMagnitude <= r * r) hurt = Mathf.Max(hurt, def.ZoneDamagePerSecond * (1f + a.Buff));
                if (_dot == null) continue;
                if (shown == _zoneViews.Count)
                {
                    var go = new GameObject("독안개");
                    go.transform.SetParent(transform, false);
                    var sr0 = go.AddComponent<SpriteRenderer>();
                    sr0.sortingOrder = 1;
                    _zoneViews.Add(sr0);
                }
                var sr = _zoneViews[shown++];
                sr.enabled = true;
                sr.sprite = _dot;
                sr.transform.position = new Vector3(at.x, at.y, 0f);
                sr.transform.localScale = Vector3.one * (zr * 2f);
                sr.color = new Color(PoisonColor.r, PoisonColor.g, PoisonColor.b, 0.22f);
            }
            for (int k = shown; k < _zoneViews.Count; k++) if (_zoneViews[k].enabled) _zoneViews[k].enabled = false;
            return hurt;
        }

        readonly List<int> _auraOwners = new List<int>();
        readonly List<int> _auraNear = new List<int>();

        // 혈사자 charge_roar(enemies.json roarSeconds, 기획서 6장): 돌진이 끝나 쉬기로 들어서는 순간 포효,
        // RoarSeconds 동안만 깃발처럼 AuraRadius 안 적에 AuraBonus. RoarSeconds 0 인 기수는 늘 켜져 있다.
        readonly Dictionary<int, (byte phase, float roar)> _roars = new Dictionary<int, (byte, float)>();

        /// <summary>시험용: 지금까지 포효한 수.</summary>
        public int RoarCount { get; private set; }

        bool AuraOn(EnemyAgent a, float dt)
        {
            var def = DefOf(a.Kind);
            if (def == null || def.RoarSeconds <= 0f) return true;
            _roars.TryGetValue(a.Id, out var r);
            if (r.phase == (byte)DashPhase.Dashing && a.Phase == (byte)DashPhase.Rest) { r.roar = def.RoarSeconds; RoarCount++; }
            else r.roar = Mathf.Max(0f, r.roar - dt);
            r.phase = a.Phase;
            _roars[a.Id] = r;
            return r.roar > 0f;
        }

        /// <summary>buff_aura(기수): 깃발 둘레 AuraRadius 안 다른 적의 Buff 를 AuraBonus 로(여럿이 겹쳐도 더하지 않고 큰 값 하나).
        /// 기수 자신은 받지 않는다. 격자는 지난 Step 끝 위치라 이번 프레임에 막 나온 적은 다음 프레임부터 받는다.</summary>
        void ApplyAuras(float dt)
        {
            int n = _sim.Count;
            if (n == 0) return;
            var agents = _sim.Agents;
            _auraOwners.Clear();
            for (int i = 0; i < n; i++)
            {
                var a = agents[i];
                if (a.AuraRadius > 0f && AuraOn(a, dt)) _auraOwners.Add(i);
                if (a.Buff != 0f) { a.Buff = 0f; agents[i] = a; }
            }
            foreach (int o in _auraOwners)
            {
                var owner = agents[o];
                _sim.QueryCircle(owner.Pos, owner.AuraRadius, _auraNear);
                foreach (int j in _auraNear)
                {
                    if (j == o || j >= n) continue; // 격자에 남은 걷어 낸 자리
                    var e = agents[j];
                    if (e.AuraRadius > 0f || e.Buff >= owner.AuraBonus) continue;
                    e.Buff = owner.AuraBonus;
                    agents[j] = e;
                }
            }
        }

        /// <summary>시험용: 지금 기수 깃발 아래(Buff &gt; 0) 있는 적 수.</summary>
        public int BuffedCount()
        {
            int c = 0;
            var agents = _sim.Agents;
            for (int i = 0; i < _sim.Count; i++) if (agents[i].Buff > 0f) c++;
            return c;
        }

        void Sync()
        {
            foreach (var id in _sim.Removed) { _views.Remove(id); _poolTimers.Remove(id); _marks.Remove(id); _twinOf.Remove(id); _hookStates.Remove(id); _roars.Remove(id); }
            _sim.Removed.Clear();
            if (clusters == null) return;
            clusters.Clear();
            var agents = _sim.Agents;
            for (int i = 0; i < _sim.Count; i++)
            {
                var a = agents[i];
                if (!_views.TryGetValue(a.Id, out var view)) continue;
                view.Position = new Vector2(a.Pos.x, a.Pos.y);
                view.Health = a.MaxHealth > 0 ? Mathf.Clamp01(a.Health / a.MaxHealth) : 1f;
                if (a.Move == (byte)EnemyMove.Hop) view.HopBeat = a.Hop;
                else view.HopBeat = -2f; // 0 아래면 콩콩 없음
                // 색은 늘 종류 색에서 다시 얹는다(ClusterView 는 프레임을 넘어 남으니 지난 프레임 색에 겹쳐 섞지 않게)
                var baseTint = a.Kind >= 0 && a.Kind < _kinds.Count ? _kinds[a.Kind].Tint : view.Tint;
                if (a.Buff > 0f) baseTint = Color.Lerp(baseTint, BuffTint, 0.45f); // 기수 깃발 아래: 핏빛이 돈다(이펙트 오기 전 임시)
                if (a.Move == (byte)EnemyMove.Dash && a.Phase == (byte)DashPhase.Telegraph)
                    view.Tint = Color.Lerp(baseTint, TelegraphTint, Mathf.PingPong(Time.time * 8f, 1f)); // 예고: 붉게 깜빡임(이펙트 오기 전 임시)
                else
                    view.Tint = baseTint;
                clusters.Add(view);
            }
        }

        void OnGUI()
        {
            if (!showStats) return;
            GUI.Label(new Rect(12, 12, 520, 22), $"적 {Count}/{targetCount}  시뮬 {_simMs:0.00}ms  {_fps:0}fps   [ ] 수 조절, C 비우기");
        }
    }
}
