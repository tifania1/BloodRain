using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>땅의 주인·정예 몸 반지름(BD-02). 웹 그림 크기에 박혀 있던 값을 balance.json 으로 옮겼다.</summary>
    public class BodyRadiusTests
    {
        [Fact]
        public void 보스는_정예보다_정예는_플레이어보다_크다()
        {
            var s = Load().Balance.Stats;
            Assert.Equal(1.18f, s.BossBodyRadius, 3);
            Assert.Equal(0.78f, s.EliteBodyRadius, 3);
            Assert.True(s.BossBodyRadius > s.EliteBodyRadius && s.EliteBodyRadius > s.PlayerBodyRadius);
        }

        [Fact]
        public void 기획서와_웹이_같은_값을_쓴다()
        {
            Assert.Contains("땅의 주인 몸은 반지름 1.18m, 정예는 0.78m", Gdd);
            var html = ReadRepoFile("web", "prototype", "template.html");
            Assert.DoesNotContain("KIND_SIZE.boss * 0.28", html);
            Assert.DoesNotContain("KIND_SIZE.elite * 0.3", html);
            Assert.Contains("r: B.stats.bossBodyRadius", html);
            Assert.Contains("r: B.stats.eliteBodyRadius", html);
        }
    }
}
