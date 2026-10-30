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

namespace BloodRain.Core.Tests
{
    /// <summary>T-18 메타 곡선: 혼 하나로 생을 이어 돌리며 연무장을 산다.</summary>
    public class MetaSimTests
    {
        [Fact] public void 생을_이으면_연무장이_늘고_버티는_시간도_는다()
        {
            var rows = BloodRain.Sim.MetaSim.Run(BloodRain.Sim.SimData.Load(TestData.DataDir), 8, null, maxLives: 4);
            Assert.Equal(4, rows.Count);
            Assert.Equal("소림", rows[0].Sect); // 첫 환혼 후보(소림·개방·남궁) 중 땅의 주인이 있는 숭산
            Assert.Equal(8, rows[0].Minutes);
            for (int i = 1; i < rows.Count; i++)
            {
                Assert.True(rows[i].TrainingLevels >= rows[i - 1].TrainingLevels);
                Assert.True(rows[i].Minutes >= rows[i - 1].Minutes);
                Assert.True(rows[i].Hours > rows[i - 1].Hours);
            }
            Assert.True(rows[0].TrainingLevels > 0);
        }
    }
}
