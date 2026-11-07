using System;
using System.IO;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>몸 그림 고르기(BD-01, 기획서 13장·21장).</summary>
    public class BodyArtTests
    {
        readonly GameData _d = TestData.Load();
        CheonhaDef C => _d.Cheonha;

        static string CharDir => Path.Combine(TestData.RepoRoot, "unity", "BloodRain", "Assets", "Art", "Characters");

        RebirthBody Body(string sect, bool female) =>
            Rebirth.Body(C, new RebirthCandidate(C.Sect(sect), C.Region(C.Sect(sect).Region), Array.Empty<BirthTraitDef>(), female));

        [Fact] public void 서장_몸은_진소운_그림()
        {
            Assert.Equal(new[] { BodyArt.Prologue }, BodyArt.SpriteIds(Rebirth.Prologue(_d)));
            Assert.True(File.Exists(Path.Combine(CharDir, BodyArt.Prologue + ".png")));
        }

        [Fact] public void 제_성별_그림을_먼저_찾고_다음에_다른_성별()
        {
            Assert.Equal(new[] { "char_gaebang_f", "char_gaebang_m" }, BodyArt.SpriteIds(Body("gaebang", true)));
            Assert.Equal(new[] { "char_namgung_m", "char_namgung_f" }, BodyArt.SpriteIds(Body("namgung", false)));
        }

        [Fact] public void M2_문파_셋은_나올_수_있는_몸마다_그림이_있다()
        {
            // 17장 M2 '문파 3(소림·개방·남궁)'. 소림은 남자만(maleOnlySects)
            foreach (var sect in new[] { "shaolin", "gaebang", "namgung" })
                foreach (var female in C.Rebirth.MaleOnlySects.Contains(sect) ? new[] { false } : new[] { false, true })
                    Assert.True(File.Exists(Path.Combine(CharDir, BodyArt.SpriteIds(Body(sect, female))[0] + ".png")),
                        $"{sect} {(female ? "여" : "남")} 몸 그림이 없음");
        }
    }
}
