#nullable enable
// cheonha.json: 천하 한 장(기획서 7장)과 환혼·윤회의 기(10·21장). 규칙 계산은 Core/World, Core/Meta 에서 한다.
using System.Collections.Generic;
using System.Linq;

namespace BloodRain.Core.Data
{
    /// <summary>칭호 하나. When: food_one_run(한 생 음식 Count 개), boss_single_weapon(공격 무공 하나만 든 채 땅의 주인 처치), all_sects_lived(모든 문파로 한 번씩 환생).</summary>
    public sealed class TitleDef
    {
        public const string FoodOneRun = "food_one_run";
        public const string BossSingleWeapon = "boss_single_weapon";
        public const string AllSectsLived = "all_sects_lived";
        public string Id = "";
        public string NameKo = "";
        public string Hanja = "";
        public string When = "";
        public int Count;
    }

    /// <summary>의상 하나의 해금 조건. When: boss_in_harmony(Boss 와 싸우는 동안 음양이 태극 구간을 한 번도 안 벗어나고 처치).</summary>
    public sealed class CostumeDef
    {
        public const string BossInHarmony = "boss_in_harmony";
        public string Id = "";
        public string Character = "";
        public string When = "";
        public string Boss = "";
        /// <summary>혼 해금 토큰(Soul.IsUnlocked).</summary>
        public string Token => UnlockToken.CostumePrefix + Id;
    }

    public sealed class CheonhaDef
    {
        public WorldDef World = new WorldDef();
        public List<RealmScaleDef> RealmScale = new List<RealmScaleDef>();
        public ClusterDef Cluster = new ClusterDef();
        public CrushDef Crush = new CrushDef();
        public BreakthroughDef Breakthrough = new BreakthroughDef();
        public DensityDef Density = new DensityDef();
        public List<RegionDef> Regions = new List<RegionDef>();
        public BossRulesDef Bosses = new BossRulesDef();
        public List<SectDef> Sects = new List<SectDef>();
        public PrologueBodyDef PrologueBody = new PrologueBodyDef();
        public BirthTraitsDef BirthTraits = new BirthTraitsDef();
        public RebirthDef Rebirth = new RebirthDef();
        public RebirthQiDef RebirthQi = new RebirthQiDef();
        public MasteryDef Mastery = new MasteryDef();
        public LifeEndDef LifeEnd = new LifeEndDef();
        public CycleDef Cycle = new CycleDef();
        public InnDef Inn = new InnDef();
        /// <summary>모드와 열리는 시점(기획서 7장 '모드'). Meta.Modes.</summary>
        public List<ModeDef> Modes = new List<ModeDef>();
        /// <summary>칭호(10장 해금 표, 모으기 보상이라 능력치 없음).</summary>
        public List<TitleDef> Titles = new List<TitleDef>();
        /// <summary>의상 해금(10장 해금 표). 그림·이름은 아트 뒤라 조건만.</summary>
        public List<CostumeDef> Costumes = new List<CostumeDef>();

        public RegionDef Region(string id) => Regions.First(r => r.Id == id);
        public SectDef Sect(string id) => Sects.First(s => s.Id == id);
        public BirthTraitDef Trait(string id) => BirthTraits.List.First(t => t.Id == id);
        /// <summary>땅의 주인이 있는 지역(봉마곡 빼고 일곱).</summary>
        public IEnumerable<RegionDef> BossRegions => Regions.Where(r => r.Boss != null && !r.Final);
    }

    /// <summary>청운객잔 시설(cheonha.json inn, 기획서 10장 '객잔 시설').</summary>
    public sealed class ModeDef
    {
        public string Id = "";
        public string NameKo = "";
        public ModeRequireDef Require = new ModeRequireDef();
    }

    public sealed class ModeRequireDef
    {
        /// <summary>서장을 마쳐야 한다.</summary>
        public bool Prologue;
        /// <summary>봉인석을 이만큼 가져야 한다(한 번 열리면 회귀로 봉인석이 지워져도 닫히지 않는다).</summary>
        public int SealStones;
        /// <summary>이 엔딩(Soul.EndingSeal 등)을 한 번 봐야 한다.</summary>
        public string? Ending;
    }

    public sealed class InnDef
    {
        public List<FacilityDef> Facilities = new List<FacilityDef>();
        public FacilityDef Facility(string id) => Facilities.First(f => f.Id == id);
        /// <summary>한 생에 지니고 출발하는 영약 수(10장 '영약(약방)').</summary>
        public int ElixirsPerLife = 1;
        public List<ElixirDef> Elixirs = new List<ElixirDef>();
    }

    /// <summary>약방 영약 하나(10장 '영약(약방)'). 효과는 이번 생 한정이고 0 인 칸은 효과 없음.</summary>
    public sealed class ElixirDef
    {
        public string Id = "";
        public string NameKo = "";
        public string Hanja = "";
        public string DescKo = "";
        /// <summary>혈정.</summary>
        public int Cost;
        /// <summary>첫 사망 때 이 비율의 기혈로 부활.</summary>
        public float ReviveHealthRatio;
        /// <summary>이 레벨로 시작.</summary>
        public int StartLevel;
        /// <summary>오의(진기) 게이지를 이만큼 채우고 시작.</summary>
        public float StartUltimate;
        /// <summary>최대 기혈 비율 더하기(0.3 = +30%).</summary>
        public float MaxHealth;
        /// <summary>혈정 드롭 배율(0 이면 1).</summary>
        public float BloodCrystalMultiplier;
        /// <summary>생 시작부터 이 초 동안 음양 게이지가 움직이지 않는다.</summary>
        public float YinyangFreezeSeconds;
    }

    public sealed class FacilityDef
    {
        public string Id = "";
        public string NameKo = "";
        public FacilityRequireDef Require = new FacilityRequireDef();
        /// <summary>0 이면 조건만 갖추면 저절로 열린다. 아니면 이만큼 혈정을 써서 짓는다.</summary>
        public int BuildBloodCrystal;
        /// <summary>열려도 '수리 중'으로 잠가 들어가지 못한다(화면을 아직 안 그린 시설. M2 의 장경각, backlog A-07).</summary>
        public bool UnderRepair;
    }

    public sealed class FacilityRequireDef
    {
        /// <summary>서장을 마쳐야 한다.</summary>
        public bool Prologue;
        /// <summary>지닌 봉인석 수.</summary>
        public int SealStones;
        /// <summary>계정에서 쓰러뜨린 땅의 주인 지역 id.</summary>
        public string? BossKilled;
        /// <summary>혼 해금 토큰(대화 진행 등).</summary>
        public string? Unlock;
    }

    public sealed class WorldDef
    {
        public float WidthMeters;
        public float HeightMeters;
        /// <summary>경지 한 단계마다 세상이 작아지는 배율(2.25).</summary>
        public float ScalePerRealm;
        /// <summary>삼류 첫 화면 대각선(m).</summary>
        public float View0Meters;
        /// <summary>경지 안 진행이 시야에 주는 몫(0.35단계).</summary>
        public float ViewInRealmShare;
        /// <summary>혈우 장막 반경 = 그 경지 첫 화면 대각선 × 이 값.</summary>
        public float BoundaryViewMultiple;
        /// <summary>장막 안쪽 밀림 띠 너비 = 그 경지 첫 화면 대각선 × 이 값(0.25).</summary>
        public float PushBandViewMultiple = 0.25f;
        /// <summary>장막 위에서 미는 속도 = 플레이어 이동 속도 × 이 값(1.25). 띠 시작에서는 0.</summary>
        public float PushMoveShare = 1.25f;
        /// <summary>이 경지부터 장막이 없다(현경).</summary>
        public string NoBoundaryFrom = "";
    }

    public sealed class RealmScaleDef
    {
        public string Realm = "";
        /// <summary>적 한 덩이에 든 사람 수.</summary>
        public int ClusterSize = 1;
        public string FoeKo = "";
        public string ScreenKo = "";
    }

    public sealed class ClusterDef
    {
        public string XpPerCluster = "one";
        public string KillCount = "members";
        public string DrawMembers = "ceil_by_health";
    }

    public sealed class CrushDef
    {
        public float XpPerCrushed;
        public float SweepViewMultiple;
        public float SweepAtSeconds;
        /// <summary>아래 경지에서 생긴 정예는 밟혀도 보갑을 떨군다.</summary>
        public bool EliteChestWhenCrushed;
    }

    public sealed class BreakthroughDef
    {
        public float TimeScale;
        public float ZoomFromSeconds;
        public float ZoomToSeconds;
        public float SpawnHoldSeconds;
        /// <summary>이미 올라 본 경지로 다시 돌파할 때 같은 순서를 이 길이로 줄인다(21장 한 번 깨친 길).</summary>
        public float RevisitSeconds;
    }

    public sealed class DensityDef
    {
        public string Blend = "nearest_two";
    }

    public sealed class RegionDef
    {
        public string Id = "";
        public string NameKo = "";
        public string Hanja = "";
        /// <summary>지도 그림 위 비율(0~1). 세계 m = pos × World 크기.</summary>
        public float[] Pos = new float[2];
        /// <summary>혈운 농도. 적 기혈·피해와 윤회의 기에 곱한다.</summary>
        public float Density = 1;
        public string? Sect;
        public string? Boss;
        public string? SealStone;
        /// <summary>enemies.json 에서 이 지역 고유 적·정예·보스가 쓰는 장 이름(prologue, chapter1 … final).</summary>
        public string? Chapter;
        public List<string> Gimmicks = new List<string>();
        public bool Final;
        public int HiddenUntilSealStones;
    }

    public sealed class BossRulesDef
    {
        public string AwakenFrom = "";
        public float EngageViewMultiple;
        /// <summary>깨어날 때 이미 싸움 원 안이면 이만큼 경고한 뒤 싸움(그사이 나가면 미룸).</summary>
        public float AwakenGraceSeconds;
        public float SpawnTargetScale;
        /// <summary>[싸움을 건 분, 기준 체력에 곱할 배율]. 사이는 곧게 잇고, 첫 점 앞은 첫 값, 마지막 점 뒤는 마지막 두 점의 기울기로 늘린다(7장).</summary>
        public List<float[]> HealthCurve = new List<float[]>();
        public BossFirstKillDef FirstKill = new BossFirstKillDef();
        public BossRepeatKillDef RepeatKill = new BossRepeatKillDef();
        public FinalRequiresDef FinalRequires = new FinalRequiresDef();
    }

    public sealed class BossFirstKillDef { public int RebirthQi; public int BloodCrystal; public bool SealStone; public bool UnlockSect; }
    public sealed class BossRepeatKillDef { public int RebirthQi; public int BloodCrystal; }
    public sealed class FinalRequiresDef { public int SealStones; public string Realm = ""; }

    /// <summary>문파 하나 = 태어날 수 있는 몸의 바탕(기획서 5장).</summary>
    public sealed class SectDef
    {
        public string Id = "";
        public string NameKo = "";
        public string Region = "";
        /// <summary>대표 인물(characters.json id). 화산·곤륜은 없음.</summary>
        public string? Leader;
        public string StartWeapon = "";
        public string Ultimate = "";
        public string Trait = "";
        public float MaxHealth = 100;
        public float MoveSpeed = 1;
        public int YinyangStart;
        /// <summary>태극 구간 반폭. 무당(음양조화)만 40이고 나머지는 balance.json 기본값(null).</summary>
        public int? HarmonyRange;
        /// <summary>"start" 또는 "boss:&lt;지역 id&gt;"(그 땅의 주인 첫 처치).</summary>
        public string Unlock = "start";
        /// <summary>마교 몸의 성장 '혈공'(5장). 다른 문파는 null.</summary>
        public BloodArtDef? BloodArt;
        /// <summary>고유 능력의 숫자(5장 '한눈에 보기' 고유 능력 칸). 없으면 null(아무 효과 없음).</summary>
        public SectTraitDef? TraitEffect;

        public string? UnlockBossRegion => UnlockToken.BossRegion(Unlock);
    }

    /// <summary>문파 고유 능력(기획서 5장 '한눈에 보기'). 능력치 이름은 StatKind 로 읽고, 비율은 0.25 = 25%.
    /// Core.Build.SectTrait 가 이 값으로 몸의 덧셈·독 무효·물 감속 무시·피해 배율·조화 보너스 배율을 낸다.</summary>
    public sealed class SectTraitDef
    {
        /// <summary>늘 붙는 능력치(소림 호신 2, 개방 탐욕 0.3·음식 회복 0.5).</summary>
        public Dictionary<string, float> Stats = new Dictionary<string, float>();
        /// <summary>기혈 비율이 이 값 이상일 때 aboveHealthStats 가 붙는다(남궁 0.5). 0 이면 쓰지 않는다.</summary>
        public float AboveHealthRatio;
        public Dictionary<string, float> AboveHealthStats = new Dictionary<string, float>();
        /// <summary>움직이는 동안 붙는 능력치(수로채 회피 0.15).</summary>
        public Dictionary<string, float> MovingStats = new Dictionary<string, float>();
        /// <summary>독 피해 무효(당가). 당가타 독안개·독 장판·독 탄 모두.</summary>
        public bool PoisonImmune;
        /// <summary>중독된 적에게 주는 피해에 더하는 비율(당가 0.2).</summary>
        public float PoisonedTargetDamage;
        /// <summary>물 지형 감속 무시(수로채).</summary>
        public bool IgnoresWater;
        /// <summary>태극(조화) 보너스(쾌속 +10%·오의 충전 +25%)에 곱하는 수(무당 2). 기본 1.</summary>
        public float HarmonyBonusMultiplier = 1f;
        /// <summary>몸 가까이(nearEnemySlowRadius m 안) 적의 이동속도를 이만큼 깎는다(곤륜 설한기 0.1).</summary>
        public float NearEnemySlow;
        public float NearEnemySlowRadius;
        /// <summary>기혈 회복 전부(회복 능력치·음식·흡혈·영약)에 곱하는 수(마교 마인의 피 0.5). 기본 1.</summary>
        public float HealingMultiplier = 1f;
        /// <summary>기혈이 낮을수록 붙는 쾌속의 끝값(마교 마인의 피 0.5). 기혈 100% 에서 0, lowHealthFullAt 이하에서 이 값, 그 사이 선형. 0 이면 없음.</summary>
        public float LowHealthCooldownReduction;
        /// <summary>쾌속이 끝값에 닿는 기혈 비율(마교 0.3).</summary>
        public float LowHealthFullAt;
    }

    /// <summary>혈공(기획서 5장 '마교 몸의 성장').</summary>
    public sealed class BloodArtDef
    {
        /// <summary>진기 구슬에서 받는 진기에 그 자리 혈운 농도의 이 제곱을 곱한다(0.25 = 네제곱근, 0 이면 혈공 진기 없음).</summary>
        public float XpDensityExponent = 0.25f;
        /// <summary>농도 덕에 더 받은 진기가 이만큼 쌓일 때마다 음양 게이지가 음 쪽으로 YinPerStep 기운다.</summary>
        public float ExtraXpPerStep = 150;
        public int YinPerStep = 1;
        /// <summary>이 오의를 쓰면 음양 게이지가 시작값으로 돌아온다.</summary>
        public string ResetUltimate = "";
        /// <summary>돌파 깨달음 가운데 마공 몫.</summary>
        public int DemonicInsightsPerBreakthrough = 1;
    }

    public sealed class PrologueBodyDef
    {
        public string Character = "";
        public string Region = "";
        /// <summary>서장 보스(enemies.json id).</summary>
        public string Boss = "";
        /// <summary>장삼전 고정 각성(evolutions.json id).</summary>
        public string FixedAwakening = "";
        /// <summary>각성 전에는 보스 기혈이 최대의 이 비율 아래로 안 내려간다. 닿으면 각성.</summary>
        public float BossHealthFloor = 0.1f;
        /// <summary>각성 순간 컷(art_queue id).</summary>
        public string AwakenCut = "";
        /// <summary>보스 처치 뒤 차례로 보여 줄 컷(art_queue id).</summary>
        public List<string> EndCuts = new List<string>();
    }

    public sealed class BirthTraitsDef
    {
        /// <summary>후보 하나에 붙는 성질 수가 0·1·2개일 비율.</summary>
        public List<float> CountWeights = new List<float>();
        public List<List<string>> Excludes = new List<List<string>>();
        public List<BirthTraitDef> List = new List<BirthTraitDef>();
    }

    public sealed class BirthTraitDef
    {
        public string Id = "";
        public string NameKo = "";
        public string Hanja = "";
        /// <summary>StatKind 이름 → 값(비율은 0.15 = 15%).</summary>
        public Dictionary<string, float> Stats = new Dictionary<string, float>();
        public int YinyangStart;
        /// <summary>윤회의 기 합계에 더하는 비율(둔재 0.3).</summary>
        public float RebirthQi;
    }

    public sealed class RebirthDef
    {
        public int Candidates = 3;
        public List<string> FirstRebirth = new List<string>();
        public bool FirstRebirthTraits;
        public int HomeSchoolTiers = 1;
        /// <summary>후보가 여자 몸일 비율(21장).</summary>
        public float FemaleRatio;
        /// <summary>늘 남자 몸인 문파(소림).</summary>
        public List<string> MaleOnlySects = new List<string>();
        public List<RebirthUpgradeDef> Upgrades = new List<RebirthUpgradeDef>();
    }

    public sealed class RebirthUpgradeDef
    {
        public string Id = "";
        public string NameKo = "";
        public string Effect = "";
        public int MaxLevel;
        public int BaseCost;
        /// <summary>참이면 비용 = 기본가 × 다음 레벨.</summary>
        public bool CostPerLevel;
    }

    public sealed class RebirthQiDef
    {
        public float PerOrbValue;
        public float LeftoverSilverRate;
        public int BossFirstKill;
        public int BossRepeatKill;
        public int FirstVisitRegion;
        public int FirstReachRealm;
        public List<RebirthQiTargetDef> Targets = new List<RebirthQiTargetDef>();
    }

    public sealed class RebirthQiTargetDef { public float Minute; public int Level; public int Qi; }

    public sealed class MasteryDef
    {
        public int RevisitInsightChoicesBonus;
        public bool ShortBreakthroughWhenRevisited;
        public bool LongAwakeningFirstTime;
    }

    public sealed class LifeEndDef
    {
        public List<string> Ways = new List<string>();
        public float ShadowFromMinute;
        public float ShadowStepMinutes;
        /// <summary>혈마의 그림자 한 단계의 세기(World/BloodShadow).</summary>
        public BloodShadowDef Shadow = new BloodShadowDef();
    }

    public sealed class BloodShadowDef
    {
        /// <summary>1단계 접촉 피해(0분·혈운 농도 1 값이 아니라 그대로 쓰는 값).</summary>
        public float ContactDamage;
        /// <summary>단계가 하나 오를 때 접촉 피해에 곱하는 값.</summary>
        public float DamageGrowth;
        /// <summary>1단계 이동속도(플레이어 기본 100 기준).</summary>
        public float MoveSpeed;
        public float SpeedPerStep;
        /// <summary>이동속도 상한. 0 이하면 상한 없음(데이터 기본, 2026-11-01 T-42).</summary>
        public float MaxSpeed;
        /// <summary>단계가 오를 때(2단계부터) 순간이동 전 예고(초). 그 자리에 핏자국이 먼저 번진다.</summary>
        public float TeleportWarningSeconds;
        /// <summary>순간이동해 내려앉는 거리(몸 뒤, 현경 축척 m). 0 이하면 순간이동 없음. 이미 더 가까우면 그대로.</summary>
        public float TeleportGap;
        /// <summary>계속 닿아 있을 때 다시 맞는 간격(초).</summary>
        public float HitIntervalSeconds;
        /// <summary>단계가 하나 오를 때 화면 위 모든 적 피해에 더하는 배율.</summary>
        public float EnemyDamagePerStep;
    }

    public sealed class CycleDef
    {
        public List<string> Resets = new List<string>();
        public List<string> Keeps = new List<string>();
        public BloodmoonDef Bloodmoon = new BloodmoonDef();
        public List<string> EndingsFirst = new List<string>();
        public List<string> EndingsSecond = new List<string>();
        public int TrueEndingDistinctSects;
        /// <summary>진엔딩 조건 하나: 모아야 하는 의서 권 수(기획서 4장·10장, 7).</summary>
        public int TrueEndingManuscripts;
    }

    public sealed class BloodmoonDef
    {
        public float EnemyHealth; public float EnemySpeed; public float Rewards; public bool ManuscriptsOnlyHere;
        /// <summary>은빛 등롱 하나를 부술 때 의서가 나올 확률(R4-08 0.15).</summary>
        public float ManuscriptChance;
    }
}
