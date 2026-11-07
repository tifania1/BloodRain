// 심법 최대 레벨 덧붙임(기획서 9장 심법 표 '최대' 칸, passives.json maxBonus).
//   금강체 Lv5 넉백 면역, 만독심법 Lv5 독 면역, 운룡보 Lv5 경공 충전 +1.
//   데이터의 글을 그대로 읽는다. 모르는 글이면 Parse 가 예외를 내고 테스트가 데이터 전부를 읽어 본다.
#nullable enable
using System;
using System.Globalization;
using BloodRain.Core.Data;

namespace BloodRain.Core.Build
{
    public readonly struct PassiveMaxBonus
    {
        /// <summary>적·패턴에 밀리지 않는다(금강체).</summary>
        public readonly bool KnockbackImmune;
        /// <summary>독 장판·독안개·독 탄 피해를 받지 않는다(만독심법). 당가 몸 SectTrait.PoisonImmune 과 같은 뜻.</summary>
        public readonly bool PoisonImmune;
        /// <summary>경공 충전 칸 더하기(운룡보). Life.DashMaxCharges 가 더한다.</summary>
        public readonly int DashCharges;

        public PassiveMaxBonus(bool knockbackImmune, bool poisonImmune, int dashCharges)
        {
            KnockbackImmune = knockbackImmune; PoisonImmune = poisonImmune; DashCharges = dashCharges;
        }

        public static readonly PassiveMaxBonus None = new PassiveMaxBonus(false, false, 0);

        const string Knockback = "넉백 면역", Poison = "독 면역", DashPrefix = "경공 충전 +";

        /// <summary>maxBonus 글 하나를 읽는다. 모르는 글이면 예외.</summary>
        public static PassiveMaxBonus Parse(string text)
        {
            string t = text.Trim();
            if (t == Knockback) return new PassiveMaxBonus(true, false, 0);
            if (t == Poison) return new PassiveMaxBonus(false, true, 0);
            if (t.StartsWith(DashPrefix, StringComparison.Ordinal)
                && int.TryParse(t.Substring(DashPrefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
                return new PassiveMaxBonus(false, false, n);
            throw new FormatException($"모르는 심법 최대 덧붙임: '{text}'");
        }

        PassiveMaxBonus Plus(PassiveMaxBonus o) =>
            new PassiveMaxBonus(KnockbackImmune || o.KnockbackImmune, PoisonImmune || o.PoisonImmune, DashCharges + o.DashCharges);

        /// <summary>지금 로드아웃에서 최대 레벨(balance.json levels.passiveMax)에 닿은 심법의 덧붙임을 모두 합친다.</summary>
        public static PassiveMaxBonus Of(GameData data, Loadout loadout)
        {
            var sum = None;
            int max = data.Balance.Levels.PassiveMax;
            foreach (var kv in loadout.Passives)
            {
                if (kv.Value < max) continue;
                var p = data.Passive(kv.Key);
                if (!string.IsNullOrEmpty(p.MaxBonus)) sum = sum.Plus(Parse(p.MaxBonus!));
            }
            return sum;
        }
    }
}
