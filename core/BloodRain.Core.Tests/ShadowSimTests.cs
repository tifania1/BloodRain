using System;
using System.Linq;
using BloodRain.Core.Spawning;
using BloodRain.Core.World;
using BloodRain.Sim;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>혈마의 그림자 떼어 내기(ShadowSim, docs/design/sim/shadow.md). 셈이 Core 와 맞는지, 보고서가 지금 데이터와 같은지 본다.</summary>
    public class ShadowSimTests
    {
        static readonly Lazy<SimData> Data = new Lazy<SimData>(() => SimData.Load(TestData.DataDir));
        static Core.Data.GameData G => Data.Value.Game;

        [Fact] public void 단계는_LifeDirector_와_같다()
        {
            var end = G.Cheonha.LifeEnd;
            var scale = new Scale(G);
            int topLevel = G.Balance.Realms.Max(r => r.Level);
            foreach (var m in new[] { 0f, 29.9f, 30f, 34.9f, 35f, 40f, 54.99f, 55f, 90f })
                Assert.Equal(LifeDirector.ShadowStep(G.Cheonha, scale, m, topLevel, 0), ShadowSim.StepAt(end, m));
        }

        [Fact] public void 현경_몸의_경공_충전은_기본에_일류_보상을_더한_것()
        {
            Assert.Equal(G.Balance.Dash.Charges + 1, ShadowSim.BaseCharges(G));
        }

        [Fact] public void 그림자보다_느리면_붙잡히고_상한이_있으면_빠른_몸은_끝내_안_잡힌다()
        {
            var slow = ShadowSim.Run(G, 90, 3, 110, 0, 0, 0);
            Assert.NotNull(slow.CaughtMinute);
            Assert.NotNull(slow.DeadMinute);
            Assert.True(slow.DeadMinute >= slow.CaughtMinute);
            var capped = ShadowSim.Options(G).First(o => o.Shadow.MaxSpeed > 0 && o.Teleport == 0);
            var fast = ShadowSim.Run(G, capped.Shadow, 0, capped.Shadow.MaxSpeed + 1, 3, 110, 0, 0, 0);
            Assert.Null(fast.CaughtMinute);
            Assert.Equal(0, fast.Hits);
        }

        [Fact] public void 붙잡히는_분은_지속_속도를_그림자가_넘는_단계_뒤다()
        {
            var end = G.Cheonha.LifeEnd;
            double speed = 100;
            double sustained = ShadowSim.SustainedSpeed(G.Balance.Dash, speed);
            int firstFaster = Enumerable.Range(1, 20).First(s => BloodShadow.Strength(end.Shadow, s).MoveSpeed > sustained);
            double from = end.ShadowFromMinute + end.ShadowStepMinutes * (firstFaster - 1);
            var run = ShadowSim.Run(G, speed, ShadowSim.BaseCharges(G), 110, 0, 0, 0);
            Assert.NotNull(run.CaughtMinute);
            Assert.True(run.CaughtMinute >= from, $"{run.CaughtMinute} < {from}");
        }

        [Fact] public void 경공을_섞은_지속_속도는_걸음보다_빠르다()
        {
            var d = G.Balance.Dash;
            Assert.True(ShadowSim.SustainedSpeed(d, 100) > 100);
            // 경공 거리 - 그동안 걸었을 거리를 충전 시간으로 나눈 만큼
            double walk = CheonhaSim.WalkMetersPerSecond;
            Assert.Equal(100 + (d.Distance - walk * d.Duration) / d.RechargeSeconds / walk * 100, ShadowSim.SustainedSpeed(d, 100), 3);
        }

        [Fact] public void 고칠_방향_첫_줄은_지금_데이터_그대로()
        {
            var opt = ShadowSim.Options(G)[0];
            Assert.Same(G.Cheonha.LifeEnd.Shadow, opt.Shadow);
            Assert.Equal(G.Cheonha.LifeEnd.Shadow.TeleportGap, opt.Teleport, 6);
            var a = ShadowSim.Run(G, 100, 3, 110, 0, 0, 0);
            var b = ShadowSim.Run(G, opt.Shadow, opt.Teleport, 100, 3, 110, 0, 0, 0);
            Assert.Equal(a.CaughtMinute, b.CaughtMinute);
            Assert.Equal(a.DeadMinute, b.DeadMinute);
            Assert.Equal(a.Hits, b.Hits);
        }

        [Fact] public void 지금_데이터는_상한이_없고_순간이동으로_더_일찍_붙인다()
        {
            var sh = G.Cheonha.LifeEnd.Shadow;
            Assert.True(sh.MaxSpeed <= 0);
            Assert.True(sh.TeleportGap > 0);
            Assert.Equal(0.5f, sh.TeleportWarningSeconds);
            // 옛 상한 125 보다 빠른 몸도 잡힌다
            Assert.NotNull(ShadowSim.Run(G, 126, 3, 110, 0, 0, 0).DeadMinute);

            var plain = ShadowSim.Run(G, sh, 0, 100, 3, 110, 0, 0, 0);
            var jumped = ShadowSim.Run(G, sh, sh.TeleportGap, 100, 3, 110, 0, 0, 0);
            Assert.True(jumped.CaughtMinute <= plain.CaughtMinute);
            Assert.True(jumped.DeadMinute <= plain.DeadMinute);
        }

        [Fact] public void 신법을_다_갖춘_몸도_100분_안에_그림자로_쓰러진다()
        {
            // T-42 목표: 운룡보 Lv5·'둘 다' 몸도 100분 안에(방어 보통)
            foreach (var c in ShadowSim.SpeedCases(G))
                foreach (var sect in G.Cheonha.Sects)
                {
                    var r = ShadowSim.RunSect(G, sect, c, false);
                    Assert.True(r.DeadMinute.HasValue && r.DeadMinute < 100, $"{sect.Id} · {c.Ko}: {r.DeadMinute}");
                }
        }

        [Fact] public void 보고서는_지금_데이터로_만든_것과_같다()
        {
            string onDisk = TestData.ReadRepoFile("docs", "design", "sim", "shadow.md").Replace("\r\n", "\n");
            Assert.Equal(ShadowSim.Report(Data.Value).Replace("\r\n", "\n"), onDisk);
        }
    }
}
