using System;
using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Run;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>재기(WP-42, 기획서 8장 '사망하면 재기가 있으면 기혈 50%로 부활'). 웹 한 생 판 WP-21 과 같은 규칙.</summary>
    public class RevivalTests
    {
        readonly GameData _d = TestData.Load();

        Life NewLife()
        {
            var soul = new Soul(_d);
            soul.EndLife(soul.BeginLife(Rebirth.Prologue(_d)).Finish(LifeEndWay.Fallen, 0));
            var c = _d.Cheonha;
            var cand = new RebirthCandidate(c.Sect("shaolin"), c.Region(c.Sect("shaolin").Region), Array.Empty<BirthTraitDef>());
            return new Life(soul, Rebirth.Body(c, cand), new Random(1));
        }

        [Fact] public void 기획서_8장_기혈_50퍼센트와_데이터가_같다()
        {
            Assert.Contains("재기가 있으면 기혈 50%로 부활", TestData.Gdd);
            Assert.Equal(0.5f, _d.Balance.Revival.HealthRatio);
        }

        [Fact] public void 재기_횟수만큼만_일어난다()
        {
            var life = NewLife();
            var none = life.CurrentStats();
            Assert.Equal(0, none.Revival);
            Assert.Null(life.TryRevive(none));

            var extra = new StatBonus();
            extra.Add(StatKind.Revival, 2);
            var two = life.CurrentStats(extra);
            Assert.Equal(0.5f, life.TryRevive(two));
            Assert.Equal(0.5f, life.TryRevive(two));
            Assert.Null(life.TryRevive(two));
            Assert.Equal(2, life.RevivalsUsed);
        }

        [Fact] public void 서장은_재기를_안_쓴다()
        {
            var soul = new Soul(_d);
            var life = new Life(soul, Rebirth.Prologue(_d), new Random(1));
            Assert.Throws<InvalidOperationException>(() => life.TryRevive(life.CurrentStats()));
        }
    }
}
