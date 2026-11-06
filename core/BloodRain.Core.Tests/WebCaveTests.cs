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
    
        [Fact] public void 의상은_태극_구간을_지킨_처치만_Core_토큰으로()
        {
            var html = Proto;
            var data = Load();
            Assert.NotEmpty(data.Cheonha.Costumes);
            // 싸우는 동안 한 번이라도 태극을 벗어나면 못 얻고(Core NoteFightHarmony), 쓰러뜨리는 순간도 본다
            Assert.Contains("if (G.lordF.fin == null && yyBand().band !== 'harmony') G.lordF.offHarmony = true;", html);
            Assert.Contains("if (!e.offHarmony && yyBand().band === 'harmony') for (const c of DATA.costumes || [])", html);
            // 혼 기록 토큰은 Core CostumeDef.Token 과 같은 머리
            Assert.Equal("costume:", BloodRain.Core.Data.UnlockToken.CostumePrefix);
            Assert.Contains("'costume:' + id", html);
            Assert.Contains("for (const t of st.newCostumes || []) if (!m.unlocks.includes(t)) m.unlocks.push(t);", html);
            Assert.Contains("tok('costume:' + c.id)", html);   // 수련동 줄
        }
}
}
