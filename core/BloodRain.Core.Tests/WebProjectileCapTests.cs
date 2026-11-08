using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>웹 동시 투사체 상한(11장 '성능 상한' 1,500, QA-16): Unity 처럼 내 탄과 적 탄을 따로 센다.
    /// 그동안 적 패턴 탄이 G.projs 에 바로 들어가 상한을 안 거치면서 내 탄 상한은 적 탄까지 셌다.</summary>
    public class WebProjectileCapTests
    {
        static string Proto => ReadRepoFile("web", "prototype", "template.html");

        [Fact] public void 적_탄은_addFoeProj_로만_들어간다()
        {
            var html = Proto;
            Assert.DoesNotContain("G.projs.push({ enemy: true,", html);
            Assert.Contains("function addFoeProj(o) { if (G.foeProjN >= LIM.projectiles) return; G.foeProjN++; G.projs.push(o); }", html);
            Assert.True(html.Split("addFoeProj({ enemy: true,").Length - 1 >= 5);
        }

        [Fact] public void 내_탄_상한은_내_것만_센다()
        {
            var html = Proto;
            Assert.Contains("function addProj(o) { if (G.myProjN >= LIM.projectiles) return; G.myProjN++;", html);
            Assert.Contains("G.myProjN = 0; G.foeProjN = 0; for (const pr of G.projs) { if (pr.enemy) G.foeProjN++; else G.myProjN++; }", html);
            Assert.Equal(1500, Load().Balance.Limits.Projectiles);
        }
    }
}
