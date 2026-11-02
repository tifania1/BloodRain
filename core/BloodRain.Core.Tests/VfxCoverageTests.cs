using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>무공 16·절기 16 이 모두 이펙트 재료를 하나 이상 받아 두었는지(V-06 이 빈손으로 시작하지 않게).
    /// 재료는 bake_brush.py 가 구운 붓 획(Art/VFX/Brush/*.json 의 weapon) 이나 art_queue 이펙트 줄(use 에 무공 id·절기 이름)이다.</summary>
    public class VfxCoverageTests
    {
        static string DataPath(string name) =>
            Path.Combine(RepoRoot, "unity", "BloodRain", "Assets", "Resources", "Data", name + ".json");

        static List<JObject> Items(string file, string key) =>
            ((JArray)JObject.Parse(File.ReadAllText(DataPath(file)))[key]!).Cast<JObject>().ToList();

        static List<JObject> VfxRows() =>
            File.ReadAllLines(Path.Combine(RepoRoot, "art_queue", "queue.jsonl")).Where(l => l.Trim().Length > 0)
                .Select(JObject.Parse).Where(o => ((string)o["kind"]!).StartsWith("vfx")).ToList();

        static Dictionary<string, string> BrushWeapons()
        {
            var dir = Path.Combine(RepoRoot, "unity", "BloodRain", "Assets", "Art", "VFX", "Brush");
            return Directory.GetFiles(dir, "*.json")
                .ToDictionary(f => Path.GetFileName(f)!, f => (string)JObject.Parse(File.ReadAllText(f))["weapon"]!);
        }

        [Fact] public void 붓_획_json의_weapon은_있는_무공이다()
        {
            var ids = Items("weapons", "weapons").Select(w => (string)w["id"]!).ToHashSet();
            foreach (var kv in BrushWeapons())
                Assert.True(ids.Contains(kv.Value), $"Brush/{kv.Key}: weapons.json 에 없는 무공 {kv.Value}");
        }

        [Fact] public void 이펙트_줄의_use_괄호_id는_있는_무공이다()
        {
            var ids = Items("weapons", "weapons").Select(w => (string)w["id"]!).ToHashSet();
            foreach (var o in VfxRows())
            {
                var use = (string?)o["use"] ?? "";
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(use, @"\(([a-z_]+)\)"))
                    Assert.True(ids.Contains(m.Groups[1].Value), $"{o["id"]}: use 의 ({m.Groups[1].Value}) 가 weapons.json 에 없다");
            }
        }

        [Fact] public void 무공마다_이펙트_재료가_있다()
        {
            var brush = BrushWeapons().Values.ToHashSet();
            var uses = VfxRows().Select(o => (string?)o["use"] ?? "").ToList();
            foreach (var w in Items("weapons", "weapons"))
            {
                var id = (string)w["id"]!;
                Assert.True(brush.Contains(id) || uses.Any(u => u.Contains($"({id})")),
                    $"{id}({w["nameKo"]}): 붓 획도 art_queue 이펙트 줄도 없다");
            }
        }

        [Fact] public void 절기마다_이펙트_재료가_있다()
        {
            var uses = VfxRows().Select(o => (string?)o["use"] ?? "").ToList();
            foreach (var e in Items("evolutions", "evolutions"))
            {
                var name = (string)e["nameKo"]!;
                Assert.True(uses.Any(u => u.StartsWith(name)), $"{e["id"]}({name}): art_queue 이펙트 줄 use 가 이 이름으로 시작하지 않는다");
            }
        }
    }
}
