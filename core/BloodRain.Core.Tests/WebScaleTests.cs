using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>
    /// WP-12: 1장 프로토타입의 천하 축척 시야가 7장 식을 데이터(cheonha.json world)에서 읽는지 글로 본다.
    /// </summary>
    public class WebScaleTests
    {
        static string Proto => ReadRepoFile("web", "prototype", "template.html");

        [Fact] public void 빌드가_world_축척_값을_넣는다()
        {
            var build = ReadRepoFile("tools", "build_web_prototype.py");
            Assert.Contains("\"world\": {k: v for k, v in load(\"cheonha\")[\"world\"].items() if k in (\"view0Meters\", \"scalePerRealm\", \"viewInRealmShare\", \"boundaryViewMultiple\", \"noBoundaryFrom\")}", build);
        }

        [Fact] public void 시야_식을_데이터로_셈하고_서장은_그대로()
        {
            var html = Proto;
            Assert.Contains("WD.view0Meters * Math.pow(WD.scalePerRealm, realmProgress())", html);
            Assert.Contains("* WD.viewInRealmShare", html);
            Assert.Contains("if (!SCALE_MODE || isPro()) { PPM = PPM0;", html);
            // 숫자를 박아 두지 않는다
            Assert.DoesNotContain("Math.pow(2.25", html);
        }

        [Fact] public void 혈우_장막은_그_경지_첫_화면_대각선_배수이고_현경부터_없다()
        {
            var html = Proto;
            Assert.Contains("WD.view0Meters * Math.pow(WD.scalePerRealm, t) * WD.boundaryViewMultiple", html);
            Assert.Contains("REALMS.findIndex(r => r.id === WD.noBoundaryFrom)", html);
        }

        [Fact] public void 커진_시야에서_스프라이트와_바닥도_같이_줄어든다()
        {
            var html = Proto;
            Assert.Contains("s.size * scale * (PPM / PPM0)", html);
            Assert.Contains("groundTile.width / DPR * (PPM / PPM0)", html);
        }
    }
}
