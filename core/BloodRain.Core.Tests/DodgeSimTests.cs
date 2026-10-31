using System;
using System.Linq;
using BloodRain.Core.Combat;
using BloodRain.Sim;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>땅의 주인 패턴 피하기(DodgeSim, docs/design/sim/dodge.md). 거리 셈이 Core 판정과 맞는지, 보고서가 지금 데이터와 같은지 본다.</summary>
    public class DodgeSimTests
    {
        static readonly Lazy<SimData> Data = new Lazy<SimData>(() => SimData.Load(TestData.DataDir));
        static Core.Data.GameData G => Data.Value.Game;
        static Core.Data.BossPatternDef P(string boss, string id) => G.BossPatterns[boss].Pattern(id);

        [Fact] public void 동그라미_하나는_반경에_몸_반지름을_더한_거리()
        {
            var palm = P("hyeokryeon_hwi", "giant_blood_palm");
            Assert.InRange(DodgeSim.CirclesEscape(palm).Single(), palm.Radius + DodgeSim.BodyRadius, palm.Radius + DodgeSim.BodyRadius + 0.051);
        }

        [Fact] public void 빠져나간_자리는_정말_판정_밖이고_조금_덜_가면_안이다()
        {
            var cone = P("wongak", "vajra_cone");
            foreach (var r in new[] { 2f, 5f, 8f })
            {
                double e = DodgeSim.ConeEscape(cone, r);
                Assert.True(e > 0);
                // 가운데 줄 위 그 자리에서 e - 0.1 만큼은 어느 쪽으로 가도 맞는다
                for (int k = 0; k < 36; k++)
                {
                    double a = 2 * Math.PI * k / 36, d = e - 0.1;
                    Assert.True(BossPatterns.InCone(cone, (float)(Math.Cos(a) * d) + r, (float)(Math.Sin(a) * d), 0f, DodgeSim.BodyRadius));
                }
            }
        }

        [Fact] public void 부채꼴의_가장_먼_자리는_옆과_끝이_같아지는_곳_근처()
        {
            var cone = P("wongak", "vajra_cone");
            double half = cone.AngleDegrees * 0.5 * Math.PI / 180;
            // 옆으로 r·sin(반각), 바깥으로 반경 - r. 둘에 몸 반지름이 붙는다.
            double r = cone.Radius / (1 + Math.Sin(half));
            var (_, worst) = DodgeSim.ConeWorst(cone);
            Assert.InRange(worst, r * Math.Sin(half), r * Math.Sin(half) + 2 * DodgeSim.BodyRadius + 0.3);
        }

        [Fact] public void 흩뿌린_동그라미는_시드가_같으면_같고_오름차순()
        {
            var bell = P("wongak", "bell_drop_x3");
            var a = DodgeSim.CirclesEscape(bell, 200, 7);
            Assert.Equal(a, DodgeSim.CirclesEscape(bell, 200, 7));
            Assert.Equal(a.OrderBy(x => x), a);
            // 겹친 동그라미는 한 덩이가 되어 하나보다 멀 수 있지만, 흩뿌림 상자 모서리 + 반경보다 멀지는 않다
            Assert.All(a, e => Assert.InRange(e, 0, bell.Scatter * Math.Sqrt(2) + bell.Radius + DodgeSim.BodyRadius + 0.051));
        }

        [Fact] public void 걸음과_경공_거리()
        {
            var d = G.Balance.Dash;
            Assert.Equal(DodgeSim.WalkMps, DodgeSim.WalkReach(1), 6);
            Assert.Equal(DodgeSim.WalkMps * 0.5, DodgeSim.WalkReach(1, 50), 6);
            Assert.Equal(d.Distance, DodgeSim.DashReach(d, d.Duration), 5);
            Assert.Equal(d.Distance + DodgeSim.WalkReach(1), DodgeSim.DashReach(d, d.Duration + 1), 5);
            Assert.Equal(d.Distance / 2, DodgeSim.DashReach(d, d.Duration / 2), 5);
        }

        [Fact] public void 파도_쪽으로_경공하면_몸을_지나는_시간이_짧아진다()
        {
            var wave = P("ma_cheonpae", "crimson_reverse_wave");
            var d = G.Balance.Dash;
            double stand = DodgeSim.WaveOverlapSeconds(wave, d, false), dash = DodgeSim.WaveOverlapSeconds(wave, d, true);
            Assert.True(dash < stand);
            Assert.Equal((wave.Width + 2 * DodgeSim.BodyRadius) / (DodgeSim.WalkMps * wave.ProjectileSpeed / 100), stand, 6);
        }

        [Fact] public void 셈한_줄은_모두_데이터_패턴이고_비율은_0에서_1()
        {
            var rows = DodgeSim.Rows(G);
            Assert.NotEmpty(rows);
            var names = G.BossPatterns.Values.SelectMany(v => v.Patterns.Concat(v.Phase2)).Select(p => p.NameKo).ToHashSet();
            Assert.All(rows, r =>
            {
                Assert.Contains(r.Pattern, names);
                Assert.InRange(r.WalkOk, 0, 1);
                Assert.InRange(r.DashOk, r.WalkOk, 1);
                Assert.True(r.Escape90 >= r.EscapeMedian);
            });
        }

        [Fact] public void dodge_md_는_지금_데이터로_쓴_것과_같다()
        {
            string onDisk = TestData.ReadRepoFile("docs", "design", "sim", "dodge.md").Replace("\r\n", "\n");
            Assert.Equal(DodgeSim.Report(Data.Value).Replace("\r\n", "\n"), onDisk);
        }
    }
}
