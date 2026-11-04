using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>화면 문구를 키로(backlog L-05). Presentation 은 클라우드에서 컴파일하지 못하니
    /// 코드가 부르는 키의 꼴과 능력치 이름 표만이라도 여기서 데이터·원문과 맞춰 본다.</summary>
    public class ScreenLocalizationTests
    {
        static readonly StringCatalog Catalog =
            StringCatalog.FromData(ReadFiles(), ReadText(Path.Combine(DataDir, "strings", "ko.json")));

        static string PresentationDir => Path.Combine(RepoRoot, "unity", "BloodRain", "Assets", "Scripts", "Presentation");

        /// <summary>Loc.T($"weapons.{w.Id}.name", …) 처럼 자리가 섞인 키. (파일, 틀)</summary>
        static List<(string file, string template)> Templates() =>
            Directory.GetFiles(PresentationDir, "*.cs", SearchOption.AllDirectories)
                .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"\b(?:Loc\.)?[TF]\(\s*\$""([^""]+)""")
                    .Select(m => (file: Path.GetFileName(f), template: m.Groups[1].Value)))
                .Distinct().ToList();

        static Regex ToRegex(string template) =>
            new Regex("^" + string.Join("[^.]+", Regex.Split(template, @"\{[^}]+\}").Select(Regex.Escape)) + "$");

        [Fact]
        public void 자리가_섞인_키도_원문에_그런_꼴이_있다()
        {
            var templates = Templates();
            Assert.Contains(templates, t => t.template == "cheonha.sects.{s.Id}.name");
            var keys = Catalog.Korean.Keys.ToList();
            var bad = templates.Where(t => !keys.Any(ToRegex(t.template).IsMatch)).Select(t => $"{t.file}: {t.template}").ToList();
            Assert.True(bad.Count == 0, "원문(데이터 JSON·strings/ko.json)에 이런 꼴의 키가 없다:\n" + string.Join("\n", bad));
        }

        [Fact]
        public void 능력치_이름은_연무장과_타고난_성질이_쓰는_것을_다_갖는다()
        {
            var data = Load();
            var used = data.Balance.Training.Items.Select(i => i.Stat)
                .Concat(data.Cheonha.BirthTraits.List.SelectMany(t => t.Stats.Keys))
                .Distinct().ToList();
            Assert.NotEmpty(used);
            var missing = used.Where(s => !Catalog.Korean.ContainsKey($"ui.stat.{s}")).ToList();
            Assert.True(missing.Count == 0, "strings/ko.json 에 ui.stat.<이름> 이 없다: " + string.Join(", ", missing));
        }

        [Fact]
        public void 능력치_이름은_StatKind_전부와_데이터가_쓰는_능력치를_다_갖는다()
        {
            // 심법 부가 능력치·문파 고유 능력(회피)도 능력치 줄(LifeRunner 디버그, 연무장, 침방)에 나올 수 있다.
            var data = Load();
            var used = System.Enum.GetNames(typeof(BloodRain.Core.Build.StatKind))
                .Concat(data.Passives.SelectMany(p => p.Effects.Select(e => e.Stat)))
                .Concat(data.Cheonha.Sects.Where(s => s.TraitEffect != null)
                    .SelectMany(s => s.TraitEffect!.Stats.Keys.Concat(s.TraitEffect.AboveHealthStats.Keys).Concat(s.TraitEffect.MovingStats.Keys)))
                .Concat(data.Synergies.Schools.SelectMany(s => s.Tiers.SelectMany(t => t.Stats.Keys)))
                .Distinct().ToList();
            var missing = used.Where(s => !Catalog.Korean.ContainsKey($"ui.stat.{s}")).ToList();
            Assert.True(missing.Count == 0, "strings/ko.json 에 ui.stat.<이름> 이 없다: " + string.Join(", ", missing));
        }

        [Fact]
        public void 화면_코드는_ui_키에_두_인자_T를_쓰지_않는다()
        {
            // ui. 키의 한국어 원문은 strings/ko.json 에 있다. T("ui.…", x) 꼴은 한국어 화면에서 원문 대신 x 를 돌려줘서
            // 침방 성질 줄에 'Might' 같은 영어 키가 그대로 나왔다(2026-11-05). ui. 키는 한 인자 T 로 부른다.
            var bad = Directory.GetFiles(PresentationDir, "*.cs", SearchOption.AllDirectories)
                .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"\bT\(\s*\$?""ui\.[^""]*""\s*,")
                    .Cast<Match>().Select(m => $"{Path.GetFileName(f)}: {m.Value}"))
                .ToList();
            Assert.True(bad.Count == 0, "ui. 키를 두 인자 T 로 부른다:\n" + string.Join("\n", bad));
        }

        [Fact]
        public void 문파_제자_이름은_Core_와_화면이_같은_꼴이다()
        {
            // Core Rebirth.CharacterOf 의 NameKo 는 한국어 원문이고, 화면은 ui.rebirth.disciple 로 옮긴 이름을 쓴다(Loc.Disciple).
            var ko = Strings.Load("ko", Catalog, null);
            foreach (var sect in Load().Cheonha.Sects)
                Assert.Equal(ko.Format("ui.rebirth.disciple", sect.NameKo), Rebirth.CharacterOf(sect).NameKo);
        }

        [Theory]
        [InlineData("Meta/SettlementScreen.cs")]
        [InlineData("Meta/ChimbangScreen.cs")]
        [InlineData("Meta/LibraryScreen.cs")]
        [InlineData("Meta/TrainingScreen.cs")]
        [InlineData("Meta/MetaShop.cs")]
        [InlineData("Meta/ManualNames.cs")]
        [InlineData("Meta/ChimbangMap.cs")]
        [InlineData("Hud/RegionHud.cs")]
        [InlineData("Hud/LevelUpScreen.cs")]
        public void 옮긴_화면에는_화면에_찍는_한글이_남지_않는다(string file)
        {
            // 주석·Tooltip·Header·Debug.Log·오브젝트 이름(new GameObject, Text("이름", …)·MakeText(부모, "이름", …) 의 이름 칸)은 화면에 안 나온다.
            // 데이터에서 한국어 이름으로 찾는 곳(RealmByName("절정"))과 Loc 의 '없을 때 한국어' 인자도 뺀다.
            var lines = File.ReadAllLines(Path.Combine(PresentationDir, file));
            var bad = new List<string>();
            for (int i = 0; i < lines.Length; i++)
            {
                var line = Regex.Replace(lines[i], @"//.*$", "");
                if (Regex.IsMatch(line, @"\[(Tooltip|Header)\(|Debug\.Log|RealmByName\(")) continue;
                line = Regex.Replace(line, @"new GameObject\(\$?""[^""]*""", "");
                line = Regex.Replace(line, @"\b(Text|Rect|Img|MakeButton|MakeImage|MakeSprite)\(\$?""[^""]*""", "");
                line = Regex.Replace(line, @"\b(MakeButton|MakeImage|MakeText)\([\w.]+,\s*\$?""[^""]*""", "");
                line = Regex.Replace(line, @"""[^""]*""\s*\+\s*r\.NameKo", "");
                line = Regex.Replace(line, @"Loc\.T\(""[a-z_.]+"",\s*""[^""]*""\)", "");
                foreach (Match m in Regex.Matches(line, @"""([^""]*)"""))
                    if (Regex.IsMatch(m.Groups[1].Value, @"[가-힣]")) bad.Add($"{i + 1}: {lines[i].Trim()}");
            }
            Assert.True(bad.Count == 0, $"{file} 에 화면에 찍힐 한글 문구가 남았다(strings/ko.json 의 ui. 키로):\n" + string.Join("\n", bad.Distinct()));
        }
    }
}
