using System;
using System.IO;
using BloodRain.Sim;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>지역 기믹의 기혈 수지(GimmickSim, docs/design/sim/gimmicks.md). 셈이 gimmicks.json 숫자와 맞는지, 보고서가 지금 데이터와 같은지 본다.</summary>
    public class GimmickSimTests
    {
        static readonly Lazy<SimData> Data = new Lazy<SimData>(() => SimData.Load(TestData.DataDir));
        static Core.Data.GameData G => Data.Value.Game;

        [Fact] public void 술독_기대값은_떨굴_확률_곱하기_회복()
        {
            var j = G.Gimmicks.WineJars;
            Assert.Equal(j.DropChance * j.HealMaxHealthRatio, GimmickSim.WineJarExpected(G), 2);
        }

        [Fact] public void 매화_더미는_다시_필_때마다_먹는다()
        {
            // 10분 동안 0초, 90초, 180초 … 540초에 7번 → 1분 평균 0.7번
            var p = G.Gimmicks.PlumHeaps;
            int uses = (int)Math.Floor(600 / p.RegrowSeconds) + 1;
            Assert.Equal(uses * p.HealMaxHealthRatio / 10.0, GimmickSim.PlumPerMinute(G), 3);
        }

        [Fact] public void 모닥불_1분은_초당_비율의_60배()
        {
            Assert.Equal(G.Gimmicks.Campfire.HealPerSecondMaxHealthRatio * 60, GimmickSim.CampfirePerMinute(G), 4);
        }

        [Fact] public void 독안개_샘_위는_퍼짐_머묾_걷힘_내내_안이다()
        {
            var f = G.Gimmicks.PoisonFog;
            var (loss, sec) = GimmickSim.FogEpisode(G, 0f);
            Assert.Equal(f.SpreadSeconds + f.HoldSeconds + f.FadeSeconds, sec, 0);
            Assert.Equal(sec * f.HealthLossPerSecond, loss, 2);
            // 샘에서 멀수록 덜 잃고, 반경 밖이면 0
            Assert.True(GimmickSim.FogEpisode(G, f.Radius * 0.5f).Loss < loss);
            Assert.Equal(0, GimmickSim.FogEpisode(G, f.Radius + 0.1f).Loss, 6);
        }

        [Fact] public void 낙석_굴림은_넓이_비율_식과_맞는다()
        {
            var (sim, formula) = GimmickSim.RockHitsPerMinute(G);
            Assert.InRange(sim, formula * 0.9, formula * 1.1);
        }

        [Fact] public void 날씨_몫은_길이_나누기_주기에_가깝다()
        {
            var r = G.Gimmicks.RedMoon;
            double share = GimmickSim.WeatherShare(r);
            // 첫 켜짐 전의 시간만큼 조금 모자란다
            Assert.InRange(share, 0.5 * r.DurationSeconds / r.PeriodSeconds, r.DurationSeconds / r.PeriodSeconds + 1e-6);
        }

        [Fact] public void 보고서는_지금_데이터와_같다()
        {
            var path = Path.Combine(TestData.RepoRoot, "docs", "design", "sim", "gimmicks.md");
            var file = File.ReadAllText(path).Replace("\r\n", "\n");
            var now = GimmickSim.Report(Data.Value).Replace("\r\n", "\n");
            Assert.Equal(now, file);
        }
    }
}
