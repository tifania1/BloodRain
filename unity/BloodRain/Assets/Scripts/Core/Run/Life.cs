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
            // GU-06 무공 성취: 시작 무공이 대성(3생)이면 Lv2 로 시작한다. 서장 몸은 혼이 아직 없어 그대로
            if (!body.IsPrologue && soul.MasteryStage(body.Character.StartWeapon) >= 2)
            {
                Loadout.StartWeaponAt(body.Character.StartWeapon, MasteredStartLevel);
                StartMastered = true;
            }
            Xp = new RunXp(new XpCurve(_data.Balance.XpCurve), new Realms(_data.Balance.Realms))
            {
                PastLife = soul.PastLifePower(), // GU-11f1 전생의 공력: 넘어 본 구간은 필요 진기가 줄어든다(서장 첫 생은 넘은 구간이 없어 그대로)
            };
            Regions = new Regions(_data);
            Scale = new Scale(_data);
            Chronicle = new Chronicle(_data.Chronicle, Regions, Scale);
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
            Bosses = new BossAwakening(_data, body.Region.Id, _bossRecord, soul.Demo ? _data.Cheonha.Demo.Bosses : null);
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

        /// <summary>대성한 시작 무공이 생을 시작하는 레벨(기획서 10장 '무공 성취', 추천값).</summary>
        public const int MasteredStartLevel = 2;
        /// <summary>시작 무공이 대성이라 Lv2 로 시작했다(화면은 이 무공 이펙트에 금빛 테두리를 세운다).</summary>
        public bool StartMastered { get; }

        /// <summary>서장 차례(서장 몸일 때만). 장삼 등장은 Tick 이 알아서 알린다.</summary>
        public Prologue? Prologue { get; }
        /// <summary>서장 생성(서장 몸일 때만). 서장이면 Tick 이 Director 대신 이걸 돌린다.</summary>
        public PrologueDirector? PrologueSpawns { get; }
        public bool IsPrologue => Prologue != null;

        public RebirthBody Body { get; }
        /// <summary>이 생을 사는 혼(대장간 몸 색 등 꾸미기를 화면이 읽는다).</summary>
        public Soul Soul => _soul;
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
        public BloodArt? BloodArt { get; private set; }

        /// <summary>WP-29a(2026-11-20 추천대로, decisions #36): 마교 몸이 아닌데 혈마의 속삭임을 받으면 마교 혈공(cheonha.json 마교 bloodArt)이
        /// 그 생에만 그대로 켜진다(진기 × 농도^0.25, 더 받은 진기 150마다 음 1). 시작값은 이 몸의 음양 시작값. 이미 혈공이 있으면 그대로.</summary>
        void LendBloodArt()
        {
            if (BloodArt != null) return;
            var art = _data.Cheonha.Sects.Select(s => s.BloodArt).FirstOrDefault(a => a != null);
            if (art != null) BloodArt = new BloodArt(art, Loadout.Gauge, Body.YinyangStart);
        }
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

        /// <summary>이번 생 병기 칸(기획서 8장 '문파 병기와 6단', gear.json). 생이 끝나면 버린다. 이어하기 저장(LifeSnapshot Gear·GearGrade)에 든다.</summary>
        public GearSlot GearSlot { get; } = new GearSlot();

        /// <summary>지금 땅에서 그 적을 쓰러뜨렸을 때 떨어뜨릴 병기 id(없으면 null). 서장에는 병기가 없다. luck 은 기연(StatSheet.Luck).</summary>
        public string? RollGearDrop(string enemyId, float luck, Random rng) =>
            IsPrologue ? null : Build.Gear.RollDrop(_data, RegionId, enemyId, luck, rng);

        /// <summary>떨어진 병기를 줍는다. 돌려주는 값은 주운 뒤 등급 자리(0 = 범병).</summary>
        public int PickGear(string gearId) => GearSlot.Pick(_data, gearId);

        /// <summary>그 무공 피해 배율(1 + 병기 등급 보너스).</summary>
        public float GearDamageMultiplier(string weaponId) => 1f + (float)Build.Gear.DamageBonus(_data, GearSlot, weaponId);

        /// <summary>그 무공 범위 배율(1 + 병기 등급 보너스).</summary>
        public float GearAreaMultiplier(string weaponId) => 1f + (float)Build.Gear.AreaBonus(_data, GearSlot, weaponId);

        /// <summary>그 무공이 6단(신병합일)인지.</summary>
        public bool IsSixth(string weaponId) => Build.Gear.IsSixth(_data, GearSlot, weaponId, Loadout.Evolved.Contains(weaponId));

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
            LendBloodArt();
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
            foreach (var e in _elixirs) if (e.MaxHealth != 0) b.Add(StatKind.MaxHealth, e.MaxHealth);   // 만년설삼
            return Body.ComputeStats(_data, Loadout.Passives, _soul.Training, b, RealmRewards.MaxHealthFlat, healthRatio, moving, _soul.GoldenToadsTotal);
        }

        /// <summary>태극(조화) 구간이면 붙는 쾌속(기본 0.1, 무당 몸 0.2). 태극이 아니면 0. StatSheet.CooldownReduction 에 더하고 상한으로 자른다.</summary>
        public float HarmonyCooldownReduction =>
            Loadout.Gauge.Band == YinYangBand.Harmony ? Body.SectTrait.HarmonyCooldownReduction(_data.Balance.Yinyang.Harmony) : 0f;

        /// <summary>경공 최대 충전: balance.json dash 기본 + 연무장·성질 + 경지 돌파(일류 +1). 돌파 뒤 Dash.SetMaxCharges 에 넣는다.</summary>
        public int DashMaxCharges(StatSheet stats) => _data.Balance.Dash.Charges + stats.DashCharges + RealmRewards.DashCharges + PassiveMax.DashCharges;

        /// <summary>최대 레벨 심법의 덧붙임(9장 심법 표 '최대' 칸 — 금강체 넉백 면역·만독심법 독 면역·운룡보 경공 충전 +1). 경공 칸은 DashMaxCharges 가 이미 더한다.</summary>
        public PassiveMaxBonus PassiveMax => PassiveMaxBonus.Of(_data, Loadout);

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
        /// <summary>천하 연표(chronicle.json). 서장에서는 보지 않는다.</summary>
        public Chronicle Chronicle { get; }
        /// <summary>지금 자리의 혈운 농도. 적 기혈·피해는 EnemyScaling.HealthAt·DamageAt 에 이 값을 넘긴다.</summary>
        public float Density => Regions.Density(Position, SealStones) + CrisisDensity(RegionId);

        /// <summary>무너진 문파 때문에 더해지는 혈운 농도(GU-11m, chronicle.json crisis): 무너진 위기 하나마다 천하 모든 땅 + fallenEachDensity,
        /// 그 위기 지역(강시 문파 땅)이면 + fallenRegionDensity. 이번 생에만.</summary>
        public float CrisisDensity(string regionId)
        {
            var d = _data.Chronicle.Crisis;
            float add = 0f;
            foreach (var e in Chronicle.Events)
            {
                if (e.Kind != SectCrisisKind || Crisis(e.Id) != CrisisState.Fallen) continue;
                add += d.FallenEachDensity;
                if (e.Region == regionId) add += d.FallenRegionDensity;
            }
            return add;
        }
        /// <summary>지금 가진 봉인석 수(생이 시작될 때 혼의 수 + 이번 생에 새로 얻은 것).</summary>
        public int SealStones => _bossRecord.SealStones;
        /// <summary>서 있는 지역(가장 가까운 지역 중심).</summary>
        public string RegionId { get; private set; }
        public LifePhase Phase => Record != null ? LifePhase.Ended : Bosses.InFight ? LifePhase.BossFight : LifePhase.Roaming;
        /// <summary>끝났으면 정산 기록(Soul.EndLife 에 넘긴다).</summary>
        public LifeRecord? Record { get; private set; }
        /// <summary>혈마의 그림자 단계(0이면 없음).</summary>
        public int ShadowStep => ShadowReplaced ? 0 : Director.ShadowStep(Minutes, Level, SealStones);

        /// <summary>2막 규칙(GU-11h4)이 켜졌으면 혈마의 그림자는 없고 모여드는 땅의 주인이 대신한다.</summary>
        public bool ShadowReplaced => _data.Cheonha.LifeEnd.GatheringLords.EveryMinutes > 0;

        /// <summary>혈월이 졌나(lifeEnd.endMinute, 60분). Presentation 이 좌화와 같은 정산으로 생을 닫는다.</summary>
        public bool BloodMoonSet => _data.Cheonha.LifeEnd.EndMinute > 0 && Minutes >= _data.Cheonha.LifeEnd.EndMinute && !IsPrologue;

        /// <summary>이번 생에 무너진 문파 땅 가운데 땅의 주인이 있고 아직 이 생에 안 쓰러뜨린 곳(무너진 차례).</summary>
        public IReadOnlyList<string> FallenLordRegions =>
            Chronicle.Events.Where(e => e.Kind == SectCrisisKind && e.Region != null && Crisis(e.Id) == CrisisState.Fallen)
                .OrderBy(e => e.AtSeconds).Select(e => e.Region!).Distinct()
                .Where(r => Regions.Region(r).Boss != null && !Log.BossesKilled.Contains(r)).ToList();

        /// <summary>지금까지 모여들었어야 할 땅의 주인 수(GU-11h4): 현경이고 봉인석이 모자라면 fromMinute 에 1, everyMinutes 마다 +1, 무너진 땅 주인 수까지.</summary>
        public int GatheringLordsDue
        {
            get
            {
                var end = _data.Cheonha.LifeEnd;
                var g = end.GatheringLords;
                if (g.EveryMinutes <= 0 || IsPrologue) return 0;
                var need = _data.Cheonha.Bosses.FinalRequires;
                if (Realm < Scale.RealmIndex(need.Realm) || SealStones >= need.SealStones || Minutes < g.FromMinute) return 0;
                int n = 1 + (int)Math.Floor((Minutes - g.FromMinute) / g.EveryMinutes + 1e-4f);
                return Math.Min(n, _gathered.Union(FallenLordRegions).Count());
            }
        }

        readonly List<string> _gathered = new List<string>();
        readonly List<string> _gatherNews = new List<string>();
        readonly List<string> _summonQueue = new List<string>();
        /// <summary>이미 모여든 땅의 주인 지역(온 차례).</summary>
        public IReadOnlyList<string> GatheredLords => _gathered;

        /// <summary>새로 모여든 땅의 주인 지역을 꺼내 간다(Presentation 의 알림용, 한 번만). 싸움은 Life.Tick 이 차례로 건다.</summary>
        public IReadOnlyList<string> TakeGatheringLords()
        {
            var l = _gatherNews.ToList();
            _gatherNews.Clear();
            return l;
        }

        /// <summary>Tick 마다: 때가 된 주인을 모여든 목록에 넣고, 싸움 중이 아니면 기다리는 주인과 싸움을 건다(BossAwakening.Summon).</summary>
        void NoteGathering()
        {
            int due = GatheringLordsDue;
            if (due > _gathered.Count)
                foreach (var r in FallenLordRegions)
                {
                    if (_gathered.Count >= due) break;
                    if (_gathered.Contains(r)) continue;
                    _gathered.Add(r);
                    _gatherNews.Add(r);
                    _summonQueue.Add(r);
                }
            while (_summonQueue.Count > 0 && !Bosses.InFight)
            {
                var r = _summonQueue[0];
                _summonQueue.RemoveAt(0);
                if (Bosses.Summon(r, Level, Minutes) != null) break;
            }
        }
        /// <summary>지금 혈마의 그림자 세기(단계 0이면 Active 가 false).</summary>
        public BloodShadowStrength Shadow => BloodShadow.Strength(_data.Cheonha.LifeEnd.Shadow, ShadowStep);
        /// <summary>혈월 천하(회귀, 21장)면 적 기혈 배율(cheonha.json cycle.bloodmoon.enemyHealth), 아니면 1. EnemyScaling.HealthAt 결과에 곱한다.</summary>
        public float EnemyHealthMultiplier => _soul.IsBloodMoon ? _data.Cheonha.Cycle.Bloodmoon.EnemyHealth : 1f;
        /// <summary>혈월 천하면 적 이동속도 배율(cycle.bloodmoon.enemySpeed), 아니면 1. 혈마의 그림자에는 곱하지 않는다(그림자 세기는 Shadow 그대로).</summary>
        public float EnemySpeedMultiplier => _soul.IsBloodMoon ? _data.Cheonha.Cycle.Bloodmoon.EnemySpeed : 1f;

        /// <summary>
        /// 지금 나오는 적의 기혈(기획서 11장 '적 능력치 증가'): 기준 × (1 + 0.08 × 경과 분) × 지금 자리 혈운 농도 × 혈월 배율.
        /// 정예·혈인·고유 적 모두 나오는 순간 한 번 셈한다(이미 나온 적은 그대로). 땅의 주인은 BossAwakening 의 기혈 배율을 쓴다.
        /// </summary>
        public float EnemyHealth(float baseHealth) =>
            new Combat.EnemyScaling(_data.Balance.EnemyScaling).HealthAt(baseHealth, Minutes, Density) * EnemyHealthMultiplier;

        /// <summary>지금 나오는 적의 피해(11장): 기준 × (1 + 0.03 × 경과 분) × √혈운 농도. 닿는 피해·탄·장판 모두 이 값으로 바꿔 넣는다.</summary>
        public float EnemyDamage(float baseDamage) =>
            new Combat.EnemyScaling(_data.Balance.EnemyScaling).DamageAt(baseDamage, Minutes, Density);
        /// <summary>지금 세상 시간 배율(돌파·각성 연출 중엔 느리다). Unity 의 Time.timeScale 에 넣는다.</summary>
        public float WorldTimeScale => Math.Min(Breakthrough.WorldTimeScale, Awakening.IsActive ? Awakening.WorldTimeScale : 1f);

        readonly List<RegionDef> _wokenByTime = new List<RegionDef>();

        /// <summary>시각(15분, cheonha.json bosses.awakenAtMinute)으로 새로 깨어난 땅의 주인. 부르면 비운다. 돌파로 깨어난 곳은 LifeAbsorb.WokenBosses.</summary>
        public List<RegionDef> TakeWokenByTime()
        {
            var r = new List<RegionDef>(_wokenByTime);
            _wokenByTime.Clear();
            return r;
        }

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
            if (_yinyangFreezeLeft > 0 && (_yinyangFreezeLeft -= dt) <= 0) Loadout.Gauge.Frozen = false;   // 태극환
            TickUltimate(dt);
            if (PrologueSpawns != null) return TickPrologue(dt, position, aliveNow);
            Director.Tick(dt);
            Awakening.TickWorld(dt);
            Dissolution?.Tick(dt);
            Backlash.Tick(dt, Loadout.Gauge.Band == YinYangBand.Extreme);

            MoveTo(position);

            Bosses.Tick(dt, Position, Level, Minutes);
            NoteGathering();
            var wokeByTime = Bosses.UpdateMinute(Minutes); // 15분이면 경지가 모자라도 장막 안 주인이 깨어난다(EB-01a)
            if (wokeByTime.Count > 0) _wokenByTime.AddRange(wokeByTime);
            Director.BossFight = Bosses.InFight;
            NoteChronicle(Minutes - dt / 60f);
            HoldCrises(dt);
            NoteFightHarmony();

            _secondAcc += dt;
            if (_secondAcc < 1.0) return null;
            _secondAcc -= 1.0; // 한 프레임이 몇 초를 건너뛰면 남은 초는 다음 프레임들이 하나씩 낸다(버리지 않는다)
            return Director.Second(Minutes, Level, RegionId, aliveNow);
        }

        /// <summary>연표(GU-11h5, 겪음 기록만): 이번 프레임에 시각이 지난 사건마다, 그 자리가 지금 화면 대각선의 절반 안이면 '겪음'.
        /// 천하 전체 사건은 필요한 경지(minRealm)에 올라 있으면 겪음. 문파의 위기를 못 막음(그 자리에 없었음)은 Crisis·FallenSects, 막음은 아직 없다.
        /// '화면 안' 기준은 2026-11-18 예약 세션이 정한 것(설계안은 '그 시각에 거기 있었는가'까지만).</summary>
        void NoteChronicle(float fromMinutes)
        {
            float from = fromMinutes * 60f, to = Minutes * 60f;
            foreach (var e in Chronicle.Events)
            {
                if (e.AtSeconds <= from || e.AtSeconds > to) continue;
                bool seen = e.Everywhere
                    ? Realm >= Chronicle.RequiredRealm(e, Position)
                    : Vector2.Distance(Chronicle.Position(e)!.Value, Position) <= Scale.ViewDiagonal(Level) * 0.5f;
                if (seen) Log.Witness(e.Id);
            }
        }

        /// <summary>문파의 위기(연표 kind sect_crisis)가 이번 생에 어떻게 됐나(GU-11m, 2026-11-20 추천대로 — decisions #65).
        /// 사건 시각이 지났는데 그 자리에 없었으면(겪음 기록이 없으면) Fallen. 있었으면 Present(버티는 중) — 그 뒤 crisis.holdSeconds 초를
        /// 겪음과 같은 반경 안에서 버티면 Blocked, 한 번이라도 반경 밖으로 나가면 Fallen. 결과는 이 생에만(혼에는 겪은·막은 기록만).</summary>
        public CrisisState Crisis(string chronicleEvent)
        {
            var e = Chronicle.Event(chronicleEvent);
            if (e.Kind != SectCrisisKind) throw new ArgumentException($"문파의 위기가 아닙니다: {chronicleEvent}");
            if (Log.CrisesBlocked.Contains(e.Id)) return CrisisState.Blocked;
            if (_crisisBroken.Contains(e.Id)) return CrisisState.Fallen;
            if (Log.Chronicle.Contains(e.Id)) return CrisisState.Present;
            return Minutes * 60f >= e.AtSeconds && !IsPrologue ? CrisisState.Fallen : CrisisState.Ahead;
        }

        readonly Dictionary<string, float> _crisisHeld = new Dictionary<string, float>(StringComparer.Ordinal);
        readonly HashSet<string> _crisisBroken = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>버티는 중인 위기의 버틴 초(생 도중 저장용).</summary>
        public IReadOnlyDictionary<string, float> CrisisHeld => _crisisHeld;
        /// <summary>그 자리에 있었지만 버티지 못하고 떠난 위기(생 도중 저장용).</summary>
        public IReadOnlyCollection<string> CrisisBroken => _crisisBroken;
        /// <summary>이번 생에 막은 위기마다 받은 윤회의 기(처음 막은 것만, 0이면 이미 막아 본 사건). 화면 알림용으로 한 번 꺼내 간다.</summary>
        readonly List<(string id, int qi)> _blockedNow = new List<(string, int)>();
        public IReadOnlyList<(string id, int qi)> TakeCrisesBlocked() { var l = _blockedNow.ToList(); _blockedNow.Clear(); return l; }

        void HoldCrises(float dt)
        {
            float hold = _data.Chronicle.Crisis.HoldSeconds;
            foreach (var e in Chronicle.Events)
            {
                if (e.Kind != SectCrisisKind || e.Everywhere || !Log.Chronicle.Contains(e.Id)) continue;
                if (Log.CrisesBlocked.Contains(e.Id) || _crisisBroken.Contains(e.Id)) continue;
                bool near = Vector2.Distance(Chronicle.Position(e)!.Value, Position) <= Scale.ViewDiagonal(Level) * 0.5f;
                if (!near) { _crisisBroken.Add(e.Id); _crisisHeld.Remove(e.Id); continue; }
                _crisisHeld.TryGetValue(e.Id, out var held);
                // 사건 시각을 넘긴 몫만 센다(겪은 프레임은 시각 뒤 남은 초만)
                held = Math.Min(held + dt, Minutes * 60f - e.AtSeconds);
                if (held >= hold) { _crisisHeld.Remove(e.Id); _blockedNow.Add((e.Id, Log.BlockCrisis(e.Id))); }
                else _crisisHeld[e.Id] = held;
            }
        }

        public const string SectCrisisKind = "sect_crisis";

        /// <summary>이번 생에 무너진 문파 id(위기 시각에 그 자리에 없었던 문파, 무너진 차례). 문파는 사건 지역에서 태어나는 문파.
        /// 병기를 떨어뜨리는 강시 제자는 이 목록의 문파에서만 나온다(2026-11-18 사람 결정 4, 병기 규칙은 GU-10 때).</summary>
        public IReadOnlyList<string> FallenSects =>
            Chronicle.Events.Where(e => e.Kind == SectCrisisKind && e.Region != null && Crisis(e.Id) == CrisisState.Fallen)
                .OrderBy(e => e.AtSeconds)
                .SelectMany(e => _data.Cheonha.Sects.Where(x => x.Region == e.Region).Select(x => x.Id))
                .Distinct().ToList();

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

        readonly List<ElixirDef> _elixirs = new List<ElixirDef>();
        bool _elixirReviveUsed;
        float _yinyangFreezeLeft;

        /// <summary>소환단을 이미 썼나, 태극환 음양 멈춤이 몇 초 남았나(생 도중 저장용).</summary>
        public bool ElixirReviveUsed => _elixirReviveUsed;
        public float YinyangFreezeLeft => Math.Max(0f, _yinyangFreezeLeft);

        /// <summary>이번 생에 지니고 나온 약방 영약(기획서 10장 '영약(약방)').</summary>
        public IReadOnlyList<ElixirDef> Elixirs => _elixirs;

        /// <summary>생을 시작할 때(첫 Tick 전) 한 번, Meta.Apothecary.TryPrepare 가 돌려준 영약을 건다.
        /// 공청석유 오의 게이지, 혈정단 정예 혈정 배율, 태극환 음양 멈춤, 대환단 시작 레벨(오른 레벨만큼 레벨업 고르기를 띄우도록 AbsorbResult 를 돌려준다),
        /// 만년설삼은 CurrentStats, 소환단은 TryRevive 가 본다.</summary>
        public AbsorbResult TakeElixirs(IReadOnlyList<ElixirDef> elixirs)
        {
            if (IsPrologue) throw new InvalidOperationException("서장에는 영약을 지니고 나오지 않습니다");
            if (Minutes > 0 || _elixirs.Count > 0) throw new InvalidOperationException("영약은 생을 시작할 때 한 번 건다");
            _elixirs.AddRange(elixirs);
            foreach (var e in elixirs)
            {
                if (e.StartUltimate > 0) Charge.AddFlat(e.StartUltimate);
                if (e.BloodCrystalMultiplier > 0) Log.DropCrystalMultiplier *= e.BloodCrystalMultiplier;
                if (e.YinyangFreezeSeconds > _yinyangFreezeLeft) _yinyangFreezeLeft = e.YinyangFreezeSeconds;
            }
            if (_yinyangFreezeLeft > 0) Loadout.Gauge.Frozen = true;
            int from = Xp.Level, target = elixirs.Count == 0 ? 0 : elixirs.Max(e => e.StartLevel);
            var breaks = new List<RealmDef>();
            while (Xp.Level < target)
            {
                if (Xp.Growth <= 0) break;
                var r = Xp.Absorb((Xp.Required - Xp.Xp) / Xp.Growth + 1e-6);
                breaks.AddRange(r.Breakthroughs);
            }
            return new AbsorbResult(from, Xp.Level, breaks);
        }

        /// <summary>이번 생에 쓴 재기 횟수.</summary>
        public int RevivalsUsed { get; private set; }

        /// <summary>한 생(서장 아님)에서 기혈이 0이 됐다. 재기(stats.Revival — 연무장·영약)가 남았으면 하나 쓰고
        /// 일어날 기혈 비율(balance.json revival.healthRatio, 8장 '기혈 50%')을 돌려준다. 남지 않았으면 null — End(fallen) 로 정산.</summary>
        public float? TryRevive(StatSheet stats)
        {
            if (IsPrologue) throw new InvalidOperationException("서장은 PlayerDown 을 씁니다");
            if (!_elixirReviveUsed && _elixirs.FirstOrDefault(e => e.ReviveHealthRatio > 0) is { } pill)
            {
                _elixirReviveUsed = true;   // 소환단: '첫 사망 때' 라 재기보다 먼저, 재기 횟수는 그대로
                return pill.ReviveHealthRatio;
            }
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
        /// <summary>생 도중 저장 ①(GU-11h1)을 막 만든 같은 몸의 Life 에 되돌린다. 적·탄은 부른 쪽이 비운다.
        /// 다른 문파 몸이거나 이미 흐른 생이면 InvalidOperationException. ②로 미룬 상태는 LifeSnapshot 머리 주석.</summary>
        public void Restore(LifeSnapshot s)
        {
            if (s.SnapshotVersion != LifeSnapshot.Version) throw new InvalidOperationException($"모르는 저장 판: {s.SnapshotVersion}");
            if (Record != null || Minutes > 0) throw new InvalidOperationException("막 만든 생에만 되돌린다");
            if (!s.SameBody(Body)) throw new InvalidOperationException($"다른 몸의 저장입니다: {s.Sect} {s.Region}");
            while (Loadout.WeaponSlots < s.WeaponSlots) Loadout.AddExtraSlot(true);
            while (Loadout.PassiveSlots < s.PassiveSlots) Loadout.AddExtraSlot(false);
            foreach (var kv in s.Weapons)
            {
                if (!Loadout.Weapons.ContainsKey(kv.Key)) Loadout.Apply(new Offer(OfferKind.NewWeapon, kv.Key));
                int more = kv.Value - Loadout.Weapons[kv.Key];
                if (more > 0) Loadout.Apply(new Offer(OfferKind.UpgradeWeapon, kv.Key, more));
            }
            foreach (var kv in s.Passives)
            {
                if (!Loadout.Passives.ContainsKey(kv.Key)) Loadout.Apply(new Offer(OfferKind.NewPassive, kv.Key));
                int more = kv.Value - Loadout.Passives[kv.Key];
                if (more > 0) Loadout.Apply(new Offer(OfferKind.UpgradePassive, kv.Key, more));
            }
            if (s.PickOrder != null) Loadout.RestorePickOrder(s.PickOrder);
            foreach (var w in s.Evolved)
                Loadout.Evolve(_data.Evolutions.FirstOrDefault(e => e.Weapon == w) ?? throw new InvalidOperationException($"절기가 없는 무공입니다: {w}"));
            foreach (var e in s.Chronicle) Log.Witness(e);
            foreach (var e in s.CrisesBlocked ?? new List<string>()) Log.BlockCrisis(e);
            foreach (var e in s.CrisisBroken ?? new List<string>()) _crisisBroken.Add(e);
            foreach (var kv in s.CrisisHeld ?? new Dictionary<string, float>()) _crisisHeld[kv.Key] = kv.Value;
            // 모여든 주인은 다시 알리지 않는다. 아직 안 쓰러뜨린 주인은 다시 싸움 차례에 넣는다
            foreach (var r in s.Gathered ?? new List<string>())
                if (!_gathered.Contains(r)) { _gathered.Add(r); if (!s.BossesKilled.Contains(r)) _summonQueue.Add(r); }
            GearSlot.Restore(_data, s.Gear, s.GearGrade);
            foreach (var id in s.Awakened)
            {
                if (_data.Evolutions.All(e => e.Id != id)) throw new InvalidOperationException($"없는 절기입니다: {id}");
                Log.Awaken(id);
            }
            if (s.Tally != null) Log.RestoreTally(s.Tally);
            foreach (var id in s.Links.Concat(s.GrandLinks))
                if (_data.Synergies.Links.All(l => l.Id != id)) throw new InvalidOperationException($"없는 연환입니다: {id}");
            Loadout.RestoreLinks(s.Links, s.GrandLinks);
            foreach (var region in s.BossesKilled)
            {
                var info = Log.KillBoss(region);   // 일지·윤회의 기·혈정을 다시 센다(장부는 저장하지 않으므로)
                _bossRecord.Apply(new BossKill { RegionId = region, First = info.FirstInCycle, SealStone = info.SealStone, UnlockSect = info.UnlockedSect });
            }
            if (s.Boons is { } b) RestoreBoons(b);
            Loadout.Gauge.Restore(s.Gauge);   // 무공·심법을 올리며 움직인 음양을 저장 값으로
            Xp.Restore(s.Level, s.Xp, s.XpTotal);
            // 오른 경지를 일지에 다시 적는다: 생 기록의 가장 높은 경지와 처음 오른 경지 윤회의 기(장부는 저장하지 않으므로)
            foreach (var r in _data.Balance.Realms.OrderBy(r => r.Level))
                if (r.Level > 1 && r.Level <= s.Level) Log.Breakthrough(r.Id);
            Bosses.Restore(s.BossesKilled, s.Level, s.Minutes);
            Charge.Restore(s.Charge);
            Minutes = s.Minutes;
            foreach (var r in s.Visited) Log.Enter(r);
            MoveTo(s.Position);
            Silver = s.Silver;
        }

        void RestoreBoons(LifeBoonsSnapshot b)
        {
            foreach (var id in b.Insights)
            {
                var def = _data.Insights.Concat(_data.DemonicInsights).FirstOrDefault(i => i.Id == id)
                    ?? throw new InvalidOperationException($"없는 깨달음입니다: {id}");
                InsightEffects.Take(def, Loadout.Gauge);   // 역근의 기울기 배율도 다시 건다. 게이지 값은 뒤에서 저장 값으로
            }
            InsightEffects.RestoreCounters(b.KillsAtSalgi, b.SimmaBaseline, b.BlockCooldown);
            Encounters.Restore(b.Encounters, b.EncounterSchools, b.SwordTombMight, b.WhisperTaken);
            if (b.WhisperTaken) LendBloodArt();
            // 영약: 다시 걸지 않는다(오의·시작 레벨은 저장 값이 이미 담음). 지닌 목록과 혈정 배율·소환단·태극환 남은 시간만
            foreach (var id in b.Elixirs)
            {
                var e = _data.Cheonha.Inn.Elixirs.FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException($"없는 영약입니다: {id}");
                _elixirs.Add(e);
                if (e.BloodCrystalMultiplier > 0) Log.DropCrystalMultiplier *= e.BloodCrystalMultiplier;
            }
            _elixirReviveUsed = b.ElixirReviveUsed;
            _yinyangFreezeLeft = b.YinyangFreezeLeft;
            Loadout.Gauge.Frozen = _yinyangFreezeLeft > 0;
            RevivalsUsed = b.RevivalsUsed;
        }

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
            kill.FirstOnAccount = info.FirstOnAccount;
            kill.SealStone = info.SealStone;
            kill.UnlockSect = info.UnlockedSect;
            kill.RebirthQi = info.RebirthQi;
            kill.BloodCrystal = info.BloodCrystal;
            _bossRecord.Apply(kill);
            kill.Woken = Bosses.Refresh();
            // 기획서 8장 '문파 병기와 6단': 그 땅 문파 병기를 쥐고 있으면 한 등급, 유품 병기는 계정 첫 처치에 그 자리에 떨어뜨린다
            var landSect = _data.Cheonha.Regions.FirstOrDefault(r => r.Id == kill.RegionId)?.Sect;
            kill.GearGradedUp = GearSlot.GradeUpIfSect(_data, landSect);
            if (info.FirstOnAccount)
                kill.GearDrop = _data.Gear.Gear.FirstOrDefault(g => g.Source == "relic:" + kill.RegionId)?.Id;
            return kill;
        }

        /// <summary>정예를 쓰러뜨렸다. 이번에 얻은 혈정 수(정산 화면·HUD 용).</summary>
        public int KillElite(string? enemyId = null)
        {
            // 떠도는 정예(엇박 2:30·7:30…, chapters.json roaming)는 혈정을 주지 않는다 — 10장 '20분 생에 8개'를 지킨다(EB-01a)
            if (enemyId != null && !IsPrologue)
            {
                _roamingElites ??= new HashSet<string>(_data.Chapters.First(c => c.Id == WaveTable.Life).RoamingElites);
                if (_roamingElites.Contains(enemyId)) return 0;
            }
            return Log.KillElite();
        }
        HashSet<string>? _roamingElites;

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
            Record.PickOrder = Loadout.PickOrder.ToList();
            int max = _data.Balance.Levels.WeaponMax;
            Record.MasteredWeapons = Loadout.Weapons.Where(kv => kv.Value >= max).Select(kv => kv.Key).OrderBy(x => x, StringComparer.Ordinal).ToList();
            return Record;
        }
    }

    /// <summary>문파의 위기 판정(Life.Crisis). Ahead: 시각 전. Present: 그 자리에서 버티는 중. Blocked: 막음. Fallen: 없었거나 버티지 못하고 떠남 — 강시 문파.</summary>
    public enum CrisisState { Ahead, Present, Fallen, Blocked }
}
