using System;
using System.Linq;
using BloodRain.Sim;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>혈월 천하 의서 일곱 권(ManuscriptSim, docs/design/sim/manuscript.md). 등롱 셈이 chapters.json 과 맞는지, 보고서가 지금 데이터와 같은지 본다.</summary>
    public class ManuscriptSimTests
    {
        static readonly Lazy<SimData> Data = new Lazy<SimData>(() => SimData.Load(TestData.DataDir));

        [Fact] public void 현경까지_가는_생의_등롱은_한두_개()
        {
            var ls = ManuscriptSim.Lanterns(Data.Value.Game);
            Assert.NotEmpty(ls);
            Assert.All(ls, l => Assert.True(l.From >= 15, "7장: 은빛 등롱은 15분 이후"));
            var rng = new Random(3);
            var counts = Enumerable.Range(0, 4000).Select(_ => ManuscriptSim.LanternsInLife(ls, 30, rng)).ToList();
            Assert.InRange(counts.Min(), 1, 2);
            Assert.InRange(counts.Max(), 1, 2);
            Assert.Equal(0, ManuscriptSim.LanternsInLife(ls, 14.9, new Random(1)));
        }

        [Fact] public void 확률이_오르면_생_수가_준다()
        {
            var ls = ManuscriptSim.Lanterns(Data.Value.Game);
            var lo = ManuscriptSim.Measure(ls, 7, 0.1, 30, 1, 3000);
            var hi = ManuscriptSim.Measure(ls, 7, 0.3, 30, 1, 3000);
            Assert.True(hi.Pct(hi.Lives7, 0.5) < lo.Pct(lo.Lives7, 0.5));
            Assert.True(lo.Pct(lo.Lives3, 0.5) <= lo.Pct(lo.Lives7, 0.5));
        }

        [Fact] public void 보고서가_지금_데이터와_같다()
        {
            string onDisk = TestData.ReadRepoFile("docs", "design", "sim", "manuscript.md").Replace("\r\n", "\n");
            Assert.Equal(ManuscriptSim.Report(Data.Value, 1).Replace("\r\n", "\n"), onDisk);
        }
    }
}
