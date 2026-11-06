using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>웹 침방 수련동(10장 '객잔 시설'·'해금과 도전 과제')이 데이터에서 줄을 만들고 시설이 열렸을 때만 보이는지 글로 본다.</summary>
    public class WebCaveTests
    {
        static string Proto => ReadRepoFile("web", "prototype", "template.html");

        [Fact] public void 수련동은_열려야_보이고_줄은_데이터에서()
        {
            var html = Proto;
            Assert.Contains("const cv = $('inncave'), cf = INN_F.find(x => x.id === 'cave'), con = !!cf && !cf.underRepair && facOpen(m, cf);", html);
            // 땅의 주인 첫 처치 줄: 그 땅으로 여는 문파·대표 인물·무공
            Assert.Contains("(DATA.sects || []).filter(x => x.unlock === 'boss:' + r.id)", html);
            Assert.Contains("DATA.weapons.filter(w => w.unlock === 'boss:' + r.id)", html);
            // 금두꺼비 문턱은 balance.json training bonusMax 에서
            Assert.Contains("B.training.items.find(i => i.bonusMax)", html);
        }

        [Fact] public void 도전_과제_무공_토큰마다_말이_있다()
        {
            var html = Proto;
            foreach (var w in Load().Weapons)
            {
                if (w.Unlock == "start" || w.Unlock.StartsWith("boss:")) continue;
                Assert.Contains(w.Unlock + ":", html);   // CAVE_TOKEN_KO 에 그 토큰이 있다
            }
        }
    }
}
