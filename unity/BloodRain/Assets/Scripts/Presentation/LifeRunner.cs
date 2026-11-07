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
    //   덩이 사람 수는 EnemyField 가 지금 경지로 정한다(TU-04 덩이 길). 보부상 사건은 MerchantScreen 을 연다(RM-05U). 보스 사건은 아직 로그만(BR-01). 등롱·드롭·은빛 등롱은 LanternLayer(C-09b·QA-02U).
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
            if (_pc != null) _pc.Dashed -= OnDashed;
        }

        PlayerController _pc;

        void OnDashed()
        {
            if (_life == null || _life.Record != null) return;
            _life.Log.Dash();
            _life.Backlash.OnDash(); // C-08b 역류 시작 1초 안이면 끊김(Canceled)
        }

        // ---- C-08b 기혈역류 ----
        // Life.Tick 이 Backlash.Tick 을 이미 부른다. 여기선 사건만 받는다: 끝까지 버티면 최대 기혈 5% 피해(호신은 빼지 않음 —
        // 웹처럼 호신만큼 더해 넘긴다), 끊으면 비전 기록(역천 판정, SecretWatch). 걸음 배율은 Update 에서, 화면 맥동·바늘은 YinYangHud.
        SecretWatch _secrets;

        void HookBacklash()
        {
            var b = _life.Backlash;
            _secrets = FindFirstObjectByType<SecretWatch>();
            b.Started += () => Debug.Log("[LifeRunner] 기혈역류 시작");
            b.Ended += () =>
            {
                if (vitals != null) vitals.Hit(b.EndDamage(vitals.MaxHealth) + vitals.armor);
                if (_secrets != null && _secrets.Record != null) _secrets.Record.MissBacklash();
                Debug.Log("[LifeRunner] 기혈역류를 끝까지 맞음");
            };
            b.Canceled += () =>
            {
                if (_secrets != null && _secrets.Record != null) { _secrets.Record.CutBacklash(); _secrets.Poke(); }
                _popups.Add((player != null ? (Vector2)player.position : Vector2.zero, Loc.T("ui.yinyang.cut"), Time.unscaledTime));
                Debug.Log($"[LifeRunner] 기혈역류 끊음 · 연속 {b.CancelStreak}");
            };
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
            _pc = player != null ? player.GetComponent<PlayerController>() : null;
            if (_pc != null) _pc.Dashed += OnDashed;
            if (breakthrough == null) breakthrough = GetComponent<BreakthroughFx>();
            if (orbs != null) orbs.Absorbed += OnAbsorbed;
            if (vitals == null && player != null) vitals = player.GetComponent<PlayerVitals>();
            if (vitals != null) vitals.Died += OnDied;
            if (vitals != null) vitals.Damaged += () => { if (_life != null && _life.Record == null) _life.Charge.OnHit(_life.Loadout.Gauge.Band); };
            if (enemies != null) { enemies.Killed += OnKilled; enemies.UseLife(_life); } // EN-02U 적 기혈·피해 11장 배율, 혈월 걸음
            // C-09b 등롱·바닥 드롭(씬에 없으면 여기서 붙인다)
            if (GetComponent<LanternLayer>() == null) gameObject.AddComponent<LanternLayer>().runner = this;
            if (GetComponent<EncounterLayer>() == null) gameObject.AddComponent<EncounterLayer>().runner = this; // WP-39U 기연
            if (GetComponent<ChestLayer>() == null) gameObject.AddComponent<ChestLayer>().runner = this; // C-09d 보갑·각성
            if (settlement == null) settlement = FindFirstObjectByType<SettlementScreen>();
            // QA-05U 화면 가장자리 땅의 주인 화살표를 이 생의 기록(혼의 봉인석·처치)으로
            _edges = FindFirstObjectByType<BloodRain.Presentation.Fx.EdgeIndicators>();
            if (_edges != null) _edges.Bind(_life.Bosses);
            HookBacklash();
            SyncScale();
        }

        BloodRain.Presentation.Fx.EdgeIndicators _edges;

        /// <summary>KC-01U 한 덩이를 쓰러뜨렸을 때 세는 처치 수 = Scale.KillCount(생긴 경지). 땅의 주인·축척이 없으면 1.</summary>
        int KillCountOf(EnemyAgent a)
        {
            var ws = WorldScale.Current;
            if (ws == null || ws.Scale == null) return 1;
            if (enemies != null && enemies.DefOf(a.Kind)?.Family == "boss") return 1;
            return Mathf.Max(1, ws.Scale.KillCount(a.BornRealm));
        }

        /// <summary>지금까지 센 처치 수(덩이 속 사람 수 합). HUD·시험이 본다.</summary>
        public long Kills => _kills;

        void OnKilled(Vector2 at, EnemyAgent a)
        {
            // KC-01U 처치 수는 덩이 속 사람 수(7장 '적 한 덩이', 웹 clusterOf). 땅의 주인은 1. 진기·혈정·오의는 덩이 하나 값 그대로
            int n = KillCountOf(a);
            _kills += n;
            // R-08U 도전 과제 처치 수. 정예·땅의 주인도 여기서 한 번(KillElite·DefeatBoss 는 보상만 센다)
            if (_life != null && _life.Record == null) _life.Log.Kill(n);
            // WP-34U 금두꺼비(golden_toad 만. 영물은 세지 않는다). 모든 생 합이 연무장 탐욕 상한을 연다
            bool beast = EncounterLayer.Instance != null && EncounterLayer.Instance.TryCatchBeast(a.Id, at); // WP-39U 영물
            if (!beast && _life != null && _life.Record == null && enemies != null && enemies.DefOf(a.Kind)?.Id == "golden_toad") _life.Log.CatchGoldenToad();
            // C-09a 오의 게이지: 처치마다(정예는 따로 더 크게), 음양 띠 배율은 Charge 가 본다
            if (_life != null && _life.Record == null && enemies != null)
            {
                var band = _life.Loadout.Gauge.Band;
                var kd = enemies.DefOf(a.Kind);
                if (kd != null && kd.Family == "elite") _life.Charge.OnEliteKill(band);
                else _life.Charge.OnKill(band);
            }
            // RM-02b 정예를 잡으면 혈정(Life.KillElite 가 이번에 얻은 수를 준다. 혈월이면 1·2개 번갈아). 정산은 LifeRecord.BloodCrystal
            if (_life == null || _life.Record != null || enemies == null) return;
            var def = enemies.DefOf(a.Kind);
            if (def == null || def.Family != "elite") return;
            int got = _life.KillElite();
            if (got > 0) _popups.Add((at, $"{Loc.T("ui.settle.crystal")} +{got}", Time.unscaledTime));
        }

        // 짧은 글자(혈정 +N). 1.2초 동안 위로 뜨며 옅어진다. 글자 모양은 나중에 HUD 쪽으로 옮길 수 있게 여기선 OnGUI 로만
        readonly List<(Vector2 at, string text, float born)> _popups = new List<(Vector2, string, float)>();
        GUIStyle _popupStyle;

        /// <summary>떠오르는 짧은 글(기연 쪽에서 씀). at 이 없으면 플레이어 자리.</summary>
        public void ShowPopup(string text, Vector2? at = null) =>
            _popups.Add((at ?? (player != null ? (Vector2)player.position : Vector2.zero), text, Time.unscaledTime));

        void DrawPopups()
        {
            if (_popups.Count == 0) return;
            var cam = Camera.main;
            if (cam == null) return;
            if (_popupStyle == null)
                _popupStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            for (int i = _popups.Count - 1; i >= 0; i--)
            {
                var (at, text, born) = _popups[i];
                float t = (Time.unscaledTime - born) / 1.2f;
                if (t >= 1f) { _popups.RemoveAt(i); continue; }
                var sp = cam.WorldToScreenPoint(at);
                var c = new Color(0.86f, 0.16f, 0.14f, 1f - t * t);
                GUI.color = c;
                GUI.Label(new Rect(sp.x - 100f, Screen.height - sp.y - 40f - t * 40f, 200f, 30f), text, _popupStyle);
            }
            GUI.color = Color.white;
        }

        // ---- C-06a 몸 능력치 ----
        // 능력치 한 장 = life.CurrentStats(심법 시너지 덧셈 + 깨달음, 기혈 비율, 움직였나). 레벨업 선택·돌파·기혈 50% 넘나듦·멈춤↔움직임을
        // 일일이 잡는 대신 0.25초마다 다시 매긴다(값이 같으면 넣는 쪽이 바뀌지 않는다).

        /// <summary>지금 몸에 얹힌 능력치(디버그 줄·다른 화면이 본다).</summary>
        public StatSheet Stats { get; private set; }

        public WeaponRunner weapons;
        float _statTimer;
        Vector3 _lastStatPos;

        int _extraSlotsGiven;

        void ApplyStats()
        {
            if (_life == null || _life.Record != null) return;
            var soul = RebirthHandoff.Soul;
            // WP-39U 진전 덧셈: 몸의 문파 덤 + 기연(고수의 유해)으로 더 가진 것으로 치는 진전
            var syn = BloodRain.Core.Build.Synergy.Compute(_data, _life.Loadout, _life.Body.SchoolExtra(_life.Encounters.SchoolExtra), soul != null ? soul.RestoredManuals : null);
            var extra = syn.StatBonus();
            float ratio = vitals != null && vitals.MaxHealth > 0f ? vitals.Health / vitals.MaxHealth : 1f;
            extra.Add(_life.InsightBonus((int)System.Math.Min(_kills, int.MaxValue), ratio));
            if (debugRevivals > 0) extra.Add(BloodRain.Core.Build.StatKind.Revival, debugRevivals); // WP-42U 시험 메뉴 '재기 +1'
            bool moving = player != null && (player.position - _lastStatPos).sqrMagnitude > 1e-6f;
            if (player != null) _lastStatPos = player.position;
            var st = _life.CurrentStats(extra, ratio, moving);
            Stats = st;
            var ie = _life.InsightEffects;

            if (vitals != null)
            {
                vitals.SetMaxHealth(st.MaxHealth);
                vitals.armor = st.Armor;
                // C-07a 일류 돌파 뒤 받는 피해 -10% 등, C-07b 혈광(기혈이 문턱 아래면 받는 피해 증가)
                vitals.takenMultiplier = _life.DamageTakenMultiplier * ie.DamageTakenMultiplier(ratio);
                // R-04Ua 문파 고유 능력: 회피(수로채 움직이는 동안), 회복 전부에 곱하는 수(마교 절반), 초당 회복
                vitals.evadeChance = st.Evade;
                // C-07b 흡혈대법이면 흡혈 말고 다른 회복 x0
                vitals.healingMultiplier = _life.HealingMultiplier * ie.OtherHealMultiplier; // 마교 절반 × 혈마의 속삭임 절반(QA-12)
                vitals.block ??= ie.TryBlock; // 금종조(안 골랐으면 늘 false)
                vitals.recoveryPerSecond = st.Recovery;
            }
            if (player != null && player.TryGetComponent<PlayerController>(out var pc))
            {
                pc.MoveMultiplier = st.MoveSpeed;
                pc.KnockbackImmune = _life.PassiveMax.KnockbackImmune; // PM-01U 금강체 Lv5
                // C-07a 경공 충전 칸 = 기본 + 몸·심법 + 경지 보상. 늘면 그만큼 바로 찬다(Dash.SetMaxCharges)
                if (pc.Dash != null) pc.Dash.SetMaxCharges(_life.DashMaxCharges(st));
            }
            // C-07a 경지 보상 7칸째. 지금은 무공 칸으로(공격·심법을 고르는 건 돌파 화면 C-07b 이후)
            int extraSlots = _life.RealmRewards.ExtraSlots;
            while (_extraSlotsGiven < extraSlots) { _life.Loadout.AddExtraSlot(true); _extraSlotsGiven++; }
            if (orbs != null) orbs.MagnetMeters = st.Magnet * ie.MagnetMultiplier; // C-07b 천리추종
            if (weapons == null) weapons = FindFirstObjectByType<WeaponRunner>();
            if (weapons != null)
            {
                // R-04Ua 태극일 때 쾌속에 조화 보너스(무당 0.2, 나머지 0.1)를 더하고 상한으로 자른다
                weapons.cooldownReduction = Mathf.Min(_data.Balance.Stats.CooldownReductionCap, st.CooldownReduction + _life.HarmonyCooldownReduction);
                weapons.areaMultiplier = st.Area;
                weapons.extraAmount = st.Amount;
                weapons.durationMultiplier = st.Duration;
                weapons.projectileSpeedMultiplier = st.ProjectileSpeed; // ST-02U
                weapons.explosionAreaMultiplier = 1f + st.Bonus[BloodRain.Core.Build.StatKind.ExplosionArea];
                weapons.insightDamage ??= ie.WeaponDamageMultiplier; // C-07b 일검파만
                weapons.insightRange ??= ie.WeaponRangeMultiplier;   // C-07b 이기어검
            }
            if (enemies != null)
            {
                // C-06c 화산 3 꽃잎이 나올 치명타(화산 몸 +10% 포함), 당가 3 독 번짐의 지속 피해 배율,
                // R-04Ua (5) 당가 몸이면 중독된 적에 +20%
                enemies.critChance = st.Bonus[BloodRain.Core.Build.StatKind.CritChance];
                enemies.dotMultiplier = 1f + st.Bonus[BloodRain.Core.Build.StatKind.DotDamage];
                enemies.poisonedTargetMultiplier = _life.Body.SectTrait.DamageMultiplier(true);
                enemies.sectTrait = _life.Body.SectTrait; // R-04Ub 당가 독 면역·곤륜 설한기
                enemies.passivePoisonImmune = _life.PassiveMax.PoisonImmune; // PM-01U 만독심법 Lv5
                enemies.eliteDamageMultiplier = 1f + st.Bonus[BloodRain.Core.Build.StatKind.EliteDamage]; // ST-02U 파산공
                enemies.knockbackMultiplier = 1f + st.Bonus[BloodRain.Core.Build.StatKind.Knockback];
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
            // WP-42U 재기: 남았으면 그 비율 x 최대 기혈로 그 자리에서 일어난다(맞은 뒤 무적, 금빛 고리 한 번). 없으면 정산.
            if (vitals != null)
            {
                if (Stats == null) ApplyStats();
                var rise = Stats != null ? _life.TryRevive(Stats) : null;
                if (rise.HasValue)
                {
                    vitals.Revive(rise.Value * vitals.MaxHealth);
                    Debug.Log($"[LifeRunner] 재기 {_life.RevivalsUsed}/{Stats.Revival} → 기혈 {vitals.Health:0}");
                    StartCoroutine(ReviveRing());
                    return;
                }
            }
            EndLife(LifeEndWay.Fallen);
        }

        /// <summary>시험 메뉴 '재기 +1(플레이 중)' 이 더하는 재기 횟수(연무장 재기를 안 사도 보려고).</summary>
        [System.NonSerialized] public int debugRevivals;
        static Sprite _ringSprite;

        // 재기 금빛 고리: 몸에서 0.6초(언스케일) 동안 커지며 옅어진다
        System.Collections.IEnumerator ReviveRing()
        {
            if (player == null) yield break;
            if (_ringSprite == null)
            {
                const int N = 128;
                var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { name = "재기 고리", wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[N * N];
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float d = new Vector2(x + 0.5f - N / 2f, y + 0.5f - N / 2f).magnitude / (N / 2f);
                        px[y * N + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(1f - Mathf.Abs(d - 0.85f) / 0.12f) * 255));
                    }
                tex.SetPixels32(px);
                tex.Apply();
                _ringSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
            }
            var go = new GameObject("재기 고리");
            go.transform.SetParent(player, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _ringSprite;
            sr.sortingOrder = 5;
            float baseSize = WorldScale.Current != null ? WorldScale.Current.Length(1f) : 1f;
            for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.6f;
                sr.color = new Color(0.95f, 0.78f, 0.3f, 1f - k);
                go.transform.localScale = Vector3.one * baseSize * Mathf.Lerp(1f, 4f, k);
                yield return null;
            }
            Destroy(go);
        }

        /// <summary>한 생을 끝내고 혼에 옮겨 저장한 뒤 정산을 연다. 정산이 끝나면 객잔(Inn) 씬.</summary>
        public void EndLife(string way)
        {
            // 은자는 한 생의 지갑(Life.Silver)으로 정산한다(C-06a). 행낭 카드가 GainSilver 로 넣는다
            var soul = RebirthHandoff.Soul;
            SfxPlayer.Instance?.Reset(); // SFX-14U
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
            // DL-05b 정산 자막 한 줄(그림자가 쫓아오던 중에 쓰러졌으면 shadow 줄)
            var facts = new BloodRain.Core.Meta.ReturnFacts
            {
                FallenToShadow = way == LifeEndWay.Fallen && _life.ShadowStep > 0,
                LifeSeconds = stats.SurvivalSeconds,
            };
            var caption = BloodRain.Core.Meta.InnDialogue.Settlement(_data, soul, record, summary, facts, new System.Random());
            settlement.SetCaption(SettlementScreen.CaptionText(_data, caption));
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
            TickInsights(dt);
            TickDissolution(dt);
            // R-15U 태극무량 동안 몸 둘레 적 탄 되돌림(받는 피해 0 은 DamageTakenMultiplier 가 이미 함)
            if (enemies != null) { var ul = _life.Ultimate; enemies.reflectRadius = ul != null && ul.ReflectsProjectiles ? ul.GuardRadius(WorldScale.Current != null ? WorldScale.Current.Length(1f) : 1f) : 0f; }
            _statTimer -= Time.unscaledDeltaTime;
            if (_statTimer <= 0f) { _statTimer = 0.25f; ApplyStats(); }
            // C-08b 역류 중 걸음 x0.5(매 프레임, 능력치 걸음에 곱함)
            if (_pc != null && Stats != null) _pc.MoveMultiplier = Stats.MoveSpeed * _life.Backlash.MoveSpeedMultiplier;
            // 비전 '역천'(세 번 연달아 끊기)을 얻으면 그 생 동안 역류가 다시 오지 않는다
            if (!_life.Backlash.Immune && _secrets != null && _secrets.Found.Any(s => s.Id == "yeokcheon")) _life.Backlash.Immune = true;
            if (breakthrough == null || !breakthrough.IsPlaying) SyncScale();
        }

        // ── R4-10U 천마해체(마교 몸 오의) ──
        // life.UseUltimate() 가 켠 Dissolution 이 켜져 있는 동안: 내는 피해 x DamageMultiplier(EnemyField.outgoingMultiplier),
        // 기혈 HealthCostPerSecond(최대 기혈) x 세상 dt 소모, 맞힌 피해만큼 UltimateEffects.LifestealHeal(능력치 흡혈 + LifestealBonus, 마도 진전 배율).
        // 마교 '회복 -50%' 는 vitals.Heal 이 healingMultiplier 로 곱하니 여기선 1 을 넘긴다. 몸에 붉은 기운(남은 초만큼 짙음).
        float _dealtSeen;
        SpriteRenderer _demonAura;
        /// <summary>시험용: 천마해체 흡혈로 찬 기혈 합.</summary>
        public float DissolutionHealed { get; private set; }

        void TickDissolution(float dt)
        {
            var dis = _life.Dissolution;
            bool on = dis != null && dis.IsActive;
            if (enemies != null)
            {
                enemies.outgoingMultiplier = dis != null ? dis.DamageMultiplier : 1f;
                float dealt = enemies.Dealt - _dealtSeen;
                _dealtSeen = enemies.Dealt;
                if (on && vitals != null && dealt > 0f)
                {
                    float ls = (Stats != null ? Stats.Bonus[BloodRain.Core.Build.StatKind.Lifesteal] : 0f) + dis.LifestealBonus;
                    DissolutionHealed += vitals.Heal(BloodRain.Core.Combat.UltimateEffects.LifestealHeal(dealt, ls, Effects.LifestealMultiplier, 1f));
                }
            }
            if (on && vitals != null && dt > 0f) vitals.Drain(dis.HealthCostPerSecond(vitals.MaxHealth) * dt);

            if (!on) { if (_demonAura != null && _demonAura.enabled) _demonAura.enabled = false; return; }
            if (_demonAura == null && player != null)
            {
                const int N = 64;
                var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { name = "천마해체 기운", wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[N * N];
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float d = new Vector2(x + 0.5f - N / 2f, y + 0.5f - N / 2f).magnitude / (N / 2f);
                        px[y * N + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d) * 255));
                    }
                tex.SetPixels32(px);
                tex.Apply();
                var go = new GameObject("천마해체 기운");
                go.transform.SetParent(player, false);
                _demonAura = go.AddComponent<SpriteRenderer>();
                _demonAura.sprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
                _demonAura.sortingOrder = 4;
            }
            if (_demonAura == null) return;
            _demonAura.enabled = true;
            float left = Mathf.Clamp01(dis.SecondsLeft / 8f);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 9f);
            _demonAura.color = new Color(0.75f, 0.05f, 0.05f, (0.35f + 0.25f * pulse) * (0.4f + 0.6f * left));
            float size = (WorldScale.Current != null ? WorldScale.Current.Length(2.4f) : 2.4f) * (1f + 0.08f * pulse);
            _demonAura.transform.localScale = Vector3.one * size;
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

        // EN-03/EN-02U 동시 적 수 상한(11장 '성능 상한' PC 600·모바일 400). 보통 적·사건 무리만 막고
        // 정예·금두꺼비·보부상 같은 중요한 것은 넘쳐도 세운다(important).
        bool Room() => _data.Balance.Limits.CanSpawn(enemies.Count, Application.isMobilePlatform);

        /// <summary>EN-02U 정예 자리: 화면 대각선 × spawnTarget.eliteDiagonal(0.62) 원 위 — 화면 가장자리 바로 바깥(웹 spawnElite).</summary>
        Vector2 ElitePos()
        {
            float diag = WorldScale.Current != null ? WorldScale.Current.ViewDiagonal : 30f;
            float ang = (float)(_spawnRng.NextDouble() * Mathf.PI * 2);
            return (Vector2)player.position + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * diag * _data.Balance.SpawnTarget.EliteDiagonal;
        }

        void SpawnSecond(LifeSecond s)
        {
            if (enemies == null || player == null) return;
            if (!s.Held)
                foreach (var sp in s.Spawns)
                {
                    if (!Room()) break;
                    enemies.SpawnAt(_data.Enemy(sp.Enemy), RingPos());
                }
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
                        if (!Room()) break;
                        enemies.SpawnAt(enemy, p - ax * diag * 0.6f + along * (i - n / 2f) * step);
                    }
                    break;
                }
                case WaveEventKind.Ring:
                {
                    var enemy = _data.Enemy(string.IsNullOrEmpty(e.Enemy) ? "donggap" : e.Enemy);
                    for (int i = 0; i < n; i++)
                    {
                        if (!Room()) break;
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
                    for (int i = 0; i < n && Room(); i++)
                        enemies.SpawnAt(enemy, at + Random.insideUnitCircle * spread);
                    break;
                }
                case WaveEventKind.Elite:
                    enemies.SpawnAt(_data.Enemy(e.Enemy), ElitePos());
                    break;
                case WaveEventKind.Toad:
                    enemies.SpawnAt(_data.Enemy(e.Enemy), RingPos(_data.Balance.SpawnTarget.ToadRingScale));
                    break;
                case WaveEventKind.Merchant:
                    OpenMerchant();
                    break;
                case WaveEventKind.SilverLantern:
                    // QA-02U 혈월 천하에서만 놓는다(1회차면 LanternLayer 가 놓지 않음)
                    if (LanternLayer.Instance == null || !LanternLayer.Instance.PlaceSilver()) Debug.Log("[LifeRunner] 은빛 등롱 사건 — 의서가 안 나오는 생이라 놓지 않음");
                    break;
                default:
                    Debug.Log($"[LifeRunner] 사건 {def.Kind} {e.Enemy} ({e.Wave.At:0.0}분) — 아직 세우지 않음");
                    break;
            }
        }

        // ---- C-07b 깨달음 ----
        // 깨달음을 주는 경지(보상 insight_choice)를 돌파하면 후보 수를 줄에 넣고, 돌파 줌이 끝나면 InsightScreen 을 연다.
        // 후보 수 = Insights.ChoiceCount(몸) + 올라 본 경지 덤(BreakthroughInfo.ExtraInsightChoices). 역근은 혼이 연 뒤에만(Soul.IsUnlocked).
        // 마교 몸은 Insights.RollWithDemonic 로 DemonicInsightsPerBreakthrough 자리를 마공으로.
        readonly Queue<int> _insightQueue = new Queue<int>();
        InsightScreen _insightScreen;
        Insights _insights;
        readonly System.Random _insightRng = new System.Random();
        bool _moving;
        Vector3 _lastFramePos;

        /// <summary>깨달음 화면이 열려 있거나 열릴 차례면 참. 레벨업 화면은 이 동안 기다린다.</summary>
        public bool InsightBusy => _insightQueue.Count > 0 || (_insightScreen != null && _insightScreen.IsOpen);

        /// <summary>WP-39U 은거기인: 깨달음 고르기를 한 번 더(지금 몸의 후보 수로).</summary>
        public void QueueExtraInsight()
        {
            _insights ??= Insights.From(_data);
            _insightQueue.Enqueue(_insights.ChoiceCount(_life.Body.Character));
        }

        void QueueInsights(IReadOnlyList<BreakthroughInfo> list)
        {
            _insights ??= Insights.From(_data);
            foreach (var b in list)
            {
                var realm = _life.Scale.Realms.FirstOrDefault(x => x.Id == b.Realm);
                if (realm == null || !_insights.OffersAt(realm)) continue;
                _insightQueue.Enqueue(_insights.ChoiceCount(_life.Body.Character) + b.ExtraInsightChoices);
            }
        }

        void TickInsights(float dt)
        {
            // 운기조식 회복·금종조 대기. 움직였나는 프레임마다
            if (player != null) { _moving = (player.position - _lastFramePos).sqrMagnitude > 1e-6f; _lastFramePos = player.position; }
            if (vitals != null && dt > 0f)
            {
                float heal = _life.InsightEffects.Tick(dt, _moving, vitals.MaxHealth);
                if (heal > 0f) vitals.Heal(heal);
            }
            if (_insightQueue.Count == 0 || (breakthrough != null && breakthrough.IsPlaying)) return;
            if (_insightScreen == null)
            {
                _insightScreen = FindFirstObjectByType<InsightScreen>();
                if (_insightScreen == null)
                {
                    _insightScreen = new GameObject("깨달음 화면").AddComponent<InsightScreen>();
                    var lu = FindFirstObjectByType<LevelUpScreen>();
                    if (lu != null) _insightScreen.font = lu.font;
                }
            }
            if (_insightScreen.IsOpen) return;
            int count = _insightQueue.Dequeue();
            var soul = RebirthHandoff.Soul;
            System.Func<string, bool> unlocked = soul != null ? soul.IsUnlocked : (System.Func<string, bool>)null;
            var taken = _life.InsightEffects.Taken.Select(i => i.Id);
            var art = _life.Body.Sect?.BloodArt;
            var cards = art != null
                ? Insights.RollWithDemonic(_insights, _data.DemonicInsights, taken, count, art.DemonicInsightsPerBreakthrough, _insightRng, unlocked)
                : _insights.Roll(taken, count, _insightRng, unlocked);
            if (cards.Count == 0) return;
            var demonic = new HashSet<string>(_data.DemonicInsights.Select(d => d.Id));
            _insightScreen.Open(cards, d => demonic.Contains(d.Id), pick =>
            {
                _life.TakeInsight(pick, (int)System.Math.Min(_kills, int.MaxValue));
                Debug.Log($"[LifeRunner] 깨달음 {pick.Id}({pick.Effect})");
                ApplyStats();
            });
        }

        // ---- RM-05U 보부상 ----
        MerchantScreen _merchant;

        /// <summary>보부상 창을 연다(생성 디렉터의 보부상 사건, 시험 메뉴 '지금 보부상 부르기').</summary>
        public void OpenMerchant()
        {
            if (_life == null || _life.Record != null) return;
            if (_merchant == null) _merchant = FindFirstObjectByType<MerchantScreen>();
            if (_merchant == null)
            {
                _merchant = new GameObject("보부상 화면").AddComponent<MerchantScreen>();
                var lu = FindFirstObjectByType<LevelUpScreen>();
                if (lu != null) _merchant.font = lu.font;
            }
            _merchant.Open(_life, _data, vitals, orbs);
        }

        // 화면 축척은 레벨 + 진기 비율(경지 안에서 조금씩 넓어진다). 돌파 중엔 BreakthroughFx 가 맡는다.
        void SyncScale()
        {
            var ws = WorldScale.Current;
            if (ws == null || _life == null) return;
            ws.SetLevel(LevelFloat);
        }

        float LevelFloat => _life.Level + (float)System.Math.Min(_life.Xp.Progress, 0.999);

        /// <summary>WP-34U 시험 메뉴용: 금두꺼비 하나를 이벤트와 같은 고리 자리에 부른다.</summary>
        public void SpawnGoldenToadNear()
        {
            if (enemies == null) return;
            enemies.SpawnAt(_data.Enemy("golden_toad"), RingPos(_data.Balance.SpawnTarget.ToadRingScale));
        }

        /// <summary>WP-39U 영물: 금두꺼비처럼 이벤트 고리 자리에 부른다(웹 spawnSpiritBeast). 적 번호를 돌려준다(없으면 -1).</summary>
        public int SpawnSpiritBeast()
        {
            if (enemies == null) return -1;
            return enemies.SpawnAt(_data.Enemy("golden_toad"), RingPos(_data.Balance.SpawnTarget.ToadRingScale));
        }

        /// <summary>시험 메뉴 '다음 경지까지 진기'. 한 레벨씩 구슬을 먹은 것처럼 넣어 경지가 바뀌면 멈춘다.</summary>
        public void GiveXpToNextRealm()
        {
            if (_life == null || _life.Record != null) return;
            int realm = _life.Realm;
            for (int i = 0; i < 200 && _life.Realm == realm; i++)
                OnAbsorbed(System.Math.Max(1.0, _life.Xp.Required - _life.Xp.Xp + 0.5), 1);
        }

        void OnAbsorbed(double xp, int count)
        {
            if (_life == null || _life.Record != null) return;
            float from = LevelFloat;
            var r = _life.Absorb(xp, count);
            if (_edges != null) _edges.Woke(r.WokenBosses); // QA-05U 혈운 기둥
            if (r.Breakthroughs.Count == 0) return;
            QueueInsights(r.Breakthroughs);
            ApplyStats(); // C-07a 돌파 보상(최대 기혈·경공 칸·받는 피해·7칸째)을 바로
            var ws = WorldScale.Current;
            if (breakthrough == null || ws == null || player == null) return;
            bool revisit = r.Breakthroughs.All(i => !i.LongCinematic);
            breakthrough.Play(ws, from, LevelFloat, player, revisit);
        }

        void OnGUI()
        {
            DrawPopups();
            if (!showDebug || _life == null) return;
            var realm = _life.Scale.Realms[_life.Realm];
            var region = _life.Regions.Region(_life.RegionId);
            GUI.color = Color.white;
            GUI.Label(new Rect(12, 96, 1300, 22),
                $"한 생  {_life.Minutes:0.0}분  레벨 {_life.Level} ({_life.Xp.Progress * 100:0}%)  경지 {realm.NameKo}  지역 {region.NameKo}  혈운 {_life.Density:0.00}" +
                (_life.IsPrologue ? "  [서장]" : "") +
                // R-04Ua 몸과 고유 능력
                $"  몸 {_life.Body.Character.NameKo}  회복 배율 x{_life.HealingMultiplier:0.0#}  태극 쾌속 +{_life.HarmonyCooldownReduction * 100:0}%");
            // EN-02U 새로 나온 적 한 칸(기혈·닿는 피해가 분·혈운으로 세지는지 보는 용)
            if (enemies != null)
                GUI.Label(new Rect(12, 162, 1100, 22), $"적 {enemies.Count}/{_data.Balance.Limits.EnemyCap(Application.isMobilePlatform)}  새로 나온 적 {enemies.LastSpawnInfo}");
            if (Stats != null)
            {
                var st = Stats;
                GUI.Label(new Rect(12, 118, 1100, 22),
                    $"능력치  최대 기혈 {st.MaxHealth:0}  호신 {st.Armor:0.#}  회복 {st.Recovery:0.#}  걸음 x{st.MoveSpeed:0.00}  공력 x{st.Might:0.00}  쾌속 {st.CooldownReduction * 100:0}%  범위 x{st.Area:0.00}  개수 +{st.Amount}  지속 x{st.Duration:0.00}  흡기 {st.Magnet:0.0}m  회피 {st.Evade * 100:0}%" +
                    $"  경공 칸 {_life.DashMaxCharges(st)}  받는 피해 x{_life.DamageTakenMultiplier:0.00}  무공 칸 {_life.Loadout.WeaponSlots}");
                // C-06b 곱하는 진전 효과
                var fx = Effects;
                var sword = fx.Tier("sword_damage");
                GUI.Label(new Rect(12, 140, 1100, 22),
                    $"진전 배율  검 피해 x{(sword != null ? 1f + sword.Param("damageBonus") : 1f):0.00}  낮은 기혈 {(fx.Has("low_health_might") ? "켜짐" : "-")}  강시 피해 {(fx.Has("jiangshi_damage") ? "켜짐" : "-")}  경공 거리 x{fx.DashDistanceMultiplier:0.00}  태극 폭 ±{_life.Loadout.Gauge.HarmonyRange}  사량발천근 {(fx.Has("harmony_deflect") ? "켜짐" : "-")}  정신부 x{fx.FreezeMultiplier:0.00}  흡혈 x{fx.LifestealMultiplier:0.00}");
            }
        }
    }
}
