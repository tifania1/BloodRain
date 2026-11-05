using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>sfx_queue/queue.jsonl(직접 만드는 효과음, backlog SFX-01)이 sfx_list.md 목록과 빠짐없이 맞고,
    /// sfx_research.md '소리 갈래별로 길을 나눈다' 표의 길을 따르며, postprocess_sfx.py 가 읽는 칸을 갖췄는지.</summary>
    public class SfxQueueTests
    {
        static readonly string[] Statuses = { "todo", "generated", "picked", "imported", "rejected" };
        static readonly string[] Routes = { "synth", "ai", "layer" };
        const string Tail = "Single, dry, close-mic, no music, no reverb tail.";

        // sfx_list.md 의 절 → 갈래
        static readonly Dictionary<string, string> Sections = new()
        {
            ["무공"] = "weapon", ["적"] = "enemy", ["보상과 성장"] = "reward", ["연출"] = "scene", ["환경"] = "ambient",
        };

        static List<JObject> Rows() =>
            File.ReadAllLines(Path.Combine(RepoRoot, "sfx_queue", "queue.jsonl")).Where(l => l.Trim().Length > 0)
                .Select(JObject.Parse).ToList();

        /// <summary>sfx_list.md 표의 id → 갈래('기획서에 없는 것' 앞까지).</summary>
        static Dictionary<string, string> ListedIds()
        {
            var md = ReadRepoFile("docs", "design", "sfx_list.md");
            md = md.Substring(0, md.IndexOf("## 기획서에 없는 것"));
            var result = new Dictionary<string, string>();
            string? cat = null;
            foreach (var line in md.Split('\n'))
            {
                var h = Regex.Match(line, @"^## (\S+(?: \S+)*?)(?: \(|$)");
                if (h.Success) cat = Sections.TryGetValue(h.Groups[1].Value.Trim(), out var c) ? c : null;
                var m = Regex.Match(line, @"^\| (sfx_[a-z0-9_]+) \|");
                if (m.Success)
                {
                    Assert.NotNull(cat);
                    result.Add(m.Groups[1].Value, cat!);
                }
            }
            return result;
        }

        [Fact] public void 목록과_큐의_id가_같고_갈래도_절과_같다()
        {
            var listed = ListedIds();
            Assert.NotEmpty(listed);
            var rows = Rows();
            var ids = rows.Select(o => (string)o["id"]!).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
            Assert.Equal(listed.Keys.OrderBy(x => x), ids.OrderBy(x => x));
            foreach (var o in rows) Assert.Equal(listed[(string)o["id"]!], (string)o["category"]!);
        }

        [Fact] public void 줄마다_필수_칸이_있다()
        {
            foreach (var o in Rows())
            {
                var id = (string)o["id"]!;
                var route = (string)o["route"]!;
                Assert.Contains(route, Routes);
                Assert.Contains((string)o["status"]!, Statuses);
                Assert.False(string.IsNullOrWhiteSpace((string?)o["use"]), $"{id}: use 가 비었다");
                Assert.Equal(JTokenType.Boolean, o["loop"]!.Type);
                Assert.InRange((double)o["seconds"]!, 0.1, 30.0);  // 가장 긴 것은 빗소리 루프
                Assert.InRange((int)o["candidates"]!, 1, 12);
                Assert.Equal(JTokenType.Integer, o["seed"]!.Type);
                Assert.Equal($"art_inbox/sfx/{id}/", (string)o["save_dir"]!);
                if (o["max_s"] != null) Assert.True((double)o["max_s"]! >= (double)o["seconds"]!, $"{id}: max_s 가 seconds 보다 짧다");

                // AI 몫은 영어 문구, 코드 합성 몫은 조리법
                var usesAi = route == "ai" || (route == "layer" && ((JArray)o["layers"]!).Any(x => (string)x["from"]! == "ai"));
                Assert.Equal(usesAi, o["prompt"] != null);
                if (usesAi)
                {
                    var p = (string)o["prompt"]!;
                    Assert.DoesNotMatch("[가-힣]", p);
                    if ((bool)o["loop"]!) Assert.Contains("seamless loop", p);
                    else Assert.EndsWith(Tail, p);
                }
                if (route != "ai") Assert.False(string.IsNullOrWhiteSpace((string?)o["recipe"]), $"{id}: recipe 가 비었다");
                if (route == "layer") Assert.True(((JArray)o["layers"]!).Count >= 2, $"{id}: 겹칠 재료가 둘 이상이어야");

                // 고른 번호는 picked·imported 에만, 후보 수 안에서
                var st = (string)o["status"]!;
                if (st == "picked" || st == "imported")
                    Assert.All((JArray)o["picked"]!, k => Assert.InRange((int)k, 1, (int)o["candidates"]!));
                else Assert.Null(o["picked"]);
            }
        }

        [Fact] public void 코드_합성은_조사_표의_코드_합성_줄에_있는_소리만()
        {
            var md = ReadRepoFile("docs", "design", "sfx_research.md");
            var synthLine = md.Split('\n').First(l => l.StartsWith("| 코드 합성"));
            foreach (var o in Rows())
            {
                var id = (string)o["id"]!;
                var usesSynth = (string)o["route"]! == "synth"
                    || ((string)o["route"]! == "layer" && ((JArray)o["layers"]!).Any(x => (string)x["from"]! == "synth"));
                Assert.True(usesSynth == synthLine.Contains($"`{id}`"), $"{id}: 코드 합성 여부가 sfx_research.md 표와 다르다");
            }
        }

        [Fact] public void 다듬는_도구가_큐의_갈래를_다_안다()
        {
            var py = ReadRepoFile("tools", "postprocess_sfx.py");
            Assert.Contains("ROOT / \"sfx_queue\" / \"queue.jsonl\"", py);
            var line = py.Split('\n').First(l => l.StartsWith("CATEGORY_MAX_S"));
            foreach (var cat in Rows().Select(o => (string)o["category"]!).Distinct())
                Assert.Contains($"\"{cat}\":", line);
            // 루프는 빗소리 같은 환경 소리만
            Assert.All(Rows().Where(o => (bool)o["loop"]!), o => Assert.Equal("ambient", (string)o["category"]!));
        }
    }
}
