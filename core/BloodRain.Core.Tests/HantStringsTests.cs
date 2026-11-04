using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BloodRain.Core.Data;
using Newtonsoft.Json.Linq;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>번체 화면 문구 strings/zh-Hant.json(backlog L-08). zh-Hans 를 OpenCC s2twp 로 바꾸고 손으로 고친 파일이라,
    /// 간체와 같은 키를 갖는지, 용어집 '번체' 표 말을 쓰는지, 간체 글자가 섞이지 않았는지, Noto Serif TC 에 글자가 다 있는지 본다.</summary>
    public class HantStringsTests
    {
        static string StringsDir => Path.Combine(DataDir, "strings");
        static string FontDir => Path.Combine(RepoRoot, "unity", "BloodRain", "Assets", "Art", "Fonts");
        static Dictionary<string, string> Table(string lang) => StringCatalog.ReadTable(ReadText(Path.Combine(StringsDir, lang + ".json")));
        static JArray Complete(string lang) => (JArray)JObject.Parse(ReadText(Path.Combine(StringsDir, lang + ".json")))["_meta"]!["complete"]!;

        static readonly Lazy<HashSet<int>> Tc = new Lazy<HashSet<int>>(() => FontCoverageTests.Cmap.Read(Path.Combine(FontDir, "NotoSerifTC-Regular.otf")));

        /// <summary>용어집 '## 번체…' 절의 표: (간체, 번체). / 로 묶은 칸은 하나씩 푼다.</summary>
        static List<(string hans, string hant)> Glossary()
        {
            var sec = Regex.Match(ReadRepoFile("docs", "design", "glossary.md"), "## 번체[^\n]*\n(.*?)(\n## |$)", RegexOptions.Singleline);
            Assert.True(sec.Success, "용어집에 '번체' 절이 없다");
            return Regex.Matches(sec.Groups[1].Value, @"^\| [^|]+ \| ([^|]+?) \| ([^|]+?) \|", RegexOptions.Multiline)
                .Select(m => (m.Groups[1].Value, m.Groups[2].Value)).Where(t => t.Item1 != "간체" && !t.Item1.StartsWith("---"))
                .SelectMany(t => t.Item1.Split(" / ").Zip(t.Item2.Split(" / "), (a, b) => (a, b))).ToList();
        }

        [Fact]
        public void 번체_글꼴과_라이선스가_있다()
        {
            Assert.True(File.Exists(Path.Combine(FontDir, "NotoSerifTC-OFL.txt")));
            Assert.True(Tc.Value.Count > 20000, $"Noto Serif TC 글자 수 {Tc.Value.Count}");
            foreach (var ch in "血雨江湖少林武當華山劍氣內功真氣境界輪迴殭屍ABCxyz0123456789%+-×·（）：，。「」")
                Assert.Contains((int)ch, Tc.Value);
        }

        [Fact]
        public void 번체_문구는_번체_글꼴로_찍힌다()
        {
            var miss = Table("zh-Hant").Values
                .SelectMany(t => Enumerable.Range(0, t.Length).Where(i => !char.IsLowSurrogate(t[i])).Select(i => char.ConvertToUtf32(t, i)))
                .Where(cp => cp > 0x20 && !Tc.Value.Contains(cp)).Distinct().Select(cp => $"{char.ConvertFromUtf32(cp)} (U+{cp:X4})").ToList();
            Assert.True(miss.Count == 0, "Noto Serif TC 에 없는 글자: " + string.Join(", ", miss));
        }

        [Fact]
        public void 번체는_간체와_같은_키와_갈래를_갖는다()
        {
            // 번체는 간체를 바꿔 만든다. 간체에 옮긴 문구를 더하면 번체도 같이 더한다(tools/make_hant.py: OpenCC s2twp 뒤 용어집 번체 표대로 손질).
            var hans = Table("zh-Hans");
            var hant = Table("zh-Hant");
            var onlyHans = hans.Keys.Except(hant.Keys).ToList();
            var onlyHant = hant.Keys.Except(hans.Keys).ToList();
            Assert.True(onlyHans.Count == 0, "간체에만 있는 키. python3 tools/make_hant.py 로 번체를 채운다(pip install opencc):\n" + string.Join("\n", onlyHans));
            Assert.True(onlyHant.Count == 0, "번체에만 있는 키:\n" + string.Join("\n", onlyHant));
            Assert.Equal(Complete("zh-Hans").Select(t => (string)t!), Complete("zh-Hant").Select(t => (string)t!));
            var len = hans.Where(kv => kv.Value.Length != hant[kv.Key].Length).Select(kv => kv.Key).ToList();
            Assert.True(len.Count == 0, "간체와 글자 수가 다른 값(낱말을 통째로 바꾼 것인지 본다):\n" + string.Join("\n", len));
        }

        [Fact]
        public void 번체는_용어집_번체_말을_쓴다()
        {
            var hans = Table("zh-Hans");
            var hant = Table("zh-Hant");
            var terms = Glossary();
            Assert.True(terms.Count >= 25, "번체 표가 너무 짧다");
            var bad = new List<string>();
            int used = 0;
            foreach (var (s, t) in terms)
                foreach (var kv in hans.Where(kv => kv.Value.Contains(s)))
                {
                    used++;
                    if (!hant[kv.Key].Contains(t)) bad.Add($"{kv.Key}: 간체 '{s}' → 용어집 '{t}', 지금 '{hant[kv.Key]}'");
                }
            Assert.True(used > 50, $"용어집 번체 말이 문구에 {used}번밖에 안 나온다(표 꼴이 바뀌었나)");
            Assert.True(bad.Count == 0, string.Join("\n", bad));
        }

        [Fact]
        public void 번체에_간체_글자와_잘못_바뀐_말이_없다()
        {
            var all = string.Concat(Table("zh-Hant").Values);
            // 간체에만 쓰는 글자(StoreCopyTests 와 같은 짝에 화면 문구에 자주 나오는 것을 더함)
            const string HansOnly = "门气绝种层个与这时级说会对发经过轻赋轮义顿极阴阳栈练战击动选觉剑华当术龙宝伤敌护尸复";
            foreach (var ch in HansOnly)
                Assert.False(all.Contains(ch), $"번체 문구에 간체 글자 {ch}");
            // OpenCC 가 뜻을 잘못 짚는 곳(손으로 고친 것이 되돌아가지 않게)
            foreach (var wrong in new[] { "回覆", "鍾", "毒後", "髮", "“", "”", "‘", "’" })
                Assert.False(all.Contains(wrong), $"번체 문구에 '{wrong}'(용어집 번체 표 아래 설명)");
            Assert.Contains("毒后", all);
            Assert.DoesNotMatch("[가-힣]", all);
        }

        [Fact]
        public void 번체_카드_이름도_짧다()
        {
            var long_ = Table("zh-Hant").Where(kv => (kv.Key.StartsWith("weapons.", StringComparison.Ordinal) || kv.Key.StartsWith("passives.", StringComparison.Ordinal))
                    && kv.Key.EndsWith(".name", StringComparison.Ordinal) && kv.Value.Length > 6)
                .Select(kv => $"{kv.Key}: {kv.Value}").ToList();
            Assert.True(long_.Count == 0, "zh-Hant: 카드 이름이 6자를 넘는다:\n" + string.Join("\n", long_));
        }
    }
}
