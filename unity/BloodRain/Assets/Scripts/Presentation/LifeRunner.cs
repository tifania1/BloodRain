using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Run;
using BloodRain.Core.Spawning;
using BloodRain.Presentation.Combat;
using BloodRain.Presentation.Fx;
using BloodRain.Presentation.Hud;
using BloodRain.Presentation.Meta;
using BloodRain.Sim;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BloodRain.Presentation
{
    // LF-01 Run 씬의 한 생. Core Life 하나를 들고 매 프레임 Tick 한다.
    //   혼·몸: 침방에서 넘어온 RebirthHandoff, 없으면(Run 씬을 바로 켬) 새 혼 + 서장 몸.
    //   진기: OrbLayer.Absorbed → life.Absorb. 경지를 넘으면 BreakthroughFx 로 줌.
    //   레벨업 화면은 Life 의 Level·Charges·Loadout 을 본다(LevelUpScreen 이 Life 를 찾는다).
    //   생성(LF-02): Tick 이 돌려주는 LifeSecond 의 Spawns 는 생성 고리(balance.json spawnTarget 대각선 배수)에, Events 는 종류대로 세운다.
    //   덩이 사람 수는 EnemyField 가 지금 경지로 정한다(TU-04 덩이 길). 보부상·은빛 등롱·보스 사건은 아직 로그만(BR-01·C-09b).
    //   자리: Unity 좌표(1 = 세계 m, 출생지 = 0,0) ↔ 천하 좌표는 WorldCoords(PropLayer 가 Origin 을 출생지 한가운데로 둔다).
    //   쓰러짐(LF-03): 천하 몸이면 Life.End → Soul.EndLife → SoulStore 저장 → 정산 → Inn 씬(침방). 서장 몸이면 PlayerDown 대로 각성·처음부터.
    [DefaultExecutionOrder(-50)]
    public sealed class LifeRunner : MonoBehaviour
    {
        public Transform player;
        public OrbLayer orbs;
        public EnemyField enemies;
        public BreakthroughFx breakthrough;
        public PlayerVitals vitals;
        [Tooltip("생이 끝나면 여는 정산 화면(loadInnScene 켬)")]
        public SettlementScreen settlement;
        public bool showDebug = true;

        long _kills;

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
                    // LF-03 혼은 침방이 넘긴 것, 없으면(Run 씬을 바로 켬) 저장된 혼(없으면 새 혼)
                    var soul = RebirthHandoff.Soul ?? SoulStore.Load(_data);
                    RebirthHandoff.Soul = soul;
                    if (RebirthHandoff.PendingLife != null)
                    {
                        // 서장에서 쓰러져 처음부터: RestartPrologue 가 만든 생(쓰러진 횟수를 잇는다)
                        _life = RebirthHandoff.PendingLife;
                        RebirthHandoff.PendingLife = null;
                    }
                    else
                    {
                        var body = RebirthHandoff.Body ?? Rebirth.Prologue(_data);
                        _life = new Life(soul, body, new System.Random());
                    }
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
            if (vitals != null) vitals.Died -= OnDied;
            if (enemies != null) enemies.Killed -= OnKilled;
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
            if (vitals == null && player != null) vitals = player.GetComponent<PlayerVitals>();
            if (vitals != null) vitals.Died += OnDied;
            if (enemies != null) enemies.Killed += OnKilled;
            if (settlement == null) settlement = FindFirstObjectByType<SettlementScreen>();
            SyncScale();
        }

        void OnKilled(Vector2 at, EnemyAgent a) => _kills++;

        // ---- C-06a 몸 능력치 ----
        // 능력치 한 장 = life.CurrentStats(심법 시너지 덧셈 + 깨달음, 기혈 비율, 움직였나). 레벨업 선택·돌파·기혈 50% 넘나듦·멈춤↔움직임을
        // 일일이 잡는 대신 0.25초마다 다시 매긴다(값이 같으면 넣는 쪽이 바뀌지 않는다).

        /// <summary>지금 몸에 얹힌 능력치(디버그 줄·다른 화면이 본다).</summary>
        public StatSheet Stats { get; private set; }

        public WeaponRunner weapons;
        float _statTimer;
        Vector3 _lastStatPos;

        void ApplyStats()
        {
            if (_life == null || _life.Record != null) return;
            var soul = RebirthHandoff.Soul;
            var syn = BloodRain.Core.Build.Synergy.Compute(_data, _life.Loadout, null, soul != null ? soul.RestoredManuals : null);
            var extra = syn.StatBonus();
            float ratio = vitals != null && vitals.MaxHealth > 0f ? vitals.Health / vitals.MaxHealth : 1f;
            extra.Add(_life.InsightBonus((int)System.Math.Min(_kills, int.MaxValue), ratio));
            bool moving = player != null && (player.position - _lastStatPos).sqrMagnitude > 1e-6f;
            if (player != null) _lastStatPos = player.position;
            var st = _life.CurrentStats(extra, ratio, moving);
            Stats = st;

            if (vitals != null)
            {
                vitals.SetMaxHealth(st.MaxHealth);
                vitals.armor = st.Armor;
            }
            if (player != null && player.TryGetComponent<PlayerController>(out var pc)) pc.MoveMultiplier = st.MoveSpeed;
            if (orbs != null) orbs.MagnetMeters = st.Magnet;
            if (weapons == null) weapons = FindFirstObjectByType<WeaponRunner>();
            if (weapons != null)
            {
                weapons.cooldownReduction = st.CooldownReduction;
                weapons.areaMultiplier = st.Area;
                weapons.extraAmount = st.Amount;
                weapons.durationMultiplier = st.Duration;
            }
            ApplySynergyEffects(syn, ratio);
        }

        // ---- C-06b 곱하는 진전 효과 ----
        // SynergyEffects 의 배율을 그 자리에 곱한다: 남궁 2 검·개방 3 낮은 기혈(무공 한 방) · 곤륜 3 강시(맞는 적) · 수로채 3 경공 거리 ·
        // 무당 3 사량발천근(태극일 때 받는 피해). 무당 2 태극 폭은 Gauge 에. 곤륜 2 정신부·마도 2 흡혈·음식은 그 장치가 생기면(C-09b 등) Effects 를 읽는다.

        /// <summary>지금 켜진 곱하는 진전 효과(디버그 줄·다른 장치가 본다).</summary>
        public BloodRain.Core.Build.SynergyEffects Effects { get; private set; } = BloodRain.Core.Build.SynergyEffects.None;

        void ApplySynergyEffects(BloodRain.Core.Build.SynergyState syn, float healthRatio)
        {
            var fx = BloodRain.Core.Build.SynergyEffects.Of(syn);
            Effects = fx;
            var gauge = _life.Loadout.Gauge;
            gauge.SetSynergyHarmonyRange(syn.HarmonyRange);
            if (weapons != null) { weapons.effects = fx; weapons.healthRatio = healthRatio; }
            if (enemies != null) enemies.effects = fx;
            if (player != null && player.TryGetComponent<PlayerController>(out var pc))
            {
                pc.DashDistanceMultiplier = fx.DashDistanceMultiplier;
                var drag = fx.Tier("dash_drag"); // C-06c 끌기
                pc.dragField = enemies;
                pc.DragRadius = drag != null ? drag.Param("dragRadius") : 0f;
                pc.DragFactor = drag != null ? drag.Param("dragFactor") : 0f;
            }
            if (vitals != null)
                vitals.deflect = fx.Has("harmony_deflect")
                    ? taken => fx.Deflect(taken, gauge.Band == BloodRain.Core.Progression.YinYangBand.Harmony).taken
                    : (System.Func<float, float>)null;
        }

        // ---- LF-03 쓰러짐 ----

        /// <summary>시험 메뉴 '지금 쓰러지기'.</summary>
        public void FallNow()
        {
            if (vitals != null) vitals.ForceDown();
            else OnDied();
        }

        void OnDied()
        {
            if (_life == null || _life.Record != null) return;
            if (_life.IsPrologue)
            {
                // 서장: 정산으로 가지 않는다. 각성이면 기혈 1로 이어서, 아니면 같은 혼·몸으로 처음부터(서장 끝은 C-10b)
                var down = _life.PlayerDown();
                Debug.Log($"[LifeRunner] 서장에서 쓰러짐 → {down}");
                if (down == PrologueDown.Awaken)
                {
                    if (vitals != null) vitals.Revive(1f);
                    return;
                }
                RebirthHandoff.PendingLife = _life.RestartPrologue(new System.Random());
                SceneManager.LoadScene("Run");
                return;
            }
            EndLife(LifeEndWay.Fallen);
        }

        /// <summary>한 생을 끝내고 혼에 옮겨 저장한 뒤 정산을 연다. 정산이 끝나면 객잔(Inn) 씬.</summary>
        public void EndLife(string way)
        {
            // 은자는 한 생의 지갑(Life.Silver)으로 정산한다(C-06a). 행낭 카드가 GainSilver 로 넣는다
            var soul = RebirthHandoff.Soul;
            var record = _life.End(way);
            var summary = soul.EndLife(record);
            SoulStore.Save(soul);
            RebirthHandoff.ClearBody();
            Debug.Log($"[LifeRunner] 생이 끝남({way}) · 윤회의 기 +{record.RebirthQi.Total:N0} → {soul.RebirthQi:N0} · {soul.Lives}생 · 저장 {SoulStore.SoulPath}");

            Time.timeScale = 0f;
            if (settlement == null)
            {
                Debug.LogWarning("[LifeRunner] 정산 화면이 없어 바로 객잔으로");
                Time.timeScale = 1f;
                SceneManager.LoadScene("Inn");
                return;
            }
            settlement.loadInnScene = true;
            settlement.Finished += () => Time.timeScale = 1f;
            var stats = new SettlementStats
            {
                SurvivalSeconds = (float)(_life.Minutes * 60.0),
                Kills = _kills,
                BloodCrystal = record.BloodCrystal,
                // 무공별 피해 막대는 WeaponRunner 가 센 합(때린 피해 × 맞은 수, 남은 기혈을 넘긴 몫도 들어감)
                DamageByWeapon = weapons != null
                    ? weapons.Dealt.Where(kv => kv.Value > 0f).ToDictionary(kv => kv.Key, kv => (double)kv.Value)
                    : new Dictionary<string, double>(),
            };
            settlement.Show(record, summary, stats, soul.RebirthQi);
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
            _statTimer -= Time.unscaledDeltaTime;
            if (_statTimer <= 0f) { _statTimer = 0.25f; ApplyStats(); }
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
            if (Stats != null)
            {
                var st = Stats;
                GUI.Label(new Rect(12, 118, 1100, 22),
                    $"능력치  최대 기혈 {st.MaxHealth:0}  호신 {st.Armor:0.#}  회복 {st.Recovery:0.#}  걸음 x{st.MoveSpeed:0.00}  공력 x{st.Might:0.00}  쾌속 {st.CooldownReduction * 100:0}%  범위 x{st.Area:0.00}  개수 +{st.Amount}  지속 x{st.Duration:0.00}  흡기 {st.Magnet:0.0}m  회피 {st.Evade * 100:0}%");
                // C-06b 곱하는 진전 효과
                var fx = Effects;
                var sword = fx.Tier("sword_damage");
                GUI.Label(new Rect(12, 140, 1100, 22),
                    $"진전 배율  검 피해 x{(sword != null ? 1f + sword.Param("damageBonus") : 1f):0.00}  낮은 기혈 {(fx.Has("low_health_might") ? "켜짐" : "-")}  강시 피해 {(fx.Has("jiangshi_damage") ? "켜짐" : "-")}  경공 거리 x{fx.DashDistanceMultiplier:0.00}  태극 폭 ±{_life.Loadout.Gauge.HarmonyRange}  사량발천근 {(fx.Has("harmony_deflect") ? "켜짐" : "-")}  정신부 x{fx.FreezeMultiplier:0.00}  흡혈 x{fx.LifestealMultiplier:0.00}");
            }
        }
    }
}
