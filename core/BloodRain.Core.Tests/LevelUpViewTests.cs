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
        public void 심법_효과_값_글은_웹_STAT_TEXT_와_같은_꼴()
        {
            string V(string stat, float v) => LevelUpView.EffectValue(new PassiveEffect { Stat = stat, PerLevel = v });
            Assert.Equal("+8%", V("Might", 0.08f));
            Assert.Equal("-6%", V("CooldownReduction", 0.06f));
            Assert.Equal("-1", V("Armor", 1f));
            Assert.Equal("+1", V("Amount", 0f));
            Assert.Equal("+0.5%", V("Lifesteal", 0.005f));
            Assert.Equal("+0.2", V("Recovery", 0.2f));
            // 데이터의 심법 효과는 모두 글이 나온다(빈 글 없음)
            foreach (var p in _d.Passives) foreach (var e in p.Effects) Assert.False(string.IsNullOrEmpty(LevelUpView.EffectValue(e)));
            Assert.Contains("const STAT_TEXT = {", TestData.ReadRepoFile("web", "prototype", "template.html"));
        }

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

        [Fact]
        public void 카드_글_무공은_피해_a에서_b와_이정표와_절기_짝()
        {
            var l = Jin();
            var up = LevelUpView.Card(_d, l, new Offer(OfferKind.UpgradeWeapon, "samjae_sword"))!;
            Assert.True(up.Weapon); Assert.False(up.IsNew);
            Assert.Equal((1, 2), (up.FromLevel, up.ToLevel));
            Assert.Equal(12f, up.DamageFrom);
            Assert.Equal(12f + 25f / 7f, up.DamageTo, 3);
            Assert.Null(up.Milestone);
            Assert.Equal("cheongmyeong", up.Pair!.Passive);
            Assert.Null(up.Threshold);
            var nw = LevelUpView.Card(_d, l, new Offer(OfferKind.NewWeapon, "nahan_fist"))!;
            Assert.True(nw.IsNew); Assert.Equal(0f, nw.DamageFrom); Assert.Equal(8f, nw.DamageTo);
            l.Apply(new Offer(OfferKind.UpgradeWeapon, "samjae_sword"));
            l.Apply(new Offer(OfferKind.UpgradeWeapon, "samjae_sword"));
            Assert.Equal("앞뒤 베기", LevelUpView.Card(_d, l, new Offer(OfferKind.UpgradeWeapon, "samjae_sword"))!.Milestone);
            Assert.Null(LevelUpView.Card(_d, l, new Offer(OfferKind.Fallback, "silver_or_food")));
        }

        [Fact]
        public void 카드_글_절기_문턱은_무공_최고_단계와_짝_심법_문턱이_만날_때()
        {
            int max = _d.Balance.Levels.WeaponMax, pair = _d.Balance.Evolution.PairPassiveMinLevel;
            var l = Jin();
            while (l.Weapons["samjae_sword"] < max - 1) l.Apply(new Offer(OfferKind.UpgradeWeapon, "samjae_sword"));
            l.Apply(new Offer(OfferKind.NewPassive, "cheongmyeong"));
            while (l.Passives["cheongmyeong"] < pair - 1) l.Apply(new Offer(OfferKind.UpgradePassive, "cheongmyeong"));
            // 무공 마지막 단계: 심법이 아직 문턱 아래라 아님
            Assert.Null(LevelUpView.Card(_d, l, new Offer(OfferKind.UpgradeWeapon, "samjae_sword"))!.Threshold);
            // 심법이 문턱에 닿으면 — 무공이 최고 단계가 아니라 아님
            Assert.Null(LevelUpView.Card(_d, l, new Offer(OfferKind.UpgradePassive, "cheongmyeong"))!.Threshold);
            l.Apply(new Offer(OfferKind.UpgradeWeapon, "samjae_sword"));
            var p = LevelUpView.Card(_d, l, new Offer(OfferKind.UpgradePassive, "cheongmyeong"))!;
            Assert.Equal("samjae_sword", p.Threshold!.Weapon);
            Assert.Equal(pair - 1, p.FromLevel);
            Assert.NotEmpty(p.Effects);
            // 반대 차례: 심법이 먼저 문턱이면 무공 마지막 단계 카드가 문턱
            var l2 = Jin();
            l2.Apply(new Offer(OfferKind.NewPassive, "cheongmyeong"));
            while (l2.Passives["cheongmyeong"] < pair) l2.Apply(new Offer(OfferKind.UpgradePassive, "cheongmyeong"));
            while (l2.Weapons["samjae_sword"] < max - 1) l2.Apply(new Offer(OfferKind.UpgradeWeapon, "samjae_sword"));
            Assert.Equal("samjae_sword", LevelUpView.Card(_d, l2, new Offer(OfferKind.UpgradeWeapon, "samjae_sword"))!.Threshold!.Weapon);
        }

        [Fact]
        public void 카드_글_심법_짝은_풀에_있는_무공만()
        {
            var l = Jin();
            var o = new Offer(OfferKind.NewPassive, "cheongmyeong");
            Assert.Equal("samjae_sword", LevelUpView.Card(_d, l, o)!.Pair!.Weapon);
            Assert.Null(LevelUpView.Card(_d, l, o, new[] { "nahan_fist" })!.Pair);
        }

        [Fact]
        public void 카드_글은_웹_offerCard_와_같은_셈()
        {
            var html = System.IO.File.ReadAllText(System.IO.Path.Combine(TestData.RepoRoot, "web", "prototype", "template.html"));
            Assert.Contains("d1 = lerp(def.damageLv1, def.damageLv8, lv / 7)", html);
            Assert.Contains("const ms = def.milestones && def.milestones[String(lv + 1)];", html);
            Assert.Contains("if (o.kind === 'uw' && lv + 1 >= B.levels.weaponMax && evo && pairLv(cur) >= EV.pairPassiveMinLevel)", html);
            Assert.Contains("if (ww && !ww.evolved && ww.lv >= B.levels.weaponMax && lv + 1 === EV.pairPassiveMinLevel)", html);
        }
    }
}
