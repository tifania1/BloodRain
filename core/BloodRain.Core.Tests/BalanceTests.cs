using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Combat;
using BloodRain.Core.Progression;
using BloodRain.Core.Spawning;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>기획서 '11. 밸런스 기본값'에 적힌 숫자가 실제 계산과 맞는지.</summary>
    public class BalanceTests
    {
        readonly Data.GameData _d = TestData.Load();

        [Theory]
        [InlineData(1, 5)]
        [InlineData(20, 195)]
        [InlineData(21, 208)]
        [InlineData(40, 455)]
        [InlineData(41, 471)]
        public void 레벨별_필요_진기(int level, int expected)
        {
            Assert.Equal(expected, new XpCurve(_d.Balance.XpCurve).Required(level));
        }

        [Fact] public void 레벨60까지_누적_진기는_약_2만300()
        {
            long total = new XpCurve(_d.Balance.XpCurve).CumulativeTo(60);
            Assert.Equal(20315, total);
        }

        [Fact] public void 누적_진기로_레벨을_되돌려_구한다()
        {
            var c = new XpCurve(_d.Balance.XpCurve);
            Assert.Equal(60, c.LevelFor(c.CumulativeTo(60)));
            Assert.Equal(59, c.LevelFor(c.CumulativeTo(60) - 1));
        }

        [Theory]
        [InlineData(9, "삼류")]
        [InlineData(10, "이류")]
        [InlineData(35, "절정")]
        [InlineData(60, "초절정")]
        [InlineData(95, "화경")]
        public void 일반_모드_경지(int level, string expected)
        {
            Assert.Equal(expected, new Realms(_d.Balance.Realms).For(level).NameKo);
        }

        [Fact] public void 현경은_종장과_혈월에서만_열린다()
        {
            var r = new Realms(_d.Balance.Realms);
            Assert.Equal("화경", r.For(95, "normal").NameKo);
            Assert.Equal("현경", r.For(95, "bloodmoon").NameKo);
        }

        [Fact] public void 레벨_1에서_60까지_돌파는_네_번()
        {
            var names = new Realms(_d.Balance.Realms).Breakthroughs(1, 60).Select(x => x.NameKo).ToArray();
            Assert.Equal(new[] { "이류", "일류", "절정", "초절정" }, names);
        }

        [Fact] public void 장1_20분_시강과_동갑시_기혈()
        {
            var s = new EnemyScaling(_d.Balance.EnemyScaling);
            Assert.Equal(20.8f, s.Health(_d.Enemy("sigang"), 20, "chapter1"), 2);
            Assert.Equal(234f, s.Health(_d.Enemy("donggap"), 20, "chapter1"), 2);
        }

        [Fact] public void 받는_피해는_최소_1()
        {
            Assert.Equal(1f, Damage.Taken(3, 5));
            Assert.Equal(7f, Damage.Taken(10, 3));
        }

        [Theory]
        [InlineData(0, 15)]
        [InlineData(10, 180)]
        [InlineData(18, 400)]
        [InlineData(20, 120)]
        [InlineData(4.5f, 74)]
        public void 화면_위_목표_적_수(float minute, float expected)
        {
            Assert.Equal(expected, new SpawnCurve(_d.Balance.SpawnTarget).TargetAt(minute), 1);
        }

        [Fact] public void 생성은_19분에_멈추고_20분_보스전에_다시_돈다()
        {
            var c = new SpawnCurve(_d.Balance.SpawnTarget);
            Assert.Equal(0, c.SpawnThisSecond(19.5f, 0));
            Assert.True(c.SpawnThisSecond(20f, 0) > 0);
            Assert.Equal(54, c.SpawnThisSecond(10f, 0)); // 180 × 0.3
        }

        [Fact] public void 무공_피해_보간()
        {
            var w = _d.Weapon("samjae_sword");
            Assert.Equal(12f, WeaponMath.DamageAt(w, 1));
            Assert.Equal(37f, WeaponMath.DamageAt(w, 8));
            Assert.Equal(12f + 25f * 3 / 7, WeaponMath.DamageAt(w, 4), 3);
        }
    }

    public class YinYangTests
    {
        readonly Data.GameData _d = TestData.Load();

        [Fact] public void 양_무공을_올리면_양으로_기울고_극성까지_간다()
        {
            var g = new YinYangGauge(_d.Balance.Yinyang);
            var yang = _d.Weapon("nahan_fist");
            for (int i = 0; i < 5; i++) g.OnWeaponLevelUp(yang);
            Assert.Equal(25, g.Value);
            Assert.Equal(YinYangBand.Harmony, g.Band);
            g.OnWeaponLevelUp(yang);
            Assert.Equal(YinYangBand.Bias, g.Band);
            for (int i = 0; i < 10; i++) g.OnWeaponLevelUp(yang);
            Assert.Equal(80, g.Value);
            Assert.Equal(YinYangBand.Extreme, g.Band);
            Assert.Equal(Data.YinYang.Yang, g.Leaning);
        }

        [Fact] public void 혈해심법은_레벨당_음으로_12()
        {
            var g = new YinYangGauge(_d.Balance.Yinyang);
            g.OnPassiveLevelUp(_d.Passive("hyeolhae"));
            Assert.Equal(-12, g.Value);
        }

        [Fact] public void 중_속성은_게이지를_움직이지_않는다()
        {
            var g = new YinYangGauge(_d.Balance.Yinyang, 10);
            g.OnWeaponLevelUp(_d.Weapon("samjae_sword"));
            g.OnPassiveLevelUp(_d.Passive("cheongmyeong"));
            Assert.Equal(10, g.Value);
        }

        [Fact] public void 게이지는_100에서_멈춘다()
        {
            var g = new YinYangGauge(_d.Balance.Yinyang, 95);
            g.OnPassiveLevelUp(_d.Passive("geumgang"));
            Assert.Equal(100, g.Value);
        }

        [Fact] public void 운화는_태극_구간이_40까지()
        {
            var unhwa = _d.Character("unhwa");
            var g = new YinYangGauge(_d.Balance.Yinyang, 38, unhwa.HarmonyRange);
            Assert.Equal(YinYangBand.Harmony, g.Band);
            var normal = new YinYangGauge(_d.Balance.Yinyang, 38);
            Assert.Equal(YinYangBand.Bias, normal.Band);
        }
    }

    public class LoadoutTests
    {
        readonly Data.GameData _d = TestData.Load();
        ISet<string> StartUnlocked => new HashSet<string>(_d.Weapons.Where(w => w.Unlock == "start").Select(w => w.Id));

        [Fact] public void 진소운은_삼재검법_Lv1로_시작한다()
        {
            var l = new Loadout(_d, _d.Character("jin_sowoon"));
            Assert.Equal(1, l.Weapons["samjae_sword"]);
            Assert.Single(l.Weapons);
        }

        [Fact] public void 레벨업_선택지는_기본_3개_기연이_높으면_4개()
        {
            var l = new Loadout(_d, _d.Character("jin_sowoon"));
            Assert.Equal(3, l.RollLevelUp(new Random(1), 0, StartUnlocked).Count);
            Assert.Equal(4, l.RollLevelUp(new Random(1), 50, StartUnlocked).Count);
        }

        [Fact] public void 잠긴_무공은_선택지에_나오지_않는다()
        {
            var l = new Loadout(_d, _d.Character("jin_sowoon"));
            var ids = l.Candidates(StartUnlocked).Select(o => o.Id).ToHashSet();
            Assert.DoesNotContain("taiji_sword", ids);
            Assert.Contains("nahan_fist", ids);
        }

        [Fact] public void 무공_칸이_차면_새_무공은_나오지_않는다()
        {
            var l = new Loadout(_d, _d.Character("jin_sowoon"));
            foreach (var id in new[] { "nahan_fist", "flying_dagger", "changcheon_qi", "palm_wind", "flash_finger" })
                l.Apply(new Offer(OfferKind.NewWeapon, id));
            Assert.Equal(6, l.Weapons.Count);
            Assert.DoesNotContain(l.Candidates(StartUnlocked), o => o.Kind == OfferKind.NewWeapon);
            l.AddExtraSlot(weapon: true);
            Assert.Contains(l.Candidates(StartUnlocked), o => o.Kind == OfferKind.NewWeapon);
        }

        [Fact] public void 절기는_Lv8_짝심법_레벨20이_모두_맞아야_열린다()
        {
            var l = new Loadout(_d, _d.Character("jin_sowoon"));
            for (int i = 0; i < 7; i++) l.Apply(new Offer(OfferKind.UpgradeWeapon, "samjae_sword"));
            Assert.Equal(8, l.Weapons["samjae_sword"]);
            Assert.Null(l.EvolvableOnChest(25));          // 짝 심법 없음
            l.Apply(new Offer(OfferKind.NewPassive, "cheongmyeong"));
            Assert.Null(l.EvolvableOnChest(19));          // 레벨 부족
            var evo = l.EvolvableOnChest(20);
            Assert.NotNull(evo);
            Assert.Equal("천지인 삼검", evo!.NameKo);
            l.Evolve(evo);
            Assert.Null(l.EvolvableOnChest(30));          // 이미 진화
        }

        [Fact] public void 모든_것이_최고_단계면_은자나_음식이_나온다()
        {
            var l = new Loadout(_d, _d.Character("jin_sowoon"));
            for (int i = 0; i < 7; i++) l.Apply(new Offer(OfferKind.UpgradeWeapon, "samjae_sword"));
            var none = new HashSet<string>();
            foreach (var p in _d.Passives.Take(6))
            {
                l.Apply(new Offer(OfferKind.NewPassive, p.Id));
                for (int i = 0; i < 4; i++) l.Apply(new Offer(OfferKind.UpgradePassive, p.Id));
            }
            // 무공 칸은 비어 있지만 해금된 새 무공이 하나도 없는 상황
            var offers = l.RollLevelUp(new Random(3), 0, none);
            Assert.Single(offers);
            Assert.Equal(OfferKind.Fallback, offers[0].Kind);
        }
    }
}
