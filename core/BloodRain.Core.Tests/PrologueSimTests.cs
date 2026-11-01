using System;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Sim;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>서장 흐름 시뮬레이션(PrologueSim, docs/design/sim/prologue.md).</summary>
    public class PrologueSimTests
    {
        static readonly Lazy<SimData> Data = new Lazy<SimData>(() => SimData.Load(TestData.DataDir));

        [Fact] public void 장삼이_나오는_시각은_chapters_json_과_같다()
        {
            var g = Data.Value.Game;
            var boss = g.Chapters.First(c => c.Id == "prologue").Events.Single(e => e.Type == WaveEventKind.Boss);
            Assert.Equal(PrologueSim.BossMinute, boss.At, 6);
            var r = PrologueSim.RunOne(Data.Value, 1);
            Assert.InRange(r.BossMinute, PrologueSim.BossMinute - 0.02, PrologueSim.BossMinute + 0.02);
        }

        [Fact] public void 서장_한_판은_차례대로_흐르고_시드가_같으면_같다()
        {
            var a = PrologueSim.RunOne(Data.Value, 3);
            var b = PrologueSim.RunOne(Data.Value, 3);
            Assert.Equal(a.LevelAt(9), b.LevelAt(9));
            Assert.Equal(a.FirstLevelUpMinute, b.FirstLevelUpMinute);
            Assert.True(a.FirstLevelUpMinute > 0);
            Assert.True(a.RealmMinute["iryu"] < a.RealmMinute["ilryu"]);
            // 강시 도부수는 5:00 에 나오니 그 전에 쓰러질 수 없다
            Assert.True(double.IsNaN(a.EliteKilledMinute) || a.EliteKilledMinute >= 5);
            // 서장은 진소운 제 몸으로 한다
            Assert.Equal(Data.Value.Game.Cheonha.PrologueBody.Character, a.Options.Character);
        }

        [Fact] public void 서장_모드는_숭산_적_구성과_다르다()
        {
            var pro = PrologueSim.RunOne(Data.Value, 1);
            var song = RunSimulator.Run(Data.Value, new SimOptions { Seed = 1, Minutes = PrologueSim.BossMinute });
            // 숭산 옛 1장 표에는 정예가 5분에 하나 있지만 장삼 사건은 없다
            Assert.True(double.IsNaN(song.BossMinute));
            Assert.False(double.IsNaN(pro.BossMinute));
            Assert.NotEqual(song.At(9).Spawned, pro.At(9).Spawned);
        }

        [Fact] public void 보고서는_세_정책을_싣는다()
        {
            var text = PrologueSim.Report(Data.Value, 2);
            foreach (var p in new[] { "average", "greedy", "slow" }) Assert.Contains($"| {p} |", text);
            Assert.Contains("이류 돌파", text);
            Assert.Contains("3:20", text);
        }

        [Fact] public void 시각_적기()
        {
            Assert.Equal("3:20", PrologueSim.Clock(10 / 3.0));
            Assert.Equal("0:40", PrologueSim.Clock(40 / 60.0));
            Assert.Equal("-", PrologueSim.Clock(double.NaN));
        }
    }
}
