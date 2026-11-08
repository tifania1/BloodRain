using System.Linq;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>
    /// synergies.json 연환의 proto 표시(웹 프로토타입에 들어감)가 template.html 과 맞는지(LK-02).
    /// proto 연환은 웹이 link('id') 로 켜고, params 숫자는 박지 않고 linkP('id', 'key') 로 읽는다.
    /// </summary>
    public class WebLinksTests
    {
        static string Html => TestData.ReadRepoFile("web", "prototype", "template.html");

        [Fact] public void proto_연환은_웹이_켜고_숫자를_데이터에서_읽는다()
        {
            var html = Html;
            foreach (var l in TestData.Load().Synergies.Links.Where(l => l.Proto))
            {
                Assert.True(html.Contains($"link('{l.Id}')"), $"{l.Id} 가 proto 인데 웹에 link('{l.Id}') 가 없다");
                foreach (var key in (l.Params ?? new()).Keys)
                    Assert.True(html.Contains($"linkP('{l.Id}', '{key}')"), $"{l.Id}.{key} 를 웹이 데이터에서 안 읽는다");
            }
        }

        [Fact] public void proto_가_아닌_연환은_웹이_안_켠다()
        {
            // 켜지는데 아무 일도 안 하는 연환이 없게: 웹에 들어가면 proto 를 붙인다. 안 붙은 것은 알림에 '(정식판에서)'
            var html = Html;
            foreach (var l in TestData.Load().Synergies.Links.Where(l => !l.Proto))
                Assert.False(html.Contains($"link('{l.Id}')"), $"{l.Id} 가 웹에 들어갔으면 synergies.json 에 proto: true");
            Assert.Contains("${l.proto ? '' : ' (정식판에서)'}", html);
        }

        [Fact] public void 금강후_점혈창_혈독()
        {
            var html = Html;
            Assert.Contains("if (link('geumganghu') && G.t < (G.fistUntil || 0)) { R *= 1 + linkP('geumganghu', 'radiusBonus'); chance *= linkP('geumganghu', 'stunChanceMultiplier'); }", html);
            Assert.Contains("G.fistUntil = G.t + hits * def.gapSeconds", html);
            // 점혈창: 섬광지가 짚은 적(땅의 주인 빼고)을 이화창이 찌르면 반드시 치명타
            Assert.Contains("pr.w.id === 'flash_finger' && link('jeomhyeol') && !e.boss", html);
            Assert.Contains("hurtEnemy(e, dmg, w, ca, sa, def.knockback, link('jeomhyeol') && (e.pressed || 0) > G.t)", html);
            Assert.Contains("if (sureCrit || Math.random() < G.stats.crit)", html);
            // 혈독: 중독된 적을 할퀸 자리에서 spreadRadius 로 번짐
            Assert.Contains("if (e.poison > 0) spreadAt.push([e.x, e.y]);", html);
        }
    }
}
