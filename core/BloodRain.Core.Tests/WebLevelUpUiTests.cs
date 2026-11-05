using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>
    /// UI-02(기획서 12장 '레벨업 카드 규칙'·'화면 편의 규칙'): 1장 프로토타입 레벨업 화면이 카드 세 줄·옆 칸·단축키·남은 N·파기를 갖는지 글로 본다.
    /// </summary>
    public class WebLevelUpUiTests
    {
        static string Proto => ReadRepoFile("web", "prototype", "template.html");

        [Fact] public void 카드는_세_줄이고_긴_설명은_마우스를_올리면()
        {
            var html = Proto;
            Assert.Contains("<p class=\"badges\">", html);
            Assert.Contains("b.title = [desc, ...badges.map(x => x[1])].join('\\n');", html);
        }

        [Fact] public void 옆_칸이_무공_심법_칸을_보이고_고르려는_카드가_켜는_칸을_빛낸다()
        {
            var html = Proto;
            Assert.Contains("id=\"luslots\"", html);
            Assert.Contains("row('w', G.weapons, G.weaponSlots", html);
            Assert.Contains("row('p', G.passives, G.passiveSlots", html);
            Assert.Contains("function cardLights(o)", html);
        }

        [Fact] public void 다시_고르기_파기_정진에_단축키를_적고_남은_레벨업을_보인다()
        {
            var html = Proto;
            Assert.Contains("남은 2회 · R", html);
            Assert.Contains("남은 0회 · X", html);
            Assert.Contains("안 고르고 오의 +20 · Z", html);
            Assert.Contains("if (e.code === 'KeyX' && !e.repeat) toggleBanish();", html);
            Assert.Contains("if (e.code === 'KeyZ' && !e.repeat) skipOffer();", html);
            Assert.Contains("`남은 레벨업 ${G.pendingLevels}`", html);
            // 열린 직후 0.35초 입력 막기(12장 '레벨업 입력')
            Assert.Contains("performance.now() - overlayAt < 350", html);
        }
    }
}
