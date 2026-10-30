using BloodRain.Core.Data;
using BloodRain.Core.Progression;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>태극 게이지 구간 경계와 옮기기·되돌리기. 기획서 8장 '음양': 태극 ±25 이내, 극성 ±70 이상.</summary>
    public class YinYangGaugeTests
    {
        readonly GameData _d = Load();
        YinYangDef Y => _d.Balance.Yinyang;

        YinYangGauge At(int v, int? harmony = null) => new YinYangGauge(Y, v, harmony);

        [Theory]
        [InlineData(0, YinYangBand.Harmony)]
        [InlineData(25, YinYangBand.Harmony)]
        [InlineData(-25, YinYangBand.Harmony)]
        [InlineData(26, YinYangBand.Bias)]
        [InlineData(-69, YinYangBand.Bias)]
        [InlineData(70, YinYangBand.Extreme)]
        [InlineData(-70, YinYangBand.Extreme)]
        [InlineData(100, YinYangBand.Extreme)]
        public void 구간_경계는_양끝을_포함한다(int value, YinYangBand band)
        {
            Assert.Equal(25, Y.HarmonyRange);
            Assert.Equal(70, Y.ExtremeFrom);
            Assert.Equal(band, At(value).Band);
        }

        [Fact] public void 태극_구간을_넓히면_경계도_따라간다()
        {
            Assert.Equal(YinYangBand.Harmony, At(40, harmony: 40).Band);
            Assert.Equal(YinYangBand.Bias, At(41, harmony: 40).Band);
            Assert.Equal(YinYangBand.Extreme, At(70, harmony: 40).Band);
        }

        [Fact] public void 기운_쪽은_태극이면_중이고_아니면_부호를_따른다()
        {
            Assert.Equal(YinYang.Neutral, At(20).Leaning);
            Assert.Equal(YinYang.Neutral, At(-20).Leaning);
            Assert.Equal(YinYang.Yang, At(30).Leaning);
            Assert.Equal(YinYang.Yin, At(-30).Leaning);
            Assert.Equal(YinYang.Yin, At(-90).Leaning);
        }

        [Fact] public void 시작값_옮기기_되돌리기는_모두_범위_안으로_자른다()
        {
            Assert.Equal(Y.Min, At(-500).Value);
            var g = At(90);
            g.Shift(50);
            Assert.Equal(Y.Max, g.Value);
            g.Shift(-300);
            Assert.Equal(Y.Min, g.Value);
            g.ResetTo(250);
            Assert.Equal(Y.Max, g.Value);
            g.Reset();
            Assert.Equal(0, g.Value);
        }

        [Fact] public void 혈해심법은_레벨당_12씩_음으로_기운다()
        {
            var g = At(0);
            var p = _d.Passive("hyeolhae");
            Assert.Equal(12, p.YinyangPerLevelOverride);
            for (int i = 0; i < 3; i++) g.OnPassiveLevelUp(p);
            Assert.Equal(-36, g.Value);
            Assert.Equal(YinYangBand.Bias, g.Band);
        }

        [Fact] public void 양_무공_한_가지만_최고_단계로_올리면_편향에_머문다()
        {
            // 무공 Lv1→8 은 일곱 번 올림(+35). 시작 무공 하나만으로는 극성에 닿지 않는다.
            var w = System.Linq.Enumerable.First(_d.Weapons, x => x.Aspect == YinYang.Yang);
            var g = At(0);
            g.OnWeaponLevelUp(w);                                   // 처음 얻을 때
            for (int i = 1; i < _d.Balance.Levels.WeaponMax; i++) g.OnWeaponLevelUp(w);
            Assert.Equal(Y.WeaponPerLevel * _d.Balance.Levels.WeaponMax, g.Value);
            Assert.Equal(YinYangBand.Bias, g.Band);
        }
    }
}
