using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>
    /// WP-11: 두 웹 판(1장 프로토타입·천하 시안)이 HUD·멈춤 문구를 strings/ko.json 의 같은 키로 읽는지 글로 본다.
    /// 빌드가 ui. 키를 DATA.ui 로 넣고, 화면은 uiT(키)·data-s·data-s-aria 로 쓴다.
    /// </summary>
    public class WebStringsTests
    {
        static string Proto => ReadRepoFile("web", "prototype", "template.html");
        static string Cheonha => ReadRepoFile("web", "lookdev", "cheonha.html");

        static string[] KoKeys()
        {
            using var doc = JsonDocument.Parse(ReadRepoFile("unity", "BloodRain", "Assets", "Resources", "Data", "strings", "ko.json"));
            return doc.RootElement.EnumerateObject().Select(p => p.Name).ToArray();
        }

        static string[] UsedKeys(string html) =>
            Regex.Matches(html, @"uiT\('(ui\.[a-z0-9_.]+)'|data-s(?:-aria)?=""(ui\.[a-z0-9_.]+)""")
                .Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value)
                .Distinct().ToArray();

        [Fact] public void 빌드가_ui_문구를_두_묶음에_넣는다()
        {
            var build = ReadRepoFile("tools", "build_web_prototype.py");
            Assert.Equal(2, Regex.Matches(build, @"""ui"": ui_strings\(\)").Count);
        }

        [Fact] public void 두_판이_쓰는_키가_ko_json_에_있다()
        {
            var ko = KoKeys();
            foreach (var html in new[] { Proto, Cheonha })
            {
                var used = UsedKeys(html);
                Assert.NotEmpty(used);
                foreach (var k in used) Assert.Contains(k, ko);
            }
        }

        [Fact] public void HUD_같은_칸은_두_판이_같은_키를_쓴다()
        {
            // 두 판에 다 있는 칸: 처치, 멈춤(단추·화면·조작 안내), 경공·오의 단추
            foreach (var k in new[] { "ui.hud.kills", "ui.hud.pause", "ui.hud.dash", "ui.hud.ult", "ui.hud.move", "ui.hud.pick", "ui.pause.title", "ui.pause.sub" })
            {
                Assert.Contains(k, UsedKeys(Proto));
                Assert.Contains(k, UsedKeys(Cheonha));
            }
        }

        [Fact] public void 처치_은자_윤회의_기_봉인석을_글자로_박지_않는다()
        {
            Assert.DoesNotContain("`처치 ${", Proto);
            Assert.DoesNotContain("`은자 ${G.silver}`", Proto);
            Assert.DoesNotContain("`처치 ${", Cheonha);
            Assert.DoesNotContain("textContent = `윤회의 기 +", Cheonha);
            Assert.DoesNotContain("textContent = `봉인석 ${", Cheonha);
        }
    }
}
