using System.Text.RegularExpressions;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>기혈 위험 표시(기획서 8장 '피격과 사망': 기혈 30% 이하면 가장자리 먹물·심장 소리).</summary>
    public class LowHealthTests
    {
        [Fact] public void 문턱은_기획서와_같다()
        {
            var m = Regex.Match(TestData.GddSection("피격과 사망"), @"기혈 (\d+)% 이하에선 화면 가장자리에 먹물");
            Assert.True(m.Success, "8장 '피격과 사망'에 30% 줄이 없다");
            Assert.Equal(float.Parse(m.Groups[1].Value) / 100f, TestData.Load().Balance.Contact.LowHealthRatio, 4);
        }

        [Fact] public void 이하에서_켜지고_0이면_끈다()
        {
            var c = TestData.Load().Balance.Contact;
            Assert.True(c.IsLowHealth(c.LowHealthRatio));
            Assert.True(c.IsLowHealth(0.01f));
            Assert.False(c.IsLowHealth(c.LowHealthRatio + 0.01f));
            Assert.False(c.IsLowHealth(0f)); // 쓰러진 뒤엔 정산·재기 화면이 맡는다
        }

        [Fact] public void 웹도_데이터를_읽는다()
        {
            var html = TestData.ReadRepoFile("web", "prototype", "template.html");
            Assert.Contains("B.contact.lowHealthRatio", html);
            Assert.DoesNotContain("p.hp / p.maxHp < 0.3", html);
        }
    }
}
