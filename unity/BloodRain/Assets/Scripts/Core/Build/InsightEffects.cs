#nullable enable
// 고른 깨달음의 효과를 숫자로 내준다(기획서 3장 깨달음, 5장 마공 깨달음). 값은 insights.json params.
// InsightPicks 가 '무엇을 골랐나'라면 이 클래스는 '그래서 몸에 무엇이 붙나'다. Unity 쪽은 효과 이름으로 가르지 말고 여기 값을 쓴다.
//   일검파만  first_weapon_damage  처음 들고 시작한 무공 피해 × (1 + damageBonus)
//   운기조식  still_regen          stillSeconds 동안 안 움직이면 초당 최대 기혈 × maxHealthPerSecond 회복
//   이기어검  melee_range          weaponBehaviors 에 든 무공 범위 × (1 + rangeBonus)
//   금종조    periodic_block       맞을 때 막을 수 있으면 막고 intervalSeconds 동안 다시 못 막는다(첫 피해는 바로 막는다)
//   천리추종  magnet_multiplier    흡기 범위 × multiplier
//   역근      yinyang_reset        고를 때 게이지를 resetTo 로, 이후 무공·심법이 기울이는 양 × tiltMultiplier
//   살기충천  kill_stack_might     고른 뒤 처치 killsPerStack 마다 공력 + mightPerStack (최대 mightMax)
//   혈해공    might_for_health     공력 + mightBonus, 최대 기혈 - maxHealthPenalty (비율 덧셈)
//   흡혈대법  lifesteal_no_heal    흡혈 + lifesteal, 흡혈 말고 다른 회복 × 0 (운기조식 포함)
//   혈광      low_health_frenzy    기혈 비율 < healthThreshold 이면 쾌속 + cooldownReduction, 받는 피해 × (1 + damageTaken)
//   심마      inner_demon_might    심마로 기운 횟수(마지막 천마해체 뒤)마다 공력 + mightPerStep (최대 mightMax)
using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Progression;

namespace BloodRain.Core.Build
{
    public sealed class InsightEffects
    {
        readonly string _firstWeapon;
        readonly List<InsightDef> _taken = new List<InsightDef>();
        float _still;
        float _blockCooldown;
        int _simmaBaseline;
        int _killsAtSalgi;

        /// <param name="firstWeaponId">일검파만이 걸리는 무공. 몸이 들고 시작한 무공(CharacterDef.StartWeapon).</param>
        public InsightEffects(string firstWeaponId)
        {
            _firstWeapon = firstWeaponId;
        }

        public IReadOnlyList<InsightDef> Taken => _taken;

        InsightDef? Find(string effect) => _taken.FirstOrDefault(i => i.Effect == effect);

        public bool Has(string effect) => Find(effect) != null;

        /// <summary>깨달음 하나를 얻는다. 역근처럼 고른 순간 걸리는 효과는 여기서 건다.</summary>
        /// <param name="killsNow">고른 때의 처치 수. 살기충천은 이 뒤 처치만 센다.</param>
        public void Take(InsightDef insight, YinYangGauge gauge, int killsNow = 0)
        {
            if (_taken.Any(i => i.Id == insight.Id))
                throw new InvalidOperationException($"이미 얻은 깨달음입니다: {insight.Id}");
            _taken.Add(insight);
            if (insight.Effect == "yinyang_reset")
            {
                gauge.ResetTo((int)Math.Round(insight.Param("resetTo")));
                gauge.SetTiltMultiplier(insight.Param("tiltMultiplier"));
            }
            if (insight.Effect == "kill_stack_might") _killsAtSalgi = killsNow;
        }

        /// <summary>그 무공 피해에 곱하는 값(일검파만).</summary>
        public float WeaponDamageMultiplier(string weaponId)
        {
            var d = Find("first_weapon_damage");
            return d != null && weaponId == _firstWeapon ? 1f + d.Param("damageBonus") : 1f;
        }

        /// <summary>그 무공 범위에 곱하는 값(이기어검, 근접 behavior 만).</summary>
        public float WeaponRangeMultiplier(WeaponDef weapon)
        {
            var d = Find("melee_range");
            return d != null && d.WeaponBehaviors.Contains(weapon.Behavior) ? 1f + d.Param("rangeBonus") : 1f;
        }

        /// <summary>흡기 범위에 곱하는 값(천리추종). StatSheet.Magnet 뒤에 곱한다.</summary>
        public float MagnetMultiplier
        {
            get { var d = Find("magnet_multiplier"); return d != null ? d.Param("multiplier") : 1f; }
        }

        /// <summary>흡혈 말고 다른 회복에 곱하는 값(흡혈대법이면 0). 회복·음식·운기조식에 곱한다.</summary>
        public float OtherHealMultiplier => Has("lifesteal_no_heal") ? 0f : 1f;

        /// <summary>받는 피해에 곱하는 값(혈광: 기혈이 문턱 아래일 때).</summary>
        public float DamageTakenMultiplier(float healthRatio)
        {
            var d = Find("low_health_frenzy");
            return d != null && healthRatio < d.Param("healthThreshold") ? 1f + d.Param("damageTaken") : 1f;
        }

        /// <summary>
        /// 능력치 덧셈. Life.CurrentStats(extra) 의 extra 로 넘긴다.
        /// 처치 수·심마 횟수·기혈 비율이 바뀌면 다시 부른다.
        /// </summary>
        /// <param name="kills">이 생의 처치 수(쌓인 그대로). 살기충천은 고른 뒤 몫만 센다.</param>
        /// <param name="simmaSteps">BloodArt.Steps(쌓인 그대로). 마지막 천마해체 뒤 몫만 센다.</param>
        public StatBonus Bonus(int kills, int simmaSteps = 0, float healthRatio = 1f)
        {
            var b = new StatBonus();
            foreach (var i in _taken)
            {
                switch (i.Effect)
                {
                    case "kill_stack_might":
                        b.Add(StatKind.Might, Math.Min(i.Param("mightMax"),
                            (float)Math.Floor(Math.Max(0, kills - _killsAtSalgi) / i.Param("killsPerStack")) * i.Param("mightPerStack")));
                        break;
                    case "might_for_health":
                        b.Add(StatKind.Might, i.Param("mightBonus"));
                        b.Add(StatKind.MaxHealth, -i.Param("maxHealthPenalty"));
                        break;
                    case "lifesteal_no_heal":
                        b.Add(StatKind.Lifesteal, i.Param("lifesteal"));
                        break;
                    case "low_health_frenzy":
                        if (healthRatio < i.Param("healthThreshold")) b.Add(StatKind.CooldownReduction, i.Param("cooldownReduction"));
                        break;
                    case "inner_demon_might":
                        int steps = Math.Max(0, simmaSteps - _simmaBaseline);
                        b.Add(StatKind.Might, Math.Min(i.Param("mightMax"), steps * i.Param("mightPerStep")));
                        break;
                }
            }
            return b;
        }

        /// <summary>천마해체를 썼다. 심마 공력이 사라지고 그 뒤 기운 횟수부터 다시 센다.</summary>
        public void OnDemonicReset(int simmaSteps) => _simmaBaseline = simmaSteps;

        /// <summary>
        /// 매 프레임. 운기조식 회복량(이번 프레임 몫, 흡혈대법이면 0)을 돌려주고 금종조 대기 시간을 줄인다.
        /// </summary>
        public float Tick(float dt, bool moving, float maxHealth)
        {
            _blockCooldown = Math.Max(0f, _blockCooldown - dt);
            _still = moving ? 0f : _still + dt;
            var d = Find("still_regen");
            if (d == null || _still < d.Param("stillSeconds")) return 0f;
            return maxHealth * d.Param("maxHealthPerSecond") * dt * OtherHealMultiplier;
        }

        /// <summary>안 움직인 시간(운기조식 표시용).</summary>
        public float StillSeconds => _still;

        /// <summary>금종조를 다시 쓸 수 있을 때까지 남은 초(0이면 다음 피해를 막는다).</summary>
        public float BlockCooldown => _blockCooldown;

        /// <summary>피해를 받기 직전에 부른다. true 면 이번 피해는 없던 일로 한다(금종조).</summary>
        public bool TryBlock()
        {
            var d = Find("periodic_block");
            if (d == null || _blockCooldown > 0f) return false;
            _blockCooldown = d.Param("intervalSeconds");
            return true;
        }
    }
}
