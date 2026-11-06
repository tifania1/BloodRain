using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>웹 조준(12장 '화면 편의 규칙' 조준, WP-52): 방향이 있는 무공이 한 곳(aimAngle)에서 가까운 적을 노리고, 설정으로 이동 방향으로 바꿀 수 있는지 글로 본다.</summary>
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
    }
}
