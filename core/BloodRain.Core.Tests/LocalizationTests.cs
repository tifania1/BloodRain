using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BloodRain.Core.Data;
using Newtonsoft.Json.Linq;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>현지화 틀(backlog L-01). 데이터 JSON 의 한국어가 원문이고, 영어·간체는 Resources/Data/strings/&lt;lang&gt;.json.
    /// 남는 키(원문 없는 키)는 늘 막고, 빠진 키는 그 언어 파일 _meta.complete 에 적은 갈래만 막는다(옮기는 중인 갈래는 한국어로 나온다).</summary>
    public class LocalizationTests
    {
        static string StringsDir => Path.Combine(DataDir, "strings");
        static string FontDir => Path.Combine(RepoRoot, "unity", "BloodRain", "Assets", "Art", "Fonts");

        static readonly Lazy<StringCatalog> Catalog = new Lazy<StringCatalog>(() =>
            StringCatalog.FromData(ReadFiles(), ReadText(Path.Combine(StringsDir, "ko.json"))));

        static string LangJson(string lang) => ReadText(Path.Combine(StringsDir, lang + ".json"));
        static Strings Lang(string lang) => Strings.Load(lang, Catalog.Value, LangJson(lang));
        static JObject Meta(string lang) => (JObject)JObject.Parse(LangJson(lang))["_meta"]!;
        static IEnumerable<string> Others => Strings.Languages.Where(l => l != "ko");

        public static IEnumerable<object[]> OtherLanguages => Strings.Languages.Where(l => l != "ko").Select(l => new object[] { l });

        [Fact]
        public void 키는_파일_id_필드로_만든다()
        {
            var ko = Catalog.Value.Korean;
            Assert.Equal("삼재검법", ko["weapons.samjae_sword.name"]);
            Assert.Equal("앞뒤 베기", ko["weapons.samjae_sword.milestones.4"]);
            Assert.Equal(TestData.Load().Enemies.First(e => e.Id == "sigang").NameKo, ko["enemies.sigang.name"]);
            Assert.EndsWith("원각", ko["enemies.bosses.wongak.name"]);
            Assert.True(ko.ContainsKey("cheonha.regions.songshan.name"));
            Assert.True(ko.ContainsKey("cheonha.realmScale.0.foe"));
            Assert.True(ko.ContainsKey("bosspatterns.bosses.wongak.patterns.vajra_cone.name"));
            Assert.True(ko.ContainsKey("synergies.secrets.nakmae_geomseon.hint.1"));
            Assert.True(ko.ContainsKey("insights.ilgeom.text"));
        }

        [Fact]
        public void 기획_메모와_한자_칸은_옮기지_않는다()
        {
            var keys = Catalog.Value.Korean.Keys.ToList();
            Assert.DoesNotContain(keys, k => k.Contains("._"));
            Assert.DoesNotContain(keys, k => k.EndsWith(".hanja", StringComparison.Ordinal));
            Assert.DoesNotContain(keys, k => k.EndsWith("Ko", StringComparison.Ordinal));
        }

        [Fact]
        public void 이름_있는_데이터는_모두_카탈로그에_있다()
        {
            var d = TestData.Load();
            var ko = Catalog.Value.Korean;
            foreach (var w in d.Weapons) Assert.Equal(w.NameKo, ko[$"weapons.{w.Id}.name"]);
            foreach (var p in d.Passives) Assert.Equal(p.NameKo, ko[$"passives.{p.Id}.name"]);
            foreach (var e in d.Evolutions) Assert.Equal(e.NameKo, ko[$"evolutions.{e.Id}.name"]);
            foreach (var c in d.Characters) Assert.Equal(c.NameKo, ko[$"characters.{c.Id}.name"]);
            foreach (var u in d.Ultimates) Assert.Equal(u.NameKo, ko[$"characters.ultimates.{u.Id}.name"]);
            foreach (var r in d.Cheonha.Regions) Assert.Equal(r.NameKo, ko[$"cheonha.regions.{r.Id}.name"]);
            foreach (var s in d.Cheonha.Sects) Assert.Equal(s.NameKo, ko[$"cheonha.sects.{s.Id}.name"]);
            foreach (var i in d.Insights) Assert.Equal(i.NameKo, ko[$"insights.{i.Id}.name"]);
        }

        [Fact]
        public void 한국어_UI_파일에는_ui_키만_있다()
        {
            var table = StringCatalog.ReadTable(LangJson("ko"));
            Assert.NotEmpty(table);
            Assert.All(table.Keys, k => Assert.StartsWith(StringCatalog.UiPrefix, k));
            var bad = Assert.Throws<InvalidOperationException>(() =>
                StringCatalog.FromData(ReadFiles(), "{\"weapons.samjae_sword.name\": \"x\"}"));
            Assert.Contains("weapons.samjae_sword.name", bad.Message);
        }

        [Fact]
        public void 언어마다_파일이_있고_meta_lang_이_파일_이름과_같다()
        {
            foreach (var lang in Strings.Languages)
            {
                Assert.True(File.Exists(Path.Combine(StringsDir, lang + ".json")), $"strings/{lang}.json 이 없다");
                Assert.Equal(lang, (string?)Meta(lang)["lang"]);
            }
            var onDisk = Directory.GetFiles(StringsDir, "*.json").Select(Path.GetFileNameWithoutExtension).OrderBy(n => n, StringComparer.Ordinal);
            Assert.Equal(Strings.Languages.OrderBy(n => n, StringComparer.Ordinal), onDisk);
        }

        [Theory, MemberData(nameof(OtherLanguages))]
        public void 남는_키가_없다(string lang)
        {
            var extra = Lang(lang).Extra().ToList();
            Assert.True(extra.Count == 0, $"{lang}: 원문이 없는 키(철자·지워진 원문):\n" + string.Join("\n", extra));
        }

        [Theory, MemberData(nameof(OtherLanguages))]
        public void 다_옮긴_갈래에는_빠진_키가_없다(string lang)
        {
            var complete = Meta(lang)["complete"]!.Select(t => (string)t!).ToList();
            var sections = Catalog.Value.Sections.ToHashSet();
            Assert.All(complete, s => Assert.True(sections.Contains(s), $"{lang}: complete 에 없는 갈래 '{s}'"));
            var missing = Lang(lang).Missing().Where(k => complete.Contains(StringCatalog.Section(k))).ToList();
            Assert.True(missing.Count == 0, $"{lang}: 다 옮겼다고 적은 갈래에 빠진 키. strings/{lang}.json 에 옮긴 문구를 넣는다(용어는 docs/design/glossary.md). 지금 못 옮기면 그 파일 _meta.complete 에서 갈래를 빼고 backlog 에 적는다:\n" + string.Join("\n", missing));
        }

        [Theory, MemberData(nameof(OtherLanguages))]
        public void 옮긴_문구의_자리표시는_원문과_같다(string lang)
        {
            var ko = Catalog.Value.Korean;
            var bad = StringCatalog.ReadTable(LangJson(lang))
                .Where(kv => ko.ContainsKey(kv.Key))
                .Where(kv => !StringCatalog.Placeholders(kv.Value).SequenceEqual(StringCatalog.Placeholders(ko[kv.Key])))
                .Select(kv => $"{kv.Key}: '{ko[kv.Key]}' → '{kv.Value}'").ToList();
            Assert.True(bad.Count == 0, $"{lang}: {{0}} 자리가 원문과 다르다:\n" + string.Join("\n", bad));
        }

        [Theory, MemberData(nameof(OtherLanguages))]
        public void 옮긴_문구에_한글이_남지_않는다(string lang)
        {
            var bad = StringCatalog.ReadTable(LangJson(lang))
                .Where(kv => kv.Value.Any(ch => ch >= '가' && ch <= '힣'))
                .Select(kv => kv.Key).ToList();
            Assert.True(bad.Count == 0, $"{lang}: 한글이 남은 값:\n" + string.Join("\n", bad));
        }

        [Fact]
        public void 옮긴_값이_없으면_한국어_그다음_키가_나온다()
        {
            var cat = Catalog.Value;
            var en = Strings.Load("en", cat, "{\"_meta\":{}, \"ui.levelup.skip\": \"Train ({0})  S\", \"ui.levelup.none\": \"\"}");
            Assert.Equal("Train (2)  S", en.Format("ui.levelup.skip", 2));
            Assert.Equal("없음", en.Get("ui.levelup.none"));            // 빈 값은 없는 것으로
            Assert.Equal("삼재검법", en.Get("weapons.samjae_sword.name"));
            Assert.Equal("ui.no_such", en.Get("ui.no_such"));
            Assert.Equal("직접", en.Get("ui.no_such", "직접"));
            Assert.Contains("ui.levelup.none", en.Missing());
            var ko = Strings.Load("ko", cat, null);
            Assert.Empty(ko.Missing());
            Assert.Equal("1 → 2 단계", ko.Format("ui.levelup.level_up", 1, 2));
        }

        [Fact]
        public void 화면_코드가_부르는_UI_키는_원문에_있다()
        {
            // Presentation 은 클라우드에서 컴파일하지 못하니 키 철자만이라도 여기서 본다. Loc.T("ui.…") · Loc.F("ui.…", …) 꼴의 글자 그대로인 키.
            var dir = Path.Combine(RepoRoot, "unity", "BloodRain", "Assets", "Scripts", "Presentation");
            var used = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories)
                .SelectMany(f => System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(f), @"Loc\.[TF]\(\s*""(ui\.[^""]+)""")
                    .Select(m => (file: Path.GetFileName(f), key: m.Groups[1].Value)))
                .ToList();
            Assert.NotEmpty(used);
            var missing = used.Where(u => !Catalog.Value.Korean.ContainsKey(u.key)).Select(u => $"{u.file}: {u.key}").Distinct().ToList();
            Assert.True(missing.Count == 0, "strings/ko.json 에 없는 UI 키:\n" + string.Join("\n", missing));
        }

        [Fact]
        public void 자리표시는_번호만_센다()
        {
            Assert.Equal(new[] { 0, 1 }, StringCatalog.Placeholders("{1} 와 {0} 그리고 {0}"));
            Assert.Equal(new[] { 0 }, StringCatalog.Placeholders("기혈 +{0:0}"));
            Assert.Empty(StringCatalog.Placeholders("{{0}} 은 자리가 아니다"));
        }

        // ── 글꼴(기획서 12장): 한국어·영어는 Noto Serif KR(없는 한자는 Yuji Boku), 간체는 Noto Serif SC ──

        static readonly Lazy<HashSet<int>> Kr = new Lazy<HashSet<int>>(() => FontCoverageTests.Cmap.Read(Path.Combine(FontDir, "NotoSerifKR-Regular.otf")));
        static readonly Lazy<HashSet<int>> Brush = new Lazy<HashSet<int>>(() => FontCoverageTests.Cmap.Read(Path.Combine(FontDir, "YujiBoku-Regular.ttf")));
        static readonly Lazy<HashSet<int>> Sc = new Lazy<HashSet<int>>(() => FontCoverageTests.Cmap.Read(Path.Combine(FontDir, "NotoSerifSC-Regular.otf")));

        static List<string> Uncovered(IEnumerable<string> texts, Func<int, bool> covered) =>
            texts.SelectMany(t => Enumerable.Range(0, t.Length).Where(i => !char.IsLowSurrogate(t[i])).Select(i => char.ConvertToUtf32(t, i)))
                .Where(cp => cp > 0x20 && !covered(cp)).Distinct().Select(cp => $"{char.ConvertFromUtf32(cp)} (U+{cp:X4})").ToList();

        [Fact]
        public void 간체_글꼴과_라이선스가_있다()
        {
            Assert.True(File.Exists(Path.Combine(FontDir, "NotoSerifSC-OFL.txt")));
            Assert.True(Sc.Value.Count > 20000, $"Noto Serif SC 글자 수 {Sc.Value.Count}");
            foreach (var ch in "血雨江湖少林武当华山剑气内功真气境界ABCxyz0123456789%+-×·（）：，。")
                Assert.Contains((int)ch, Sc.Value);
        }

        [Fact]
        public void 한국어_UI_문구는_본문_글꼴이나_대체_글꼴로_찍힌다()
        {
            var miss = Uncovered(StringCatalog.ReadTable(LangJson("ko")).Values, cp => Kr.Value.Contains(cp) || Brush.Value.Contains(cp));
            Assert.True(miss.Count == 0, "글꼴에 없는 글자: " + string.Join(", ", miss));
        }

        [Fact]
        public void 영어_문구는_본문_글꼴로_찍힌다()
        {
            var miss = Uncovered(StringCatalog.ReadTable(LangJson("en")).Values, cp => Kr.Value.Contains(cp));
            Assert.True(miss.Count == 0, "Noto Serif KR 에 없는 글자: " + string.Join(", ", miss));
        }

        [Fact]
        public void 간체_문구는_간체_글꼴로_찍힌다()
        {
            var miss = Uncovered(StringCatalog.ReadTable(LangJson("zh-Hans")).Values, cp => Sc.Value.Contains(cp));
            Assert.True(miss.Count == 0, "Noto Serif SC 에 없는 글자: " + string.Join(", ", miss));
        }
    }
}
