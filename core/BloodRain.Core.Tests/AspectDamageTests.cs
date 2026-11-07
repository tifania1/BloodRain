using System;
using System.Globalization;
using BloodRain.Core.Data;
using BloodRain.Core.Progression;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>음양 구간의 속성 무공 피해 배율(기획서 3장 '음양과 주화입마', YY-01). 웹 aspectMult 와 같은 셈.</summary>
    public class AspectDamageTests
    {
        readonly GameData _d = TestData.Load();
        YinYangGauge At(int v) => new YinYangGauge(_d.Balance.Yinyang, v);

        [Fact] public void 태극이면_모두_1()
        {
            var g = At(10);
            Assert.Equal(YinYangBand.Harmony, g.Band);
            Assert.Equal(1f, g.AspectDamageMultiplier(YinYang.Yang));
            Assert.Equal(1f, g.AspectDamageMultiplier(YinYang.Yin));
        }

        [Fact] public void 편향이면_기운_쪽만()
        {
            var g = At(40);
            Assert.Equal(YinYangBand.Bias, g.Band);
            Assert.Equal(1f + _d.Balance.Yinyang.Bias.AspectDamageBonus, g.AspectDamageMultiplier(YinYang.Yang), 4);
            Assert.Equal(1f, g.AspectDamageMultiplier(YinYang.Yin));
            Assert.Equal(1f, g.AspectDamageMultiplier(YinYang.Neutral));
        }

        [Fact] public void 극성_음이면_음_무공_40()
        {
            var g = At(-80);
            Assert.Equal(YinYangBand.Extreme, g.Band);
            Assert.Equal(1f + _d.Balance.Yinyang.Extreme.AspectDamageBonus, g.AspectDamageMultiplier(YinYang.Yin), 4);
            Assert.Equal(1f, g.AspectDamageMultiplier(YinYang.Yang));
        }

        [Fact] public void 무극이면_그_값을_그대로()
        {
            var g = At(0);
            Assert.Equal(1.3f, g.AspectDamageMultiplier(YinYang.Yang, 0.3f), 4);
            Assert.Equal(1f, g.AspectDamageMultiplier(YinYang.Neutral, 0f), 4);
        }

        [Fact] public void 기획서_표와_같다()
        {
            string Pct(float f) => ((int)Math.Round(f * 100)).ToString(CultureInfo.InvariantCulture) + "%";
            var y = _d.Balance.Yinyang;
            Assert.Contains($"| 편향 | ±{y.HarmonyRange + 1} \\~ ±{y.ExtremeFrom - 1} | 기운 쪽 속성 무공 피해 +{Pct(y.Bias.AspectDamageBonus)} |", TestData.Gdd);
            Assert.Contains($"기운 쪽 속성 무공 피해 +{Pct(y.Extreme.AspectDamageBonus)}. 대신", TestData.Gdd);
        }
    }
}
