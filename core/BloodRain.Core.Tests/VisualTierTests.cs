using System.Collections.Generic;
using System.IO;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>docs/design/vfx.md '단계 규칙': 이펙트 2단·3단 문턱 레벨(WeaponGrowth.VisualTier).</summary>
    public class VisualTierTests
    {
        readonly GameData _d = TestData.Load();

        static WeaponDef With(params string[] levels)
        {
            var w = new WeaponDef { Id = "t", Milestones = new Dictionary<string, string>() };
            foreach (var l in levels) w.Milestones[l] = "m" + l;
            return w;
        }

        [Fact]
        public void 삼재검법은_마일스톤_Lv4_Lv7_그대로()
        {
            var w = _d.Weapon("samjae_sword");
            Assert.Equal((4, 7), WeaponGrowth.VisualTierLevels(w));
            Assert.Equal(1, WeaponGrowth.VisualTier(w, 3));
            Assert.Equal(2, WeaponGrowth.VisualTier(w, 4));
            Assert.Equal(2, WeaponGrowth.VisualTier(w, 6));
            Assert.Equal(3, WeaponGrowth.VisualTier(w, 7));
            Assert.Equal(3, WeaponGrowth.VisualTier(w, 8));
        }

        [Theory]
        [InlineData(new string[0], 4, 7)]          // 없으면 Lv4·Lv7
        [InlineData(new[] { "5" }, 5, 7)]          // 하나(Lv7 밑) → 2단 문턱
        [InlineData(new[] { "7" }, 4, 7)]          // 하나(Lv7) → 3단 문턱
        [InlineData(new[] { "8" }, 4, 8)]          // 하나(Lv8) → 3단 문턱
        [InlineData(new[] { "6", "3", "8" }, 3, 6)] // 셋 이상이면 낮은 둘, 적힌 순서와 무관
        public void 마일스톤_수에_따른_문턱(string[] ms, int second, int third)
            => Assert.Equal((second, third), WeaponGrowth.VisualTierLevels(With(ms)));

        [Fact]
        public void 마일스톤이_null_이어도_기본값()
            => Assert.Equal((4, 7), WeaponGrowth.VisualTierLevels(new WeaponDef { Id = "t" }));

        [Fact]
        public void 모든_무공_단계는_레벨에_따라_줄지_않고_Lv8에_3단()
        {
            foreach (var w in _d.Weapons)
            {
                var (second, third) = WeaponGrowth.VisualTierLevels(w);
                Assert.True(1 < second && second < third && third <= WeaponGrowth.MaxLevel, $"{w.Id}: {second},{third}");
                int prev = 1;
                for (int lv = 1; lv <= WeaponGrowth.MaxLevel; lv++)
                {
                    int t = WeaponGrowth.VisualTier(w, lv);
                    Assert.True(t >= prev, w.Id);
                    prev = t;
                }
                Assert.Equal(1, WeaponGrowth.VisualTier(w, 1));
                Assert.Equal(3, prev);
            }
        }

        [Fact]
        public void vfx_md_가_규칙을_적고_있다()
        {
            var md = File.ReadAllText(Path.Combine(TestData.RepoRoot, "docs", "design", "vfx.md"));
            Assert.Contains("마일스톤이 없는 무공은 Lv4·Lv7로 본다", md);
            Assert.Contains("WeaponGrowth.VisualTier", md);
        }
    }
}
