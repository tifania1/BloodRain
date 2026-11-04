using System;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Run;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>QA-01 Core 점검에서 찾아 고친 것들을 묶어 둔 테스트.</summary>
    public class CoreQaTests
    {
        readonly GameData _d = TestData.Load();

        Life NewLife(string sect, params string[] traits)
        {
            var soul = new Soul(_d);
            soul.EndLife(soul.BeginLife(Rebirth.Prologue(_d)).Finish(LifeEndWay.Fallen, 0));
            var c = _d.Cheonha;
            var cand = new RebirthCandidate(c.Sect(sect), c.Region(c.Sect(sect).Region), traits.Select(c.Trait).ToArray());
            return new Life(soul, Rebirth.Body(c, cand), new Random(1));
        }

        [Fact] public void 한_생의_진기에_몸의_심득이_곱해진다()
        {
            var plain = NewLife("shaolin");
            var dull = NewLife("shaolin", "dunjae");
            plain.Absorb(1);
            dull.Absorb(1);
            float growth = 1f + (float)_d.Cheonha.Trait("dunjae").Stats["Growth"];
            Assert.Equal(1.0, plain.Xp.Total, 6);
            Assert.Equal(growth, dull.Xp.Total, 4);
            Assert.True(dull.Xp.Total < plain.Xp.Total);
        }
    }
}
