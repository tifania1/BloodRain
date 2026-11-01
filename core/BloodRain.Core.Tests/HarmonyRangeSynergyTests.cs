using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using BloodRain.Core.Progression;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>무당 진전 2단계 '태극 구간 ±25 → ±35'(기획서 20장, synergies.json harmonyRange).</summary>
    public class HarmonyRangeSynergyTests
    {
        readonly GameData _d = TestData.Load();

        [Fact] public void 태극_구간을_넓히는_진전은_무당_2단계_하나뿐이고_설명과_맞는다()
        {
            var tiers = _d.Synergies.Schools.SelectMany(s => s.Tiers.Select(t => (s.Id, t))).Where(x => x.t.HarmonyRange != null).ToList();
            Assert.Single(tiers);
            Assert.Equal("wudang", tiers[0].Id);
            Assert.Equal(2, tiers[0].t.Count);
            Assert.Contains($"±{_d.Balance.Yinyang.HarmonyRange}에서 ±{tiers[0].t.HarmonyRange}로", tiers[0].t.Desc);
            Assert.Contains($"태극 구간 ±{_d.Balance.Yinyang.HarmonyRange} → ±{tiers[0].t.HarmonyRange}", TestData.Gdd);
        }

        [Fact] public void 무당_둘을_모으면_30도_태극이다()
        {
            var one = TestData.Build("jin_sowoon", ("samjae_sword", 1), ("taiji_sword", 1));
            Assert.Null(Synergy.Compute(_d, one).HarmonyRange);
            var two = TestData.Build("jin_sowoon", ("samjae_sword", 1), ("taiji_sword", 1), ("yangui", 1));
            var syn = Synergy.Compute(_d, two);
            Assert.Equal(35, syn.HarmonyRange);

            var g = new YinYangGauge(_d.Balance.Yinyang, 30);
            Assert.Equal(YinYangBand.Bias, g.Band);
            g.SetSynergyHarmonyRange(syn.HarmonyRange);
            Assert.Equal(35, g.HarmonyRange);
            Assert.Equal(YinYangBand.Harmony, g.Band);
            g.Shift(6);
            Assert.Equal(YinYangBand.Bias, g.Band);
            g.SetSynergyHarmonyRange(null);
            Assert.Equal(25, g.HarmonyRange);
        }

        [Fact] public void 운화는_원래_40이라_좁아지지_않는다()
        {
            var unhwa = _d.Characters.First(c => c.Id == "unhwa");
            var g = new YinYangGauge(_d.Balance.Yinyang, 38, unhwa.HarmonyRange);
            g.SetSynergyHarmonyRange(35);
            Assert.Equal(40, g.HarmonyRange);
            Assert.Equal(YinYangBand.Harmony, g.Band);
        }
    }
}
