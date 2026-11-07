using System.IO;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.World;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>DN-01: 피해 숫자 띄우기(12장 설정 '피해 숫자 표시', 11장 상한 80).</summary>
    public class DamageNumbersTests
    {
        readonly BalanceDef _b = Load().Balance;

        [Fact] public void 상한은_기획서_80()
        {
            Assert.Contains("| 동시 피해 숫자 표시 | 80 |", Gdd);
            Assert.Equal(80, _b.Limits.DamageNumbers);
        }

        [Fact] public void 치명타는_늘_나머지는_문턱과_확률()
        {
            var d = _b.DamageNumber;
            Assert.True(DamageNumbers.ShouldShow(d, 1, crit: true, eliteOrBoss: false, roll: 0.99));
            Assert.False(DamageNumbers.ShouldShow(d, d.MinDamage - 0.1f, false, true, 0));
            Assert.True(DamageNumbers.ShouldShow(d, d.MinDamage, false, false, d.Chance - 0.01));
            Assert.False(DamageNumbers.ShouldShow(d, d.MinDamage, false, false, d.Chance));
            Assert.True(DamageNumbers.ShouldShow(d, d.MinDamage, false, true, d.ChanceEliteBoss - 0.01));
            Assert.True(d.ChanceEliteBoss > d.Chance);
        }

        [Fact] public void 상한을_넘으면_오래된_것부터_버린다()
        {
            var n = new DamageNumbers(_b);
            for (int i = 0; i < 100; i++) n.Add(0, 0, 0.5f, i, false, 0.5);
            Assert.Equal(80, n.Items.Count);
            Assert.Equal(20, n.Items[0].Value);
            Assert.Equal(99, n.Items[^1].Value);
        }

        [Fact] public void 자리와_수명()
        {
            var d = _b.DamageNumber;
            var n = new DamageNumbers(_b);
            var a = n.Add(3, 4, 0.5f, 12.5f, true, 0);
            Assert.Equal(3 - d.JitterMeters, a.X, 4);
            Assert.Equal(4, a.Y);
            Assert.Equal(0.5f * d.HeadOffsetRadii, a.Lift, 4);
            Assert.Equal(13, a.Value);
            Assert.True(a.Crit);
            n.Tick(d.LifeSeconds / 2);
            Assert.Equal(0.5f, n.Alpha(n.Items[0]), 3);
            n.Tick(d.LifeSeconds / 2);
            Assert.Empty(n.Items);
        }

        [Fact] public void 웹도_같은_값을_읽는다()
        {
            var web = File.ReadAllText(Path.Combine(RepoRoot, "web/prototype/template.html"));
            Assert.Contains("B.damageNumber", web);
            Assert.DoesNotContain("e.r * 2.4", web);
            Assert.DoesNotContain("n.t < 0.7", web);
        }
    }
}
