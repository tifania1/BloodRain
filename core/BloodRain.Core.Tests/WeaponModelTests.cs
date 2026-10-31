using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using BloodRain.Core.Progression;
using BloodRain.Sim;
using Xunit;
using static BloodRain.Sim.Assumptions;

namespace BloodRain.Core.Tests
{
    /// <summary>
    /// 시뮬레이터의 무공 모형(core/BloodRain.Sim/WeaponModel.cs). level-curve·synergy 보고서가 모두 이 계산 위에 서 있는데
    /// 따로 보는 테스트가 없었다. 주석에 적힌 단계 규칙(Lv4 앞뒤, Lv7 3연, Lv5 두 바퀴, Lv6 중독 등)이 코드와 맞는지 본다.
    /// </summary>
    public class WeaponModelTests
    {
        static readonly Lazy<SimData> Data = new Lazy<SimData>(() => SimData.Load(TestData.DataDir));
        static GameData G => Data.Value.Game;
        static int Max => G.Balance.Levels.WeaponMax;

        static BuildStats Stats0(YinYangBand band = YinYangBand.Harmony, YinYang leaning = YinYang.Neutral, double crit = 0)
        {
            var sheet = Stats.Compute(G, G.Character("jin_sowoon"), new Dictionary<string, int>(), new Dictionary<string, int>());
            return new BuildStats
            {
                Sheet = sheet, Cdr = 0, Band = band, Leaning = leaning,
                BiasBonus = Data.Value.BiasAspectBonus, ExtremeBonus = Data.Value.ExtremeAspectBonus, Crit = crit,
            };
        }

        static WeaponExtras? Ex(string id) => Data.Value.Extras.TryGetValue(id, out var x) ? x : null;

        static List<Strike> S(string id, int lv, bool evolved = false, BuildStats? b = null) =>
            WeaponModel.Strikes(G.Weapon(id), Ex(id), lv, evolved, false, b ?? Stats0(), Max);

        [Fact]
        public void 한번_피해는_단계값에_치명_절기_빛남을_곱한다()
        {
            var def = G.Weapon("samjae_sword");
            var b = Stats0();
            Assert.Equal(def.DamageLv1, WeaponModel.HitDamage(def, 1, false, false, b, Max), 4);
            Assert.Equal(def.DamageLv8, WeaponModel.HitDamage(def, Max, false, false, b, Max), 4);
            Assert.Equal(WeaponMath.DamageAt(def, 4, Max), WeaponModel.HitDamage(def, 4, false, false, b, Max), 4);
            Assert.Equal(def.DamageLv1 * EvolvedDamage, WeaponModel.HitDamage(def, 1, true, false, b, Max), 4);
            Assert.Equal(def.DamageLv1 * GlowMultiplier, WeaponModel.HitDamage(def, 1, false, true, b, Max), 4);
            Assert.Equal(def.DamageLv1 * 1.25, WeaponModel.HitDamage(def, 1, false, false, Stats0(crit: 0.25), Max), 4);
        }

        [Fact]
        public void 음양_보너스는_기운_쪽_속성_무공에만()
        {
            var yang = G.Weapon("nahan_fist");     // 양
            var yin = G.Weapon("flying_dagger");   // 음
            var neutral = G.Weapon("samjae_sword");
            Assert.Equal(YinYang.Yang, yang.Aspect);
            Assert.Equal(YinYang.Yin, yin.Aspect);
            Assert.Equal(YinYang.Neutral, neutral.Aspect);

            var bias = Stats0(YinYangBand.Bias, YinYang.Yang);
            var extreme = Stats0(YinYangBand.Extreme, YinYang.Yang);
            Assert.Equal(1, WeaponModel.AspectMultiplier(yang, Stats0()));
            Assert.Equal(1 + Data.Value.BiasAspectBonus, WeaponModel.AspectMultiplier(yang, bias), 5);
            Assert.Equal(1 + Data.Value.ExtremeAspectBonus, WeaponModel.AspectMultiplier(yang, extreme), 5);
            Assert.Equal(1, WeaponModel.AspectMultiplier(yin, extreme));
            Assert.Equal(1, WeaponModel.AspectMultiplier(neutral, extreme));
            Assert.True(Data.Value.ExtremeAspectBonus > Data.Value.BiasAspectBonus);
        }

        [Fact]
        public void 발동_간격은_쾌속만큼_줄고_하한이_있다()
        {
            var sword = G.Weapon("samjae_sword");
            Assert.Equal(sword.Cooldown, WeaponModel.Interval(sword, null, 1, false, 0, Max), 4);
            Assert.Equal(sword.Cooldown * 0.8, WeaponModel.Interval(sword, null, 1, false, 0.2, Max), 4);
            Assert.Equal(0.05, WeaponModel.Interval(sword, null, 1, false, 0.999, Max), 6);

            // 독무는 쿨다운 없이 0.5초마다 닿는다.
            var mist = G.Weapon("poison_mist");
            Assert.Equal(0.5, WeaponModel.Interval(mist, Ex("poison_mist"), 1, false, 0, Max), 6);

            // 섬광지는 초당 발수(Lv1 4발 → Lv8 9발), 절기는 2.2배.
            var finger = G.Weapon("flash_finger");
            var fx = Ex("flash_finger");
            Assert.Equal(1 / 4.0, WeaponModel.Interval(finger, fx, 1, false, 0, Max), 6);
            Assert.Equal(1 / 9.0, WeaponModel.Interval(finger, fx, Max, false, 0, Max), 6);
            Assert.Equal(1 / 9.0 / 2.2, WeaponModel.Interval(finger, fx, Max, true, 0, Max), 6);
        }

        [Fact]
        public void 삼재검법_Lv4_앞뒤_Lv7_3연_절기_검기()
        {
            double a1 = S("samjae_sword", 1)[0].Area, a4 = S("samjae_sword", 4)[0].Area;
            Assert.Equal(2 * a1, a4, 6);                 // 반원 → 앞뒤
            Assert.Equal(1, S("samjae_sword", 6)[0].Hits);
            Assert.Equal(3, S("samjae_sword", 7)[0].Hits);
            Assert.Single(S("samjae_sword", Max));
            var ev = S("samjae_sword", Max, true);
            Assert.Equal(2, ev.Count);
            Assert.True(ev[1].Random);
            Assert.Equal(ev[0].Damage * 1.5, ev[1].Damage, 6);
        }

        [Fact]
        public void 나한권은_3에서_5연타()
        {
            Assert.Equal(3, S("nahan_fist", 1)[0].Hits);
            Assert.Equal(5, S("nahan_fist", Max)[0].Hits);
            Assert.Equal(S("nahan_fist", 1)[0].Hits, (int)S("nahan_fist", 1)[0].SingleHits);
        }

        [Fact]
        public void 비도술은_Lv6부터_중독이고_자루와_관통이_는다()
        {
            Assert.Equal(0, S("flying_dagger", 5)[0].Dot);
            Assert.True(S("flying_dagger", 6)[0].Dot > 0);
            // Lv1: 1자루 × (관통 1 + 1) = 2마리. Lv8: 4자루 × (3 + 1) = 16마리.
            Assert.Equal(2, S("flying_dagger", 1)[0].Targets);
            Assert.Equal(16, S("flying_dagger", Max)[0].Targets);
            // 절기: +2자루, 자루마다 5번 더 튕김 → 6 × 9.
            Assert.Equal(54, S("flying_dagger", Max, true)[0].Targets);
            Assert.Equal(6, S("flying_dagger", Max, true)[0].SingleHits);
        }

        [Fact]
        public void 창천검기는_Lv5부터_두_바퀴_절기는_반경_1점3배()
        {
            Assert.Equal(1, S("changcheon_qi", 4)[0].Hits);
            Assert.Equal(2, S("changcheon_qi", 5)[0].Hits);
            var def = G.Weapon("changcheon_qi");
            double r = def.Range * 1.3 + EnemyRadius;
            Assert.Equal(Math.PI * r * r, S("changcheon_qi", Max, true)[0].Area, 6);
        }

        [Fact]
        public void 벽력탄_절기는_폭탄마다_세_번_터지고_지속피해는_보스에게_한_번만()
        {
            var plain = S("thunder_bomb", Max);
            var ev = S("thunder_bomb", Max, true);
            Assert.Equal(3, plain.Count);                // Lv8 3개
            Assert.Equal(4 * 3, ev.Count);                // 절기 +1개, 3연쇄
            Assert.Equal(1, ev.Count(s => s.SingleDot > 0));
            Assert.True(ev[1].Area < ev[0].Area && ev[2].Area < ev[1].Area);
        }

        [Fact]
        public void 독무_절기만_즉사이고_반경은_두_배()
        {
            Assert.False(S("poison_mist", Max)[0].AuraExecute);
            Assert.True(S("poison_mist", Max, true)[0].AuraExecute);
            double r = 3.5;  // rangeLv8
            double plain = Math.PI * Math.Pow(r + EnemyRadius, 2), ev = Math.PI * Math.Pow(2 * r + EnemyRadius, 2);
            Assert.Equal(plain, S("poison_mist", Max)[0].Area, 6);
            Assert.Equal(ev, S("poison_mist", Max, true)[0].Area, 6);
        }

        [Fact]
        public void 무공_16종_모두_단계와_절기에서_획이_있고_피해가_양수()
        {
            Assert.Equal(16, G.Weapons.Count);
            foreach (var def in G.Weapons)
                foreach (int lv in new[] { 1, Max })
                    foreach (bool ev in new[] { false, true })
                    {
                        var st = S(def.Id, lv, ev);
                        Assert.NotEmpty(st);
                        Assert.All(st, s => Assert.True(s.Damage > 0, $"{def.Id} Lv{lv} 절기 {ev}"));
                        Assert.All(st, s => Assert.True(s.Area > 0 || s.Targets > 0, $"{def.Id} 맞는 적 0"));
                        double iv = WeaponModel.Interval(def, Ex(def.Id), lv, ev, 0, Max);
                        Assert.True(WeaponModel.SingleTargetDps(st, iv) > 0, def.Id);
                    }
        }

        [Fact]
        public void 단계가_오르면_보스_화력이_줄지_않는다()
        {
            foreach (var def in G.Weapons)
            {
                double prev = 0;
                for (int lv = 1; lv <= Max; lv++)
                {
                    double dps = WeaponModel.SingleTargetDps(S(def.Id, lv), WeaponModel.Interval(def, Ex(def.Id), lv, false, 0, Max));
                    Assert.True(dps >= prev - 1e-9, $"{def.Id} Lv{lv}: {dps} < {prev}");
                    prev = dps;
                }
            }
        }

        [Fact]
        public void 화력_합계식()
        {
            var st = new List<Strike>
            {
                new Strike(area: 2, hits: 3, damage: 10, dot: 4, singleHits: 3, singleDot: 4),
                new Strike(targets: 5, damage: 7, singleHits: 1),
            };
            // 보스: (10×3 + 4) + 7×1 = 41, 간격 2초 → 20.5
            Assert.Equal(20.5, WeaponModel.SingleTargetDps(st, 2), 6);
            // 무리 밀도 1.5, 무리 100: 넓이 획 3마리 × (3×10 + 4) = 102, 대상 획 5 × 7 = 35 → 137 / 2초
            Assert.Equal(68.5, WeaponModel.ExpectedCrowdDps(st, 2, 1.5, 100), 6);
            // 무리가 2마리뿐이면 두 획 모두 2마리에서 막힌다: 2×34 + 2×7 = 82 / 2
            Assert.Equal(41, WeaponModel.ExpectedCrowdDps(st, 2, 1.5, 2), 6);
        }
    }

    /// <summary>시뮬레이터의 카드 고르기 정책(core/BloodRain.Sim/Policies.cs).</summary>
    public class PolicyTests
    {
        static GameData G => TestData.Load();

        static PolicyContext Ctx(int seed, Func<Offer, double>? dps = null)
        {
            var l = new Loadout(G, G.Character("jin_sowoon"));
            return new PolicyContext { Data = G, Loadout = l, Rng = new Random(seed), CrowdDpsAfter = dps ?? (_ => 0) };
        }

        [Fact]
        public void 정책_이름으로_만들고_모르는_이름은_예외()
        {
            foreach (var id in Policies.All)
            {
                var p = Policies.Create(id);
                Assert.Equal(id, p.Id);
                Assert.False(string.IsNullOrEmpty(p.NameKo));
            }
            var ex = Assert.Throws<ArgumentException>(() => Policies.Create("lucky"));
            Assert.Contains("lucky", ex.Message);
        }

        [Fact]
        public void 평균_정책은_무공이_적으면_새_무공을_먼저()
        {
            var p = Policies.Create("average");
            for (int seed = 0; seed < 50; seed++)
            {
                var offers = new[] { new Offer(OfferKind.UpgradeWeapon, "samjae_sword"), new Offer(OfferKind.NewWeapon, "flying_dagger"),
                    new Offer(OfferKind.NewPassive, "cheongmyeong") };
                Assert.Equal(OfferKind.NewWeapon, p.Choose(offers, Ctx(seed)).Kind);
            }
        }

        [Fact]
        public void 평균_정책은_가진_무공의_짝_심법을_짝_없는_심법보다_먼저()
        {
            var p = Policies.Create("average");
            Assert.Contains(G.Evolutions, e => e.Weapon == "samjae_sword" && e.Passive == "cheongmyeong");
            for (int seed = 0; seed < 50; seed++)
            {
                var offers = new[] { new Offer(OfferKind.NewPassive, "geumgang"), new Offer(OfferKind.NewPassive, "noejeong"),
                    new Offer(OfferKind.NewPassive, "cheongmyeong") };
                Assert.Equal("cheongmyeong", p.Choose(offers, Ctx(seed)).Id);
            }
        }

        [Fact]
        public void 욕심_정책은_화력이_가장_많이_오르는_카드()
        {
            var p = Policies.Create("greedy");
            var offers = new[] { new Offer(OfferKind.NewPassive, "geumgang"), new Offer(OfferKind.NewWeapon, "thunder_bomb"),
                new Offer(OfferKind.UpgradeWeapon, "samjae_sword") };
            var dps = new Dictionary<string, double> { ["geumgang"] = 10, ["thunder_bomb"] = 30, ["samjae_sword"] = 20 };
            for (int seed = 0; seed < 20; seed++)
                Assert.Equal("thunder_bomb", p.Choose(offers, Ctx(seed, o => dps[o.Id])).Id);
        }

        [Fact]
        public void 아무거나_정책은_시드가_같으면_같은_카드이고_고루_고른다()
        {
            var p = Policies.Create("slow");
            var offers = new[] { new Offer(OfferKind.NewPassive, "geumgang"), new Offer(OfferKind.NewWeapon, "thunder_bomb"),
                new Offer(OfferKind.UpgradeWeapon, "samjae_sword") };
            Assert.Equal(p.Choose(offers, Ctx(7)).Id, p.Choose(offers, Ctx(7)).Id);
            var seen = new HashSet<string>();
            for (int seed = 0; seed < 60; seed++) seen.Add(p.Choose(offers, Ctx(seed)).Id);
            Assert.Equal(3, seen.Count);
        }

        [Fact]
        public void 절기_고르기와_7칸째()
        {
            var cands = G.Evolutions.Take(2).ToList();
            Assert.Same(cands[0], Policies.Create("average").PickAwaken(cands, Ctx(1)));
            Assert.Same(cands[0], Policies.Create("greedy").PickAwaken(cands, Ctx(1)));
            Assert.True(Policies.Create("average").ExtraSlotIsWeapon(Ctx(1)));
            Assert.True(Policies.Create("greedy").ExtraSlotIsWeapon(Ctx(1)));
            var slow = Policies.Create("slow");
            var picks = Enumerable.Range(0, 40).Select(s => slow.ExtraSlotIsWeapon(Ctx(s))).Distinct().Count();
            Assert.Equal(2, picks);
        }
    }
}
