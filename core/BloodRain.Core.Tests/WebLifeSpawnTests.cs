using System.Linq;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>
    /// 웹 한 생 판이 19분에 적 생성을 멈추지 않는지 본다(2026-11-07 WP-70). 옛 1장 흐름(20분 원각)의 STOP_SPAWN_AT 19 가
    /// 한 생 판에도 걸려 19분부터 적이 안 나오고 가중치표 마지막 두 줄(19~22·22~30분)이 안 쓰였다.
    /// chapters.json life 는 openEnded 이고 Core LifeDirector 도 멈추지 않는다.
    /// </summary>
    public class WebLifeSpawnTests
    {
        static string Proto => ReadRepoFile("web", "prototype", "template.html");

        [Fact] public void 한_생_판은_생성을_멈추지_않는다()
        {
            var html = Proto;
            Assert.Contains("const spawnStopAt = () => isPro() ? PRO.stopSpawningAtMinute : G.body ? Infinity : STOP_SPAWN_AT;", html);
            Assert.Contains("const stopped = G.gm >= spawnStopAt() && !G.won;", html);
            Assert.DoesNotContain("G.gm >= (isPro() ? PRO.stopSpawningAtMinute : STOP_SPAWN_AT)", html);
        }

        [Fact] public void 한_생_판은_가중치표를_끝까지_쓴다()
        {
            var html = Proto;
            Assert.Contains("pickType(G.body ? G.gm : Math.min(G.gm, 19))", html);
            // 데이터 쪽: 한 생 표가 19분 뒤 줄을 갖고, 생 흐름은 끝이 열려 있고 생성 중지가 없다
            var life = Load().Chapters.Single(c => c.Id == "life");
            Assert.True(life.OpenEnded);
            Assert.Null(life.StopSpawningAtMinute);
            Assert.Contains(life.Bands, b => b.FromMinute >= 19);
        }

        [Fact] public void 한_생_판_위쪽_글에_보스가_온다가_안_뜬다()
        {
            Assert.Contains("G.gm >= 19 && !G.body ? '보스가 온다'", Proto);
        }
    }
}
