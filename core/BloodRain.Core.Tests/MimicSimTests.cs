using System;
using System.Linq;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using BloodRain.Sim;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>혈영 분신 피해(MimicSim, docs/design/sim/mimic.md). 셈이 Core Mimicry 값에 맞는지, 보고서가 지금 데이터와 같은지 본다.</summary>
    public class MimicSimTests
    {
        static readonly Lazy<SimData> Data = new Lazy<SimData>(() => SimData.Load(TestData.DataDir));
        static GameData G => Data.Value.Game;

        [Fact] public void 피해_배율에_거의_비례하고_호신은_줄이기만_한다()
        {
            var def = G.Weapons.First(w => w.Id == "samjae_sword");
            var half = new EnemyMimic { DamageScale = 0.5f, CooldownScale = 1, AllWeapons = true };
            var quarter = new EnemyMimic { DamageScale = 0.25f, CooldownScale = 1, AllWeapons = true };
            double a = MimicSim.Dps(Data.Value, def, 8, half, 0), b = MimicSim.Dps(Data.Value, def, 8, quarter, 0);
            Assert.InRange(a / b, 1.99, 2.01);
            Assert.True(MimicSim.Dps(Data.Value, def, 8, half, 5) < a);
        }

        [Fact] public void 쿨_배율_2면_초당_피해가_절반()
        {
            var def = G.Weapons.First(w => w.Id == "blood_claw");
            var one = new EnemyMimic { DamageScale = 0.3f, CooldownScale = 1 };
            var two = new EnemyMimic { DamageScale = 0.3f, CooldownScale = 2 };
            Assert.InRange(MimicSim.Dps(Data.Value, def, 8, one, 0) / MimicSim.Dps(Data.Value, def, 8, two, 0), 1.99, 2.01);
        }

        [Fact] public void 상시_무공도_0이나_무한이_아니다()
        {
            foreach (var def in G.Weapons)
            {
                double d = MimicSim.Dps(Data.Value, def, 8, MimicSim.BossMimic(G), 0);
                Assert.True(d > 0 && !double.IsInfinity(d) && !double.IsNaN(d), def.Id);
            }
        }

        [Fact] public void 보고서가_지금_데이터와_같다()
        {
            string onDisk = TestData.ReadRepoFile("docs", "design", "sim", "mimic.md").Replace("\r\n", "\n");
            Assert.Equal(MimicSim.Report(Data.Value).Replace("\r\n", "\n"), onDisk);
        }
    }
}
