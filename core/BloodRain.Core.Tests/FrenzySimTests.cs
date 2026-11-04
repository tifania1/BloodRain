using System;
using BloodRain.Sim;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>마교 '마인의 피' 곡선 세 안(FrenzySim, docs/design/sim/frenzy.md, R-04c). 끝값과 상한, 보고서가 지금 데이터와 같은지 본다.</summary>
    public class FrenzySimTests
    {
        static readonly Lazy<SimData> Data = new Lazy<SimData>(() => SimData.Load(TestData.DataDir));

        [Fact] public void 곡선은_100에서_0_30이하에서_끝값()
        {
            Assert.Equal(0, FrenzySim.Ramp(1.0), 6);
            Assert.Equal(1, FrenzySim.Ramp(0.3), 6);
            Assert.Equal(1, FrenzySim.Ramp(0.05), 6);
            Assert.Equal(0.5, FrenzySim.Ramp(0.65), 6);
        }

        [Fact] public void 맨몸_30퍼센트_빈도는_가_2배_나다_1점5배()
        {
            float cap = Data.Value.Game.Balance.Stats.CooldownReductionCap;
            Assert.Equal(2.0, FrenzySim.Rate(FrenzySim.Plan.CdrHalf, 0, 0.3, cap), 6);
            Assert.Equal(1.5, FrenzySim.Rate(FrenzySim.Plan.RateMultiplier, 0, 0.3, cap), 6);
            Assert.Equal(1.5, FrenzySim.Rate(FrenzySim.Plan.CdrThird, 0, 0.3, cap), 6);
        }

        [Fact] public void 쾌속_안은_상한을_넘지_않고_빈도_배율만_넘는다()
        {
            float cap = Data.Value.Game.Balance.Stats.CooldownReductionCap;
            double max = 1 / (1 - cap);
            Assert.Equal(max, FrenzySim.Rate(FrenzySim.Plan.CdrHalf, cap, 0.1, cap), 6);
            Assert.Equal(max, FrenzySim.Rate(FrenzySim.Plan.CdrThird, cap, 0.1, cap), 6);
            Assert.Equal(max * 1.5, FrenzySim.Rate(FrenzySim.Plan.RateMultiplier, cap, 0.1, cap), 6);
        }

        [Fact] public void 보고서가_지금_데이터와_같다()
        {
            string onDisk = TestData.ReadRepoFile("docs", "design", "sim", "frenzy.md").Replace("\r\n", "\n");
            Assert.Equal(FrenzySim.Report(Data.Value).Replace("\r\n", "\n"), onDisk);
        }
    }
}
