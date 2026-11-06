using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Run;
using Xunit;

namespace BloodRain.Core.Tests
{
    // 기획서 12장 무공별 피해(일시정지)와 정산 비중이 같은 셈인지, 웹 pauselist 와 같은지.
    public class DamageReportTests
    {
        static readonly List<KeyValuePair<string, double>> Dealt = new List<KeyValuePair<string, double>>
        {
            new KeyValuePair<string, double>("samjae_sword", 300),
            new KeyValuePair<string, double>("palm_wind", 600),
            new KeyValuePair<string, double>("flying_dagger", 100),
        };

        [Fact]
        public void 비중은_합에_대한_몫이고_초당은_얻은_뒤_시간으로()
        {
            var at = new Dictionary<string, float> { ["palm_wind"] = 40f, ["flying_dagger"] = 59.5f };
            var l = DamageReport.Lines(Dealt, 60f, at);
            Assert.Equal(new[] { "samjae_sword", "palm_wind", "flying_dagger" }, l.Select(x => x.Id).ToArray());
            Assert.Equal(0.3f, l[0].Share, 4);
            Assert.Equal(0.6f, l[1].Share, 4);
            Assert.Equal(5.0, l[0].PerSecond, 4);    // 300 / 60
            Assert.Equal(30.0, l[1].PerSecond, 4);   // 600 / 20
            Assert.Equal(100.0, l[2].PerSecond, 4);  // 얻은 지 0.5초여도 1초로 나눔
        }

        [Fact]
        public void 정산_막대는_큰_것부터_자르고_합이_0이면_비중_0()
        {
            Assert.Equal(new[] { "palm_wind", "samjae_sword" }, DamageReport.ByDamage(Dealt, 2).Select(x => x.Id).ToArray());
            var zero = DamageReport.Lines(new[] { new KeyValuePair<string, double>("a", 0) }, 10f);
            Assert.Equal(0f, zero[0].Share);
        }

        [Fact]
        public void 웹_멈춤_화면과_같은_셈()
        {
            var html = TestData.ReadRepoFile("web", "prototype", "template.html");
            Assert.Contains("dps = w.dealt / Math.max(1, G.t - (w.t0 || 0))", html);
            Assert.Contains("Math.round(w.dealt / dsum * 100)", html);
        }
    }
}
