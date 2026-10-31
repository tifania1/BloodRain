using System;
using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using BloodRain.Core.Progression;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>돌파 보상 낱말을 숫자로 모으는 RealmRewards(기획서 3장 경지표 '돌파 보상').</summary>
    public class RealmRewardSumTests
    {
        readonly GameData _d = TestData.Load();

        [Fact] public void 데이터의_모든_보상_낱말을_읽을_수_있다()
        {
            foreach (var r in _d.Balance.Realms)
                foreach (var t in RealmRewards.Tokens(r.Reward))
                    RealmRewards.Apply(new RealmRewardSum(), t); // 모르는 낱말이면 던진다
        }

        [Fact] public void 모르는_낱말은_조용히_넘기지_않는다()
        {
            var s = new RealmRewardSum();
            Assert.Throws<FormatException>(() => RealmRewards.Apply(s, "max_helth_10"));
            Assert.Throws<FormatException>(() => RealmRewards.Apply(s, "dash_charge_"));
            Assert.Throws<FormatException>(() => RealmRewards.Apply(s, "damage_taken_minus-5"));
        }

        [Fact] public void 현경까지의_합은_경지표_숫자와_같다()
        {
            var top = _d.Balance.Realms.Max(r => r.Level);
            var s = RealmRewards.Upto(_d.Balance.Realms, top);
            Assert.Equal(10f, s.MaxHealthFlat);
            Assert.Equal(1, s.DashCharges);
            Assert.Equal(0.25f, s.UltimatePower, 4);
            Assert.Equal(1, s.ExtraSlots);
            Assert.Equal(0.9f, s.DamageTakenMultiplier, 4);
            Assert.True(s.DoubleUltimate);
            Assert.Equal(_d.Balance.Realms.Count(r => RealmRewards.Tokens(r.Reward).Contains("insight_choice")), s.InsightChoices);
            var gdd = TestData.GddSection("경지표");
            foreach (var phrase in new[] { "최대 기혈 +10", "경공 충전 +1", "오의 위력 +25%", "받는 피해 -10%", "오의 2연속 사용" })
                Assert.Contains(phrase, gdd);
        }

        [Fact] public void 절기_그릇_수는_Loadout_Vessels_와_경지마다_같다()
        {
            var l = new Loadout(_d, _d.Characters.First(c => c.Id == "jin_sowoon"));
            foreach (var r in _d.Balance.Realms)
                Assert.Equal(l.Vessels(r.Level), RealmRewards.Upto(_d.Balance.Realms, r.Level).Vessels);
        }

        [Fact] public void 경지_문턱_바로_아래에선_그_경지_보상이_없다()
        {
            var iryu = _d.Balance.Realms.First(r => r.Id == "iryu");
            Assert.Equal(0f, RealmRewards.Upto(_d.Balance.Realms, iryu.Level - 1).MaxHealthFlat);
            Assert.Equal(10f, RealmRewards.Upto(_d.Balance.Realms, iryu.Level).MaxHealthFlat);
            Assert.Equal(10f, RealmRewards.Of(iryu).MaxHealthFlat);
            Assert.Equal(1, RealmRewards.Of(iryu).InsightChoices);
        }

        [Fact] public void 삼류는_보상이_없다()
        {
            var s = RealmRewards.Of(_d.Balance.Realms.First(r => r.Id == "samryu"));
            Assert.Equal(0, s.InsightChoices);
            Assert.Equal(1f, s.DamageTakenMultiplier);
            Assert.False(s.DoubleUltimate);
        }
    }
}
