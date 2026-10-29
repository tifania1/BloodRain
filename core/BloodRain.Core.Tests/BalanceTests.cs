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
        [InlineData(89, "화경")]
        [InlineData(90, "현경")]
        [InlineData(95, "현경")]
        public void 레벨별_경지(int level, string expected)
        {
            Assert.Equal(expected, new Realms(_d.Balance.Realms).For(level).NameKo);
        }

        [Fact] public void 현경은_모든_생에서_열린다()
        {
            // 천하 구조(기획서 3장 경지표, 7장 경지와 축척): 어느 생이든 레벨 90이면 현경. 모드 인자는 결과를 바꾸지 않는다.
            Assert.All(_d.Balance.Realms, r => Assert.Null(r.OnlyIn));
            var realms = new Realms(_d.Balance.Realms);
            foreach (var mode in new[] { "normal", "final", "bloodmoon" })
            {
                Assert.Equal("현경", realms.For(95, mode).NameKo);
                Assert.Equal("현경", realms.Breakthroughs(89, 90, mode).Single().NameKo);
            }
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

        [Theory]
        [InlineData(34, "normal", 0)]
        [InlineData(35, "normal", 1)]   // 절정
        [InlineData(50, "normal", 2)]   // 초절정
        [InlineData(70, "normal", 3)]   // 화경
        [InlineData(89, "normal", 3)]
        [InlineData(90, "normal", 4)]   // 현경: 천하에선 모든 생에서 열린다
        [InlineData(95, "bloodmoon", 4)]
        public void 절기_그릇은_절정1_초절정2_화경3_현경4(int level, string mode, int expected)
        {
            var l = new Loadout(_d, _d.Character("jin_sowoon"));
            Assert.Equal(expected, l.Vessels(level, mode));
        }

        [Fact] public void 절기는_문턱과_그릇이_모두_맞아야_깨어난다()
        {
            var l = new Loadout(_d, _d.Character("jin_sowoon"));
            for (int i = 0; i < 7; i++) l.Apply(new Offer(OfferKind.UpgradeWeapon, "samjae_sword"));
            Assert.Equal(8, l.Weapons["samjae_sword"]);
            Assert.Null(l.AwakenOnChest(40));                       // 짝 심법 없음
            l.Apply(new Offer(OfferKind.NewPassive, "cheongmyeong"));
            l.Apply(new Offer(OfferKind.UpgradePassive, "cheongmyeong"));
            Assert.Empty(l.AtThreshold());                          // 짝 심법 Lv2: 아직 문턱 아님
            l.Apply(new Offer(OfferKind.UpgradePassive, "cheongmyeong"));
            Assert.Single(l.AtThreshold());                         // Lv3: 문턱
            Assert.Null(l.AwakenOnChest(34));                       // 일류까지는 그릇이 없다
            var evo = l.AwakenOnChest(35);
            Assert.NotNull(evo);
            Assert.Equal("천지인 삼검", evo!.NameKo);
            l.Evolve(evo);
            Assert.Null(l.AwakenOnChest(40));                       // 이미 절기
        }

        [Fact] public void 그릇이_차면_둘째_절기는_초절정까지_기다린다()
        {
            var l = new Loadout(_d, _d.Character("jin_sowoon"));
            for (int i = 0; i < 7; i++) l.Apply(new Offer(OfferKind.UpgradeWeapon, "samjae_sword"));
            l.Apply(new Offer(OfferKind.NewWeapon, "nahan_fist"));
            for (int i = 0; i < 7; i++) l.Apply(new Offer(OfferKind.UpgradeWeapon, "nahan_fist"));
            foreach (var pid in new[] { "cheongmyeong", "geumgang" })
            {
                l.Apply(new Offer(OfferKind.NewPassive, pid));
                l.Apply(new Offer(OfferKind.UpgradePassive, pid));
                l.Apply(new Offer(OfferKind.UpgradePassive, pid));
            }
            Assert.Equal(2, l.AtThreshold().Count);

            // 절정 돌파 순간: 그릇이 하나 생기고 문턱 둘 중 하나를 고른다
            var pick = l.AwakenOnBreakthrough(34, 35);
            Assert.Equal(2, pick.Count);
            l.Evolve(pick[1]);
            Assert.Equal("nahan_fist", pick[1].Weapon);

            Assert.Null(l.AwakenOnChest(49));                       // 그릇 하나를 다 썼다
            Assert.Empty(l.AwakenOnBreakthrough(40, 45));           // 그릇이 안 느는 구간
            var second = l.AwakenOnBreakthrough(49, 50);            // 초절정
            Assert.Single(second);
            Assert.Equal("samjae_sword", second[0].Weapon);
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
