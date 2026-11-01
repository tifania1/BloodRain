using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>이펙트 텍스처(Art/VFX/Brush·Common·AI)가 Unity 에 VfxBootstrap 이 맞추는 설정으로 들어가 있는지 .meta 로 본다.
    /// Sprite·Clamp·밉맵 없음·알파 투명, 그리고 한 변 길이(json sizeMeters)대로 Pixels Per Unit = 512 / sizeMeters.
    /// PPU 가 기본값 100 이면 512px 한 장이 5.12m 로 그려져 붓 획이 몸보다 몇 배 커진다.</summary>
    public class VfxImportSettingsTests
    {
        static string VfxDir(string sub) => Path.Combine(RepoRoot, "unity", "BloodRain", "Assets", "Art", "VFX", sub);

        public static IEnumerable<object[]> Folders() => new[] { new object[] { "Brush" }, new object[] { "Common" }, new object[] { "AI" } };

        /// <summary>VfxBootstrap.Sizes 와 같은 읽기: textures[].name → sizeMeters, 그리고 Common 처럼 sizeMeters 가 {앞머리: m} 인 꼴.</summary>
        static Dictionary<string, float> Sizes(string dir)
        {
            var map = new Dictionary<string, float>();
            foreach (var json in Directory.GetFiles(dir, "*.json"))
            {
                var root = JObject.Parse(File.ReadAllText(json));
                if (root["textures"] is JArray arr)
                    foreach (var t in arr)
                        if (t["name"] != null && t["sizeMeters"] != null) map[(string)t["name"]!] = (float)t["sizeMeters"]!;
                if (root["sizeMeters"] is JObject prefixes)
                    foreach (var p in prefixes.Properties()) map[p.Name] = (float)p.Value;
            }
            return map;
        }

        static string Field(string meta, string key)
        {
            var m = Regex.Match(meta, @"^\s*" + key + @":\s*(.+?)\s*$", RegexOptions.Multiline);
            Assert.True(m.Success, key);
            return m.Groups[1].Value;
        }

        [Theory, MemberData(nameof(Folders))]
        public void 텍스처마다_meta가_있고_Sprite_Clamp_밉맵없음_알파투명이다(string sub)
        {
            var pngs = Directory.GetFiles(VfxDir(sub), "*.png");
            Assert.NotEmpty(pngs);
            foreach (var png in pngs)
            {
                var name = $"{sub}/{Path.GetFileName(png)}";
                Assert.True(File.Exists(png + ".meta"), $"{name}: .meta 가 없다(Unity 에서 한 번 열고 VfxBootstrap)");
                var meta = File.ReadAllText(png + ".meta");
                Assert.True(Field(meta, "textureType") == "8", $"{name}: Sprite 가 아니다");
                Assert.True(Field(meta, "spriteMode") == "1", $"{name}: Single 이 아니다");
                Assert.True(Field(meta, "enableMipMap") == "0", $"{name}: 밉맵이 켜져 있다");
                Assert.True(Field(meta, "alphaIsTransparency") == "1", $"{name}: 알파 투명이 꺼져 있다");
                Assert.True(Field(meta, "wrapU") == "1" && Field(meta, "wrapV") == "1", $"{name}: Clamp 가 아니다");
            }
        }

        [Theory, MemberData(nameof(Folders))]
        public void 크기가_적힌_텍스처는_PPU가_512_나누기_sizeMeters다(string sub)
        {
            var dir = VfxDir(sub);
            var sizes = Sizes(dir);
            Assert.NotEmpty(sizes);
            int matched = 0;
            foreach (var png in Directory.GetFiles(dir, "*.png"))
            {
                var stem = Path.GetFileNameWithoutExtension(png);
                var hit = sizes.Where(kv => stem == kv.Key || stem.StartsWith(kv.Key + "_"))
                    .OrderByDescending(kv => kv.Key.Length).Select(kv => (float?)kv.Value).FirstOrDefault();
                if (hit == null) continue; // noise·seal_base 처럼 크기가 없는 것은 그리는 쪽이 정한다
                matched++;
                var ppu = float.Parse(Field(File.ReadAllText(png + ".meta"), "spritePixelsToUnits"), CultureInfo.InvariantCulture);
                Assert.True(System.Math.Abs(ppu - 512f / hit.Value) < 0.01f,
                    $"{sub}/{stem}: PPU {ppu}, sizeMeters {hit} 이면 {512f / hit.Value:0.###} 이어야 한다");
            }
            Assert.True(matched > 0, $"{sub}: 크기가 맞는 텍스처가 하나도 없다");
        }
    }
}
