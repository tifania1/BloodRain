using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>웹 판 wasm 을 줄이려고 코드 줄이기(Managed Stripping)를 올리면, 데이터 클래스의 setter·생성자가 잘려
    /// JSON 을 읽어도 값이 비는 일이 생긴다(WEB-01). Assets/link.xml 이 Core 를 통째로 남기는지 본다.</summary>
    public class LinkXmlTests
    {
        [Fact]
        public void 링크_파일이_Core_를_통째로_남긴다()
        {
            var path = Path.Combine(RepoRoot, "unity", "BloodRain", "Assets", "link.xml");
            Assert.True(File.Exists(path), "Assets/link.xml 이 없다");
            Assert.True(File.Exists(path + ".meta"), "Assets/link.xml.meta 가 없다");
            var core = XDocument.Load(path).Root!.Elements("assembly")
                .FirstOrDefault(a => (string?)a.Attribute("fullname") == "BloodRain.Core");
            Assert.NotNull(core);
            Assert.Equal("all", (string?)core!.Attribute("preserve"));
        }
    }
}
