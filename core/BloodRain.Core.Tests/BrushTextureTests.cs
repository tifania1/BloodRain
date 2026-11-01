using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>tools/bake_brush.py 가 구운 붓 획 텍스처(V-01, docs/design/vfx.md). 그림 내용은 사람이 미리보기로 보고, 여기선 틀만 본다.</summary>
    public class BrushTextureTests
    {
        static readonly string Dir = Path.GetFullPath(Path.Combine(TestData.DataDir, "..", "..", "Art", "VFX", "Brush"));

        static (int w, int h, byte colorType) Header(string path)
        {
            var b = File.ReadAllBytes(path);
            Assert.True(b.Length > 33, path);
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, b.Take(8).ToArray());
            Assert.Equal("IHDR", System.Text.Encoding.ASCII.GetString(b, 12, 4));
            int Be(int o) => (b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3];
            return (Be(16), Be(20), b[25]);
        }

        [Fact] public void 삼재검법_1_2_3단_각_세_장이_설명_파일과_맞다()
        {
            var meta = JObject.Parse(File.ReadAllText(Path.Combine(Dir, "samjae.json")));
            var tex = meta["textures"]!.ToArray();
            Assert.Equal(9, tex.Length);
            foreach (int tier in new[] { 1, 2, 3 })
                Assert.Equal(3, tex.Count(t => (int)t["tier"]! == tier));
            var expected = tex.SelectMany(t => t["layers"]!.Select(l => $"{t["name"]}_{l}.png")).OrderBy(x => x, StringComparer.Ordinal).ToList();
            var actual = Directory.GetFiles(Dir, "samjae_t*.png").Select(f => Path.GetFileName(f)!).OrderBy(x => x, StringComparer.Ordinal).ToList();
            Assert.Equal(expected, actual);
            Assert.Equal("samjae_sword", (string)meta["weapon"]!);
            // 1단 먹, 2단 담채, 3단 금빛 테두리(vfx.md 단계 규칙)
            Assert.All(tex, t => Assert.Equal((int)t["tier"]! switch { 1 => "stroke", 2 => "stroke,wash", _ => "stroke,wash,gold" },
                string.Join(",", t["layers"]!.Select(l => (string)l!))));
            Assert.All(tex, t => Assert.True((double)t["sizeMeters"]! > 2 * (double)t["R"]!));
        }

        [Fact] public void 시작_무공_아홉이_모두_3단까지_구워져_있다()
        {
            var weapons = TestData.Load().Weapons.Where(w => w.Unlock == "start").Select(w => w.Id).OrderBy(x => x).ToList();
            var baked = Directory.GetFiles(Dir, "*.json").Select(f => JObject.Parse(File.ReadAllText(f))).ToList();
            Assert.Equal(weapons, baked.Select(m => (string)m["weapon"]!).OrderBy(x => x).ToList());
            foreach (var m in baked)
            {
                var tex = m["textures"]!.ToArray();
                Assert.Equal(new[] { 1, 2, 3 }, tex.Select(t => (int)t["tier"]!).Distinct().OrderBy(x => x));
                foreach (var t in tex)
                    foreach (var l in t["layers"]!)
                        Assert.True(File.Exists(Path.Combine(Dir, $"{t["name"]}_{l}.png")), $"{t["name"]}_{l}");
            }
        }

        [Fact] public void 텍스처는_512px_알파가_있는_PNG다()
        {
            foreach (var f in Directory.GetFiles(Dir, "*.png"))
            {
                var (w, h, ct) = Header(f);
                Assert.Equal(512, w);
                Assert.Equal(512, h);
                Assert.Equal(6, ct); // RGBA
            }
            Assert.True(File.Exists(Path.Combine(TestData.DataDir, "..", "..", "..", "..", "..", "docs", "vfx_preview", "brush_samjae.png")));
        }
    }
}
