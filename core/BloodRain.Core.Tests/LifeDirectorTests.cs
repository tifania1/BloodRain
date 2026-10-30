using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Spawning;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>한 생의 생성(기획서 7장 '한 생의 흐름', 11장 '생성 규칙').</summary>
    public class LifeDirectorTests
    {
        readonly GameData _d = TestData.Load();
        LifeDirector New(int seed = 1) => new LifeDirector(_d, new Random(seed));

        [Fact] public void 목표는_곡선_그대로이고_땅의_주인과_싸우는_동안_0점4배()
        {
            var ld = New();
            Assert.Equal(400, ld.TargetAt(18), 3);
            Assert.Equal(400, ld.TargetAt(35), 3);
            ld.BossFight = true;
            Assert.Equal(400 * _d.Cheonha.Bosses.SpawnTargetScale, ld.TargetAt(18), 3);
            Assert.Equal(160, ld.TargetAt(18), 3);
        }

        [Fact] public void 부족분의_30퍼센트를_만들고_목표를_넘으면_안_만든다()
        {
            var ld = New();
            Assert.Equal(5, ld.Second(0, 1, "songshan", 0).Spawns.Count); // 15 x 0.3 = 4.5 → 5
            Assert.Empty(ld.Second(10, 30, "songshan", 500).Spawns);
        }

        [Theory]
        [InlineData(1, 0, 1)]
        [InlineData(10, 1, 4)]
        [InlineData(35, 3, 16)]
        [InlineData(50, 4, 28)]
        [InlineData(90, 6, -1)]
        public void 덩이_크기는_그_순간_경지(int level, int realm, int members)
        {
            var s = New().Second(5, level, "songshan", 0).Spawns;
            Assert.NotEmpty(s);
            Assert.All(s, x => Assert.Equal(realm, x.BornRealm));
            int expect = members > 0 ? members : new BloodRain.Core.World.Scale(_d).ClusterSize(realm);
            Assert.All(s, x => Assert.Equal(expect, x.Members));
        }

        [Fact] public void 돌파_연출_1점4초_동안은_안_만들고_사건은_그대로_나온다()
        {
            var ld = New();
            ld.OnBreakthrough();
            Assert.Equal(_d.Cheonha.Breakthrough.SpawnHoldSeconds, ld.HoldRemaining, 3);
            var s = ld.Second(3.33f, 10, "songshan", 0);
            Assert.True(s.Held);
            Assert.Empty(s.Spawns);
            ld.Tick(1.0f);
            Assert.True(ld.Second(3.35f, 10, "songshan", 0).Held);
            ld.Tick(0.4f);
            var after = ld.Second(3.37f, 10, "songshan", 0);
            Assert.False(after.Held);
            Assert.NotEmpty(after.Spawns);
        }

        [Fact] public void 각성_멈춤은_더_긴_쪽을_지킨다()
        {
            var ld = New();
            ld.Hold(_d.Balance.Awakening.SpawnPauseSeconds);
            ld.OnBreakthrough();
            Assert.Equal(_d.Balance.Awakening.SpawnPauseSeconds, ld.HoldRemaining, 3);
        }

        [Fact] public void 숭산에선_고유_적이_섞이고_고유_적_없는_땅에선_안_섞인다()
        {
            var local = _d.Enemies.Where(e => e.Chapter == "chapter1").Select(e => e.Id).ToHashSet();
            Assert.NotEmpty(local);
            var ld = New(7);
            var seen = new List<string>();
            for (int i = 0; i < 300; i++) seen.AddRange(ld.Second(20, 60, "songshan", 0).Spawns.Select(s => s.Enemy));
            Assert.Contains(seen, local.Contains);

            var plain = _d.Cheonha.Regions.First(r => r.Chapter == null);
            var ld2 = New(7);
            var seen2 = new List<string>();
            for (int i = 0; i < 300; i++) seen2.AddRange(ld2.Second(20, 60, plain.Id, 0).Spawns.Select(s => s.Enemy));
            Assert.DoesNotContain(seen2, local.Contains);
        }

        [Fact] public void 매초_부르면_한_생_사건은_한_번씩만_나온다()
        {
            var ld = New(3);
            var events = new List<LifeEvent>();
            for (int sec = 0; sec <= 30 * 60; sec++) events.AddRange(ld.Second(sec / 60f, 30, "songshan", 1000).Events);
            Assert.Equal(ld.Schedule.Events.Count(e => e.At <= 30), events.Count);
            var elites = events.Where(e => e.Wave.Def.Nth > 0).Select(e => e.Wave.Def.Nth).ToList();
            Assert.Equal(elites.Distinct().Count(), elites.Count);
        }

        [Theory]
        [InlineData(90, 0, 29.9f, 0)]
        [InlineData(90, 0, 30f, 1)]
        [InlineData(90, 0, 34.9f, 1)]
        [InlineData(90, 0, 35f, 2)]
        [InlineData(90, 0, 45f, 4)]
        [InlineData(90, 7, 40f, 0)]
        [InlineData(89, 0, 40f, 0)]
        public void 혈마의_그림자는_현경에_봉인석이_모자랄_때_30분부터_5분마다(int level, int stones, float minute, int step)
        {
            Assert.Equal(step, New().ShadowStep(minute, level, stones));
        }
    }
}
