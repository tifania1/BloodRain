using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>docs/store/trailer_screens.md(트레일러 콘티·스크린샷 10장, ST-02)가 M2 에 실제로 있는 것만 보여 주는지.
    /// 스토어 페이지는 M2 버티컬 슬라이스 끝에 연다(기획서 17장). 그때 없는 문파·지역·경지를 찍겠다고 적으면 찍을 수가 없다.</summary>
    public class StoreShotsTests
    {
        // 기획서 17장 M2: 서장과 숭산 일대(삼류–절정, 원각), 문파 3(소림·개방·남궁).
        static readonly string[] M2Regions = { "cheongun", "songshan", "gaebang_hq", "namgung_manor" };
        static readonly string[] M2Sects = { "shaolin", "gaebang", "namgung" };
        const string PrologueBody = "jin_sowoon";
        static readonly string[] M2Realms = { "samryu", "iryu", "ilryu", "jeoljeong" };

        static string Doc() => ReadRepoFile("docs", "store", "trailer_screens.md");
        static JObject Json(string name) => JObject.Parse(File.ReadAllText(Path.Combine(DataDir, name + ".json")));

        static string Section(string heading)
        {
            var m = Regex.Match(Doc(), $"## {heading}[^\\n]*\\n(.*?)(?=\\n## |$)", RegexOptions.Singleline);
            Assert.True(m.Success, $"trailer_screens.md 에 '{heading}' 절이 없다");
            return m.Groups[1].Value;
        }

        static string[] Ids(string cell) =>
            Regex.Matches(cell, "`([a-z_]+)`").Select(m => m.Groups[1].Value).ToArray();

        internal sealed class Shot
        {
            public int No;
            public string[] Region = new string[0], Body = new string[0], Realm = new string[0],
                Weapons = new string[0], Evolution = new string[0], Ultimate = new string[0];
            /// <summary>'이펙트' 칸 글(예: "나한권 1단 먹 획, 먹 튐").</summary>
            public string Effects = "";
        }

        /// <summary>trailer_screens.md '스크린샷 10장' 표. StoreShotStageTests 도 쓴다.</summary>
        internal static List<Shot> Shots()
        {
            var rows = Regex.Matches(Section("스크린샷 10장"), @"^\| (\d+) \|(.*)\|\s*$", RegexOptions.Multiline);
            return rows.Select(r =>
            {
                var c = r.Groups[2].Value.Split('|');
                // 열: 장면 | 지역 | 몸 | 경지 | 무공 | 절기 | 오의 | 화면 | 이펙트
                return new Shot
                {
                    No = int.Parse(r.Groups[1].Value),
                    Region = Ids(c[1]), Body = Ids(c[2]), Realm = Ids(c[3]),
                    Weapons = Ids(c[4]), Evolution = Ids(c[5]), Ultimate = Ids(c[6]),
                    Effects = c[8].Trim(),
                };
            }).ToList();
        }

        [Fact]
        public void 스크린샷은_열_장이고_차례대로다()
        {
            Assert.Equal(Enumerable.Range(1, 10), Shots().Select(s => s.No));
        }

        [Fact]
        public void 스크린샷은_M2_범위_안이다()
        {
            var ch = Json("cheonha");
            var regions = ch["regions"]!.Select(r => (string)r["id"]!).ToHashSet();
            var realms = ch["realmScale"]!.Select(r => (string)r["realm"]!).ToList();
            Assert.Equal(M2Realms, realms.Take(4));
            foreach (var id in M2Regions) Assert.Contains(id, regions);

            foreach (var s in Shots())
            {
                foreach (var r in s.Region) Assert.Contains(r, M2Regions);
                foreach (var b in s.Body) Assert.True(M2Sects.Contains(b) || b == PrologueBody, $"{s.No}번 몸 {b} 는 M2 밖");
                foreach (var r in s.Realm) Assert.Contains(r, M2Realms);
                // 진소운은 서장(청운진)에서만 제 몸으로 싸운다(기획서 5장).
                if (s.Body.Contains(PrologueBody)) Assert.Equal(new[] { "cheongun" }, s.Region);
            }
        }

        [Fact]
        public void 무공_절기_오의가_데이터에_있고_몸과_맞다()
        {
            var weapons = Json("weapons")["weapons"]!.ToDictionary(w => (string)w["id"]!, w => (string)w["unlock"]!);
            var evolutions = Json("evolutions")["evolutions"]!.ToDictionary(e => (string)e["id"]!, e => (string)e["weapon"]!);
            var ultimates = Json("characters")["ultimates"]!.Select(u => (string)u["id"]!).ToHashSet();
            var bodies = Json("cheonha")["sects"]!.ToDictionary(s => (string)s["id"]!, s => ((string)s["startWeapon"]!, (string)s["ultimate"]!));
            var jin = Json("characters")["characters"]!.First(c => (string)c["id"]! == PrologueBody);
            bodies[PrologueBody] = ((string)jin["startWeapon"]!, (string)jin["ultimate"]!);

            foreach (var s in Shots())
            {
                foreach (var w in s.Weapons)
                {
                    Assert.True(weapons.ContainsKey(w), $"{s.No}번 무공 {w} 가 weapons.json 에 없다");
                    // M2 에서 레벨업에 나오는 것은 처음부터 열린 무공뿐(boss:<지역> 은 M3 이후 땅의 주인).
                    Assert.True(weapons[w] == "start", $"{s.No}번 무공 {w} 는 '{weapons[w]}' 로 열려 M2 화면에 없을 수 있다");
                }
                foreach (var e in s.Evolution)
                {
                    Assert.True(evolutions.ContainsKey(e), $"{s.No}번 절기 {e} 가 evolutions.json 에 없다");
                    Assert.Contains(evolutions[e], s.Weapons);
                }
                foreach (var u in s.Ultimate) Assert.Contains(u, ultimates);
                if (s.Body.Length == 1)
                {
                    var (start, ult) = bodies[s.Body[0]];
                    Assert.Contains(start, s.Weapons);
                    foreach (var u in s.Ultimate) Assert.Equal(ult, u);
                }
            }
        }

        [Fact]
        public void 트레일러와_스크린샷에_M3_이후_이름이_없다()
        {
            // 콘티 표와 스크린샷 표에 M3 이후 문파·땅의 주인 이름이 나오면 M2 빌드로는 찍을 수 없다.
            var later = new[] { "당가", "수로채", "무당", "화산", "곤륜", "마교", "당소혜", "마천패", "혈허진인", "낙매검선", "설무향", "혁련휘", "단목현" };
            var text = Section("60초 트레일러 콘티") + Section("스크린샷 10장");
            foreach (var name in later) Assert.DoesNotContain(name, text);
            Assert.Contains("원각", text);
        }

        [Fact]
        public void 콘티는_60초를_빈틈없이_잇는다()
        {
            var spans = Regex.Matches(Section("60초 트레일러 콘티"), @"^\| (\d):(\d\d)–(\d):(\d\d) \|", RegexOptions.Multiline)
                .Select(m => (from: int.Parse(m.Groups[1].Value) * 60 + int.Parse(m.Groups[2].Value),
                              to: int.Parse(m.Groups[3].Value) * 60 + int.Parse(m.Groups[4].Value))).ToList();
            Assert.NotEmpty(spans);
            Assert.Equal(0, spans[0].from);
            for (int i = 1; i < spans.Count; i++) Assert.Equal(spans[i - 1].to, spans[i].from);
            Assert.Equal(60, spans[^1].to);
        }
    }
}
