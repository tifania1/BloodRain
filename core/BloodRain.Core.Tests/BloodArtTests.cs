using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Progression;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>마교 몸의 혈공(T-38, 기획서 5장).</summary>
    public class BloodArtTests
    {
        readonly GameData _d = TestData.Load();

        (BloodArt art, YinYangGauge gauge) New()
        {
            var sect = _d.Cheonha.Sect("demonic");
            var gauge = new YinYangGauge(_d.Balance.Yinyang, sect.YinyangStart);
            return (new BloodArt(sect.BloodArt!, gauge, sect.YinyangStart), gauge);
        }

        [Fact] public void 마교만_혈공이_있다()
        {
            foreach (var s in _d.Cheonha.Sects)
                Assert.Equal(s.Id == "demonic", s.BloodArt != null);
            var b = _d.Cheonha.Sect("demonic").BloodArt!;
            Assert.Equal(300, b.ExtraXpPerStep);
            Assert.Equal(1, b.YinPerStep);
            Assert.Equal(_d.Cheonha.Sect("demonic").Ultimate, b.ResetUltimate);
        }

        [Fact] public void 진기는_농도배이고_더_받은_300마다_음으로_1()
        {
            var (art, gauge) = New();
            int start = gauge.Value;
            Assert.Equal(43, art.OrbXp(10, 4.3f), 3);
            Assert.Equal(start, gauge.Value);
            // 더 받은 진기 33씩: 10번째 구슬에서 330 → 한 번
            for (int i = 0; i < 9; i++) art.OrbXp(10, 4.3f);
            Assert.Equal(start - 1, gauge.Value);
            Assert.Equal(1, art.Steps);
        }

        [Fact] public void 옅은_땅에선_심마가_안_차고_진기도_줄어든다()
        {
            var (art, gauge) = New();
            int start = gauge.Value;
            Assert.Equal(8, art.OrbXp(10, 0.8f), 3);
            for (int i = 0; i < 100; i++) art.OrbXp(10, 1f);
            Assert.Equal(start, gauge.Value);
        }

        [Fact] public void 천마해체를_쓰면_시작값으로_돌아온다()
        {
            var (art, gauge) = New();
            for (int i = 0; i < 200; i++) art.OrbXp(10, 4.3f);
            Assert.True(gauge.Value < -15);
            Assert.False(art.OnUltimate("il_geom"));
            Assert.True(art.OnUltimate("cheonma_haeche"));
            Assert.Equal(-15, gauge.Value);
        }
    }
}
