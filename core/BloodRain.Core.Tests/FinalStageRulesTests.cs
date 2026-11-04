using System;
using System.Linq;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>R4-03: 종장 단계 판정(맥 짚는 손 고리, 역병 구름, 피바다 섬, 혈우 탄막). 수치는 기획서 6장 '단계 패턴 수치'.</summary>
    public class FinalStageRulesTests
    {
        readonly GameData _d = Load();
        float MoveBase => _d.Balance.Stats.MoveBase;
        BossPatternSetDef Set(string stage) => _d.FinalBoss.Stages.Single(s => s.Id == stage).Set;
        BossPatternDef P(string stage, string id) => Set(stage).Pattern(id);
        static float Dist(float x, float y) => (float)Math.Sqrt(x * x + y * y);

        [Fact] public void 맥_짚는_손은_9m에서_2초에_1점5m까지_조이고_띠에_닿으면_맞는다()
        {
            var p = P("hyeoleui", "pulse_ring");
            Assert.Equal(2f, BossPatterns.ClosingSeconds(p), 3);
            Assert.True(BossPatterns.InClosingRing(p, 0f, 9f, 0f));
            Assert.True(BossPatterns.InClosingRing(p, 0f, 9.5f, 0f));      // 띠 폭 1.2 의 절반 안
            Assert.False(BossPatterns.InClosingRing(p, 0f, 10f, 0f));      // 바깥은 안전
            Assert.False(BossPatterns.InClosingRing(p, 0f, 3f, 0f));       // 아직 안쪽은 안전
            Assert.True(BossPatterns.InClosingRing(p, 1.6f, 3f, 0f));      // 1.6초면 반경 3
            Assert.False(BossPatterns.InClosingRing(p, 1.6f, 9f, 0f));     // 뚫고 나가면 그만
            Assert.True(BossPatterns.InClosingRing(p, 2f, 0f, 0f));        // 끝까지 안에 남으면 맞는다
            Assert.False(BossPatterns.InClosingRing(p, 2f, 5f, 0f));
        }

        [Fact] public void 역병_구름은_10m_안에_셋이_피고_플레이어_쪽으로_떠다니다_12초에_흩어진다()
        {
            var p = P("hyeoleui", "plague_cloud");
            for (int seed = 0; seed < 30; seed++)
            {
                var c = BossPatterns.CloudOffsets(p, new Random(seed));
                Assert.Equal(3, c.Length);
                Assert.All(c, o => Assert.True(Dist(o.x, o.y) <= p.Scatter + 1e-3f));
            }
            var step = BossPatterns.CloudDrift(p, 10f, 0f, 1f, MoveBase);
            Assert.Equal(BossPatterns.MetersPerSecond(25f, MoveBase), step.x, 3);
            Assert.Equal(0f, step.y, 3);
            Assert.True(step.x < MoveBase);                                // 플레이어보다 한참 느리다
            var close = BossPatterns.CloudDrift(p, 0.01f, 0f, 1f, MoveBase);
            Assert.Equal(0.01f, close.x, 4);                               // 지나치지 않는다
            Assert.True(BossPatterns.CloudAlive(p, 11.9f));
            Assert.False(BossPatterns.CloudAlive(p, 12f));
        }

        [Fact] public void 피바다_섬은_7m에서_90초에_2점5m가_되고_섬_밖은_피_위다()
        {
            var p = P("hyeolhae", "rising_blood_sea");
            Assert.Equal(90f, BossPatterns.IslandSeconds(p), 2);
            Assert.Equal(7f, BossPatterns.IslandRadius(p, 0f), 3);
            Assert.Equal(2.5f, BossPatterns.IslandRadius(p, 200f), 3);
            var islands = new[] { (0f, 0f), (20f, 0f) };
            Assert.True(BossPatterns.OnIsland(p, 0f, islands, 6f, 0f));
            Assert.True(BossPatterns.OnIsland(p, 0f, islands, 15f, 0f));
            Assert.False(BossPatterns.OnIsland(p, 0f, islands, 10f, 0f));
            Assert.False(BossPatterns.OnIsland(p, 90f, islands, 6f, 0f));  // 섬이 줄었다
            Assert.True(BossPatterns.OnIsland(p, 90f, islands, 2f, 0f));
            Assert.Equal(5, p.Count);
        }

        [Fact] public void 혈우_탄막은_12m_안에_14방울씩_5파이고_3초_간격_안에_끝난다()
        {
            var p = P("jinhyeolma", "blood_rain_barrage");
            for (int seed = 0; seed < 30; seed++)
            {
                var d = BossPatterns.RainDropOffsets(p, new Random(seed));
                Assert.Equal(14, d.Length);
                Assert.All(d, o => Assert.True(Dist(o.x, o.y) <= p.Scatter + 1e-3f));
            }
            Assert.Equal(2f, BossPatterns.RainWaveSeconds(p, 4), 3);
            Assert.Equal(2.6f, BossPatterns.CastSeconds(p), 3);
            Assert.True(BossPatterns.CastSeconds(p) < Set("jinhyeolma").Phase2IntervalSeconds);
            Assert.Contains($"핏방울 {p.Count}개(반경 {Num(p.Radius)}m) × {p.Waves}파", Gdd);
        }
    }
}
