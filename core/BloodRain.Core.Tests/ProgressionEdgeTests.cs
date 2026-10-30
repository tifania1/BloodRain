using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Progression;
using BloodRain.Core.Spawning;
using BloodRain.Core.World;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>진기 곡선·한 판 진행·생성 곡선·혈마의 그림자의 가장자리 경우.
    /// BalanceTests·RunProgressTests·FinalStagesTests 가 안 보는 쪽만 모았다.</summary>
    public class ProgressionEdgeTests
    {
        readonly GameData _d = Load();
        XpCurve Curve => new XpCurve(_d.Balance.XpCurve);
        RunXp NewRun(float growth = 1f) => new RunXp(Curve, new Realms(_d.Balance.Realms), "normal", growth);

        // ---- XpCurve ----

        [Fact] public void 레벨_0_이하는_터진다()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Curve.Required(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Curve.Required(-3));
        }

        /// <summary>구간이 바뀌는 자리에서 필요 진기가 끊기지 않는다(다음 구간 base = 앞 구간 끝 값).</summary>
        [Fact] public void 구간_경계에서_곡선이_이어진다()
        {
            var segs = _d.Balance.XpCurve.Segments;
            var c = Curve;
            for (int i = 1; i < segs.Count; i++)
            {
                Assert.Equal(segs[i - 1].ToLevel + 1, segs[i].FromLevel);
                Assert.Equal(c.Required(segs[i].Anchor), segs[i].Base);
                Assert.True(c.Required(segs[i].FromLevel) > c.Required(segs[i - 1].ToLevel));
            }
        }

        [Fact] public void 필요_진기는_레벨마다_늘어난다()
        {
            var c = Curve;
            for (int n = 2; n <= 120; n++) Assert.True(c.Required(n) > c.Required(n - 1), $"레벨 {n}");
        }

        [Fact] public void 마지막_구간_뒤로도_같은_기울기로_늘어난다()
        {
            var last = _d.Balance.XpCurve.Segments[^1];
            var c = Curve;
            int beyond = last.ToLevel + 5;
            Assert.Equal(last.Step, c.Required(beyond) - c.Required(beyond - 1));
        }

        [Theory]
        [InlineData(1)] [InlineData(2)] [InlineData(20)] [InlineData(21)] [InlineData(41)] [InlineData(90)]
        public void 누적_진기와_레벨은_서로_뒤집힌다(int level)
        {
            var c = Curve;
            long cum = c.CumulativeTo(level);
            Assert.Equal(level, c.LevelFor(cum));
            if (cum > 0) Assert.Equal(level - 1, c.LevelFor(cum - 1));
        }

        // ---- RunXp ----

        [Fact] public void 음수_진기는_터진다()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => NewRun().Absorb(-0.1));
        }

        [Fact] public void 진기_0은_아무것도_바꾸지_않는다()
        {
            var r = NewRun();
            var a = r.Absorb(0);
            Assert.Equal(0, a.LevelUps);
            Assert.Empty(a.Breakthroughs);
            Assert.Equal(1, r.Level);
            Assert.Equal(0, r.Total);
        }

        /// <summary>구슬을 잘게 먹든 한 번에 먹든 도착 레벨·남은 진기·총량이 같다.</summary>
        [Fact] public void 잘게_먹어도_한_번에_먹어도_같다()
        {
            var big = NewRun();
            var small = NewRun();
            big.Absorb(5000);
            int ups = 0, breaks = 0;
            for (int i = 0; i < 2500; i++)
            {
                var a = small.Absorb(2);
                ups += a.LevelUps;
                breaks += a.Breakthroughs.Count;
            }
            Assert.Equal(big.Level, small.Level);
            Assert.Equal(big.Xp, small.Xp, 6);
            Assert.Equal(big.Total, small.Total, 6);
            Assert.Equal(big.Level - 1, ups);
            Assert.Equal(new Realms(_d.Balance.Realms).Breakthroughs(1, big.Level, "normal").Count(), breaks);
        }

        [Fact] public void 채움_비율은_0_이상_1_미만()
        {
            var r = NewRun();
            var rng = new Random(7);
            for (int i = 0; i < 500; i++)
            {
                r.Absorb(rng.NextDouble() * 40);
                Assert.InRange(r.Progress, 0, 0.999999);
            }
        }

        [Fact] public void 심득_배율을_판_도중에_바꾸면_그_뒤로만_곱한다()
        {
            var r = NewRun();
            r.Absorb(3);
            r.Growth = 2f;
            r.Absorb(3);
            Assert.Equal(9, r.Total, 6);
        }

        // ---- SpawnCurve ----

        [Fact] public void 점이_하나뿐인_곡선은_터진다()
        {
            var def = new SpawnTargetDef { Points = new List<float[]> { new[] { 0f, 10f } } };
            Assert.Throws<ArgumentException>(() => new SpawnCurve(def));
        }

        [Fact] public void 첫_점_앞은_첫_값을_쓴다()
        {
            var t = _d.Balance.SpawnTarget;
            Assert.Equal(t.Points[0][1], new SpawnCurve(t).TargetAt(t.Points[0][0] - 3f));
        }

        [Fact] public void 다시_시작_시각이_없으면_계속_멈춘다()
        {
            var c = new SpawnCurve(_d.Balance.SpawnTarget, 5f);
            Assert.False(c.SpawningStopped(4.99f));
            foreach (var m in new[] { 5f, 10f, 30f, 120f }) Assert.Equal(0, c.SpawnThisSecond(m, 0));
        }

        [Fact] public void 목표보다_많이_살아_있으면_만들지_않는다()
        {
            var c = new SpawnCurve(_d.Balance.SpawnTarget);
            float target = c.TargetAt(12f);
            Assert.Equal(0, c.SpawnThisSecond(12f, (int)Math.Ceiling(target)));
            Assert.Equal(0, c.SpawnThisSecond(12f, (int)target + 50));
        }

        /// <summary>부족분이 조금이라도 있으면 적어도 한 마리는 만든다(올림).</summary>
        [Fact] public void 부족분이_작아도_한_마리는_만든다()
        {
            var c = new SpawnCurve(_d.Balance.SpawnTarget);
            int target = (int)Math.Floor(c.TargetAt(19f));
            Assert.Equal(1, c.SpawnThisSecond(19f, target - 1));
        }

        // ---- BloodShadow ----

        BloodShadowDef Sh => _d.Cheonha.LifeEnd.Shadow;

        [Fact] public void 그림자_음수_단계도_없다()
        {
            var s = BloodShadow.Strength(Sh, -2);
            Assert.False(s.Active);
            Assert.Equal(0f, s.ContactDamage);
            Assert.Equal(1f, s.EnemyDamageMultiplier);
        }

        [Fact] public void 그림자_속도는_상한에서_멈추고_맞는_간격은_그대로다()
        {
            var far = BloodShadow.Strength(Sh, 200);
            Assert.Equal(Sh.MaxSpeed, far.MoveSpeed);
            for (int step = 1; step <= 20; step++)
                Assert.Equal(Sh.HitIntervalSeconds, BloodShadow.Strength(Sh, step).HitIntervalSeconds);
        }

        [Fact] public void 그림자가_끝까지_안_빨라지면_0()
        {
            var slow = new BloodShadowDef { ContactDamage = 10, DamageGrowth = 1.1f, MoveSpeed = 60, SpeedPerStep = 10, MaxSpeed = 95, HitIntervalSeconds = 1, EnemyDamagePerStep = 0.1f };
            Assert.Equal(0, BloodShadow.FirstStepFasterThanPlayer(slow));
            var still = new BloodShadowDef { ContactDamage = 10, DamageGrowth = 1f, MoveSpeed = 80, SpeedPerStep = 0, MaxSpeed = 200, HitIntervalSeconds = 1 };
            Assert.Equal(0, BloodShadow.FirstStepFasterThanPlayer(still));
        }
    }
}
