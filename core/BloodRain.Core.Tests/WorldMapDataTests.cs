using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>worldmap.json(기획서 21장)이 장·캐릭터·문파 데이터와 맞고, 지도 규칙대로 끝까지 갈 수 있는지.
    /// Core 타입은 아직 없어서 JSON을 직접 읽는다(backlog M-01).</summary>
    public class WorldMapDataTests
    {
        readonly Data.GameData _d = TestData.Load();
        readonly JObject _m = JObject.Parse(File.ReadAllText(Path.Combine(TestData.DataDir, "worldmap.json")));
        readonly JObject _s = JObject.Parse(File.ReadAllText(Path.Combine(TestData.DataDir, "synergies.json")));

        List<JObject> Regions => _m["regions"]!.Children<JObject>().ToList();
        Dictionary<string, JObject> ById => Regions.ToDictionary(r => (string)r["id"]!);

        [Fact] public void 장은_서장부터_종장까지_하나씩_있고_봉인석은_일곱이다()
        {
            var chapters = Regions.Where(r => (string)r["kind"]! == "chapter").Select(r => (string)r["chapter"]!).ToList();
            var expected = new[] { "prologue", "chapter1", "chapter2", "chapter3", "chapter4", "chapter5", "chapter6", "chapter7", "final" };
            Assert.Equal(expected.OrderBy(x => x), chapters.OrderBy(x => x));
            Assert.Equal(Regions.Count, ById.Count);
            Assert.Equal(7, Regions.Count(r => r["sealStone"]!.Type == JTokenType.String));
            Assert.Single(Regions, r => (bool?)r["start"] == true);
        }

        [Fact] public void 길은_있는_지역만_잇고_지도는_한_덩어리다()
        {
            var ids = ById.Keys.ToHashSet();
            var adj = ids.ToDictionary(x => x, x => new HashSet<string>());
            foreach (var road in _m["roads"]!.Children<JArray>())
            {
                string a = (string)road[0]!, b = (string)road[1]!;
                Assert.Contains(a, ids);
                Assert.Contains(b, ids);
                adj[a].Add(b); adj[b].Add(a);
            }
            var seen = new HashSet<string> { "cheongun" };
            var stack = new Stack<string>(seen);
            while (stack.Count > 0) foreach (var n in adj[stack.Pop()]) if (seen.Add(n)) stack.Push(n);
            Assert.Equal(ids.Count, seen.Count);
        }

        /// <summary>열 수 있는 장을 아무 순서로나 해방해 나가도 종장까지 막히지 않고, 이야기 순서(무당 → 화산·곤륜, 봉인석 6 → 십만대산, 7 → 종장)가 지켜진다.</summary>
        [Theory]
        [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
        public void 어느_순서로_넓혀도_종장까지_간다(int seed)
        {
            var rng = new System.Random(seed);
            var adj = ById.Keys.ToDictionary(x => x, x => new HashSet<string>());
            foreach (var road in _m["roads"]!.Children<JArray>()) { adj[(string)road[0]!].Add((string)road[1]!); adj[(string)road[1]!].Add((string)road[0]!); }

            var liberated = new List<string> { "cheongun" };
            int stones = 0;
            bool Open(JObject r)
            {
                string id = (string)r["id"]!;
                if ((string)r["kind"]! != "chapter" || liberated.Contains(id)) return false;
                if (!adj[id].Any(liberated.Contains)) return false;
                return stones >= ((int?)r["sealStonesRequired"] ?? 0);
            }
            while (true)
            {
                var open = Regions.Where(Open).ToList();
                if (open.Count == 0) break;
                var pick = open[rng.Next(open.Count)];
                liberated.Add((string)pick["id"]!);
                if (pick["sealStone"]!.Type == JTokenType.String) stones++;
            }
            Assert.Equal(9, liberated.Count);
            Assert.Equal("bongmagok", liberated.Last());
            Assert.Equal("sipmandaesan", liberated[liberated.Count - 2]);
            int wudang = liberated.IndexOf("wudangsan");
            Assert.True(wudang < liberated.IndexOf("huashan") && wudang < liberated.IndexOf("kunlun"), "무당의 첫 단서가 화산·곤륜보다 먼저");
        }

        [Fact] public void 캐릭터마다_연고지가_있고_문파는_20장_진전과_같다()
        {
            var homes = (JObject)_m["homes"]!;
            var schools = _s["schools"]!.Children<JObject>().Select(x => (string)x["id"]!).ToHashSet();
            foreach (var c in _d.Characters)
            {
                var h = homes[c.Id] as JObject;
                Assert.True(h != null, $"{c.NameKo} 연고지 없음");
                Assert.Contains((string)h!["region"]!, ById.Keys);
                var sect = h["sect"]!;
                if (sect.Type == JTokenType.String) Assert.Contains((string)sect!, schools);
                // 연고지 지역의 문파와 캐릭터 문파가 같아야 한다
                Assert.Equal(ById[(string)h["region"]!]["sect"]!.ToString(), sect.ToString());
            }
            // 외전 지역은 모두 누군가의 연고지
            foreach (var side in Regions.Where(r => (string)r["kind"]! == "side"))
                Assert.Contains(_d.Characters, c => (string)homes[c.Id]!["region"]! == (string)side["id"]!);
        }

        [Fact] public void 정화_단계는_쌓인_시간_순서이고_2회차는_1회차_20분을_15분에_맞춘다()
        {
            var mins = _m["purification"]!["stages"]!.Children<JObject>()
                .Where(s => s["minutes"]!.Type == JTokenType.Integer).Select(s => (int)s["minutes"]!).ToList();
            Assert.Equal(mins.OrderBy(x => x), mins);

            var second = _m["cycle"]!["second"]!;
            double scale = (double)second["timelineScale"]!, xp = (double)second["xpMultiplier"]!;
            Assert.Equal(15.0, _d.Balance.SpawnTarget.BossAtMinute * scale, 1);
            Assert.InRange(scale * xp, 0.98, 1.02);   // 같은 경지에 같은 비율로 닿는다
            Assert.Equal(new[] { "seal" }, _m["cycle"]!["endingsFirst"]!.Select(x => (string)x!));
        }
    }
}
