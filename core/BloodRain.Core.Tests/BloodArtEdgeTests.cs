using System;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Progression;
using BloodRain.Core.Run;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>혈공(T-38) 가장자리: 큰 구슬 한 번에 여러 번 기울기, 음 끝에서 멈춤, Life 에 붙은 모양.</summary>
    public class BloodArtEdgeTests
    {
        readonly GameData _d = TestData.Load();

        (BloodArt art, YinYangGauge gauge) New()
        {
            var sect = _d.Cheonha.Sect("demonic");
            var gauge = new YinYangGauge(_d.Balance.Yinyang, sect.YinyangStart);
            return (new BloodArt(sect.BloodArt!, gauge, sect.YinyangStart), gauge);
        }

        [Fact] public void 큰_구슬_하나로_300을_여러_번_넘으면_그만큼_한꺼번에_기운다()
        {
            var (art, gauge) = New();
            int start = gauge.Value;
            // 200 × 4.3 = 860, 더 받은 것 660 → 두 번
            Assert.Equal(860, art.OrbXp(200, 4.3f), 3);
            Assert.Equal(2, art.Steps);
            Assert.Equal(start - 2, gauge.Value);
            // 남은 60 에 240 을 더 채우면 세 번째
            art.OrbXp(100, 3.4f);
            Assert.Equal(3, art.Steps);
            Assert.Equal(start - 3, gauge.Value);
        }

        [Fact] public void 옅은_땅에서_받은_손해는_심마를_되돌리지_않는다()
        {
            var (art, gauge) = New();
            art.OrbXp(100, 4f); // 더 받은 300 → 한 번
            int after = gauge.Value;
            double extra = art.ExtraXp;
            for (int i = 0; i < 50; i++) art.OrbXp(100, 0.8f);
            Assert.Equal(extra, art.ExtraXp);
            Assert.Equal(after, gauge.Value);
            Assert.Equal(1, art.Steps);
        }

        [Fact] public void 음_끝에서는_게이지가_멈추고_천마해체로_돌아온다()
        {
            var (art, gauge) = New();
            for (int i = 0; i < 2000; i++) art.OrbXp(100, 5f);
            Assert.Equal(_d.Balance.Yinyang.Min, gauge.Value);
            Assert.True(art.Steps > 100);
            Assert.True(art.OnUltimate(_d.Cheonha.Sect("demonic").Ultimate));
            Assert.Equal(_d.Cheonha.Sect("demonic").YinyangStart, gauge.Value);
        }

        [Fact] public void 천마해체_뒤에도_쌓인_진기는_이어서_센다()
        {
            var (art, gauge) = New();
            art.OrbXp(100, 3.5f); // 250
            art.OnUltimate("cheonma_haeche");
            int start = gauge.Value;
            art.OrbXp(100, 1.5f); // +50 → 300, 한 번
            Assert.Equal(1, art.Steps);
            Assert.Equal(start - 1, gauge.Value);
        }

        // ---- Life 에 붙은 모양 ----

        Life NewLife(string sect)
        {
            var soul = new Soul(_d);
            soul.EndLife(soul.BeginLife(Rebirth.Prologue(_d)).Finish(LifeEndWay.Fallen, 0));
            var c = _d.Cheonha;
            var cand = new RebirthCandidate(c.Sect(sect), c.Region(c.Sect(sect).Region), Array.Empty<BirthTraitDef>());
            return new Life(soul, Rebirth.Body(c, cand), new Random(1));
        }

        [Fact] public void 마교_몸만_혈공이_붙는다()
        {
            Assert.NotNull(NewLife("demonic").BloodArt);
            foreach (var s in _d.Cheonha.Sects)
                if (s.Id != "demonic") Assert.Null(NewLife(s.Id).BloodArt);
        }

        [Fact] public void 마교_몸은_진기를_농도배로_받고_윤회의_기는_구슬_값_그대로()
        {
            var demonic = NewLife("demonic");
            float density = demonic.Density;
            Assert.True(density > 1, $"십만대산 농도 {density}");
            demonic.Absorb(5);
            Assert.Equal(5 * density, demonic.Xp.Total, 3);
            Assert.Equal(5, demonic.Log.Qi.OrbValue, 6);
            Assert.Equal(5 * density, demonic.Log.Qi.WeightedOrbValue, 3);

            // 같은 농도 자리라도 다른 문파는 1배
            var shaolin = NewLife("shaolin");
            shaolin.MoveTo(demonic.Position);
            Assert.Equal(density, shaolin.Density, 4);
            shaolin.Absorb(5);
            Assert.Equal(5, shaolin.Xp.Total, 6);
            Assert.Equal(demonic.Log.Qi.WeightedOrbValue, shaolin.Log.Qi.WeightedOrbValue, 6);
        }

        [Fact] public void 마교_몸이_진기를_먹으면_생의_음양_게이지가_음으로_기운다()
        {
            var life = NewLife("demonic");
            int start = life.Loadout.Gauge.Value;
            double perStep = _d.Cheonha.Sect("demonic").BloodArt!.ExtraXpPerStep;
            double orb = Math.Ceiling(perStep / (life.Density - 1)) + 1;
            life.Absorb(orb);
            Assert.Equal(1, life.BloodArt!.Steps);
            Assert.Equal(start - 1, life.Loadout.Gauge.Value);
        }
    }
}
