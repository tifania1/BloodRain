using System;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>몸 성별(T-37, 기획서 21장): 후보마다 반반, 소림은 남자만, 능력치는 같다.</summary>
    public class RebirthGenderTests
    {
        readonly GameData _d = TestData.Load();
        CheonhaDef C => _d.Cheonha;

        [Fact] public void 데이터는_반반이고_소림만_남자()
        {
            Assert.Equal(0.5f, C.Rebirth.FemaleRatio);
            Assert.Equal(new[] { "shaolin" }, C.Rebirth.MaleOnlySects);
        }

        [Fact] public void 후보_성별은_반반이고_소림은_늘_남자()
        {
            var all = C.Sects.Select(s => s.Id).ToList();
            int female = 0, other = 0;
            var rng = new Random(11);
            for (int i = 0; i < 3000; i++)
                foreach (var c in Rebirth.Roll(C, all, 3, rng))
                {
                    if (c.Sect.Id == "shaolin") { Assert.False(c.Female); continue; }
                    other++;
                    if (c.Female) female++;
                }
            Assert.InRange(female / (double)other, 0.47, 0.53);
        }

        [Fact] public void 첫_환혼도_성별을_굴리고_몸에_그대로_넘어간다()
        {
            bool sawFemale = false;
            for (int seed = 0; seed < 40; seed++)
                foreach (var c in Rebirth.First(C, new Random(seed)))
                {
                    if (c.Sect.Id == "shaolin") Assert.False(c.Female);
                    var body = Rebirth.Body(C, c);
                    Assert.Equal(c.Female, body.Female);
                    sawFemale |= c.Female;
                }
            Assert.True(sawFemale);
        }

        [Fact] public void 성별은_능력치를_바꾸지_않는다()
        {
            var sect = C.Sect("gaebang");
            var m = Rebirth.Body(C, new RebirthCandidate(sect, C.Region(sect.Region), Array.Empty<BirthTraitDef>(), female: false));
            var f = Rebirth.Body(C, new RebirthCandidate(sect, C.Region(sect.Region), Array.Empty<BirthTraitDef>(), female: true));
            Assert.Equal(m.Character.MaxHealth, f.Character.MaxHealth);
            Assert.Equal(m.Character.MoveSpeed, f.Character.MoveSpeed);
            Assert.Equal(m.YinyangStart, f.YinyangStart);
            Assert.True(f.Female);
        }
    }
}
