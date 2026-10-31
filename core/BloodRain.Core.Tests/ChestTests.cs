using System;
using System.Linq;
using System.Text.RegularExpressions;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>보갑 열기(기획서 8장 보상 표·절기 그릇과 각성, balance.json chest).</summary>
    public class ChestTests
    {
        readonly GameData _d = TestData.Load();

        Loadout Jin() => new Loadout(_d, _d.Characters.First(c => c.Id == "jin_sowoon"));

        [Fact] public void 강화_개수_가중치는_기획서_보상_표와_같다()
        {
            var gdd = TestData.Gdd;
            var m = Regex.Match(gdd, @"\| 보갑 \| 무공·심법 강화 1\\~(\d)개\(1개 (\d+)%·2개 (\d+)%·3개 (\d+)%");
            Assert.True(m.Success, "기획서 8장 보상 표에 보갑 줄이 없다");
            var w = _d.Balance.Chest.UpgradeCountWeights;
            Assert.Equal(int.Parse(m.Groups[1].Value), w.Count);
            for (int i = 0; i < w.Count; i++) Assert.Equal(float.Parse(m.Groups[i + 2].Value), w[i]);
            Assert.Equal(100f, w.Sum());
        }

        [Fact] public void 뽑은_개수는_가중치_비율을_따른다()
        {
            var rng = new Random(7);
            var counts = new int[4];
            for (int i = 0; i < 20000; i++) counts[Chest.RollUpgradeCount(_d.Balance.Chest, rng)]++;
            Assert.Equal(0, counts[0]);
            Assert.InRange(counts[1] / 20000.0, 0.49, 0.53);
            Assert.InRange(counts[2] / 20000.0, 0.32, 0.36);
            Assert.InRange(counts[3] / 20000.0, 0.13, 0.17);
        }

        [Fact] public void 강화만_주고_새_무공은_주지_않는다()
        {
            var lo = Jin();
            int before = lo.Weapons["samjae_sword"];
            for (int s = 0; s < 3; s++)
            {
                var r = Chest.Open(_d, lo, 1, new Random(s));
                Assert.Null(r.Awakened);
                Assert.NotEmpty(r.Upgrades);
                Assert.All(r.Upgrades, o => Assert.True(o.Kind == OfferKind.UpgradeWeapon || o.Kind == OfferKind.Fallback));
            }
            Assert.Single(lo.Weapons);
            Assert.True(lo.Weapons["samjae_sword"] > before);
        }

        [Fact] public void 덧셈_몫만큼_더_나온다()
        {
            var a = Chest.Open(_d, Jin(), 1, new Random(3));
            var b = Chest.Open(_d, Jin(), 1, new Random(3), extraUpgrades: 1);
            Assert.Equal(a.Upgrades.Count + 1, b.Upgrades.Count);
        }

        [Fact] public void 강화할_것이_없으면_은자_음식()
        {
            var lo = Jin();
            while (lo.Weapons["samjae_sword"] < _d.Balance.Levels.WeaponMax) lo.Apply(new Offer(OfferKind.UpgradeWeapon, "samjae_sword"));
            var r = Chest.Open(_d, lo, 1, new Random(1));
            Assert.All(r.Upgrades, o => Assert.Equal(OfferKind.Fallback, o.Kind));
        }

        [Fact] public void 그릇이_남고_문턱이면_강화_대신_각성()
        {
            var evo = _d.Evolutions.First(e => e.Weapon == "samjae_sword");
            var lo = Jin();
            while (lo.Weapons["samjae_sword"] < _d.Balance.Levels.WeaponMax) lo.Apply(new Offer(OfferKind.UpgradeWeapon, "samjae_sword"));
            lo.Apply(new Offer(OfferKind.NewPassive, evo.Passive));
            while (lo.Passives[evo.Passive] < _d.Balance.Evolution.PairPassiveMinLevel) lo.Apply(new Offer(OfferKind.UpgradePassive, evo.Passive));

            int jeoljeong = _d.Balance.Realms.First(x => x.Id == "jeoljeong").Level;
            var under = Chest.Open(_d, lo, jeoljeong - 1, new Random(1));
            Assert.Null(under.Awakened); // 그릇이 아직 없다
            var r = Chest.Open(_d, lo, jeoljeong, new Random(1));
            Assert.Equal(evo.Weapon, r.Awakened!.Weapon);
            Assert.Empty(r.Upgrades);
        }

        [Fact] public void 음수_가중치와_음수_덧셈은_막는다()
        {
            Assert.Throws<InvalidOperationException>(() => Chest.RollUpgradeCount(new ChestDef { UpgradeCountWeights = { -1 } }, new Random(1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => Chest.Open(_d, Jin(), 1, new Random(1), -1));
        }

        [Fact] public void 장강수로채_4단계면_하나_더()
        {
            var three = TestData.Build("jin_sowoon", ("samjae_sword", 1), ("chain_sickle", 1), ("pear_spear", 1), ("suryong", 1));
            Assert.Equal(0, Chest.ExtraUpgrades(Synergy.Compute(_d, three)));
            var four = TestData.Build("jin_sowoon", ("samjae_sword", 1), ("chain_sickle", 1), ("pear_spear", 1), ("suryong", 1), ("cheollian", 1));
            var syn = Synergy.Compute(_d, four);
            Assert.Equal(4, syn.TierOf("waterfort"));
            Assert.Equal(1, Chest.ExtraUpgrades(syn));
        }

        [Fact] public void 보갑_덧셈을_주는_진전은_수로채_4단계_하나뿐이고_설명과_맞는다()
        {
            var tiers = _d.Synergies.Schools.SelectMany(s => s.Tiers.Select(t => (s.Id, t))).Where(x => x.t.ChestUpgrades > 0).ToList();
            Assert.Single(tiers);
            Assert.Equal("waterfort", tiers[0].Id);
            Assert.Equal(4, tiers[0].t.Count);
            Assert.Contains("보갑", tiers[0].t.Desc);
            Assert.Contains("정예 보갑에서 무공 강화가 하나 더", TestData.Gdd);
        }
    }
}
