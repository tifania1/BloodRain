using System.IO;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>소품 아틀라스(4096·밉맵·DXT5, 압축 전 21.3MB)는 웹 판에서 가장 큰 덩어리다(WEB-05).
    /// WebGL 만 Crunch 를 걸어 받는 양을 줄인다. 다른 플랫폼은 그대로 DXT5.</summary>
    public class AtlasWebGlTests
    {
        [Fact]
        public void 소품_아틀라스는_WebGL_만_Crunch_다()
        {
            var meta = File.ReadAllText(Path.Combine(RepoRoot, "unity", "BloodRain", "Assets", "Art", "Props", "PropParts.spriteatlasv2.meta"))
                .Replace("\r\n", "\n");
            var web = meta.IndexOf("buildTarget: WebGL");
            Assert.True(web > 0, "WebGL 덮어쓰기가 없다");
            var block = meta.Substring(web, meta.IndexOf("forceMaximumCompressionQuality", web) - web);
            Assert.Contains("overridden: 1", block);
            Assert.Contains("crunchedCompression: 1", block);
            Assert.Contains("textureFormat: 29", block); // DXT5Crunched
            Assert.Contains("maxTextureSize: 4096", block);
            var def = meta.Substring(meta.IndexOf("buildTarget: DefaultTexturePlatform"), web - meta.IndexOf("buildTarget: DefaultTexturePlatform"));
            Assert.Contains("crunchedCompression: 0", def);
        }
    }
}
