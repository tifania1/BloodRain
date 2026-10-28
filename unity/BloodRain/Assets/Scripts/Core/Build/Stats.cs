#nullable enable
// 능력치 합산. 심법 레벨과 연무장 레벨(그리고 나중에 깨달음·진전 같은 덧셈)을 모아
// 캐릭터 기본값 위에 얹는다. 기획서 '8. 전투 시스템' 능력치 표, '10. 성장·메타 시스템' 연무장 표.
using System;
using System.Collections.Generic;
using BloodRain.Core.Data;

namespace BloodRain.Core.Build
{
    /// <summary>passives.json·balance.json training 의 stat 이름. 이름을 바꾸면 JSON도 바꾼다.</summary>
    public enum StatKind
    {
        // 기획서 능력치 14종
        MaxHealth,          // 기혈: 비율(+0.10 = +10%)
        Recovery,           // 회복: 초당 기혈, 그대로 더함
        Armor,              // 호신: 받는 피해에서 빼는 값
        MoveSpeed,          // 신법: 비율
        Might,              // 공력: 비율
        CooldownReduction,  // 쾌속: 비율, 상한 있음
        Area,               // 기세: 비율
        Amount,             // 분광: 투사체 수
        Duration,           // 지속: 비율
        ProjectileSpeed,    // 탄속: 비율
        Magnet,             // 흡기: 비율
        Growth,             // 심득: 비율
        Luck,               // 기연: 그대로 더함
        Revival,            // 재기: 부활 횟수

        // 심법에만 붙는 부가 능력치
        Knockback, EliteDamage, CritChance, BurnDamage, ExplosionArea, DotDamage, FoodHeal, Lifesteal,

        // 연무장에만 있는 항목
        Greed,              // 은자 획득 비율
        UltimateCharge,     // 진기(오의) 충전 비율
        DashCharges, Rerolls, Banishes, Skips,
    }

    /// <summary>더한 값의 원본 합. 비율 능력치는 0.10 = +10%.</summary>
    public sealed class StatBonus
    {
        static readonly int Count = Enum.GetValues(typeof(StatKind)).Length;
        readonly float[] _sum = new float[Count];

        public float this[StatKind k] => _sum[(int)k];
        public void Add(StatKind k, float value) => _sum[(int)k] += value;

        public void Add(StatBonus other)
        {
            for (int i = 0; i < Count; i++) _sum[i] += other._sum[i];
        }
    }

    /// <summary>캐릭터 기본값에 덧셈을 얹은 최종 능력치.</summary>
    public sealed class StatSheet
    {
        public StatBonus Bonus { get; }

        public float MaxHealth { get; }
        public float Recovery { get; }
        public float Armor { get; }
        public float MoveSpeed { get; }
        public float Might { get; }
        public float CooldownReduction { get; }
        public float Area { get; }
        public int Amount { get; }
        public float Duration { get; }
        public float ProjectileSpeed { get; }
        public float Magnet { get; }
        public float Growth { get; }
        public float Luck { get; }
        public int Revival { get; }

        public StatSheet(CharacterDef character, StatsDef rules, StatBonus bonus)
        {
            Bonus = bonus;
            MaxHealth = character.MaxHealth * (1 + bonus[StatKind.MaxHealth]);
            Recovery = bonus[StatKind.Recovery];
            Armor = bonus[StatKind.Armor];
            MoveSpeed = character.MoveSpeed * (1 + bonus[StatKind.MoveSpeed]);
            Might = 1 + bonus[StatKind.Might];
            CooldownReduction = Math.Min(rules.CooldownReductionCap, bonus[StatKind.CooldownReduction]);
            Area = 1 + bonus[StatKind.Area];
            Amount = (int)Math.Round(bonus[StatKind.Amount]);
            Duration = 1 + bonus[StatKind.Duration];
            ProjectileSpeed = 1 + bonus[StatKind.ProjectileSpeed];
            Magnet = rules.MagnetBase * (1 + bonus[StatKind.Magnet]);
            Growth = 1 + bonus[StatKind.Growth];
            Luck = bonus[StatKind.Luck];
            Revival = (int)Math.Round(bonus[StatKind.Revival]);
        }
    }

    public static class Stats
    {
        public static StatKind Parse(string name)
        {
            if (Enum.TryParse<StatKind>(name, false, out var k)) return k;
            throw new InvalidOperationException($"모르는 능력치 이름입니다: {name}");
        }

        /// <summary>심법 하나가 그 레벨에서 주는 덧셈. atLevels 가 있으면 그 레벨 이하 값을 모두 더한다.</summary>
        public static StatBonus FromPassive(PassiveDef passive, int level)
        {
            var b = new StatBonus();
            foreach (var e in passive.Effects)
            {
                var kind = Parse(e.Stat);
                float v = e.PerLevel * level;
                if (e.AtLevels != null)
                    foreach (var kv in e.AtLevels)
                        if (int.Parse(kv.Key) <= level) v += kv.Value;
                b.Add(kind, v);
            }
            return b;
        }

        /// <summary>연무장 레벨(stat 이름 → 산 레벨). 최대 레벨을 넘는 값은 최대로 자른다.</summary>
        public static StatBonus FromTraining(TrainingDef training, IReadOnlyDictionary<string, int> levels)
        {
            var b = new StatBonus();
            foreach (var item in training.Items)
            {
                if (!levels.TryGetValue(item.Stat, out var lv) || lv <= 0) continue;
                b.Add(Parse(item.Stat), item.PerLevel * Math.Min(lv, item.MaxLevel));
            }
            return b;
        }

        /// <summary>한 판 시작 능력치: 캐릭터 + 심법 + 연무장 (+ 깨달음·진전 같은 덧셈은 extra 로).</summary>
        public static StatSheet Compute(GameData data, CharacterDef character,
            IReadOnlyDictionary<string, int> passiveLevels,
            IReadOnlyDictionary<string, int> trainingLevels,
            StatBonus? extra = null)
        {
            var total = new StatBonus();
            foreach (var kv in passiveLevels)
                total.Add(FromPassive(data.Passive(kv.Key), kv.Value));
            total.Add(FromTraining(data.Balance.Training, trainingLevels));
            if (extra != null) total.Add(extra);
            return new StatSheet(character, data.Balance.Stats, total);
        }

        /// <summary>연무장 다음 레벨 값. 기본가 x 다음 레벨 x (1 + 0.1 x 지금까지 산 총 레벨 수). 이미 최대면 null.</summary>
        public static int? TrainingCost(TrainingDef training, string stat, IReadOnlyDictionary<string, int> levels)
        {
            TrainingItemDef? item = null;
            foreach (var it in training.Items) if (it.Stat == stat) { item = it; break; }
            if (item == null) throw new InvalidOperationException($"연무장에 없는 항목입니다: {stat}");
            levels.TryGetValue(stat, out var current);
            if (current >= item.MaxLevel) return null;
            int bought = 0;
            foreach (var kv in levels) bought += Math.Max(0, kv.Value);
            double cost = item.BaseCost * (current + 1) * (1 + training.CostGrowthPerBought * bought);
            return (int)Math.Round(cost);
        }
    }
}
