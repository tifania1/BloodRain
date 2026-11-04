#nullable enable
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BloodRain.Core.Data;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>인트로 컷신(DL-07): dialogue.json intro 가 기획서 19장 A 표와 같은지.</summary>
    public class IntroCutsTests
    {
        static DialogueDef D => Load().Dialogue;
        static System.Collections.Generic.List<string[]> Table() => GddTable(GddSection("A. 인트로 컷신 「혈우」"));

        // "(속삭임) 깨어나십시오." → "깨어나십시오.", "없음" → null
        static string? Spoken(string cell)
        {
            if (cell == "없음") return null;
            return Regex.Replace(cell, @"^\([^)]*\)\s*", "");
        }

        [Fact]
        public void 컷_열둘이_기획서_표_차례대로다()
        {
            var rows = Table();
            Assert.Equal(12, rows.Count);
            Assert.Equal(12, D.Intro.Cuts.Count);
            for (int i = 0; i < 12; i++)
            {
                var cut = D.Intro.Cuts[i];
                Assert.Equal($"c{i + 1:00}", cut.Id);
                Assert.Equal($"sb_intro_{i + 1:00}", cut.Art);
                Assert.Equal(float.Parse(rows[i][5].Replace("초", "")), cut.Seconds);
                var spoken = i == 11 ? null : Spoken(rows[i][3]);   // 컷 12 제목은 Unity 가 얹는다
                if (spoken == null) Assert.Empty(cut.Lines);
                else Assert.Equal(spoken, Assert.Single(cut.Lines).TextKo);
            }
        }

        [Fact]
        public void 합이_71초이고_속삭임과_노고수가_표시된다()
        {
            Assert.Equal(71f, D.Intro.TotalSeconds);
            Assert.Contains("인트로 컷신 12컷(약 71초)", Gdd);
            Assert.True(D.Intro.Cuts[5].Lines[0].Whisper);
            Assert.Equal("old_master", D.Intro.Cuts[9].Lines[0].Speaker);
            // 서장 각성 때 메아리로 같은 문장(prologue.md)
            Assert.Equal(D.Prologue.Lines.Single(l => l.Id == "awaken").TextKo, D.Intro.Cuts[9].Lines[0].TextKo);
        }

        [Fact]
        public void 컷_그림이_대기열과_저장소에_있다()
        {
            var queue = ReadText(Path.Combine(RepoRoot, "art_queue", "queue.jsonl"));
            foreach (var c in D.Intro.Cuts)
            {
                Assert.Contains($"\"id\": \"{c.Art}\"", queue);
                Assert.True(File.Exists(Path.Combine(RepoRoot, "unity", "BloodRain", "Assets", "Art", "Cutscenes", c.Art + ".jpg")), c.Art);
            }
        }
    }
}
