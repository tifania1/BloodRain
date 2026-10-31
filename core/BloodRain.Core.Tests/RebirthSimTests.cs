using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Sim;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>침방 환생 후보 시뮬레이션(docs/design/sim/rebirth.md). 숫자 자체는 보고서 몫이고 여기서는 세는 방식만 본다.</summary>
    public class RebirthSimTests
    {
        static readonly Lazy<SimData> Data = new Lazy<SimData>(() => SimData.Load(TestData.DataDir));
        static Core.Data.CheonhaDef C => Data.Value.Game.Cheonha;
        static List<string> All => C.Sects.Select(s => s.Id).ToList();

        [Fact] public void 뽑기_셈은_카드_수와_성질_수가_맞는다()
        {
            var s = RebirthSim.Draws(C, All, 3, 500, new Random(1));
            Assert.Equal(1500, s.Cards);
            Assert.Equal(s.Cards, s.TraitCount.Sum());
            Assert.Equal(s.Cards, s.Sect.Values.Sum());
            Assert.Equal(s.TraitCount[1] + 2 * s.TraitCount[2], s.Trait.Values.Sum());
            Assert.Equal(0, s.ExcludedPairs);
            Assert.Equal(0, s.SameSectTwice);
            Assert.Equal(s.Cards - s.Sect.GetValueOrDefault("shaolin"), s.FemaleEligible);
        }

        [Fact] public void 문파보다_후보가_많으면_같은_문파가_두_장()
        {
            var three = C.Sects.Where(x => x.Unlock == "start").Select(x => x.Id).ToList();
            var s = RebirthSim.Draws(C, three, 4, 200, new Random(2));
            Assert.Equal(200, s.SameSectTwice);
        }

        [Fact] public void 다_해_봤으면_0생_후보가_아홉이면_딱_아홉_생()
        {
            Assert.Equal(0, RebirthSim.LivesToAllSects(C, 3, 0, new Random(3), new HashSet<string>(All)));
            Assert.Equal(All.Count, RebirthSim.LivesToAllSects(C, All.Count, 0, new Random(3)));
            var rng = new Random(4);
            for (int i = 0; i < 200; i++)
                Assert.InRange(RebirthSim.LivesToAllSects(C, 3, 3, rng), All.Count, 500);
        }

        [Fact] public void 원하는_문파는_후보가_아홉이면_늘_나오고_다시_고르기를_늘리면_줄지_않는다()
        {
            Assert.Equal(1.0, RebirthSim.ChanceOfSect(C, All, "demonic", All.Count, 0, 100, new Random(5)));
            double p0 = RebirthSim.ChanceOfSect(C, All, "demonic", 3, 0, 4000, new Random(6));
            double p3 = RebirthSim.ChanceOfSect(C, All, "demonic", 3, 3, 4000, new Random(6));
            Assert.InRange(p0, 0.30, 0.37);           // 3/9
            Assert.InRange(p3, 0.77, 0.83);           // 1 - (2/3)^4
        }

        [Fact] public void 같은_시드면_같은_결과()
        {
            Assert.Equal(RebirthSim.LivesToAllSects(C, 3, 1, new Random(7)), RebirthSim.LivesToAllSects(C, 3, 1, new Random(7)));
        }

        [Fact] public void 보고서_성질_수_줄은_데이터_비율을_기획으로_적는다()
        {
            var md = RebirthSim.Report(Data.Value, draws: 300, souls: 30);
            var w = C.BirthTraits.CountWeights;
            double t = w.Sum();
            for (int k = 0; k < 3; k++)
                Assert.Contains($"| 성질 {k}개 | {Math.Round(w[k] / t * 100):0}% |", md);
            Assert.Contains("## 구파윤회", md);
            Assert.Contains($"| 여자 몸(소림 빼고) | {Math.Round(C.Rebirth.FemaleRatio * 100):0}% |", md);
        }
    }
}
