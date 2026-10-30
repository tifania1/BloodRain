using System;
using System.Linq;
using BloodRain.Sim;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>천하 한 생의 기연 시뮬레이션(docs/design/sim/encounters.md). 숫자 자체는 보고서 몫이고 여기서는 굴리는 방식만 본다.</summary>
    public class EncounterSimTests
    {
        static readonly Lazy<SimData> Data = new Lazy<SimData>(() => SimData.Load(TestData.DataDir));
        static readonly Lazy<CheonhaLifeResult> Shaolin = new Lazy<CheonhaLifeResult>(() => CheonhaSim.Run(Data.Value, "shaolin", 1));

        [Fact] public void 지역_흐름은_태어난_곳에서_시작해_분마다_한_칸()
        {
            var r = Shaolin.Value;
            Assert.Equal("songshan", r.RegionByMinute[0]);
            Assert.InRange(r.RegionByMinute.Count, 30, 31);
            Assert.All(r.Visited, v => Assert.Contains(v, r.RegionByMinute));
        }

        [Fact] public void 땅을_가리는_사건은_그_땅을_안_밟으면_나오지_않는다()
        {
            var g = Data.Value.Game;
            var onlyKunlun = Enumerable.Repeat("kunlun", 31).ToList();
            var rng = new Random(3);
            for (int i = 0; i < 300; i++)
            {
                var got = EncounterSim.RollLife(g, onlyKunlun, 50, rng);
                Assert.DoesNotContain("cliff", got);
                Assert.DoesNotContain("sword_tomb", got);
                foreach (var id in got)
                {
                    var e = g.Synergies.Encounter(id);
                    Assert.True(e.Chapters == null || e.Chapters.Contains("chapter6"), id);
                }
            }
        }

        [Fact] public void 같은_시드면_같은_결과()
        {
            var g = Data.Value.Game;
            var t = Shaolin.Value.RegionByMinute;
            Assert.Equal(EncounterSim.RollLife(g, t, 10, new Random(5)), EncounterSim.RollLife(g, t, 10, new Random(5)));
            int a = EncounterSim.LivesToComplete(g, new[] { t }, 30, new Random(9), 300, out var la);
            int b = EncounterSim.LivesToComplete(g, new[] { t }, 30, new Random(9), 300, out var lb);
            Assert.Equal(a, b);
            Assert.Equal(la, lb);
        }

        [Fact] public void 나올_수_없는_사건이_있으면_도감을_못_채운다()
        {
            var g = Data.Value.Game;
            var onlyKunlun = Enumerable.Repeat("kunlun", 31).ToList();
            Assert.Equal(-1, EncounterSim.LivesToComplete(g, new[] { onlyKunlun }, 50, new Random(1), 50, out _));
        }
    }
}
