using System;
using System.Linq;
using BloodRain.Sim;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>T-17 천하 한 생 시뮬레이터가 Core Life 를 끝까지 돌리는지(숫자 자체는 보고서 몫).</summary>
    public class CheonhaSimTests
    {
        static readonly Lazy<CheonhaLifeResult> Shaolin = new Lazy<CheonhaLifeResult>(() =>
            CheonhaSim.Run(SimData.Load(TestData.DataDir), "shaolin", 1));

        [Fact] public void 소림은_숭산에서_태어나_경지를_차례로_오른다()
        {
            var r = Shaolin.Value;
            Assert.Equal("숭산", r.Birth);
            var mins = r.RealmMinute.OrderBy(kv => kv.Value).Select(kv => kv.Value).ToList();
            Assert.True(mins.Count >= 3);
            Assert.Equal(mins.OrderBy(x => x), mins);
        }

        [Fact] public void 원각이_깨어나면_걸어가_싸우고_정산에_보스_몫이_있다()
        {
            var r = Shaolin.Value;
            var wongak = r.Bosses.Single(b => b.BossKo == "혈나한 원각");
            Assert.False(double.IsNaN(wongak.EngagedAt));
            Assert.True(wongak.EngagedAt >= wongak.WokeAt);
            Assert.True(wongak.FightSeconds > 0);
            Assert.True(r.Qi.Total > r.Qi.FromOrbs);
            Assert.Contains("songshan", r.Visited);
        }
    }
}
