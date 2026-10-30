using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>art_queue 프롬프트가 기획서 13장 '용도별 틀'의 고정 문장을 지키는지(T-34).</summary>
    public class ArtFrameTests
    {
        static string Frame(string name)
        {
            var sec = GddSection("용도별 틀");
            var m = Regex.Match(sec, @"\[" + Regex.Escape(name) + @"\]\n([^\n]+)");
            Assert.True(m.Success, $"13장에 [{name}] 틀이 없음");
            return m.Groups[1].Value;
        }

        /// <summary>틀에서 {…} 칸을 뺀 고정 문장들.</summary>
        static string[] Fixed(string frame) =>
            Regex.Split(frame, @"\{[^}]*\}").Select(x => x.Trim()).Where(x => x.Length > 12).ToArray();

        static JObject[] Queue(string kind) =>
            File.ReadAllLines(Path.Combine(RepoRoot, "art_queue", "queue.jsonl")).Where(l => l.Trim().Length > 0)
                .Select(JObject.Parse).Where(o => (string)o["kind"]! == kind).ToArray();

        [Theory]
        [InlineData("card", "환생 카드(세로 2:3)", "2:3")]
        [InlineData("background", "화면 배경", "3:2")]
        [InlineData("map", "지도 배경", "3:2")]
        public void 그_종류의_줄은_틀의_고정_문장을_모두_담는다(string kind, string frame, string ratio)
        {
            var parts = Fixed(Frame(frame));
            Assert.NotEmpty(parts);
            var rows = Queue(kind);
            Assert.NotEmpty(rows);
            foreach (var o in rows)
            {
                var p = (string)o["prompt"]!;
                Assert.StartsWith("{STYLE}", p);
                Assert.Equal(ratio, (string)o["ratio"]!);
                foreach (var part in parts)
                    foreach (var sentence in part.Split(". ").Select(x => x.Trim().TrimEnd('.')).Where(x => x.Length > 12))
                        Assert.True(p.Contains(sentence), $"{o["id"]}: '{sentence}' 없음");
            }
        }
    }
}
