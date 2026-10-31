using System;
using System.Globalization;
using System.Linq;
using BloodRain.Sim;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>
    /// 조정 시험 목록(core/BloodRain.Sim/Scenarios.cs). 보고서 '조정 제안'에 그대로 찍히는 Change 글이
    /// "지금 값 → 바꿀 값" 모양이라, 데이터가 바뀌면 글의 '지금 값'이 거짓이 된다. 그걸 여기서 잡는다.
    /// </summary>
    public class ScenarioTests
    {
        static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        [Fact]
        public void id는_겹치지_않고_모두_이름과_설명이_있다()
        {
            Assert.Equal(Scenarios.All.Count, Scenarios.All.Select(s => s.Id).Distinct().Count());
            Assert.Same(Scenarios.Baseline, Scenarios.All[0]);
            foreach (var s in Scenarios.All.Skip(1))
            {
                Assert.False(string.IsNullOrWhiteSpace(s.NameKo), s.Id);
                Assert.False(string.IsNullOrWhiteSpace(s.Change), s.Id);
                Assert.False(string.IsNullOrWhiteSpace(s.Why), s.Id);
            }
            Assert.Contains(Scenarios.All, s => s.Id == Report.RecommendedId);
        }

        [Fact]
        public void 모르는_id는_목록을_말하며_예외()
        {
            var ex = Assert.Throws<ArgumentException>(() => Scenarios.Find("nope"));
            Assert.Contains("nope", ex.Message);
            Assert.Contains("kill-xp", ex.Message);
            Assert.Same(Scenarios.Baseline, Scenarios.Find("baseline"));
        }

        [Fact]
        public void 바꾸는_글의_지금_값이_데이터와_같다()
        {
            var b = SimData.Load(TestData.DataDir).Game.Balance;
            Assert.Contains($"{F(b.SpawnTarget.SpawnFractionPerSecond)} →", Scenarios.Find("refill").Change);
            Assert.Contains($"{F(b.EnemyScaling.HealthPerMinute)} →", Scenarios.Find("health").Change);
            Assert.Contains($"{F(b.EnemyScaling.HealthPerMinute)} →", Scenarios.Find("kill-xp+health").Change);
            Assert.Contains($"{b.Evolution.PairPassiveMinLevel} →", Scenarios.Find("kill-xp+pair2").Change);
            var seg = b.XpCurve.Segments;
            var curve = Scenarios.Find("curve").Change;
            Assert.Contains($"step {F(seg[1].Step)} →", curve);
            Assert.Contains($"base {F(seg[2].Base)} →", curve);
            Assert.Contains($"step {F(seg[2].Step)} →", curve);
        }

        [Fact]
        public void 시험은_메모리에서만_바꾸고_파일은_그대로()
        {
            var before = SimData.Load(TestData.DataDir).Game.Balance;
            foreach (var s in Scenarios.All) s.LoadData(TestData.DataDir);
            var after = SimData.Load(TestData.DataDir).Game.Balance;
            Assert.Equal(before.SpawnTarget.SpawnFractionPerSecond, after.SpawnTarget.SpawnFractionPerSecond);
            Assert.Equal(before.EnemyScaling.HealthPerMinute, after.EnemyScaling.HealthPerMinute);
            Assert.Equal(before.Evolution.PairPassiveMinLevel, after.Evolution.PairPassiveMinLevel);
            Assert.Equal(before.XpCurve.Segments[2].Base, after.XpCurve.Segments[2].Base);

            Assert.Equal(0.1f, Scenarios.Find("refill").LoadData(TestData.DataDir).Game.Balance.SpawnTarget.SpawnFractionPerSecond);
            Assert.Equal(2, Scenarios.Find("kill-xp+pair2").LoadData(TestData.DataDir).Game.Balance.Evolution.PairPassiveMinLevel);
        }

        [Fact]
        public void 곡선_시험은_둘째와_셋째_구간이_이어진다()
        {
            var seg = Scenarios.Find("curve").LoadData(TestData.DataDir).Game.Balance.XpCurve.Segments;
            Assert.Equal((double)(seg[1].Base + seg[1].Step * (seg[1].ToLevel - seg[1].Anchor)), (double)seg[2].Base, 3);
            Assert.Contains($"→ {F(seg[2].Base)},", Scenarios.Find("curve").Change);   // 글의 795 가 계산과 같은지
        }

        [Fact]
        public void 처치_진기_표는_있는_적만_가리킨다()
        {
            var g = TestData.Load();
            foreach (var id in Scenarios.LeanKillXp.Keys) Assert.Contains(g.Enemies, e => e.Id == id);
            Assert.All(Scenarios.LeanKillXp.Values, v => Assert.True(v > 0));
            var o = Scenarios.Find("kill-xp").ApplyOptions(new SimOptions());
            Assert.Same(Scenarios.LeanKillXp, o.KillXp);
            Assert.Null(Scenarios.Baseline.ApplyOptions(new SimOptions()).KillXp);
        }
    }
}
