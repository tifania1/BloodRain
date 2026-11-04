using System;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Run;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>한 생의 은자 지갑(7장 보부상, 10장 '남은 은자' 정산). 주운 은자와 정산의 남은 은자가 같은 값인지.</summary>
    public class LifeSilverTests
    {
        readonly GameData _d = TestData.Load();

        Life NewLife(bool bloodmoon)
        {
            var soul = new Soul(_d);
            soul.EndLife(soul.BeginLife(Rebirth.Prologue(_d)).Finish(LifeEndWay.Fallen, 0));
            if (bloodmoon) soul.Regress();
            var c = _d.Cheonha;
            var cand = new RebirthCandidate(c.Sect("shaolin"), c.Region(c.Sect("shaolin").Region), Array.Empty<BirthTraitDef>());
            return new Life(soul, Rebirth.Body(c, cand), new Random(1));
        }

        [Fact] public void 주운_은자가_지갑에_쌓이고_쓰면_줄어든다()
        {
            var life = NewLife(false);
            Assert.Equal(0, life.Silver);
            Assert.Equal(30, life.GainSilver(30));
            Assert.Equal(0, life.GainSilver(0));
            Assert.Equal(0, life.GainSilver(-5));
            Assert.True(life.SpendSilver(20));
            Assert.Equal(10, life.Silver);
            Assert.False(life.SpendSilver(11)); // 모자라면 안 쓴다
            Assert.Equal(10, life.Silver);
            Assert.Throws<ArgumentOutOfRangeException>(() => life.SpendSilver(-1));
        }

        [Fact] public void 혈월_천하에선_보상_배율을_곱해_반올림한다()
        {
            var life = NewLife(true);
            float m = _d.Cheonha.Cycle.Bloodmoon.Rewards;
            int expect = (int)Math.Round(25 * (double)m, MidpointRounding.AwayFromZero);
            Assert.Equal(expect, life.GainSilver(25));
            Assert.Equal(expect, life.Silver);
        }

        [Fact] public void 정산의_남은_은자는_지갑_값()
        {
            var life = NewLife(false);
            life.GainSilver(400);
            life.SpendSilver(150);
            var rec = life.End(LifeEndWay.Fallen);
            Assert.Equal(250, rec.RebirthQi.LeftoverSilver);
            Assert.Throws<InvalidOperationException>(() => life.GainSilver(1)); // 끝난 생
        }

        [Fact] public void 남은_은자를_따로_주면_그_값으로_정산한다()
        {
            // Unity 쪽이 아직 지갑을 안 쓰는 동안(LevelUpScreen.Silver) 옛 호출이 그대로 돈다.
            var life = NewLife(false);
            life.GainSilver(400);
            var rec = life.End(LifeEndWay.Zuohwa, 90);
            Assert.Equal(90, rec.RebirthQi.LeftoverSilver);
        }
    }
}
