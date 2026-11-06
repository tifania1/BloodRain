using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>웹 화면 편의(12장 '화면 편의 규칙' 조준 WP-52·설정 음량 WP-53·레벨업 방향키 WP-54·인트로 WP-55·땅의 주인 컷 WP-56): 방향이 있는 무공이 한 곳(aimAngle)에서 가까운 적을 노리고, 설정으로 이동 방향으로 바꿀 수 있는지 글로 본다.</summary>
    public class WebAimTests
    {
        static string Proto => ReadRepoFile("web", "prototype", "template.html");

        [Fact] public void 방향_무공은_aimAngle_로_노린다()
        {
            var html = Proto;
            Assert.Contains("function aimAngle(reach) {", html);
            Assert.Contains("if (settings.aimMode !== 'move') { const near = nearestEnemies(p.x, p.y, 1, reach)[0];", html);
            // 장풍·혈조·삼재검법·이화창·낙화검우가 바라보는 쪽을 바로 쓰지 않고 aimAngle 을 거친다
            Assert.Contains("const a = aimAngle(sp * sh.lifeSeconds * S.duration);", html);
            Assert.Contains("base = aimAngle(R * 1.25);   // 12장 조준", html);
            Assert.DoesNotContain("vx: p.fx * sp, vy: p.fy * sp", html);
            Assert.DoesNotContain("base = near ? Math.atan2(near.y - p.y, near.x - p.x) : Math.atan2(fy, fx)", html);
        }

        [Fact] public void 조준_방식은_제목과_멈춤_화면_두_곳에()
        {
            var html = Proto;
            Assert.Equal(2, html.Split("data-set=\"aimMode\"").Length - 1);
            Assert.Contains("aimMode: store.get('aimMode') || 'near',", html);
        }

        [Fact] public void 음량은_한_마디를_거쳐_두_곳에서()
        {
            // 12장 설정 '음량': 모든 소리가 audio.master 하나를 거쳐 나가고, 단추는 제목·멈춤 화면 두 곳
            var html = Proto;
            Assert.DoesNotContain("connect(c.destination)", html);
            Assert.Contains("audio.master.connect(audio.ctx.destination)", html);
            Assert.Equal(2, html.Split("data-set=\"vol\"").Length - 1);
        }

        [Fact] public void 레벨업_화면은_방향키로_포커스를_옮긴다()
        {
            // 12장 '레벨업 입력': 방향키로 모든 카드와 단추에 포커스(깨달음·절기 고르기도 같이)
            var html = Proto;
            Assert.Contains("function arrowFocus(e, root) {", html);
            Assert.Contains("if (arrowFocus(e, $('levelup'))) return;", html);
            Assert.Contains("if (arrowFocus(e, $('brk'))) return;", html);
        }

        [Fact] public void 인트로는_첫_실행에만_데이터_컷대로()
        {
            // 12장 타이틀 '첫 실행에만 짧은 인트로 컷신', 19장 A(dialogue.json intro 12컷). 자동 점검(webdriver)·주소 뒤 ? 판에선 저절로 안 뜬다
            var html = Proto;
            Assert.Contains("if (!store.get('introSeen') && !location.search && !location.hash && !navigator.webdriver) openIntro();", html);
            Assert.Contains("introTimer = setTimeout(nextIntro, (cut.seconds || 4) * 1000);", html);
            var build = ReadRepoFile("tools", "build_web_prototype.py");
            Assert.Contains("\"intro\": drop_notes(load(\"dialogue\")[\"intro\"]),", build);
        }

        [Fact] public void 땅의_주인_컷은_만남은_회차_처음_처치는_계정_처음()
        {
            // docs/story/boss_cuts.md '고르는 코드': 만남 컷은 회차마다 처음 만날 때(혈영 아님), 처치 컷은 계정 첫 처치에만
            var html = Proto;
            Assert.Contains("if (!shadow && !met.includes(metKey) && lordCuts(def.id, 'meet'))", html);
            Assert.Contains("const acctFirst = first && !((G.soul0.bossKills || {})[def.id] > 0)", html);
            Assert.Contains("if (!cuts.length || G.auto) return false;", html);
        }
    }
}
