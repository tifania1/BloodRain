#nullable enable
// 문파 고유 능력(기획서 5장 '한눈에 보기' 고유 능력 칸). 숫자는 cheonha.json sects[].traitEffect.
//   소림 금강불괴: 받는 피해 -2 → 호신(Armor) 2. Damage.Taken 이 호신을 빼니 같은 셈이다.
//   개방 동냥: 은자 +30%(Greed), 음식 회복 +50%(FoodHeal). 은자는 줍는 쪽이 (1 + Greed)를, 음식은 Drops.FoodHeal 에 (1 + FoodHeal)을 넘긴다.
//   남궁 세가의 긍지: 기혈 50% 이상이면 공력 +25%. 그래서 Bonus 가 지금 기혈 비율을 받는다.
//   당가 만독불침: 독 피해 무효(PoisonFogGimmick.HealthLoss 의 poisonImmune), 중독된 적에게 피해 +20%(DamageMultiplier).
//   수로채 수적의 감: 움직이는 동안 회피 15%(Evade), 물 지형 감속 무시(WaterGimmick.MoveSpeedMultiplier 의 ignoresWater).
//   무당 음양조화: 태극 구간 ±40 은 sects[].harmonyRange 가 맡고(Loadout 이 게이지에 넣는다), 조화 보너스 2배는 HarmonyBonusMultiplier.
//   화산 매화의 눈: 치명 확률 +10%(CritChance).
//   곤륜 설한기: 3m 안 적이 10% 느려짐(EnemySpeedMultiplier). 거리는 세계 m 이고, 축척을 따르게 하려면 부른 쪽이 scale 을 넘긴다.
//   마교 마인의 피: 회복 효과 -50%(HealingMultiplier). 회복 능력치는 RecoveryHeal, 음식은 Drops.FoodHeal·모닥불은 CampfireGimmick.Heal 의 healingMultiplier, 흡혈은 LifestealHeal 에 넘긴다. '기혈이 낮을수록 공격속도'는 쾌속(CooldownReduction)으로 기혈 100% 에서 0, 30% 이하에서 +0.5, 그 사이 선형(R-04c 2026-11-05 사람이 추천대로).
//                 쾌속 상한 0.6 은 StatSheet 가 자르니 심법 쾌속과 겹치면 금방 막힌다(docs/design/sim/frenzy.md 가안).
// 서장 진소운 몸은 None(아무것도 안 함)이다.
using System;
using System.Collections.Generic;
using BloodRain.Core.Data;

namespace BloodRain.Core.Build
{
    public sealed class SectTrait
    {
        static readonly SectTraitDef Empty = new SectTraitDef();
        readonly SectTraitDef _def;

        public SectTrait(SectTraitDef? def) { _def = def ?? Empty; }

        /// <summary>아무 효과도 없는 고유 능력(서장 몸, 숫자를 아직 안 옮긴 문파).</summary>
        public static SectTrait None { get; } = new SectTrait(null);

        public static SectTrait Of(SectDef? sect) => sect?.TraitEffect is { } d ? new SectTrait(d) : None;

        /// <summary>독 피해를 받지 않는가(당가).</summary>
        public bool PoisonImmune => _def.PoisonImmune;
        /// <summary>물 지형에서 느려지지 않는가(수로채).</summary>
        public bool IgnoresWater => _def.IgnoresWater;
        /// <summary>조화 보너스에 곱하는 수(무당 2, 나머지 1).</summary>
        public float HarmonyBonusMultiplier => _def.HarmonyBonusMultiplier;

        /// <summary>기혈 회복 전부에 곱하는 수(마교 0.5, 나머지 1). 회복 능력치·음식·흡혈·영약 모두.</summary>
        public float HealingMultiplier => _def.HealingMultiplier;

        /// <summary>회복량 하나에 고유 능력을 곱한다(마교면 절반). 음식·모닥불·영약처럼 따로 셈한 양을 넘긴다.</summary>
        public float Heal(float amount) => amount <= 0f ? 0f : amount * HealingMultiplier;

        /// <summary>이번 프레임 회복 능력치로 차는 기혈(초당 Recovery x dt, 마교면 절반). 음수 회복·dt 는 0.</summary>
        public float RecoveryHeal(StatSheet stats, float dt) => dt <= 0f ? 0f : Heal(stats.Recovery * dt);

        /// <summary>몸에서 distance(세계 m) 떨어진 적의 이동속도 배율. 곤륜이면 반경 3m(x scale) 안에서 0.9, 나머지는 1.</summary>
        public float EnemySpeedMultiplier(float distance, float scale = 1f) =>
            _def.NearEnemySlow > 0f && distance <= _def.NearEnemySlowRadius * scale ? 1f - _def.NearEnemySlow : 1f;

        /// <summary>EnemySpeedMultiplier 가 느리게 하는 반경(세계 m, x scale). 곤륜이 아니면 0. 적 걸음을 잡(job) 안에서 셈하는 쪽이 쓴다.</summary>
        public float NearEnemySlowRadius(float scale = 1f) => _def.NearEnemySlow > 0f ? _def.NearEnemySlowRadius * scale : 0f;

        /// <summary>지금 몸에 붙는 능력치. healthRatio 는 지금 기혈 / 최대 기혈(0~1), moving 은 이번 프레임에 움직였는가.</summary>
        public StatBonus Bonus(float healthRatio = 1f, bool moving = false)
        {
            var b = new StatBonus();
            Add(b, _def.Stats);
            if (_def.AboveHealthRatio > 0f && healthRatio >= _def.AboveHealthRatio) Add(b, _def.AboveHealthStats);
            if (moving) Add(b, _def.MovingStats);
            float frenzy = LowHealthCooldownReduction(healthRatio);
            if (frenzy > 0f) b.Add(StatKind.CooldownReduction, frenzy);
            return b;
        }

        /// <summary>기혈이 낮을수록 붙는 쾌속(마교). 100% 에서 0, lowHealthFullAt(30%) 이하에서 끝값(0.5), 그 사이 선형. 없는 몸은 0.</summary>
        public float LowHealthCooldownReduction(float healthRatio)
        {
            float max = _def.LowHealthCooldownReduction, full = _def.LowHealthFullAt;
            if (max <= 0f) return 0f;
            if (full >= 1f) return max;
            float t = (1f - Math.Clamp(healthRatio, 0f, 1f)) / (1f - full);
            return max * Math.Clamp(t, 0f, 1f);
        }

        /// <summary>무공이 적에게 주는 피해에 곱하는 값. 중독된 적이면 1 + poisonedTargetDamage(당가 1.2), 아니면 1.</summary>
        public float DamageMultiplier(bool targetPoisoned) => targetPoisoned ? 1f + _def.PoisonedTargetDamage : 1f;

        /// <summary>태극(조화) 구간에서 붙는 쾌속(balance.json yinyang.harmony.cooldownReduction x 배수). 무당이면 0.2.</summary>
        public float HarmonyCooldownReduction(YinYangHarmonyDef harmony) => harmony.CooldownReduction * HarmonyBonusMultiplier;

        /// <summary>태극(조화) 구간에서 오의 게이지가 더 빨리 차는 비율(yinyang.harmony.ultimateChargeBonus x 배수). 무당이면 0.5.</summary>
        public float HarmonyUltimateChargeBonus(YinYangHarmonyDef harmony) => harmony.UltimateChargeBonus * HarmonyBonusMultiplier;

        static void Add(StatBonus b, Dictionary<string, float> stats)
        {
            foreach (var kv in stats) b.Add(Stats.Parse(kv.Key), kv.Value);
        }
    }

    /// <summary>회피(수로채 고유 능력). 피격 판정 직전에 한 번 굴린다.</summary>
    public static class Evade
    {
        /// <summary>이번 피격을 피했는가. chance 는 StatSheet.Evade(0~1).</summary>
        public static bool Roll(float chance, Random rng) => chance > 0f && rng.NextDouble() < chance;
    }
}
