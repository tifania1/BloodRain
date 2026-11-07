using System.Linq;
using BloodRain.Core.Build;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>전투 HUD 왼쪽 위 칸(기획서 12장 '전투 HUD 배치' — 문턱 금빛, 그릇이 없으면 흐림, 그릇 ◆◇).</summary>
    public class HudSlotsTests
    {
        readonly Data.GameData _d = TestData.Load();

        Loadout Jin() => new Loadout(_d, _d.Characters.First(c => c.Id == "jin_sowoon"));

        Loadout AtThreshold()
        {
            var evo = _d.Evolutions.First(e => e.Weapon == "samjae_sword");
            var lo = Jin();
            while (lo.Weapons["samjae_sword"] < _d.Balance.Levels.WeaponMax) lo.Apply(new Offer(OfferKind.UpgradeWeapon, "samjae_sword"));
            lo.Apply(new Offer(OfferKind.NewPassive, evo.Passive));
            while (lo.Passives[evo.Passive] < _d.Balance.Evolution.PairPassiveMinLevel) lo.Apply(new Offer(OfferKind.UpgradePassive, evo.Passive));
            return lo;
        }

        int Level(string realm) => _d.Balance.Realms.First(r => r.Id == realm).Level;

        [Fact] public void 칸_수만큼_채우고_빈칸은_null()
        {
            var v = HudSlots.Build(Jin(), 1);
            Assert.Equal(_d.Balance.Slots.Weapons, v.Weapons.Count);
            Assert.Equal(_d.Balance.Slots.Passives, v.Passives.Count);
            Assert.Equal("samjae_sword", v.Weapons[0].Id);
            Assert.All(v.Weapons.Skip(1), s => Assert.Null(s.Id));
            Assert.Equal(0, v.VesselsTotal); // 절정 전엔 그릇이 없다
        }

        [Fact] public void 문턱은_그릇이_없으면_흐리고_있으면_뛴다()
        {
            var lo = AtThreshold();
            Assert.Equal(SlotGlow.Waiting, HudSlots.Build(lo, Level("jeoljeong") - 1).Weapons[0].Glow);
            var v = HudSlots.Build(lo, Level("jeoljeong"));
            Assert.Equal(SlotGlow.Ready, v.Weapons[0].Glow);
            Assert.Equal((0, 1), (v.VesselsUsed, v.VesselsTotal));
        }

        [Fact] public void 깨운_뒤엔_절기_칸과_채운_그릇()
        {
            var lo = AtThreshold();
            lo.Evolve(_d.Evolutions.First(e => e.Weapon == "samjae_sword"));
            var v = HudSlots.Build(lo, Level("chojeoljeong"));
            Assert.Equal(SlotGlow.Evolved, v.Weapons[0].Glow);
            Assert.Equal((1, 2), (v.VesselsUsed, v.VesselsTotal));
        }

        [Fact] public void 기획서_HUD_표에_그릇_줄이_있다()
        {
            var sec = TestData.GddSection("전투 HUD 배치");
            Assert.Contains("절기 문턱에 선 무공은 테두리가 금빛으로 뛰고(그릇이 없으면 흐림)", sec);
            Assert.Contains("절기 그릇 ◆◇", sec);
        }
    }
}
