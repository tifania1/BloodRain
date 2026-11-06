using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>
    /// WP-27: 웹 한 생 판 기연 추첨이 Core EncounterRoller 와 같은 규칙인지 글로 본다
    /// (사건마다 판에 한 번, 확률 = chance + 기연 × luck.chancePerLuck, chapters·minMinute·needsChapterSchool).
    /// </summary>
    public class WebEncounterTests
    {
        static string Proto => ReadRepoFile("web", "prototype", "template.html");

        [Fact] public void 확률과_나올_형편이_Core_와_같다()
        {
            var html = Proto;
            Assert.Contains("Math.max(0, Math.min(1, e.chance + c.luck * SYN.luck.chancePerLuck))", html);
            Assert.Contains("!(e.chapters && (!c.chapter || !e.chapters.includes(c.chapter))) && c.minute >= (e.minMinute || 0) && !(e.needsChapterSchool && !c.school)", html);
            var core = ReadRepoFile("unity", "BloodRain", "Assets", "Scripts", "Core", "Spawning", "Encounters.cs");
            Assert.Contains("double p = e.Chance + ctx.Luck * _syn.Luck.ChancePerLuck;", core);
            Assert.Contains("if (e.NeedsChapterSchool && ctx.ChapterSchool == null) return false;", core);
        }

        [Fact] public void 한_생_판은_한_번만_굴리고_유해는_그_땅_문파()
        {
            var html = Proto;
            Assert.Contains("G.body && !ev.rolled", html);
            Assert.Contains("if (!encCanAppear(e, c)) { ev.at = G.gm + 1; continue; }", html);
            Assert.Contains("S.bonus[sc.id] = (S.bonus[sc.id] || 0) + 1", html);
            Assert.DoesNotContain("S.bonus.shaolin = (S.bonus.shaolin || 0) + 1", html);
        }

        [Fact] public void 은거기인_곁_시간은_기획서_20장대로()
        {
            Assert.Contains("| 은거기인 | 맵 구석에서 바둑 두는 노인 | 곁에서 6초 동안 떠나지 않는다", Gdd);
            Assert.Contains("if (ev.prog >= 6 && !overlay) { ev.done = true; ev.met = true; discover('enc:hermit');", Proto);
        }
    
        [Fact] public void 영약은_한_생_판에서_오의_가득_음양_0()
        {
            var html = Proto;
            Assert.Contains("for (const key of ['relic', 'hermit', 'elixir'])", html);
            Assert.Contains("(key === 'elixir' && !G.body)", html);
            Assert.Contains("discover('enc:elixir'); G.ult = B.ultimateCharge.max; G.yy = 0;", html);
            Assert.Contains("| 영약 | 빛나는 덩굴·바위 틈(천년하수오, 공청석유) | 줍는다 | 오의 게이지 가득, 음양 0 |", Gdd);
        }
    }
}
