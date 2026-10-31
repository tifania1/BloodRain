using System;
using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Run;
using BloodRain.Core.Progression;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>고른 깨달음이 몸에 붙이는 값(InsightEffects)과 역근의 기울기 절반(YinYangGauge.TiltMultiplier).</summary>
    public class InsightEffectsTests
    {
        readonly GameData _d = TestData.Load();

        InsightDef Common(string id) => _d.Insight(id);
        InsightDef Demonic(string id) => _d.DemonicInsights.First(i => i.Id == id);
        YinYangGauge Gauge(int start = 0) => new YinYangGauge(_d.Balance.Yinyang, start);

        InsightEffects With(params InsightDef[] picks)
        {
            var fx = new InsightEffects("samjae_sword");
            var g = Gauge();
            foreach (var p in picks) fx.Take(p, g);
            return fx;
        }

        [Fact]
        public void 고른_것이_없으면_모두_그대로()
        {
            var fx = new InsightEffects("samjae_sword");
            Assert.Equal(1f, fx.WeaponDamageMultiplier("samjae_sword"));
            Assert.Equal(1f, fx.MagnetMultiplier);
            Assert.Equal(1f, fx.OtherHealMultiplier);
            Assert.Equal(1f, fx.DamageTakenMultiplier(0.1f));
            Assert.False(fx.TryBlock());
            Assert.Equal(0f, fx.Tick(10f, false, 100f));
            var b = fx.Bonus(5000, 99, 0.1f);
            foreach (StatKind k in Enum.GetValues(typeof(StatKind))) Assert.Equal(0f, b[k]);
        }

        [Fact]
        public void 데이터의_효과_이름을_모두_안다()
        {
            var known = new[] { "first_weapon_damage", "still_regen", "melee_range", "periodic_block", "magnet_multiplier",
                "yinyang_reset", "kill_stack_might", "might_for_health", "lifesteal_no_heal", "low_health_frenzy", "inner_demon_might" };
            foreach (var i in _d.Insights.Concat(_d.DemonicInsights))
                Assert.Contains(i.Effect, known);
        }

        [Fact]
        public void 일검파만은_시작_무공에만()
        {
            var fx = With(Common("ilgeom"));
            Assert.Equal(1f + Common("ilgeom").Param("damageBonus"), fx.WeaponDamageMultiplier("samjae_sword"), 5);
            Assert.Equal(1f, fx.WeaponDamageMultiplier("flying_dagger"));
        }

        [Fact]
        public void 이기어검은_근접_무공_범위만()
        {
            var igi = Common("igi");
            var fx = With(igi);
            foreach (var w in _d.Weapons)
            {
                float expect = igi.WeaponBehaviors.Contains(w.Behavior) ? 1f + igi.Param("rangeBonus") : 1f;
                Assert.Equal(expect, fx.WeaponRangeMultiplier(w), 5);
            }
            Assert.Contains(_d.Weapons, w => fx.WeaponRangeMultiplier(w) > 1f);
            Assert.Contains(_d.Weapons, w => fx.WeaponRangeMultiplier(w) == 1f);
        }

        [Fact]
        public void 천리추종은_흡기_배율()
        {
            Assert.Equal(Common("cheolli").Param("multiplier"), With(Common("cheolli")).MagnetMultiplier);
        }

        [Fact]
        public void 운기조식은_멈춘_뒤에만_회복()
        {
            var fx = With(Common("ungi"));
            float still = Common("ungi").Param("stillSeconds"), rate = Common("ungi").Param("maxHealthPerSecond");
            Assert.Equal(0f, fx.Tick(still - 0.5f, false, 200f));
            Assert.Equal(0f, fx.Tick(0.4f, false, 200f));
            Assert.Equal(200f * rate * 0.2f, fx.Tick(0.2f, false, 200f), 4);
            Assert.Equal(0f, fx.Tick(0.1f, true, 200f));   // 움직이면 처음부터
            Assert.Equal(0f, fx.StillSeconds);
        }

        [Fact]
        public void 흡혈대법이면_운기조식_회복도_0()
        {
            var fx = With(Common("ungi"), Demonic("heupyeol"));
            Assert.Equal(0f, fx.Tick(100f, false, 200f));
            Assert.Equal(0f, fx.OtherHealMultiplier);
            Assert.Equal(Demonic("heupyeol").Param("lifesteal"), fx.Bonus(0)[StatKind.Lifesteal], 5);
        }

        [Fact]
        public void 금종조는_첫_피해를_막고_간격마다_다시()
        {
            var fx = With(Common("geumjong"));
            float gap = Common("geumjong").Param("intervalSeconds");
            Assert.True(fx.TryBlock());
            Assert.False(fx.TryBlock());
            fx.Tick(gap - 0.01f, true, 100f);
            Assert.False(fx.TryBlock());
            fx.Tick(0.02f, true, 100f);
            Assert.True(fx.TryBlock());
        }

        [Fact]
        public void 살기충천은_처치마다_쌓이고_상한에서_멈춘다()
        {
            var s = Common("salgi");
            var fx = With(s);
            float per = s.Param("killsPerStack");
            Assert.Equal(0f, fx.Bonus((int)per - 1)[StatKind.Might]);
            Assert.Equal(s.Param("mightPerStack") * 3, fx.Bonus((int)per * 3 + 5)[StatKind.Might], 5);
            Assert.Equal(s.Param("mightMax"), fx.Bonus(1_000_000)[StatKind.Might], 5);
        }

        [Fact]
        public void 혈해공은_공력과_기혈을_맞바꾼다()
        {
            var h = Demonic("hyeolhae_gong");
            var b = With(h).Bonus(0);
            Assert.Equal(h.Param("mightBonus"), b[StatKind.Might], 5);
            Assert.Equal(-h.Param("maxHealthPenalty"), b[StatKind.MaxHealth], 5);
        }

        [Fact]
        public void 혈광은_기혈이_문턱_아래일_때만()
        {
            var h = Demonic("hyeolgwang");
            var fx = With(h);
            float thr = h.Param("healthThreshold");
            Assert.Equal(0f, fx.Bonus(0, 0, thr)[StatKind.CooldownReduction]);
            Assert.Equal(1f, fx.DamageTakenMultiplier(thr));
            Assert.Equal(h.Param("cooldownReduction"), fx.Bonus(0, 0, thr - 0.01f)[StatKind.CooldownReduction], 5);
            Assert.Equal(1f + h.Param("damageTaken"), fx.DamageTakenMultiplier(thr - 0.01f), 5);
        }

        [Fact]
        public void 심마_공력은_천마해체_뒤로_다시_센다()
        {
            var s = Demonic("simma");
            var fx = With(s);
            Assert.Equal(s.Param("mightPerStep") * 4, fx.Bonus(0, 4)[StatKind.Might], 5);
            Assert.Equal(s.Param("mightMax"), fx.Bonus(0, 10_000)[StatKind.Might], 5);
            fx.OnDemonicReset(4);
            Assert.Equal(0f, fx.Bonus(0, 4)[StatKind.Might]);
            Assert.Equal(s.Param("mightPerStep") * 2, fx.Bonus(0, 6)[StatKind.Might], 5);
        }

        [Fact]
        public void 역근은_게이지를_되돌리고_기울기를_절반으로()
        {
            var y = Common("yeokgeun");
            var g = Gauge(60);
            var fx = new InsightEffects("samjae_sword");
            fx.Take(y, g);
            Assert.Equal((int)y.Param("resetTo"), g.Value);
            Assert.Equal(y.Param("tiltMultiplier"), g.TiltMultiplier);

            var yang = _d.Weapons.First(w => w.Aspect == YinYang.Yang);
            int per = _d.Balance.Yinyang.WeaponPerLevel;
            for (int k = 0; k < 4; k++) g.OnWeaponLevelUp(yang);
            // 소수 몫을 넘겨 가며 쌓으니 네 번이면 정확히 절반
            Assert.Equal((int)Math.Round(per * 4 * y.Param("tiltMultiplier")), g.Value);
        }

        [Fact]
        public void 기울기_배율의_소수_몫은_반대쪽으로도_맞는다()
        {
            var g = Gauge();
            g.SetTiltMultiplier(0.5f);
            var yang = _d.Weapons.First(w => w.Aspect == YinYang.Yang);
            var yin = _d.Weapons.First(w => w.Aspect == YinYang.Yin);
            g.OnWeaponLevelUp(yang);
            g.OnWeaponLevelUp(yin);
            Assert.Equal(0, g.Value);
            g.Shift(-3); // 심마는 배율 없이 그대로
            Assert.Equal(-3, g.Value);
        }

        [Fact]
        public void 같은_깨달음을_두_번_얻을_수_없다()
        {
            var fx = With(Common("ilgeom"));
            Assert.Throws<InvalidOperationException>(() => fx.Take(Common("ilgeom"), Gauge()));
        }

        Life NewLife(string sect)
        {
            var soul = new Soul(_d);
            soul.EndLife(soul.BeginLife(Rebirth.Prologue(_d)).Finish(LifeEndWay.Fallen, 0));
            var c = _d.Cheonha;
            var cand = new RebirthCandidate(c.Sect(sect), c.Region(c.Sect(sect).Region), Array.Empty<BirthTraitDef>());
            return new Life(soul, Rebirth.Body(c, cand), new Random(1));
        }

        [Fact]
        public void 생의_일검파만은_그_몸이_들고_시작한_무공에()
        {
            var life = NewLife("huashan");
            life.InsightEffects.Take(Common("ilgeom"), life.Loadout.Gauge);
            string start = life.Body.Character.StartWeapon;
            Assert.True(life.Loadout.Weapons.ContainsKey(start));
            Assert.True(life.InsightEffects.WeaponDamageMultiplier(start) > 1f);
        }

        [Fact]
        public void 생의_깨달음_덧셈이_능력치에_얹힌다()
        {
            var life = NewLife("shaolin");
            float before = life.CurrentStats().Might;
            life.InsightEffects.Take(Common("salgi"), life.Loadout.Gauge);
            float after = life.CurrentStats(life.InsightBonus(kills: 1_000_000)).Might;
            Assert.Equal(before + Common("salgi").Param("mightMax"), after, 4);
        }

        [Fact]
        public void 마교_몸_심마_공력은_천마해체로_사라진다()
        {
            var life = NewLife("demonic");
            life.InsightEffects.Take(Demonic("simma"), life.Loadout.Gauge);
            for (int i = 0; i < 20; i++) life.Absorb(200);
            int steps = life.BloodArt!.Steps;
            Assert.True(steps > 0);
            Assert.True(life.InsightBonus(0)[StatKind.Might] > 0f);
            life.Charge.AddFlat(100);
            Assert.True(life.UseUltimate());
            Assert.Equal(0f, life.InsightBonus(0)[StatKind.Might]);
        }
    }
}
