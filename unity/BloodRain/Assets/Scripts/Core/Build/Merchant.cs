#nullable enable
// 보부상(기획서 7장 '보부상', balance.json merchant). 한 생의 12·24분(chapters.json life 사건)에 들른다.
// 이번 생에서 주운 은자(Life.Silver)로 산다. 남은 은자는 절반만 윤회의 기가 되니(10장) 사는 만큼 다음 생 몫이 준다.
// 한 번 들를 때마다 MerchantVisit 를 새로 만든다(품목마다 maxPerVisit 개까지).
// 다시 고르기 권은 여기서 Life.Charges 에 바로 더하고, 회복약·취기부는 효과를 화면 쪽(Run)이 건다:
//   회복약은 PotionHeal 만큼 기혈을 채우고, 취기부는 등롱 취기부(Drops.ChwigiTalisman)와 같이 화면 안 진기 구슬을 끌어온다.
using System;
using System.Collections.Generic;
using BloodRain.Core.Data;
using BloodRain.Core.Run;

namespace BloodRain.Core.Build
{
    public enum MerchantBuy
    {
        Bought,
        NotEnoughSilver,
        SoldOut,
        UnknownItem,
    }

    public sealed class MerchantVisit
    {
        public const string Potion = "potion";
        public const string RerollTicket = "reroll_ticket";
        public const string ChwigiTalisman = Drops.ChwigiTalisman;

        readonly MerchantDef _rule;
        readonly Dictionary<string, int> _bought = new Dictionary<string, int>();

        public MerchantVisit(MerchantDef rule)
        {
            if (rule.MaxPerVisit < 0) throw new InvalidOperationException("merchant.maxPerVisit 은 0 이상입니다");
            _rule = rule;
        }

        public IReadOnlyList<MerchantItemDef> Items => _rule.Items;

        public MerchantItemDef? Item(string id)
        {
            foreach (var i in _rule.Items) if (i.Id == id) return i;
            return null;
        }

        /// <summary>이번에 이 품목을 산 수.</summary>
        public int Bought(string id) => _bought.TryGetValue(id, out var n) ? n : 0;

        /// <summary>이번에 더 살 수 있는 수(은자는 보지 않음). 없는 품목은 0.</summary>
        public int Left(string id) => Item(id) == null ? 0 : Math.Max(0, _rule.MaxPerVisit - Bought(id));

        /// <summary>지금 은자로 하나 살 수 있는지.</summary>
        public bool CanBuy(string id, int silver)
        {
            var item = Item(id);
            return item != null && Left(id) > 0 && silver >= item.Price;
        }

        /// <summary>하나 산다. 은자를 빼고, 다시 고르기 권이면 그 판 다시 고르기 횟수를 더한다.
        /// 회복약·취기부는 Bought 를 돌려받은 쪽이 효과를 건다.</summary>
        public MerchantBuy Buy(string id, Life life)
        {
            var item = Item(id);
            if (item == null) return MerchantBuy.UnknownItem;
            if (Left(id) <= 0) return MerchantBuy.SoldOut;
            if (!life.SpendSilver(item.Price)) return MerchantBuy.NotEnoughSilver;
            _bought[id] = Bought(id) + 1;
            if (item.Rerolls > 0) life.Charges.Add(LevelUpAction.Reroll, item.Rerolls);
            return MerchantBuy.Bought;
        }

        /// <summary>회복약 하나가 채우는 기혈. healingMultiplier 는 SectTrait.HealingMultiplier(마교 0.5).
        /// 음식이 아니라 음식 회복 배율(개방 대식 등)은 곱하지 않는다.</summary>
        public static float PotionHeal(MerchantItemDef item, float maxHp, float healingMultiplier = 1f)
        {
            if (maxHp < 0) throw new ArgumentOutOfRangeException(nameof(maxHp));
            if (healingMultiplier < 0) throw new ArgumentOutOfRangeException(nameof(healingMultiplier));
            return item.HealFraction * maxHp * healingMultiplier;
        }
    }
}
