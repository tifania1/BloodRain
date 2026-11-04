using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>art_queue/queue.jsonl 한 줄 한 줄이 CLAUDE.md '이미지' 규칙과 postprocess_art.py 가 기대하는 모양을 지키는지.</summary>
    public class ArtQueueTests
    {
        static readonly string[] Statuses = { "todo", "generated", "picked", "imported", "rejected" };

        static List<JObject> Rows() =>
            File.ReadAllLines(Path.Combine(RepoRoot, "art_queue", "queue.jsonl")).Where(l => l.Trim().Length > 0)
                .Select(JObject.Parse).ToList();

        /// <summary>postprocess_art.py 의 KINDS 표(종류 → 출력 폴더).</summary>
        static Dictionary<string, string> Kinds()
        {
            var py = File.ReadAllText(Path.Combine(RepoRoot, "tools", "postprocess_art.py"));
            var d = Regex.Matches(py, "\"(\\w+)\": \\(\"([^\"]+)\", \\d+, (?:True|False)\\)")
                .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
            Assert.NotEmpty(d);
            return d;
        }

        [Fact] public void 줄마다_id가_하나뿐이고_상태와_종류를_안다()
        {
            var rows = Rows();
            var kinds = Kinds();
            var ids = rows.Select(o => (string)o["id"]!).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
            foreach (var o in rows)
            {
                var id = (string)o["id"]!;
                Assert.Contains((string)o["status"]!, Statuses);
                Assert.True(kinds.ContainsKey((string)o["kind"]!), $"{id}: postprocess_art.py 가 모르는 종류 {o["kind"]}");
                // 원화는 {STYLE}, 이펙트 재료는 바탕이 달라 {VFX_INK}(흰 바탕 먹)·{VFX_GLOW}(검은 바탕 빛)로 시작한다
                var head = (string)o["kind"]! switch { "vfx_ink" => "{VFX_INK}", "vfx_glow" => "{VFX_GLOW}", _ => "{STYLE}" };
                Assert.StartsWith(head, (string)o["prompt"]!);
                Assert.False(string.IsNullOrWhiteSpace((string?)o["chat"]), $"{id}: chat 이 비었다");
                Assert.StartsWith("art_inbox/", (string)o["save_as"]!);
                Assert.Matches(@"^\d+:\d+$", (string)o["ratio"]!);
            }
        }

        [Fact] public void 문구_앞머리_파일이_있다()
        {
            foreach (var f in new[] { "STYLE.txt", "VFX_INK.txt", "VFX_GLOW.txt" })
                Assert.True(File.Exists(Path.Combine(RepoRoot, "art_queue", f)), $"art_queue/{f} 가 없다");
        }

        [Fact] public void 탈락한_줄은_이유를_적는다()
        {
            foreach (var o in Rows().Where(o => (string)o["status"]! == "rejected"))
                Assert.False(string.IsNullOrWhiteSpace((string?)o["reason"]), $"{o["id"]}: rejected 인데 reason 이 없다");
        }

        [Fact] public void 이펙트_재료_줄은_방향과_크기가_있다()
        {
            // tools/import_vfx_ai.py 가 ai.json 에 옮겨 적고 셰이더가 그어짐 방향으로 쓴다(art_pipeline.md '문구')
            var dirs = new[] { "radial", "down", "up", "right", "left" };
            foreach (var o in Rows().Where(o => ((string)o["kind"]!).StartsWith("vfx_")))
            {
                Assert.Contains((string)o["progress"]!, dirs);
                Assert.True((double)o["sizeM"]! > 0, $"{o["id"]}: sizeM");
                Assert.False(string.IsNullOrWhiteSpace((string?)o["use"]), $"{o["id"]}: use(어디에 쓰는지)가 비었다");
            }
        }

        [Fact] public void 같은_chat은_같은_대화방_주소를_쓴다()
        {
            // 같은 계열은 같은 대화방에서 뽑아야 그림체가 맞는다(CLAUDE.md).
            foreach (var g in Rows().GroupBy(o => (string)o["chat"]!))
            {
                var urls = g.Select(o => (string?)o["chat_url"]).Where(u => !string.IsNullOrEmpty(u)).Distinct().ToList();
                Assert.True(urls.Count <= 1, $"chat '{g.Key}' 에 대화방 주소가 {urls.Count}개");
            }
        }

        [Fact] public void 이펙트_재료_imported_줄과_ai_json이_서로_맞다()
        {
            // import_vfx_ai.py 가 큐의 sizeM·progress·use 를 ai.json 에 옮겨 적는다. 손으로 한쪽만 고치면 셰이더 방향·크기가 어긋난다.
            var dir = Path.Combine(RepoRoot, "unity", "BloodRain", "Assets", "Art", "VFX", "AI");
            var ai = JObject.Parse(File.ReadAllText(Path.Combine(dir, "ai.json")));
            var tex = ((JArray)ai["textures"]!).Cast<JObject>().ToDictionary(t => (string)t["name"]!);
            var rows = Rows().Where(o => ((string)o["kind"]!).StartsWith("vfx_") && (string)o["status"]! == "imported").ToList();
            Assert.Equal(rows.Select(o => (string)o["id"]!).OrderBy(s => s), tex.Keys.OrderBy(s => s));
            foreach (var o in rows)
            {
                var t = tex[(string)o["id"]!];
                Assert.Equal((double)o["sizeM"]!, (double)t["sizeMeters"]!, 6);
                Assert.Equal((string)o["progress"]!, (string)t["progress"]!);
                Assert.Equal((string)o["use"]!, (string)t["use"]!);
                Assert.Equal((string)o["kind"]! == "vfx_ink" ? "ai_ink" : "ai_glow", (string)t["kind"]!);
                // 빛 재료는 밝은 종이 바닥에서 묻히지 않게 먹 받침(shade)을 같이 둔다(art_pipeline.md '적용')
                if ((string)t["kind"]! == "ai_glow")
                    Assert.Equal(new[] { "core", "gold", "shade" }, ((JArray)t["layers"]!).Select(l => (string)l!).OrderBy(s => s));
                foreach (var layer in ((JArray)t["layers"]!).Select(l => (string)l!))
                    Assert.True(File.Exists(Path.Combine(dir, $"{t["name"]}_{layer}.png")), $"{t["name"]}: {layer} 층 파일이 없다");
            }
        }

        [Fact] public void 먹_재료는_stroke_한_층이고_감마가_스크립트와_같다()
        {
            // import_vfx_ai.py 의 INK_GAMMA 를 바꾸면 이미 반영된 먹 재료는 옛 감마로 남는다.
            // ai.json 의 inkGamma 가 스크립트 값과 다르면 --reink 로 원본에서 다시 키잉해야 한다(V-11). 없는 줄은 감마 전에 키잉된 것.
            var script = File.ReadAllText(Path.Combine(RepoRoot, "tools", "import_vfx_ai.py"));
            var m = System.Text.RegularExpressions.Regex.Match(script, @"^INK_GAMMA = ([0-9.]+)", System.Text.RegularExpressions.RegexOptions.Multiline);
            Assert.True(m.Success, "import_vfx_ai.py 에 INK_GAMMA 가 없다");
            var gamma = double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            Assert.InRange(gamma, 0.4, 1.0);
            var ai = JObject.Parse(File.ReadAllText(Path.Combine(RepoRoot, "unity", "BloodRain", "Assets", "Art", "VFX", "AI", "ai.json")));
            foreach (var t in ((JArray)ai["textures"]!).Cast<JObject>().Where(t => (string)t["kind"]! == "ai_ink"))
            {
                Assert.Equal(new[] { "stroke" }, ((JArray)t["layers"]!).Select(l => (string)l!));
                if (t["inkGamma"] != null)
                    Assert.True(System.Math.Abs((double)t["inkGamma"]! - gamma) < 1e-6, $"{t["name"]}: inkGamma {t["inkGamma"]} 인데 스크립트는 {gamma}. --reink 로 다시 키잉");
            }
        }

        [Fact] public void imported_줄은_Unity_Art_폴더에_파일이_있다()
        {
            var kinds = Kinds();
            var jpg = new[] { "storyboard", "map", "card", "background", "store" };
            foreach (var o in Rows().Where(o => (string)o["status"]! == "imported"))
            {
                var kind = (string)o["kind"]!;
                // 이펙트 재료는 층 이름이 붙는다(import_vfx_ai.py): 먹은 _stroke, 빛은 _gold(+_core)
                var file = (string)o["id"]! + (kind == "vfx_ink" ? "_stroke.png" : kind == "vfx_glow" ? "_gold.png" : jpg.Contains(kind) ? ".jpg" : ".png");
                var path = Path.Combine(new[] { RepoRoot, "unity", "BloodRain", "Assets", "Art" }
                    .Concat(kinds[kind].Split('/')).Append(file).ToArray());
                Assert.True(File.Exists(path), $"{o["id"]}: imported 인데 {path} 가 없다");
            }
        }
    }
}
