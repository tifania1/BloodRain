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

        [Fact] public void 잘린_텍스처는_meta_피벗이_몸_위치이고_Tight_메시다()
        {
            // VfxBootstrap.ImportSettings 와 같은 값: alignment 9(Custom), spritePivot = json pivot, spriteMeshType 1(Tight)
            foreach (var json in Directory.GetFiles(Dir, "*.json"))
                foreach (var t in JObject.Parse(File.ReadAllText(json))["textures"]!)
                {
                    var p = t["pivot"]!.Select(v => (double)v).ToArray();
                    foreach (var l in t["layers"]!)
                    {
                        var name = $"{t["name"]}_{l}.png";
                        var meta = File.ReadAllText(Path.Combine(Dir, name + ".meta"));
                        Assert.Matches(@"\n  alignment: 9\r?\n", meta);
                        Assert.Matches(@"\n  spriteMeshType: 1\r?\n", meta);
                        var m = System.Text.RegularExpressions.Regex.Match(meta, @"spritePivot: \{x: ([-0-9.eE]+), y: ([-0-9.eE]+)\}");
                        Assert.True(m.Success, name);
                        Assert.True(System.Math.Abs(p[0] - double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)) < 1e-4, name);
                        Assert.True(System.Math.Abs(p[1] - double.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)) < 1e-4, name);
                    }
                }
        }

        [Fact] public void 공통_텍스처_낙관_바탕_먹_튐_6_바닥_먹_자국_4_노이즈_2()
        {
            var dir = Path.GetFullPath(Path.Combine(Dir, "..", "Common"));
            var names = Directory.GetFiles(dir, "*.png").Select(f => Path.GetFileName(f)!).ToList();
            Assert.Single(names, n => n == "seal_base.png");
            Assert.Equal(6, names.Count(n => n.StartsWith("ink_splat_")));
            Assert.Equal(4, names.Count(n => n.StartsWith("ground_ink_")));
            Assert.Equal(2, names.Count(n => n.StartsWith("noise_")));
            Assert.Equal(13, names.Count);
            Assert.True(File.Exists(Path.Combine(dir, "common.json")));
            foreach (var n in names)
            {
                var (w, h, ct) = Header(Path.Combine(dir, n));
                Assert.Equal((512, 512), (w, h));
                Assert.Equal(n.StartsWith("noise_") ? 0 : 6, ct); // 노이즈는 회색 한 채널, 나머지는 흰색 + 알파
            }
        }

        [Fact] public void 텍스처는_알파가_있는_PNG이고_json_crop_크기로_잘려_있다()
        {
            // V-10 (2): 512 칸에 그린 뒤 층들이 함께 덮는 쪽만 잘라 쓴다. 너비·높이는 4의 배수(휴대폰 압축 블록), 층은 같은 크기.
            int listed = 0;
            foreach (var json in Directory.GetFiles(Dir, "*.json"))
                foreach (var t in JObject.Parse(File.ReadAllText(json))["textures"]!)
                {
                    var name = (string)t["name"]!;
                    var c = t["crop"]!.Select(v => (int)v).ToArray();
                    Assert.Equal(4, c.Length);
                    int x = c[0], y = c[1], cw = c[2], ch = c[3];
                    Assert.True(x >= 0 && y >= 0 && cw > 0 && ch > 0 && x + cw <= 512 && y + ch <= 512, $"{name}: crop {string.Join(",", c)}");
                    Assert.True(cw % 4 == 0 && ch % 4 == 0, $"{name}: {cw}x{ch} 가 4의 배수가 아니다");
                    foreach (var l in t["layers"]!)
                    {
                        var (w, h, _) = Header(Path.Combine(Dir, $"{name}_{l}.png"));
                        Assert.True((w, h) == (cw, ch), $"{name}_{l}: PNG {w}x{h}, crop {cw}x{ch}");
                        listed++;
                    }
                    // 피벗 = 원래 칸 가운데(몸)가 잘린 그림의 어디인가(왼쪽 아래 0, 위로 +)
                    var p = t["pivot"]!.Select(v => (double)v).ToArray();
                    Assert.True(System.Math.Abs((256.0 - x) / cw - p[0]) < 1e-4, $"{name}: pivot x {p[0]}");
                    Assert.True(System.Math.Abs((y + ch - 256.0) / ch - p[1]) < 1e-4, $"{name}: pivot y {p[1]}");
                }
            foreach (var f in Directory.GetFiles(Dir, "*.png"))
            {
                var (w, h, ct) = Header(f);
                Assert.True(w <= 512 && h <= 512, f);
                Assert.Equal(6, ct); // RGBA
            }
            Assert.Equal(Directory.GetFiles(Dir, "*.png").Length, listed);
            Assert.True(File.Exists(Path.Combine(TestData.DataDir, "..", "..", "..", "..", "..", "docs", "vfx_preview", "brush_samjae.png")));
        }
    }
}
