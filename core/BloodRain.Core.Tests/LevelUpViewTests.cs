using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using Xunit;

namespace BloodRain.Core.Tests
{
    // 기획서 12장 '화면 편의 규칙' 레벨업 옆 칸·절기 조합표. 웹 cardLights·evoTableHtml 과 같은 셈인지.
    public class LevelUpViewTests
    {
        readonly GameData _d = TestData.Load();
        Loadout Jin() => new Loadout(_d, _d.Character("jin_sowoon"));   // 삼재검법 Lv1
        static string[] S(System.Collections.Generic.IEnumerable<SlotLight> l) => l.Select(x => x.ToString()).ToArray();

        [Fact]
        public void 카드는_오를_칸_새_칸_가진_절기_짝_칸을_켠다()
        {
            var l = Jin();
            Assert.Equal(new[] { "w:samjae_sword" }, S(LevelUpView.CardLights(_d, l, new Offer(OfferKind.UpgradeWeapon, "samjae_sword"))));
            Assert.Equal(new[] { "p:+", "w:samjae_sword" }, S(LevelUpView.CardLights(_d, l, new Offer(OfferKind.NewPassive, "cheongmyeong"))));
            Assert.Equal(new[] { "w:+" }, S(LevelUpView.CardLights(_d, l, new Offer(OfferKind.NewWeapon, "nahan_fist"))));
            l.Apply(new Offer(OfferKind.NewPassive, "cheongmyeong"));
            Assert.Equal(new[] { "w:samjae_sword", "p:cheongmyeong" }, S(LevelUpView.CardLights(_d, l, new Offer(OfferKind.UpgradeWeapon, "samjae_sword"))));
            Assert.Empty(LevelUpView.CardLights(_d, l, new Offer(OfferKind.Fallback, "silver_or_food")));
        }

        [Fact]
        public void 조합표는_찾은_절기만_보이고_못_맞추는_것은_흐리게()
        {
            var l = Jin();
            var seen = new[] { "cheonjiin", "baekbo_fist" };
            var (rows, total) = LevelUpView.EvolutionTable(_d, l, seen);
            Assert.Equal(_d.Evolutions.Count, total);
            Assert.Equal(seen, rows.Select(r => r.Evolution.Id).ToArray());
            Assert.Equal(1, rows[0].WeaponLevel);
            Assert.False(rows[0].Dead);
            Assert.False(rows[1].HasAny);
            Assert.False(rows[1].Dead);

            // 무공 칸이 다 차면 나한권이 없는 백보신권은 흐리게
            foreach (var w in new[] { "flying_dagger", "changcheon_qi", "palm_wind", "flash_finger", "thunder_bomb" })
                l.Apply(new Offer(OfferKind.NewWeapon, w));
            Assert.True(LevelUpView.EvolutionTable(_d, l, seen).rows[1].Dead);

            // 이미 깨어난 절기도 흐리게(깨어남 표시)
            l.Evolve(_d.Evolutions.First(e => e.Id == "cheonjiin"));
            var r0 = LevelUpView.EvolutionTable(_d, l, seen).rows[0];
            Assert.True(r0.Evolved && r0.Dead);
        }

        [Fact]
        public void 웹_프로토타입과_같은_규칙을_쓴다()
        {
            var html = TestData.ReadRepoFile("web", "prototype", "template.html");
            Assert.Contains("function cardLights(o)", html);
            Assert.Contains("const dead = (!hw && G.weapons.length >= G.weaponSlots) || (!hp && G.passives.length >= G.passiveSlots) || (hw && hw.evolved);", html);
        }
    }
}
