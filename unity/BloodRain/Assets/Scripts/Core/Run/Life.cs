// 한 생을 묶는 입구(기획서 7장 '한 생의 흐름', 21장 환혼과 윤회).
// 환생으로 몸을 받으면 Life 를 만들고, 매 프레임 Tick(실제 시간, 플레이어 위치, 화면 위 적 수)을 부른다.
// Life 가 하는 일: 세상 시간(돌파·각성 연출 중엔 느려짐), 서 있는 지역과 처음 밟은 땅, 진기 흡수 → 레벨·돌파(축척) → 땅의 주인 깨어남,
// 땅의 주인 싸움 시작·처치, 매초 적 생성(LifeDirector), 기혈역류와 오의 게이지(Charge), 생의 끝(쓰러짐·좌화·봉인)과 정산 기록.
// 전투(맞히기·피해)는 Unity 쪽 Sim 이 하고 결과만 여기로 알려 준다(Absorb, DefeatBoss, End).
// 서장 몸(Rebirth.Prologue)이면 서장 10분이 된다(기획서 4장 '서장 상세 흐름', 21장): 생성은 PrologueDirector, 차례는 Prologue.
//   장삼 전에 쓰러지면 PlayerDown 이 Restart 를 주고 RestartPrologue 로 새 Life 를 만든다(혼에 아무것도 안 남는다, 생 횟수도 안 셈).
//   장삼전에서 바닥나면 천지인 삼검 고정 각성이 저절로 시작되고, 장삼을 쓰러뜨리면 KillPrologueBoss 가 정산 기록을 만든다.
//   그 기록을 Soul.EndLife 에 넘기면 PrologueDone 이 되고, 컷(Prologue.NextCut) 뒤 Soul.OpenRebirth 가 첫 환혼 셋을 준다.
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BloodRain.Core.Build;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Progression;
using BloodRain.Core.Spawning;
using BloodRain.Core.World;

namespace BloodRain.Core.Run
{
    public enum LifePhase
    {
        /// <summary>보통 싸움.</summary>
        Roaming,
        /// <summary>땅의 주인과 싸우는 중.</summary>
        BossFight,
        /// <summary>생이 끝났다(정산 기록이 있다).</summary>
        Ended,
    }

    /// <summary>생이 끝나는 세 가지(cheonha.json lifeEnd.ways).</summary>
    public static class LifeEndWay
    {
        public const string Fallen = "fallen";
        public const string Zuohwa = "zuohwa";
        public const string Sealed = "sealed";
    }

    /// <summary>진기를 먹은 결과. 돌파가 있었으면 경지마다 연출 정보가 붙는다.</summary>
    public sealed class LifeAbsorb
    {
        public AbsorbResult Xp { get; }
        public IReadOnlyList<BreakthroughInfo> Breakthroughs { get; }
        /// <summary>이 돌파로 새로 깨어난 땅의 주인 지역.</summary>
        public IReadOnlyList<RegionDef> WokenBosses { get; }

        public LifeAbsorb(AbsorbResult xp, IReadOnlyList<BreakthroughInfo> breakthroughs, IReadOnlyList<RegionDef> woken)
        {
            Xp = xp; Breakthroughs = breakthroughs; WokenBosses = woken;
        }
    }

    public sealed class Life
    {
        readonly GameData _data;
        readonly Soul _soul;
        // 이번 생이 보는 땅의 주인 기록. 이번 회차에 봉인석을 가져온 땅이면 혈영으로 일어난다(LifeLog.KillBoss 와 같은 기준).
        // 생 도중 처치로 봉인석이 늘면 여기에 반영해 봉마곡이 그 생에서 열린다.
        readonly BossRecord _bossRecord;
        double _secondAcc;
        // 의상 해금(cheonha.json costumes boss_in_harmony): 지금 땅의 주인 싸움 동안 음양이 태극 구간을 지켰나
        bool _fightWas, _harmonyKept;

        public Life(Soul soul, RebirthBody body, Random rng) : this(soul, body, rng, 0) { }

        Life(Soul soul, RebirthBody body, Random rng, int prologueRestarts)
        {
            _soul = soul;
            _data = soul.Data;
            Body = body;
            Log = soul.BeginLife(body);
            Loadout = body.NewLoadout(_data);
            Xp = new RunXp(new XpCurve(_data.Balance.XpCurve), new Realms(_data.Balance.Realms));
            Regions = new Regions(_data);
            Scale = new Scale(_data);
            Breakthrough = new Breakthrough(Scale, _data.Cheonha.Breakthrough, _data.Cheonha.Crush);
            Awakening = new Awakening(_data.Balance.Awakening);
            Charge = new UltimateCharge(_data.Balance.UltimateCharge, _data.Balance.Yinyang.Harmony, StartChargeBonus(soul, body))
            {
                HarmonyBonusMultiplier = body.SectTrait.HarmonyBonusMultiplier, // 무당 '조화 보너스 2배'
            };
            Charges = LevelUpCharges.Start(_data.Balance.LevelUp, body.Character, StartBonus(soul, body));
            // 역류를 경공으로 끊으면 오의 게이지 +50 이 이 게이지로 들어간다.
            Backlash = new Backlash(_data.Balance.Yinyang.Extreme, Charge);
            Director = new LifeDirector(_data, rng);
            _bossRecord = new BossRecord(soul.SealStones, soul.SealStoneCount);
            Bosses = new BossAwakening(_data, body.Region.Id, _bossRecord);
            if (body.Sect?.BloodArt is { } art) BloodArt = new BloodArt(art, Loadout.Gauge, body.YinyangStart);
            InsightEffects = new InsightEffects(body.Character.StartWeapon);
            Encounters = new EncounterBoons(_data);
            if (body.Character.Ultimate == UltimateEffects.CheonmaHaeche)
                Dissolution = new DemonDissolution(Ultimates.Find(_data, UltimateEffects.CheonmaHaeche));
            RegionId = body.Region.Id;
            Position = Regions.Center(body.Region);
            if (body.IsPrologue)
            {
                Prologue = new Prologue(_data, prologueRestarts);
                PrologueSpawns = new PrologueDirector(_data, rng);
            }
        }

        /// <summary>서장 차례(서장 몸일 때만). 장삼 등장은 Tick 이 알아서 알린다.</summary>
        public Prologue? Prologue { get; }
        /// <summary>서장 생성(서장 몸일 때만). 서장이면 Tick 이 Director 대신 이걸 돌린다.</summary>
        public PrologueDirector? PrologueSpawns { get; }
        public bool IsPrologue => Prologue != null;

        public RebirthBody Body { get; }
        public LifeLog Log { get; }
        public Loadout Loadout { get; }
        public RunXp Xp { get; }
        public Regions Regions { get; }
        public Scale Scale { get; }
        public Breakthrough Breakthrough { get; }
        public Awakening Awakening { get; }
        public Backlash Backlash { get; }
        /// <summary>진기(오의) 게이지. 처치·피격은 Unity 쪽 Sim 이 Charge.OnKill(Loadout.Gauge.Band) 처럼 알린다.
        /// 시작 충전 보너스는 연무장 + 몸의 성질. 심법을 올리면 부른 쪽이 ChargeBonus 를 다시 넣는다.</summary>
        public UltimateCharge Charge { get; }
        /// <summary>레벨업 화면의 다시 고르기·파기·정진 남은 횟수(연무장 + 몸의 성질 + 캐릭터 특성).</summary>
        public LevelUpCharges Charges { get; }
        public LifeDirector Director { get; }
        public BossAwakening Bosses { get; }
        /// <summary>마교 몸의 혈공(5장). 다른 몸은 null.</summary>
        public BloodArt? BloodArt { get; }
        /// <summary>천마해체 8초(마교 몸만, 다른 몸은 null). UseUltimate 가 시작하고 Tick 이 줄인다(UltimateEffects).</summary>
        public DemonDissolution? Dissolution { get; }
        /// <summary>고른 깨달음이 붙이는 값. 고르면 TakeInsight(def, 지금 처치 수).</summary>
        public InsightEffects InsightEffects { get; }

        /// <summary>깨달음 하나를 얻는다. 살기충천·심마가 고른 때부터 세도록 지금 처치 수와 심마 횟수를 함께 넘긴다.</summary>
        public void TakeInsight(InsightDef insight, int killsNow = 0) =>
            InsightEffects.Take(insight, Loadout.Gauge, killsNow, BloodArt?.Steps ?? 0);

        /// <summary>이번 생에 만난 기연이 준 것(진전 덧셈·검총 공력·속삭임 회복 배율). 20장 '기연 사건 8'.
        /// 검총 공력은 CurrentStats 가 저절로 더하고, 진전 덧셈은 Body.ComputeSynergy(…, Encounters.SchoolExtra) 로 넘긴다.</summary>
        public EncounterBoons Encounters { get; }

        /// <summary>몸이 받는 회복 전부에 곱하는 수: 문파 고유 능력(마교 0.5) × 혈마의 속삭임(받았으면 0.5).
        /// 흡혈대법 같은 깨달음 배율(InsightEffects.OtherHealMultiplier)은 Presentation 이 따로 곱한다.</summary>
        public float HealingMultiplier => Body.SectTrait.HealingMultiplier * Encounters.HealingMultiplier;

        /// <summary>하오문 밀서를 주웠다. 도감에서 못 찾은 비전 하나(데이터 순서, 밀서로 안 본 것)를 돌려준다. 다 봤으면 null.</summary>
        public SecretDef? ReadLetter(IEnumerable<string> discoveredSecrets)
        {
            Encounters.NoteLetter();
            return Log.ReadLetter(discoveredSecrets);
        }

        /// <summary>혈마의 속삭임을 받았다(석상 앞에서 오의). 마도 진전 +1·회복 절반, 일지에 계승 표식. 두 번째는 false.</summary>
        public bool AcceptWhisper()
        {
            if (!Encounters.AcceptWhisper()) return false;
            Log.Whispered = true;
            return true;
        }

        /// <summary>영약을 주웠다: 오의 가득, 음양 0.</summary>
        public void TakeElixir() => Encounters.TakeElixir(Charge, Loadout.Gauge);

        /// <summary>영물을 잡았다: 영약 효과와 혈정. 늘어난 혈정을 돌려준다.</summary>
        public int CatchSpiritBeast() => Encounters.CatchSpiritBeast(Charge, Loadout.Gauge, Log);

        /// <summary>깨달음 능력치 덧셈. 심마 횟수는 혈공에서 가져온다. CurrentStats(extra) 에 넣는다.</summary>
        public StatBonus InsightBonus(int kills, float healthRatio = 1f) =>
            InsightEffects.Bonus(kills, BloodArt?.Steps ?? 0, healthRatio);

        /// <summary>세상 시간으로 흐른 분.</summary>
        public float Minutes { get; private set; }
        public int Level => Xp.Level;
        public int Realm => Scale.RealmIndex(Xp.Level);
        /// <summary>지금까지 돌파한 경지들의 보상 합(경지표 '돌파 보상'). 효과를 얹는 일은 부른 쪽이 한다.</summary>
        public RealmRewardSum RealmRewards => Progression.RealmRewards.Upto(_data.Balance.Realms, Xp.Level);

        /// <summary>지금 몸의 능력치: 몸 + 지금 심법 레벨 + 연무장 + 성질 + 문파 고유 능력 + 경지 돌파 최대 기혈(이류 +10).
        /// 깨달음·진전 같은 덧셈은 extra 로. 심법을 올리거나 경지를 넘으면 다시 부른다(값을 들고 있지 않는다).
        /// healthRatio·moving 은 조건이 붙은 고유 능력(남궁 기혈 50% 이상 공력 +25%, 수로채 움직이는 동안 회피 15%)에 쓴다.</summary>
        public StatSheet CurrentStats(StatBonus? extra = null, float healthRatio = 1f, bool moving = false)
        {
            var b = Encounters.StatBonus();
            if (extra != null) b.Add(extra);
            return Body.ComputeStats(_data, Loadout.Passives, _soul.Training, b, RealmRewards.MaxHealthFlat, healthRatio, moving);
        }

        /// <summary>태극(조화) 구간이면 붙는 쾌속(기본 0.1, 무당 몸 0.2). 태극이 아니면 0. StatSheet.CooldownReduction 에 더하고 상한으로 자른다.</summary>
        public float HarmonyCooldownReduction =>
            Loadout.Gauge.Band == YinYangBand.Harmony ? Body.SectTrait.HarmonyCooldownReduction(_data.Balance.Yinyang.Harmony) : 0f;

        /// <summary>경공 최대 충전: balance.json dash 기본 + 연무장·성질 + 경지 돌파(일류 +1). 돌파 뒤 Dash.SetMaxCharges 에 넣는다.</summary>
        public int DashMaxCharges(StatSheet stats) => _data.Balance.Dash.Charges + stats.DashCharges + RealmRewards.DashCharges;

        /// <summary>오의 위력 배율(절정 돌파면 1.25).</summary>
        public float UltimatePowerMultiplier => 1f + RealmRewards.UltimatePower;

        /// <summary>받는 피해 배율(화경 돌파면 0.9, 태극무량이 붙어 있으면 0). Damage.Taken(…, 호신) 뒤에 곱한다.</summary>
        public float DamageTakenMultiplier => RealmRewards.DamageTakenMultiplier * (Ultimate?.DamageTakenMultiplier ?? 1f);

        /// <summary>지금 흐르는 오의(쓴 뒤 지속 시간·때릴 박자가 남은 동안). 없으면 null. 박자·모양·적에 붙는 것은 여기서 읽는다(천마해체 몸 효과는 Dissolution).</summary>
        public UltimateCast? Ultimate { get; private set; }

        /// <summary>이번 Tick 에 오의가 때릴 박자 수. 한 박자마다 Ultimates.HitDamage 를 UltimateCasts 번(쓴 직후 첫 Tick 에 1, 만천화우는 1초마다).</summary>
        public int UltimatePulses { get; private set; }

        /// <summary>오의 한 번에 몇 번 나가나. 현경이면 2(오의 2연속). 게이지는 한 번만 비운다.</summary>
        public int UltimateCasts => RealmRewards.DoubleUltimate ? 2 : 1;
        public Vector2 Position { get; private set; }
        /// <summary>지금 자리의 혈운 농도. 적 기혈·피해는 EnemyScaling.HealthAt·DamageAt 에 이 값을 넘긴다.</summary>
        public float Density => Regions.Density(Position, SealStones);
        /// <summary>지금 가진 봉인석 수(생이 시작될 때 혼의 수 + 이번 생에 새로 얻은 것).</summary>
        public int SealStones => _bossRecord.SealStones;
        /// <summary>서 있는 지역(가장 가까운 지역 중심).</summary>
        public string RegionId { get; private set; }
        public LifePhase Phase => Record != null ? LifePhase.Ended : Bosses.InFight ? LifePhase.BossFight : LifePhase.Roaming;
        /// <summary>끝났으면 정산 기록(Soul.EndLife 에 넘긴다).</summary>
        public LifeRecord? Record { get; private set; }
        /// <summary>혈마의 그림자 단계(0이면 없음).</summary>
        public int ShadowStep => Director.ShadowStep(Minutes, Level, SealStones);
        /// <summary>지금 혈마의 그림자 세기(단계 0이면 Active 가 false).</summary>
        public BloodShadowStrength Shadow => BloodShadow.Strength(_data.Cheonha.LifeEnd.Shadow, ShadowStep);
        /// <summary>혈월 천하(회귀, 21장)면 적 기혈 배율(cheonha.json cycle.bloodmoon.enemyHealth), 아니면 1. EnemyScaling.HealthAt 결과에 곱한다.</summary>
        public float EnemyHealthMultiplier => _soul.IsBloodMoon ? _data.Cheonha.Cycle.Bloodmoon.EnemyHealth : 1f;
        /// <summary>혈월 천하면 적 이동속도 배율(cycle.bloodmoon.enemySpeed), 아니면 1. 혈마의 그림자에는 곱하지 않는다(그림자 세기는 Shadow 그대로).</summary>
        public float EnemySpeedMultiplier => _soul.IsBloodMoon ? _data.Cheonha.Cycle.Bloodmoon.EnemySpeed : 1f;
        /// <summary>지금 세상 시간 배율(돌파·각성 연출 중엔 느리다). Unity 의 Time.timeScale 에 넣는다.</summary>
        public float WorldTimeScale => Math.Min(Breakthrough.WorldTimeScale, Awakening.IsActive ? Awakening.WorldTimeScale : 1f);

        /// <summary>한 프레임. 세상 시간으로 1초가 찰 때마다 그 1초의 생성 지시를 돌려준다(아니면 null).</summary>
        public LifeSecond? Tick(float unscaledDt, Vector2 position, int aliveNow)
        {
            if (Record != null) return null;

            if (Breakthrough.IsActive) Breakthrough.Tick(unscaledDt);
            if (Awakening.IsActive)
            {
                var cues = Awakening.Tick(unscaledDt);
                if ((cues & AwakeningCue.Shock) != 0)
                {
                    Director.Hold(_data.Balance.Awakening.SpawnPauseSeconds);
                    PrologueSpawns?.Hold(_data.Balance.Awakening.SpawnPauseSeconds);
                }
                if ((cues & AwakeningCue.End) != 0) Prologue?.AwakeningEnded();
            }

            float dt = unscaledDt * WorldTimeScale;
            Minutes += dt / 60f;
            TickUltimate(dt);
            if (PrologueSpawns != null) return TickPrologue(dt, position, aliveNow);
            Director.Tick(dt);
            Awakening.TickWorld(dt);
            Dissolution?.Tick(dt);
            Backlash.Tick(dt, Loadout.Gauge.Band == YinYangBand.Extreme);

            MoveTo(position);

            Bosses.Tick(dt, Position, Level, Minutes);
            Director.BossFight = Bosses.InFight;
            NoteFightHarmony();

            _secondAcc += dt;
            if (_secondAcc < 1.0) return null;
            _secondAcc -= 1.0; // 한 프레임이 몇 초를 건너뛰면 남은 초는 다음 프레임들이 하나씩 낸다(버리지 않는다)
            return Director.Second(Minutes, Level, RegionId, aliveNow);
        }

        /// <summary>땅의 주인 싸움이 시작되면 태극을 지켰다고 두고, 싸우는 동안 한 번이라도 태극 밖이면 지움.</summary>
        void NoteFightHarmony()
        {
            bool inFight = Bosses.InFight;
            if (inFight && !_fightWas) _harmonyKept = true;
            if (inFight && Loadout.Gauge.Band != YinYangBand.Harmony) _harmonyKept = false;
            _fightWas = inFight;
        }

        void TickUltimate(float dt)
        {
            if (Ultimate != null && !Ultimate.IsActive) Ultimate = null;
            UltimatePulses = Ultimate?.Advance(dt) ?? 0;
        }

        LifeSecond? TickPrologue(float dt, Vector2 position, int aliveNow)
        {
            PrologueSpawns!.Tick(dt);
            Awakening.TickWorld(dt);
            Backlash.Tick(dt, Loadout.Gauge.Band == YinYangBand.Extreme);
            MoveTo(position);

            _secondAcc += dt;
            if (_secondAcc < 1.0) return null;
            _secondAcc -= 1.0;
            var s = PrologueSpawns.Second(Minutes, Level, aliveNow);
            foreach (var e in s.Events)
                if (e.Wave.Def.Type == WaveEventKind.Boss) Prologue!.BossAppeared(e.Enemy);
            return s;
        }

        /// <summary>이번 생에 쓴 재기 횟수.</summary>
        public int RevivalsUsed { get; private set; }

        /// <summary>한 생(서장 아님)에서 기혈이 0이 됐다. 재기(stats.Revival — 연무장·영약)가 남았으면 하나 쓰고
        /// 일어날 기혈 비율(balance.json revival.healthRatio, 8장 '기혈 50%')을 돌려준다. 남지 않았으면 null — End(fallen) 로 정산.</summary>
        public float? TryRevive(StatSheet stats)
        {
            if (IsPrologue) throw new InvalidOperationException("서장은 PlayerDown 을 씁니다");
            if (RevivalsUsed >= stats.Revival) return null;
            RevivalsUsed++;
            return _data.Balance.Revival.HealthRatio;
        }

        /// <summary>서장: 진소운의 기혈이 0이 됐다. Awaken 이면 고정 각성이 이미 시작됐고 기혈은 1로 남긴다.
        /// Restart 면 RestartPrologue 로 처음부터.</summary>
        public PrologueDown PlayerDown()
        {
            var p = Prologue ?? throw new InvalidOperationException("서장에서만 씁니다. 한 생은 End(fallen)");
            var down = p.PlayerDown();
            if (down == PrologueDown.Awaken) StartPrologueAwakening();
            return down;
        }

        /// <summary>서장: 장삼에게 피해를 준다. 각성 전엔 기혈 바닥(prologueBody.bossHealthFloor)에서 멈추고 그 순간 고정 각성을 시작한다.
        /// 돌려준 값이 새 기혈이다. 0이 되면 KillPrologueBoss 를 부른다.</summary>
        public float DamagePrologueBoss(float health, float maxHealth, float damage)
        {
            var p = Prologue ?? throw new InvalidOperationException("서장에서만 씁니다");
            float next = p.ApplyBossDamage(health, maxHealth, damage);
            if (p.Step == PrologueStep.Awakening && !Awakening.IsActive) StartPrologueAwakening();
            return next;
        }

        void StartPrologueAwakening()
        {
            // 서장 각성은 혼 기록(첫 각성 긴 연출)에 남기지 않는다. 첫 생에 맛본 절기를 내 힘으로 다시 깨우는 것이 목표라서(21장).
            if (Awakening.IsActive) return;
            Prologue!.StartAwakening(Awakening, Loadout);
        }

        /// <summary>서장: 장삼을 쓰러뜨렸다. 진소운의 몸이 다해 생이 끝난다(정산 기록). 혼에 옮기는 일은 부른 쪽이 Soul.EndLife 로.
        /// 그 뒤 Prologue.CurrentCut 컷을 NextCut 으로 넘기고, 침방에서 Soul.OpenRebirth(첫 환혼 셋) → Prologue.ChooseFirst.</summary>
        public LifeRecord KillPrologueBoss(int? leftoverSilver = null)
        {
            var p = Prologue ?? throw new InvalidOperationException("서장에서만 씁니다");
            p.BossKilled();
            return End(LifeEndWay.Fallen, leftoverSilver);
        }

        /// <summary>서장: 장삼 전에 쓰러져 처음부터. 같은 혼·같은 몸으로 새 판을 만든다. 이 판의 일지는 버린다(혼에 남는 것 없음).</summary>
        public Life RestartPrologue(Random rng)
        {
            var p = Prologue ?? throw new InvalidOperationException("서장에서만 씁니다");
            // 장삼 전에 쓰러졌거나, 장삼을 쓰러뜨리기 전 어느 때든 좌화·그만두기를 골랐을 때. 쓰러진 횟수(Restarts)는 PlayerDown 만 센다.
            if (p.FightOver) throw new InvalidOperationException($"장삼을 쓰러뜨린 뒤에는 처음부터 하지 않습니다: {p.Step}");
            return new Life(_soul, Body, rng, p.Restarts);
        }

        /// <summary>플레이어 위치를 옮긴다(Tick 이 부른다). 다른 지역에 들어서면 일지에 남긴다.</summary>
        public void MoveTo(Vector2 position)
        {
            Position = position;
            string now = Regions.Nearest(position, SealStones).Id;
            if (now == RegionId) return;
            RegionId = now;
            Log.Enter(now);
        }

        /// <summary>진기 구슬을 먹었다. 윤회의 기는 구슬을 주운 자리의 혈운 농도로 센다.
        /// 레벨에 들어가는 진기는 지금 몸의 심득(Growth: 성질·청심공·연무장)을 곱한다. 깨달음에는 심득이 없어 extra 없이 센다.</summary>
        public LifeAbsorb Absorb(double orbXp, int orbs = 1)
        {
            if (Record != null) throw new InvalidOperationException("끝난 생입니다");
            Xp.Growth = CurrentStats().Growth;
            float density = Density;
            Log.AbsorbOrb(orbXp, density, orbs); // 윤회의 기는 구슬 값 그대로(혈공으로 늘지 않음)
            var r = Xp.Absorb(BloodArt?.OrbXp(orbXp, density) ?? orbXp);
            var infos = new List<BreakthroughInfo>();
            var woken = new List<RegionDef>();
            if (r.Breakthroughs.Count > 0)
            {
                foreach (var realm in r.Breakthroughs) infos.Add(Log.Breakthrough(realm.Id));
                bool revisit = infos.All(i => !i.LongCinematic);
                // 연출 도중 또 넘었으면(큰 구슬·쓸림 진기) 돌던 연출의 끝만 새 경지로 옮긴다.
                if (Breakthrough.IsActive) Breakthrough.Extend(r.ToLevel);
                else Breakthrough.Start(r.FromLevel, r.ToLevel, revisit);
                if (PrologueSpawns != null) PrologueSpawns.OnBreakthrough();
                else Director.OnBreakthrough(revisit);
                woken = Bosses.UpdateRealm(Xp.Level);
            }
            return new LifeAbsorb(r, infos, woken);
        }

        /// <summary>절기가 깨어났다. 긴 연출 여부는 혼의 기록과 설정으로.</summary>
        public AwakenInfo Awaken(EvolutionDef evolution, AwakeningLength setting = AwakeningLength.Auto)
        {
            var info = Log.Awaken(evolution.Id);
            Loadout.Evolve(evolution);
            Awakening.Start(evolution, info.LongByDefault, setting);
            return info;
        }

        /// <summary>오의를 쓴다. 게이지가 가득 찼을 때만 비우고 true. 그 몸의 오의가 Ultimate 로 흐르기 시작한다(R-15). 마교 몸이면 천마해체로 음양 게이지가 시작값으로 돌아오고(혈공) 8초 Dissolution 이 켜진다.
        /// Unity 쪽은 Charge.TryUse 를 따로 부르지 말고 이걸 부른다.</summary>
        public bool UseUltimate()
        {
            if (Record != null) throw new InvalidOperationException("끝난 생입니다");
            if (!Charge.TryUse()) return false;
            Ultimate = new UltimateCast(Ultimates.Of(_data, Body.Character)); // 다음 Tick 에 첫 박자(UltimatePulses)
            Dissolution?.Start();
            if (BloodArt != null && BloodArt.OnUltimate(Body.Character.Ultimate))
                InsightEffects.OnDemonicReset(BloodArt.Steps); // 마공 깨달음 '심마' 공력도 사라진다
            return true;
        }

        /// <summary>싸우던 땅의 주인을 쓰러뜨렸다. 종장(봉마곡)이면 봉인으로 생이 끝난다.
        /// 보상(첫 처치·봉인석·문파·윤회의 기·혈정)은 혼 장부(LifeLog.KillBoss) 값으로 맞춘다. 혈월 천하 배율도 거기서 붙는다.
        /// 봉인석이 늘어 새로 깨어난 땅(일곱째면 봉마곡)은 kill.Woken.</summary>
        public BossKill DefeatBoss(int? leftoverSilverIfFinal = null)
        {
            NoteFightHarmony();   // 쓰러뜨리는 순간의 음양까지 본다(의상 해금)
            var kill = Bosses.Defeat();
            Director.BossFight = false;
            if (kill.IsFinal)
            {
                End(LifeEndWay.Sealed, leftoverSilverIfFinal);
                return kill;
            }
            // 10장 해금: 극성(±70 이상)으로 땅의 주인 처치 → 깨달음 '역근' 풀에 추가(QA-06)
            if (Loadout.Gauge.Band == YinYangBand.Extreme) _soul.Unlock(UnlockToken.BossAtExtreme);
            // 10장 해금: 무공 하나만으로 땅의 주인 처치 → 칭호 '일검무적'(정산 때 Soul.EndLife 가 연다)
            if (Loadout.Weapons.Count == 1) Log.SingleWeaponBoss = true;
            // 10장 해금: 태극 구간만 지키며 원각 처치 → 운화 의상(cheonha.json costumes)
            string? bossId = _data.Cheonha.Regions.FirstOrDefault(r => r.Id == kill.RegionId)?.Boss;
            foreach (var c in _data.Cheonha.Costumes)
                if (c.When == CostumeDef.BossInHarmony && c.Boss == bossId && _harmonyKept) _soul.Unlock(c.Token);
            _fightWas = false; _harmonyKept = false;
            var info = Log.KillBoss(kill.RegionId);
            kill.First = info.FirstInCycle;
            kill.SealStone = info.SealStone;
            kill.UnlockSect = info.UnlockedSect;
            kill.RebirthQi = info.RebirthQi;
            kill.BloodCrystal = info.BloodCrystal;
            _bossRecord.Apply(kill);
            kill.Woken = Bosses.Refresh();
            return kill;
        }

        /// <summary>정예를 쓰러뜨렸다. 이번에 얻은 혈정 수(정산 화면·HUD 용).</summary>
        public int KillElite() => Log.KillElite();

        static StatBonus StartBonus(Soul soul, RebirthBody body)
        {
            var b = soul.TrainingBonus();
            b.Add(body.TraitStats);
            return b;
        }

        static float StartChargeBonus(Soul soul, RebirthBody body) => StartBonus(soul, body)[StatKind.UltimateCharge];

        /// <summary>은자 지갑. 레벨업 행낭·잡몹 주머니·등롱·보갑에서 주운 은자가 GainSilver 로 들어오고 보부상(7장)에서 SpendSilver 로 나간다.
        /// 생이 끝날 때 남은 은자를 따로 주지 않으면 이 값이 정산의 '남은 은자'가 된다(10장 윤회의 기).</summary>
        public int Silver { get; private set; }

        /// <summary>은자를 주웠다. 혈월 천하면 보상 배율(LifeLog.RewardMultiplier)을 곱해 반올림한다. 실제로 들어온 양을 돌려준다.</summary>
        public int GainSilver(int amount)
        {
            if (Record != null) throw new InvalidOperationException("끝난 생입니다");
            if (amount <= 0) return 0;
            int got = (int)Math.Round(amount * (double)Log.RewardMultiplier, MidpointRounding.AwayFromZero);
            Silver += got;
            return got;
        }

        /// <summary>은자를 쓴다. 모자라면 쓰지 않고 false.</summary>
        public bool SpendSilver(int amount)
        {
            if (Record != null) throw new InvalidOperationException("끝난 생입니다");
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount > Silver) return false;
            Silver -= amount;
            return true;
        }

        /// <summary>생이 끝났다(쓰러짐·좌화·봉인). 정산 기록을 만든다. 혼에 옮기는 일은 부른 쪽이 Soul.EndLife 로.
        /// leftoverSilver 를 안 주면 지갑(Silver)에 남은 은자로 정산한다.</summary>
        public LifeRecord End(string way, int? leftoverSilver = null)
        {
            if (Record != null) throw new InvalidOperationException("이미 끝난 생입니다");
            if (!_data.Cheonha.LifeEnd.Ways.Contains(way)) throw new ArgumentException($"모르는 생의 끝입니다: {way}");
            // 서장은 장삼을 쓰러뜨려야(KillPrologueBoss) 끝난다. 그 전에 좌화·그만두기를 고르면 RestartPrologue 로 처음부터.
            if (Prologue != null && !Prologue.FightOver)
                throw new InvalidOperationException("서장은 장삼을 쓰러뜨려야 끝납니다. 그 전에 그만두면 RestartPrologue 로 처음부터");
            Record = Log.Finish(way, leftoverSilver ?? Silver);
            return Record;
        }
    }
}
