using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BloodRain.Core.Data;
using BloodRain.Core.Spawning;
using Newtonsoft.Json.Linq;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>chapters.json 이 적 데이터·지도·기획서 표(서장 흐름, 1장 시간대별 흐름, 공통 적 첫 등장)와 맞는지.</summary>
    public class ChapterDataTests
    {
        readonly GameData _d = TestData.Load();

        static readonly string[] ChapterOrder = { "prologue", "chapter1", "chapter2", "chapter3", "chapter4", "chapter5", "chapter6", "chapter7", "final" };

        IEnumerable<ChapterDef> All => _d.Chapters;

        EnemyDef? EnemyByName(string nameKo) => _d.Enemies.FirstOrDefault(e => e.NameKo == nameKo);

        /// <summary>"0~3분" → (0, 3), "5분" → (5, 5). 시간이 아닌 칸은 null.</summary>
        static (float from, float to)? Minutes(string cell)
        {
            var m = Regex.Match(cell, @"^(\d+(?:\.\d+)?)(?:~(\d+(?:\.\d+)?))?분$");
            if (!m.Success) return null;
            float a = float.Parse(m.Groups[1].Value);
            return (a, m.Groups[2].Success ? float.Parse(m.Groups[2].Value) : a);
        }

        /// <summary>"3:30" → 3.5</summary>
        static float? Clock(string cell)
        {
            var m = Regex.Match(cell, @"^(\d+):(\d\d)$");
            return m.Success ? int.Parse(m.Groups[1].Value) + int.Parse(m.Groups[2].Value) / 60f : (float?)null;
        }

        // ---- 데이터끼리 ----

        [Fact] public void 장은_서장과_1장이고_지도의_지역과_맞는다()
        {
            Assert.Equal(new[] { "prologue", "chapter1" }, All.Select(c => c.Id));
            var map = JObject.Parse(File.ReadAllText(Path.Combine(TestData.DataDir, "worldmap.json")));
            foreach (var c in All)
            {
                var region = map["regions"]!.Children<JObject>().Single(r => (string)r["id"]! == c.Region);
                Assert.Equal(c.Id, (string)region["chapter"]!);
                Assert.Contains((string)region["nameKo"]!, c.NameKo);
                Assert.False(string.IsNullOrWhiteSpace(c.TitleKo));
                Assert.True(_d.Balance.EnemyScaling.ChapterMultiplier.ContainsKey(c.Id), $"{c.Id} 장 배율 없음");
            }
        }

        [Fact] public void 판_길이는_서장_10분_1장_20분()
        {
            Assert.Equal(10f, _d.Chapter("prologue").DurationMinutes);
            Assert.Equal(20f, _d.Chapter("chapter1").DurationMinutes);
        }

        [Fact] public void 시간대마다_가중치_합은_1이다()
        {
            foreach (var c in All)
                foreach (var b in c.Bands)
                    Assert.True(Math.Abs(b.TotalWeight - 1f) < 1e-4, $"{c.Id} {b.FromMinute}분 시간대 합 {b.TotalWeight}");
        }

        [Fact] public void 시간대는_0분부터_판_끝까지_빈틈없이_이어진다()
        {
            foreach (var c in All)
            {
                Assert.Equal(0f, c.Bands[0].FromMinute);
                for (int i = 1; i < c.Bands.Count; i++) Assert.Equal(c.Bands[i - 1].ToMinute, c.Bands[i].FromMinute);
                Assert.Equal(c.DurationMinutes, c.Bands[^1].ToMinute);
                foreach (var b in c.Bands)
                    Assert.Equal(b.Spawns.Count, b.Spawns.Select(s => s.Enemy).Distinct().Count());
            }
        }

        [Fact] public void 가중치표와_사건의_적은_모두_enemies에_있다()
        {
            foreach (var c in All)
            {
                foreach (var s in c.Bands.SelectMany(b => b.Spawns))
                    Assert.True(_d.Enemies.Any(e => e.Id == s.Enemy), $"{c.Id}: 없는 적 {s.Enemy}");
                foreach (var e in c.Events)
                {
                    Assert.NotEqual(WaveEventKind.Unknown, e.Type);
                    switch (e.Type)
                    {
                        case WaveEventKind.Elite: Assert.Contains(e.Enemy, _d.Elites[c.Id]); break;
                        case WaveEventKind.Boss: Assert.Contains(_d.Bosses, b => b.Id == e.Enemy && b.Chapter == c.Id); break;
                        case WaveEventKind.Merchant:
                        case WaveEventKind.SilverLantern: Assert.Equal("", e.Enemy); break;
                        default: Assert.True(_d.Enemies.Any(x => x.Id == e.Enemy), $"{c.Id} {e.Kind}: 없는 적 {e.Enemy}"); break;
                    }
                }
            }
            WaveTable.From(_d); // 읽을 때 같은 검사를 한 번 더 한다
        }

        [Fact] public void 사건_종류에_맞는_적이_나온다()
        {
            foreach (var e in All.SelectMany(c => c.Events))
            {
                if (e.Type == WaveEventKind.Charge) Assert.Equal("line_charge", _d.Enemy(e.Enemy).Behavior);
                if (e.Type == WaveEventKind.Toad) Assert.Equal("event", _d.Enemy(e.Enemy).Family);
                if (e.Type == WaveEventKind.Charge || e.Type == WaveEventKind.Ring || e.Type == WaveEventKind.Group) Assert.True(e.Count > 1);
                else Assert.Equal(1, e.Count);
            }
        }

        [Fact] public void 장_고유_적은_자기_장에서만_공통_적은_처음_나오는_장부터()
        {
            foreach (var c in All)
            {
                int here = Array.IndexOf(ChapterOrder, c.Id);
                var used = c.Bands.SelectMany(b => b.Spawns.Select(s => s.Enemy))
                    .Concat(c.Events.Where(e => e.Type != WaveEventKind.Elite && e.Type != WaveEventKind.Boss && e.Enemy != "").Select(e => e.Enemy))
                    .Distinct();
                foreach (var id in used)
                {
                    var e = _d.Enemy(id);
                    if (e.Chapter != null) Assert.Equal(c.Id, e.Chapter);
                    else if (e.FirstSeen != "any") Assert.True(Array.IndexOf(ChapterOrder, e.FirstSeen) <= here, $"{e.NameKo} 는 {e.FirstSeen} 부터인데 {c.Id} 에 나옴");
                }
            }
        }

        [Fact] public void 사건은_시각_순서이고_판_길이_안에_있다()
        {
            foreach (var c in All)
            {
                float prev = 0;
                foreach (var e in c.Events)
                {
                    Assert.True(e.EarliestMinute > 0, $"{c.Id} {e.Kind} 가 0분");
                    Assert.True(e.EarliestMinute >= prev, $"{c.Id} {e.Kind} {e.EarliestMinute}분이 앞 사건보다 이름");
                    Assert.True(e.LatestMinute <= c.DurationMinutes, $"{c.Id} {e.Kind} 가 판 길이 밖");
                    Assert.InRange(e.Chance, 0.01f, 1f);
                    prev = e.EarliestMinute;
                }
                Assert.True(c.StopSpawningAtMinute < c.BossAtMinute);
                Assert.True(c.BossAtMinute <= c.DurationMinutes);
            }
        }

        [Fact] public void 정예는_enemies_정예_순서대로_5분_10분_15분()
        {
            var minutes = new[] { 5f, 10f, 15f };
            foreach (var c in All)
            {
                var elites = c.Events.Where(e => e.Type == WaveEventKind.Elite).ToList();
                Assert.Equal(_d.Elites[c.Id], elites.Select(e => e.Enemy));
                Assert.Equal(minutes.Take(elites.Count), elites.Select(e => e.At));
            }
            Assert.Equal(3, _d.Elites["chapter1"].Count);
        }

        [Fact] public void 보스는_서장_9분_장삼_1장_20분_원각()
        {
            Assert.Equal("jang_sam", _d.Chapter("prologue").Boss);
            Assert.Equal(9f, _d.Chapter("prologue").BossAtMinute);
            Assert.Equal("wongak", _d.Chapter("chapter1").Boss);
            Assert.Equal(20f, _d.Chapter("chapter1").BossAtMinute);
        }

        [Fact] public void 장1_생성_중지와_보스_시각은_balance와_같다()
        {
            var c = _d.Chapter("chapter1");
            Assert.Equal(_d.Balance.SpawnTarget.StopSpawningAtMinute, c.StopSpawningAtMinute);
            Assert.Equal(_d.Balance.SpawnTarget.BossAtMinute, c.BossAtMinute);
        }

        [Fact] public void 맵_형태는_서장_담장_안_60x60_1장_무한_반복()
        {
            var p = _d.Chapter("prologue").Map;
            Assert.Equal("bounded", p.Type);
            Assert.Equal(60f, p.WidthMeters);
            Assert.Equal(60f, p.HeightMeters);
            var c1 = _d.Chapter("chapter1").Map;
            Assert.Equal("endless", c1.Type);
            Assert.Null(c1.WidthMeters);
            Assert.Contains("bell", c1.Gimmicks);
        }

        // ---- 기획서 표 ----

        [Fact] public void 장1_사건_시각이_기획서_시간대별_흐름과_같다()
        {
            var c = _d.Chapter("chapter1");
            var rows = TestData.GddTable(TestData.GddSection("1장 시간대별 흐름"));
            Assert.Equal(14, rows.Count);
            int checkedRows = 0;
            foreach (var row in rows)
            {
                var t = Minutes(row[0]);
                if (t == null) continue;
                var (a, b) = t.Value;
                string ev = row[2];
                checkedRows++;
                if (a == b)
                {
                    if (ev.Contains("정예")) Assert.Contains(c.Events, e => e.Type == WaveEventKind.Elite && e.At == a);
                    if (ev.Contains("보부상")) Assert.Contains(c.Events, e => e.Type == WaveEventKind.Merchant && e.At == a);
                    if (ev.Contains("생성 멈춤")) Assert.Equal(a, c.StopSpawningAtMinute);
                    if (ev.Contains("보스"))
                    {
                        Assert.Equal(a, c.BossAtMinute);
                        Assert.Contains(_d.Bosses.Single(x => x.Id == c.Boss).NameKo, ev);
                    }
                    continue;
                }
                // 구간 [a, b): 돌진 횟수가 기획서와 같아야 한다(언급이 없으면 0회)
                var charge = Regex.Match(ev, @"돌진 (\d+)회");
                int charges = c.Events.Count(e => e.Type == WaveEventKind.Charge && e.At >= a && e.At < b);
                Assert.True((charge.Success ? int.Parse(charge.Groups[1].Value) : 0) == charges, $"{row[0]} 돌진 {charges}회");
                if (ev.Contains("금두꺼비"))
                {
                    var toad = c.Events.Single(e => e.Type == WaveEventKind.Toad);
                    Assert.Equal(new[] { a, b }, toad.Window);
                }
                if (ev.Contains("포위"))
                    Assert.Contains(c.Events, e => e.Type == WaveEventKind.Ring && e.At > a && e.At < b && ev.Contains(_d.Enemy(e.Enemy).NameKo));
                if (ev.Contains("목인나한"))
                    Assert.Contains(c.Events, e => e.Type == WaveEventKind.Group && e.Enemy == "mogin_nahan" && e.At >= a && e.At < b);
                if (ev.Contains("은빛 등롱"))
                {
                    var lanterns = c.Events.Where(e => e.Type == WaveEventKind.SilverLantern).ToList();
                    Assert.NotEmpty(lanterns);
                    Assert.All(lanterns, l => Assert.True(l.EarliestMinute >= a && l.LatestMinute <= b));
                }
            }
            Assert.Equal(13, checkedRows); // '보스 처치 후' 줄만 시각이 없다
        }

        /// <summary>'주력 적' 칸에 적힌 적이 그 구간 시간대마다 절반 이상을 차지하고, 구간 안 어느 시간대에는 꼭 나온다.</summary>
        [Fact] public void 장1_주력_적이_기획서_시간대별_흐름과_같다()
        {
            var c = _d.Chapter("chapter1");
            int checkedRows = 0;
            foreach (var row in TestData.GddTable(TestData.GddSection("1장 시간대별 흐름")))
            {
                var t = Minutes(row[0]);
                if (t == null || t.Value.from == t.Value.to || row[1] == "") continue;
                var names = row[1].Split('+').Select(n => n.Trim().Replace(" 벽", "")).ToList();
                var ids = names.Select(n => EnemyByName(n)?.Id).ToList();
                if (ids.Any(id => id == null)) continue; // '전 계열 혼합', '최대 밀도' 처럼 적 이름이 아닌 칸
                checkedRows++;
                var (a, b) = t.Value;
                var bands = c.Bands.Where(x => x.FromMinute < b && x.ToMinute > a).ToList();
                Assert.NotEmpty(bands);
                foreach (var band in bands)
                {
                    float share = ids.Sum(id => band.WeightOf(id!));
                    Assert.True(share >= 0.5f, $"{band.FromMinute}~{band.ToMinute}분 주력 적({row[1]}) 비중 {share}");
                }
                foreach (var id in ids) Assert.Contains(bands, x => x.WeightOf(id!) > 0);
            }
            Assert.Equal(5, checkedRows);
        }

        [Fact] public void 서장_사건_시각이_기획서_서장_흐름과_같다()
        {
            var c = _d.Chapter("prologue");
            var rows = TestData.GddTable(TestData.GddSection("서장 상세 흐름"));
            int charges = 0, elites = 0;
            foreach (var row in rows)
            {
                var at = Clock(row[0]);
                Assert.True(at != null, $"서장 표 시간 칸 모양이 다름: {row[0]}");
                string what = row[1];
                if (what.Contains("돌진")) { charges++; Assert.Contains(c.Events, e => e.Type == WaveEventKind.Charge && Math.Abs(e.At - at!.Value) < 1e-4); }
                if (what.Contains("정예")) { elites++; Assert.Contains(c.Events, e => e.Type == WaveEventKind.Elite && e.At == at); }
                if (what.Contains("보스")) Assert.Equal(at, c.BossAtMinute);
                if (what.Contains("시강")) Assert.True(c.Bands.First(b => b.FromMinute <= at && at < b.ToMinute).WeightOf("sigang") > 0);
            }
            Assert.Equal(charges, c.Events.Count(e => e.Type == WaveEventKind.Charge));
            Assert.Equal(elites, c.Events.Count(e => e.Type == WaveEventKind.Elite));
            Assert.DoesNotContain(c.Events, e => e.Type == WaveEventKind.Merchant); // 보부상은 12분이라 10분짜리 서장엔 없다
        }

        [Fact] public void 공통_적이_처음_나오는_시각이_기획서_적_표와_같다()
        {
            var table = new WaveTable(_d.Chapters);
            int checkedRows = 0;
            foreach (var row in TestData.GddTable(TestData.GddSection("공통 적")))
            {
                string name = Regex.Replace(row[0], @"\(.*\)", "").Trim();
                var m = Regex.Match(row[5], @"^(서장|(\d)장) (?:(\d+):(\d\d)|(\d+)분)$");
                if (!m.Success) continue; // '2장', '모든 장 무작위'
                string chapter = m.Groups[1].Value == "서장" ? "prologue" : "chapter" + m.Groups[2].Value;
                float minute = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) + int.Parse(m.Groups[4].Value) / 60f : int.Parse(m.Groups[5].Value);
                var enemy = EnemyByName(name);
                Assert.True(enemy != null, $"기획서 적 {name} 이 enemies.json 에 없음");
                Assert.True(table.FirstAppearance(chapter, enemy!.Id) is float f && Math.Abs(f - minute) < 1e-4,
                    $"{name}: 기획서 {row[5]}, 데이터 {table.FirstAppearance(chapter, enemy.Id)}");
                checkedRows++;
            }
            Assert.Equal(5, checkedRows); // 시강, 혈인, 혈강시, 혈무사, 동갑시
        }
    }

    /// <summary>WaveTable: 분마다 적 종류 추첨, 한 판 사건 일정, 장별 SpawnCurve.</summary>
    public class WaveTableTests
    {
        readonly GameData _d = TestData.Load();
        WaveTable Table => WaveTable.From(_d);

        [Theory]
        [InlineData(-1f, 0)] [InlineData(0f, 0)] [InlineData(2.99f, 0)] [InlineData(3f, 1)]
        [InlineData(5.5f, 2)] [InlineData(6f, 3)] [InlineData(19.9f, 7)] [InlineData(25f, 7)]
        public void 분에_맞는_시간대를_고른다(float minute, int bandIndex)
        {
            var c = _d.Chapter("chapter1");
            Assert.Same(c.Bands[bandIndex], Table.BandAt("chapter1", minute));
        }

        [Fact] public void 처음_3분은_시강만_나온다()
        {
            var t = Table;
            var rng = new Random(1);
            for (int i = 0; i < 500; i++) Assert.Equal("sigang", t.PickEnemy("chapter1", i % 300 / 100f, rng));
        }

        [Theory]
        [InlineData("chapter1", 4f)] [InlineData("chapter1", 7f)] [InlineData("chapter1", 9f)]
        [InlineData("chapter1", 11f)] [InlineData("chapter1", 13f)] [InlineData("chapter1", 18f)]
        public void 고정_시드로_많이_뽑으면_가중치_비율대로_나온다(string chapter, float minute)
        {
            var t = Table;
            var band = t.BandAt(chapter, minute);
            var rng = new Random(20261029);
            const int n = 20000;
            var count = new Dictionary<string, int>();
            for (int i = 0; i < n; i++)
            {
                var id = t.PickEnemy(chapter, minute, rng);
                count[id] = count.GetValueOrDefault(id) + 1;
            }
            Assert.Equal(band.Spawns.Select(s => s.Enemy).OrderBy(x => x), count.Keys.OrderBy(x => x));
            // 표준편차는 많아야 0.0035. 0.015 안이면 가중치표를 그대로 따른 것
            foreach (var s in band.Spawns) Assert.InRange(count[s.Enemy] / (double)n, s.Weight - 0.015, s.Weight + 0.015);
        }

        [Fact] public void 같은_시드면_같은_적_순서()
        {
            var t = Table;
            string Seq(int seed) { var r = new Random(seed); return string.Join(",", Enumerable.Range(0, 50).Select(i => t.PickEnemy("chapter1", 13, r))); }
            Assert.Equal(Seq(9), Seq(9));
            Assert.NotEqual(Seq(9), Seq(10));
        }

        [Fact] public void 일정은_시각_순서이고_프레임마다_넘기면_사건이_한_번씩만_나온다()
        {
            foreach (var chapter in new[] { "prologue", "chapter1" })
            {
                var s = Table.Schedule(chapter, new Random(3));
                Assert.Equal(s.Events.OrderBy(e => e.At).Select(e => e.At), s.Events.Select(e => e.At));
                var seen = new List<WaveEvent>();
                float prev = 0;
                for (int frame = 1; frame <= 22 * 60 * 60; frame++) // 60fps 로 22분
                {
                    float now = frame / 3600f;
                    seen.AddRange(s.Between(prev, now));
                    prev = now;
                }
                Assert.Equal(s.Events.Count, seen.Count);
                Assert.Equal(s.Events.Select(e => e.ToString()), seen.Select(e => e.ToString()));
            }
        }

        [Fact] public void 사건은_지난_분_초과_이번_분_이하에서_나온다()
        {
            var s = Table.Schedule("chapter1", new Random(1));
            Assert.Contains(s.Between(4.9f, 5f), e => e.Kind == WaveEventKind.Elite && e.Enemy == "dongin");
            Assert.DoesNotContain(s.Between(5f, 5.1f), e => e.Kind == WaveEventKind.Elite);
            var boss = s.Between(19.99f, 20f).Single();
            Assert.Equal(WaveEventKind.Boss, boss.Kind);
            Assert.Equal("wongak", boss.Enemy);
            Assert.Empty(s.Between(20f, 30f));
        }

        [Fact] public void 장1_정해진_사건은_시드와_상관없이_그대로다()
        {
            var fixedAts = _d.Chapter("chapter1").Events.Where(e => e.Window == null).Select(e => e.At).ToList();
            foreach (var seed in new[] { 1, 2, 3 })
            {
                var ats = Table.Schedule("chapter1", new Random(seed)).Events.Where(e => e.Def.Window == null).Select(e => e.At).ToList();
                Assert.Equal(fixedAts, ats);
            }
            Assert.Equal(new[] { 3.5f, 8.5f, 9.5f, 15.5f, 16.6f, 17.7f },
                Table.Schedule("chapter1", new Random(1)).Events.Where(e => e.Kind == WaveEventKind.Charge).Select(e => e.At));
        }

        [Fact] public void 금두꺼비는_10분에서_12분_사이_무작위_시각에_한_번()
        {
            var ats = new List<float>();
            for (int seed = 1; seed <= 300; seed++)
            {
                var toads = Table.Schedule("chapter1", new Random(seed)).Events.Where(e => e.Kind == WaveEventKind.Toad).ToList();
                Assert.Single(toads);
                Assert.InRange(toads[0].At, 10f, 12f);
                ats.Add(toads[0].At);
            }
            Assert.True(ats.Min() < 10.2f && ats.Max() > 11.8f, "창 전체에 고르게 퍼져야 함");
            Assert.InRange(ats.Average(), 10.85f, 11.15f);
        }

        [Fact] public void 은빛_등롱은_15분_뒤_한_판에_한두_개()
        {
            int two = 0;
            const int runs = 1000;
            for (int seed = 1; seed <= runs; seed++)
            {
                var lanterns = Table.Schedule("chapter1", new Random(seed)).Events.Where(e => e.Kind == WaveEventKind.SilverLantern).ToList();
                Assert.InRange(lanterns.Count, 1, 2);
                Assert.All(lanterns, l => Assert.InRange(l.At, 15f, 19f));
                if (lanterns.Count == 2) two++;
            }
            Assert.InRange(two / (double)runs, 0.44, 0.56);
        }

        [Fact] public void 같은_시드면_같은_일정()
        {
            string Plan(int seed) => string.Join(" / ", Table.Schedule("chapter1", new Random(seed)).Events.Select(e => e.ToString()));
            Assert.Equal(Plan(77), Plan(77));
        }

        [Fact] public void 장별_곡선은_화면_적_수는_같고_멈춤과_보스_시각만_다르다()
        {
            var shared = new SpawnCurve(_d.Balance.SpawnTarget);
            var c1 = Table.Curve("chapter1", _d.Balance.SpawnTarget);
            var pro = Table.Curve("prologue", _d.Balance.SpawnTarget);
            for (float m = 0; m <= 20; m += 0.5f)
            {
                Assert.Equal(shared.TargetAt(m), c1.TargetAt(m));
                Assert.Equal(shared.TargetAt(m), pro.TargetAt(m));
                Assert.Equal(shared.SpawningStopped(m), c1.SpawningStopped(m));
            }
            Assert.Equal(54, c1.SpawnThisSecond(10f, 0));
            Assert.True(pro.SpawnThisSecond(8f, 0) > 0);
            Assert.Equal(0, pro.SpawnThisSecond(8.7f, 0));
            Assert.True(pro.SpawnThisSecond(9f, 0) > 0); // 장삼 보스전에는 다시 돈다
        }

        [Fact] public void 나오지_않는_적의_첫_등장은_없다()
        {
            Assert.Null(Table.FirstAppearance("prologue", "donggap"));
            Assert.Equal(6.5f, Table.FirstAppearance("chapter1", "mogin_nahan"));
            Assert.Equal(10f, Table.FirstAppearance("chapter1", "golden_toad"));
        }

        [Fact] public void 없는_장을_찾으면_장_id를_알려준다()
        {
            var e = Assert.Throws<KeyNotFoundException>(() => Table.PickEnemy("chapter9", 1, new Random(1)));
            Assert.Contains("chapter9", e.Message);
        }

        // ---- 잘못된 데이터는 읽을 때 막는다 ----

        static ChapterDef Sample() => new ChapterDef
        {
            Id = "test",
            DurationMinutes = 10,
            StopSpawningAtMinute = 8,
            Bands =
            {
                new SpawnBandDef { FromMinute = 0, ToMinute = 5, Spawns = { new SpawnWeightDef { Enemy = "sigang", Weight = 1 } } },
                new SpawnBandDef { FromMinute = 5, ToMinute = 10, Spawns = { new SpawnWeightDef { Enemy = "sigang", Weight = 1 } } },
            },
            Events = { new ChapterEventDef { At = 9, Kind = "boss", Enemy = "jang_sam" } },
        };

        [Fact] public void 정상_예시는_읽힌다()
        {
            var t = new WaveTable(new[] { Sample() });
            Assert.Equal("sigang", t.PickEnemy("test", 3, new Random(1)));
        }

        [Theory]
        [InlineData("시간대_틈")] [InlineData("판_끝까지_안_닿음")] [InlineData("0분부터_아님")] [InlineData("가중치_0")]
        [InlineData("모르는_사건")] [InlineData("보스_둘")] [InlineData("보스_없음")] [InlineData("창_거꾸로")]
        [InlineData("생성_중지가_보스보다_늦음")] [InlineData("확률_0")] [InlineData("적_없는_돌진")]
        public void 잘못된_장_데이터는_막는다(string broken)
        {
            var c = Sample();
            switch (broken)
            {
                case "시간대_틈": c.Bands[1].FromMinute = 6; break;
                case "판_끝까지_안_닿음": c.Bands[1].ToMinute = 9; break;
                case "0분부터_아님": c.Bands[0].FromMinute = 1; break;
                case "가중치_0": c.Bands[0].Spawns[0].Weight = 0; break;
                case "모르는_사건": c.Events.Add(new ChapterEventDef { At = 2, Kind = "meteor" }); break;
                case "보스_둘": c.Events.Add(new ChapterEventDef { At = 9.5f, Kind = "boss", Enemy = "wongak" }); break;
                case "보스_없음": c.Events.Clear(); break;
                case "창_거꾸로": c.Events.Insert(0, new ChapterEventDef { Window = new[] { 6f, 4f }, Kind = "toad", Enemy = "golden_toad" }); break;
                case "생성_중지가_보스보다_늦음": c.StopSpawningAtMinute = 9.5f; break;
                case "확률_0": c.Events.Insert(0, new ChapterEventDef { At = 2, Kind = "merchant", Chance = 0 }); break;
                case "적_없는_돌진": c.Events.Insert(0, new ChapterEventDef { At = 2, Kind = "charge", Count = 5 }); break;
            }
            Assert.ThrowsAny<Exception>(() => new WaveTable(new[] { c }));
        }

        [Theory]
        [InlineData("\"enemy\": \"dongin\"", "\"enemy\": \"no_such_elite\"")]
        [InlineData("\"enemy\": \"hyeol_musa\"", "\"enemy\": \"hyeol_musaa\"")]
        [InlineData("\"enemy\": \"wongak\"", "\"enemy\": \"jang_sam\"")] // 다른 장 보스
        public void 데이터에_없는_적_id는_읽을_때_막는다(string from, string to)
        {
            var files = GameData.FileNames.ToDictionary(n => n, n => File.ReadAllText(Path.Combine(TestData.DataDir, n + ".json")));
            Assert.Contains(from, files["chapters"]);
            files["chapters"] = files["chapters"].Replace(from, to);
            Assert.Throws<InvalidOperationException>(() => WaveTable.From(GameData.FromJson(files)));
        }
    }
}
