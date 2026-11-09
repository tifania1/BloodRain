using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BloodRain.Core.Data;
using Newtonsoft.Json.Linq;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>TT-02 크레딧(credits.json). 게임에 실제로 들어간 글꼴·패키지·소리마다 고지가 있는지,
    /// 글이 네 언어로 다 있는지 본다. 고지 내용과 약관 출처는 docs/design/credits.md.</summary>
    public class CreditsTests
    {
        static string AssetsDir => Path.Combine(RepoRoot, "unity", "BloodRain", "Assets");
        static string StringsDir => Path.Combine(DataDir, "strings");
        readonly CreditsDef _c = Load().Credits;

        static string Norm(string s) => Regex.Replace(s, @"\s+", " ").Trim();
        static readonly Regex Hangul = new Regex("[가-힣]");

        /// <summary>Assets 아래 글꼴 파일(.ttf·.otf) 전부. TextMesh Pro 기본 글꼴도 Resources 에 있어 빌드에 들어간다.</summary>
        static List<string> FontFiles() =>
            Directory.GetFiles(AssetsDir, "*.*", SearchOption.AllDirectories)
                .Where(p => p.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".otf", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.Ordinal).ToList();

        CreditSectionDef Fonts => _c.Sections.Single(s => s.Id == "fonts");

        [Fact] public void 크레딧을_읽는다()
        {
            Assert.Contains("credits", GameData.FileNames);
            Assert.Equal("血雨江湖", _c.Hanja);
            Assert.Equal("플레이해 주셔서 고맙습니다", _c.ThanksKo);
            // 만든 사람 이름은 사람이 나중에 정한다(2026-11-09). 그때까지 빈 값.
            Assert.Contains(_c.Makers, m => m.RoleKo == "만든 사람");
            Assert.Equal(new[] { "fonts", "software" }, _c.Sections.Select(s => s.Id));
        }

        [Fact] public void 들어_있는_글꼴_파일마다_고지가_있다()
        {
            var files = FontFiles();
            Assert.True(files.Count >= 5, "글꼴 파일을 못 찾았다");
            foreach (var path in files)
            {
                var file = Path.GetFileName(path);
                var item = Fonts.Items.SingleOrDefault(i => i.Covers.Contains(file));
                Assert.True(item != null, $"{file}: credits.json fonts 에 이 파일을 고지하는 줄이 없다(covers 에 파일 이름). 저작권 줄은 글꼴 파일 이름표 0번, 라이선스는 13번을 본다 — docs/design/credits.md");

                var names = NameTable.Read(path);
                Assert.Equal(names[1], item!.Name);
                foreach (var line in names[0].Split('\n').Select(l => l.Trim().TrimEnd('.')).Where(l => l.Length > 0))
                    Assert.True(Norm(item.Copyright).Contains(Norm(line)), $"{file}: 저작권 줄에 글꼴 안 저작권 '{line}' 이 없다");
                Assert.Contains("Open Font License", names[13]);
                Assert.Equal("ofl", item.License);
                // OFL 은 글꼴과 함께 라이선스 원문을 둬야 한다. 저장소에서는 글꼴 옆 *OFL*.txt.
                Assert.True(Directory.GetFiles(Path.GetDirectoryName(path)!, "*OFL*.txt").Length > 0, $"{file}: 옆에 OFL 원문 파일이 없다");
            }
            var onDisk = files.Select(Path.GetFileName).ToHashSet();
            Assert.All(Fonts.Items.SelectMany(i => i.Covers), f => Assert.True(onDisk.Contains(f), $"credits.json fonts 의 {f} 가 Assets 에 없다(뺀 글꼴이면 줄도 지운다)"));
        }

        [Fact] public void OFL_원문은_글꼴_폴더의_OFL_파일과_같다()
        {
            var ofl = _c.License("ofl");
            Assert.NotNull(ofl);
            Assert.Equal("SIL Open Font License, Version 1.1", ofl!.Name);
            foreach (var txt in Directory.GetFiles(AssetsDir, "*OFL*.txt", SearchOption.AllDirectories))
            {
                var t = ReadText(txt);
                var body = t.Substring(t.IndexOf("SIL OPEN FONT LICENSE Version 1.1", StringComparison.Ordinal));
                body = string.Join("\n", body.Split('\n').Where(l => !Regex.IsMatch(l.Trim(), "^-{5,}$")));
                Assert.Equal(Norm(body), Norm(ofl.Text));
            }
        }

        [Fact] public void 표기가_필요한_패키지마다_고지가_있다()
        {
            // Unity 패키지(com.unity.*)는 Unity Companion License 라 고지가 없어도 된다. 다만 com.unity.nuget.* 은 남의 라이브러리(MIT 등)를 묶은 것이라 고지한다.
            // 그 밖의 패키지는 MIT 처럼 고지가 필요한 라이선스가 대부분이라 하나씩 credits 에 적는다(빌드에 안 들어가는 것도 확인 뒤 적는다).
            var deps = ((JObject)JObject.Parse(ReadRepoFile("unity", "BloodRain", "Packages", "manifest.json"))["dependencies"]!).Properties().Select(p => p.Name).ToList();
            var need = deps.Where(id => !id.StartsWith("com.unity.", StringComparison.Ordinal) || id.StartsWith("com.unity.nuget.", StringComparison.Ordinal)).ToList();
            Assert.Contains("com.unity.nuget.newtonsoft-json", need);
            foreach (var id in need)
                Assert.True(_c.Covers(id), $"manifest.json 의 {id}: credits.json software 에 고지 줄이 없다(라이선스·저작권 줄을 패키지 LICENSE·Third Party Notices 에서 옮김, docs/design/credits.md)");
            Assert.True(_c.Covers("unity"));
            foreach (var cov in _c.Sections.Single(s => s.Id == "software").Items.SelectMany(i => i.Covers).Where(c => c != "unity"))
                Assert.True(deps.Contains(cov), $"credits.json software 의 {cov} 가 manifest.json 에 없다(뺀 패키지면 줄도 지운다)");
        }

        [Fact] public void 게임에_든_소리_갈래마다_고지를_다시_본다()
        {
            // 코드로 합성한 효과음(synth)은 고지할 것이 없다. AI(Stable Audio)·층 쌓기(녹음 재료)·배경음(Gemini)이 들어오면
            // docs/design/credits.md '소리와 음악' 절의 준비한 줄을 credits.json 에 넣는다(약관상 표기 의무가 없어도, 넣었는지 한 번 보게 하려고 막는다).
            var sfx = JObject.Parse(ReadText(Path.Combine(AssetsDir, "Audio", "SFX", "sfx.json")));
            var routes = sfx["sounds"]!.Select(s => (string)s["route"]!).Distinct().ToList();
            var present = routes.Where(r => r != "synth").Select(r => "sfx:" + r).ToList();
            var bgmDir = Path.Combine(AssetsDir, "Audio", "BGM");
            if (Directory.Exists(bgmDir) && Directory.GetFiles(bgmDir, "*.ogg").Length > 0) present.Add("bgm:gemini");
            foreach (var p in present)
                Assert.True(_c.Covers(p), $"게임에 {p} 소리가 들어왔다. docs/design/credits.md '소리와 음악' 절을 보고 credits.json 에 그 줄을 넣는다(covers 에 \"{p}\")");
            var covered = _c.Items.SelectMany(i => i.Covers).Where(c => c.StartsWith("sfx:", StringComparison.Ordinal) || c.StartsWith("bgm:", StringComparison.Ordinal));
            Assert.All(covered, c => Assert.True(present.Contains(c), $"credits.json 의 {c} 소리가 게임에 아직 없다"));
        }

        [Fact] public void 라이선스_참조가_맞고_원문은_쓰이는_것만()
        {
            Assert.All(_c.Items, i => Assert.True(i.License == "" || _c.License(i.License) != null, $"{i.Id}: 없는 라이선스 {i.License}"));
            Assert.All(_c.Licenses, l => Assert.Contains(_c.Items, i => i.License == l.Id));
            Assert.All(_c.Items.Where(i => i.License == "mit"), i => Assert.StartsWith("Copyright (c) ", i.Copyright));
            var mit = _c.License("mit")!.Text;
            Assert.Contains("Permission is hereby granted, free of charge", mit);
            Assert.Contains("The above copyright notice and this permission notice shall be included", mit);
            Assert.Equal(_c.Items.Count(), _c.Items.Select(i => i.Id).Distinct().Count());
        }

        [Fact] public void 고유_이름과_원문은_옮기지_않는다()
        {
            // 한글이 들면 StringCatalog 가 옮길 문구로 잡는다. 이름·저작권 줄·라이선스 원문에는 한글을 쓰지 않는다.
            // (만든 사람 이름은 예외 — 한글로 넣으면 영어·중국어 표기를 strings 에 따로 넣어야 테스트가 통과한다.)
            foreach (var i in _c.Items)
                Assert.DoesNotMatch(Hangul, i.Name + i.Copyright + i.License + string.Join("", i.Covers));
            Assert.All(_c.Licenses, l => Assert.DoesNotMatch(Hangul, l.Name + l.Text));
        }

        [Fact] public void 화면_글은_네_언어로_다_있다()
        {
            var catalog = StringCatalog.FromData(ReadFiles(), ReadText(Path.Combine(StringsDir, "ko.json")));
            var keys = new List<(string key, string ko)>();
            keys.AddRange(_c.Makers.Select(m => (CreditsDef.MakerRoleKey(m), m.RoleKo)));
            foreach (var s in _c.Sections)
            {
                keys.Add((CreditsDef.SectionTitleKey(s), s.TitleKo));
                keys.AddRange(s.Items.Select(i => (CreditsDef.ItemUseKey(s, i), i.UseKo)));
            }
            keys.Add((CreditsDef.LicensesTitleKey, _c.LicensesTitleKo));
            keys.Add((CreditsDef.ThanksKey, _c.ThanksKo));

            var fromData = catalog.Korean.Keys.Where(k => k.StartsWith("credits.", StringComparison.Ordinal)).OrderBy(k => k, StringComparer.Ordinal);
            Assert.Equal(fromData, keys.Select(k => k.key).OrderBy(k => k, StringComparer.Ordinal));
            foreach (var (key, ko) in keys)
            {
                Assert.Equal(ko, catalog.Korean[key]);
                foreach (var lang in Strings.Languages.Where(l => l != "ko"))
                {
                    var table = StringCatalog.ReadTable(ReadText(Path.Combine(StringsDir, lang + ".json")));
                    Assert.True(table.TryGetValue(key, out var v) && v.Length > 0, $"{lang}: {key} 가 없다");
                }
            }
            foreach (var lang in Strings.Languages.Where(l => l != "ko"))
                Assert.Contains("credits", JObject.Parse(ReadText(Path.Combine(StringsDir, lang + ".json")))["_meta"]!["complete"]!.Select(t => (string)t!));
        }

        [Fact] public void 흘리는_줄은_제목에서_끝_인사까지_빈_이름을_건너뛴다()
        {
            string Tr(string key, string ko) => ko;
            var roll = _c.Roll(Tr);
            Assert.Equal(new CreditLine(CreditLineKind.Title, "血雨江湖").ToString(), roll[0].ToString());
            Assert.Equal(CreditLineKind.Thanks, roll[^1].Kind);
            Assert.Equal("플레이해 주셔서 고맙습니다", roll[^1].Text);
            Assert.All(roll, l => Assert.False(string.IsNullOrWhiteSpace(l.Text)));
            Assert.DoesNotContain(roll, l => l.Text == "만든 사람");     // 이름이 비어 있어 묶음째 건너뜀
            Assert.Equal(new[] { "글꼴", "엔진과 라이브러리", "라이선스 원문" }, roll.Where(l => l.Kind == CreditLineKind.Heading).Select(l => l.Text));
            Assert.Contains(roll, l => l.Kind == CreditLineKind.Name && l.Text == "Noto Serif KR");
            Assert.Equal(2, roll.Count(l => l.Kind == CreditLineKind.LicenseText));

            // 이름을 넣으면 제목 바로 아래에 '만든 사람'과 이름이 나온다.
            var named = new CreditsDef { Hanja = _c.Hanja, Sections = _c.Sections, Licenses = _c.Licenses, LicensesTitleKo = _c.LicensesTitleKo, ThanksKo = _c.ThanksKo,
                Makers = new List<CreditMakerDef> { new CreditMakerDef { Id = "maker", RoleKo = "만든 사람", Name = "Someone" } } };
            var r2 = named.Roll(Tr);
            Assert.Equal((CreditLineKind.Heading, "만든 사람"), (r2[1].Kind, r2[1].Text));
            Assert.Equal((CreditLineKind.Name, "Someone"), (r2[2].Kind, r2[2].Text));

            // 옮긴 글을 받는다.
            var en = _c.Roll((k, ko) => k == CreditsDef.ThanksKey ? "Thank you for playing" : ko);
            Assert.Equal("Thank you for playing", en[^1].Text);
        }

        [Fact] public void 기획서_12장_화면_목록에_크레딧이_있다()
        {
            var rows = GddTable(GddSection("화면 목록")).ToDictionary(r => r[0], r => r);
            Assert.Contains("크레딧", rows["타이틀"][1]);
            Assert.True(rows.ContainsKey("크레딧"), "기획서 12장 화면 목록에 '크레딧' 줄이 없다");
            Assert.Contains("credits.json", rows["크레딧"][2]);
        }

        /// <summary>OpenType 이름표(name)에서 Windows 영어(3·1·0x409) 이름을 읽는다. 없으면 Mac 로마(1·0).</summary>
        internal static class NameTable
        {
            public static Dictionary<int, string> Read(string path)
            {
                var b = File.ReadAllBytes(path);
                int numTables = U16(b, 4), table = -1;
                for (int i = 0; i < numTables; i++)
                {
                    int rec = 12 + i * 16;
                    if (Encoding.ASCII.GetString(b, rec, 4) == "name") table = (int)U32(b, rec + 8);
                }
                if (table < 0) throw new InvalidDataException(path + ": name 표 없음");
                int count = U16(b, table + 2), strings = table + U16(b, table + 4);
                var win = new Dictionary<int, string>();
                var mac = new Dictionary<int, string>();
                for (int i = 0; i < count; i++)
                {
                    int r = table + 6 + i * 12;
                    int platform = U16(b, r), encoding = U16(b, r + 2), language = U16(b, r + 4), nameId = U16(b, r + 6);
                    int length = U16(b, r + 8), offset = U16(b, r + 10);
                    if (platform == 3 && encoding == 1 && language == 0x409)
                        win[nameId] = Encoding.BigEndianUnicode.GetString(b, strings + offset, length);
                    else if (platform == 1 && encoding == 0 && language == 0)
                        mac[nameId] = Encoding.Latin1.GetString(b, strings + offset, length);
                }
                foreach (var kv in mac) if (!win.ContainsKey(kv.Key)) win[kv.Key] = kv.Value;
                return win;
            }

            static int U16(byte[] b, int p) => (b[p] << 8) | b[p + 1];
            static uint U32(byte[] b, int p) => (uint)((b[p] << 24) | (b[p + 1] << 16) | (b[p + 2] << 8) | b[p + 3]);
        }
    }
}
