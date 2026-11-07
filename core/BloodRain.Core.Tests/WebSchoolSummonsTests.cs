using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>웹이 소림 4 나한 허상·개방 4 제자를 Core SchoolSummons 와 같은 칸으로 도는지(SS-01).</summary>
    public class WebSchoolSummonsTests
    {
        [Fact] public void 같은_칸을_읽는다()
        {
            var html = TestData.ReadRepoFile("web", "prototype", "template.html");
            Assert.Contains("ar = synEff('arhat_ring'), bg = synEff('beggar_allies')", html);
            // 허상: 문턱 위였다가 아래로 떨어질 때, 판마다 timesPerRun 번
            Assert.Contains("S.last >= ar.healthThreshold && ratio < ar.healthThreshold && S.arhatT <= 0 && S.used < ar.timesPerRun", html);
            // 땅의 주인은 안 밀림(Core ArhatPush isBoss)
            Assert.Contains("forNear(p.x, p.y, ar.radius, e => { if (e.boss || e.dead) return;", html);
            // 제자: intervalSeconds 마다 allies 명 seconds 동안, attackSeconds 마다 damage × 공력
            Assert.Contains("while (S.clock >= bg.intervalSeconds) { S.clock -= bg.intervalSeconds; S.allyT = bg.seconds; S.allies = bg.allies; S.atk = 0;", html);
            Assert.Contains("dmg = bg.damage * G.stats.might", html);
            Assert.Contains("schoolSummons(dt);", html);
        }
    }
}
