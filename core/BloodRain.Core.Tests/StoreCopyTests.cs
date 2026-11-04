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
            Assert.Contains($"{n["공격 무공"]} attack arts, {n["심법"]} Heart Methods", en);
            Assert.Contains($"{n["절기"]} Pinnacle Arts", en);
            Assert.Contains($"{n["경지"]} Realms", en);
            Assert.Contains($"{n["지역"]} regions, {n["땅의 주인"]} Lords of the Land", en);
            Assert.Contains($"{n["타고난 성질"]} Birth Traits", en);

            var hans = Block("long:zh-Hans");
            Assert.Contains($"{n["문파"]} 个门派", hans);
            Assert.Contains($"{n["공격 무공"]} 种攻击武功、{n["심법"]} 种心法", hans);
            Assert.Contains($"{n["절기"]} 种绝技", hans);
            Assert.Contains($"{n["경지"]} 层境界", hans);
            Assert.Contains($"{n["지역"]} 个地区与 {n["땅의 주인"]} 位一方之主", hans);
            Assert.Contains($"{n["타고난 성질"]} 种天赋", hans);

            var hant = Block("long:zh-Hant");
            Assert.Contains($"{n["문파"]} 個門派", hant);
            Assert.Contains($"{n["공격 무공"]} 種攻擊武功、{n["심법"]} 種心法", hant);
            Assert.Contains($"{n["절기"]} 種絕技", hant);
            Assert.Contains($"{n["경지"]} 層境界", hant);
            Assert.Contains($"{n["지역"]} 個地區與 {n["땅의 주인"]} 位一方之主", hant);
            Assert.Contains($"{n["타고난 성질"]} 種天賦", hant);
        }

        [Theory]
        [InlineData("short:zh-Hans")]
        [InlineData("short:zh-Hant")]
        public void 중국어_짧은_소개도_한_줄_300자_이하(string key)
        {
            var text = Block(key);
            Assert.InRange(text.Length, 60, 300);
            Assert.DoesNotContain("\n", text);
        }

        static readonly string[] Keys = { "short:ko", "short:en", "short:zh-Hans", "short:zh-Hant", "long:ko", "long:en", "long:zh-Hans", "long:zh-Hant" };

        [Fact]
        public void 영어는_용어집_말을_쓴다()
        {
            // ST-06(2026-11-04): ST-01 때 임시로 쓴 말을 용어집(docs/design/glossary.md)에 맞췄다. 옛 말이 돌아오지 않게.
            var en = Block("short:en") + "\n" + Block("long:en");
            foreach (var old in new[] { "Lightness Skill", "Secret Art", "cultivation method", "innate trait", "Profound", "Tang Clan", "seal shard" })
                Assert.DoesNotContain(old, en);
            foreach (var term in new[] { "Lightfoot", "Pinnacle Art", "Heart Methods", "Birth Traits", "Mystic", "Dang Clan", "Seal Stones", "Samsara Qi", "Ultimate Art" })
                Assert.Contains(term, en);
        }

        /// <summary>용어집 '번체(상점 글)' 표: (간체, 번체).</summary>
        static List<(string hans, string hant)> HantTable()
        {
            var sec = Regex.Match(ReadRepoFile("docs", "design", "glossary.md"), "## 번체[^\n]*\n(.*?)(\n## |$)", RegexOptions.Singleline);
            Assert.True(sec.Success, "용어집에 '번체' 절이 없다");
            return Regex.Matches(sec.Groups[1].Value, @"^\| [^|]+ \| ([^|]+?) \| ([^|]+?) \|", RegexOptions.Multiline)
                .Select(m => (m.Groups[1].Value, m.Groups[2].Value)).Where(t => t.Item1 != "간체" && !t.Item1.StartsWith("---"))
                .SelectMany(t => t.Item1.Split(" / ").Zip(t.Item2.Split(" / "), (a, b) => (a, b))).ToList();
        }

        [Fact]
        public void 중국어는_용어집_말을_쓰고_간체_번체가_섞이지_않는다()
        {
            var hans = Block("short:zh-Hans") + Block("long:zh-Hans");
            var hant = Block("short:zh-Hant") + Block("long:zh-Hant");
            var table = HantTable();
            Assert.True(table.Count >= 20, "번체 표가 너무 짧다");
            // 상점 글에 꼭 나오는 말
            foreach (var hansTerm in new[] { "境界", "奥义", "绝技", "心法", "轮回之气", "一方之主", "封印石", "天赋", "玄境", "唐门", "走火入魔" })
            {
                Assert.Contains(hansTerm, hans);
                Assert.Contains(table.First(t => t.hans == hansTerm).hant, hant);
            }
            // 간체에만 쓰는 글자는 번체 글에, 번체에만 쓰는 글자는 간체 글에 없어야 한다(回 처럼 둘 다 쓰는 글자는 빼고 고른 짝)
            const string HansOnly = "门气绝种层个与这时级说会对发经过轻赋轮义顿极阴阳栈练战击动选觉剑";
            const string HantOnly = "門氣絕種層個與這時級說會對發經過輕賦輪義頓極陰陽棧練戰擊動選覺劍";
            Assert.Equal(HansOnly.Length, HantOnly.Length);
            for (int i = 0; i < HansOnly.Length; i++)
            {
                Assert.False(hant.Contains(HansOnly[i]), $"번체 글에 간체 글자 {HansOnly[i]}(번체 {HantOnly[i]})");
                Assert.False(hans.Contains(HantOnly[i]), $"간체 글에 번체 글자 {HantOnly[i]}(간체 {HansOnly[i]})");
            }
            Assert.DoesNotMatch("[\uAC00-\uD7A3]", hans + hant);
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
            foreach (var key in Keys)
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
            Assert.Contains("没有抽卡开箱，也没有出售属性或武功的商品", Block("long:zh-Hans"));
            Assert.Contains("沒有抽卡開箱，也沒有出售屬性或武功的商品", Block("long:zh-Hant"));
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
