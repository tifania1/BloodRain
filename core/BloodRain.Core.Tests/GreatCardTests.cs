using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using Xunit;

namespace BloodRain.Core.Tests
{
    // 기획서 8장 '레벨업 규칙'의 대오(大悟) 카드(GU-02): 가진 것 강화 카드가 낮은 확률로 금빛, 집으면 +2레벨.
    public class GreatCardTests
    {
        readonly GameData _d = TestData.Load();
        Loadout Jin() => new Loadout(_d, _d.Character("jin_sowoon"));   // 삼재검법 Lv1
        HashSet<string> All() => new HashSet<string>(_d.Weapons.Select(w => w.Id));

        [Fact]
        public void 확률은_기획서_추천값_3퍼센트_더하기_기연마다_0_2퍼센트포인트()
        {
            var rule = _d.Balance.LevelUp;
            Assert.Equal(0.03f, rule.GreatChance, 4);
            Assert.Equal(0.002f, rule.GreatChancePerLuck, 5);
            Assert.Equal(2, rule.GreatLevels);
            var l = Jin();
            Assert.Equal(0.03f, l.GreatChance(0), 4);
            Assert.Equal(0.13f, l.GreatChance(50), 4);
            Assert.Equal(1f, l.GreatChance(100000), 4);
            Assert.Equal(0.03f, l.GreatChance(-10), 4);
            var gdd = TestData.ReadRepoFile("docs", "design", "gdd.md");
            Assert.Contains("**대오(大悟) 카드**", gdd);
            Assert.Contains("3% + 기연 × 0.2%p", gdd);
        }

        [Fact]
        public void 대오_카드는_두_레벨_오르고_음양도_두_번_움직인다()
        {
            var a = Jin(); var b = Jin();
            a.Apply(new Offer(OfferKind.UpgradeWeapon, "samjae_sword", 2));
            b.Apply(new Offer(OfferKind.UpgradeWeapon, "samjae_sword"));
            b.Apply(new Offer(OfferKind.UpgradeWeapon, "samjae_sword"));
            Assert.Equal(3, a.Weapons["samjae_sword"]);
            Assert.Equal(b.Gauge.Value, a.Gauge.Value);
        }

        [Fact]
        public void 최고_단계를_넘지_않는다()
        {
            var l = Jin();
            int max = _d.Balance.Levels.WeaponMax;
            for (int i = 1; i < max - 1; i++) l.Apply(new Offer(OfferKind.UpgradeWeapon, "samjae_sword"));
            Assert.Equal(max - 1, l.Weapons["samjae_sword"]);
            // 한 단계 남은 카드는 대오가 될 수 없고, 억지로 넣어도 최고 단계에서 멈춘다
            Assert.False(l.CanBeGreat(new Offer(OfferKind.UpgradeWeapon, "samjae_sword")));
            l.Apply(new Offer(OfferKind.UpgradeWeapon, "samjae_sword", 2));
            Assert.Equal(max, l.Weapons["samjae_sword"]);
        }

        [Fact]
        public void 새_무공_새_심법_은자_음식은_대오가_되지_않는다()
        {
            var l = Jin();
            Assert.True(l.CanBeGreat(new Offer(OfferKind.UpgradeWeapon, "samjae_sword")));
            Assert.False(l.CanBeGreat(new Offer(OfferKind.NewWeapon, "nahan_fist")));
            Assert.False(l.CanBeGreat(new Offer(OfferKind.NewPassive, "cheongmyeong")));
            Assert.False(l.CanBeGreat(new Offer(OfferKind.Fallback, "silver_or_food")));
            // 여러 시드로 뽑아도 대오는 강화 카드에만
            for (int seed = 0; seed < 300; seed++)
            {
                var x = Jin();
                x.Apply(new Offer(OfferKind.NewPassive, "cheongmyeong"));
                foreach (var o in x.RollLevelUp(new Random(seed), 200f, All()))
                    if (o.Great) Assert.True(o.Kind == OfferKind.UpgradeWeapon || o.Kind == OfferKind.UpgradePassive, o.ToString());
            }
        }

        [Fact]
        public void 같은_시드는_같은_카드_뜨는_비율은_확률_가까이()
        {
            var a = Jin().RollLevelUp(new Random(7), 0f, All()).Select(o => o.ToString()).ToArray();
            var b = Jin().RollLevelUp(new Random(7), 0f, All()).Select(o => o.ToString()).ToArray();
            Assert.Equal(a, b);

            // 강화 카드가 나온 만큼만 많이 뽑아 센다(기연 0 → 3%, 기연 50 → 13%)
            double Rate(float luck)
            {
                int up = 0, great = 0;
                var rng = new Random(11);
                for (int i = 0; i < 12000; i++)
                {
                    var l = Jin();
                    foreach (var o in l.RollLevelUp(rng, luck, All()))
                    {
                        if (!l.CanBeGreat(new Offer(o.Kind, o.Id))) continue;
                        up++; if (o.Great) great++;
                    }
                }
                Assert.True(up > 1000, $"강화 카드 {up}장");
                return (double)great / up;
            }
            Assert.InRange(Rate(0f), 0.02, 0.04);
            Assert.InRange(Rate(50f), 0.11, 0.15);
        }

        [Fact]
        public void 카드_글은_두_단계를_보이고_대오_표시가_붙는다()
        {
            var l = Jin();
            var card = LevelUpView.Card(_d, l, new Offer(OfferKind.UpgradeWeapon, "samjae_sword", 2))!;
            Assert.True(card.Great);
            Assert.Equal(1, card.FromLevel);
            Assert.Equal(3, card.ToLevel);
            var plain = LevelUpView.Card(_d, l, new Offer(OfferKind.UpgradeWeapon, "samjae_sword"))!;
            Assert.False(plain.Great);
            Assert.Equal(2, plain.ToLevel);
            Assert.Equal("UpgradeWeapon:samjae_sword+2", new Offer(OfferKind.UpgradeWeapon, "samjae_sword", 2).ToString());
            foreach (var lang in new[] { "ko", "en", "zh-Hans", "zh-Hant" })
                Assert.Contains("\"ui.levelup.badge.great\"", TestData.ReadRepoFile("unity", "BloodRain", "Assets", "Resources", "Data", "strings", lang + ".json"));
        }

        [Fact]
        public void 시너지_미리보기도_두_레벨로_센다()
        {
            var l = Jin();
            var after = BuildLevels.Of(_d, l).After(new Offer(OfferKind.UpgradeWeapon, "samjae_sword", 2));
            Assert.Equal(3, after.Level("samjae_sword"));
            Assert.Equal(2, BuildLevels.Of(_d, l).After(new Offer(OfferKind.UpgradeWeapon, "samjae_sword")).Level("samjae_sword"));
        }
    }
}
