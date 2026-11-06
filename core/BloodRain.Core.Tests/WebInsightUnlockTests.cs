using System.Linq;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>WP-35: 10장 해금 표 '극성(±70 이상) 상태로 땅의 주인 처치 → 깨달음 역근 풀에 추가'를 웹 한 생 판도
    /// Core(Insights.Roll isUnlocked, Life 의 UnlockToken.BossAtExtreme)와 같게 하는지 글로 본다.</summary>
    public class WebInsightUnlockTests
    {
        static string Proto => ReadRepoFile("web", "prototype", "template.html");

        [Fact] public void 역근은_해금_토큰이_있고_다른_깨달음은_처음부터()
        {
            var d = Load();
            Assert.Equal(new[] { "yeokgeun" }, d.Insights.Where(i => i.Unlock != "start").Select(i => i.Id));
            Assert.Equal("boss_at_extreme", d.Insights.Single(i => i.Id == "yeokgeun").Unlock);
            Assert.Contains("| 극성(±70 이상) 상태로 땅의 주인 처치 | 깨달음 '역근' 풀에 추가 |", Gdd);
        }

        [Fact] public void 웹_한_생_판은_혼에_토큰이_있거나_이번_생에_극성으로_잡았을_때만()
        {
            var html = Proto;
            Assert.Contains("const insightOpen = x => !G.body || soulUnlocked(G.soul0 || {}, x.unlock) || (x.unlock === 'boss_at_extreme' && !!G.extremeKill);", html);
            Assert.Contains("INSIGHTS.filter(x => !G.insights.includes(x.id) && insightOpen(x))", html);
            Assert.Contains("if (yyBand().band === 'extreme') G.extremeKill = true;", html);
            Assert.Contains("if (st.extremeKill && !m.unlocks.includes('boss_at_extreme')) m.unlocks.push('boss_at_extreme');", html);
        }
    }
}
