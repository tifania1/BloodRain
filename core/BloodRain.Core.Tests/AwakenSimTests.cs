using System;
using System.Linq;
using BloodRain.Sim;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>캐릭터별 절기 각성 시뮬레이션(AwakenSim, docs/design/sim/awakenings.md).</summary>
    public class AwakenSimTests
    {
        static readonly Lazy<SimData> Data = new Lazy<SimData>(() => SimData.Load(TestData.DataDir));

        [Fact] public void 각성_시각은_차례대로이고_닿은_판_수는_줄기만_한다()
        {
            var r = AwakenSim.Measure(Data.Value, "jin_sowoon", 3);
            Assert.Equal(3, r.Runs);
            for (int i = 1; i < 4; i++)
            {
                Assert.True(r.Reached[i] <= r.Reached[i - 1]);
                if (r.Reached[i] == r.Runs && r.Reached[i - 1] == r.Runs) Assert.True(r.Minute[i] >= r.Minute[i - 1]);
            }
            Assert.InRange(r.FirstIsStart, 0, r.Runs);
            Assert.True(double.IsNaN(r.ChestShare) || (r.ChestShare >= 0 && r.ChestShare <= 1));
        }

        [Fact] public void 절기_수는_그릇_넷을_넘지_않고_RunSimulator_기록과_같다()
        {
            var data = Data.Value;
            var run = RunSimulator.Run(data, new SimOptions { Character = "hyegong", Seed = 1, Minutes = 30 });
            var r = AwakenSim.Measure(data, "hyegong", 1);
            Assert.Equal(run.Awakenings.Count, (int)r.Count30);
            Assert.True(r.Count30 <= 4);
            if (run.Awakenings.Count > 0) Assert.Equal(run.Awakenings[0].Minute, r.Minute[0], 9);
            // 같은 무공이 두 번 깨어나지 않는다
            Assert.Equal(run.Awakenings.Count, run.Awakenings.Select(a => a.WeaponId).Distinct().Count());
        }

        [Fact] public void 이름_뒤_조사()
        {
            Assert.Equal("혜공이", AwakenSim.Subject("혜공"));
            Assert.Equal("하연이", AwakenSim.Subject("하연"));
            Assert.Equal("운화가", AwakenSim.Subject("운화"));
            Assert.Equal("백야가", AwakenSim.Subject("백야"));
        }
    }
}
