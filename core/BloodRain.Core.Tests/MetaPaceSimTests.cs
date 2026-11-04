using System;
using System.Linq;
using BloodRain.Sim;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>메타 진행 속도 조정안(R-02, docs/design/sim/meta-pace.md).</summary>
    public class MetaPaceSimTests
    {
        static readonly Lazy<SimData> Data = new Lazy<SimData>(() => SimData.Load(TestData.DataDir));

        [Fact] public void 조각이_하나면_MetaSim_과_같다()
        {
            var a = MetaSim.Run(Data.Value, 8, null, maxLives: 6);
            var b = MetaSim.Run(Data.Value, 8, null, maxLives: 6, killsPerStone: 1);
            Assert.Equal(a.Select(r => (r.SealStones, r.Qi, r.Minutes)), b.Select(r => (r.SealStones, r.Qi, r.Minutes)));
        }

        [Fact] public void 조각이_둘이면_봉인석이_더_늦게_모인다()
        {
            var one = MetaSim.Run(Data.Value, 12, null, maxLives: 8);
            var two = MetaSim.Run(Data.Value, 12, null, maxLives: 8, killsPerStone: 2);
            for (int i = 0; i < Math.Min(one.Count, two.Count); i++) Assert.True(two[i].SealStones <= one[i].SealStones, $"{i + 1}생");
            Assert.True(two.Sum(r => r.SealStones) < one.Sum(r => r.SealStones));
        }

        [Fact] public void 안을_적용해도_원래_데이터는_그대로다()
        {
            var cost = MetaPaceSim.Variants.Single(v => v.Id == "cost");
            var awaken = MetaPaceSim.Variants.Single(v => v.Id == "awaken");
            var a = MetaPaceSim.Apply(cost, TestData.DataDir);
            var b = MetaPaceSim.Apply(awaken, TestData.DataDir);
            Assert.Equal(0.3f, a.Game.Balance.Training.CostGrowthPerBought);
            Assert.Equal("chojeoljeong", b.Game.Cheonha.Bosses.AwakenFrom);
            Assert.Equal(0.1f, Data.Value.Game.Balance.Training.CostGrowthPerBought);
            Assert.Equal("jeoljeong", Data.Value.Game.Cheonha.Bosses.AwakenFrom);
            Assert.Equal(0.1f, b.Game.Balance.Training.CostGrowthPerBought);
        }

        [Fact] public void 보고서의_지금_데이터_줄은_meta_md_와_같다()
        {
            var pace = TestData.ReadRepoFile("docs", "design", "sim", "meta-pace.md").Replace("\r\n", "\n").Split('\n');
            var meta = TestData.ReadRepoFile("docs", "design", "sim", "meta.md").Replace("\r\n", "\n").Split('\n');
            foreach (var m in new[] { 6, 8, 12 })
            {
                var p = pace.Single(l => l.StartsWith($"| 지금 데이터 | — | {m}분 |")).Split('|').Select(x => x.Trim()).ToArray();
                var q = meta.First(l => l.StartsWith($"| 지금 데이터 | {m}분 |")).Split('|').Select(x => x.Trim()).ToArray();
                Assert.Equal(q[3], p[4]); // 첫 절정
                Assert.Equal(q[4], p[5]); // 첫 봉인석
                Assert.Equal(q[5], p[7]); // 봉인석 7
                Assert.Equal(q[6], p[8]); // 연무장 레벨
            }
        }
    }
}
