using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>WP-22: 웹 한 생 판 보부상이 balance.json merchant 값과 Core MerchantVisit 규칙을 따르는지 글로 본다.</summary>
    public class WebMerchantTests
    {
        static string Proto => ReadRepoFile("web", "prototype", "template.html");

        [Fact] public void 한_생_시간표의_보부상_사건에_들른다()
        {
            var html = Proto;
            Assert.Contains("e.kind === 'merchant' ? { at: e.at, kind: 'merchant' }", html);
            Assert.Contains("else if (ev.kind === 'merchant') openMerchant();", html);
            Assert.Contains("const MER = B.merchant", html);
        }

        [Fact] public void 품목은_데이터_값으로_산다()
        {
            var html = Proto;
            Assert.Contains("(V[id] || 0) >= MER.maxPerVisit || G.silver < it.price", html);   // 품목마다 maxPerVisit, 은자 모자라면 못 삼
            Assert.Contains("p.maxHp * it.healFraction * healMul()", html);   // 회복약(마교 회복 배율만, Core PotionHeal)
            Assert.Contains("G.rerolls += it.rerolls || 1", html);   // 다시 고르기 권
            Assert.Contains("else if (id === 'chwigi_talisman') for (const o of G.orbs) o.pull = true;", html);   // 취기부
        }
    }
}
