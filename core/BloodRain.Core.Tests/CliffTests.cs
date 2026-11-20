using System;
using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using BloodRain.Core.Run;
using BloodRain.Core.Spawning;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>WP-32a(2026-11-20 사람이 추천대로 — decisions #37): 벼랑 기연을 천하 한 장에.</summary>
    public class CliffTests
    {
        readonly GameData _d = TestData.Load();

        [Fact] public void 벼랑은_3_14분에_10m_고리에_길이_4m_띠()
        {
            var e = _d.Synergies.Encounter(EncounterBoons.Cliff);
            Assert.NotNull(e.Spot);
            Assert.Equal(3f, e.Spot!.AppearFromMinute);
            Assert.Equal(14f, e.Spot.AppearToMinute);
            Assert.Equal(10f, e.Spot.RingRadius);
            Assert.Equal(4f, e.Param("bandLength"));
            Assert.Equal(2f, e.Param("levels"));
            Assert.True(EncounterSpots.OnGround(e));
            Assert.Equal(0.35, (double)e.Chance, 3);
        }

        Loadout NewLoadout(params (string id, int lv)[] weapons)
        {
            var c = _d.Cheonha;
            var body = BloodRain.Core.Meta.Rebirth.Body(c, new BloodRain.Core.Meta.RebirthCandidate(c.Sect("shaolin"), c.Region("songshan"), Array.Empty<BirthTraitDef>()));
            var l = body.NewLoadout(_d);
            foreach (var (id, lv) in weapons)
            {
                if (!l.Weapons.ContainsKey(id)) l.Apply(new Offer(OfferKind.NewWeapon, id));
                while (l.Weapons[id] < lv) l.Apply(new Offer(OfferKind.UpgradeWeapon, id));
            }
            return l;
        }

        [Fact] public void 가진_무공_둘에_2단계씩과_못_연_무공_하나()
        {
            var l = NewLoadout(("flying_dagger", 3), ("palm_wind", 7));
            var unlocked = _d.Weapons.Where(w => w.Unlock == "start").Select(w => w.Id).ToHashSet();
            for (int seed = 0; seed < 20; seed++)
            {
                var offers = EncounterBoons.CliffOffers(_d, l, unlocked, new Random(seed));
                Assert.Equal(3, offers.Count);
                var ups = offers.Where(o => o.Kind == OfferKind.UpgradeWeapon).ToList();
                Assert.Equal(2, ups.Count);
                Assert.Equal(ups.Count, ups.Select(o => o.Id).Distinct().Count());
                Assert.All(ups, o => Assert.True(l.Weapons.ContainsKey(o.Id)));
                Assert.All(ups, o => Assert.Equal(Math.Min(2, _d.Balance.Levels.WeaponMax - l.Weapons[o.Id]), o.Levels));
                var fresh = offers.Single(o => o.Kind == OfferKind.NewWeapon);
                Assert.False(l.Weapons.ContainsKey(fresh.Id));
                Assert.DoesNotContain(fresh.Id, unlocked);    // 못 연 무공 먼저
            }
        }

        [Fact] public void 무공_칸이_차면_못_연_무공_대신_가진_무공_한_장_더()
        {
            var ids = _d.Weapons.Select(w => w.Id).ToList();
            var c = _d.Cheonha;
            var body = BloodRain.Core.Meta.Rebirth.Body(c, new BloodRain.Core.Meta.RebirthCandidate(c.Sect("shaolin"), c.Region("songshan"), Array.Empty<BirthTraitDef>()));
            var l = body.NewLoadout(_d);
            foreach (var id in ids) { if (l.Weapons.Count >= l.WeaponSlots) break; if (!l.Weapons.ContainsKey(id)) l.Apply(new Offer(OfferKind.NewWeapon, id)); }
            Assert.Equal(l.WeaponSlots, l.Weapons.Count);
            var offers = EncounterBoons.CliffOffers(_d, l, ids.ToHashSet(), new Random(1));
            Assert.Equal(Math.Min(3, l.Weapons.Count), offers.Count);
            Assert.All(offers, o => Assert.Equal(OfferKind.UpgradeWeapon, o.Kind));
            foreach (var o in offers) l.Apply(o);   // 그대로 물려도 깨지지 않는다
        }
    }
}
