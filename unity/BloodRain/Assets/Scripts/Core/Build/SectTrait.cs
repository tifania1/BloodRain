#nullable enable
// 문파 고유 능력(기획서 5장 '한눈에 보기' 고유 능력 칸). 숫자는 cheonha.json sects[].traitEffect.
//   소림 금강불괴: 받는 피해 -2 → 호신(Armor) 2. Damage.Taken 이 호신을 빼니 같은 셈이다.
//   개방 동냥: 은자 +30%(Greed), 음식 회복 +50%(FoodHeal). 은자는 줍는 쪽이 (1 + Greed)를, 음식은 Drops.FoodHeal 에 (1 + FoodHeal)을 넘긴다.
//   남궁 세가의 긍지: 기혈 50% 이상이면 공력 +25%. 그래서 Bonus 가 지금 기혈 비율을 받는다.
//   당가 만독불침: 독 피해 무효(PoisonFogGimmick.HealthLoss 의 poisonImmune), 중독된 적에게 피해 +20%(DamageMultiplier).
//   수로채 수적의 감: 움직이는 동안 회피 15%(Evade), 물 지형 감속 무시(WaterGimmick.MoveSpeedMultiplier 의 ignoresWater).
//   무당 음양조화: 태극 구간 ±40 은 sects[].harmonyRange 가 맡고(Loadout 이 게이지에 넣는다), 조화 보너스 2배는 HarmonyBonusMultiplier.
// 서장 진소운 몸과 아직 숫자를 옮기지 않은 문파는 None(아무것도 안 함)이다.
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

        /// <summary>지금 몸에 붙는 능력치. healthRatio 는 지금 기혈 / 최대 기혈(0~1), moving 은 이번 프레임에 움직였는가.</summary>
        public StatBonus Bonus(float healthRatio = 1f, bool moving = false)
        {
            var b = new StatBonus();
            Add(b, _def.Stats);
            if (_def.AboveHealthRatio > 0f && healthRatio >= _def.AboveHealthRatio) Add(b, _def.AboveHealthStats);
            if (moving) Add(b, _def.MovingStats);
            return b;
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
