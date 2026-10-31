using System;
using System.IO;
using System.Linq;
using BloodRain.Sim;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>음양 게이지 흐름(YinYangSim, docs/design/sim/yinyang.md). 구간 시간·역류 셈이 Core 규칙과 맞는지, 표가 지금 데이터와 같은지 본다.</summary>
    public class YinYangSimTests
    {
        static readonly Lazy<SimData> Data = new Lazy<SimData>(() => SimData.Load(TestData.DataDir));

        static SimResult Run(string character, string policy, int seed, double minutes) =>
            RunSimulator.Run(Data.Value, new SimOptions { Character = character, Policy = policy, Seed = seed, Minutes = minutes });

        [Fact] public void 구간에_머문_시간을_더하면_판_길이()
        {
            var r = Run("jin_sowoon", "average", 1, 12);
            Assert.Equal(12 * 60, r.BandSeconds.Sum(), 3);
            Assert.All(r.BandSeconds, s => Assert.True(s >= 0));
        }

        [Fact] public void 처음_게이지는_캐릭터_시작값()
        {
            foreach (var ch in Data.Value.Game.Characters)
                Assert.Equal(ch.YinyangStart, Run(ch.Id, "slow", 1, 1).At(0).Gauge);
        }

        [Fact] public void 역류는_극성에_60초_머물_때마다()
        {
            var g = Data.Value.Game;
            double interval = g.Balance.Yinyang.Extreme.BacklashIntervalSeconds;
            foreach (var ch in new[] { "jin_sowoon", "namgung_hyeon", "unhwa" })
                for (int seed = 1; seed <= 3; seed++)
                {
                    var r = Run(ch, "average", seed, 30);
                    // 극성을 드나들면 쌓인 시간이 0으로 돌아가니 역류는 극성 시간 ÷ 60 을 넘지 못한다
                    Assert.True(r.BacklashMinutes.Count <= Math.Floor(r.BandSeconds[2] / interval + 1e-6), $"{ch} 시드 {seed}");
                    if (double.IsNaN(r.FirstExtremeMinute)) { Assert.Empty(r.BacklashMinutes); continue; }
                    Assert.False(double.IsNaN(r.FirstOffHarmonyMinute));
                    Assert.True(r.FirstOffHarmonyMinute <= r.FirstExtremeMinute);
                    if (r.BacklashMinutes.Count > 0)
                        Assert.True(r.BacklashMinutes[0] >= r.FirstExtremeMinute + interval / 60 - 1e-6);
                    for (int i = 1; i < r.BacklashMinutes.Count; i++)
                        Assert.True(r.BacklashMinutes[i] - r.BacklashMinutes[i - 1] >= interval / 60 - 1e-6);
                }
        }

        [Fact] public void 게이지는_범위_안에서만_움직인다()
        {
            var yy = Data.Value.Game.Balance.Yinyang;
            var r = Run("dang_yeonhwa", "greedy", 2, 30);
            Assert.All(r.Minutes, m => Assert.InRange(m.Gauge, yy.Min, yy.Max));
        }

        [Fact] public void 같은_시드면_같은_흐름()
        {
            var a = Run("hyegong", "average", 4, 15);
            var b = Run("hyegong", "average", 4, 15);
            Assert.Equal(a.BandSeconds, b.BandSeconds);
            Assert.Equal(a.BacklashMinutes, b.BacklashMinutes);
            Assert.Equal(a.Minutes.Select(m => m.Gauge), b.Minutes.Select(m => m.Gauge));
        }

        [Fact] public void 보고서_진소운_줄이_지금_데이터와_같다()
        {
            var path = Path.Combine(TestData.RepoRoot, "docs", "design", "sim", "yinyang.md");
            var text = File.ReadAllText(path).Replace("\r\n", "\n");
            const string hint = " — dotnet run --project core/BloodRain.Sim -c Release -- --yinyang docs/design/sim/yinyang.md 로 다시 쓴다";
            var row = YinYangSim.Measure(Data.Value, "jin_sowoon", "average", 10);
            string pct(double x) => (x * 100).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "%";
            string head = $"| {row.NameKo} | 0 | average | {pct(row.Harmony)} | {pct(row.Bias)} | {pct(row.Extreme)} |";
            Assert.True(text.Contains(head), "yinyang.md 진소운 average 줄이 지금 데이터와 다르다" + hint);
            var gauge = $"| {row.NameKo} | " + string.Join(" | ", YinYangSim.Marks.Select(m =>
                row.GaugeMedian[m].ToString("+0;-0;0", System.Globalization.CultureInfo.InvariantCulture))) + " |";
            Assert.True(text.Contains(gauge), "yinyang.md 진소운 게이지 줄이 지금 데이터와 다르다" + hint);
        }

        [Fact] public void 가운데값()
        {
            Assert.Equal(2, YinYangSim.Median(new double[] { 3, 1, 2 }));
            Assert.Equal(2.5, YinYangSim.Median(new double[] { 4, 1, 2, 3 }));
            Assert.True(double.IsNaN(YinYangSim.Median(Array.Empty<double>())));
        }
    }
}
