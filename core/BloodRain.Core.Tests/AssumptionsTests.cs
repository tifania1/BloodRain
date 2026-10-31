using BloodRain.Core.Data;
using System.Linq;
using BloodRain.Sim;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>시뮬레이터 가정(Assumptions.cs) 가운데 데이터에 같은 값이 있는 것은 데이터와 같아야 한다.
    /// 한쪽만 바꾸면 level-curve 보고서가 데이터와 다른 게임을 재게 된다.</summary>
    public class AssumptionsTests
    {
        static GameData G => TestData.Load();

        [Fact] public void 처치_진기_표와_적_구성의_적은_모두_enemies에_있다()
        {
            var ids = G.Enemies.Select(e => e.Id).ToHashSet();
            Assert.All(Assumptions.KillXp.Keys, id => Assert.Contains(id, ids));
            Assert.All(Assumptions.EnemyMix.SelectMany(m => m.Mix), x => Assert.Contains(x.Id, ids));
        }

        [Fact] public void 적_구성은_시각이_오르고_가중치_합이_1()
        {
            Assert.Equal(0, Assumptions.EnemyMix[0].FromMinute);
            for (int i = 1; i < Assumptions.EnemyMix.Length; i++)
                Assert.True(Assumptions.EnemyMix[i].FromMinute > Assumptions.EnemyMix[i - 1].FromMinute);
            Assert.All(Assumptions.EnemyMix, m =>
            {
                Assert.All(m.Mix, x => Assert.True(x.Weight > 0));
                Assert.Equal(1.0, m.Mix.Sum(x => x.Weight), 6);
            });
        }

        [Fact] public void 사건은_시각_차례이고_20분_안이며_정예_번호가_표_안()
        {
            var ev = Assumptions.Events;
            for (int i = 1; i < ev.Length; i++) Assert.True(ev[i].Minute >= ev[i - 1].Minute);
            Assert.All(ev, e => Assert.InRange(e.Minute, 0, Assumptions.RunMinutes));
            Assert.All(ev.Where(e => e.Kind == Assumptions.EventKind.Elite), e => Assert.InRange(e.Count, 0, Assumptions.EliteHealth.Length - 1));
            var toad = ev.Single(e => e.Kind == Assumptions.EventKind.Toad);
            Assert.Equal(Assumptions.ToadFrom, toad.Minute);
            Assert.True(Assumptions.ToadTo > Assumptions.ToadFrom);
        }

        [Fact] public void 정예_기혈과_이름은_enemies의_1장_정예와_같다()
        {
            var elites = G.Elites["chapter1"].Select(G.Enemy).ToList();
            Assert.Equal(Assumptions.EliteHealth.Length, elites.Count);
            Assert.Equal(Assumptions.EliteNameKo, elites.Select(e => e.NameKo));
            Assert.Equal(Assumptions.EliteHealth, elites.Select(e => (double)e.Health));
        }

        [Fact] public void 각성_파동_값은_balance_awakening과_같다()
        {
            var a = G.Balance.Awakening;
            Assert.Equal(Assumptions.WaveEliteRatio, a.EliteMaxHealthRatio, 4);
            Assert.Equal(Assumptions.WaveSpawnPause, a.SpawnPauseSeconds, 4);
            Assert.Equal(Assumptions.GlowSeconds, a.AfterglowSeconds, 4);
            Assert.Equal(Assumptions.GlowMultiplier, a.AfterglowDamageMultiplier, 4);
        }

        [Fact] public void 무리에_닿는_시간은_생성_고리_거리와_1장_적_걸음에서_나온다()
        {
            // 주석의 셈: 대각선 38m × 고리 가운데(0.6~0.75의 평균) − 무공 사거리 3m, 걸음 초속 약 3.3m → 약 7초.
            var s = G.Balance.SpawnTarget;
            double ring = 38 * (s.RingMinDiagonal + s.RingMaxDiagonal) / 2 - 3;
            Assert.InRange(ring / 3.3, Assumptions.ArrivalSeconds * 0.85, Assumptions.ArrivalSeconds * 1.15);
        }
    }
}
