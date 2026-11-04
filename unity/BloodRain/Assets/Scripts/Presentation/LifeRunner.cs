using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Run;
using BloodRain.Core.Spawning;
using BloodRain.Presentation.Fx;
using BloodRain.Presentation.Meta;
using BloodRain.Sim;
using UnityEngine;

namespace BloodRain.Presentation
{
    // LF-01 Run 씬의 한 생. Core Life 하나를 들고 매 프레임 Tick 한다.
    //   혼·몸: 침방에서 넘어온 RebirthHandoff, 없으면(Run 씬을 바로 켬) 새 혼 + 서장 몸.
    //   진기: OrbLayer.Absorbed → life.Absorb. 경지를 넘으면 BreakthroughFx 로 줌.
    //   레벨업 화면은 Life 의 Level·Charges·Loadout 을 본다(LevelUpScreen 이 Life 를 찾는다).
    //   생성(LF-02): Tick 이 돌려주는 LifeSecond 의 Spawns 는 생성 고리(balance.json spawnTarget 대각선 배수)에, Events 는 종류대로 세운다.
    //   덩이 사람 수는 EnemyField 가 지금 경지로 정한다(TU-04 덩이 길). 보부상·은빛 등롱·보스 사건은 아직 로그만(BR-01·C-09b).
    //   자리: Unity 좌표(1 = 세계 m, 출생지 = 0,0) ↔ 천하 좌표는 WorldCoords(PropLayer 가 Origin 을 출생지 한가운데로 둔다).
    [DefaultExecutionOrder(-50)]
    public sealed class LifeRunner : MonoBehaviour
    {
        public Transform player;
        public OrbLayer orbs;
        public EnemyField enemies;
        public BreakthroughFx breakthrough;
        public bool showDebug = true;

        public static LifeRunner Instance { get; private set; }

        Life _life;
        GameData _data;

        /// <summary>이번 생. 처음 부를 때 만든다(다른 컴포넌트의 Start 순서와 상관없이 같은 것).</summary>
        public Life Life
        {
            get
            {
                if (_life == null)
                {
                    _data = DataBoot.Data;
                    var soul = RebirthHandoff.Soul ?? new Soul(_data);
                    var body = RebirthHandoff.Body ?? Rebirth.Prologue(_data);
                    _life = new Life(soul, body, new System.Random());
                }
                return _life;
            }
        }

        void Awake()
        {
            Instance = this;
            _ = Life;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (orbs != null) orbs.Absorbed -= OnAbsorbed;
        }

        void Start()
        {
            if (orbs == null) orbs = FindFirstObjectByType<OrbLayer>();
            if (enemies == null) enemies = FindFirstObjectByType<EnemyField>();
            if (player == null)
            {
                var pc = FindFirstObjectByType<PlayerController>();
                if (pc != null) player = pc.transform;
            }
            if (breakthrough == null) breakthrough = GetComponent<BreakthroughFx>();
            if (orbs != null) orbs.Absorbed += OnAbsorbed;
            SyncScale();
        }

        void Update()
        {
            if (_life == null || _life.Record != null) return;
            // 레벨업 화면 등으로 세상이 멈췄으면(timeScale 0) 생의 시간도 멈춘다
            float dt = Time.timeScale > 0f ? Time.unscaledDeltaTime : 0f;
            var pos = player != null ? WorldCoords.FromUnity(player.position) : _life.Position;
            int alive = enemies != null ? enemies.Count : 0;
            var second = _life.Tick(dt, pos, alive);
            if (second != null) SpawnSecond(second);
            if (breakthrough == null || !breakthrough.IsPlaying) SyncScale();
        }

        System.Random _spawnRng = new System.Random();

        Vector2 RingPos(float scale = 1f)
        {
            var st = _data.Balance.SpawnTarget;
            float diag = WorldScale.Current != null ? WorldScale.Current.ViewDiagonal : 30f;
            float ang = (float)(_spawnRng.NextDouble() * Mathf.PI * 2);
            float rad = diag * Mathf.Lerp(st.RingMinDiagonal, st.RingMaxDiagonal, (float)_spawnRng.NextDouble()) * scale;
            return (Vector2)player.position + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad;
        }

        void SpawnSecond(LifeSecond s)
        {
            if (enemies == null || player == null) return;
            if (!s.Held)
                foreach (var sp in s.Spawns)
                    enemies.SpawnAt(_data.Enemy(sp.Enemy), RingPos());
            foreach (var e in s.Events) SpawnEvent(e);
        }

        // 사건 모양은 웹 프로토타입 runEvent 와 같다(혈인 떼 한 줄 벽, 동갑시 포위 원, 금두꺼비는 가까운 고리).
        void SpawnEvent(LifeEvent e)
        {
            var def = e.Wave.Def;
            var p = (Vector2)player.position;
            float diag = WorldScale.Current != null ? WorldScale.Current.ViewDiagonal : 30f;
            int n = Mathf.Max(1, def.Count);
            switch (def.Type)
            {
                case WaveEventKind.Charge:
                {
                    // 한 방향에서 한 줄 벽으로 돌진. 어딘가에 두 칸 빈틈(경공으로 뚫거나 빈틈으로 빠진다)
                    var enemy = _data.Enemy(string.IsNullOrEmpty(e.Enemy) ? "hyeolin" : e.Enemy);
                    int side = _spawnRng.Next(4);
                    var ax = new Vector2(new[] { 1f, -1f, 0f, 0f }[side], new[] { 0f, 0f, 1f, -1f }[side]);
                    var along = new Vector2(-ax.y, ax.x);
                    int gap = Mathf.FloorToInt(Mathf.Lerp(n * 0.25f, n * 0.75f, (float)_spawnRng.NextDouble()));
                    float step = WorldScale.Current != null ? WorldScale.Current.Length(1.2f, WorldScale.Current.Realm) : 1.2f;
                    for (int i = 0; i < n; i++)
                    {
                        if (i == gap || i == gap + 1) continue;
                        enemies.SpawnAt(enemy, p - ax * diag * 0.6f + along * (i - n / 2f) * step);
                    }
                    break;
                }
                case WaveEventKind.Ring:
                {
                    var enemy = _data.Enemy(string.IsNullOrEmpty(e.Enemy) ? "donggap" : e.Enemy);
                    for (int i = 0; i < n; i++)
                    {
                        float a = i / (float)n * Mathf.PI * 2f;
                        enemies.SpawnAt(enemy, p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * diag * 0.6f);
                    }
                    break;
                }
                case WaveEventKind.Group:
                {
                    // 한 방향에서 한 종류가 무리로
                    var enemy = _data.Enemy(e.Enemy);
                    var at = RingPos();
                    float spread = diag * 0.08f;
                    for (int i = 0; i < n; i++)
                        enemies.SpawnAt(enemy, at + Random.insideUnitCircle * spread);
                    break;
                }
                case WaveEventKind.Elite:
                    enemies.SpawnAt(_data.Enemy(e.Enemy), RingPos());
                    break;
                case WaveEventKind.Toad:
                    enemies.SpawnAt(_data.Enemy(e.Enemy), RingPos(_data.Balance.SpawnTarget.ToadRingScale));
                    break;
                default:
                    Debug.Log($"[LifeRunner] 사건 {def.Kind} {e.Enemy} ({e.Wave.At:0.0}분) — 아직 세우지 않음");
                    break;
            }
        }

        // 화면 축척은 레벨 + 진기 비율(경지 안에서 조금씩 넓어진다). 돌파 중엔 BreakthroughFx 가 맡는다.
        void SyncScale()
        {
            var ws = WorldScale.Current;
            if (ws == null || _life == null) return;
            ws.SetLevel(LevelFloat);
        }

        float LevelFloat => _life.Level + (float)System.Math.Min(_life.Xp.Progress, 0.999);

        void OnAbsorbed(double xp, int count)
        {
            if (_life == null || _life.Record != null) return;
            float from = LevelFloat;
            var r = _life.Absorb(xp, count);
            if (r.Breakthroughs.Count == 0) return;
            var ws = WorldScale.Current;
            if (breakthrough == null || ws == null || player == null) return;
            bool revisit = r.Breakthroughs.All(i => !i.LongCinematic);
            breakthrough.Play(ws, from, LevelFloat, player, revisit);
        }

        void OnGUI()
        {
            if (!showDebug || _life == null) return;
            var realm = _life.Scale.Realms[_life.Realm];
            var region = _life.Regions.Region(_life.RegionId);
            GUI.color = Color.white;
            GUI.Label(new Rect(12, 96, 900, 22),
                $"한 생  {_life.Minutes:0.0}분  레벨 {_life.Level} ({_life.Xp.Progress * 100:0}%)  경지 {realm.NameKo}  지역 {region.NameKo}  혈운 {_life.Density:0.00}" +
                (_life.IsPrologue ? "  [서장]" : ""));
        }
    }
}
