using System;
using System.IO;
using System.Linq;
using BloodRain.Sim;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>사람 결정 묶음(DecisionSim, docs/design/sim/decisions.md). 조정 시험이 천하·혈공 판에 실제로 얹히는지 본다.</summary>
    public class DecisionSimTests
    {
        static readonly Lazy<SimData> Data = new Lazy<SimData>(() => SimData.Load(TestData.DataDir));

        [Fact] public void 천하_한_생에_조정_시험이_얹힌다()
        {
            var lean = Scenarios.Find("kill-xp");
            var a = CheonhaSim.Run(Data.Value, "shaolin", 1);
            var b = CheonhaSim.Run(Data.Value, "shaolin", 1, lean.ApplyOptions);
            Assert.True(b.LevelByMinute[10] < a.LevelByMinute[10], $"처치 진기를 줄였는데 10분 레벨이 {a.LevelByMinute[10]} → {b.LevelByMinute[10]}");
        }

        [Fact] public void 정책_인자_겹침은_예전과_같다()
        {
            var a = CheonhaSim.Run(Data.Value, "wudang", 2, "greedy");
            var b = CheonhaSim.Run(Data.Value, "wudang", 2, o => o.With(x => x.Policy = "greedy"));
            Assert.Equal(a.FinalLevel, b.FinalLevel);
            Assert.Equal(a.LevelByMinute, b.LevelByMinute);
        }

        [Fact] public void 혈공_측정에_조정_시험이_얹힌다()
        {
            var g = Data.Value.Game;
            var songshan = g.Cheonha.Regions.First(r => r.Chapter == "chapter1");
            var a = BloodArtSim.Measure(Data.Value, "a", BloodArtSim.Baseline, songshan, false, 1, minutes: 10);
            var b = BloodArtSim.Measure(Data.Value, "b", BloodArtSim.Baseline, songshan, false, 1, minutes: 10, apply: Scenarios.Find("kill-xp").ApplyOptions);
            Assert.True(b.LevelMedian(10) < a.LevelMedian(10));
        }

        [Fact] public void 제곱근_농도는_진기를_덜_준다()
        {
            var g = Data.Value.Game;
            var home = g.Cheonha.Regions.First(r => r.Id == g.Cheonha.Sects.First(s => s.BloodArt != null).Region);
            string bk = BloodArtSim.DemonicCharacter(g);
            var full = BloodArtSim.Measure(Data.Value, "full", bk, home, true, 1, minutes: 8);
            var sqrt = BloodArtSim.Measure(Data.Value, "sqrt", bk, home, true, 1, minutes: 8, xpDensity: Math.Sqrt(home.Density));
            Assert.True(sqrt.Runs[0].BloodArtExtraXp < full.Runs[0].BloodArtExtraXp);
            Assert.True(sqrt.LevelMedian(8) <= full.LevelMedian(8));
            // 적 기혈 배율은 그 땅 그대로
            Assert.Equal(full.Chapter, sqrt.Chapter);
        }

        [Fact] public void 보고서_결론줄이_지금_데이터와_같다()
        {
            // 보고서는 시드 3. 같은 시드로 혈공 격차만 다시 재서 보고서 줄과 견준다(천하 쪽은 오래 걸려 뺀다)
            var md = TestData.ReadText(Path.Combine(TestData.RepoRoot, "docs", "design", "sim", "decisions.md"));
            var g = Data.Value.Game;
            var top = g.Balance.Realms.OrderBy(r => r.Level).Last().Id;
            var home = g.Cheonha.Regions.First(r => r.Id == g.Cheonha.Sects.First(s => s.BloodArt != null).Region);
            var songshan = g.Cheonha.Regions.First(r => r.Chapter == "chapter1");
            var b = BloodArtSim.Measure(Data.Value, "기준", BloodArtSim.Baseline, songshan, false, 3);
            var x = BloodArtSim.Measure(Data.Value, "혈공", BloodArtSim.DemonicCharacter(g), home, true, 3);
            double d = x.RealmMedian(top) - b.RealmMedian(top);
            int sec = (int)Math.Round(Math.Abs(d) * 60);
            string want = $"| 혈공 현경 − 기준 현경 | {(d >= 0 ? "+" : "-")}{sec / 60}:{sec % 60:00} |";
            Assert.Contains(want, md);
        }
    }
}
