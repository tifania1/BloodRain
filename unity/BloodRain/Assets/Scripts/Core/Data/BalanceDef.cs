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

    public sealed class EvolutionRuleDef
    {
        public int MinPlayerLevel = 20;
        public bool RequiresChest = true;
    }

    public sealed class YinYangDef
    {
        public int Min = -100;
        public int Max = 100;
        public int WeaponPerLevel = 5;
        public int PassivePerLevel = 8;
        public int HarmonyRange = 25;
        public int ExtremeFrom = 70;
    }

    public sealed class EnemyScalingDef
    {
        public float HealthPerMinute = 0.08f;
        public float DamagePerMinute = 0.03f;
        public Dictionary<string, float> ChapterMultiplier = new Dictionary<string, float>();
    }

    public sealed class SpawnTargetDef
    {
        public List<float[]> Points = new List<float[]>();
        public float SpawnFractionPerSecond = 0.3f;
        public float StopSpawningAtMinute = 19;
        public float BossAtMinute = 20;
    }

    public sealed class ContactDef
    {
        public float SameEnemyIntervalSeconds = 0.35f;
        public float MinDamage = 1;
    }

    public sealed class UltimateChargeDef
    {
        public float Max = 100;
        public float PerKill = 0.4f;
        public float PerHit = 3;
        public float PerElite = 15;
    }

    public sealed class BalanceDef
    {
        public XpCurveDef XpCurve = new XpCurveDef();
        public List<RealmDef> Realms = new List<RealmDef>();
        public SlotsDef Slots = new SlotsDef();
        public LevelsDef Levels = new LevelsDef();
        public LevelUpDef LevelUp = new LevelUpDef();
        public EvolutionRuleDef Evolution = new EvolutionRuleDef();
        public YinYangDef Yinyang = new YinYangDef();
        public UltimateChargeDef UltimateCharge = new UltimateChargeDef();
        public ContactDef Contact = new ContactDef();
        public EnemyScalingDef EnemyScaling = new EnemyScalingDef();
        public SpawnTargetDef SpawnTarget = new SpawnTargetDef();
    }
}
