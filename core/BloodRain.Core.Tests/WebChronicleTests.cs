using System.Linq;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>CX-01: 웹 연표 화면이 chronicle.json 사건 전부를 넣고, 겪음 판정이 Core Life.NoteChronicle 와 같은 규칙인지 글로 본다.</summary>
    public class WebChronicleTests
    {
        static string Proto => ReadRepoFile("web", "prototype", "template.html");
        static string Build => ReadRepoFile("tools", "build_web_prototype.py");

        [Fact] public void 빌드가_사건_전부와_자리_칸을_넣는다()
        {
            Assert.Contains("\"chronicleAll\": [{k: e.get(k) for k in (\"id\", \"kind\", \"atSeconds\", \"region\", \"pos\", \"offset\", \"minRealm\", \"everywhere\", \"nameKo\", \"placeKo\", \"branches\")} for e in load(\"chronicle\")[\"events\"]]", Build);
            Assert.Contains("\"camp\": s.get(\"camp\")", Build);
        }

        [Fact] public void 겪음_판정이_Core_와_같다()
        {
            var html = Proto;
            foreach (var used in new[]
            {
                "if (e.atSeconds <= from || e.atSeconds > now) continue;",                          // 이번 프레임에 시각이 지난 사건
                "e.everywhere ? ri >= chronMinRealm(e) : Math.hypot(e.x - px, e.y - py) <= R",       // 천하 전체는 경지, 나머지는 화면 대각선 절반
                "R = viewDiag() * U * 0.5",
                "if (!G.body || !SCALE_MODE || isPro() || G.rush || !CHRON_ALL.length) return;",    // 서장은 안 봄
                "x = r.x + ((e.offset || [])[0] || 0)",                                              // Chronicle.Position: 지역 가운데 + offset
                "m.chronicle.push(id)",                                                              // Soul.Chronicle
                "(e.branches || []).find(b => b.camp === G.body.camp)",                              // Chronicle.BranchFor(CX-04)
            })
                Assert.Contains(used, html);
        }

        [Fact] public void 천하_전체_사건은_자리가_없고_나머지는_자리나_지역이_있다()
        {
            var data = Load();
            foreach (var e in data.Chronicle.Events)
                Assert.True(e.Everywhere ? e.Pos == null && e.Region == null : e.Pos != null || e.Region != null, e.Id);
        }
    }
}
