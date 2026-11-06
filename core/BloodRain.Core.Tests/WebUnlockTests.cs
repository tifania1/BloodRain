using System.Linq;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>
    /// WP-17: 웹 프로토타입 한 생 판이 무공 해금(기획서 9장 해금 칸·10장 도전 과제)을 Core Soul 과 같은 규칙으로 하는지 글로 본다.
    /// </summary>
    public class WebUnlockTests
    {
        static string Proto => ReadRepoFile("web", "prototype", "template.html");

        [Fact] public void 도전_과제_토큰_모양이_Core_와_같다()
        {
            Assert.Contains("const CHALLENGE_RE = /^(kills|dash|seal_talisman)_(\\d+)_(one_run|total)$/;", Proto);
            var core = ReadRepoFile("unity", "BloodRain", "Assets", "Scripts", "Core", "Data", "Definitions.cs");
            Assert.Contains("@\"^(kills|dash|seal_talisman)_(\\d+)_(one_run|total)$\"", core);
        }

        [Fact] public void 데이터의_해금_토큰을_웹이_다_읽을_수_있다()
        {
            // start · boss:<땅의 주인이 있는 지역> · 도전 과제, 이 셋 말고 다른 모양이 생기면 웹 soulUnlocked 도 고쳐야 한다
            var data = Load();
            var bossRegions = data.Cheonha.Regions.Where(r => r.Boss != null).Select(r => r.Id).ToHashSet();
            foreach (var w in data.Weapons)
            {
                var t = w.Unlock;
                bool ok = t == "start" || (t.StartsWith("boss:") && bossRegions.Contains(t.Substring(5)))
                    || BloodRain.Core.Data.UnlockToken.ParseChallenge(t) != null;
                Assert.True(ok, $"{w.Id} 해금 '{t}' 을 웹이 모른다");
            }
        }

        [Fact] public void 한_생_판은_혼_기록으로_레벨업_풀을_정하고_정산에서_연다()
        {
            var html = Proto;
            Assert.Contains("G.wpool = weaponPoolOf(G.soul0);", html);
            Assert.Contains("for (const id of (G.wpool || START_WEAPONS))", html);
            Assert.Contains("G.dashes = (G.dashes || 0) + 1;", html);
            Assert.Contains("G.talis = (G.talis || 0) + 1;", html);
            Assert.Contains("m.talismans = (m.talismans || 0) + (st.talis || 0);", html);
            Assert.Contains("for (const t of st.newUnlocks || []) if (!m.unlocks.includes(t)) m.unlocks.push(t);", html);
            Assert.Contains("'새로 얻은 무공 · 다음 생부터'", html);
        }
    }
}
