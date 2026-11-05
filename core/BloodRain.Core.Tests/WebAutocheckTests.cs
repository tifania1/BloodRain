using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>
    /// WP-03 웹 자동 점검(tools/web_autocheck.js)에서 찾은 것이 다시 깨지지 않게 글로 본다.
    /// 자동 판 자체는 브라우저가 있어야 해서 여기서 돌리지 않는다(README '웹 자동 점검').
    /// </summary>
    public class WebAutocheckTests
    {
        static string Cheonha => ReadRepoFile("web", "lookdev", "cheonha.html");
        static string Proto => ReadRepoFile("web", "prototype", "template.html");

        [Fact] public void 천하_시안_hitPlayer_가_맞은_뒤_무적을_준다()
        {
            // RM-10 때 무적 줄이 앞줄 주석 끝에 붙어 꺼졌다. 그 뒤로 붙은 적에게 매 프레임 맞아 삼류 초반에 쓰러지는 판이 늘었다.
            var html = Cheonha;
            int at = html.IndexOf("function hitPlayer(");
            Assert.True(at >= 0);
            var body = html.Substring(at, html.IndexOf("\n}\n", at) - at);
            var line = Regex.Match(body, @"^\s*p\.hp -= .*$", RegexOptions.Multiline).Value;
            Assert.DoesNotContain("p.iframe", line);   // 주석에 말려 들어가지 않았는지
            Assert.Matches(new Regex(@"^\s*p\.iframe = LD\.hitIframe;", RegexOptions.Multiline), body);
        }

        [Fact] public void 두_페이지_모두_프레임_시간이_음수가_되지_않는다()
        {
            // 화면을 닫을 때 시계를 performance.now() 로 맞추는데 다음 rAF 시각이 그보다 이를 수 있다. 음수면 효과 반경이 음수가 돼 콘솔 오류.
            Assert.Contains("clamp((ts - last) / 1000, 0, 0.05)", Cheonha);
            Assert.Contains("Math.min(0.05, Math.max(0, (ts - lastTs) / 1000))", Proto);
        }

        [Fact] public void 프로토타입_판이_끝나면_다른_화면이_안_열린다()
        {
            // 쓰러진 프레임 끝에 레벨업이 열려 결과 화면 뒤에 겹쳤다. endRun 이 overlay 를 잡아 둔다.
            var html = Proto;
            int at = html.IndexOf("function endRun(");
            var body = html.Substring(at, html.IndexOf("\n}\n", at) - at);
            Assert.Contains("overlay = 'result';", body);
        }

        [Fact] public void 천하_시안_속도_없는_줄은_달리지_않고_벤다()
        {
            // 검혼 일섬(screen_slash)은 shape line 인데 projectileSpeed 가 없다. 방패 돌진처럼 달리게 두면 보스 자리가 NaN.
            var d = Load();
            var slash = d.BossPatternSet("huashan_sword_soul").Patterns.Single(p => p.Id == "screen_slash");
            Assert.Equal("line", slash.Shape);
            Assert.True(slash.ProjectileSpeed <= 0);
            Assert.Contains("pt.shape === 'line' && !pt.projectileSpeed", Cheonha);
        }

        [Fact] public void 점검_스크립트가_있고_README_에_표가_있다()
        {
            var js = ReadRepoFile("tools", "web_autocheck.js");
            foreach (var s in new[] { "__hyeolu.sim", "__ch.step", "pageerror", "멈춤", "화면 겹침" }) Assert.Contains(s, js);
            Assert.Contains("## 웹 자동 점검", ReadRepoFile("web", "prototype", "README.md"));
        }

        [Fact] public void CI_가_묶은_뒤_자동_점검을_돌린다()
        {
            // WP-04: 올리기 전에 자동 점검. 한동안은 결과만 남긴다(continue-on-error).
            var yml = ReadRepoFile(".github", "workflows", "web-prototype.yml");
            int build = yml.IndexOf("python tools/build_web_prototype.py");
            int check = yml.IndexOf("node tools/web_autocheck.js --runs 2");
            int deploy = yml.IndexOf("name: 올리기");
            Assert.True(build >= 0 && check > build && deploy > check);
            Assert.Contains("npx playwright install --with-deps chromium", yml);
            Assert.Contains("- tools/web_autocheck.js", yml);   // 점검 스크립트만 고쳐도 돈다
            // ubuntu-latest(26.04)에선 Playwright 가 크로미움을 못 깔아 점검이 2초 만에 끝나고 continue-on-error 로 초록이 됐다(실행 150)
            Assert.DoesNotContain("runs-on: ubuntu-latest", yml);
            Assert.Contains("if: always()", yml);   // 준비가 실패해도 요약·경고는 남긴다
        }

        [Fact] public void 서장_점검은_장삼_뒤에_남은_화면을_닫고_컷을_본다()
        {
            // 장삼을 잡은 걸음에 열린 레벨업이 남거나 컷을 기다리는 사이 또 열리면 게임은 컷을 기다린다(WP-02).
            // 점검이 안 닫으면 판마다 '컷이 안 뜸'이 들쭉날쭉 났다(WP-04 를 CI 에 붙이며 찾음).
            var js = ReadRepoFile("tools", "web_autocheck.js");
            int at = js.IndexOf("async function prologueRuns(");
            var body = js.Substring(at, js.IndexOf("\n}\n", at) - at);
            int close = body.IndexOf("chooseOffer(0)"), live = body.IndexOf("liveCheck(");
            Assert.True(close >= 0 && close < live);
            Assert.Contains("!== 'procut'", body);   // 컷이 뜰 때까지 계속 닫는다
        }

        [Fact] public void 프로토타입_일시정지_키는_8장_조작표를_따른다()
        {
            // QA-11: 8장 '일시정지·무공 정보 | Esc / Tab'
            Assert.Contains("| 일시정지·무공 정보 | Esc / Tab |", Gdd);
            Assert.Contains("e.code === 'Escape' || e.code === 'Tab'", Proto);
        }
    }
}
