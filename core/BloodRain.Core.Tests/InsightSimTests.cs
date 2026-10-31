using System;
using System.IO;
using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Sim;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>돌파 깨달음 시뮬레이션(InsightSim, docs/design/sim/insights.md). 후보 확률 셈, 피해 출처 기록, 살기충천 셈이 맞는지 본다.</summary>
    public class InsightSimTests
    {
        static readonly Lazy<SimData> Data = new Lazy<SimData>(() => SimData.Load(TestData.DataDir));

        [Fact] public void 섞어_본_확률이_바로_셈한_값과_같다()
        {
            var ins = Insights.From(Data.Value.Game);
            int n = ins.All.Count;
            foreach (int c in new[] { 3, 4 })
                for (int k = 1; k <= 5; k++)
                {
                    double mc = InsightSim.SeenWithin(ins, ins.All[2].Id, c, k, 20000, 3);
                    Assert.True(Math.Abs(mc - InsightSim.SeenWithinExact(n, c, k)) < 0.015, $"후보 {c}장 돌파 {k}번: {mc}");
                }
        }

        [Fact] public void 첫_돌파는_후보_수_나누기_깨달음_수()
        {
            Assert.Equal(3.0 / 7, InsightSim.SeenWithinExact(7, 3, 1), 9);
            Assert.Equal(1.0, InsightSim.SeenWithinExact(7, 3, 5), 9);   // 남은 풀이 후보 수 이하면 반드시 보인다
            Assert.Equal(0.0, InsightSim.SeenWithinExact(7, 3, 0), 9);
        }

        [Fact] public void 깨달음_고르는_경지는_보상에_insight_choice_가_붙은_곳()
        {
            var g = Data.Value.Game;
            var realms = InsightSim.InsightRealms(g);
            Assert.Equal(g.Balance.Realms.Count(r => r.Reward != null && r.Reward.Contains("insight_choice")), realms.Count);
            Assert.Equal(realms.OrderBy(r => r.Level).Select(r => r.Id), realms.Select(r => r.Id));
        }

        [Fact] public void 살기충천은_100마리마다_1퍼센트_상한_30()
        {
            var g = Data.Value.Game;
            Assert.Equal(0, InsightSim.SalgiMight(g, 99), 6);
            Assert.Equal(0.01, InsightSim.SalgiMight(g, 100), 6);
            Assert.Equal(0.29, InsightSim.SalgiMight(g, 2999), 6);
            Assert.Equal(0.3, InsightSim.SalgiMight(g, 3000), 6);
            Assert.Equal(0.3, InsightSim.SalgiMight(g, 100000), 6);
            Assert.Equal(0, InsightSim.SalgiMight(g, -5), 6);
        }

        [Fact] public void 출처별_피해를_더하면_분마다_기록한_피해와_같다()
        {
            foreach (var ch in new[] { "jin_sowoon", "hyegong" })
            {
                var r = RunSimulator.Run(Data.Value, new SimOptions { Character = ch, Seed = 2, Minutes = 8 });
                double perMinute = r.Minutes.Sum(m => m.CrowdDamagePerSecond * 60);
                Assert.Equal(perMinute, r.DamageBySource.Values.Sum(), 3);
                Assert.True(r.DamageBySource[Data.Value.Game.Character(ch).StartWeapon] > 0);
                Assert.All(r.DamageBySource.Values, v => Assert.True(v >= 0));
            }
        }

        [Fact] public void 피해_기록은_판_흐름을_바꾸지_않는다()
        {
            // 같은 시드 두 번이 같고, 보고서 yinyang.md 의 진소운 평균 줄과도 어긋나지 않게 난수를 더 쓰지 않는다
            var a = RunSimulator.Run(Data.Value, new SimOptions { Seed = 5, Minutes = 10 });
            var b = RunSimulator.Run(Data.Value, new SimOptions { Seed = 5, Minutes = 10 });
            Assert.Equal(a.Minutes.Select(m => m.Kills), b.Minutes.Select(m => m.Kills));
            Assert.Equal(a.DamageBySource.OrderBy(kv => kv.Key), b.DamageBySource.OrderBy(kv => kv.Key));
        }

        [Fact] public void 보고서_후보_확률표가_지금_데이터와_같다()
        {
            var path = Path.Combine(TestData.RepoRoot, "docs", "design", "sim", "insights.md");
            var lines = File.ReadAllLines(path);
            var g = Data.Value.Game;
            var ins = Insights.From(g);
            int k = InsightSim.InsightRealms(g).Count;
            string row = "| 3장 |" + string.Concat(Enumerable.Range(1, k)
                .Select(i => $" {Math.Round(InsightSim.SeenWithin(ins, g.Insights[0].Id, 3, i, 20000, 7) * 100):0}% |"));
            Assert.Contains(row, lines);
        }
    }
}
