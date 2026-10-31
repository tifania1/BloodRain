using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BloodRain.Sim;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>시뮬레이터 명령줄(Program.cs)과 사용 설명(docs/design/sim/README.md)이 서로 맞는지 본다.</summary>
    public class SimCommandLineTests
    {
        static string Readme => TestData.ReadRepoFile("docs", "design", "sim", "README.md");
        static string Source => TestData.ReadRepoFile("core", "BloodRain.Sim", "Program.cs");

        static string[] Cases() =>
            Regex.Matches(Source, "case \"(--[a-z-]+)\"").Select(m => m.Groups[1].Value).Distinct().ToArray();

        static string Help()
        {
            var old = Console.Out;
            var w = new StringWriter();
            try { Console.SetOut(w); Assert.Equal(0, Program.Main(new[] { "--help" })); }
            finally { Console.SetOut(old); }
            return w.ToString();
        }

        [Fact] public void README_에_적힌_인자는_모두_받는다()
        {
            var cases = Cases();
            var inReadme = Regex.Matches(Readme, @"(?:-- |`)(--[a-z][a-z-]*)").Select(m => m.Groups[1].Value).Distinct().ToList();
            Assert.NotEmpty(inReadme);
            foreach (var a in inReadme) Assert.Contains(a, cases);
        }

        [Fact] public void 받는_인자는_모두_README_에_있다()
        {
            foreach (var a in Cases().Where(a => a != "--help"))
                Assert.True(Readme.Contains(a), $"{a} 가 sim/README.md 에 없다");
        }

        [Fact] public void 도움말이_모든_인자를_보여_준다()
        {
            var help = Help();
            foreach (var a in Cases().Where(a => a != "--help"))
                Assert.True(help.Contains(a), $"--help 에 {a} 가 없다");
        }

        [Fact] public void README_의_조정_시험_목록이_Scenarios_와_같다()
        {
            var m = Regex.Match(Readme, @"id는 `Scenarios\.cs`\(([^)]*)\)");
            Assert.True(m.Success, "README 의 --scenario 줄 형식이 바뀌었다");
            var listed = m.Groups[1].Value.Split(',').Select(x => x.Trim()).ToList();
            Assert.Equal(Scenarios.All.Select(s => s.Id).OrderBy(x => x), listed.OrderBy(x => x));
            Assert.Contains("kill-xp", Help());
        }

        [Fact] public void 모르는_인자는_2를_돌려준다()
        {
            var old = Console.Error;
            try { Console.SetError(TextWriter.Null); Assert.Equal(2, Program.Main(new[] { "--없는-인자" })); }
            finally { Console.SetError(old); }
        }
    }
}
