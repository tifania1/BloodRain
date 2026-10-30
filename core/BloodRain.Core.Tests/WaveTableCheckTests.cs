using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Spawning;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>WaveTable 이 chapters.json 을 읽을 때 잘못 적힌 흐름을 막는지. 지금 데이터는 다 맞아서 검사 하나하나가 실제로 터지는지는
    /// 따로 본 적이 없었다. 작은 흐름을 손으로 만들고 한 군데씩 망가뜨려 본다.</summary>
    public class WaveTableCheckTests
    {
        /// <summary>검사를 통과하는 가장 작은 흐름: 10분, 시간대 둘, 정예·상점·보스, 8분부터 생성 중지.</summary>
        static ChapterDef Valid() => new ChapterDef
        {
            Id = "t",
            DurationMinutes = 10,
            StopSpawningAtMinute = 8,
            Bands = new List<SpawnBandDef>
            {
                new SpawnBandDef { FromMinute = 0, ToMinute = 5, Spawns = { new SpawnWeightDef { Enemy = "a", Weight = 1 } } },
                new SpawnBandDef { FromMinute = 5, ToMinute = 10, Spawns = { new SpawnWeightDef { Enemy = "a", Weight = 1 }, new SpawnWeightDef { Enemy = "b", Weight = 3 } } },
            },
            Events = new List<ChapterEventDef>
            {
                new ChapterEventDef { Kind = "elite", At = 3, Enemy = "e" },
                new ChapterEventDef { Kind = "merchant", At = 4 },
                new ChapterEventDef { Kind = "toad", Window = new[] { 2f, 6f }, Enemy = "toad", Chance = 0.5f },
                new ChapterEventDef { Kind = "boss", At = 9, Enemy = "boss" },
            },
            LocalShare = new List<LocalShareDef> { new LocalShareDef { FromMinute = 0, Share = 0 }, new LocalShareDef { FromMinute = 5, Share = 0.3f } },
        };

        static void Bad(Action<ChapterDef> breakIt, string expect)
        {
            var c = Valid();
            breakIt(c);
            var ex = Assert.Throws<InvalidOperationException>(() => new WaveTable(new[] { c }));
            Assert.Contains(expect, ex.Message);
        }

        [Fact]
        public void 손으로_만든_흐름은_통과한다()
        {
            var t = new WaveTable(new[] { Valid() });
            Assert.Equal(new[] { "t" }, t.ChapterIds);
            Assert.Equal(9f, t.Chapter("t").BossAtMinute);
        }

        [Fact]
        public void 같은_id_가_두_번이면_막는다()
        {
            Assert.Throws<ArgumentException>(() => new WaveTable(new[] { Valid(), Valid() }));
        }

        [Fact]
        public void 없는_흐름을_찾으면_KeyNotFound()
        {
            var t = new WaveTable(new[] { Valid() });
            Assert.Throws<KeyNotFoundException>(() => t.Chapter("nope"));
            Assert.Throws<KeyNotFoundException>(() => t.BandAt("nope", 1));
        }

        // ---- 판 길이와 시간대 ----

        [Fact] public void 판_길이가_0이면() => Bad(c => c.DurationMinutes = 0, "durationMinutes");
        [Fact] public void 시간대가_없으면() => Bad(c => c.Bands.Clear(), "bands");
        [Fact] public void 첫_시간대가_0분이_아니면() => Bad(c => c.Bands[0].FromMinute = 1, "0분에서 시작");
        [Fact] public void 시간대_길이가_0이면() => Bad(c => c.Bands[0].ToMinute = 0, "길이가 0 이하");
        [Fact] public void 시간대_사이가_비면() => Bad(c => c.Bands[1].FromMinute = 6, "이어지지 않음");
        [Fact] public void 가중치가_비면() => Bad(c => c.Bands[1].Spawns.Clear(), "가중치");
        [Fact] public void 가중치가_0이면() => Bad(c => c.Bands[1].Spawns[1].Weight = 0, "가중치");
        [Fact] public void 시간대가_판_끝까지_안_닿으면() => Bad(c => c.DurationMinutes = 11, "판 길이까지");

        // ---- 사건 ----

        [Fact] public void 모르는_사건_종류() => Bad(c => c.Events[1].Kind = "festival", "모르는 사건");
        [Fact] public void 창이_거꾸로면() => Bad(c => c.Events[2].Window = new[] { 6f, 2f }, "창(window)");
        [Fact] public void 창_값이_하나면() => Bad(c => c.Events[2].Window = new[] { 2f }, "창(window)");
        [Fact] public void 확률이_0이면() => Bad(c => c.Events[2].Chance = 0, "chance");
        [Fact] public void 확률이_1을_넘으면() => Bad(c => c.Events[2].Chance = 1.01f, "chance");
        [Fact] public void 마리_수가_0이면() => Bad(c => c.Events[2].Count = 0, "count");
        [Fact] public void nth_가_음수면() => Bad(c => { c.Events[0].Enemy = ""; c.Events[0].Nth = -1; }, "nth");
        [Fact] public void nth_는_정예에만() => Bad(c => c.Events[2].Nth = 1, "정예 사건에만");
        [Fact] public void 정예에_적과_nth_둘_다면() => Bad(c => c.Events[0].Nth = 1, "하나만");
        [Fact] public void 정예에_적도_nth_도_없으면() => Bad(c => c.Events[0].Enemy = "", "하나만");
        [Fact] public void local_은_무리에만() => Bad(c => c.Events[2].Local = true, "무리 사건에만");
        [Fact] public void 적을_만드는_사건에_적이_없으면() => Bad(c => c.Events[2].Enemy = "", "enemy 가 없음");

        [Fact]
        public void 고유_무리에_대신_나올_적이_없으면_그렇게_말한다()
        {
            Bad(c => c.Events.Add(new ChapterEventDef { Kind = "group", At = 6, Count = 20, Local = true }), "고유 적이 없는 땅");
        }

        [Fact]
        public void 상점과_등롱은_적이_없어도_된다()
        {
            var c = Valid();
            c.Events.Add(new ChapterEventDef { Kind = "silver_lantern", At = 7, Chance = 0.1f });
            new WaveTable(new[] { c });
        }

        // ---- 보스와 생성 중지 ----

        [Fact] public void 보스가_둘이면() => Bad(c => c.Events.Add(new ChapterEventDef { Kind = "boss", At = 9.5f, Enemy = "boss2" }), "많아야 하나");
        [Fact] public void 보스를_창으로_적으면() => Bad(c => c.Events[3].Window = new[] { 8.5f, 9.5f }, "많아야 하나");
        [Fact] public void 보스_없이_생성_중지만_있으면() => Bad(c => c.Events.RemoveAt(3), "보스 사건이 있어야");
        [Fact] public void 생성_중지가_보스보다_늦으면() => Bad(c => c.StopSpawningAtMinute = 9.5f, "보스보다 늦음");

        [Fact]
        public void 생성_중지가_보스와_같은_분이면_통과()
        {
            var c = Valid();
            c.StopSpawningAtMinute = 9;
            new WaveTable(new[] { c });
        }

        [Fact]
        public void 생성_중지가_없으면_보스가_없어도_된다()
        {
            var c = Valid();
            c.StopSpawningAtMinute = null;
            c.Events.RemoveAt(3);
            new WaveTable(new[] { c });
        }

        // ---- 고유 적 몫과 정예 대신 지역 ----

        [Fact] public void 몫이_음수면() => Bad(c => c.LocalShare[1].Share = -0.1f, "0 이상 1 미만");
        [Fact] public void 몫이_1이면() => Bad(c => c.LocalShare[1].Share = 1f, "0 이상 1 미만");
        [Fact] public void 몫_시각이_거꾸로면() => Bad(c => c.LocalShare[1].FromMinute = 0, "시각 순서");
        [Fact] public void 정예_대신_지역이_비면() => Bad(c => c.EliteFallback = new EliteFallbackDef(), "eliteFallback.region");

        // ---- 진짜 데이터에 대고 id 검사(CheckIds). 캐시를 건드리지 않게 새로 읽는다 ----

        static GameData Fresh() => GameData.FromJson(TestData.ReadFiles());

        static void BadIds(Action<GameData> breakIt, string expect)
        {
            var d = Fresh();
            breakIt(d);
            var ex = Assert.Throws<InvalidOperationException>(() => WaveTable.From(d));
            Assert.Contains(expect, ex.Message);
        }

        [Fact]
        public void 새로_읽은_데이터는_통과한다() => WaveTable.From(Fresh());

        [Fact]
        public void 가중치표에_없는_적()
            => BadIds(d => d.Chapter(WaveTable.Life).Bands[0].Spawns[0].Enemy = "no_such_enemy", "없는 적 no_such_enemy");

        [Fact]
        public void 가중치표에_지역_고유_적을_넣으면()
        {
            BadIds(d =>
            {
                var local = d.Enemies.First(e => e.Chapter != null);
                d.Chapter(WaveTable.Life).Bands[0].Spawns[0].Enemy = local.Id;
            }, "localShare 로 섞는다");
        }

        [Fact]
        public void 서장_지역이_천하에_없으면()
            => BadIds(d => d.Chapter(WaveTable.Prologue).Region = "atlantis", "없는 지역 atlantis");

        [Fact]
        public void 서장_보스가_다른_장_보스면()
        {
            BadIds(d =>
            {
                var pro = d.Chapter(WaveTable.Prologue);
                var other = d.Bosses.First(b => b.Id != pro.Boss);
                pro.BossEvent!.Enemy = other.Id;
            }, "boss 사건");
        }

        [Fact]
        public void 상점에_적이_적혀_있으면()
        {
            BadIds(d =>
            {
                var m = d.Chapter(WaveTable.Life).Events.First(e => e.Type == WaveEventKind.Merchant);
                m.Enemy = d.Enemies[0].Id;
            }, "merchant 사건");
        }

        [Fact]
        public void 고유_무리의_대신_적이_고유_적이면()
        {
            BadIds(d =>
            {
                var g = d.Chapter(WaveTable.Life).Events.First(e => e.Type == WaveEventKind.Group && e.Local);
                g.Enemy = d.Enemies.First(e => e.Chapter != null).Id;
            }, "group 사건");
        }

        [Fact]
        public void 정예_대신_지역이_천하에_없으면()
        {
            BadIds(d =>
            {
                var c = d.Chapters.First(x => x.EliteFallback != null);
                c.EliteFallback!.Regions.Add("atlantis");
            }, "없는 지역 atlantis");
        }

        // ---- 일정 경계 ----

        [Fact]
        public void Between_은_앞은_빼고_뒤는_넣는다()
        {
            var s = new WaveTable(new[] { Valid() }).Schedule("t", new Random(0));
            Assert.DoesNotContain(s.Between(3f, 3.5f), e => e.Kind == WaveEventKind.Elite);
            Assert.Contains(s.Between(2.5f, 3f), e => e.Kind == WaveEventKind.Elite);
            Assert.Empty(s.Between(9f, 9f));
            Assert.Empty(s.Between(5f, 4f)); // 거꾸로 넣으면 아무것도 없다
        }

        [Fact]
        public void BandAt_은_0분_전이면_첫_표_끝_뒤면_마지막_표()
        {
            var t = new WaveTable(new[] { Valid() });
            var c = t.Chapter("t");
            Assert.Same(c.Bands[0], t.BandAt("t", -1));
            Assert.Same(c.Bands[1], t.BandAt("t", 5));   // 경계는 다음 표(ToMinute 는 안 들어감)
            Assert.Same(c.Bands[1], t.BandAt("t", 99));
        }

        [Fact]
        public void 확률_사건은_시드마다_들어가거나_빠지고_들어가면_창_안이다()
        {
            var t = new WaveTable(new[] { Valid() });
            int with = 0;
            for (int seed = 0; seed < 400; seed++)
            {
                var toads = t.Schedule("t", new Random(seed)).Events.Where(e => e.Kind == WaveEventKind.Toad).ToList();
                Assert.True(toads.Count <= 1);
                foreach (var e in toads) Assert.InRange(e.At, 2f, 6f);
                with += toads.Count;
            }
            Assert.InRange(with, 160, 240); // chance 0.5
        }

        [Fact]
        public void FirstAppearance_는_표와_사건_가운데_이른_쪽()
        {
            var t = new WaveTable(new[] { Valid() });
            Assert.Equal(0f, t.FirstAppearance("t", "a"));
            Assert.Equal(5f, t.FirstAppearance("t", "b"));
            Assert.Equal(2f, t.FirstAppearance("t", "toad")); // 창 시작
            Assert.Equal(3f, t.FirstAppearance("t", "e"));
            Assert.Null(t.FirstAppearance("t", "zzz"));
        }
    }
}
