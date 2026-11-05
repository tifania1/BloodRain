using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BloodRain.Core.Data;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>용어집(docs/design/glossary.md)의 경지·문파·지역 표와 strings/&lt;lang&gt;.json 이 같은 표기를 쓰는지.
    /// 용어집을 고치고 문구 파일을 안 고치면(또는 반대) 여기서 걸린다.</summary>
    public class GlossaryTests
    {
        static string Glossary => ReadRepoFile("docs", "design", "glossary.md");

        static string StringsDir => Path.Combine(DataDir, "strings");

        /// <summary>"## 제목" 절의 표 → (한국어, 영어, 간체). "남궁 / 남궁세가" 처럼 / 로 묶은 칸은 하나씩 푼다.</summary>
        static List<(string ko, string en, string zh)> Table(string heading)
        {
            var g = Glossary;
            int start = g.IndexOf("\n## " + heading + "\n", StringComparison.Ordinal);
            Assert.True(start >= 0, $"용어집에 '## {heading}' 절이 없다");
            int end = g.IndexOf("\n## ", start + 4, StringComparison.Ordinal);
            var section = end < 0 ? g.Substring(start) : g.Substring(start, end - start);
            var rows = new List<(string, string, string)>();
            foreach (var line in section.Split('\n').Where(l => l.StartsWith("| ", StringComparison.Ordinal)).Skip(2))
            {
                var c = line.Trim('|').Split('|').Select(x => x.Trim()).ToArray();
                var ko = c[0].Split(" / ");
                var en = c[1].Split(" / ");
                var zh = c[2].Split(" / ");
                for (int i = 0; i < ko.Length; i++)
                    rows.Add((ko[i], i < en.Length ? en[i] : en[0], i < zh.Length ? zh[i] : zh[0]));
            }
            return rows;
        }

        static readonly Lazy<StringCatalog> Catalog = new Lazy<StringCatalog>(() =>
            StringCatalog.FromData(ReadFiles(), ReadText(Path.Combine(StringsDir, "ko.json"))));

        static Dictionary<string, string> Lang(string lang) => StringCatalog.ReadTable(ReadText(Path.Combine(StringsDir, lang + ".json")));

        /// <summary>그 갈래 키 가운데 한국어 원문이 용어집 한국어와 같은 것마다, 옮긴 값이 있으면 용어집과 같아야 한다.</summary>
        static List<string> Mismatches(string heading, string keyPrefix, string lang, Func<(string ko, string en, string zh), string> pick)
        {
            var table = Lang(lang);
            var terms = Table(heading);
            var bad = new List<string>();
            int matched = 0;
            foreach (var kv in Catalog.Value.Korean.Where(kv => kv.Key.StartsWith(keyPrefix, StringComparison.Ordinal) && kv.Key.EndsWith(".name", StringComparison.Ordinal)))
            {
                var term = terms.FirstOrDefault(t => t.ko == kv.Value);
                if (term.ko == null) continue;
                matched++;
                var want = pick(term);
                if (want.Contains("사람 결정 대기")) continue;
                if (table.TryGetValue(kv.Key, out var got) && got != want) bad.Add($"{kv.Key} ({kv.Value}): 용어집 '{want}', {lang}.json '{got}'");
            }
            Assert.True(matched > 0, $"'{heading}' 표와 {keyPrefix} 이름이 하나도 안 맞는다(표 꼴이 바뀌었나)");
            return bad;
        }

        public static IEnumerable<object[]> Cases()
        {
            foreach (var lang in new[] { "en", "zh-Hans" })
            {
                yield return new object[] { "경지", "balance.realms.", lang };
                yield return new object[] { "문파·세가", "cheonha.sects.", lang };
                yield return new object[] { "지역", "cheonha.regions.", lang };
                yield return new object[] { "사람(대표 인물)", "characters.", lang };
            }
        }

        [Theory]
        [InlineData("zh-Hans")]
        [InlineData("zh-Hant")]
        public void 중국어_사람_이름에_로마자가_없다(string lang)
        {
            // L-04b(2026-11-05 사람): 간체·번체 화면의 사람 이름은 한자로. 로마자(Jin Sowoon)가 섞이면 안 된다.
            var bad = Lang(lang).Where(kv => (kv.Key.StartsWith("characters.", StringComparison.Ordinal) || kv.Key.StartsWith("enemies.bosses.", StringComparison.Ordinal))
                    && kv.Key.EndsWith(".name", StringComparison.Ordinal) && kv.Value.Any(ch => ch < 128 && char.IsLetter(ch)))
                .Select(kv => $"{kv.Key}: {kv.Value}").ToList();
            Assert.True(bad.Count == 0, string.Join("\n", bad));
        }

        [Theory, MemberData(nameof(Cases))]
        public void 용어집_표기와_문구_파일이_같다(string heading, string keyPrefix, string lang)
        {
            var bad = Mismatches(heading, keyPrefix, lang, t => lang == "en" ? t.en : t.zh);
            Assert.True(bad.Count == 0, string.Join("\n", bad));
        }

        [Theory]
        [InlineData("en", 24)]
        [InlineData("zh-Hans", 6)]
        public void 레벨업_카드_이름은_짧다(string lang, int max)
        {
            // 원칙 8. 카드 한 장 너비(300px, 44pt)에 한 줄로 들어가야 한다.
            var long_ = Lang(lang).Where(kv => (kv.Key.StartsWith("weapons.", StringComparison.Ordinal) || kv.Key.StartsWith("passives.", StringComparison.Ordinal))
                    && kv.Key.EndsWith(".name", StringComparison.Ordinal) && new System.Globalization.StringInfo(kv.Value).LengthInTextElements > max)
                .Select(kv => $"{kv.Key}: {kv.Value}").ToList();
            Assert.True(long_.Count == 0, $"{lang}: 카드 이름이 {max}자를 넘는다:\n" + string.Join("\n", long_));
        }

        [Fact]
        public void 경지_일곱이_용어집에_다_있다()
        {
            var realms = Table("경지").Select(t => t.ko).ToHashSet();
            foreach (var r in TestData.Load().Balance.Realms) Assert.Contains(r.NameKo, realms);
        }

        [Fact]
        public void 용어집에_작가_고유_명칭이_없다()
        {
            // CLAUDE.md 금지 예. 금지 규칙을 적은 원칙 6번 줄은 빼고 본다.
            var body = string.Join("\n", Glossary.Split('\n').Where(l => !l.Contains("특정 작가 고유 명칭 금지")));
            foreach (var name in new[] { "항룡십팔장", "독고구검", "타구봉법", "구양신공", "능파미보" })
                Assert.DoesNotContain(name, body);
        }
    }
}
