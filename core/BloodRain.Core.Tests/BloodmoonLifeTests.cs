using System;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Run;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>혈월 천하(회귀, 기획서 21장)의 적 기혈·속도 배율을 한 생이 내주는지.</summary>
    public class BloodmoonLifeTests
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

        [Fact] public void 첫_회차에선_배율이_1()
        {
            var life = NewLife(false);
            Assert.Equal(1f, life.EnemyHealthMultiplier);
            Assert.Equal(1f, life.EnemySpeedMultiplier);
        }

        [Fact] public void 혈월_천하에선_데이터의_배율()
        {
            var life = NewLife(true);
            Assert.Equal(_d.Cheonha.Cycle.Bloodmoon.EnemyHealth, life.EnemyHealthMultiplier);
            Assert.Equal(_d.Cheonha.Cycle.Bloodmoon.EnemySpeed, life.EnemySpeedMultiplier);
            Assert.True(life.EnemyHealthMultiplier > 1f && life.EnemySpeedMultiplier > 1f);
        }

        [Fact] public void 기획서_21장_회차표와_같다()
        {
            // | 혈월 천하(회귀) | 적 기혈 +50%, 속도 +15%, 보상 +50% ...
            string gdd = TestData.ReadRepoFile("docs", "design", "gdd.md");
            var b = _d.Cheonha.Cycle.Bloodmoon;
            string Pct(float m) => ((int)Math.Round((m - 1) * 100)).ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";
            Assert.Contains($"적 기혈 +{Pct(b.EnemyHealth)}, 속도 +{Pct(b.EnemySpeed)}, 보상 +{Pct(b.Rewards)}", gdd);
        }
    }
}
