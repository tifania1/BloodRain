#nullable enable
// 진전 단계의 effect(synergies.json tiers[].effect + params, 기획서 20장 진전 표).
//   능력치 덧셈은 SynergyState.StatBonus(), 태극 구간은 SynergyState.HarmonyRange 가 따로 맡는다.
//   여기는 곱하는 값만 숫자로 내준다. 꽃잎·검기·독 번짐 같은 모양은 아직 effect 가 없고 Unity 쪽 몫이다.
//
//   sword_damage      남궁 2  검 계열(WeaponDef.Category "sword") 무공 피해 +damageBonus
//   low_health_might  개방 3  기혈 비율 < healthThreshold 면 무공 피해 +damageBonus
//   jiangshi_damage   곤륜 3  강시 계열(EnemyDef.Family "jiangshi") 적에게 피해 +damageBonus
//   dash_drag         수로채 3 경공 거리 +distanceBonus(끌고 가는 모양은 Unity)
//   talisman_freeze   곤륜 2  정신부 멈춤 시간 × freezeMultiplier
//   harmony_deflect   무당 3  태극 구간이면 받는 피해 -damageCut, 줄인 몫 × reflectMultiplier 를 부딪힌 적에게
//   blood_trade       마도 2  흡혈 × lifestealMultiplier, 음식 회복 × foodHealMultiplier
//
// 시너지가 바뀔 때(레벨업에서 고른 뒤) SynergyEffects.Of(Synergy.Compute(…)) 로 새로 만든다.
using System.Collections.Generic;
using BloodRain.Core.Data;

namespace BloodRain.Core.Build
{
    public sealed class SynergyEffects
    {
        /// <summary>코드가 읽는 effect 이름. 데이터에 이 밖의 이름이 있으면 테스트가 잡는다.</summary>
        public static readonly IReadOnlyList<string> Known = new[]
        {
            "sword_damage", "low_health_might", "jiangshi_damage", "dash_drag",
            "talisman_freeze", "harmony_deflect", "blood_trade",
        };

        readonly Dictionary<string, SchoolTierDef> _active = new Dictionary<string, SchoolTierDef>();

        SynergyEffects() { }

        public static SynergyEffects Of(SynergyState synergy)
        {
            var e = new SynergyEffects();
            foreach (var s in synergy.Schools)
                foreach (var t in s.School.Tiers)
                    if (t.Count <= s.TierCount && t.Effect != null) e._active[t.Effect] = t;
            return e;
        }

        public static readonly SynergyEffects None = new SynergyEffects();

        public bool Has(string effect) => _active.ContainsKey(effect);
        SchoolTierDef? Find(string effect) => _active.TryGetValue(effect, out var t) ? t : null;

        /// <summary>무공 한 방 피해에 곱하는 값(남궁 2 검 계열, 개방 3 낮은 기혈).</summary>
        public float WeaponDamageMultiplier(WeaponDef weapon, float healthRatio)
        {
            float m = 1f;
            var sword = Find("sword_damage");
            if (sword != null && weapon.Category == "sword") m *= 1f + sword.Param("damageBonus");
            var low = Find("low_health_might");
            if (low != null && healthRatio < low.Param("healthThreshold")) m *= 1f + low.Param("damageBonus");
            return m;
        }

        /// <summary>그 적에게 주는 피해에 곱하는 값(곤륜 3 강시 계열).</summary>
        public float EnemyDamageMultiplier(EnemyDef enemy)
        {
            var j = Find("jiangshi_damage");
            return j != null && enemy.Family == "jiangshi" ? 1f + j.Param("damageBonus") : 1f;
        }

        /// <summary>경공 거리에 곱하는 값(수로채 3).</summary>
        public float DashDistanceMultiplier => Find("dash_drag") is SchoolTierDef t ? 1f + t.Param("distanceBonus") : 1f;

        /// <summary>정신부 멈춤 시간에 곱하는 값(곤륜 2).</summary>
        public float FreezeMultiplier => Find("talisman_freeze") is SchoolTierDef t ? t.Param("freezeMultiplier") : 1f;

        /// <summary>흡혈에 곱하는 값(마도 2).</summary>
        public float LifestealMultiplier => Find("blood_trade") is SchoolTierDef t ? t.Param("lifestealMultiplier") : 1f;

        /// <summary>음식 회복에 곱하는 값(마도 2).</summary>
        public float FoodHealMultiplier => Find("blood_trade") is SchoolTierDef t ? t.Param("foodHealMultiplier") : 1f;

        /// <summary>
        /// 무당 3 사량발천근. 호신·경지 배율까지 뺀 받는 피해를 넘기면 실제로 받을 피해와 부딪힌 적에게 되돌릴 피해를 준다.
        /// 태극 구간이 아니거나 단계가 없으면 그대로, 되돌림 0.
        /// </summary>
        public (float taken, float reflect) Deflect(float taken, bool inHarmony)
        {
            var t = Find("harmony_deflect");
            if (t == null || !inHarmony) return (taken, 0f);
            float cut = taken * t.Param("damageCut");
            return (taken - cut, cut * t.Param("reflectMultiplier"));
        }
    }
}
