using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>혈영 분신이 흉내 내는 무공(Core Mimicry). 잡몹은 첫 무공 하나, 혈영회주가 부른 분신은 가진 무공 모두.</summary>
    public class MimicryTests
    {
        readonly GameData _d = Load();
        CharacterDef Jin => _d.Character("jin_sowoon");
        EnemyDef Clone => _d.Enemy("blood_shadow_clone");
        BossPatternDef Mirror => _d.BossPatternSet("hyeokryeon_hwi").Pattern("mirror_clone");

        Loadout JinWith(params (string id, int level)[] extra)
        {
            var l = new Loadout(_d, Jin);
            foreach (var (id, level) in extra)
            {
                if (!l.Weapons.ContainsKey(id)) l.Apply(new Offer(OfferKind.NewWeapon, id));
                while (l.Weapons[id] < level) l.Apply(new Offer(OfferKind.UpgradeWeapon, id));
            }
            return l;
        }

        [Fact] public void 잡몹_분신은_시작_무공_하나만_배율대로_쓴다()
        {
            var l = JinWith(("flying_dagger", 3));
            var list = Mimicry.Weapons(_d, Jin, l, Clone.Mimic!);
            var m = Assert.Single(list);
            var w = _d.Weapon(Jin.StartWeapon);
            Assert.Equal(Jin.StartWeapon, m.WeaponId);
            Assert.Equal(1, m.Level);
            Assert.Equal(w.DamageLv1 * Clone.Mimic!.DamageScale, m.Damage, 4);
            Assert.Equal(w.Cooldown * Clone.Mimic.CooldownScale, m.Cooldown, 4);
        }

        [Fact] public void 레벨을_올리면_분신도_같은_레벨_피해를_쓴다()
        {
            var l = JinWith((Jin.StartWeapon, 8));
            var m = Mimicry.Weapons(_d, Jin, l, Clone.Mimic!).Single();
            Assert.Equal(8, m.Level);
            Assert.Equal(_d.Weapon(Jin.StartWeapon).DamageLv8 * Clone.Mimic!.DamageScale, m.Damage, 4);
        }

        [Fact] public void 보스_분신은_가진_무공_모두를_시작_무공부터_쓴다()
        {
            var l = JinWith(("thunder_bomb", 2), ("flying_dagger", 4));
            var mim = Mimicry.For(Clone, Mirror)!;
            Assert.True(mim.AllWeapons);
            var list = Mimicry.Weapons(_d, Jin, l, mim);
            Assert.Equal(new[] { Jin.StartWeapon, "flying_dagger", "thunder_bomb" }, list.Select(x => x.WeaponId));
            foreach (var m in list)
            {
                var w = _d.Weapon(m.WeaponId);
                Assert.Equal(l.Weapons[m.WeaponId], m.Level);
                Assert.Equal(WeaponMath.DamageAt(w, m.Level) * 0.5f, m.Damage, 4);
                Assert.Equal(WeaponGrowth.Cooldown(w, m.Level), m.Cooldown, 4);
            }
        }

        [Fact] public void 초당_발_수로_도는_무공도_쿨다운에_배율이_붙는다()
        {
            var nahan = _d.Characters.First(c => c.StartWeapon != "flash_finger");
            var l = new Loadout(_d, nahan);
            l.Apply(new Offer(OfferKind.NewWeapon, "flash_finger"));
            var m = Mimicry.Weapons(_d, nahan, l, new EnemyMimic { DamageScale = 1, CooldownScale = 2, AllWeapons = true })
                .Single(x => x.WeaponId == "flash_finger");
            Assert.Equal(2f / _d.Weapon("flash_finger").ShotsPerSecond![0], m.Cooldown, 4);
        }

        [Fact] public void 소환_패턴이_mimic_을_안_주면_적의_값을_쓴다()
        {
            Assert.Same(Clone.Mimic, Mimicry.For(Clone));
            Assert.Same(Clone.Mimic, Mimicry.For(Clone, new BossPatternDef()));
            Assert.Null(Mimicry.For(_d.Enemy("sigang")));
        }

        [Fact] public void 혈영회주_분신_값이_기획서와_같다()
        {
            Assert.Equal("blood_shadow_clone", Mirror.Summon);
            Assert.NotNull(Clone.Mimic);
            Assert.False(Clone.Mimic!.AllWeapons);
            var row = GddTable(GddSection("혈영회주 혁련휘")).Single(r => r[0] == "혈영 분신");
            Assert.Equal(0.5f, Mirror.Mimic!.DamageScale);
            Assert.Contains("피해 절반", row[4]);
            Assert.Equal(1f, Mirror.Mimic.CooldownScale);
            Assert.Contains("쿨다운 그대로", row[4]);
            Assert.Contains($"{Num(Mirror.LeaveSeconds)}초 뒤 사라진다", row[4]);
        }
    }
}
