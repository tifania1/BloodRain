// 한 생을 묶는 입구(기획서 7장 '한 생의 흐름', 21장 환혼과 윤회).
// 환생으로 몸을 받으면 Life 를 만들고, 매 프레임 Tick(실제 시간, 플레이어 위치, 화면 위 적 수)을 부른다.
// Life 가 하는 일: 세상 시간(돌파·각성 연출 중엔 느려짐), 서 있는 지역과 처음 밟은 땅, 진기 흡수 → 레벨·돌파(축척) → 땅의 주인 깨어남,
// 땅의 주인 싸움 시작·처치, 매초 적 생성(LifeDirector), 기혈역류와 오의 게이지(Charge), 생의 끝(쓰러짐·좌화·봉인)과 정산 기록.
// 전투(맞히기·피해)는 Unity 쪽 Sim 이 하고 결과만 여기로 알려 준다(Absorb, DefeatBoss, End).
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

        public Life(Soul soul, RebirthBody body, Random rng)
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
            Charge = new UltimateCharge(_data.Balance.UltimateCharge, _data.Balance.Yinyang.Harmony, StartChargeBonus(soul, body));
            Charges = LevelUpCharges.Start(_data.Balance.LevelUp, body.Character, StartBonus(soul, body));
            // 역류를 경공으로 끊으면 오의 게이지 +50 이 이 게이지로 들어간다.
            Backlash = new Backlash(_data.Balance.Yinyang.Extreme, Charge);
            Director = new LifeDirector(_data, rng);
            _bossRecord = new BossRecord(soul.SealStones, soul.SealStoneCount);
            Bosses = new BossAwakening(_data, body.Region.Id, _bossRecord);
            if (body.Sect?.BloodArt is { } art) BloodArt = new BloodArt(art, Loadout.Gauge, body.YinyangStart);
            InsightEffects = new InsightEffects(body.Character.StartWeapon);
            RegionId = body.Region.Id;
            Position = Regions.Center(body.Region);
        }

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
        /// <summary>고른 깨달음이 붙이는 값. 고르면 InsightEffects.Take(def, Loadout.Gauge).</summary>
        public InsightEffects InsightEffects { get; }

        /// <summary>깨달음 능력치 덧셈. 심마 횟수는 혈공에서 가져온다. CurrentStats(extra) 에 넣는다.</summary>
        public StatBonus InsightBonus(int kills, float healthRatio = 1f) =>
            InsightEffects.Bonus(kills, BloodArt?.Steps ?? 0, healthRatio);

        /// <summary>세상 시간으로 흐른 분.</summary>
        public float Minutes { get; private set; }
        public int Level => Xp.Level;
        public int Realm => Scale.RealmIndex(Xp.Level);
        /// <summary>지금까지 돌파한 경지들의 보상 합(경지표 '돌파 보상'). 효과를 얹는 일은 부른 쪽이 한다.</summary>
        public RealmRewardSum RealmRewards => Progression.RealmRewards.Upto(_data.Balance.Realms, Xp.Level);

        /// <summary>지금 몸의 능력치: 몸 + 지금 심법 레벨 + 연무장 + 성질 + 경지 돌파 최대 기혈(이류 +10).
        /// 깨달음·진전 같은 덧셈은 extra 로. 심법을 올리거나 경지를 넘으면 다시 부른다(값을 들고 있지 않는다).</summary>
        public StatSheet CurrentStats(StatBonus? extra = null) =>
            Body.ComputeStats(_data, Loadout.Passives, _soul.Training, extra, RealmRewards.MaxHealthFlat);

        /// <summary>경공 최대 충전: balance.json dash 기본 + 연무장·성질 + 경지 돌파(일류 +1). 돌파 뒤 Dash.SetMaxCharges 에 넣는다.</summary>
        public int DashMaxCharges(StatSheet stats) => _data.Balance.Dash.Charges + stats.DashCharges + RealmRewards.DashCharges;

        /// <summary>오의 위력 배율(절정 돌파면 1.25).</summary>
        public float UltimatePowerMultiplier => 1f + RealmRewards.UltimatePower;

        /// <summary>받는 피해 배율(화경 돌파면 0.9). Damage.Taken(…, 호신) 뒤에 곱한다.</summary>
        public float DamageTakenMultiplier => RealmRewards.DamageTakenMultiplier;

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
            if (Awakening.IsActive && (Awakening.Tick(unscaledDt) & AwakeningCue.Shock) != 0)
                Director.Hold(_data.Balance.Awakening.SpawnPauseSeconds);

            float dt = unscaledDt * WorldTimeScale;
            Minutes += dt / 60f;
            Director.Tick(dt);
            Awakening.TickWorld(dt);
            Backlash.Tick(dt, Loadout.Gauge.Band == YinYangBand.Extreme);

            MoveTo(position);

            Bosses.Tick(dt, Position, Level, Minutes);
            Director.BossFight = Bosses.InFight;

            _secondAcc += dt;
            if (_secondAcc < 1.0) return null;
            _secondAcc -= 1.0; // 한 프레임이 몇 초를 건너뛰면 남은 초는 다음 프레임들이 하나씩 낸다(버리지 않는다)
            return Director.Second(Minutes, Level, RegionId, aliveNow);
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
                Director.OnBreakthrough(revisit);
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

        /// <summary>오의를 쓴다. 게이지가 가득 찼을 때만 비우고 true. 마교 몸이면 천마해체로 음양 게이지가 시작값으로 돌아온다(혈공).
        /// Unity 쪽은 Charge.TryUse 를 따로 부르지 말고 이걸 부른다.</summary>
        public bool UseUltimate()
        {
            if (Record != null) throw new InvalidOperationException("끝난 생입니다");
            if (!Charge.TryUse()) return false;
            if (BloodArt != null && BloodArt.OnUltimate(Body.Character.Ultimate))
                InsightEffects.OnDemonicReset(BloodArt.Steps); // 마공 깨달음 '심마' 공력도 사라진다
            return true;
        }

        /// <summary>싸우던 땅의 주인을 쓰러뜨렸다. 종장(봉마곡)이면 봉인으로 생이 끝난다.
        /// 보상(첫 처치·봉인석·문파·윤회의 기·혈정)은 혼 장부(LifeLog.KillBoss) 값으로 맞춘다. 혈월 천하 배율도 거기서 붙는다.
        /// 봉인석이 늘어 새로 깨어난 땅(일곱째면 봉마곡)은 kill.Woken.</summary>
        public BossKill DefeatBoss(int leftoverSilverIfFinal = 0)
        {
            var kill = Bosses.Defeat();
            Director.BossFight = false;
            if (kill.IsFinal)
            {
                End(LifeEndWay.Sealed, leftoverSilverIfFinal);
                return kill;
            }
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

        /// <summary>생이 끝났다(쓰러짐·좌화·봉인). 정산 기록을 만든다. 혼에 옮기는 일은 부른 쪽이 Soul.EndLife 로.</summary>
        public LifeRecord End(string way, int leftoverSilver)
        {
            if (Record != null) throw new InvalidOperationException("이미 끝난 생입니다");
            if (!_data.Cheonha.LifeEnd.Ways.Contains(way)) throw new ArgumentException($"모르는 생의 끝입니다: {way}");
            Record = Log.Finish(way, leftoverSilver);
            return Record;
        }
    }
}
