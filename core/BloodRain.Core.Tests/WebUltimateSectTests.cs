using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>WP-25: 웹 한 생 판이 몸 문파의 오의를 쓰고, 박자·숫자가 characters.json ultimates params 와 Core UltimateCast 를 따르는지 글로 본다.</summary>
    public class WebUltimateSectTests
    {
        static string Proto => ReadRepoFile("web", "prototype", "template.html");

        [Fact] public void 빌드가_문파_오의를_넣는다()
        {
            Assert.Contains("\"ultimate\": s.get(\"ultimate\")}", ReadRepoFile("tools", "build_web_prototype.py"));
        }

        [Fact] public void 박자는_Core_UltimateCast_와_같다()
        {
            var html = Proto;
            Assert.Contains("per > 0 ? Math.max(1, Math.ceil(dur / per - 1e-4)) : 1", html);
            Assert.Contains("u.mightMultiplier <= 0 ? 0", html);   // 배율 0 은 안 때림
            Assert.Contains("for (let c = 0; c < (G.stats.ultCasts || 1); c++) ultPulse(C.u, C, c)", html);   // 현경 2연속
            Assert.Contains("ultBaseHit() * u.mightMultiplier * G.stats.ultPower", html);
        }

        [Fact] public void 모양_숫자는_데이터에서()
        {
            var html = Proto;
            foreach (var k in new[] { "'strikeRadius'", "'shockSeconds'", "'strikes'", "'radius'", "'stunSeconds'", "'poisonSeconds'", "'freezeSeconds'", "'healthCostPerSecond'", "'lifesteal'", "'damageMultiplier'" })
                Assert.Contains(k, html);
            Assert.Contains("A.resetUltimate === u.id", html);   // 혈공은 천마해체를 쓸 때만 음양이 돌아온다
        }
    }
}
