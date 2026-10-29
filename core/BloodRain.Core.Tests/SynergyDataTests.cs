using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using Newtonsoft.Json.Linq;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>synergies.json 이 무공·심법·캐릭터·지도 데이터, 기획서 20장과 어긋나지 않는지.</summary>
    public class SynergyDataTests
    {
        readonly GameData _d = TestData.Load();
        SynergyDefs S => _d.Synergies;

        /// <summary>worldmap.json 은 아직 Core 타입이 없어서(backlog M-01) 장 이름과 문파만 직접 읽는다.</summary>
        static List<JObject> Regions() =>
            JObject.Parse(File.ReadAllText(Path.Combine(TestData.DataDir, "worldmap.json")))["regions"]!.Children<JObject>().ToList();

        static HashSet<string> ChapterIds() =>
            Regions().Where(r => (string)r["kind"]! == "chapter").Select(r => (string)r["chapter"]!).ToHashSet();

        [Fact] public void 연환은_16개이고_무공마다_정확히_두_번_나온다()
        {
            Assert.Equal(16, S.Links.Count);
            var count = new Dictionary<string, int>();
            foreach (var l in S.Links)
            {
                Assert.NotEqual(l.A, l.B);
                Assert.Contains(_d.Weapons, w => w.Id == l.A);
                Assert.Contains(_d.Weapons, w => w.Id == l.B);
                count[l.A] = count.GetValueOrDefault(l.A) + 1;
                count[l.B] = count.GetValueOrDefault(l.B) + 1;
            }
            foreach (var w in _d.Weapons) Assert.True(count.GetValueOrDefault(w.Id) == 2, $"{w.NameKo} 연환 {count.GetValueOrDefault(w.Id)}개");
            var pairs = S.Links.Select(l => string.Join("+", new[] { l.A, l.B }.OrderBy(x => x))).ToList();
            Assert.Equal(pairs.Count, pairs.Distinct().Count());
            Assert.Equal(S.Links.Count, S.Links.Select(l => l.Id).Distinct().Count());
            Assert.All(S.Links, l => Assert.Equal(4, l.MinLevel)); // 기획서: 둘 다 Lv4(비주얼 2단) 이상
        }

        [Fact] public void 삼재검법을_뺀_무공과_심법은_모두_한_문파에만_속한다()
        {
            var members = S.Schools.SelectMany(sc => sc.Members).ToList();
            Assert.Equal(members.Count, members.Distinct().Count());
            var all = _d.Weapons.Select(w => w.Id).Concat(_d.Passives.Select(p => p.Id)).Where(id => id != "samjae_sword");
            foreach (var id in all) Assert.True(members.Contains(id), $"{id} 가 어느 문파에도 없음");
            Assert.Equal(31, members.Count);
            Assert.Equal("samjae_sword", S.Common.Weapon);
            Assert.Null(S.SchoolOf("samjae_sword"));
            Assert.Equal("shaolin", S.SchoolOf("geumgang")!.Id);
        }

        [Fact] public void 진전_단계는_문파_인원을_넘지_않고_오름차순이다()
        {
            Assert.Equal(9, S.Schools.Count);
            foreach (var sc in S.Schools)
            {
                var counts = sc.Tiers.Select(t => t.Count).ToList();
                Assert.Equal(counts.OrderBy(c => c), counts);
                Assert.True(counts.Max() <= sc.Members.Count, $"{sc.NameKo} 단계가 인원보다 많음");
                Assert.True(counts.Min() >= 2);
                Assert.Equal(counts.Max(), sc.MaxTierCount);
            }
        }

        [Fact] public void 문파의_출발_캐릭터는_그_문파_무공으로_시작한다()
        {
            foreach (var sc in S.Schools.Where(s => s.Character != null))
            {
                var c = _d.Characters.FirstOrDefault(x => x.Id == sc.Character);
                Assert.True(c != null, $"{sc.NameKo} 출발 캐릭터 {sc.Character} 가 없음");
                Assert.Contains(c!.StartWeapon, sc.Members);
            }
        }

        [Fact] public void 기본의_극은_삼재검법_Lv8이고_대상은_best_아니면_all()
        {
            Assert.Equal(_d.Balance.Levels.WeaponMax, S.Common.AtLevel);
            Assert.Contains(S.Common.Target, new[] { "best", "all" });
        }

        [Fact] public void 비전은_8개이고_시구_힌트가_두_줄이다()
        {
            Assert.Equal(8, S.Secrets.Count);
            foreach (var s in S.Secrets) Assert.Equal(2, s.Hint.Count);
            Assert.Equal(8, S.Secrets.Select(s => s.Id).Distinct().Count());
        }

        [Fact] public void 비전_조건은_있는_무공_심법_캐릭터_장과_판_기록만_쓴다()
        {
            var chapters = ChapterIds();
            foreach (var s in S.Secrets)
            {
                var r = s.Require;
                Assert.True(r.Levels.Count > 0 || r.Counts.Count > 0 || r.Event != null, $"{s.NameKo} 조건이 비어 있음");
                if (r.Character != null) Assert.Contains(_d.Characters, c => c.Id == r.Character);
                foreach (var ch in r.Cleared) Assert.Contains(ch, chapters);
                if (r.OnlyWeapon != null) Assert.Contains(_d.Weapons, w => w.Id == r.OnlyWeapon);
                foreach (var kv in r.Levels)
                {
                    bool weapon = _d.Weapons.Any(w => w.Id == kv.Key), passive = _d.Passives.Any(p => p.Id == kv.Key);
                    Assert.True(weapon || passive, $"{s.NameKo}: {kv.Key} 는 무공도 심법도 아님");
                    int max = weapon ? _d.Balance.Levels.WeaponMax : _d.Balance.Levels.PassiveMax;
                    Assert.InRange(kv.Value, 1, max);
                }
                foreach (var name in r.Counts.Keys) Assert.Contains(name, RunCounters.Names);
                if (r.Hold != null) { Assert.Contains(r.Hold.Counter, RunCounters.Names); Assert.True(r.Hold.Seconds > 0); }
                if (r.Event != null)
                {
                    RunCounters.ParseEvent(r.Event.Kind);
                    Assert.True(r.Event.Times >= 1);
                    if (r.Event.MaxHpRatio is float hp) Assert.InRange(hp, 0f, 1f);
                }
            }
        }

        [Fact] public void 비전_조건_글에_require의_무공_심법_이름과_레벨이_적혀_있다()
        {
            foreach (var s in S.Secrets)
            {
                foreach (var kv in s.Require.Levels)
                {
                    string name = _d.Weapons.FirstOrDefault(w => w.Id == kv.Key)?.NameKo ?? _d.Passive(kv.Key).NameKo;
                    Assert.True(s.Condition.Contains(name), $"{s.NameKo}: 조건 글에 {name} 이 없음");
                    if (kv.Value > 1) Assert.True(s.Condition.Contains($"Lv{kv.Value}"), $"{s.NameKo}: 조건 글에 Lv{kv.Value} 가 없음");
                }
                if (s.Require.Character != null) Assert.Contains(_d.Character(s.Require.Character).NameKo, s.Condition);
            }
        }

        [Fact] public void 기연_사건은_8개이고_확률은_0과_1_사이다()
        {
            Assert.Equal(8, S.Encounters.Count);
            foreach (var e in S.Encounters) Assert.InRange(e.Chance, 0.0, 1.0);
            Assert.Equal(8, S.Encounters.Select(e => e.Id).Distinct().Count());
        }

        [Fact] public void 기연_사건의_장은_지도에_있는_장이고_시간은_글과_같다()
        {
            var chapters = ChapterIds();
            foreach (var e in S.Encounters)
            {
                if (e.Chapters != null)
                {
                    Assert.NotEmpty(e.Chapters);
                    foreach (var ch in e.Chapters) Assert.Contains(ch, chapters);
                }
                if (e.MinMinute > 0) Assert.Contains($"{e.MinMinute:0}분", e.Where);
            }
            Assert.Equal(15, S.Encounter("whisper").MinMinute);
        }

        [Fact] public void 고수의_유해가_줄_문파는_지도의_장마다_20장_문파이거나_없다()
        {
            Assert.True(S.Encounter("relic").NeedsChapterSchool);
            foreach (var r in Regions().Where(r => (string)r["kind"]! == "chapter"))
            {
                var sect = (string?)r["sect"];
                if (sect != null) Assert.Contains(S.Schools, s => s.Id == sect);
            }
        }

        [Fact] public void 기획서_기연_표의_확률과_기연_능력치_값이_데이터와_같다()
        {
            var gdd = File.ReadAllText(Path.Combine(TestData.DataDir, "..", "..", "..", "..", "..", "docs", "design", "gdd.md"));
            int start = gdd.IndexOf("### 기연(奇緣) 사건");
            Assert.True(start >= 0);
            var section = gdd.Substring(start, gdd.IndexOf("### ", start + 5) - start);
            var rows = section.Split('\n').Where(l => l.StartsWith("| ") && !l.StartsWith("| 사건") && !l.StartsWith("| ---")).ToList();
            Assert.Equal(S.Encounters.Count, rows.Count);
            foreach (var row in rows)
            {
                var cols = row.Split('|').Select(c => c.Trim()).ToArray();
                var e = S.Encounters.FirstOrDefault(x => x.NameKo == cols[1]);
                Assert.True(e != null, $"기획서의 {cols[1]} 가 데이터에 없음");
                var pct = double.Parse(Regex.Match(cols[^2], @"[\d.]+").Value, CultureInfo.InvariantCulture) / 100;
                Assert.Equal(e!.Chance, pct, 6);
            }
            Assert.Contains("기연 능력치 1당 기연 사건 확률이 0.5%p", gdd);
            Assert.Equal(0.005, S.Luck.ChancePerLuck, 9);
        }
    }
}
