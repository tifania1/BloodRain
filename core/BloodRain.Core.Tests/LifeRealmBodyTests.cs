using System;
using BloodRain.Core.Build;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Run;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>경지 돌파 보상이 몸에 얹히는 길(Life.CurrentStats·DashMaxCharges·오의 배율·받는 피해·오의 2연속). 기획서 3장 경지표.</summary>
    public class LifeRealmBodyTests
    {
        readonly GameData _d = TestData.Load();

        Life NewLife(string sect = "shaolin")
        {
            var soul = new Soul(_d);
            soul.EndLife(soul.BeginLife(Rebirth.Prologue(_d)).Finish(LifeEndWay.Fallen, 0));
            var c = _d.Cheonha;
            var cand = new RebirthCandidate(c.Sect(sect), c.Region(c.Sect(sect).Region), Array.Empty<BirthTraitDef>());
            return new Life(soul, Rebirth.Body(c, cand), new Random(1));
        }

        static void ReachLevel(Life life, int level)
        {
            for (int i = 0; i < 100000 && life.Level < level; i++) life.Absorb(50);
            Assert.True(life.Level >= level);
        }

        [Fact] public void 삼류에서는_돌파_보상이_없다()
        {
            var life = NewLife();
            var s = life.CurrentStats();
            Assert.Equal(life.Body.Character.MaxHealth, s.MaxHealth, 3);
            Assert.Equal(_d.Balance.Dash.Charges, life.DashMaxCharges(s));
            Assert.Equal(1f, life.UltimatePowerMultiplier);
            Assert.Equal(1f, life.DamageTakenMultiplier);
            Assert.Equal(1, life.UltimateCasts);
        }

        [Fact] public void 이류는_최대_기혈_10을_그대로_더하고_일류는_경공_충전_1()
        {
            var life = NewLife();
            ReachLevel(life, 20);
            var s = life.CurrentStats();
            Assert.Equal(life.Body.Character.MaxHealth + 10f, s.MaxHealth, 3);
            Assert.Equal(_d.Balance.Dash.Charges + 1, life.DashMaxCharges(s));
        }

        [Fact] public void 현경까지_오면_오의_위력_받는_피해_오의_2연속()
        {
            var life = NewLife();
            ReachLevel(life, 90);
            Assert.Equal(1.25f, life.UltimatePowerMultiplier, 4);
            Assert.Equal(0.9f, life.DamageTakenMultiplier, 4);
            Assert.Equal(2, life.UltimateCasts);
        }

        [Fact] public void 최대_기혈_덧셈은_비율을_곱한_뒤에_더한다()
        {
            var ch = _d.Characters[0];
            var b = new StatBonus();
            b.Add(StatKind.MaxHealth, 0.5f);
            var s = new StatSheet(ch, _d.Balance.Stats, b, 10f);
            Assert.Equal(ch.MaxHealth * 1.5f + 10f, s.MaxHealth, 3);
        }

        [Fact] public void 연무장_경공_충전은_StatSheet_에서_정수로()
        {
            var ch = _d.Characters[0];
            var b = new StatBonus();
            b.Add(StatKind.DashCharges, 1f);
            Assert.Equal(1, new StatSheet(ch, _d.Balance.Stats, b).DashCharges);
        }
    }
}
