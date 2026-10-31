using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>
    /// 데이터 JSON 에 나오는 글자가 Unity 에 들어 있는 글꼴로 다 찍히는지.
    /// 본문·HUD 는 Noto Serif KR(기획서 12장), 거기 없는 한자는 Yuji Boku 로 대체한다(TMP 대체 글꼴).
    /// </summary>
    public class FontCoverageTests
    {
        static string FontDir => Path.Combine(RepoRoot, "unity", "BloodRain", "Assets", "Art", "Fonts");

        static readonly Lazy<HashSet<int>> Body = new Lazy<HashSet<int>>(() => Cmap.Read(Path.Combine(FontDir, "NotoSerifKR-Regular.otf")));
        static readonly Lazy<HashSet<int>> Brush = new Lazy<HashSet<int>>(() => Cmap.Read(Path.Combine(FontDir, "YujiBoku-Regular.ttf")));

        /// <summary>데이터 파일마다 나오는 글자(공백·제어 문자 빼고).</summary>
        static IEnumerable<(string file, int cp)> DataChars()
        {
            foreach (var p in Directory.GetFiles(DataDir, "*.json").OrderBy(p => p, StringComparer.Ordinal))
            {
                var text = File.ReadAllText(p);
                var seen = new HashSet<int>();
                for (int i = 0; i < text.Length; i++)
                {
                    int cp = char.ConvertToUtf32(text, i);
                    if (char.IsSurrogatePair(text, i)) i++;
                    if (cp < 0x21 || cp == 0x7F) continue;
                    if (seen.Add(cp)) yield return (Path.GetFileName(p), cp);
                }
            }
        }

        [Fact] public void 본문_글꼴과_라이선스가_있다()
        {
            Assert.True(File.Exists(Path.Combine(FontDir, "NotoSerifKR-OFL.txt")));
            var body = Body.Value;
            Assert.True(body.Count > 20000, $"Noto Serif KR 글자 수 {body.Count}");
            foreach (var ch in "가힣혈우강호중원숭산소림ABCxyz0123456789%+-×·")
                Assert.Contains((int)ch, body);
        }

        [Fact] public void 데이터의_글자는_본문_글꼴이나_대체_글꼴에_모두_있다()
        {
            var missing = DataChars().Where(t => !Body.Value.Contains(t.cp) && !Brush.Value.Contains(t.cp))
                .Select(t => $"{t.file}: {char.ConvertFromUtf32(t.cp)} (U+{t.cp:X4})").ToList();
            Assert.True(missing.Count == 0, "어느 글꼴에도 없는 글자: " + string.Join(", ", missing));
        }

        [Fact] public void 대체_글꼴로_가는_글자는_적다()
        {
            // 붓글씨 글꼴이 본문에 섞이면 눈에 띈다. 늘어나면 대체를 Noto Serif TC 로 바꿀지 본다.
            var viaBrush = DataChars().Where(t => !Body.Value.Contains(t.cp)).Select(t => t.cp).Distinct()
                .Select(char.ConvertFromUtf32).OrderBy(s => s, StringComparer.Ordinal).ToList();
            Assert.Equal(new[] { "丐", "罩" }, viaBrush);
        }

        /// <summary>sfnt(TTF·OTF) 의 cmap 에서 유니코드 코드포인트만 읽는다. 형식 4·12 만.</summary>
        static class Cmap
        {
            public static HashSet<int> Read(string path)
            {
                var b = File.ReadAllBytes(path);
                int numTables = U16(b, 4);
                int cmap = -1;
                for (int i = 0; i < numTables; i++)
                {
                    int rec = 12 + i * 16;
                    if (b[rec] == 'c' && b[rec + 1] == 'm' && b[rec + 2] == 'a' && b[rec + 3] == 'p') cmap = (int)U32(b, rec + 8);
                }
                if (cmap < 0) throw new InvalidDataException(path + ": cmap 없음");

                var set = new HashSet<int>();
                int n = U16(b, cmap + 2);
                for (int i = 0; i < n; i++)
                {
                    int rec = cmap + 4 + i * 8;
                    int platform = U16(b, rec), encoding = U16(b, rec + 2);
                    int sub = cmap + (int)U32(b, rec + 4);
                    bool unicode = platform == 0 || (platform == 3 && (encoding == 1 || encoding == 10));
                    if (!unicode) continue;
                    int format = U16(b, sub);
                    if (format == 12) ReadFormat12(b, sub, set);
                    else if (format == 4) ReadFormat4(b, sub, set);
                }
                return set;
            }

            static void ReadFormat12(byte[] b, int sub, HashSet<int> set)
            {
                uint groups = U32(b, sub + 12);
                for (uint g = 0; g < groups; g++)
                {
                    int p = sub + 16 + (int)g * 12;
                    uint start = U32(b, p), end = U32(b, p + 4);
                    for (uint c = start; c <= end; c++) set.Add((int)c);
                }
            }

            static void ReadFormat4(byte[] b, int sub, HashSet<int> set)
            {
                int segX2 = U16(b, sub + 6);
                int ends = sub + 14, starts = ends + segX2 + 2, deltas = starts + segX2, offsets = deltas + segX2;
                for (int s = 0; s < segX2; s += 2)
                {
                    int end = U16(b, ends + s), start = U16(b, starts + s), delta = U16(b, deltas + s), ro = U16(b, offsets + s);
                    for (int c = start; c <= end && c != 0xFFFF; c++)
                    {
                        int glyph;
                        if (ro == 0) glyph = (c + delta) & 0xFFFF;
                        else
                        {
                            int at = offsets + s + ro + (c - start) * 2;
                            glyph = U16(b, at);
                            if (glyph != 0) glyph = (glyph + delta) & 0xFFFF;
                        }
                        if (glyph != 0) set.Add(c);
                    }
                }
            }

            static int U16(byte[] b, int p) => (b[p] << 8) | b[p + 1];
            static uint U32(byte[] b, int p) => (uint)((b[p] << 24) | (b[p + 1] << 16) | (b[p + 2] << 8) | b[p + 3]);
        }
    }
}
