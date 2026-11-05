using System;
using System.Linq;
using System.Text.RegularExpressions;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Run;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>보부상(7장, balance.json merchant, backlog RM-04·RM-05). 값이 기획서 표와 같은지, 사면 지갑·다시 고르기가 맞게 바뀌는지.</summary>
    public class MerchantTests
    {
        readonly GameData _d = TestData.Load();
        MerchantDef R => _d.Balance.Merchant;

        Life NewLife()
        {
            var soul = new Soul(_d);
            soul.EndLife(soul.BeginLife(Rebirth.Prologue(_d)).Finish(LifeEndWay.Fallen, 0));
            var c = _d.Cheonha;
            var cand = new RebirthCandidate(c.Sect("shaolin"), c.Region(c.Sect("shaolin").Region), Array.Empty<BirthTraitDef>());
            return new Life(soul, Rebirth.Body(c, cand), new Random(1));
        }

        [Fact] public void 품목_셋이_7장_표와_같다()
        {
            Assert.Equal(new[] { MerchantVisit.Potion, MerchantVisit.RerollTicket, MerchantVisit.ChwigiTalisman }, R.Items.Select(i => i.Id).ToArray());
            foreach (var item in R.Items)
            {
                var m = Regex.Match(TestData.Gdd, @"\| " + Regex.Escape(item.NameKo) + @" \| (\d+)냥 \| ([^|]+)\|");
                Assert.True(m.Success, $"7장 보부상 표에 {item.NameKo} 줄이 없다");
                Assert.Equal(item.Price, int.Parse(m.Groups[1].Value));
                if (item.HealFraction > 0) Assert.Contains($"최대 기혈 {item.HealFraction * 100:0}% 회복", m.Groups[2].Value);
                if (item.Rerolls > 0) Assert.Contains($"다시 고르기 +{item.Rerolls}회", m.Groups[2].Value);
            }
            var per = Regex.Match(TestData.Gdd, @"한 번 들를 때 품목마다 (\d+)개까지");
            Assert.True(per.Success);
            Assert.Equal(R.MaxPerVisit, int.Parse(per.Groups[1].Value));
        }

        [Fact] public void 취기부는_등롱_취기부와_같은_종류다()
        {
            Assert.Contains(_d.Balance.Drops.Lantern, l => l.Kind == MerchantVisit.ChwigiTalisman);
        }

        [Fact] public void 사면_은자가_빠지고_품목마다_두_개까지()
        {
            var life = NewLife();
            life.GainSilver(1000);
            var v = new MerchantVisit(R);
            int price = v.Item(MerchantVisit.Potion)!.Price;
            Assert.Equal(MerchantBuy.Bought, v.Buy(MerchantVisit.Potion, life));
            Assert.Equal(MerchantBuy.Bought, v.Buy(MerchantVisit.Potion, life));
            Assert.Equal(MerchantBuy.SoldOut, v.Buy(MerchantVisit.Potion, life));
            Assert.Equal(1000 - 2 * price, life.Silver);
            Assert.Equal(0, v.Left(MerchantVisit.Potion));
            Assert.Equal(R.MaxPerVisit, v.Left(MerchantVisit.ChwigiTalisman));
            Assert.Equal(R.MaxPerVisit, new MerchantVisit(R).Left(MerchantVisit.Potion)); // 다음에 들르면 다시 두 개
        }

        [Fact] public void 은자가_모자라면_안_산다()
        {
            var life = NewLife();
            var v = new MerchantVisit(R);
            int price = v.Item(MerchantVisit.ChwigiTalisman)!.Price;
            life.GainSilver(price - 1);
            Assert.False(v.CanBuy(MerchantVisit.ChwigiTalisman, life.Silver));
            Assert.Equal(MerchantBuy.NotEnoughSilver, v.Buy(MerchantVisit.ChwigiTalisman, life));
            Assert.Equal(price - 1, life.Silver);
            Assert.Equal(0, v.Bought(MerchantVisit.ChwigiTalisman));
            Assert.Equal(MerchantBuy.UnknownItem, v.Buy("nope", life));
            Assert.Equal(0, v.Left("nope"));
        }

        [Fact] public void 다시_고르기_권은_그_판_횟수를_더한다()
        {
            var life = NewLife();
            life.GainSilver(1000);
            int before = life.Charges.Rerolls;
            var v = new MerchantVisit(R);
            v.Buy(MerchantVisit.RerollTicket, life);
            Assert.Equal(before + v.Item(MerchantVisit.RerollTicket)!.Rerolls, life.Charges.Rerolls);
        }

        [Fact] public void 회복약은_최대_기혈_비율이고_마교는_절반()
        {
            var potion = new MerchantVisit(R).Item(MerchantVisit.Potion)!;
            Assert.Equal(50f, MerchantVisit.PotionHeal(potion, 100f), 3);
            Assert.Equal(25f, MerchantVisit.PotionHeal(potion, 100f, 0.5f), 3);
        }

        [Fact] public void 남은_은자가_정산과_같다()
        {
            var life = NewLife();
            life.GainSilver(700);
            var v = new MerchantVisit(R);
            v.Buy(MerchantVisit.Potion, life);
            v.Buy(MerchantVisit.RerollTicket, life);
            var rec = life.End(LifeEndWay.Fallen);
            Assert.Equal(700 - 120 - 150, rec.RebirthQi.LeftoverSilver);
        }
    }
}
