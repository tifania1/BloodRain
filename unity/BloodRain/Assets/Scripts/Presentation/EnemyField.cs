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

        float _playerBody = 0.8f;
        readonly List<EnemySim.ContactHit> _hits = new List<EnemySim.ContactHit>();

        // 시험용 생성표: (적 id, 가중치)
        static readonly (string id, float w)[] Mix =
        {
            ("sigang", 50f), ("hyeol_gangsi", 22f), ("hyeolin", 10f), ("hyeol_musa", 10f), ("donggap", 8f),
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
                var pc = player.GetComponent<PlayerController>();
                if (pc != null) _playerBody = pc.bodyRealmMeters;
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
                default: return EnemyMove.Chase; // telegraph_dash·fly_curve 등은 C-04 뒤 갈래를 붙이기 전까지 곧장 쫓는다
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
                ContactDamage = def.ContactDamage, // 시간에 따른 배율(enemyScaling.damagePerMinute)은 C-10 디렉터가 곱한다
                BornRealm = realm,
                Move = (byte)move,
            };
            int kindIndex = _kinds.FindIndex(k => k.Def == def);
            a.Kind = kindIndex;
            int id = _sim.Spawn(a);
            if (id < 0) return -1;
            var tint = kindIndex >= 0 ? _kinds[kindIndex].Tint : TintOf(def.Family);
            _views[id] = new ClusterView { BornRealm = realm, Seed = id, Tint = tint, Position = pos, HopDuty = EnemySim.HopDuty, HopBeat = move == EnemyMove.Hop ? a.Hop : -2f };
            return id;
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
            float cell = Mathf.Max(0.01f, RadiusOf(ws.Realm) * 2.2f);
            _sim.Step(dt, new float2(p.x, p.y), new float2(_heading.x, _heading.y), cell,
                      diag * st.RecycleBeyondDiagonal, diag * st.RingMaxDiagonal);
            // 접촉 피해(C-03): 플레이어 원에 닿은 적, 같은 적은 contact.sameEnemyIntervalSeconds 마다 한 번
            if (vitals != null)
            {
                float pr = ws.Length(_playerBody) * 0.5f;
                var scale = ws.Scale;
                _sim.Contacts(new float2(p.x, p.y), pr, DataBoot.Data.Balance.Contact.SameEnemyIntervalSeconds, ws.Realm,
                              (born, pr2) => scale.DealsContactDamage(born, pr2), _hits);
                foreach (var h in _hits) vitals.Hit(h.Damage);
            }
            _watch.Stop();
            _simMs = Mathf.Lerp(_simMs, (float)_watch.Elapsed.TotalMilliseconds, 0.1f);
            _fps = Mathf.Lerp(_fps, dt > 0 ? 1f / Time.unscaledDeltaTime : 0f, 0.05f);

            Sync();
        }

        void Sync()
        {
            foreach (var id in _sim.Removed) _views.Remove(id);
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
