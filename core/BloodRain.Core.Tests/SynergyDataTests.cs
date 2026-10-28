using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>synergies.json 이 무공·심법 데이터와 어긋나지 않는지. Core 타입은 아직 없어서 JSON을 직접 읽는다(backlog S-01).</summary>
    public class SynergyDataTests
    {
        readonly Data.GameData _d = TestData.Load();
        readonly JObject _s = JObject.Parse(File.ReadAllText(Path.Combine(TestData.DataDir, "synergies.json")));

        [Fact] public void 연환은_16개이고_무공마다_정확히_두_번_나온다()
        {
            var links = _s["links"]!.Children<JObject>().ToList();
            Assert.Equal(16, links.Count);
            var count = new Dictionary<string, int>();
            foreach (var l in links)
            {
                string a = (string)l["a"]!, b = (string)l["b"]!;
                Assert.NotEqual(a, b);
                Assert.Contains(_d.Weapons, w => w.Id == a);
                Assert.Contains(_d.Weapons, w => w.Id == b);
                count[a] = count.GetValueOrDefault(a) + 1;
                count[b] = count.GetValueOrDefault(b) + 1;
            }
            foreach (var w in _d.Weapons) Assert.True(count.GetValueOrDefault(w.Id) == 2, $"{w.NameKo} 연환 {count.GetValueOrDefault(w.Id)}개");
            var pairs = links.Select(l => string.Join("+", new[] { (string)l["a"]!, (string)l["b"]! }.OrderBy(x => x))).ToList();
            Assert.Equal(pairs.Count, pairs.Distinct().Count());
        }

        [Fact] public void 삼재검법을_뺀_무공과_심법은_모두_한_문파에만_속한다()
        {
            var members = _s["schools"]!.Children<JObject>().SelectMany(sc => sc["members"]!.Select(m => (string)m!)).ToList();
            Assert.Equal(members.Count, members.Distinct().Count());
            var all = _d.Weapons.Select(w => w.Id).Concat(_d.Passives.Select(p => p.Id)).Where(id => id != "samjae_sword");
            foreach (var id in all) Assert.True(members.Contains(id), $"{id} 가 어느 문파에도 없음");
            Assert.Equal(31, members.Count);
            Assert.Equal("samjae_sword", (string)_s["common"]!["weapon"]!);
        }

        [Fact] public void 진전_단계는_문파_인원을_넘지_않고_오름차순이다()
        {
            foreach (var sc in _s["schools"]!.Children<JObject>())
            {
                int n = sc["members"]!.Count();
                var counts = sc["tiers"]!.Select(t => (int)t["count"]!).ToList();
                Assert.Equal(counts.OrderBy(c => c), counts);
                Assert.True(counts.Max() <= n, $"{sc["nameKo"]} 단계가 인원보다 많음");
                Assert.True(counts.Min() >= 2);
            }
        }

        [Fact] public void 비전은_8개이고_시구_힌트가_두_줄이다()
        {
            var secrets = _s["secrets"]!.Children<JObject>().ToList();
            Assert.Equal(8, secrets.Count);
            foreach (var s in secrets) Assert.Equal(2, s["hint"]!.Count());
            Assert.Equal(8, secrets.Select(s => (string)s["id"]!).Distinct().Count());
        }

        [Fact] public void 기연_사건은_8개이고_확률은_0과_1_사이다()
        {
            var ev = _s["encounters"]!.Children<JObject>().ToList();
            Assert.Equal(8, ev.Count);
            foreach (var e in ev) { var c = (double)e["chance"]!; Assert.InRange(c, 0.0, 1.0); }
        }
    }
}
