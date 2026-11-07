using System;
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

        // ---- AQ-01: 기획서 13장 규격·색과 대기열 문구 ----

        /// <summary>단색 #F2EBDD 바탕에서 뽑아 배경을 지우는 종류(13장 에셋 규격, postprocess 가 색 키로 지운다). 나머지 원화는 장면 그대로.</summary>
        static readonly string[] FlatBgKinds = { "character", "character_sheet", "enemy", "elite", "boss", "props", "portrait", "ui" };

        static IEnumerable<JObject> ArtRows() => Rows().Where(o => !((string)o["kind"]!).StartsWith("vfx_"));
        static IEnumerable<JObject> Waiting() => ArtRows().Where(o => (string)o["status"]! is "todo" or "generated");

        [Fact] public void 단색_바탕_종류만_F2EBDD를_쓰고_png로_받는다()
        {
            foreach (var o in ArtRows())
            {
                bool flat = FlatBgKinds.Contains((string)o["kind"]!);
                Assert.True(flat == ((string)o["prompt"]!).Contains("#F2EBDD"), $"{o["id"]}: {o["kind"]} 인데 #F2EBDD 바탕 문구가 {(flat ? "없다" : "있다")}");
                Assert.EndsWith(".png", (string)o["save_as"]!);
            }
        }

        /// <summary>ChatGPT 는 문구 속 비율을 따른다. 기다리는 줄은 ratio 칸의 비율을 문구에도 적는다(13장 틀의 'Square 1:1'·'wide 3:2'·'tall 2:3').</summary>
        [Fact] public void 기다리는_줄은_문구에_비율을_적는다()
        {
            foreach (var o in Waiting())
                Assert.True(((string)o["prompt"]!).Contains((string)o["ratio"]!), $"{o["id"]}: 문구에 {o["ratio"]} 가 없다");
        }

        /// <summary>주홍은 적·혈우·위험에만 쓴다(13장 색). 플레이어 몸과 아군 인물(보스가 아닌 캐릭터·시트·초상)은 문구로 막는다.</summary>
        [Fact] public void 아군_인물은_주홍을_막는다()
        {
            foreach (var o in Waiting().Where(o => (string)o["kind"]! is "character" or "character_sheet" or "portrait" && !((string)o["id"]!).StartsWith("boss_")))
            {
                var p = (string)o["prompt"]!;
                Assert.True(p.Contains("No red or crimson") || p.Contains("No crimson"), $"{o["id"]}: 아군 인물인데 주홍을 막는 문구가 없다");
            }
        }

        /// <summary>한 인물의 시트·스프라이트·초상은 한 대화방에서(13장 '일관성 지키는 법' 1·3).</summary>
        [Fact] public void 한_인물의_시트_스프라이트_초상은_같은_chat()
        {
            var rows = ArtRows().ToDictionary(o => (string)o["id"]!);
            foreach (var o in rows.Values.Where(o => (string)o["kind"]! == "character_sheet"))
            {
                var id = (string)o["id"]!;
                var name = id.Substring(id.IndexOf('_') + 1).Replace("_sheet", "");
                foreach (var other in new[] { $"char_{name}", $"portrait_{name}", $"boss_{name}" })
                    if (rows.TryGetValue(other, out var r))
                        Assert.True((string)r["chat"]! == (string)o["chat"]!, $"{other} chat '{r["chat"]}' 이 시트 {id} 의 '{o["chat"]}' 와 다르다");
            }
        }

        /// <summary>M3·M4 그림 목록(art_list_m3.md A-08~A-11, art_list_m4.md A-13~)의 표 줄과 큐에서 note 가 'art_list_mN' 으로 시작하는 줄이 빠짐없이 같고, chat 도 같다.
        /// 목록에 없는 그림은 뽑지 않는다는 art_list.md 규칙을 M3·M4 몫에도 지키게 한다.</summary>
        [Theory, InlineData("art_list_m3"), InlineData("art_list_m4")]
        public void 단계별_그림_목록과_큐가_같다(string list)
        {
            var md = ReadRepoFile("docs", "design", list + ".md");
            var listed = Regex.Matches(md, @"^\| [^|]+ \| ([a-z0-9_]+) \| [^|]+ \| ([^|]+) \|", RegexOptions.Multiline)
                .Where(m => m.Groups[1].Value != "id").ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value.Trim());
            Assert.NotEmpty(listed);
            var rows = Rows().Where(o => ((string?)o["note"] ?? "").StartsWith(list + " ")).ToDictionary(o => (string)o["id"]!);
            Assert.Equal(listed.Keys.OrderBy(x => x), rows.Keys.OrderBy(x => x));
            foreach (var (id, chat) in listed) Assert.True(chat == (string)rows[id]["chat"]!, $"{id}: 목록 chat '{chat}' 과 큐 chat '{rows[id]["chat"]}' 이 다르다");
            var total = Regex.Match(md, @"^\| 합계 \| (\d+) \|", RegexOptions.Multiline);
            Assert.True(total.Success, $"{list}.md 에 합계 줄이 없다");
            Assert.Equal(rows.Count, int.Parse(total.Groups[1].Value));
        }

        /// <summary>글자·낙관은 굽지 않는다(CLAUDE.md). 글자 비슷한 것을 그리라면 반드시 비워 두라고 적는다.</summary>
        [Fact] public void 기다리는_줄은_글자를_그리라고_하지_않는다()
        {
            var texty = new Regex(@"\b(calligraph(y|ic) (on|of|text)|inscri\w+|lettering|signboard|seal stamp|caption|kanji|hanzi|Chinese characters)\b", RegexOptions.IgnoreCase);
            foreach (var o in Waiting())
                foreach (Match m in texty.Matches((string)o["prompt"]!))
                {
                    var p = (string)o["prompt"]!;
                    var around = p.Substring(Math.Max(0, m.Index - 60), Math.Min(p.Length, m.Index + m.Length + 30) - Math.Max(0, m.Index - 60));
                    Assert.True(Regex.IsMatch(around, @"\b(no|blank|empty|without|bare|unmarked)\b", RegexOptions.IgnoreCase), $"{o["id"]}: 글자를 그리라는 말 같다: …{around}…");
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
