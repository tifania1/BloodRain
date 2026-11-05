using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>
    /// WP-14d: 1장 프로토타입 침방·연무장이 천하 시안과 같은 셈(후보 수·환혼 강화 값·연무장 비용 식·타고난 성질)을 데이터에서 읽는지 글로 본다.
    /// </summary>
    public class WebInnTests
    {
        static string Proto => ReadRepoFile("web", "prototype", "template.html");
        static string Sian => ReadRepoFile("web", "lookdev", "cheonha.html");

        [Fact] public void 빌드가_환혼과_타고난_성질을_넣는다()
        {
            var build = ReadRepoFile("tools", "build_web_prototype.py");
            Assert.Contains("\"rebirth\": {k: v for k, v in drop_notes(load(\"cheonha\")[\"rebirth\"]).items() if k in (\"candidates\", \"firstRebirth\", \"firstRebirthTraits\", \"upgrades\", \"femaleRatio\", \"maleOnlySects\")}", build);
            Assert.Contains("\"birthTraits\": drop_notes(load(\"cheonha\")[\"birthTraits\"])", build);
        }

        [Fact] public void 연무장_비용_식이_시안과_같다()
        {
            Assert.Contains("Math.round(it.baseCost * (lv + 1) * (1 + B.training.costGrowthPerBought * bought))", Proto);
            Assert.Contains("Math.round(it.baseCost * (lv + 1) * (1 + BAL.training.costGrowthPerBought * bought))", Sian);
        }

        [Fact] public void 환혼_강화_값과_후보_수가_시안과_같다()
        {
            Assert.Contains("lv >= u.maxLevel ? null : u.costPerLevel ? u.baseCost * (lv + 1) : u.baseCost", Proto);
            Assert.Contains("lv >= u.maxLevel ? null : u.costPerLevel ? u.baseCost * (lv + 1) : u.baseCost", Sian);
            Assert.Contains("RB.candidates + upgEffect(m, 'candidates')", Proto);
            Assert.Contains("RB.firstRebirth.map(id => ({ sect: id, traits: RB.firstRebirthTraits ? rollTraits(rnd) : [] }))", Proto);
        }

        [Fact] public void 타고난_성질은_겹치지_않게_굴리고_윤회의_기_보너스를_정산에()
        {
            Assert.Contains("BT.excludes.some(", Proto);
            Assert.Contains("qiBonus += t.rebirthQi || 0;", Proto);
            Assert.Contains("Math.round(rows.reduce((a, r) => a + r[2], 0) * G.body.qiBonus)", Proto);
        }

        [Fact] public void 몸_성별과_카드_글과_목_노인_줄을_데이터에서()
        {
            var build = ReadRepoFile("tools", "build_web_prototype.py");
            Assert.Contains("\"femaleRatio\", \"maleOnlySects\"", build);
            Assert.Contains("\"bedroom\": drop_notes(load(\"dialogue\")[\"bedroom\"][\"lines\"])", build);
            Assert.Contains("\"cards\": drop_notes(load(\"dialogue\")[\"cards\"][\"list\"])", build);
            var html = Proto;
            Assert.Contains("!(RB.maleOnlySects || []).includes(sect) && rnd() < (RB.femaleRatio || 0)", html);
            Assert.Contains("c.female && (card.femaleBodiesKo || []).length > idx", html);
            // 첫 환혼 줄은 늘, 나머지는 한 침방에 한 마디(Core BedroomTalk)
            Assert.Contains("if (!l || talk.key || talk.extra) return false;", html);
            Assert.Contains("bedLine('first:open')", html);
            Assert.Contains("bedLine('pick:female')", html);
        }

        [Fact] public void 서장_끝은_이_판의_침방으로_잇고_서장_정산은_시안과_같은_셈()
        {
            var html = Proto;
            Assert.Contains("if (SCALE_MODE) { $('profirst').hidden = true; overlay = null; recordPrologue(G.orbRaw, G.realm.id); openInn(); return; }", html);
            Assert.Contains("Math.round(QI.perOrbValue * Math.max(0, orbRaw || 0) * home.density + visits.length * QI.firstVisitRegion + realms.length * QI.firstReachRealm)", html);
            Assert.Contains("Math.round(orbQi + visits.length * QI.firstVisitRegion + realms.length * QI.firstReachRealm)", Sian);
            Assert.Contains("__hyeolu.prologueEnd(900, 'ilryu')", ReadRepoFile("tools", "web_autocheck.js"));
        }
    }
}
