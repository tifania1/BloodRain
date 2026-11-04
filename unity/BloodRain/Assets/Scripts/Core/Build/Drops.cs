#nullable enable
// 바닥에 떨어지는 것(기획서 8장 '드롭', balance.json drops).
// 등롱을 부수면 drops.lantern 가중치로 하나를 뽑고, 음식은 FoodHeal 배율을 곱해 회복, 은자 주머니는 silverBag 범위에서 뽑는다.
// 은자에 곱하는 탐욕·혈월 배율은 줍는 쪽(Run)이 곱한다. 진기 구슬 값은 balance.json orbs(OrbValuesDef).
using System;
using BloodRain.Core.Data;

namespace BloodRain.Core.Build
{
    public static class Drops
    {
        public const string Bun = "bun";
        public const string Duck = "duck";
        public const string ChwigiTalisman = "chwigi_talisman";
        public const string JeongsinTalisman = "jeongsin_talisman";
        public const string SilverBag = "silver_bag";

        /// <summary>등롱 하나를 부쉈을 때 나오는 것. 표 앞에서부터 깎는다(웹 프로토타입과 같은 순서).</summary>
        public static string RollLantern(DropsDef rule, Random rng)
        {
            if (rule.Lantern.Count == 0) throw new InvalidOperationException("등롱 드롭 표가 비었습니다");
            double total = 0;
            foreach (var l in rule.Lantern)
            {
                if (l.Weight < 0) throw new InvalidOperationException("등롱 가중치는 0 이상입니다");
                total += l.Weight;
            }
            if (total <= 0) throw new InvalidOperationException("등롱 가중치 합이 0입니다");
            double r = rng.NextDouble() * total;
            foreach (var l in rule.Lantern)
            {
                r -= l.Weight;
                if (r < 0) return l.Kind;
            }
            return rule.Lantern[rule.Lantern.Count - 1].Kind;
        }

        /// <summary>음식 회복량. foodHeal 은 StatSheet 의 음식 회복 배율(기본 1, 개방 대식·혈공 거래 등이 곱해진 값),
        /// healingMultiplier 는 SectTrait.HealingMultiplier(마교 0.5). 음식이 아니면 0.</summary>
        public static float FoodHeal(DropsDef rule, string kind, float foodHeal = 1f, float healingMultiplier = 1f)
        {
            if (foodHeal < 0) throw new ArgumentOutOfRangeException(nameof(foodHeal));
            if (healingMultiplier < 0) throw new ArgumentOutOfRangeException(nameof(healingMultiplier));
            switch (kind)
            {
                case Bun: return rule.BunHeal * foodHeal * healingMultiplier;
                case Duck: return rule.DuckHeal * foodHeal * healingMultiplier;
                default: return 0;
            }
        }

        /// <summary>은자 주머니 하나(탐욕·혈월 배율 전). 최소·최대를 모두 포함한다.</summary>
        public static int RollSilverBag(DropsDef rule, Random rng)
        {
            if (rule.SilverBag.Count != 2) throw new InvalidOperationException("drops.silverBag 은 [최소, 최대] 두 칸입니다");
            int lo = rule.SilverBag[0], hi = rule.SilverBag[1];
            if (lo < 0 || hi < lo) throw new InvalidOperationException("drops.silverBag 범위가 잘못됐습니다");
            return rng.Next(lo, hi + 1);
        }

        /// <summary>잡몹 하나가 쓰러질 때 은자 주머니를 떨구는가.</summary>
        public static bool EnemyDropsSilver(DropsDef rule, Random rng) => rng.NextDouble() < rule.EnemySilverChance;
    }
}
