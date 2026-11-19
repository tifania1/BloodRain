using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>게임 글꼴은 쓰는 글자만 남긴 판이다(WEB-01, tools/subset_fonts.py).
    /// 데이터에 새 글자를 넣었는데 줄인 글꼴에 없으면 화면에 네모가 뜬다. 원본 글꼴(tools/fonts_src)에 있던 글자가
    /// 줄인 글꼴 셋(KR·SC·TC, 본문 글꼴과 대체 글꼴)에 하나도 없으면 실패한다. 고치는 길: python tools/subset_fonts.py</summary>
    public class FontSubsetTests
    {
        static readonly string[] Body = { "NotoSerifKR-Regular.otf", "NotoSerifSC-Regular.otf", "NotoSerifTC-Regular.otf" };
        static string Assets => Path.Combine(RepoRoot, "unity", "BloodRain", "Assets");
        static string GameFonts => Path.Combine(Assets, "Art", "Fonts");
        static string SourceFonts => Path.Combine(RepoRoot, "tools", "fonts_src");

        [Fact]
        public void 데이터에_쓴_글자는_줄인_글꼴에_있다()
        {
            var have = new HashSet<int>(Body.SelectMany(f => Cmap(Path.Combine(GameFonts, f))));
            var could = new HashSet<int>(Body.SelectMany(f => Cmap(Path.Combine(SourceFonts, f))));
            var used = Directory.GetFiles(Path.Combine(Assets, "Resources", "Data"), "*.json", SearchOption.AllDirectories)
                .SelectMany(p => CodePoints(File.ReadAllText(p, Encoding.UTF8)))
                .Where(c => c > 0x20)
                .ToHashSet();
            var missing = used.Where(c => could.Contains(c) && !have.Contains(c)).OrderBy(c => c).ToList();
            Assert.True(missing.Count == 0,
                $"줄인 글꼴에 없는 글자 {missing.Count}개(python tools/subset_fonts.py 로 다시 줄일 것): "
                + string.Concat(missing.Take(40).Select(char.ConvertFromUtf32)));
        }

        [Fact]
        public void 게임_글꼴은_줄인_판이다()
        {
            // 원본을 Assets 에 도로 넣으면 웹 판이 30MB 넘게 무거워진다
            foreach (var f in Body.Append("YujiBoku-Regular.ttf"))
            {
                long game = new FileInfo(Path.Combine(GameFonts, f)).Length;
                long src = new FileInfo(Path.Combine(SourceFonts, f)).Length;
                Assert.True(game * 2 < src, $"{f} 가 원본과 크기가 비슷하다({game / 1000}KB / {src / 1000}KB). python tools/subset_fonts.py");
            }
        }

        static IEnumerable<int> CodePoints(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length) { yield return char.ConvertToUtf32(s[i], s[i + 1]); i++; }
                else yield return s[i];
            }
        }

        static HashSet<int> Cmap(string path) => FontCoverageTests.Cmap.Read(path);
    }
}
