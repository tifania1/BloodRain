using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Progression;
using BloodRain.Core.Run;
using BloodRain.Core.World;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>문파 고유 능력(기획서 5장 '한눈에 보기' 고유 능력 칸, cheonha.json sects[].traitEffect, R-04).</summary>
    public class SectTraitTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        readonly GameData _d = TestData.Load();
        CheonhaDef C => _d.Cheonha;

        RebirthBody Body(string sect) =>
            Rebirth.Body(C, new RebirthCandidate(C.Sect(sect), C.Region(C.Sect(sect).Region), Array.Empty<BirthTraitDef>()));

        SectTrait Trait(string sect) => SectTrait.Of(C.Sect(sect));

        StatSheet Sheet(string sect, float healthRatio = 1f, bool moving = false) =>
            Body(sect).ComputeStats(_d, new Dictionary<string, int>(), new Dictionary<string, int>(), healthRatio: healthRatio, moving: moving);

        static string Pct(float v) => Math.Round(v * 100).ToString(Inv) + "%";

        string Cell(string sectKo) =>
            TestData.GddTable(TestData.GddSection("한눈에 보기")).Single(r => r[0] == sectKo)[4];

        // ---- 기획서 표와 데이터 ----

        [Fact] public void 고유_능력_칸의_숫자가_데이터와_같다()
        {
            var shaolin = C.Sect("shaolin").TraitEffect!;
            Assert.Contains($"받는 피해 -{shaolin.Stats["Armor"].ToString(Inv)}", Cell("소림"));

            var gaebang = C.Sect("gaebang").TraitEffect!;
            Assert.Contains($"은자 +{Pct(gaebang.Stats["Greed"])}", Cell("개방"));
            Assert.Contains($"음식 회복 +{Pct(gaebang.Stats["FoodHeal"])}", Cell("개방"));

            var namgung = C.Sect("namgung").TraitEffect!;
            Assert.Contains($"기혈 {Pct(namgung.AboveHealthRatio)} 이상이면 공력 +{Pct(namgung.AboveHealthStats["Might"])}", Cell("남궁"));

            var tang = C.Sect("tang").TraitEffect!;
            Assert.True(tang.PoisonImmune);
            Assert.Contains("독 피해 무효", Cell("당가"));
            Assert.Contains($"중독된 적에게 피해 +{Pct(tang.PoisonedTargetDamage)}", Cell("당가"));

            var waterfort = C.Sect("waterfort").TraitEffect!;
            Assert.True(waterfort.IgnoresWater);
            Assert.Contains($"움직이는 동안 회피 {Pct(waterfort.MovingStats["Evade"])}", Cell("수로채"));
            Assert.Contains("물 지형 감속 무시", Cell("수로채"));

            var wudang = C.Sect("wudang");
            Assert.Contains($"-{wudang.HarmonyRange}~+{wudang.HarmonyRange}", Cell("무당"));
            Assert.Contains($"조화 보너스 {wudang.TraitEffect!.HarmonyBonusMultiplier.ToString(Inv)}배", Cell("무당"));
        }

        [Fact] public void M3_여섯_문파는_효과_칸이_있고_능력치_이름은_모두_읽힌다()
        {
            foreach (var id in new[] { "shaolin", "gaebang", "namgung", "tang", "waterfort", "wudang" })
                Assert.NotNull(C.Sect(id).TraitEffect);
            foreach (var s in C.Sects.Where(s => s.TraitEffect != null))
            {
                var t = s.TraitEffect!;
                foreach (var name in t.Stats.Keys.Concat(t.AboveHealthStats.Keys).Concat(t.MovingStats.Keys))
                    Stats.Parse(name);
                if (t.AboveHealthStats.Count > 0) Assert.InRange(t.AboveHealthRatio, 0.01f, 1f);
            }
        }

        // ---- 몸 능력치 ----

        [Fact] public void 소림은_호신_2라_받는_피해가_2_줄어든다()
        {
            var sheet = Sheet("shaolin");
            Assert.Equal(2f, sheet.Armor, 3);
            Assert.Equal(3f, Damage.Taken(5f, sheet.Armor), 3);
            Assert.Equal(0f, Sheet("namgung", 0.4f).Armor, 3);
        }

        [Fact] public void 개방은_은자_30퍼센트_음식_회복_50퍼센트()
        {
            var b = Sheet("gaebang").Bonus;
            Assert.Equal(0.30f, b[StatKind.Greed], 3);
            Assert.Equal(0.50f, b[StatKind.FoodHeal], 3);
            float bun = Drops.FoodHeal(_d.Balance.Drops, Drops.Bun);
            Assert.Equal(bun * 1.5f, Drops.FoodHeal(_d.Balance.Drops, Drops.Bun, 1f + b[StatKind.FoodHeal]), 3);
        }

        [Fact] public void 남궁은_기혈_50퍼센트_이상일_때만_공력_25퍼센트()
        {
            Assert.Equal(1.25f, Sheet("namgung", 1f).Might, 3);
            Assert.Equal(1.25f, Sheet("namgung", 0.5f).Might, 3);
            Assert.Equal(1.00f, Sheet("namgung", 0.49f).Might, 3);
            Assert.Equal(1.00f, Sheet("shaolin", 1f).Might, 3);
        }

        [Fact] public void 수로채는_움직이는_동안만_회피_15퍼센트()
        {
            Assert.Equal(0.15f, Sheet("waterfort", moving: true).Evade, 3);
            Assert.Equal(0f, Sheet("waterfort", moving: false).Evade, 3);
            Assert.Equal(0f, Sheet("shaolin", moving: true).Evade, 3);
        }

        [Fact] public void 회피_굴림은_확률대로_나온다()
        {
            var rng = new Random(7);
            int n = 20000, hit = Enumerable.Range(0, n).Count(_ => Evade.Roll(0.15f, rng));
            Assert.InRange(hit / (double)n, 0.14, 0.16);
            Assert.False(Evade.Roll(0f, new Random(1)));
        }

        [Fact] public void 회피는_0에서_1_사이로_자른다()
        {
            var extra = new StatBonus();
            extra.Add(StatKind.Evade, 2f);
            var sheet = Body("waterfort").ComputeStats(_d, new Dictionary<string, int>(), new Dictionary<string, int>(), extra, moving: true);
            Assert.Equal(1f, sheet.Evade, 3);
        }

        // ---- 기믹·피해 ----

        [Fact] public void 당가는_독안개를_맞지_않고_중독된_적에게_20퍼센트_더_준다()
        {
            var fog = new PoisonFogGimmick(_d.Gimmicks.PoisonFog);
            var tang = Body("tang").SectTrait;
            Assert.Equal(0f, fog.HealthLoss(GimmickTargetKind.Player, 85f, 1f, tang.PoisonImmune));
            Assert.True(fog.HealthLoss(GimmickTargetKind.Player, 100f, 1f, Body("shaolin").SectTrait.PoisonImmune) > 0f);
            Assert.Equal(1.2f, tang.DamageMultiplier(targetPoisoned: true), 3);
            Assert.Equal(1f, tang.DamageMultiplier(targetPoisoned: false), 3);
            Assert.Equal(1f, Body("namgung").SectTrait.DamageMultiplier(true), 3);
        }

        [Fact] public void 수로채는_물에서_느려지지_않는다()
        {
            var water = _d.Gimmicks.Water;
            Assert.Equal(1f, WaterGimmick.MoveSpeedMultiplier(water, inWater: true, ignoresWater: Body("waterfort").SectTrait.IgnoresWater));
            Assert.Equal(water.MoveSpeedMultiplier, WaterGimmick.MoveSpeedMultiplier(water, inWater: true, ignoresWater: Body("tang").SectTrait.IgnoresWater));
        }

        // ---- 무당 조화 보너스 2배 ----

        [Fact] public void 무당은_조화_보너스가_두_배()
        {
            var h = _d.Balance.Yinyang.Harmony;
            var wudang = Trait("wudang");
            Assert.Equal(h.CooldownReduction * 2, wudang.HarmonyCooldownReduction(h), 4);
            Assert.Equal(h.UltimateChargeBonus * 2, wudang.HarmonyUltimateChargeBonus(h), 4);
            Assert.Equal(h.CooldownReduction, Trait("shaolin").HarmonyCooldownReduction(h), 4);
        }

        Life NewLife(string sect)
        {
            var soul = new Soul(_d);
            soul.EndLife(soul.BeginLife(Rebirth.Prologue(_d)).Finish(LifeEndWay.Fallen, 0));
            return new Life(soul, Body(sect), new Random(1));
        }

        [Fact] public void 무당_몸의_생은_태극일_때_오의가_1점5배_쾌속이_20퍼센트()
        {
            var h = _d.Balance.Yinyang.Harmony;
            var wudang = NewLife("wudang");
            var shaolin = NewLife("shaolin");
            wudang.Loadout.Gauge.ResetTo(0);
            shaolin.Loadout.Gauge.ResetTo(0);
            Assert.Equal(1f + h.UltimateChargeBonus * 2, wudang.Charge.Rate(YinYangBand.Harmony), 4);
            Assert.Equal(1f + h.UltimateChargeBonus, shaolin.Charge.Rate(YinYangBand.Harmony), 4);
            Assert.Equal(1f, wudang.Charge.Rate(YinYangBand.Bias), 4);
            Assert.Equal(h.CooldownReduction * 2, wudang.HarmonyCooldownReduction, 4);
            Assert.Equal(h.CooldownReduction, shaolin.HarmonyCooldownReduction, 4);
            wudang.Loadout.Gauge.ResetTo(60); // 태극 구간(±40) 밖
            Assert.Equal(0f, wudang.HarmonyCooldownReduction);
        }

        [Fact] public void 생의_능력치에도_고유_능력이_얹힌다()
        {
            var life = NewLife("namgung");
            Assert.Equal(1.25f, life.CurrentStats().Might, 3);
            Assert.Equal(1.00f, life.CurrentStats(healthRatio: 0.3f).Might, 3);
            Assert.Equal(0.15f, NewLife("waterfort").CurrentStats(moving: true).Evade, 3);
        }

        [Fact] public void 서장_몸과_숫자가_없는_문파는_아무것도_하지_않는다()
        {
            var prologue = Rebirth.Prologue(_d);
            Assert.Same(SectTrait.None, prologue.SectTrait);
            var b = SectTrait.None.Bonus(1f, true);
            foreach (StatKind k in Enum.GetValues(typeof(StatKind))) Assert.Equal(0f, b[k]);
            Assert.False(SectTrait.None.PoisonImmune);
            Assert.False(SectTrait.None.IgnoresWater);
            Assert.Equal(1f, SectTrait.None.HarmonyBonusMultiplier);
        }
    }
}
