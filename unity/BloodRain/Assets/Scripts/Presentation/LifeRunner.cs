using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Run;
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
    //   생성 지시(Tick 이 돌려주는 LifeSecond)는 LF-02 전까지 버린다.
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
            _ = _life.Tick(dt, pos, alive); // LF-02 에서 적 생성에 쓴다
            if (breakthrough == null || !breakthrough.IsPlaying) SyncScale();
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
