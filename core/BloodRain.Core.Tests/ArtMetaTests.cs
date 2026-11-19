using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>원화가 Sprite 로 들여와졌는지 본다(B-12).
    /// Unity 가 처음 만든 meta 는 Default 텍스처라 SpriteRenderer·Image 에 꽂히지 않는다. 고치는 길은 `python tools/fix_art_meta.py`.
    /// 규칙은 그 파일 머리글. 참고 시트(Characters/Sheets)·컷신·스토어 시안·VFX(VfxBootstrap 이 맡음)는 보지 않는다.</summary>
    public class ArtMetaTests
    {
        static readonly string[] SpriteFolders =
            { "Characters", "Enemies", "Bosses", "Props", "Portraits", "UI", "Tiles", "Map", "Cards", "Backgrounds", "Icons" };

        static string ArtDir => Path.Combine(RepoRoot, "unity", "BloodRain", "Assets", "Art");

        static string Field(string text, string key)
        {
            var m = Regex.Match(text, $@"^\s{{2,4}}{key}: (\S+)\s*$", RegexOptions.Multiline);
            return m.Success ? m.Groups[1].Value : "";
        }

        [Fact]
        public void 원화_텍스처는_Sprite_한_장으로_들여온다()
        {
            var bad = SpriteFolders
                .Select(f => Path.Combine(ArtDir, f))
                .Where(Directory.Exists)
                .SelectMany(d => Directory.GetFiles(d, "*.meta", SearchOption.AllDirectories))
                .Where(m => !m.Replace('\\', '/').Contains("/Characters/Sheets/"))
                .Where(m => new[] { ".png", ".jpg", ".jpeg" }.Contains(Path.GetExtension(m[..^5]).ToLowerInvariant()))
                .Select(m => (path: Path.GetRelativePath(ArtDir, m).Replace('\\', '/'), text: File.ReadAllText(m)))
                .Where(x => Field(x.text, "textureType") != "8" || Field(x.text, "spriteMode") != "1")
                .Select(x => x.path)
                .ToList();
            Assert.True(bad.Count == 0,
                "Sprite 가 아닌 원화 meta(python tools/fix_art_meta.py 로 고칠 것):\n" + string.Join("\n", bad));
        }

        [Fact]
        public void 배경을_지운_원화는_알파를_투명으로_쓴다()
        {
            var bad = new[] { "Characters", "Enemies", "Bosses", "Props", "Portraits", "UI" }
                .Select(f => Path.Combine(ArtDir, f))
                .Where(Directory.Exists)
                .SelectMany(d => Directory.GetFiles(d, "*.png.meta", SearchOption.AllDirectories))
                .Where(m => !m.Replace('\\', '/').Contains("/Characters/Sheets/"))
                .Where(m => Field(File.ReadAllText(m), "alphaIsTransparency") != "1")
                .Select(m => Path.GetRelativePath(ArtDir, m).Replace('\\', '/'))
                .ToList();
            Assert.True(bad.Count == 0, "alphaIsTransparency 가 꺼진 원화:\n" + string.Join("\n", bad));
        }

        [Fact]
        public void 원화_종류의_폴더는_모두_Sprite_규칙이_안다()
        {
            // postprocess_art.py KINDS 에 새 종류가 생기면 fix_art_meta.py 와 이 테스트도 같이 고쳐야 한다.
            var post = File.ReadAllText(Path.Combine(RepoRoot, "tools", "postprocess_art.py"));
            var skip = new[] { "Characters/Sheets", "Cutscenes", "Store", "VFX/AI" };
            var kinds = Regex.Matches(post, @"^\s+""[a-z_]+"": \(""([A-Za-z/]+)"",", RegexOptions.Multiline)
                .Select(m => m.Groups[1].Value).Distinct().ToList();
            Assert.Contains("Enemies", kinds);
            var unknown = kinds.Where(k => !skip.Contains(k) && !SpriteFolders.Contains(k)).ToList();
            Assert.True(unknown.Count == 0, "Sprite 규칙에 없는 원화 폴더: " + string.Join(", ", unknown));

            var fix = File.ReadAllText(Path.Combine(RepoRoot, "tools", "fix_art_meta.py"));
            var sets = Regex.Matches(fix, @"^(?:ALPHA|WORLD|SCREEN) = \{([^}]*)\}", RegexOptions.Multiline)
                .SelectMany(m => Regex.Matches(m.Groups[1].Value, @"""(\w+)""").Select(x => x.Groups[1].Value))
                .Distinct().OrderBy(x => x).ToList();
            Assert.Equal(SpriteFolders.OrderBy(x => x).ToList(), sets);
        }
        [Fact]
        public void 소품_낱장은_가로세로가_4의_배수다()
        {
            // DXT 압축 조건. 아니면 RGBA32 로 실려 웹 판이 무거워진다(WEB-01). 고치는 길: python tools/pad_mult4.py
            var bad = Directory.GetFiles(Path.Combine(ArtDir, "Props", "Parts"), "*.png")
                .Select(p => (name: Path.GetFileName(p), size: PngSize(p)))
                .Where(x => x.size.w % 4 != 0 || x.size.h % 4 != 0)
                .Select(x => $"{x.name} {x.size.w}x{x.size.h}")
                .ToList();
            Assert.True(bad.Count == 0, "4의 배수가 아닌 소품 낱장:\n" + string.Join("\n", bad));
        }

        static (int w, int h) PngSize(string path)
        {
            var b = new byte[24];
            using (var f = File.OpenRead(path)) f.Read(b, 0, 24);
            int U32(int o) => (b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3];
            return (U32(16), U32(20));
        }
    }
}
