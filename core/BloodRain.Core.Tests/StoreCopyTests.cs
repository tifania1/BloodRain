using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>docs/store/store_text.md(스팀 상점 문구, ST-01)가 데이터와 맞는지.
    /// 상점에 '무공 16종'이라 써 놓고 데이터가 바뀌면 거짓 광고가 된다. 개수가 바뀌면 상점 글도 같이 고친다.</summary>
    public class StoreCopyTests
    {
        static string Doc() => ReadRepoFile("docs", "store", "store_text.md");

        static JObject Json(string name) => JObject.Parse(File.ReadAllText(Path.Combine(DataDir, name + ".json")));

        static string Block(string key)
        {
            var m = Regex.Match(Doc(), $"<!-- store:{key} -->\\n(.*?)\\n<!-- /store:", RegexOptions.Singleline);
            Assert.True(m.Success, $"store_text.md 에 <!-- store:{key} --> 칸이 없다");
            return m.Groups[1].Value.Trim();
        }

        /// <summary>'숫자와 출처' 표: 항목 → 숫자.</summary>
        static Dictionary<string, string> Numbers()
        {
            var sec = Regex.Match(Doc(), "## 숫자와 출처\\n(.*?)\\n## ", RegexOptions.Singleline);
            Assert.True(sec.Success, "'숫자와 출처' 절이 없다");
            return Regex.Matches(sec.Groups[1].Value, @"^\| ([^|]+?) \| ([\d.]+) \|", RegexOptions.Multiline)
                .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
        }

        static Dictionary<string, int> Actual()
        {
            var ch = Json("cheonha");
            var regions = (JArray)ch["regions"]!;
            return new Dictionary<string, int>
            {
                ["문파"] = ((JArray)ch["sects"]!).Count,
                ["공격 무공"] = ((JArray)Json("weapons")["weapons"]!).Count,
                ["심법"] = ((JArray)Json("passives")["passives"]!).Count,
                ["절기"] = ((JArray)Json("evolutions")["evolutions"]!).Count,
                ["지역"] = regions.Count,
                // 봉마곡의 혈마는 종장 보스라 '땅의 주인'에 세지 않는다(기획서 1장 '땅의 주인 7').
                ["땅의 주인"] = regions.Count(r => r["boss"] != null && r["boss"]!.Type != JTokenType.Null && (string?)r["id"] != "bongmagok"),
                ["경지"] = ((JArray)ch["realmScale"]!).Count,
                ["타고난 성질"] = ((JArray)ch["birthTraits"]!["list"]!).Count,
                ["봉인석"] = (int)ch["bosses"]!["finalRequires"]!["sealStones"]!,
            };
        }

        [Fact]
        public void 숫자_표가_데이터와_같다()
        {
            var doc = Numbers();
            foreach (var kv in Actual())
            {
                Assert.True(doc.ContainsKey(kv.Key), $"'숫자와 출처' 표에 {kv.Key} 줄이 없다");
                Assert.Equal(kv.Value.ToString(), doc[kv.Key]);
            }
            Assert.Equal("2.25", doc["축척"]);
            Assert.Contains("2.25배", ReadRepoFile("docs", "design", "gdd.md"));
        }

        [Fact]
        public void 긴_소개의_숫자가_표와_같다()
        {
            var n = Actual();
            var ko = Block("long:ko");
            var en = Block("long:en");
            Assert.Contains($"문파 {n["문파"]}곳", ko);
            Assert.Contains($"공격 무공 {n["공격 무공"]}종, 심법 {n["심법"]}종", ko);
            Assert.Contains($"절기 {n["절기"]}종", ko);
            Assert.Contains($"경지 {n["경지"]}단계", ko);
            Assert.Contains($"지역 {n["지역"]}곳과 땅의 주인 {n["땅의 주인"]}", ko);
            Assert.Contains($"타고난 성질 {n["타고난 성질"]}가지", ko);

            Assert.Contains($"{n["문파"]} sects", en);
            Assert.Contains($"{n["공격 무공"]} attack arts, {n["심법"]} cultivation methods", en);
            Assert.Contains($"{n["절기"]} Secret Arts", en);
            Assert.Contains($"{n["경지"]} Realms", en);
            Assert.Contains($"{n["지역"]} regions, {n["땅의 주인"]} Lords of the Land", en);
            Assert.Contains($"{n["타고난 성질"]} innate traits", en);
        }

        [Theory]
        [InlineData("short:ko")]
        [InlineData("short:en")]
        public void 짧은_소개는_300자_이하(string key)
        {
            var text = Block(key);
            Assert.InRange(text.Length, 100, 300);
            Assert.DoesNotContain("\n", text);
        }

        [Fact]
        public void 소개_글에_링크가_없다()
        {
            // 2024-09 부터 Valve 가 소개 글 칸의 바깥 링크·다른 Steam 상품 링크를 막았다(docs/store/README.md).
            foreach (var key in new[] { "short:ko", "short:en", "long:ko", "long:en" })
            {
                var text = Block(key);
                Assert.DoesNotMatch(@"https?://|www\.|\[url", text);
            }
        }

        [Fact]
        public void 힘을_파는_결제가_없다고_밝힌다()
        {
            // 기획서 16장: 확률형 뽑기·능력치 상품은 어느 플랫폼에도 넣지 않는다. 상점 글에서 분명히 말한다.
            Assert.Contains("확률형 뽑기도, 능력치나 무공을 파는 상품도 없습니다", Block("long:ko"));
            Assert.Contains("No loot boxes. No paid stats or arts.", Block("long:en"));
            Assert.Contains("확률형 뽑기", ReadRepoFile("docs", "design", "gdd.md"));
        }

        [Fact]
        public void 태그_후보는_겹치지_않는_20개()
        {
            var sec = Regex.Match(Doc(), "## 태그 후보[^\\n]*\\n(.*?)\\n## ", RegexOptions.Singleline);
            Assert.True(sec.Success, "'태그 후보' 절이 없다");
            var tags = Regex.Matches(sec.Groups[1].Value, @"^\| (\d+) \| ([^|]+?) \|", RegexOptions.Multiline)
                .Select(m => (n: int.Parse(m.Groups[1].Value), tag: m.Groups[2].Value)).ToList();
            Assert.Equal(Enumerable.Range(1, 20), tags.Select(t => t.n));
            Assert.Equal(20, tags.Select(t => t.tag).Distinct().Count());
            Assert.Contains("Bullet Heaven", tags.Take(5).Select(t => t.tag));
            Assert.Contains("Wuxia", tags.Take(5).Select(t => t.tag));
        }
    }
}
