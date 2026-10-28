using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using BloodRain.Core.Build;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>능력치 합산(B-02): 심법 + 연무장 → 최종 능력치, 연무장 비용.</summary>
    public class StatsTests
    {
        readonly Data.GameData _d = TestData.Load();
        static readonly Dictionary<string, int> None = new Dictionary<string, int>();

        [Fact] public void 심법과_연무장의_stat_이름은_전부_StatKind에_있다()
        {
            foreach (var p in _d.Passives)
                foreach (var e in p.Effects) Stats.Parse(e.Stat);
            foreach (var t in _d.Balance.Training.Items) Stats.Parse(t.Stat);
        }

        [Fact] public void 연무장_항목은_20개이고_겹치지_않는다()
        {
            var items = _d.Balance.Training.Items;
            Assert.Equal(20, items.Count);
            Assert.Equal(items.Count, items.Select(i => i.Stat).Distinct().Count());
        }

        [Fact] public void 아무것도_없으면_기획서_기본값()
        {
            var s = Stats.Compute(_d, _d.Character("jin_sowoon"), None, None);
            Assert.Equal(100, s.MaxHealth);
            Assert.Equal(0, s.Recovery);
            Assert.Equal(0, s.Armor);
            Assert.Equal(1, s.MoveSpeed);
            Assert.Equal(1, s.Might);
            Assert.Equal(0, s.CooldownReduction);
            Assert.Equal(1, s.Area);
            Assert.Equal(0, s.Amount);
            Assert.Equal(1, s.Duration);
            Assert.Equal(1, s.ProjectileSpeed);
            Assert.Equal(1.5f, s.Magnet, 4);
            Assert.Equal(1, s.Growth);
            Assert.Equal(0, s.Luck);
            Assert.Equal(0, s.Revival);
        }

        [Fact] public void 캐릭터_기혈과_신법이_바탕이_된다()
        {
            var s = Stats.Compute(_d, _d.Character("hyegong"), new Dictionary<string, int> { ["hyeolhae"] = 5 }, None);
            Assert.Equal(120 * 1.5f, s.MaxHealth, 3);
            Assert.Equal(0.9f, s.MoveSpeed, 4);
            Assert.Equal(0.02f, s.Bonus[StatKind.Lifesteal], 4);
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(2, 1)]
        [InlineData(3, 2)]
        [InlineData(4, 2)]
        [InlineData(5, 3)]
        public void 천수공은_1_3_5레벨에서만_분광이_는다(int level, int amount)
        {
            var s = Stats.Compute(_d, _d.Character("jin_sowoon"), new Dictionary<string, int> { ["cheonsu"] = level }, None);
            Assert.Equal(amount, s.Amount);
        }

        [Fact] public void 심법과_연무장이_같은_능력치에_더해진다()
        {
            var s = Stats.Compute(_d, _d.Character("jin_sowoon"),
                new Dictionary<string, int> { ["noejeong"] = 5, ["suryong"] = 2 },
                new Dictionary<string, int> { ["Might"] = 5, ["Magnet"] = 2 });
            Assert.Equal(1 + 0.40f + 0.25f, s.Might, 4);
            Assert.Equal(1.5f * (1 + 0.20f + 0.50f), s.Magnet, 4);
            Assert.Equal(1.20f, s.ProjectileSpeed, 4);
        }

        [Fact] public void 쾌속은_60퍼센트에서_멈춘다()
        {
            var extra = new StatBonus();
            extra.Add(StatKind.CooldownReduction, 0.5f);
            var s = Stats.Compute(_d, _d.Character("jin_sowoon"),
                new Dictionary<string, int> { ["cheongmyeong"] = 5 },
                new Dictionary<string, int> { ["CooldownReduction"] = 2 }, extra);
            Assert.Equal(0.6f, s.CooldownReduction, 4);
            Assert.Equal(0.30f + 0.05f + 0.5f, s.Bonus[StatKind.CooldownReduction], 4);
        }

        [Fact] public void 연무장_레벨은_최대에서_잘린다()
        {
            var s = Stats.Compute(_d, _d.Character("jin_sowoon"), None, new Dictionary<string, int> { ["Revival"] = 9, ["Armor"] = 3 });
            Assert.Equal(1, s.Revival);
            Assert.Equal(3, s.Armor);
        }

        [Fact] public void 모르는_심법_능력치는_바로_알린다()
        {
            Assert.Throws<System.InvalidOperationException>(() => Stats.Parse("Strength"));
        }

        [Fact] public void 연무장_비용은_산_만큼_비싸진다()
        {
            var t = _d.Balance.Training;
            Assert.Equal(200, Stats.TrainingCost(t, "Might", None));
            // 공력 2레벨을 이미 샀으면: 200 x 3 x (1 + 0.1 x 2)
            Assert.Equal(720, Stats.TrainingCost(t, "Might", new Dictionary<string, int> { ["Might"] = 2 }));
            // 다른 항목을 산 것도 센다: 600 x 1 x (1 + 0.1 x 3)
            Assert.Equal(780, Stats.TrainingCost(t, "Armor", new Dictionary<string, int> { ["Might"] = 2, ["Luck"] = 1 }));
            Assert.Null(Stats.TrainingCost(t, "Revival", new Dictionary<string, int> { ["Revival"] = 1 }));
        }

        [Fact] public void 연무장_표가_기획서와_같다()
        {
            var gdd = File.ReadAllText(Path.Combine(TestData.DataDir, "..", "..", "..", "..", "..", "docs", "design", "gdd.md"));
            int start = gdd.IndexOf("### 연무장(영구 강화)");
            Assert.True(start >= 0);
            var section = gdd.Substring(start, gdd.IndexOf("### ", start + 5) - start);
            var rows = section.Split('\n').Where(l => l.StartsWith("| ") && !l.StartsWith("| 항목") && !l.StartsWith("| ---")).ToList();
            var items = _d.Balance.Training.Items;
            Assert.Equal(items.Count, rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                var cols = rows[i].Split('|').Select(c => c.Trim()).ToArray();
                Assert.Equal(items[i].MaxLevel, int.Parse(cols[3]));
                Assert.Equal(items[i].BaseCost, int.Parse(cols[4].Replace(",", "")));
                var num = float.Parse(Regex.Match(cols[2], @"[\d.]+").Value, System.Globalization.CultureInfo.InvariantCulture);
                if (cols[2].Contains("%")) num /= 100f;
                Assert.Equal(items[i].PerLevel, num, 4);
            }
        }
    }
}
