#nullable enable
// 경지 돌파 보상 낱말(balance.json realms 의 reward)을 읽어 숫자로 모은다. 기획서 '3. 무공 체계' 경지표 '돌파 보상' 칸.
//   insight_choice          깨달음 고르기(Insights 가 후보를 뽑는다)
//   max_health_N            최대 기혈 +N(그대로 더함)
//   dash_charge_N           경공 충전 +N
//   ultimate_power_N        오의 위력 +N%
//   extra_slot              공격 또는 심법 7칸째(고르는 쪽은 Loadout.AddExtraSlot)
//   damage_taken_minusN     받는 피해 -N%
//   double_ultimate         오의 2연속 사용
//   jeolgi_vessel           절기 그릇 하나(Loadout.Vessels 와 같은 수)
// 모르는 낱말은 예외를 던진다. 데이터 오타가 조용히 보상 없음이 되지 않게.
// 효과를 실제로 얹는 일(StatSheet·Dash·오의)은 돌파 화면(C-07)을 만들 때 부른 쪽이 이 합을 읽어서 한다.
using System;
using System.Collections.Generic;
using System.Globalization;
using BloodRain.Core.Data;

namespace BloodRain.Core.Progression
{
    public sealed class RealmRewardSum
    {
        public int InsightChoices { get; internal set; }
        public float MaxHealthFlat { get; internal set; }
        public int DashCharges { get; internal set; }
        /// <summary>오의 위력 덧셈(0.25 = +25%).</summary>
        public float UltimatePower { get; internal set; }
        public int ExtraSlots { get; internal set; }
        /// <summary>받는 피해 배율(0.9 = -10%). 여러 개면 곱한다.</summary>
        public float DamageTakenMultiplier { get; internal set; } = 1f;
        public bool DoubleUltimate { get; internal set; }
        public int Vessels { get; internal set; }
    }

    public static class RealmRewards
    {
        public const string InsightChoice = "insight_choice";
        public const string ExtraSlot = "extra_slot";
        public const string DoubleUltimate = "double_ultimate";
        public const string JeolgiVessel = "jeolgi_vessel";
        const string MaxHealth = "max_health_";
        const string DashCharge = "dash_charge_";
        const string UltimatePower = "ultimate_power_";
        const string DamageTakenMinus = "damage_taken_minus";

        public static IEnumerable<string> Tokens(string? reward)
        {
            if (reward == null) yield break;
            foreach (var t in reward.Split(','))
            {
                var s = t.Trim();
                if (s.Length > 0) yield return s;
            }
        }

        /// <summary>낱말 하나를 sum 에 더한다.</summary>
        public static void Apply(RealmRewardSum sum, string token)
        {
            switch (token)
            {
                case InsightChoice: sum.InsightChoices++; return;
                case ExtraSlot: sum.ExtraSlots++; return;
                case DoubleUltimate: sum.DoubleUltimate = true; return;
                case JeolgiVessel: sum.Vessels++; return;
            }
            if (Suffix(token, MaxHealth, out var n)) sum.MaxHealthFlat += n;
            else if (Suffix(token, DashCharge, out n)) sum.DashCharges += (int)n;
            else if (Suffix(token, UltimatePower, out n)) sum.UltimatePower += n / 100f;
            else if (Suffix(token, DamageTakenMinus, out n)) sum.DamageTakenMultiplier *= 1f - n / 100f;
            else throw new FormatException($"모르는 돌파 보상 낱말입니다: {token}");
        }

        static bool Suffix(string token, string prefix, out float value)
        {
            value = 0;
            return token.StartsWith(prefix, StringComparison.Ordinal)
                && float.TryParse(token.Substring(prefix.Length), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && value >= 0;
        }

        /// <summary>한 경지의 보상만.</summary>
        public static RealmRewardSum Of(RealmDef realm)
        {
            var sum = new RealmRewardSum();
            foreach (var t in Tokens(realm.Reward)) Apply(sum, t);
            return sum;
        }

        /// <summary>level 까지 닿은 모든 경지의 보상 합(삼류부터). 돌파를 지난 몸의 상태.</summary>
        public static RealmRewardSum Upto(IEnumerable<RealmDef> realms, int level)
        {
            var sum = new RealmRewardSum();
            foreach (var r in realms)
            {
                if (r.Level > level) continue;
                foreach (var t in Tokens(r.Reward)) Apply(sum, t);
            }
            return sum;
        }
    }
}
