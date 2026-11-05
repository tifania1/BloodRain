using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>
    /// WP-14c: 1장 프로토타입 한 생 판(?life=)의 정산이 10장 윤회의 기 식(Core RebirthQiLedger)을 데이터에서 읽고,
    /// 천하 시안과 같은 혼 기록(localStorage cheonha_meta2)에 새기는지 글로 본다.
    /// </summary>
    public class WebLifeSettleTests
    {
        static string Proto => ReadRepoFile("web", "prototype", "template.html");

        [Fact] public void 빌드가_윤회의_기_값과_지역_농도를_넣는다()
        {
            var build = ReadRepoFile("tools", "build_web_prototype.py");
            Assert.Contains("\"rebirthQi\": {k: v for k, v in drop_notes(load(\"cheonha\")[\"rebirthQi\"]).items() if k != \"targets\"}", build);
            Assert.Contains("\"density\": r[\"density\"]", build);
        }

        [Fact] public void 정산_식이_데이터_값을_쓰고_줄마다_반올림한다()
        {
            var html = Proto;
            Assert.Contains("Math.round(QI.perOrbValue * G.orbW)", html);
            Assert.Contains("newVisits.length * QI.firstVisitRegion", html);
            Assert.Contains("newRealms.length * QI.firstReachRealm", html);
            Assert.Contains("Math.round(QI.leftoverSilverRate * G.silver)", html);
            Assert.Contains("rows.reduce((a, r) => a + r[2], 0)", html);
            Assert.Contains("B.drops.eliteBloodCrystal", html);
            // 구슬 값은 심득 전 값에 그 자리 혈운 농도를 곱한다
            Assert.Contains("G.orbV += o.v; G.orbW += o.v * G.dens;", html);
            Assert.DoesNotContain("perOrbValue = 0.04", html);
        }

        [Fact] public void 혈운_농도는_시안과_같은_두_지역_섞기()
        {
            Assert.Contains("return (a.density * db + b.density * da) / (da + db);", Proto);
            Assert.Contains("return (a.density * db + b.density * da) / (da + db);", ReadRepoFile("web", "lookdev", "cheonha.html"));
        }

        [Fact] public void 혼_기록은_시안과_같은_열쇠()
        {
            Assert.Contains("const SOUL_KEY = 'cheonha_meta2';", Proto);
            Assert.Contains("localStorage.getItem('cheonha_' + k)", ReadRepoFile("web", "lookdev", "cheonha.html"));
            Assert.Contains("store.get('meta2')", ReadRepoFile("web", "lookdev", "cheonha.html"));
        }
    }
}
