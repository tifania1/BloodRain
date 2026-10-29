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
    /// <summary>chapters.json(서장, 한 생)이 적 데이터·천하 지도·기획서 표(서장 흐름, 한 생의 시간대별 흐름, 공통 적 첫 등장)와 맞는지.</summary>
    public class ChapterDataTests
    {
        readonly GameData _d = TestData.Load();

        IEnumerable<ChapterDef> All => _d.Chapters;
        ChapterDef Life => _d.Chapter(WaveTable.Life);
        ChapterDef Prologue => _d.Chapter(WaveTable.Prologue);

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

        /// <summary>표 머리줄의 칸 이름.</summary>
        static string[] Header(string section) =>
            section.Split('\n').First(l => l.StartsWith("|")).Split('|').Skip(1).SkipLast(1).Select(c => c.Trim()).ToArray();

        /// <summary>평균 빌드가 그 경지에 처음 오르는 분. 목표 곡선(balance.json xpCurve.targets)대로 분당 3레벨, 첫 경지는 0분.</summary>
        float ReachMinute(RealmDef realm)
        {
            var bal = JObject.Parse(File.ReadAllText(Path.Combine(TestData.DataDir, "balance.json")));
            float levelsPerMinute = 60f / (float)bal["xpCurve"]!["targets"]!["level60AtMinute"]!;
            return realm == _d.Balance.Realms[0] ? 0 : realm.Level / levelsPerMinute;
        }

        RealmDef RealmByName(string nameKo) => _d.Balance.Realms.Single(r => r.NameKo == nameKo);

        /// <summary>그 분에 평균 빌드가 머무는 경지.</summary>
        RealmDef RealmAt(float minute) => _d.Balance.Realms.Last(r => ReachMinute(r) <= minute + 1e-4f);

        // ---- 데이터끼리 ----

        [Fact] public void 흐름은_서장과_한_생이고_서장만_자리가_정해져_있다()
        {
            Assert.Equal(new[] { "prologue", "life" }, All.Select(c => c.Id));
            var cheongun = _d.Cheonha.Region(Prologue.Region!);
            Assert.Equal(_d.Cheonha.PrologueBody.Region, cheongun.Id);
            Assert.Equal("prologue", cheongun.Chapter);
            Assert.Contains(cheongun.NameKo, Prologue.NameKo);
            Assert.False(string.IsNullOrWhiteSpace(Prologue.TitleKo));
            Assert.True(_d.Balance.EnemyScaling.ChapterMultiplier.ContainsKey(cheongun.Chapter!), "서장 혈운 배율 없음");
            Assert.Null(Life.Region); // 한 생은 서 있는 지역을 따른다
        }

        [Fact] public void 서장은_10분에_끝나고_한_생은_30분_현경_뒤로도_이어진다()
        {
            Assert.Equal(10f, Prologue.DurationMinutes);
            Assert.False(Prologue.OpenEnded);
            Assert.True(Life.OpenEnded);
            Assert.Equal(ReachMinute(_d.Balance.Realms[^1]), Life.DurationMinutes, 3);
            Assert.Equal(_d.Cheonha.LifeEnd.ShadowFromMinute, Life.DurationMinutes);
            // 표가 끝난 뒤에도 마지막 가중치표와 곡선 끝값을 그대로 쓴다
            var t = WaveTable.From(_d);
            Assert.Same(Life.Bands[^1], t.BandAt(WaveTable.Life, 60));
            Assert.Equal(new SpawnCurve(_d.Balance.SpawnTarget).TargetAt(Life.DurationMinutes), new SpawnCurve(_d.Balance.SpawnTarget).TargetAt(60));
        }

        [Fact] public void 시간대마다_가중치_합은_1이다()
        {
            foreach (var c in All)
                foreach (var b in c.Bands)
                    Assert.True(Math.Abs(b.TotalWeight - 1f) < 1e-4, $"{c.Id} {b.FromMinute}분 시간대 합 {b.TotalWeight}");
        }

        [Fact] public void 시간대는_0분부터_표_끝까지_빈틈없이_이어진다()
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

        [Fact] public void 가중치표와_사건의_적은_모두_데이터에_있다()
        {
            foreach (var c in All)
            {
                string? chapterName = c.Region != null ? _d.Cheonha.Region(c.Region).Chapter : null;
                foreach (var s in c.Bands.SelectMany(b => b.Spawns))
                    Assert.True(_d.Enemies.Any(e => e.Id == s.Enemy), $"{c.Id}: 없는 적 {s.Enemy}");
                foreach (var e in c.Events)
                {
                    Assert.NotEqual(WaveEventKind.Unknown, e.Type);
                    switch (e.Type)
                    {
                        case WaveEventKind.Elite:
                            if (e.Nth == 0) Assert.Contains(e.Enemy, _d.Elites[chapterName!]);
                            else Assert.Equal("", e.Enemy);
                            break;
                        case WaveEventKind.Boss: Assert.Contains(_d.Bosses, b => b.Id == e.Enemy && b.Chapter == chapterName); break;
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

        [Fact] public void 지역_고유_적은_가중치표와_사건에_직접_나오지_않는다()
        {
            foreach (var c in All)
            {
                var used = c.Bands.SelectMany(b => b.Spawns.Select(s => s.Enemy))
                    .Concat(c.Events.Where(e => WaveEventKinds.SpawnsEnemy(e.Type) && e.Type != WaveEventKind.Elite && e.Type != WaveEventKind.Boss).Select(e => e.Enemy))
                    .Distinct();
                foreach (var id in used)
                    Assert.True(_d.Enemy(id).Chapter == null, $"{c.Id}: 지역 고유 적 {_d.Enemy(id).NameKo} 가 흐름에 직접 들어 있음");
            }
            // 고유 적은 한 생의 localShare 와 무리 사건으로만 나온다
            Assert.NotEmpty(Life.LocalShare);
            Assert.Single(Life.Events, e => e.Local);
            // 서장 청운진은 고유 적이 없는 땅이라 섞을 것도 없다
            Assert.Empty(new RegionSpawns(_d).LocalEnemies(Prologue.Region!));
            Assert.Empty(Prologue.LocalShare);
        }

        [Fact] public void 사건은_시각_순서이고_표_길이_안에_있다()
        {
            foreach (var c in All)
            {
                float prev = 0;
                foreach (var e in c.Events)
                {
                    Assert.True(e.EarliestMinute > 0, $"{c.Id} {e.Kind} 가 0분");
                    Assert.True(e.EarliestMinute >= prev, $"{c.Id} {e.Kind} {e.EarliestMinute}분이 앞 사건보다 이름");
                    Assert.True(e.LatestMinute <= c.DurationMinutes, $"{c.Id} {e.Kind} 가 표 길이 밖");
                    Assert.InRange(e.Chance, 0.01f, 1f);
                    prev = e.EarliestMinute;
                }
                if (c.StopSpawningAtMinute != null)
                {
                    Assert.NotNull(c.BossAtMinute);
                    Assert.True(c.StopSpawningAtMinute < c.BossAtMinute);
                }
                if (c.BossAtMinute != null) Assert.True(c.BossAtMinute <= c.DurationMinutes);
            }
        }

        [Fact] public void 서장_정예는_도부수_하나이고_한_생_정예는_순번으로_다섯()
        {
            var pro = Prologue.Events.Where(e => e.Type == WaveEventKind.Elite).ToList();
            Assert.Equal(_d.Elites["prologue"], pro.Select(e => e.Enemy));
            Assert.Equal(new[] { 5f }, pro.Select(e => e.At));

            var life = Life.Events.Where(e => e.Type == WaveEventKind.Elite).ToList();
            Assert.Equal(new[] { 5f, 10f, 15f, 20f, 25f }, life.Select(e => e.At));
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, life.Select(e => e.Nth));
            Assert.All(life, e => Assert.Equal("", e.Enemy));
        }

        [Fact] public void 보스와_생성_중지는_서장에만_있다()
        {
            Assert.Equal("jang_sam", Prologue.Boss);
            Assert.Equal(9f, Prologue.BossAtMinute);
            Assert.Equal(8.5f, Prologue.StopSpawningAtMinute);
            // 한 생의 땅의 주인은 정해진 시각이 아니라 지역 한가운데 들어설 때 싸움이 시작된다(7장)
            Assert.Null(Life.BossEvent);
            Assert.Null(Life.StopSpawningAtMinute);
        }

        // ---- 기획서 표 ----

        [Fact] public void 한_생_사건이_기획서_시간대별_흐름과_같다()
        {
            var c = Life;
            var rows = TestData.GddTable(TestData.GddSection("한 생의 시간대별 흐름"));
            Assert.Equal(16, rows.Count);
            int elites = 0, merchants = 0, charges = 0, checkedRows = 0;
            foreach (var row in rows)
            {
                var t = Minutes(row[0]);
                Assert.True(t != null, $"한 생 표 시간 칸 모양이 다름: {row[0]}");
                var (a, b) = t!.Value;
                string ev = row[3];
                checkedRows++;
                if (a == b)
                {
                    bool known = false;
                    if (ev.Contains("정예"))
                    {
                        known = true;
                        elites++;
                        var e = c.Events.Single(x => x.Type == WaveEventKind.Elite && x.At == a);
                        Assert.Equal(elites, e.Nth);
                    }
                    if (ev.Contains("보부상"))
                    {
                        known = true;
                        merchants++;
                        Assert.Contains(c.Events, e => e.Type == WaveEventKind.Merchant && e.At == a);
                    }
                    if (ev.Contains("혈마의 그림자"))
                    {
                        // 30분 현경: 그림자는 사건이 아니라 cheonha.json lifeEnd 규칙이고, 여기서 표가 끝난다
                        known = true;
                        Assert.Equal(a, _d.Cheonha.LifeEnd.ShadowFromMinute);
                        Assert.Equal(a, c.DurationMinutes);
                    }
                    Assert.True(known, $"{row[0]} 줄 '{ev}' 를 확인하는 곳이 없음");
                    continue;
                }
                // 구간 [a, b): 돌진 횟수가 기획서와 같아야 한다(언급이 없으면 0회)
                var charge = Regex.Match(ev, @"돌진 (\d+)회");
                int n = charge.Success ? int.Parse(charge.Groups[1].Value) : 0;
                charges += n;
                Assert.True(n == c.Events.Count(e => e.Type == WaveEventKind.Charge && e.At >= a && e.At < b), $"{row[0]} 돌진 {n}회가 아님");
                if (ev.Contains("금두꺼비"))
                    Assert.Equal(new[] { a, b }, c.Events.Single(e => e.Type == WaveEventKind.Toad).Window);
                if (ev.Contains("포위"))
                    Assert.Contains(c.Events, e => e.Type == WaveEventKind.Ring && e.At > a && e.At < b && ev.Contains(_d.Enemy(e.Enemy).NameKo));
                if (ev.Contains("무리"))
                    Assert.Contains(c.Events, e => e.Type == WaveEventKind.Group && e.Local && e.At >= a && e.At < b);
                if (ev.Contains("은빛 등롱"))
                {
                    var lanterns = c.Events.Where(e => e.Type == WaveEventKind.SilverLantern).ToList();
                    Assert.NotEmpty(lanterns);
                    Assert.All(lanterns, l => Assert.True(l.EarliestMinute >= a && l.LatestMinute <= b));
                }
                if (ev.Contains("땅의 주인 깨어남"))
                {
                    // 절정부터 깨어난다(cheonha.json bosses.awakenFrom). 깨어나는 줄은 절정 이상 경지에 오르는 줄이다
                    var realm = RealmByName(Regex.Match(row[1], @"^\S+?(?=\(|$)").Value);
                    int from = _d.Balance.Realms.FindIndex(r => r.Id == _d.Cheonha.Bosses.AwakenFrom);
                    Assert.True(_d.Balance.Realms.IndexOf(realm) >= from, $"{row[0]} {realm.NameKo} 에서 땅의 주인이 깨어남");
                }
            }
            Assert.Equal(16, checkedRows);
            Assert.Equal(5, elites);
            Assert.Equal(elites, c.Events.Count(e => e.Type == WaveEventKind.Elite));
            Assert.Equal(merchants, c.Events.Count(e => e.Type == WaveEventKind.Merchant));
            Assert.Equal(charges, c.Events.Count(e => e.Type == WaveEventKind.Charge));
            Assert.Single(c.Events, e => e.Type == WaveEventKind.Toad);
            Assert.Single(c.Events, e => e.Type == WaveEventKind.Ring);
            Assert.Single(c.Events, e => e.Type == WaveEventKind.Group);
        }

        /// <summary>'주력 적' 칸에 적힌 적이 그 구간 시간대마다 절반 이상을 차지하고, 구간 안 어느 시간대에는 꼭 나온다.</summary>
        [Fact] public void 한_생_주력_적이_기획서_시간대별_흐름과_같다()
        {
            var c = Life;
            int checkedRows = 0;
            foreach (var row in TestData.GddTable(TestData.GddSection("한 생의 시간대별 흐름")))
            {
                var t = Minutes(row[0]);
                if (t == null || t.Value.from == t.Value.to || row[2] == "") continue;
                var names = row[2].Split('+').Select(n => n.Trim().Replace(" 벽", "")).ToList();
                var ids = names.Select(n => EnemyByName(n)?.Id).ToList();
                if (ids.Any(id => id == null)) continue; // '전 계열 혼합', '최대 밀도' 처럼 적 이름이 아닌 칸
                checkedRows++;
                var (a, b) = t.Value;
                var bands = c.Bands.Where(x => x.FromMinute < b && x.ToMinute > a).ToList();
                Assert.NotEmpty(bands);
                foreach (var band in bands)
                {
                    float share = ids.Sum(id => band.WeightOf(id!));
                    Assert.True(share >= 0.5f, $"{band.FromMinute}~{band.ToMinute}분 주력 적({row[2]}) 비중 {share}");
                }
                foreach (var id in ids) Assert.Contains(bands, x => x.WeightOf(id!) > 0);
            }
            Assert.Equal(5, checkedRows);
        }

        /// <summary>'경지' 칸: 괄호가 있으면 그 구간에 그 경지로 돌파하고 시각이 경지표(분당 3레벨)와 같다. 없으면 그 시각에 머무는 경지.</summary>
        [Fact] public void 한_생_경지_칸이_경지_도달_시각과_같다()
        {
            int checkedRows = 0, breakthroughs = 0;
            foreach (var row in TestData.GddTable(TestData.GddSection("한 생의 시간대별 흐름")))
            {
                if (row[1] == "") continue;
                var (a, b) = Minutes(row[0])!.Value;
                var m = Regex.Match(row[1], @"^(\S+?)(?:\((\d+)분(?: (\d+)초)?\))?$");
                Assert.True(m.Success, $"경지 칸 모양이 다름: {row[1]}");
                var realm = RealmByName(m.Groups[1].Value);
                checkedRows++;
                if (m.Groups[2].Success)
                {
                    breakthroughs++;
                    float at = int.Parse(m.Groups[2].Value) + (m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0) / 60f;
                    Assert.Equal(ReachMinute(realm), at, 2);
                    Assert.True(at >= a && at < b, $"{row[0]} 안에 {realm.NameKo} 돌파({at:0.##}분)가 없음");
                }
                else Assert.Same(realm, RealmAt(a));
            }
            Assert.Equal(9, checkedRows);
            Assert.Equal(5, breakthroughs); // 이류·일류·절정·초절정·화경. 현경은 30분 줄에 괄호 없이
            Assert.Equal(_d.Balance.Realms[^1], RealmAt(Life.DurationMinutes));
        }

        [Fact] public void 서장_사건_시각이_기획서_서장_흐름과_같다()
        {
            var c = Prologue;
            var rows = TestData.GddTable(TestData.GddSection("서장 상세 흐름"));
            int charges = 0, elites = 0, breakthroughs = 0;
            foreach (var row in rows)
            {
                var at = Clock(row[0]);
                Assert.True(at != null, $"서장 표 시간 칸 모양이 다름: {row[0]}");
                string what = row[1];
                if (what.Contains("돌진")) { charges++; Assert.Contains(c.Events, e => e.Type == WaveEventKind.Charge && Math.Abs(e.At - at!.Value) < 1e-4); }
                if (what.Contains("정예")) { elites++; Assert.Contains(c.Events, e => e.Type == WaveEventKind.Elite && e.At == at); }
                if (what.Contains("보스")) Assert.Equal(at, c.BossAtMinute);
                if (what.Contains("시강")) Assert.True(c.Bands.First(b => b.FromMinute <= at && at < b.ToMinute).WeightOf("sigang") > 0);
                var brk = Regex.Match(what, @"^(\S+) 돌파");
                if (brk.Success)
                {
                    // 서장도 천하 규칙 그대로라 경지표 시각에 돌파한다(7장 '서장(첫 생)')
                    breakthroughs++;
                    Assert.Equal(ReachMinute(RealmByName(brk.Groups[1].Value)), at!.Value, 2);
                }
            }
            Assert.Equal(charges, c.Events.Count(e => e.Type == WaveEventKind.Charge));
            Assert.Equal(elites, c.Events.Count(e => e.Type == WaveEventKind.Elite));
            Assert.Equal(2, breakthroughs); // 이류 3:20, 일류 6:40
            Assert.Equal(c.DurationMinutes, Clock(rows[^1][0]));
            Assert.DoesNotContain(c.Events, e => e.Type == WaveEventKind.Merchant); // 보부상은 12분이라 10분짜리 서장엔 없다
        }

        [Fact] public void 공통_적이_처음_나오는_시각이_기획서_적_표와_같다()
        {
            var sec = TestData.GddSection("공통 적");
            int col = Array.IndexOf(Header(sec), "처음 등장(한 생 시각)");
            Assert.True(col >= 0, "공통 적 표에 '처음 등장(한 생 시각)' 칸이 없음");
            var table = WaveTable.From(_d);
            int checkedRows = 0;
            foreach (var row in TestData.GddTable(sec))
            {
                string name = Regex.Replace(row[0], @"\(.*\)", "").Trim();
                var enemy = EnemyByName(name);
                Assert.True(enemy != null, $"기획서 적 {name} 이 enemies.json 에 없음");
                Assert.Null(enemy!.Chapter);
                float? first = table.FirstAppearance(WaveTable.Life, enemy.Id);
                var at = Clock(row[col]);
                var window = Regex.Match(row[col], @"^(\d+)~(\d+)분 사이 한 번$");
                if (at != null)
                    Assert.True(first is float f && Math.Abs(f - at.Value) < 1e-4, $"{name}: 기획서 {row[col]}, 데이터 {first}");
                else
                {
                    Assert.True(window.Success, $"{name}: 처음 등장 칸 모양이 다름 '{row[col]}'");
                    var e = Life.Events.Single(x => x.Enemy == enemy.Id);
                    Assert.Equal(new[] { float.Parse(window.Groups[1].Value), float.Parse(window.Groups[2].Value) }, e.Window);
                    Assert.Equal(e.EarliestMinute, first);
                }
                checkedRows++;
            }
            Assert.Equal(11, checkedRows); // 시강 … 기수, 금두꺼비
        }

        /// <summary>7장 '지역과 혈운 농도' 아래 문단: 고유 적은 5분부터 생성의 10%, 15분부터 15%. 5~8분 무리는 숭산이면 목인나한, 고유 적이 없는 땅이면 혈강시.</summary>
        [Fact] public void 지역_고유_적_비율과_무리가_기획서_7장과_같다()
        {
            var sec = TestData.GddSection("지역과 혈운 농도");
            var share = Regex.Match(sec, @"(\d+)분부터 생성의 (\d+)%, (\d+)분부터 (\d+)%");
            Assert.True(share.Success, "7장 고유 적 비율 문장 모양이 바뀜");
            Assert.Equal(
                new[] { (float.Parse(share.Groups[1].Value), float.Parse(share.Groups[2].Value) / 100f), (float.Parse(share.Groups[3].Value), float.Parse(share.Groups[4].Value) / 100f) },
                Life.LocalShare.Select(s => (s.FromMinute, s.Share)));
            Assert.Equal(0f, Life.LocalShareAt(4.99f));

            var group = Regex.Match(sec, @"(\d+)~(\d+)분의 '무리' 사건은 서 있는 지역의 고유 적으로 나옵니다\((\S+)이면 (\S+), 고유 적이 없는 땅이면 (\S+)\)");
            Assert.True(group.Success, "7장 무리 사건 문장 모양이 바뀜");
            var ev = Life.Events.Single(e => e.Type == WaveEventKind.Group);
            Assert.True(ev.Local);
            Assert.InRange(ev.At, float.Parse(group.Groups[1].Value), float.Parse(group.Groups[2].Value));
            var region = _d.Cheonha.Regions.Single(r => r.NameKo == group.Groups[3].Value);
            var regions = new RegionSpawns(_d);
            Assert.Contains(group.Groups[4].Value, regions.LocalEnemies(region.Id).Select(id => _d.Enemy(id).NameKo));
            Assert.Equal(group.Groups[5].Value, _d.Enemy(ev.Enemy).NameKo);
        }
    }

    /// <summary>WaveTable: 분마다 적 종류 추첨(지역 고유 적 섞기 포함), 한 생 사건 일정, 흐름별 SpawnCurve.</summary>
    public class WaveTableTests
    {
        readonly GameData _d = TestData.Load();
        WaveTable Table => WaveTable.From(_d);
        const string Life = WaveTable.Life;

        [Theory]
        [InlineData(-1f, 0)] [InlineData(0f, 0)] [InlineData(2.99f, 0)] [InlineData(3f, 1)]
        [InlineData(5.5f, 2)] [InlineData(6f, 3)] [InlineData(10.5f, 5)] [InlineData(11f, 6)]
        [InlineData(29.9f, 11)] [InlineData(45f, 11)]
        public void 분에_맞는_시간대를_고른다(float minute, int bandIndex)
        {
            var c = _d.Chapter(Life);
            Assert.Same(c.Bands[bandIndex], Table.BandAt(Life, minute));
        }

        [Fact] public void 처음_3분은_시강만_나온다()
        {
            var t = Table;
            var rng = new Random(1);
            for (int i = 0; i < 500; i++) Assert.Equal("sigang", t.PickEnemy(Life, i % 300 / 100f, rng));
        }

        [Theory]
        [InlineData(4f)] [InlineData(7f)] [InlineData(9f)] [InlineData(11.5f)]
        [InlineData(14f)] [InlineData(18f)] [InlineData(25f)]
        public void 고정_시드로_많이_뽑으면_가중치_비율대로_나온다(float minute)
        {
            var t = Table;
            var band = t.BandAt(Life, minute);
            var rng = new Random(20261029);
            const int n = 20000;
            var count = new Dictionary<string, int>();
            for (int i = 0; i < n; i++)
            {
                var id = t.PickEnemy(Life, minute, rng);
                count[id] = count.GetValueOrDefault(id) + 1;
            }
            Assert.Equal(band.Spawns.Select(s => s.Enemy).OrderBy(x => x), count.Keys.OrderBy(x => x));
            // 표준편차는 많아야 0.0035. 0.015 안이면 가중치표를 그대로 따른 것
            foreach (var s in band.Spawns) Assert.InRange(count[s.Enemy] / (double)n, s.Weight - 0.015, s.Weight + 0.015);
        }

        [Theory]
        [InlineData("songshan", 7f, 0.10)]
        [InlineData("songshan", 16f, 0.15)]
        [InlineData("danggata", 16f, 0.15)]
        [InlineData("gaebang_hq", 16f, 0)]
        public void 서_있는_지역의_고유_적이_정한_몫만큼_섞인다(string region, float minute, double share)
        {
            var t = Table;
            var local = new RegionSpawns(_d).LocalEnemies(region);
            var rng = new Random(7);
            const int n = 20000;
            var count = new Dictionary<string, int>();
            for (int i = 0; i < n; i++)
            {
                var id = t.PickEnemy(Life, minute, local, rng);
                count[id] = count.GetValueOrDefault(id) + 1;
            }
            double localTotal = local.Sum(id => count.GetValueOrDefault(id)) / (double)n;
            Assert.InRange(localTotal, share - 0.012, share + 0.012);
            foreach (var id in local) Assert.InRange(count[id] / (double)n, share / local.Count - 0.012, share / local.Count + 0.012); // 고유 적끼리는 같은 비율
            // 나머지는 가중치표 비율 그대로
            var band = t.BandAt(Life, minute);
            foreach (var s in band.Spawns) Assert.InRange(count[s.Enemy] / (double)n, s.Weight * (1 - share) - 0.015, s.Weight * (1 - share) + 0.015);
        }

        [Fact] public void 고유_적이_없거나_5분_전이면_난수를_더_쓰지_않는다()
        {
            var t = Table;
            var mogin = new RegionSpawns(_d).LocalEnemies("songshan");
            string Seq(IReadOnlyList<string>? local, float minute)
            {
                var r = new Random(5);
                return string.Join(",", Enumerable.Range(0, 200).Select(_ => t.PickEnemy(Life, minute, local, r)));
            }
            string Plain(float minute)
            {
                var r = new Random(5);
                return string.Join(",", Enumerable.Range(0, 200).Select(_ => t.PickEnemy(Life, minute, r)));
            }
            Assert.Equal(Plain(4.9f), Seq(mogin, 4.9f));
            Assert.Equal(Plain(12f), Seq(Array.Empty<string>(), 12f));
            Assert.Equal(Plain(12f), Seq(null, 12f));
            Assert.NotEqual(Plain(12f), Seq(mogin, 12f));
        }

        [Fact] public void 같은_시드면_같은_적_순서()
        {
            var t = Table;
            string Seq(int seed) { var r = new Random(seed); return string.Join(",", Enumerable.Range(0, 50).Select(i => t.PickEnemy(Life, 13, r))); }
            Assert.Equal(Seq(9), Seq(9));
            Assert.NotEqual(Seq(9), Seq(10));
        }

        [Fact] public void 일정은_시각_순서이고_프레임마다_넘기면_사건이_한_번씩만_나온다()
        {
            foreach (var chapter in new[] { WaveTable.Prologue, Life })
            {
                var s = Table.Schedule(chapter, new Random(3));
                Assert.Equal(s.Events.OrderBy(e => e.At).Select(e => e.At), s.Events.Select(e => e.At));
                var seen = new List<WaveEvent>();
                float prev = 0;
                for (int frame = 1; frame <= 32 * 60 * 60; frame++) // 60fps 로 32분
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
            var s = Table.Schedule(Life, new Random(1));
            Assert.Contains(s.Between(4.9f, 5f), e => e.Kind == WaveEventKind.Elite && e.Def.Nth == 1);
            Assert.DoesNotContain(s.Between(5f, 5.1f), e => e.Kind == WaveEventKind.Elite);
            Assert.Contains(s.Between(24.99f, 25f), e => e.Kind == WaveEventKind.Elite && e.Def.Nth == 5);
            Assert.Empty(s.Between(25f, 1000f)); // 마지막 사건은 25분 정예. 그 뒤(현경, 혈마의 그림자)는 사건이 아니다

            var p = Table.Schedule(WaveTable.Prologue, new Random(1));
            var boss = p.Between(8.99f, 9f).Single();
            Assert.Equal(WaveEventKind.Boss, boss.Kind);
            Assert.Equal("jang_sam", boss.Enemy);
            Assert.Empty(p.Between(9f, 30f));
        }

        [Fact] public void 한_생의_정해진_사건은_시드와_상관없이_그대로다()
        {
            var fixedAts = _d.Chapter(Life).Events.Where(e => e.Window == null && e.Chance >= 1).Select(e => e.At).ToList();
            foreach (var seed in new[] { 1, 2, 3 })
            {
                var ats = Table.Schedule(Life, new Random(seed)).Events.Where(e => e.Def.Window == null).Select(e => e.At).ToList();
                Assert.Equal(fixedAts, ats);
            }
            Assert.Equal(new[] { 3.5f, 8.5f, 9.5f, 15.5f, 16.6f, 17.7f, 21.5f, 24.5f },
                Table.Schedule(Life, new Random(1)).Events.Where(e => e.Kind == WaveEventKind.Charge).Select(e => e.At));
            Assert.Equal(new[] { 12f, 24f },
                Table.Schedule(Life, new Random(1)).Events.Where(e => e.Kind == WaveEventKind.Merchant).Select(e => e.At));
        }

        [Fact] public void 금두꺼비는_10분에서_12분_사이_무작위_시각에_한_번()
        {
            var ats = new List<float>();
            for (int seed = 1; seed <= 300; seed++)
            {
                var toads = Table.Schedule(Life, new Random(seed)).Events.Where(e => e.Kind == WaveEventKind.Toad).ToList();
                Assert.Single(toads);
                Assert.InRange(toads[0].At, 10f, 12f);
                ats.Add(toads[0].At);
            }
            Assert.True(ats.Min() < 10.2f && ats.Max() > 11.8f, "창 전체에 고르게 퍼져야 함");
            Assert.InRange(ats.Average(), 10.85f, 11.15f);
        }

        [Fact] public void 은빛_등롱은_15분_뒤_한_생에_한두_개()
        {
            int two = 0;
            const int runs = 1000;
            for (int seed = 1; seed <= runs; seed++)
            {
                var lanterns = Table.Schedule(Life, new Random(seed)).Events.Where(e => e.Kind == WaveEventKind.SilverLantern).ToList();
                Assert.InRange(lanterns.Count, 1, 2);
                Assert.All(lanterns, l => Assert.InRange(l.At, 15f, 19f));
                if (lanterns.Count == 2) two++;
            }
            Assert.InRange(two / (double)runs, 0.44, 0.56);
        }

        [Fact] public void 같은_시드면_같은_일정()
        {
            string Plan(int seed) => string.Join(" / ", Table.Schedule(Life, new Random(seed)).Events.Select(e => e.ToString()));
            Assert.Equal(Plan(77), Plan(77));
        }

        [Fact] public void 흐름별_곡선은_화면_적_수는_같고_서장만_장삼_앞에서_멈춘다()
        {
            var shared = new SpawnCurve(_d.Balance.SpawnTarget);
            var life = Table.Curve(Life, _d.Balance.SpawnTarget);
            var pro = Table.Curve(WaveTable.Prologue, _d.Balance.SpawnTarget);
            for (float m = 0; m <= 40; m += 0.5f)
            {
                Assert.Equal(shared.TargetAt(m), life.TargetAt(m));
                Assert.Equal(shared.TargetAt(m), pro.TargetAt(m));
                Assert.False(life.SpawningStopped(m), $"한 생이 {m}분에 멈춤");
            }
            Assert.Equal(54, life.SpawnThisSecond(10f, 0));
            Assert.Equal(120, life.SpawnThisSecond(19.5f, 0));
            Assert.True(pro.SpawnThisSecond(8f, 0) > 0);
            Assert.Equal(0, pro.SpawnThisSecond(8.7f, 0));
            Assert.True(pro.SpawnThisSecond(9f, 0) > 0); // 장삼 보스전에는 다시 돈다
        }

        [Fact] public void 나오지_않는_적의_첫_등장은_없다()
        {
            Assert.Null(Table.FirstAppearance(WaveTable.Prologue, "donggap"));
            Assert.Null(Table.FirstAppearance(Life, "mogin_nahan")); // 지역 고유 적은 흐름에 직접 없다
            Assert.Equal(3.5f, Table.FirstAppearance(Life, "hyeolin"));
            Assert.Equal(10f, Table.FirstAppearance(Life, "golden_toad"));
            Assert.Equal(22f, Table.FirstAppearance(Life, "gisu"));
        }

        [Fact] public void 없는_흐름을_찾으면_id를_알려준다()
        {
            var e = Assert.Throws<KeyNotFoundException>(() => Table.PickEnemy("chapter1", 1, new Random(1)));
            Assert.Contains("chapter1", e.Message); // 옛 1장 흐름은 한 생(life)으로 바뀌었다
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

        [Fact] public void 보스도_생성_중지도_없는_흐름은_읽힌다()
        {
            var c = Sample();
            c.Events.Clear();
            c.StopSpawningAtMinute = null;
            c.Events.Add(new ChapterEventDef { At = 5, Kind = "elite", Nth = 1 });
            c.Events.Add(new ChapterEventDef { At = 6, Kind = "group", Local = true, Enemy = "hyeol_gangsi", Count = 8 });
            c.LocalShare.Add(new LocalShareDef { FromMinute = 5, Share = 0.1f });
            var t = new WaveTable(new[] { c });
            Assert.False(t.Curve("test", new SpawnTargetDef { Points = { new[] { 0f, 10f }, new[] { 10f, 20f } } }).SpawningStopped(9));
        }

        [Theory]
        [InlineData("시간대_틈")] [InlineData("판_끝까지_안_닿음")] [InlineData("0분부터_아님")] [InlineData("가중치_0")]
        [InlineData("모르는_사건")] [InlineData("보스_둘")] [InlineData("생성_중지만_있고_보스_없음")] [InlineData("창_거꾸로")]
        [InlineData("생성_중지가_보스보다_늦음")] [InlineData("확률_0")] [InlineData("적_없는_돌진")]
        [InlineData("정예에_적과_순번_둘다")] [InlineData("정예에_적도_순번도_없음")] [InlineData("순번은_정예에만")]
        [InlineData("지역_무리에_대신할_적_없음")] [InlineData("local은_무리에만")] [InlineData("고유_적_몫_1")] [InlineData("고유_적_몫_시각_거꾸로")]
        public void 잘못된_흐름_데이터는_막는다(string broken)
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
                case "생성_중지만_있고_보스_없음": c.Events.Clear(); break;
                case "창_거꾸로": c.Events.Insert(0, new ChapterEventDef { Window = new[] { 6f, 4f }, Kind = "toad", Enemy = "golden_toad" }); break;
                case "생성_중지가_보스보다_늦음": c.StopSpawningAtMinute = 9.5f; break;
                case "확률_0": c.Events.Insert(0, new ChapterEventDef { At = 2, Kind = "merchant", Chance = 0 }); break;
                case "적_없는_돌진": c.Events.Insert(0, new ChapterEventDef { At = 2, Kind = "charge", Count = 5 }); break;
                case "정예에_적과_순번_둘다": c.Events.Insert(0, new ChapterEventDef { At = 5, Kind = "elite", Enemy = "dongin", Nth = 1 }); break;
                case "정예에_적도_순번도_없음": c.Events.Insert(0, new ChapterEventDef { At = 5, Kind = "elite" }); break;
                case "순번은_정예에만": c.Events.Insert(0, new ChapterEventDef { At = 2, Kind = "charge", Enemy = "hyeolin", Count = 5, Nth = 1 }); break;
                case "지역_무리에_대신할_적_없음": c.Events.Insert(0, new ChapterEventDef { At = 6, Kind = "group", Local = true, Count = 8 }); break;
                case "local은_무리에만": c.Events.Insert(0, new ChapterEventDef { At = 2, Kind = "charge", Enemy = "hyeolin", Count = 5, Local = true }); break;
                case "고유_적_몫_1": c.LocalShare.Add(new LocalShareDef { FromMinute = 5, Share = 1 }); break;
                case "고유_적_몫_시각_거꾸로":
                    c.LocalShare.Add(new LocalShareDef { FromMinute = 5, Share = 0.1f });
                    c.LocalShare.Add(new LocalShareDef { FromMinute = 3, Share = 0.15f });
                    break;
            }
            Assert.ThrowsAny<Exception>(() => new WaveTable(new[] { c }));
        }

        [Theory]
        [InlineData("\"enemy\": \"gangsi_axeman\"", "\"enemy\": \"no_such_elite\"")]
        [InlineData("\"enemy\": \"hyeol_musa\"", "\"enemy\": \"hyeol_musaa\"")]
        [InlineData("\"enemy\": \"jang_sam\"", "\"enemy\": \"wongak\"")] // 다른 지역 보스
        [InlineData("{ \"enemy\": \"bigang\", \"weight\": 0.15 }", "{ \"enemy\": \"mogin_nahan\", \"weight\": 0.15 }")] // 고유 적을 가중치표에
        [InlineData("\"local\": true, \"enemy\": \"hyeol_gangsi\"", "\"local\": true, \"enemy\": \"mogin_nahan\"")] // 대신할 적이 고유 적
        [InlineData("\"region\": \"cheongun\"", "\"region\": \"nowhere\"")]
        [InlineData("\"region\": \"songshan\"", "\"region\": \"nowhere\"")] // eliteFallback
        public void 데이터에_없는_id는_읽을_때_막는다(string from, string to)
        {
            var files = GameData.FileNames.ToDictionary(n => n, n => File.ReadAllText(Path.Combine(TestData.DataDir, n + ".json")));
            Assert.Contains(from, files["chapters"]);
            files["chapters"] = files["chapters"].Replace(from, to);
            Assert.Throws<InvalidOperationException>(() => WaveTable.From(GameData.FromJson(files)));
        }
    }
}
