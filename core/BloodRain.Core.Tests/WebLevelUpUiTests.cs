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

        [Fact] public void 능력치_이름은_ui_stat_에_쉬운_말을_붙인다()
        {
            var html = Proto;
            Assert.Contains("const STAT_PFX = 'ui.stat.';", html);
            Assert.Contains("Amount: '투사체', Magnet: '흡수 반경', Growth: '경험치', Area: '범위'", html);
            Assert.Contains("const statLabel = k => STAT_EASY[k] ? `${statName(k)} · ${STAT_EASY[k]}` : statName(k);", html);
        }

        [Fact] public void 멈춤_화면에_무공별_피해와_초당_피해()
        {
            var html = Proto;
            Assert.Contains("dps = w.dealt / Math.max(1, G.t - (w.t0 || 0))", html);
            Assert.Contains("<details class=\"card wdmg\">", html);
        }

        [Fact] public void 설정은_멈춤에서도_깜빡임_끄기와_체력바_끄기()
        {
            var html = Proto;
            Assert.Contains("<div class=\"row setrow\">", html);
            Assert.Contains("const blink = settings.flash && (", html);
            Assert.Contains("showBoss = !!(G.boss && !G.boss.dead) && settings.bossbar;", html);
        }

        [Fact] public void 멈춤_화면_절기_조합표는_찾은_절기만_보이고_못_맞추는_조합은_흐리게()
        {
            var html = Proto;
            Assert.Contains("found = all.filter(e => seenAwaken.has(e.id))", html);
            Assert.Contains("(!hw && G.weapons.length >= G.weaponSlots) || (!hp && G.passives.length >= G.passiveSlots)", html);
            Assert.Contains("synSummaryHtml() + evoTableHtml()", html);
        }

        [Fact] public void 능력치_이름이_처음_나오면_한_번_풀이()
        {
            var html = Proto;
            Assert.Contains("id=\"luhelp\"", html);
            Assert.Contains("const statSeen = new Set(store.get('statSeen') || []);", html);
            Assert.Contains("const help = statFirstHelp(currentOffers);", html);
        }

        [Fact] public void 혈우_연출_세기와_방향_표시_크기를_설정에서()
        {
            var html = Proto;
            Assert.Contains("data-set=\"rain\"", html);
            Assert.Contains("data-set=\"aim\"", html);
            Assert.Contains("ctx.fillStyle = `rgba(110,22,20,${0.22 * settings.rain})`;", html);
            Assert.Contains("if (settings.aim > 0) {", html);
        }
    }
}
