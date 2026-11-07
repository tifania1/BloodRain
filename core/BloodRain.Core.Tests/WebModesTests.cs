using System.Linq;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>WP-67: 웹 한 생 판이 모드 열림을 Core Meta/Modes 와 같은 조건·같은 토큰으로 센다.</summary>
    public class WebModesTests
    {
        static string Proto => ReadRepoFile("web", "prototype", "template.html");

        [Fact] public void 조건과_토큰이_Core_와_같다()
        {
            var html = Proto;
            Assert.Equal("mode:", BloodRain.Core.Meta.Modes.TokenPrefix);
            Assert.Contains("const t = 'mode:' + d.id;", html);
            Assert.Contains("if (r.prologue && !m.prologue) return false;", html);
            Assert.Contains("if ((m.stones || []).length < (r.sealStones || 0)) return false;", html);
            Assert.Contains("if (r.ending && !(m.endingsSeen || []).includes(r.ending)) return false;", html);
            // 데이터의 조건 칸은 셋뿐이다(늘면 웹·Core 둘 다 고친다)
            var json = Newtonsoft.Json.Linq.JObject.Parse(ReadRepoFile("unity", "BloodRain", "Assets", "Resources", "Data", "cheonha.json"));
            var keys = json["modes"]!.SelectMany(m => ((Newtonsoft.Json.Linq.JObject)m["require"]!).Properties().Select(p => p.Name)).Distinct().OrderBy(x => x);
            Assert.Equal(new[] { "ending", "prologue", "sealStones" }, keys);
        }

        [Fact] public void 서장_끝과_생을_마칠_때_부르고_정산에_보인다()
        {
            var html = Proto;
            Assert.Contains("modeRefresh(m);   // 천하 모드(서장 끝", html);
            Assert.Contains("m.newModes = modeRefresh(m);", html);
            Assert.True(html.IndexOf("m.newModes = modeRefresh(m);") > html.IndexOf("if (!m.endingsSeen.includes(st.ending)) m.endingsSeen.push(st.ending);"), "엔딩을 적은 뒤에 센다");
            Assert.Contains("'새 모드 · 이 판엔 아직 없다'", html);
            Assert.Contains("\"modes\": [drop_notes(m) for m in load(\"cheonha\").get(\"modes\", [])]", ReadRepoFile("tools", "build_web_prototype.py"));
        }
    }
}
