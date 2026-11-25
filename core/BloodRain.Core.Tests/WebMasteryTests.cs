using BloodRain.Core.Meta;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>GU-06 웹 몫: 웹 한 생 판의 무공 성취가 Core Soul.MasteryStage·EndLife·Life.StartMastered 와 같은 규칙인지 글로 본다.</summary>
    public class WebMasteryTests
    {
        static string Proto => ReadRepoFile("web", "prototype", "template.html");

        [Fact] public void 소성_대성_문턱이_Core_와_같다()
        {
            Assert.Contains($"const MASTERY_LIVES = [{Soul.MasteryLives[0]}, {Soul.MasteryLives[1]}];", Proto);
            Assert.Contains("n >= MASTERY_LIVES[1] ? 2 : n >= MASTERY_LIVES[0] ? 1 : 0", Proto);
        }

        [Fact] public void 최고_레벨로_마친_무공만_세고_대성이면_Lv2_로_시작한다()
        {
            var html = Proto;
            foreach (var used in new[]
            {
                "G.weapons.filter(w => w.lv >= B.levels.weaponMax)",               // Core Life.End MasteredWeapons
                "m.mastery[id] = (m.mastery[id] || 0) + 1",                         // Core Soul.EndLife
                "G.startMastered = masteryStage(G.soul0, wd.id) >= 2",             // Core Life: 대성(2)부터
                $"lv: G.startMastered ? Math.min({BloodRain.Core.Run.Life.MasteredStartLevel}, B.levels.weaponMax) : 1",
                "grp('무공 성취'",                                                   // 도감 무공 쪽 도장
            })
                Assert.Contains(used, html);
        }
    }
}
