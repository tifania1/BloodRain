using System;
using System.Linq;
using System.Numerics;
using BloodRain.Core.Data;
using BloodRain.Core.World;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>지역 기믹 둘째 묶음(T-16, 기획서 7장 '지형 기믹 수치').</summary>
    public class Gimmicks2Tests
    {
        readonly GameData _d = Load();
        GimmicksDef G => _d.Gimmicks;
        string[] Row(string nameKo) => GddTable(GddSection("지형 기믹 수치")).Single(r => r[0] == nameKo);
        string[] RegionRow(string nameKo) => GddTable(GddSection("지역과 혈운 농도")).Single(r => r[0] == nameKo);

        [Fact] public void 수치_표가_데이터와_같다()
        {
            Assert.Equal($"무공에 {G.WineJars.HitsToBreak}번 맞으면 깨짐. {Num(G.WineJars.DropChance * 100)}%로 음식(최대 기혈 {Num(G.WineJars.HealMaxHealthRatio * 100)}% 회복)", Row("술독")[1]);
            Assert.Equal($"무공에 {G.SwordRacks.HitsToBreak}번 맞으면 무너짐. 은자 {G.SwordRacks.SilverMin}~{G.SwordRacks.SilverMax}", Row("검각 시렁")[1]);
            var r = G.FallingRocks;
            Assert.Equal($"{Num(r.PeriodSeconds)}초마다 플레이어 둘레 ±{Num(r.Scatter)}m 안 세 곳에 그림자, {Num(r.TelegraphSeconds)}초 뒤 반경 {Num(r.Radius)}m 바위. 플레이어는 최대 기혈 {Num(r.PlayerDamageMaxHealthRatio * 100)}%, 잡몹은 쓰러지고 정예·보스는 {Num(r.EliteDamageMaxHealthRatio * 100)}%", Row("낙석")[1]);
            Assert.Equal(3, r.Count);
            var p = G.PlumHeaps;
            Assert.Equal($"{Num(p.TouchRadius)}m 안에 닿으면 최대 기혈 {Num(p.HealMaxHealthRatio * 100)}% 회복, {Num(p.RegrowSeconds)}초 뒤 다시 핌", Row("매화 더미")[1]);
            var b = G.Blizzard;
            Assert.Equal($"한 생 {Num(b.FirstSeconds)}초에 처음, 그 뒤 {Num(b.PeriodSeconds)}초마다 {Num(b.DurationSeconds)}초. 보이는 범위 {Num(b.ViewScale)}배, 이동속도 {Num(b.PlayerMoveSpeedMultiplier)}배", Row("눈보라")[1]);
            Assert.Equal(b.PlayerMoveSpeedMultiplier, b.EnemyMoveSpeedMultiplier);
            Assert.Equal($"속도가 바뀌는 데 {Num(G.IceFloor.InertiaSeconds)}초(관성)", Row("빙판")[1]);
            var c = G.Campfire;
            Assert.Equal($"반경 {Num(c.Radius)}m 안에서 초당 최대 기혈 {Num(c.HealPerSecondMaxHealthRatio * 100)}% 회복. 빙강시 계열은 이동속도 {Num(c.SlowedMoveSpeedMultiplier)}배", Row("모닥불")[1]);
            var a = G.BloodAltars;
            Assert.Equal($"네 개. 한 생 {Num(a.FirstLightSeconds)}초에 하나, 그 뒤 {Num(a.LightEverySeconds)}초마다 하나씩 켜짐. 켜진 제단 반경 {Num(a.BuffRadius)}m 안 적은 피해 {Num(a.EnemyDamageMultiplier)}배·이동속도 {Num(a.EnemyMoveSpeedMultiplier)}배. 기혈 {a.Health:N0}을 깎아 부수면 보갑", Row("혈제단")[1]);
            Assert.Equal(4, a.Count);
            var m = G.RedMoon;
            Assert.Equal($"한 생 {Num(m.PeriodSeconds / 60)}분마다 {Num(m.DurationSeconds)}초 동안 적 이동속도 {Num(m.EnemyMoveSpeedMultiplier)}배", Row("붉은 달")[1]);
        }

        [Fact] public void 지역표의_기믹_칸과_어긋나지_않는다()
        {
            Assert.Contains("혈제단 4개", RegionRow("십만대산")[5]);
            Assert.Equal(4, G.BloodAltars.Count);
            var m = G.RedMoon;
            Assert.Contains($"붉은 달({Num(m.PeriodSeconds / 60)}분마다 {Num(m.DurationSeconds)}초 적 속도 +{Num((m.EnemyMoveSpeedMultiplier - 1) * 100)}%)", RegionRow("십만대산")[5]);
            Assert.Contains("술독: 부수면 음식", RegionRow("개방 총타")[5]);
            Assert.Equal("food", G.WineJars.Drop);
            Assert.Contains("검각 시렁: 부수면 은자", RegionRow("남궁세가")[5]);
            Assert.Equal("silver", G.SwordRacks.Drop);
        }

        [Fact] public void 붉은_달은_180초부터_3분마다_30초()
        {
            var m = G.RedMoon;
            Assert.False(WeatherGimmick.ActiveAt(m, 179.9f));
            Assert.True(WeatherGimmick.ActiveAt(m, 180f));
            Assert.True(WeatherGimmick.ActiveAt(m, 209.9f));
            Assert.False(WeatherGimmick.ActiveAt(m, 210f));
            Assert.True(WeatherGimmick.ActiveAt(m, 360f));
            Assert.Equal(1.2f, WeatherGimmick.EnemyMoveSpeed(m, 365f), 3);
            Assert.Equal(1f, WeatherGimmick.EnemyMoveSpeed(m, 300f));
            Assert.Equal(1f, WeatherGimmick.PlayerMoveSpeed(m, 365f)); // 플레이어는 그대로
            Assert.Equal(540f, WeatherGimmick.NextStartAt(m, 400f));
        }

        [Fact] public void 눈보라는_시야와_걸음을_줄인다()
        {
            var b = G.Blizzard;
            Assert.Equal(1f, WeatherGimmick.ViewScale(b, 59f));
            Assert.Equal(0.7f, WeatherGimmick.ViewScale(b, 61f), 3);
            Assert.Equal(0.9f, WeatherGimmick.PlayerMoveSpeed(b, 61f), 3);
            Assert.Equal(1f, WeatherGimmick.ViewScale(b, 81f));
            Assert.Equal(0.7f, WeatherGimmick.ViewScale(b, 185f), 3);
        }

        [Fact] public void 술독은_한_번에_시렁은_세_번에_깨지고_떨군다()
        {
            var jar = new BreakableGimmick(G.WineJars);
            Assert.True(jar.Hit());
            Assert.False(jar.Hit());
            var rack = new BreakableGimmick(G.SwordRacks);
            Assert.False(rack.Hit());
            Assert.False(rack.Hit());
            Assert.True(rack.Hit());
            var rng = new Random(1);
            for (int i = 0; i < 200; i++)
            {
                var (heal, silver) = rack.RollDrop(rng);
                Assert.Equal(0f, heal);
                Assert.InRange(silver, 5, 10);
            }
            int food = Enumerable.Range(0, 2000).Count(_ => jar.RollDrop(rng).heal > 0);
            Assert.InRange(food / 2000.0, 0.55, 0.65);
        }

        [Fact] public void 낙석은_8초마다_세_곳_잡몹은_쓰러지고_플레이어는_10퍼센트()
        {
            var r = G.FallingRocks;
            Assert.Equal(0, FallingRocksGimmick.FallsBetween(r, 0, 7.9f));
            Assert.Equal(1, FallingRocksGimmick.FallsBetween(r, 7.9f, 8f));
            Assert.Equal(3, FallingRocksGimmick.FallsBetween(r, 0, 24f));
            var places = FallingRocksGimmick.Places(r, new Vector2(100, 100), new Random(3));
            Assert.Equal(3, places.Count);
            Assert.All(places, p => Assert.True(Math.Abs(p.X - 100) <= 8 && Math.Abs(p.Y - 100) <= 8));
            Assert.Equal(10f, FallingRocksGimmick.Damage(r, GimmickTargetKind.Player, 100), 3);
            Assert.True(float.IsPositiveInfinity(FallingRocksGimmick.Damage(r, GimmickTargetKind.Normal, 50)));
            Assert.Equal(500f, FallingRocksGimmick.Damage(r, GimmickTargetKind.Boss, 10000), 3);
            Assert.True(FallingRocksGimmick.Hits(r, Vector2.Zero, new Vector2(1.8f, 0)));
            Assert.False(FallingRocksGimmick.Hits(r, Vector2.Zero, new Vector2(1.9f, 0)));
        }

        [Fact] public void 매화_더미는_한_번_회복하고_90초_뒤_다시_핀다()
        {
            var h = new PlumHeapGimmick(G.PlumHeaps);
            Assert.Equal(0f, h.TryUse(2f, 100));
            Assert.Equal(15f, h.TryUse(1f, 100), 3);
            Assert.Equal(0f, h.TryUse(1f, 100));
            h.Tick(89f);
            Assert.False(h.Blooming);
            h.Tick(1f);
            Assert.True(h.Blooming);
        }

        [Fact] public void 빙판에선_속도가_천천히_바뀌고_나는_적은_그대로()
        {
            var ice = G.IceFloor;
            var v = IceFloorGimmick.Step(ice, Vector2.Zero, new Vector2(5, 0), 0.1f, onIce: true);
            Assert.True(v.X > 0 && v.X < 5);
            Assert.Equal(new Vector2(5, 0), IceFloorGimmick.Step(ice, Vector2.Zero, new Vector2(5, 0), 0.1f, onIce: false));
            Assert.Equal(new Vector2(5, 0), IceFloorGimmick.Step(ice, Vector2.Zero, new Vector2(5, 0), 0.1f, onIce: true, flying: true));
            var cur = Vector2.Zero;
            for (int i = 0; i < 60; i++) cur = IceFloorGimmick.Step(ice, cur, new Vector2(5, 0), 1f / 60f, true);
            Assert.True(cur.X > 4f); // 1초면 거의 다 따라잡는다
        }

        [Fact] public void 모닥불은_곁에서_회복하고_빙강시만_느리게()
        {
            var c = G.Campfire;
            Assert.Equal(1f, CampfireGimmick.Heal(c, 3f, 100, 1f), 3);
            Assert.Equal(0f, CampfireGimmick.Heal(c, 5f, 100, 1f));
            Assert.Equal(0.5f, CampfireGimmick.EnemyMoveSpeed(c, "ice", 3f));
            Assert.Equal(1f, CampfireGimmick.EnemyMoveSpeed(c, "jiangshi", 3f));
            Assert.Equal(1f, CampfireGimmick.EnemyMoveSpeed(c, "ice", 5f));
            Assert.Equal("campfire", _d.BossPatternSet("seol_muhyang").Phase2.Single().SlowedNear);
        }

        [Fact] public void 혈제단은_120초마다_하나씩_넷까지_켜진다()
        {
            var a = G.BloodAltars;
            Assert.Equal(0, BloodAltarGimmick.LitCount(a, 119f));
            Assert.Equal(1, BloodAltarGimmick.LitCount(a, 120f));
            Assert.Equal(2, BloodAltarGimmick.LitCount(a, 240f));
            Assert.Equal(4, BloodAltarGimmick.LitCount(a, 480f));
            Assert.Equal(4, BloodAltarGimmick.LitCount(a, 3000f));
            Assert.Equal(1.25f, BloodAltarGimmick.EnemyDamage(a, true));
            Assert.Equal(1f, BloodAltarGimmick.EnemyDamage(a, false));
            Assert.True(BloodAltarGimmick.Near(a, 8f));
            Assert.True(a.ChestOnBreak);
        }
    }
}
