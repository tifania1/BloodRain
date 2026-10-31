#nullable enable
// balance.json 구조. 값은 기획서 '11. 밸런스 기본값'과 맞춘다.
using System.Collections.Generic;

namespace BloodRain.Core.Data
{
    public sealed class XpSegment
    {
        public int FromLevel;
        public int ToLevel;
        public int Base;
        public int Step;
        public int Anchor;
    }

    public sealed class XpCurveDef
    {
        public List<XpSegment> Segments = new List<XpSegment>();
    }

    public sealed class RealmDef
    {
        public string Id = "";
        public string NameKo = "";
        public int Level;
        public string? Reward;
        /// <summary>이 경지가 열리는 모드. null 이면 모든 생. 천하 구조에선 현경도 모든 생에서 열려 지금은 쓰는 경지가 없다.</summary>
        public List<string>? OnlyIn;
    }

    public sealed class SlotsDef
    {
        public int Weapons = 6;
        public int Passives = 6;
        public string ExtraSlotAtRealm = "chojeoljeong";
    }

    public sealed class LevelsDef
    {
        public int WeaponMax = 8;
        public int PassiveMax = 5;
    }

    public sealed class LevelUpDef
    {
        public int BaseChoices = 3;
        public int MaxChoices = 4;
        public float LuckForFourthChoice = 50;
    }

    /// <summary>절기 규칙. 경지마다 담을 수 있는 절기 수(그릇)가 정해져 있고, 문턱에 선 무공만 깨어난다.</summary>
    public sealed class EvolutionRuleDef
    {
        /// <summary>경지 id → 그 경지까지 담을 수 있는 절기 수(누적). 없는 경지는 0.</summary>
        public Dictionary<string, int> Vessels = new Dictionary<string, int>();
        /// <summary>문턱 조건: 짝 심법 최소 레벨(무공은 최고 단계).</summary>
        public int PairPassiveMinLevel = 3;
        /// <summary>깨어나는 순간: breakthrough(그릇이 늘어나는 돌파), chest(보갑).</summary>
        public List<string> Triggers = new List<string> { "breakthrough", "chest" };
    }

    public sealed class YinYangDef
    {
        public int Min = -100;
        public int Max = 100;
        public int WeaponPerLevel = 5;
        public int PassivePerLevel = 8;
        public int HarmonyRange = 25;
        public int ExtremeFrom = 70;
        public YinYangHarmonyDef Harmony = new YinYangHarmonyDef();
        public YinYangBiasDef Bias = new YinYangBiasDef();
        public YinYangExtremeDef Extreme = new YinYangExtremeDef();
    }

    /// <summary>태극(조화) 구간 보너스.</summary>
    public sealed class YinYangHarmonyDef
    {
        public float CooldownReduction = 0.10f;
        public float UltimateChargeBonus = 0.25f;
    }

    /// <summary>편향 구간: 기운 쪽 속성 무공 피해 보너스.</summary>
    public sealed class YinYangBiasDef
    {
        public float AspectDamageBonus = 0.15f;
    }

    /// <summary>극성 구간: 피해 보너스가 커지는 대신 기혈역류가 온다(Combat.Backlash).</summary>
    public sealed class YinYangExtremeDef
    {
        public float AspectDamageBonus = 0.40f;
        /// <summary>극성에 머문 시간이 이만큼 쌓일 때마다 역류가 온다(초).</summary>
        public float BacklashIntervalSeconds = 60;
        /// <summary>역류가 이어지는 시간(초).</summary>
        public float BacklashSeconds = 3;
        /// <summary>역류 동안 이동속도에서 빼는 비율. 0.5면 -50%.</summary>
        public float BacklashSlow = 0.5f;
        /// <summary>끝까지 버텼을 때 받는 피해(최대 기혈 대비).</summary>
        public float BacklashDamageMaxHealthRatio = 0.05f;
        /// <summary>역류가 시작된 뒤 이 시간 안에 경공을 쓰면 끊긴다(초).</summary>
        public float DashCancelWindowSeconds = 1.0f;
        /// <summary>끊었을 때 오의 게이지에 그대로 더하는 양(배율 없음).</summary>
        public float DashCancelUltimateCharge = 50;
    }

    /// <summary>각성 연출 한 벌(긴 것 / 짧은 것)의 장면 시각. 연출 시작부터 실제 시간(초), 그 연출에 없는 장면은 null.</summary>
    public sealed class AwakeningTimelineDef
    {
        public float DurationSeconds;
        /// <summary>문턱: 무공 칸 글자가 날아와 몸에 스민다.</summary>
        public float? GlyphSeconds;
        /// <summary>균열: 금빛 균열이 위아래로 화면을 가른다.</summary>
        public float? CrackSeconds;
        /// <summary>이름: 한자 이름 붓질이 시작된다.</summary>
        public float NameSeconds;
        /// <summary>낙관: 絶 낙관이 찍히고 무공 칸이 절기로 바뀐다.</summary>
        public float StampSeconds;
        /// <summary>파동: 판정(잡몹 처치, 정예·보스 깎기, 적 탄 지우기)이 이 순간 일어난다.</summary>
        public float ShockSeconds;
    }

    /// <summary>절기 각성(Combat.Awakening). 기획서 8장 '절기 그릇과 각성'의 각성 연출 표, docs/design/vfx.md.</summary>
    public sealed class AwakeningDef
    {
        public AwakeningTimelineDef Long = new AwakeningTimelineDef
        {
            DurationSeconds = 3.5f, GlyphSeconds = 0.45f, CrackSeconds = 0.95f, NameSeconds = 1.15f, StampSeconds = 1.8f, ShockSeconds = 2.3f,
        };
        public AwakeningTimelineDef Short = new AwakeningTimelineDef
        {
            DurationSeconds = 1.5f, NameSeconds = 0.1f, StampSeconds = 0.42f, ShockSeconds = 0.64f,
        };
        /// <summary>파동 전까지 세상 시간 배율.</summary>
        public float WorldTimeScale = 0.04f;
        /// <summary>연출 시작부터 이 시간 동안 1 → WorldTimeScale 로 느려진다(실제 시간).</summary>
        public float TimeScaleInSeconds = 0.18f;
        /// <summary>파동 뒤 이 시간 동안 WorldTimeScale → 1 로 돌아온다(실제 시간).</summary>
        public float TimeScaleOutSeconds = 0.25f;
        /// <summary>파동 반경 = 화면 대각선(월드 단위) × 이 값.</summary>
        public float ShockRadiusViewDiagonal = 0.62f;
        public float EliteMaxHealthRatio = 0.15f;
        public float BossMaxHealthRatio = 0.04f;
        /// <summary>정예를 밀어내는 힘(프로토타입 단위). 보스는 밀리지 않는다.</summary>
        public float EliteKnockback = 8f;
        public bool ClearEnemyProjectiles = true;
        /// <summary>파동 때 깨어난 절기의 재사용 대기를 0으로.</summary>
        public bool ResetCooldownOnShock = true;
        /// <summary>파동 뒤 적이 새로 몰려오지 않는 시간(세상 시간).</summary>
        public float SpawnPauseSeconds = 2.5f;
        /// <summary>파동 뒤 금빛 여운 시간(세상 시간).</summary>
        public float AfterglowSeconds = 8f;
        /// <summary>여운 동안 그 절기 피해 배율.</summary>
        public float AfterglowDamageMultiplier = 1.5f;
        /// <summary>무적 = 연출 길이 + 이 값. 시작할 때 걸고 세상 시간으로 줄어든다(프로토타입과 같다).</summary>
        public float InvulnerableExtraSeconds = 0.8f;
    }

    public sealed class EnemyScalingDef
    {
        public float HealthPerMinute = 0.08f;
        public float DamagePerMinute = 0.03f;
        public Dictionary<string, float> ChapterMultiplier = new Dictionary<string, float>();
    }

    /// <summary>화면 위 목표 적 수. 생성을 멈추는 구간은 흐름(chapters.json)마다 따로 정한다.</summary>
    public sealed class SpawnTargetDef
    {
        /// <summary>[분, 마리]. 마지막 점 뒤로는 마지막 값.</summary>
        public List<float[]> Points = new List<float[]>();
        public float SpawnFractionPerSecond = 0.3f;
        /// <summary>생성 고리(화면 대각선 배수). 화면 기준이라 축척이 바뀌어도 그대로다.</summary>
        public float RingMinDiagonal = 0.6f;
        public float RingMaxDiagonal = 0.75f;
        /// <summary>플레이어에게서 이만큼(대각선 배수) 넘게 멀어진 적은 진행 방향 반대편으로 옮긴다.</summary>
        public float RecycleBeyondDiagonal = 1.5f;
    }

    public sealed class ContactDef
    {
        public float SameEnemyIntervalSeconds = 0.35f;
        public float MinDamage = 1;
    }

    /// <summary>경공 대시(기획서 8장). 움직임은 Unity 쪽이고 Core 는 숫자만 들고 있다.</summary>
    public sealed class DashDef
    {
        public float Distance = 4f;
        public float Duration = 0.25f;
        /// <summary>무적 시간(초).</summary>
        public float Iframes = 0.2f;
        public int Charges = 2;
        public float RechargeSeconds = 4f;
    }

    public sealed class UltimateChargeDef
    {
        public float Max = 100;
        public float PerKill = 0.4f;
        public float PerHit = 3;
        public float PerElite = 15;
        /// <summary>레벨업 화면 '정진(건너뛰기)'으로 얻는 양. 배율을 받지 않는다.</summary>
        public float PerSkip = 20;
    }

    public sealed class StatsDef
    {
        public float MagnetBase = 1.5f;
        public float CooldownReductionCap = 0.6f;
    }

    /// <summary>연무장 한 항목. Stat은 Core.Build.StatKind 이름.</summary>
    public sealed class TrainingItemDef
    {
        public string Stat = "";
        public float PerLevel;
        public int MaxLevel = 1;
        public int BaseCost;
    }

    public sealed class TrainingDef
    {
        public float CostGrowthPerBought = 0.1f;
        public List<TrainingItemDef> Items = new List<TrainingItemDef>();
    }

    public sealed class BalanceDef
    {
        public XpCurveDef XpCurve = new XpCurveDef();
        public List<RealmDef> Realms = new List<RealmDef>();
        public SlotsDef Slots = new SlotsDef();
        public LevelsDef Levels = new LevelsDef();
        public LevelUpDef LevelUp = new LevelUpDef();
        public EvolutionRuleDef Evolution = new EvolutionRuleDef();
        public AwakeningDef Awakening = new AwakeningDef();
        public YinYangDef Yinyang = new YinYangDef();
        public UltimateChargeDef UltimateCharge = new UltimateChargeDef();
        public ContactDef Contact = new ContactDef();
        public DashDef Dash = new DashDef();
        public EnemyScalingDef EnemyScaling = new EnemyScalingDef();
        public SpawnTargetDef SpawnTarget = new SpawnTargetDef();
        public StatsDef Stats = new StatsDef();
        public TrainingDef Training = new TrainingDef();
    }
}
