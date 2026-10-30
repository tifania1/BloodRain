using System;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Progression;
using BloodRain.Core.Run;
using Newtonsoft.Json.Linq;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>한 생(Life)이 오의 게이지를 들고 있고, 기혈역류를 경공으로 끊으면 그 게이지에 +50 이 들어간다.</summary>
    public class LifeChargeTests
    {
        readonly GameData _d = TestData.Load();

        Soul NewSoul(int qi = 0)
        {
            var soul = new Soul(_d);
            soul.EndLife(soul.BeginLife(Rebirth.Prologue(_d)).Finish(LifeEndWay.Fallen, 0));
            if (qi == 0) return soul;
            var j = JObject.Parse(soul.ToJson());
            var key = j.Property("RebirthQi") != null ? "RebirthQi" : "rebirthQi";
            j[key] = qi;
            return Soul.FromJson(_d, j.ToString());
        }

        Life NewLife(Soul soul, string sect = "shaolin")
        {
            var c = _d.Cheonha;
            var cand = new RebirthCandidate(c.Sect(sect), c.Region(c.Sect(sect).Region), Array.Empty<BirthTraitDef>());
            return new Life(soul, Rebirth.Body(c, cand), new Random(1));
        }

        [Fact] public void 새_생은_빈_게이지로_시작한다()
        {
            var life = NewLife(NewSoul());
            Assert.Equal(0f, life.Charge.Value);
            Assert.Equal(0f, life.Charge.ChargeBonus);
        }

        [Fact] public void 역류를_경공으로_끊으면_생의_게이지에_50이_들어간다()
        {
            var life = NewLife(NewSoul());
            life.Loadout.Gauge.ResetTo(100); // 극성
            Assert.Equal(YinYangBand.Extreme, life.Loadout.Gauge.Band);
            var x = _d.Balance.Yinyang.Extreme;
            int steps = (int)Math.Ceiling(x.BacklashIntervalSeconds / 0.1f) + 2;
            for (int i = 0; i < steps && !life.Backlash.IsActive; i++) life.Tick(0.1f, life.Position, 0);
            Assert.True(life.Backlash.IsActive, "극성 60초가 지나도 역류가 안 왔다");
            Assert.True(life.Backlash.OnDash());
            Assert.Equal(x.DashCancelUltimateCharge, life.Charge.Value);
        }

        [Fact] public void 연무장_오의_충전은_생의_게이지_배율에_들어간다()
        {
            var soul = NewSoul(qi: 100_000);
            Assert.True(soul.TryBuyTraining("UltimateCharge"));
            Assert.True(soul.TryBuyTraining("UltimateCharge"));
            var life = NewLife(soul);
            float per = soul.TrainingBonus()[Build.StatKind.UltimateCharge];
            Assert.True(per > 0f);
            Assert.Equal(per, life.Charge.ChargeBonus, 4);
            life.Charge.OnHit(YinYangBand.Bias);
            Assert.Equal(_d.Balance.UltimateCharge.PerHit * (1 + per), life.Charge.Value, 3);
        }
    }
}
