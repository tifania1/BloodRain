using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>GU-11h4·GU-11m: 웹 한 생 판의 문파의 위기가 chronicle.json 값과 Core Life.Crisis·HoldCrises·CrisisDensity 와 같은 규칙인지 글로 본다.</summary>
    public class WebCrisisTests
    {
        static string Proto => ReadRepoFile("web", "prototype", "template.html");

        [Fact] public void 빌드가_연표의_문파_위기와_버팀_값을_넣는다()
        {
            var build = ReadRepoFile("tools", "build_web_prototype.py");
            Assert.Contains("\"chronicle\": {\"crisis\": drop_notes(load(\"chronicle\")[\"crisis\"])", build);
            Assert.Contains("if e[\"kind\"] == \"sect_crisis\" and e.get(\"region\")", build);
        }

        [Fact] public void 겪음_버팀_무너짐이_Core_와_같다()
        {
            var html = Proto;
            foreach (var used in new[]
            {
                "R = viewDiag() * U * 0.5",                                         // 겪음과 같은 반경: 화면 대각선 절반
                "now = G.gm * 60",                                                  // 생 시작부터 초(Core Minutes × 60)
                "H[e.id] = Math.min(H[e.id] + dt * G.timeScale, now - e.atSeconds)",  // 사건 시각을 넘긴 몫만
                "H[e.id] >= (CRISIS.holdSeconds || 60)",
                "add += CRISIS.fallenEachDensity || 0; if (e.region === regionId) add += CRISIS.fallenRegionDensity || 0;",
                "densityAt(G.player.x * worldU(), G.player.y * worldU()) + crisisDensity(r.id)",
                "newBlocks.length * QI.crisisFirstBlock",                           // 혼에 처음 막은 것만
                "m.crisesBlocked.push(id)",
            })
                Assert.Contains(used, html);
            // 정산 줄 순서는 Core RebirthQiLineKind: 오른 경지 → 처음 막은 위기 → 은자
            Assert.True(html.IndexOf("처음 오른 경지 ${newRealms.length}") < html.IndexOf("처음 막은 문파의 위기 ${newBlocks.length}"));
            Assert.True(html.IndexOf("처음 막은 문파의 위기 ${newBlocks.length}") < html.IndexOf("rows.push([`남은 은자"));
        }
    }
}
