using System.Linq;
using System.Numerics;
using BloodRain.Core.World;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>수귀: 물에서만 솟고, 물 위에서 4m 안 플레이어를 초속 1.5m 끌어당긴다(여럿이어도 큰 것 하나) — 기획서 6장, 웹 WP-13c 와 같은 규칙(2026-11-27).</summary>
    public class WaterGhostTests
    {
        readonly Data.GameData _d = TestData.Load();
        Regions R => new Regions(_d);
        Data.GimmicksDef G => _d.Gimmicks;
        Data.GimmickLayoutDef L => _d.Gimmicks.Layout;
        Data.EnemyDef Ghost => _d.Enemies.First(e => e.Id == "water_ghost");

        [Fact] public void 수귀는_물에서만_솟고_다른_적은_어디서나()
        {
            var r = R; var o = r.Center("janggang");
            Assert.True(Ghost.OnlyInWater);
            Assert.True(WaterGhost.CanSpawnAt(Ghost, r, G, 1f, o, 0));
            Assert.False(WaterGhost.CanSpawnAt(Ghost, r, G, 1f, o + new Vector2(0, L.WaterSpacing / 2f), 0));
            Assert.False(WaterGhost.CanSpawnAt(Ghost, r, G, 1f, r.Center("songshan"), 0));
            var bandit = _d.Enemies.First(e => e.Id == "water_bandit");
            Assert.False(bandit.OnlyInWater);
            Assert.True(WaterGhost.CanSpawnAt(bandit, r, G, 1f, o + new Vector2(0, L.WaterSpacing / 2f), 0));
            // 우두머리의 수귀 부르기는 물 밖에서도(6장 표) — 우두머리 자체는 onlyInWater 가 아님
            Assert.False(_d.Enemies.First(e => e.Id == "water_ghost_chief").OnlyInWater);
        }

        [Fact] public void 물로_붙이기는_가장_가까운_띠_가운데로_가고_멀면_못_붙인다()
        {
            var r = R; var o = r.Center("janggang");
            foreach (float s in new[] { 1f, 2.4f })
            {
                // 띠와 띠 사이 조금 아래(j=0 쪽) → j=0 가운데 줄
                float x = o.X + 5f * s;
                var p = new Vector2(x, GimmickLayout.BandCenterY(o, 0, L.WaterSpacing * s, L.WaterWave * s, L.WaterWaveLen * s, x) + L.WaterSpacing * s * 0.4f);
                Assert.False(GimmickLayout.InWater(r, G, s, p, 0));
                var q = WaterGhost.SnapToWater(r, G, s, p, L.WaterSpacing * s, 0);
                Assert.NotNull(q);
                Assert.Equal(p.X, q!.Value.X, 3);
                Assert.True(GimmickLayout.InWater(r, G, s, q.Value, 0));
                Assert.Equal(GimmickLayout.BandCenterY(o, 0, L.WaterSpacing * s, L.WaterWave * s, L.WaterWaveLen * s, p.X), q.Value.Y, 2);
                // 옮길 거리 상한보다 멀면 null
                Assert.Null(WaterGhost.SnapToWater(r, G, s, p, 0.5f, 0));
            }
            // 이미 물이면 그대로, 물 없는 땅은 null
            Assert.Equal(o, WaterGhost.SnapToWater(r, G, 1f, o, 1f, 0));
            Assert.Null(WaterGhost.SnapToWater(r, G, 1f, r.Center("songshan"), 1000f, 0));
        }

        [Fact] public void 끌기는_물_위_수귀가_범위_안일_때만_수귀_쪽으로()
        {
            var pull = Ghost.Pull!;
            Assert.Equal(4f, pull.Range);   // 기획서 6장 '4m 안이면 … 초속 1.5m'
            Assert.Equal(1.5f, pull.Speed);
            var player = new Vector2(10, 10);
            var ghost = player + new Vector2(3, 0);
            var off = WaterGhost.PullOffset(pull, ghost, player, true, false, 0.1f);
            Assert.Equal(0.15f, off.X, 4);
            Assert.Equal(0f, off.Y, 4);
            Assert.Equal(Vector2.Zero, WaterGhost.PullOffset(pull, ghost, player, false, false, 0.1f));   // 뭍의 수귀
            Assert.Equal(Vector2.Zero, WaterGhost.PullOffset(pull, ghost, player, true, true, 0.1f));    // 경공 중
            Assert.Equal(Vector2.Zero, WaterGhost.PullOffset(pull, player + new Vector2(4.5f, 0), player, true, false, 0.1f));
            Assert.Equal(Vector2.Zero, WaterGhost.PullOffset(null, ghost, player, true, false, 0.1f));
            // 긴 걸음이어도 수귀 몸을 지나쳐 끌지 않는다
            Assert.Equal(3f, WaterGhost.PullOffset(pull, ghost, player, true, false, 10f).Length(), 3);
        }

        [Fact] public void 여럿이면_큰_것_하나만()
        {
            var pull = Ghost.Pull!; var player = Vector2.Zero;
            var step = new PullStep();
            step.Offer(WaterGhost.PullOffset(pull, new Vector2(3, 0), player, true, false, 0.1f));
            step.Offer(WaterGhost.PullOffset(pull, new Vector2(0, 0.05f), player, true, false, 0.1f));   // 가까워 덜 끄는 것
            step.Offer(WaterGhost.PullOffset(pull, new Vector2(-2, 0), player, true, false, 0.1f));
            Assert.Equal(0.15f, step.Offset.Length(), 4);
            step.Reset();
            Assert.Equal(Vector2.Zero, step.Offset);
        }
    }
}
