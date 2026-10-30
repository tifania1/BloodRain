using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BloodRain.Core.Data;
using BloodRain.Core.Spawning;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>RegionSpawns: 서 있는 지역의 고유 적과 n번째 정예(기획서 6장 '지역별 고유 적'·'정예(5분마다)', 7장).</summary>
    public class RegionSpawnsTests
    {
        readonly GameData _d = TestData.Load();
        RegionSpawns R => new RegionSpawns(_d);
        ChapterDef Life => _d.Chapter(WaveTable.Life);

        static ChapterEventDef EliteEvent(int nth) => new ChapterEventDef { At = 5, Kind = "elite", Nth = nth };

        [Fact] public void 고유_적은_그_지역의_장_이름으로_찾는다()
        {
            Assert.Equal(new[] { "mogin_nahan" }, R.LocalEnemies("songshan"));
            Assert.Equal(new[] { "poison_toad", "golden_centipede", "dogin" }, R.LocalEnemies("danggata"));
            foreach (var none in new[] { "cheongun", "gaebang_hq", "namgung_manor", "bongmagok" })
                Assert.Empty(R.LocalEnemies(none));
        }

        [Fact] public void 고유_적은_모두_어느_지역엔가_속한다()
        {
            var r = R;
            foreach (var e in _d.Enemies.Where(x => x.Chapter != null))
                Assert.Contains(_d.Cheonha.Regions, g => r.LocalEnemies(g.Id).Contains(e.Id));
        }

        [Fact] public void 숭산_정예는_동인_계율원_승려_방장_대리이고_넷째부터_셋째가_다시()
        {
            var r = R;
            Assert.Equal(new[] { "dongin", "gyeyul_monk", "acting_abbot", "acting_abbot", "acting_abbot" },
                Enumerable.Range(1, 5).Select(n => r.Elite("songshan", n)));
        }

        [Theory]
        [InlineData("cheongun")] [InlineData("gaebang_hq")] [InlineData("namgung_manor")]
        public void 정예가_없는_땅은_숭산_표를_쓴다(string region)
        {
            Assert.Equal(R.EliteTable("songshan"), R.EliteTable(region));
        }

        [Fact] public void 땅의_주인이_있는_지역은_제_정예를_쓰고_봉마곡엔_정예가_없다()
        {
            var r = R;
            foreach (var g in _d.Cheonha.BossRegions)
            {
                Assert.Equal(_d.Elites[g.Chapter!], r.EliteTable(g.Id));
                Assert.Equal(3, r.EliteTable(g.Id).Count);
            }
            Assert.False(r.HasElites("bongmagok"));
            Assert.Throws<InvalidOperationException>(() => r.Elite("bongmagok", 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => r.Elite("songshan", 0));
            Assert.Throws<KeyNotFoundException>(() => r.LocalEnemies("nowhere"));
        }

        [Fact] public void 사건의_적을_서_있는_지역에_맞춘다()
        {
            var r = R;
            var rng = new Random(1);
            Assert.Equal("dongin", r.EnemyFor(EliteEvent(1), "songshan", rng));
            Assert.Equal("dongin", r.EnemyFor(EliteEvent(1), "namgung_manor", rng));
            Assert.Equal("dogin_elder", r.EnemyFor(EliteEvent(5), "danggata", rng));

            var group = Life.Events.Single(e => e.Type == WaveEventKind.Group);
            Assert.Equal("mogin_nahan", r.EnemyFor(group, "songshan", rng));
            Assert.Equal("hyeol_gangsi", r.EnemyFor(group, "gaebang_hq", rng)); // 고유 적이 없는 땅
            var seen = new HashSet<string>();
            for (int seed = 0; seed < 60; seed++) seen.Add(r.EnemyFor(group, "danggata", new Random(seed)));
            Assert.Equal(R.LocalEnemies("danggata").OrderBy(x => x), seen.OrderBy(x => x));

            var charge = Life.Events.First(e => e.Type == WaveEventKind.Charge);
            Assert.Equal("hyeolin", r.EnemyFor(charge, "danggata", rng));
            var axeman = _d.Chapter(WaveTable.Prologue).Events.Single(e => e.Type == WaveEventKind.Elite);
            Assert.Equal("gangsi_axeman", r.EnemyFor(axeman, "cheongun", rng)); // 서장 정예는 정해져 있다
        }

        [Fact] public void 한_생_일정의_정예는_어느_지역에서든_정해진다()
        {
            var r = R;
            var schedule = WaveTable.From(_d).Schedule(WaveTable.Life, new Random(2));
            foreach (var region in _d.Cheonha.Regions.Where(g => !g.Final))
                foreach (var e in schedule.Events.Where(x => x.Kind == WaveEventKind.Elite))
                    Assert.Contains(r.EnemyFor(e, region.Id, new Random(0)), r.EliteTable(region.Id));
        }

        // ---- 기획서 6장 ----

        [Fact] public void 정예_표와_규칙이_기획서_6장과_같다()
        {
            var sec = TestData.GddSection("정예(5분마다)");
            var r = R;
            int rows = 0;
            foreach (var row in TestData.GddTable(sec))
            {
                string name = Regex.Replace(row[0], @"\(.*\)", "").Trim();
                var region = _d.Cheonha.Regions.Single(g => g.NameKo == name);
                int cells = row.Skip(1).Count(c => c != "-");
                Assert.True(_d.Elites.TryGetValue(region.Chapter!, out var table), $"{name}: enemies.json elites 에 {region.Chapter} 가 없음");
                Assert.Equal(cells, table!.Count);
                rows++;
            }
            Assert.Equal(8, rows); // 청운진(서장) + 땅의 주인이 있는 일곱

            var times = Regex.Match(sec, @"정예는 ([\d·]+)분에 나오고");
            Assert.True(times.Success, "6장 정예 시각 문장 모양이 바뀜");
            Assert.Equal(times.Groups[1].Value.Split('·').Select(s => float.Parse(s)),
                Life.Events.Where(e => e.Type == WaveEventKind.Elite).Select(e => e.At));

            Assert.Contains("넷째·다섯째는 셋째가 다시 나옴", sec);
            foreach (var g in _d.Cheonha.BossRegions)
                Assert.Equal(r.Elite(g.Id, 3), r.Elite(g.Id, 5));

            var fb = Regex.Match(sec, @"지역 정예가 없는 땅\(([^)]+)\)에서는 (\S+) 표를 씁니다");
            Assert.True(fb.Success, "6장 정예 대신 쓰는 땅 문장 모양이 바뀜");
            var names = fb.Groups[1].Value.Split('·');
            Assert.Equal(names.Select(n => _d.Cheonha.Regions.Single(g => g.NameKo == n).Id).OrderBy(x => x), Life.EliteFallback!.Regions.OrderBy(x => x));
            Assert.Equal(fb.Groups[2].Value, _d.Cheonha.Region(Life.EliteFallback.Region).NameKo);
        }

        [Fact] public void 데이터에_있는_고유_적은_기획서_6장_지역별_고유_적_표의_그_지역에_있다()
        {
            var rows = TestData.GddTable(TestData.GddSection("지역별 고유 적"));
            foreach (var e in _d.Enemies.Where(x => x.Chapter != null))
            {
                var region = _d.Cheonha.Regions.Single(g => g.Chapter == e.Chapter);
                Assert.Contains(rows, row => row[0] == region.NameKo && row[1].StartsWith(e.NameKo));
            }
            // 목인나한 기혈 120(6장 행동 칸)
            var mogin = rows.Single(row => row[1].StartsWith("목인나한"));
            Assert.Contains($"기혈 {TestData.Num(_d.Enemy("mogin_nahan").Health)}", mogin[2]);
        }
    }
}
