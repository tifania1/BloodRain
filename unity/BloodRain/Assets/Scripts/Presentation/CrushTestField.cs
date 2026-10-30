using System.Collections.Generic;
using BloodRain.Presentation.Fx;
using BloodRain.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BloodRain.Presentation
{
    // 밟힘과 쓸림 시험(TU-03). 적 시뮬레이션(C-02)이 아직 없으니 축척 시험 씬에 적을 직접 놓는다.
    //   E  지금 경지 적 한 무리를 플레이어 둘레에   T  지금 경지 구슬·음식·은자·등롱을 둘레에
    // 같은 경지 적은 플레이어를 쫓는다(닿아도 여기선 피해 계산 없음). 플레이어 경지가 그 적보다 오르면
    // 이동을 멈추고 CrushField 로 옮겨 작게 남는다. 닿으면 먹물로 터지며 쓰러진다(진기 0.3).
    // 돌파 0.8초(BreakthroughFx.Swept)에 쓸림 반경 안 아래 경지 적이 한꺼번에 쓸린다.
    public sealed class CrushTestField : MonoBehaviour
    {
        [SerializeField] int foesPerWave = 24;
        [SerializeField] int itemsPerDrop = 16;
        [SerializeField] float foeRealmMeters = 1.0f;     // 지름
        [SerializeField] float foeRealmSpeed = 1.6f;
        [SerializeField] int seed = 11;

        static readonly Color FoeColor = new Color(0.32f, 0.30f, 0.34f);
        static readonly Color EliteColor = new Color(0.55f, 0.18f, 0.16f);

        struct Active { public int Id; public Transform T; public int Born; public bool Elite; }

        readonly List<Active> _active = new List<Active>();
        readonly Dictionary<int, Transform> _shown = new Dictionary<int, Transform>();
        readonly CrushField _field = new CrushField();
        readonly List<Crushable> _hits = new List<Crushable>();
        int _nextActiveId = 1_000_000;

        ScaleTestDriver _d;
        InkBurst _ink;
        System.Random _rng;
        LineRenderer _sweepRing;
        float _sweepRingAge = -1f, _sweepRingRadius;

        public int Crushed { get; private set; }
        public int Swept { get; private set; }
        public float Xp { get; private set; }
        public int Orbs { get; private set; }
        public int Food { get; private set; }
        public int Silver { get; private set; }
        public int Lanterns { get; private set; }
        public int Chests { get; private set; }
        public CrushField Field => _field;
        public int ActiveCount => _active.Count;

        void Start()
        {
            _d = GetComponent<ScaleTestDriver>();
            _rng = new System.Random(seed);
            _ink = gameObject.AddComponent<InkBurst>();
            _ink.Init(_d.Circle, _d.Mat);
            _sweepRing = new GameObject("쓸림 고리").AddComponent<LineRenderer>();
            _sweepRing.useWorldSpace = false;
            _sweepRing.loop = true;
            _sweepRing.positionCount = 96;
            _sweepRing.sharedMaterial = _d.Mat;
            _sweepRing.sortingOrder = 11;
            _sweepRing.enabled = false;
            _d.Fx.Swept += OnSwept;

            SpawnWave();
            DropItems();
        }

        void OnDestroy()
        {
            if (_d != null && _d.Fx != null) _d.Fx.Swept -= OnSwept;
        }

        Vector2 AroundPlayer(float min, float max)
        {
            float a = (float)(_rng.NextDouble() * Mathf.PI * 2);
            float r = Mathf.Lerp(min, max, (float)_rng.NextDouble());
            return (Vector2)_d.Player.position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }

        /// <summary>지금 경지 적 한 무리. 화면 대각선 0.15~0.45배 둘레에.</summary>
        public void SpawnWave()
        {
            var ws = _d.World;
            int realm = ws.Realm;
            float diag = ws.Scale.ViewDiagonalAtRealm(realm);
            for (int i = 0; i < foesPerWave; i++)
            {
                bool elite = i == 0;
                var p = AroundPlayer(diag * 0.15f, diag * 0.45f);
                var t = _d.MakeDisc($"적 {ws.Scale.Realms[realm].NameKo}{(elite ? " 정예" : "")}",
                    elite ? EliteColor : FoeColor, ws.Length(foeRealmMeters * (elite ? 1.4f : 1f), realm), -0.5f);
                t.position = new Vector3(p.x, p.y, -0.5f);
                _active.Add(new Active { Id = _nextActiveId++, T = t, Born = realm, Elite = elite });
            }
        }

        /// <summary>지금 경지 바닥 물건. 화면 대각선 0.05~0.4배 둘레에.</summary>
        public void DropItems()
        {
            var ws = _d.World;
            int realm = ws.Realm;
            float diag = ws.Scale.ViewDiagonalAtRealm(realm);
            for (int i = 0; i < itemsPerDrop; i++)
            {
                var kind = (CrushKind)(1 + i % 4);
                float rad; Color col;
                switch (kind)
                {
                    case CrushKind.Orb: rad = 0.15f; col = new Color(0.35f, 0.65f, 0.55f); break;
                    case CrushKind.Food: rad = 0.25f; col = new Color(0.85f, 0.75f, 0.55f); break;
                    case CrushKind.Silver: rad = 0.15f; col = new Color(0.75f, 0.76f, 0.80f); break;
                    default: rad = 0.4f; col = new Color(0.85f, 0.35f, 0.15f); break;
                }
                var p = AroundPlayer(diag * 0.05f, diag * 0.4f);
                int id = _field.Add(new Crushable { Kind = kind, Position = p, BornRealm = realm, RadiusRealmMeters = rad });
                var t = _d.MakeDisc(kind.ToString(), col, ws.Length(rad * 2f, realm), -0.4f);
                t.position = new Vector3(p.x, p.y, -0.4f);
                _shown[id] = t;
            }
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.eKey.wasPressedThisFrame) SpawnWave();
                if (kb.tKey.wasPressedThisFrame) DropItems();
            }

            var ws = _d.World;
            Vector2 pp = _d.Player.position;

            // 같은 경지 적은 쫓고, 아래 경지가 된 적은 멈춘 목록으로 옮긴다
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var a = _active[i];
                if (ws.Scale.Crushes(a.Born, ws.Realm))
                {
                    int id = _field.Add(new Crushable
                    {
                        Kind = CrushKind.Foe, Position = a.T.position, BornRealm = a.Born,
                        RadiusRealmMeters = foeRealmMeters * (a.Elite ? 0.7f : 0.5f), Elite = a.Elite,
                    });
                    _shown[id] = a.T;
                    _active.RemoveAt(i);
                    continue;
                }
                Vector2 p = a.T.position;
                var to = pp - p;
                float stop = ws.Length(foeRealmMeters, a.Born);
                if (to.sqrMagnitude > stop * stop)
                    p += to.normalized * ws.Speed(foeRealmSpeed, a.Born) * Time.deltaTime;
                a.T.position = new Vector3(p.x, p.y, a.T.position.z);
            }

            _hits.Clear();
            _field.Touch(ws, pp, _d.PlayerRadius, _hits);
            foreach (var h in _hits) Resolve(h, sweep: false);

            if (_sweepRingAge >= 0f)
            {
                _sweepRingAge += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(_sweepRingAge / 0.45f);
                _sweepRing.transform.position = new Vector3(pp.x, pp.y, -2f);
                float r = _sweepRingRadius * Mathf.Lerp(0.2f, 1f, 1f - (1f - k) * (1f - k));
                _sweepRing.startWidth = _sweepRing.endWidth = ws.ViewDiagonal * 0.004f * (1f - k);
                _sweepRing.startColor = _sweepRing.endColor = new Color(0.08f, 0.07f, 0.07f, 0.8f * (1f - k));
                for (int i = 0; i < _sweepRing.positionCount; i++)
                {
                    float ang = i * Mathf.PI * 2f / _sweepRing.positionCount;
                    _sweepRing.SetPosition(i, new Vector3(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r, 0f));
                }
                if (k >= 1f) { _sweepRingAge = -1f; _sweepRing.enabled = false; }
            }
        }

        void OnSwept(BloodRain.Core.World.Breakthrough bt)
        {
            var ws = _d.World;
            Vector2 pp = _d.Player.position;
            _hits.Clear();
            int n = _field.Sweep(ws.Scale, pp, bt.SweepRadius, bt.ToRealm, _hits);
            foreach (var h in _hits) Resolve(h, sweep: true);
            Swept += n;
            _sweepRingRadius = bt.SweepRadius;
            _sweepRingAge = 0f;
            _sweepRing.enabled = true;
            Debug.Log($"[밟힘] 쓸림 {n}명, 반경 {bt.SweepRadius:0.0}m, 남은 밟힘 목록 {_field.Count}");
        }

        void Resolve(Crushable c, bool sweep)
        {
            var ws = _d.World;
            float size = ws.Length(c.RadiusRealmMeters * 2f, c.BornRealm);
            switch (c.Kind)
            {
                case CrushKind.Foe:
                    Crushed++;
                    Xp += ws.Scale.CrushXp;
                    if (ws.Scale.CrushedDropsChest(c.Elite)) { Chests++; Debug.Log("[밟힘] 아래 경지 정예가 보갑을 떨굼"); }
                    _ink.Burst(c.Position, size, InkBurst.Ink, sweep ? (Vector2)_d.Player.position : (Vector2?)null);
                    break;
                case CrushKind.Lantern:
                    Lanterns++;
                    _ink.Burst(c.Position, size, InkBurst.Ember, null);
                    break;
                case CrushKind.Orb: Orbs++; break;
                case CrushKind.Food: Food++; break;
                case CrushKind.Silver: Silver++; break;
            }
            if (_shown.TryGetValue(c.Id, out var t))
            {
                if (t != null) Destroy(t.gameObject);
                _shown.Remove(c.Id);
            }
        }

        void OnGUI()
        {
            GUI.color = new Color(0.15f, 0.1f, 0.08f);
            GUI.Label(new Rect(12, 30, 1000, 24),
                $"밟힘 {Crushed} (쓸림 {Swept})  진기 +{Xp:0.0}  보갑 {Chests}  구슬 {Orbs}  음식 {Food}  은자 {Silver}  등롱 {Lanterns}   " +
                $"쫓는 적 {_active.Count}  밟힘 목록 {_field.Count}   [E 적 무리  T 바닥 물건]");
        }
    }
}
