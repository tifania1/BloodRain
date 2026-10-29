#nullable enable
// cheonha.json: 천하 한 장(기획서 7장)과 환혼·윤회의 기(10·21장). 규칙 계산은 Core/World, Core/Meta 에서 한다.
using System.Collections.Generic;
using System.Linq;

namespace BloodRain.Core.Data
{
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

        public RegionDef Region(string id) => Regions.First(r => r.Id == id);
        public SectDef Sect(string id) => Sects.First(s => s.Id == id);
        public BirthTraitDef Trait(string id) => BirthTraits.List.First(t => t.Id == id);
        /// <summary>땅의 주인이 있는 지역(봉마곡 빼고 일곱).</summary>
        public IEnumerable<RegionDef> BossRegions => Regions.Where(r => r.Boss != null && !r.Final);
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
    }

    public sealed class BreakthroughDef
    {
        public float TimeScale;
        public float ZoomFromSeconds;
        public float ZoomToSeconds;
        public float SpawnHoldSeconds;
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
        public float SpawnTargetScale;
        public float HealthPerMinute;
        public float NormalizeAt20;
        public BossFirstKillDef FirstKill = new BossFirstKillDef();
        public BossRepeatKillDef RepeatKill = new BossRepeatKillDef();
        public FinalRequiresDef FinalRequires = new FinalRequiresDef();
    }

    public sealed class BossFirstKillDef { public int RebirthQi; public bool SealStone; public bool UnlockSect; }
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
        /// <summary>"start" 또는 "boss:&lt;지역 id&gt;"(그 땅의 주인 첫 처치).</summary>
        public string Unlock = "start";

        public string? UnlockBossRegion => Unlock.StartsWith("boss:") ? Unlock.Substring(5) : null;
    }

    public sealed class PrologueBodyDef { public string Character = ""; public string Region = ""; }

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
    }

    public sealed class CycleDef
    {
        public List<string> Resets = new List<string>();
        public List<string> Keeps = new List<string>();
        public BloodmoonDef Bloodmoon = new BloodmoonDef();
        public List<string> EndingsFirst = new List<string>();
        public List<string> EndingsSecond = new List<string>();
        public int TrueEndingDistinctSects;
    }

    public sealed class BloodmoonDef { public float EnemyHealth; public float EnemySpeed; public float Rewards; public bool ManuscriptsOnlyHere; }
}
