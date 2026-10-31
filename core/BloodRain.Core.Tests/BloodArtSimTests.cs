using System;
using System.IO;
using System.Linq;
using BloodRain.Sim;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>혈공 시뮬레이션(BloodArtSim, docs/design/sim/blood-art.md). RunSimulator 에 얹은 혈공이 Core BloodArt 규칙대로 도는지 본다.</summary>
    public class BloodArtSimTests
    {
        static readonly Lazy<SimData> Data = new Lazy<SimData>(() => SimData.Load(TestData.DataDir));

        static SimResult Run(string chapter, double? density, double minutes, bool reset = true, int seed = 1) =>
            RunSimulator.Run(Data.Value, new SimOptions
            {
                Character = BloodArtSim.DemonicCharacter(Data.Value.Game), Policy = "average", Seed = seed, Minutes = minutes,
                Chapter = chapter, BloodArtDensity = density, BloodArtReset = reset,
            });

        [Fact] public void 혈공_몸은_천마해체를_쓰는_백야()
        {
            Assert.Equal("baekya", BloodArtSim.DemonicCharacter(Data.Value.Game));
        }

        [Fact] public void 혈공을_안_얹으면_예전과_같다()
        {
            var a = RunSimulator.Run(Data.Value, new SimOptions { Character = "baekya", Seed = 2, Minutes = 6 });
            var b = Run("chapter1", null, 6, seed: 2);
            Assert.Equal(a.At(6).TotalXp, b.At(6).TotalXp, 6);
            Assert.Equal(a.At(6).Gauge, b.At(6).Gauge);
            Assert.Equal(0, b.BloodArtSteps);
            Assert.Equal(0, b.BloodArtExtraXp);
        }

        [Fact] public void 농도_1이면_진기도_심마도_그대로()
        {
            // 천마해체 되돌림은 무공으로 기운 게이지까지 시작값으로 돌려 판이 달라지니 끄고 견준다
            var a = Run("chapter1", null, 6);
            var b = Run("chapter1", 1.0, 6, reset: false);
            Assert.Equal(a.At(6).TotalXp, b.At(6).TotalXp, 6);
            Assert.Equal(0, b.BloodArtSteps);
        }

        [Fact] public void 천마해체를_쓰면_게이지가_시작값으로()
        {
            var start = Data.Value.Game.Character("baekya").YinyangStart;
            var r = Run("chapter1", 1.0, 10);
            Assert.NotEmpty(r.UltimateMinutes);
            // 마지막 오의 뒤 레벨업이 없었던 분이면 시작값 그대로다. 적어도 한 분은 그렇다
            Assert.Contains(Enumerable.Range(1, 10), m => r.At(m).Gauge == start);
        }

        [Fact] public void 심마_횟수는_더_받은_진기를_300으로_나눈_몫()
        {
            var art = Data.Value.Game.Cheonha.Sects.Select(s => s.BloodArt).First(x => x != null)!;
            var r = Run("chapter7", 4.3, 8);
            Assert.True(r.BloodArtExtraXp > 0);
            Assert.Equal((int)Math.Floor(r.BloodArtExtraXp / art.ExtraXpPerStep), r.BloodArtSteps);
        }

        [Fact] public void 되돌리지_않으면_게이지가_음_끝까지_간다()
        {
            var r = Run("chapter7", 4.3, 20, reset: false);
            Assert.True(r.At(12).Gauge <= -70, $"게이지 {r.At(12).Gauge}");
            Assert.NotEmpty(r.BacklashMinutes);
        }

        [Fact] public void 오의를_쓴_시각을_센다()
        {
            var r = Run("chapter7", 4.3, 6);
            Assert.Equal(r.Ultimates, r.UltimateMinutes.Count);
            Assert.True(r.UltimateMinutes.SequenceEqual(r.UltimateMinutes.OrderBy(x => x)));
        }

        [Fact] public void 보고서_표가_지금_데이터와_같다()
        {
            // 보고서를 다시 만들지 않고 데이터만 바꾸면 깨진다. 고치려면: dotnet run --project core/BloodRain.Sim -c Release -- --blood-art docs/design/sim/blood-art.md
            var path = Path.Combine(TestData.RepoRoot, "docs", "design", "sim", "blood-art.md");
            var line = File.ReadAllLines(path).First(l => l.StartsWith("| 백야 · 십만대산, 혈공 |"));
            var row = BloodArtSim.Measure(Data.Value, "", "baekya",
                Data.Value.Game.Cheonha.Regions.First(r => r.Id == "sipmandaesan"), true, 5);
            Assert.EndsWith($"| {row.LevelMedian(10):0} | {row.LevelMedian(20):0} | {row.LevelMedian(30):0} |", line);
        }
    }
}
