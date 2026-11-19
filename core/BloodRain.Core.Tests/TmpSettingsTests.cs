using System.IO;
using System.Linq;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>TextMesh Pro 의 Resources 폴더는 무엇이 가리키든 말든 빌드에 통째로 들어간다(WEB-04).
    /// 기본 글꼴은 한글이 나오는 NotoSerifKR SDF 로 두고, 쓰지 않는 LiberationSans·EmojiOne 은 Resources 밖에 둔다.</summary>
    public class TmpSettingsTests
    {
        static string TmpDir => Path.Combine(RepoRoot, "unity", "BloodRain", "Assets", "TextMesh Pro");

        [Fact]
        public void 기본_글꼴이_NotoSerifKR_이다()
        {
            var kr = Path.Combine(RepoRoot, "unity", "BloodRain", "Assets", "Art", "Fonts", "NotoSerifKR SDF.asset.meta");
            var guid = File.ReadLines(kr).First(l => l.StartsWith("guid:")).Substring(5).Trim();
            var settings = File.ReadAllText(Path.Combine(TmpDir, "Resources", "TMP Settings.asset"));
            Assert.Contains($"m_defaultFontAsset: {{fileID: 11400000, guid: {guid}, type: 2}}", settings);
            Assert.Contains("m_defaultSpriteAsset: {fileID: 0}", settings);
        }

        [Fact]
        public void 안_쓰는_TMP_에셋이_Resources_에_없다()
        {
            var res = Path.Combine(TmpDir, "Resources");
            var stray = Directory.GetFiles(res, "*", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith(".meta"))
                .Select(Path.GetFileName)
                .Where(n => n!.Contains("LiberationSans") || n.Contains("EmojiOne"))
                .ToList();
            Assert.Empty(stray);
        }
    }
}
