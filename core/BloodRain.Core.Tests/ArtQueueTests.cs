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
        static readonly string[] Statuses = { "todo", "picked", "imported" };

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
                Assert.StartsWith("{STYLE}", (string)o["prompt"]!);
                Assert.False(string.IsNullOrWhiteSpace((string?)o["chat"]), $"{id}: chat 이 비었다");
                Assert.StartsWith("art_inbox/", (string)o["save_as"]!);
                Assert.Matches(@"^\d+:\d+$", (string)o["ratio"]!);
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

        [Fact] public void imported_줄은_Unity_Art_폴더에_파일이_있다()
        {
            var kinds = Kinds();
            var jpg = new[] { "storyboard", "map", "card", "background" };
            foreach (var o in Rows().Where(o => (string)o["status"]! == "imported"))
            {
                var kind = (string)o["kind"]!;
                var file = (string)o["id"]! + (jpg.Contains(kind) ? ".jpg" : ".png");
                var path = Path.Combine(new[] { RepoRoot, "unity", "BloodRain", "Assets", "Art" }
                    .Concat(kinds[kind].Split('/')).Append(file).ToArray());
                Assert.True(File.Exists(path), $"{o["id"]}: imported 인데 {path} 가 없다");
            }
        }
    }
}
