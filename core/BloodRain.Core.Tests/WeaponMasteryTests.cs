using System;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Run;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>무공 성취 세기(backlog GU-06, roguelike_benchmark 안 ③): 가장 높은 레벨로 마친 생을 무공마다 세고, 1생 소성·3생 대성.</summary>
    public class WeaponMasteryTests
    {
        readonly GameData _d = TestData.Load();

        static Life Born(Soul soul, GameData d)
        {
            var c = d.Cheonha;
            var cand = new RebirthCandidate(c.Sect("shaolin"), c.Region("songshan"), Array.Empty<BirthTraitDef>());
            return new Life(soul, Rebirth.Body(c, cand), new Random(1));
        }

        void LiveMaxed(Soul soul, bool maxed)
        {
            var life = Born(soul, _d);
            if (maxed)
                while (life.Loadout.Weapons["nahan_fist"] < _d.Balance.Levels.WeaponMax)
                    life.Loadout.Apply(new Offer(OfferKind.UpgradeWeapon, "nahan_fist"));
            soul.EndLife(life.End(LifeEndWay.Fallen, 0));
        }

        Soul Fresh()
        {
            var soul = new Soul(_d);
            soul.EndLife(soul.BeginLife(Rebirth.Prologue(_d)).Finish(LifeEndWay.Fallen, 0));
            return soul;
        }

        [Fact]
        public void 가장_높은_레벨로_마친_생만_세고_1생_소성_3생_대성()
        {
            var soul = Fresh();
            Assert.Equal(0, soul.MasteryStage("nahan_fist"));
            LiveMaxed(soul, false);
            Assert.Equal(0, soul.MasteryStage("nahan_fist"));
            LiveMaxed(soul, true);
            Assert.Equal(1, soul.MasteryStage("nahan_fist"));
            LiveMaxed(soul, true);
            Assert.Equal(1, soul.MasteryStage("nahan_fist"));
            LiveMaxed(soul, true);
            Assert.Equal(2, soul.MasteryStage("nahan_fist"));
            Assert.Equal(3, soul.WeaponMastery["nahan_fist"]);
        }

        [Fact]
        public void 저장했다_불러와도_성취가_남는다()
        {
            var soul = Fresh();
            LiveMaxed(soul, true);
            var back = Soul.FromJson(_d, soul.ToJson());
            Assert.Equal(1, back.MasteryStage("nahan_fist"));
        }
    }
}
