using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>Assets 아래 .meta 가 짝·guid 모두 맞는지 본다.
    /// 이미지 세션은 Unity 를 열지 않고 .meta 를 손으로 만든다(기존 meta 를 본뜸). 그때 guid 를 안 바꾸면 두 에셋이 한 guid 를 나눠
    /// Unity 가 하나를 새 guid 로 바꾸고 그걸 가리키던 참조가 끊긴다. 에셋을 지우고 .meta 만 남겨도 Unity 가 지우며 커밋이 흔들린다.</summary>
    public class MetaGuidTests
    {
        static string AssetsDir => Path.Combine(RepoRoot, "unity", "BloodRain", "Assets");

        static string[] Metas() => Directory.GetFiles(AssetsDir, "*.meta", SearchOption.AllDirectories);

        static string Rel(string path) => Path.GetRelativePath(AssetsDir, path).Replace('\\', '/');

        [Fact]
        public void meta마다_32자리_guid가_있고_겹치지_않는다()
        {
            var metas = Metas();
            Assert.NotEmpty(metas);
            var guids = metas.Select(m =>
            {
                var g = Regex.Match(File.ReadAllText(m), @"^guid: ([0-9a-f]{32})\s*$", RegexOptions.Multiline);
                Assert.True(g.Success, $"{Rel(m)}: guid 줄이 없거나 32자리 16진수가 아니다");
                return (guid: g.Groups[1].Value, path: Rel(m));
            }).ToList();

            var dups = guids.GroupBy(x => x.guid).Where(g => g.Count() > 1)
                .Select(g => $"{g.Key}: {string.Join(", ", g.Select(x => x.path))}").ToList();
            Assert.True(dups.Count == 0, "guid 가 겹친다(손으로 만든 .meta 의 guid 를 새로 뽑을 것):\n" + string.Join("\n", dups));
        }

        [Fact]
        public void meta만_남은_것이_없다()
        {
            var orphans = Metas().Where(m =>
            {
                var asset = m.Substring(0, m.Length - ".meta".Length);
                return !File.Exists(asset) && !Directory.Exists(asset);
            }).Select(Rel).ToList();
            Assert.True(orphans.Count == 0, "에셋 없이 .meta 만 있다:\n" + string.Join("\n", orphans));
        }

        [Fact]
        public void 에셋마다_meta가_있다()
        {
            var entries = Directory.GetFileSystemEntries(AssetsDir, "*", SearchOption.AllDirectories)
                .Where(p => !p.EndsWith(".meta", StringComparison.Ordinal))
                .Where(p => !Path.GetFileName(p).StartsWith(".", StringComparison.Ordinal)) // Unity 가 무시하는 숨김 파일
                .Where(p => !Path.GetFileName(p).EndsWith("~", StringComparison.Ordinal));
            var missing = entries.Where(p => !File.Exists(p + ".meta")).Select(Rel).ToList();
            Assert.True(missing.Count == 0, ".meta 가 없다(Unity 가 새 guid 를 만들어 PC 커밋에 섞여 들어간다):\n" + string.Join("\n", missing));
        }
    }
}
