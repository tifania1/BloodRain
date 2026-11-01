using System.Globalization;
using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>진전 단계의 능력치 덧셈(synergies.json stats, 기획서 20장 진전 표).</summary>
    public class SynergyStatsTests
    {
        readonly GameData _d = TestData.Load();

        [Fact] public void 능력치_이름은_모두_StatKind_이고_숫자가_설명에_있다()
        {
            int n = 0;
            foreach (var sc in _d.Synergies.Schools)
                foreach (var t in sc.Tiers)
                    foreach (var kv in t.Stats)
                    {
                        Stats.Parse(kv.Key);
                        bool ratio = kv.Key != nameof(StatKind.Armor);
                        string num = ratio ? $"+{(kv.Value * 100).ToString("0", CultureInfo.InvariantCulture)}%" : $"+{kv.Value.ToString("0", CultureInfo.InvariantCulture)}";
                        Assert.Contains(num, t.Desc);
                        n++;
                    }
            Assert.Equal(6, n);
        }

        [Fact] public void 소림_셋이면_호신_2와_기혈_15퍼센트를_다_더한다()
        {
            var b = TestData.Build("jin_sowoon", ("samjae_sword", 1), ("nahan_fist", 1), ("geumgang", 1), ("cheongsim", 1));
            var syn = Synergy.Compute(_d, b);
            Assert.Equal(3, syn.TierOf("shaolin"));
            var bonus = syn.StatBonus();
            Assert.Equal(2f, bonus[StatKind.Armor]);
            Assert.Equal(0.15f, bonus[StatKind.MaxHealth], 4);
        }

        [Fact] public void 단계가_없으면_아무것도_안_더한다()
        {
            var b = TestData.Build("jin_sowoon", ("samjae_sword", 1), ("nahan_fist", 1));
            var bonus = Synergy.Compute(_d, b).StatBonus();
            foreach (StatKind k in System.Enum.GetValues(typeof(StatKind))) Assert.Equal(0f, bonus[k]);
        }

        [Fact] public void 수로채_2단계_은자는_Greed_로_온다()
        {
            var b = TestData.Build("jin_sowoon", ("samjae_sword", 1), ("chain_sickle", 1), ("suryong", 1));
            var syn = Synergy.Compute(_d, b);
            Assert.True(syn.TierOf("waterfort") >= 2);
            Assert.Equal(0.3f, syn.StatBonus()[StatKind.Greed], 4);
        }
    }
}
