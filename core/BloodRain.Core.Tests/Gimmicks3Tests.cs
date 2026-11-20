using System;
using System.Linq;
using System.Numerics;
using BloodRain.Core.Data;
using BloodRain.Core.World;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>R-10 남은 것: 지역 기믹 다섯의 판정(값은 gimmicks.json, 근거 gimmick_start_values.md 1절).</summary>
    public class Gimmicks3Tests
    {
        readonly GameData _d = TestData.Load();
        GimmicksDef G => _d.Gimmicks;
        float MoveBase => _d.Balance.Stats.MoveBase;

        [Fact] public void 대나무_덤불은_걷는_것만_막고_한_번에_베이고_90초_뒤_자란다()
        {
            var b = G.BambooThicket;
            var stalks = BambooThicketGimmick.Layout(b, new Random(1));
            Assert.NotEmpty(stalks);
            Assert.All(stalks, s => Assert.Equal(b.StalkRadius, s.Radius, 3));
            Assert.All(stalks, s => Assert.True(new Vector2(s.X, s.Y).Length() <= b.PatchRadius + 0.01f));
            Assert.True(BambooThicketGimmick.Blocks(b, flying: false, projectile: false));
            Assert.False(BambooThicketGimmick.Blocks(b, flying: true, projectile: false));
            Assert.False(BambooThicketGimmick.Blocks(b, flying: false, projectile: true));
            Assert.True(BambooThicketGimmick.Breaks(b, 1));
            Assert.Equal(130f, BambooThicketGimmick.RegrowAt(b, 40f));
            Assert.Equal(20f, BambooThicketGimmick.FoodHeal(b, 100f), 3);
            int drops = Enumerable.Range(0, 5000).Count(i => BambooThicketGimmick.DropsFood(b, new Random(i)));
            Assert.InRange(drops, 50, 160); // 2%
        }

        [Fact] public void 배는_24m_간격으로_초속_1_5m_하류로_흐르고_판_위를_가린다()
        {
            var b = G.DriftingBoats;
            var start = new Vector2(0, 0);
            var t0 = DriftingBoatsGimmick.Boats(b, start, 96f, 0f);
            Assert.Equal(4, t0.Count);
            Assert.Equal(new Vector2(0, 0), t0[0]);
            Assert.Equal(new Vector2(24, 0), t0[1]);
            var t10 = DriftingBoatsGimmick.Boats(b, start, 96f, 10f);
            Assert.Equal(15f, t10[0].X, 3);   // 1.5 m/s × 10초
            var tWrap = DriftingBoatsGimmick.Boats(b, start, 96f, 64f); // 96m 흐르면 한 바퀴
            Assert.Equal(0f, tWrap.Min(p => p.X), 2);
            Assert.True(DriftingBoatsGimmick.OnBoat(b, new Vector2(10, 0), new Vector2(11.9f, 0.7f)));
            Assert.False(DriftingBoatsGimmick.OnBoat(b, new Vector2(10, 0), new Vector2(12.1f, 0)));
            Assert.False(DriftingBoatsGimmick.OnBoat(b, new Vector2(10, 0), new Vector2(10, 0.9f)));
        }

        [Fact] public void 역류_파도는_2분마다_예고하고_배_위나_경공이면_안_맞는다()
        {
            var w = G.ReverseWave;
            Assert.Equal(120f, ReverseWaveGimmick.StartAt(w, 0));
            Assert.Equal(360f, ReverseWaveGimmick.StartAt(w, 2));
            Assert.Equal(0, ReverseWaveGimmick.StartsBetween(w, 0f, 119f));
            Assert.Equal(1, ReverseWaveGimmick.StartsBetween(w, 119f, 120f));
            Assert.Equal(3, ReverseWaveGimmick.StartsBetween(w, 0f, 360f));
            Assert.True(ReverseWaveGimmick.Telegraphing(w, 121f));
            Assert.False(ReverseWaveGimmick.Telegraphing(w, 122f));
            Assert.Equal(300f / 100f * MoveBase, ReverseWaveGimmick.SpeedMetersPerSecond(w, MoveBase), 3);
            Assert.True(ReverseWaveGimmick.Hits(w, new Vector2(5, 0), new Vector2(6.4f, 30)));   // 폭 3m, 상류(서)쪽으로 감
            Assert.False(ReverseWaveGimmick.Hits(w, new Vector2(5, 0), new Vector2(6.6f, 0)));
            Assert.True(ReverseWaveGimmick.Immune(onBoat: true, dashInvulnerable: false));
            Assert.Equal(10f, ReverseWaveGimmick.Damage(w, GimmickTargetKind.Player, 100f), 3);
            Assert.True(float.IsPositiveInfinity(ReverseWaveGimmick.Damage(w, GimmickTargetKind.Normal, 50f)));
            Assert.Equal(25f, ReverseWaveGimmick.Damage(w, GimmickTargetKind.Boss, 500f), 3);
        }

        [Fact] public void 운해는_12초마다_세_곳에_피어_흐르고_보스는_못_속인다()
        {
            var c = G.CloudSea;
            Assert.Equal(1, CloudSeaGimmick.BloomsBetween(c, 11f, 12f));
            Assert.Equal(5, CloudSeaGimmick.BloomsBetween(c, 0f, 60f));
            var at = CloudSeaGimmick.Places(c, new Vector2(100, 100), new Random(3));
            Assert.Equal(3, at.Count);
            Assert.All(at, p => Assert.True(Math.Abs(p.X - 100) <= 10 && Math.Abs(p.Y - 100) <= 10));
            var d = CloudSeaGimmick.Drift(c, Vector2.Zero, 10f, MoveBase);
            Assert.Equal(25f / 100f * MoveBase * 10f, d.X, 3);   // 동쪽으로 초속 1.15m
            Assert.False(CloudSeaGimmick.Covering(c, 0.5f));
            Assert.True(CloudSeaGimmick.Covering(c, 5f));
            Assert.False(CloudSeaGimmick.Covering(c, 13.5f));
            Assert.True(CloudSeaGimmick.Hides(c, Vector2.Zero, new Vector2(3, 0), isBoss: false));
            Assert.False(CloudSeaGimmick.Hides(c, Vector2.Zero, new Vector2(3, 0), isBoss: true));
            Assert.False(CloudSeaGimmick.Hides(c, Vector2.Zero, new Vector2(4, 0), isBoss: false));
        }

        [Fact] public void 잔도는_가로세로_두_길만_걸을_수_있고_경공으로도_못_넘는다()
        {
            var p = G.CliffPath;
            var o = Vector2.Zero;
            Assert.False(CliffPathGimmick.OnCliff(p, o, new Vector2(8, 0.7f)));   // 가로 잔도 위
            Assert.False(CliffPathGimmick.OnCliff(p, o, new Vector2(0.7f, -9)));  // 세로 잔도 위
            Assert.True(CliffPathGimmick.OnCliff(p, o, new Vector2(5, 5)));
            Assert.False(CliffPathGimmick.OnCliff(p, o, new Vector2(13, 0.0f + 5)));   // 무더기 밖
            Assert.True(CliffPathGimmick.Blocks(p, flying: false, projectile: false, dashing: true));
            Assert.False(CliffPathGimmick.Blocks(p, flying: true, projectile: false, dashing: false));
            Assert.False(CliffPathGimmick.Blocks(p, flying: false, projectile: true, dashing: false));
        }
    }
}
