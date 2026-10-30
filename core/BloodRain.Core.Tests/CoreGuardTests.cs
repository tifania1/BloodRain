using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using BloodRain.Core.Spawning;
using BloodRain.Core.World;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>데이터가 잘못 들어왔을 때 터뜨리려고 만들어 둔 검사 가운데, 지금 데이터가 맞아서 한 번도 안 돌던 것들(커버리지로 찾음).
    /// 검사가 실제로 알아들을 말로 터지는지만 본다.</summary>
    public class CoreGuardTests
    {
        readonly GameData _d = TestData.Load();

        static GameData Fresh() => GameData.FromJson(TestData.ReadFiles());

        [Fact]
        public void 데이터_파일이_빠지면_어느_파일인지_말한다()
        {
            var files = TestData.ReadFiles();
            files.Remove("gimmicks");
            var ex = Assert.Throws<InvalidOperationException>(() => GameData.FromJson(files));
            Assert.Contains("gimmicks.json", ex.Message);
        }

        [Fact]
        public void 깨달음_id_가_겹치면_막는다()
        {
            var twice = new List<InsightDef> { _d.Insights[0], _d.Insights[0] };
            Assert.Throws<ArgumentException>(() => new Insights(twice, _d.InsightChoices));
        }

        [Fact]
        public void 없는_깨달음을_찾으면_id_를_말한다()
        {
            var ins = Insights.From(_d);
            Assert.Same(_d.Insights[0], ins.Get(_d.Insights[0].Id));
            var ex = Assert.Throws<KeyNotFoundException>(() => ins.Get("no_such_insight"));
            Assert.Contains("no_such_insight", ex.Message);
        }

        [Fact]
        public void 판_기록_이름은_모두_읽히고_모르는_이름은_막는다()
        {
            var c = new RunCounters { TalismansUsed = 1, FoodEaten = 2, MedicalBooks = 3, BestBacklashCutStreak = 4, ElapsedSeconds = 5, GaugeZeroSeconds = 6 };
            Assert.Equal(new double[] { 1, 2, 3, 4, 5, 6 }, RunCounters.Names.Select(c.Get));
            Assert.Throws<ArgumentException>(() => c.Get("killCount"));
        }

        [Fact]
        public void 판_사건_이름은_셋이고_모르는_이름은_막는다()
        {
            Assert.Equal(RunEventKind.Talisman, RunCounters.ParseEvent("talisman"));
            Assert.Equal(RunEventKind.Chest, RunCounters.ParseEvent("chest"));
            Assert.Equal(RunEventKind.SilverLantern, RunCounters.ParseEvent("silverLantern"));
            Assert.Throws<ArgumentException>(() => RunCounters.ParseEvent("silver_lantern")); // chapters.json 표기와 다르다
        }

        [Fact]
        public void 정예_대신_지역이_천하에_없으면_RegionSpawns_가_막는다()
        {
            var d = Fresh();
            d.Chapters.First(c => c.Id == WaveTable.Life).EliteFallback!.Region = "atlantis";
            var ex = Assert.Throws<InvalidOperationException>(() => new RegionSpawns(d));
            Assert.Contains("atlantis", ex.Message);
        }

        [Fact]
        public void 없는_흐름으로_RegionSpawns_를_만들면_KeyNotFound()
        {
            Assert.Throws<KeyNotFoundException>(() => new RegionSpawns(_d, "nope"));
        }

        [Fact]
        public void 보스_패턴_간격이_0이면_막는다()
        {
            var src = _d.BossPatterns.Values.First();
            var zero = new BossPatternSetDef
            {
                FirstCastSeconds = src.FirstCastSeconds, IntervalSeconds = 0, Phase2IntervalSeconds = src.Phase2IntervalSeconds,
                Phase2At = src.Phase2At, Order = src.Order.ToList(), Patterns = src.Patterns, Phase2 = src.Phase2,
            };
            Assert.Throws<InvalidOperationException>(() => BossPatterns.Plan(zero, 60));
            zero.IntervalSeconds = src.IntervalSeconds;
            zero.Phase2IntervalSeconds = -1;
            Assert.Throws<InvalidOperationException>(() => new BossPatterns(zero));
        }

        [Fact]
        public void 부서지는_것이_모르는_것을_떨구면_아무것도_안_준다()
        {
            var def = new BreakableGimmickDef { Drop = "jade", DropChance = 1, HealMaxHealthRatio = 0.5f, SilverMin = 3, SilverMax = 5 };
            var rng = new Random(1);
            for (int i = 0; i < 50; i++) Assert.Equal((0f, 0), new BreakableGimmick(def).RollDrop(rng));
        }

        [Fact]
        public void 데이터의_부서지는_것은_음식_아니면_은자만_떨군다()
        {
            foreach (var b in _d.Gimmicks.All.OfType<BreakableGimmickDef>())
                Assert.Contains(b.Drop, new[] { "food", "silver" });
        }
    }
}
