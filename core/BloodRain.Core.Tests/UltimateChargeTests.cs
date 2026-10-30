using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using BloodRain.Core.Progression;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>오의 진기 게이지: 처치·피격·정예 충전, 태극 25%와 연무장 보너스 곱, 정해진 양은 배율 없이.</summary>
    public class UltimateChargeTests
    {
        readonly GameData _d = Load();
        UltimateChargeDef U => _d.Balance.UltimateCharge;
        float HarmonyBonus => _d.Balance.Yinyang.Harmony.UltimateChargeBonus;

        UltimateCharge Charge(float bonus = 0f, float start = 0f) =>
            new UltimateCharge(U, _d.Balance.Yinyang.Harmony, bonus, start);

        [Fact] public void 기획서_오의_상세_문단과_숫자가_같다()
        {
            var gdd = ReadRepoFile("docs", "design", "gdd.md");
            int start = gdd.IndexOf("### 오의 상세");
            Assert.True(start >= 0);
            var section = gdd.Substring(start, gdd.IndexOf("### ", start + 5) - start);
            Assert.Contains($"진기 게이지 {Num(U.Max)}을 다 쓰고", section);
            Assert.Contains($"처치 1마리당 {Num(U.PerKill)}, 피격 1회당 {Num(U.PerHit)}, 정예 처치 시 {Num(U.PerElite)}가 찹니다", section);
            Assert.Contains($"태극 구간이면 {Num(HarmonyBonus * 100)}% 더 빨리", section);
            Assert.Contains($"정진({Num(U.PerSkip)})", section);
        }

        [Fact] public void 편향과_극성은_배율_1_태극만_더_빠르다()
        {
            var c = Charge();
            Assert.Equal(1f, c.Rate(YinYangBand.Bias), 4);
            Assert.Equal(1f, c.Rate(YinYangBand.Extreme), 4);
            Assert.Equal(1f + HarmonyBonus, c.Rate(YinYangBand.Harmony), 4);
        }

        [Fact] public void 연무장_보너스와_태극은_더하지_않고_곱한다()
        {
            var c = Charge(bonus: 0.2f);
            Assert.Equal(1.2f * (1f + HarmonyBonus), c.Rate(YinYangBand.Harmony), 4);
            Assert.NotEqual(1f + 0.2f + HarmonyBonus, c.Rate(YinYangBand.Harmony), 4);
        }

        [Fact] public void 처치_피격_정예는_각자_값에_배율을_곱해_찬다()
        {
            var c = Charge(bonus: 0.2f);
            float rate = c.Rate(YinYangBand.Harmony);
            c.OnKill(YinYangBand.Harmony);
            Assert.Equal(U.PerKill * rate, c.Value, 4);
            c.OnHit(YinYangBand.Harmony);
            Assert.Equal((U.PerKill + U.PerHit) * rate, c.Value, 3);
            c.OnEliteKill(YinYangBand.Harmony);
            Assert.Equal((U.PerKill + U.PerHit + U.PerElite) * rate, c.Value, 3);
        }

        [Fact] public void 정진은_배율_없이_그대로_더한다()
        {
            var c = Charge(bonus: 1f);
            c.OnSkip();
            Assert.Equal(U.PerSkip, c.Value, 4);
        }

        [Fact] public void 처치만으로_채우면_편향은_250마리_태극은_200마리()
        {
            // 0.4 × 250 = 100, 태극이면 0.5 × 200 = 100. 처치 수 감각을 잡는 기준.
            int Needed(YinYangBand band)
            {
                var c = Charge();
                int n = 0;
                while (!c.IsFull && n < 10_000) { c.OnKill(band); n++; }
                return n;
            }
            Assert.InRange(Needed(YinYangBand.Bias), 250, 251);
            Assert.InRange(Needed(YinYangBand.Harmony), 200, 201);
        }

        [Fact] public void 가득_찬_뒤_더해도_최대에서_멈추고_쓰면_0이다()
        {
            var c = Charge(start: 95f);
            c.OnEliteKill(YinYangBand.Harmony);
            Assert.Equal(U.Max, c.Value);
            Assert.True(c.TryUse());
            Assert.Equal(0f, c.Value);
            Assert.False(c.TryUse());
        }
    }
}
