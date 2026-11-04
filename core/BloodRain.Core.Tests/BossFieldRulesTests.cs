using System;
using System.Linq;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>R-11: 당소혜·마천패·혈허진인 판정(흑백 구역, 분열 되살림, 칠성검진 자리, 태극권 끌어당김, 갈고리 사슬, 반쪽 독안개).</summary>
    public class BossFieldRulesTests
    {
        readonly GameData _d = Load();
        float MoveBase => _d.Balance.Stats.MoveBase;
        BossPatternDef P(string boss, string id)
        {
            var w = _d.BossPatternSet(boss);
            return w.Patterns.Concat(w.Phase2).Single(p => p.Id == id);
        }

        // ---- 흑백 ----

        [Fact] public void 흑백_예고_때_플레이어_쪽이_검은_구역이다()
        {
            var f = P("hyeolheo", "yin_yang_split_field");
            Assert.Equal(YinYang.Yin, BossPatterns.SplitFieldColor(f, 5f, 0f, 0f, 0f));
            Assert.Equal(YinYang.Yang, BossPatterns.SplitFieldColor(f, -5f, 0f, 0f, 0f));
            Assert.Equal(YinYang.Yin, BossPatterns.SplitFieldColor(f, 0f, 7f, 0f, 0f));   // 경계 위는 검은 쪽
            Assert.Equal(YinYang.Yin, BossPatterns.SplitFieldColor(f, 0f, 5f, 90f, 0f));
            Assert.Equal(f.TurnDegreesPerSecond, 0f);   // 1막 흑백은 돌지 않는다
            Assert.Equal(YinYang.Yin, BossPatterns.SplitFieldColor(f, 5f, 0f, 0f, 9f));
        }

        [Fact] public void 내_색이_아니면_받는_피해_2배_태극이면_양쪽_다_안전()
        {
            var f = P("hyeolheo", "yin_yang_split_field");
            Assert.Equal(2f, BossPatterns.SplitFieldDamageTaken(f, YinYang.Yin, YinYang.Yang));
            Assert.Equal(2f, BossPatterns.SplitFieldDamageTaken(f, YinYang.Yang, YinYang.Yin));
            Assert.Equal(1f, BossPatterns.SplitFieldDamageTaken(f, YinYang.Yin, YinYang.Yin));
            Assert.Equal(1f, BossPatterns.SplitFieldDamageTaken(f, YinYang.Yang, YinYang.Yang));
            Assert.Equal(1f, BossPatterns.SplitFieldDamageTaken(f, YinYang.Yin, YinYang.Neutral));
            Assert.Equal(1f, BossPatterns.SplitFieldDamageTaken(f, YinYang.Yang, YinYang.Neutral));
            // 양으로 기운 플레이어가 예고 때 선 쪽(검은 구역)에 그대로 있으면 2배, 건너가면 1배
            Assert.Equal(2f, BossPatterns.SplitFieldDamageTaken(f, 4f, 1f, 0f, 0f, YinYang.Yang));
            Assert.Equal(1f, BossPatterns.SplitFieldDamageTaken(f, -4f, 1f, 0f, 0f, YinYang.Yang));
        }

        [Fact] public void 이막_흑백_경계는_초당_20도로_돈다()
        {
            var f = P("hyeolheo", "rotating_yin_yang_border");
            Assert.Equal(20f, f.TurnDegreesPerSecond);
            Assert.Equal(90f, BossPatterns.SplitFieldBlackDegrees(f, 0f, 4.5f), 3);
            // 처음엔 검은 쪽이던 (5, 0) 이 9초(180도) 뒤엔 흰 쪽
            Assert.Equal(YinYang.Yin, BossPatterns.SplitFieldColor(f, 5f, 0f, 0f, 0f));
            Assert.Equal(YinYang.Yang, BossPatterns.SplitFieldColor(f, 5f, 0f, 0f, 9f));
            Assert.Equal(YinYang.Yin, BossPatterns.SplitFieldColor(f, 5f, 0f, 0f, 18f));
            Assert.Equal(YinYang.Yin, BossPatterns.SplitFieldColor(f, 0f, 5f, 0f, 4.5f));
        }

        [Fact] public void 흑백_내_색은_음양_게이지가_기운_쪽이다()
        {
            var g = new BloodRain.Core.Progression.YinYangGauge(_d.Balance.Yinyang, start: 0);
            Assert.Equal(YinYang.Neutral, g.Leaning);
            g.Shift(_d.Balance.Yinyang.HarmonyRange + 1);
            Assert.Equal(YinYang.Yang, g.Leaning);
            var f = P("hyeolheo", "yin_yang_split_field");
            Assert.Equal(1f, BossPatterns.SplitFieldDamageTaken(f, YinYang.Yang, g.Leaning));
        }

        // ---- 분열 ----

        BossSplit NewSplit(float health = 300f) => new BossSplit(P("dang_sohye", "split_into_three"), health);

        [Fact] public void 분열은_남은_기혈을_셋이_나눈다()
        {
            var s = NewSplit(300f);
            Assert.Equal(3, s.Count);
            for (int i = 0; i < 3; i++) Assert.Equal(300f * 0.3333f, s.Health(i), 3);
            Assert.Null(s.RejoinLeft);
            Assert.Empty(s.Tick(10f));
        }

        [Fact] public void 첫_몸이_쓰러지고_4초_안에_다_쓰러지면_끝()
        {
            var s = NewSplit();
            Assert.True(s.Damage(0, 1000f));
            Assert.Equal(4f, s.RejoinLeft);
            Assert.Empty(s.Tick(1.5f));
            Assert.True(s.Damage(1, 1000f));
            Assert.Equal(2.5f, s.RejoinLeft!.Value, 4);   // 둘째가 쓰러져도 시계는 이어 간다
            Assert.Empty(s.Tick(1.5f));
            Assert.True(s.Damage(2, 1000f));
            Assert.True(s.Defeated);
            Assert.Null(s.RejoinLeft);
            Assert.Empty(s.Tick(5f));
            Assert.False(s.Damage(2, 5f));
        }

        [Fact] public void 못_다_쓰러뜨리면_쓰러진_몸이_가장_많은_기혈로_일어난다()
        {
            var s = NewSplit(300f);
            s.Damage(1, 30f);                  // 1번 70쯤
            s.Damage(2, 10f);                  // 2번 90쯤 — 남은 몸 가운데 가장 많다
            Assert.True(s.Damage(0, 1000f));
            Assert.Empty(s.Tick(3.9f));
            var revived = s.Tick(0.2f);
            Assert.Equal(new[] { 0 }, revived);
            Assert.Equal(s.Health(2), s.Health(0));
            Assert.Equal(3, s.AliveCount);
            Assert.Null(s.RejoinLeft);
            // 다음에 처음 쓰러지는 몸부터 다시 잰다
            Assert.True(s.Damage(1, 1000f));
            Assert.Equal(4f, s.RejoinLeft);
        }

        [Fact] public void 분열_한_프레임이_길어도_되살림은_한_번()
        {
            var s = NewSplit();
            s.Damage(0, 1000f); s.Damage(1, 1000f);
            Assert.Equal(new[] { 0, 1 }, s.Tick(100f));
            Assert.Empty(s.Tick(100f));
            Assert.False(s.Damage(0, 0f));
            Assert.False(s.Damage(0, -5f));
        }

        // ---- 칠성검진 ----

        [Fact] public void 칠성검진_일곱째가_플레이어_자리이고_손잡이가_보스_쪽()
        {
            var p = P("hyeolheo", "seven_star_sword_drop");
            var o = BossPatterns.BigDipperOffsets(p, 0f);
            Assert.Equal(7, o.Length);
            Assert.Equal(0f, o[6].x, 4); Assert.Equal(0f, o[6].y, 4);
            // 손잡이 끝(첫째)은 handleDegrees 쪽으로 전체 길이만큼
            Assert.Equal(p.Length, o[0].x, 1);
            Assert.True(Math.Abs(o[0].y) < 1.5f);
            // 다른 방향으로 돌려도 거리는 같다
            var r = BossPatterns.BigDipperOffsets(p, 137f);
            for (int i = 0; i < 7; i++)
                Assert.Equal(Math.Sqrt(o[i].x * o[i].x + o[i].y * o[i].y), Math.Sqrt(r[i].x * r[i].x + r[i].y * r[i].y), 3);
            // 일곱 자리는 서로 다르다(겹친 별 없음)
            for (int i = 0; i < 7; i++) for (int j = i + 1; j < 7; j++)
                Assert.True(Math.Sqrt(Math.Pow(o[i].x - o[j].x, 2) + Math.Pow(o[i].y - o[j].y, 2)) > p.Radius, $"{i}·{j} 가 너무 붙음");
        }

        [Fact] public void 칠성검진_예고_때_자리에_그대로_서면_맞고_몸_세_개쯤_비키면_산다()
        {
            var p = P("hyeolheo", "seven_star_sword_drop");
            float body = _d.Balance.Stats.PlayerBodyRadius;
            var o = BossPatterns.BigDipperOffsets(p, 180f);
            bool Hit(float x, float y) => o.Any(s => BossPatterns.InCircle(p.Radius, x - s.x, y - s.y, body));
            Assert.True(Hit(0f, 0f));
            // 손잡이 반대(보스 반대쪽)로 반경 + 몸만큼 걸어 나가면 어느 별에도 안 맞는다
            Assert.False(Hit(p.Radius + body + 0.1f, 0f));
            // 걸어서 1.2초 안에 닿는 거리다
            Assert.True((p.Radius + body + 0.1f) / MoveBase < p.TelegraphSeconds);
        }

        [Fact] public void 북두칠성이_아닌_개수는_받지_않는다()
        {
            var p = new BossPatternDef { Id = "x", Count = 6, Length = 10, Arrangement = "big_dipper" };
            Assert.Throws<InvalidOperationException>(() => BossPatterns.BigDipperOffsets(p, 0f));
        }

        // ---- 태극권 ----

        [Fact] public void 태극권은_반경_안을_기본_이동속도_절반으로_끌어당긴다()
        {
            var p = P("hyeolheo", "taiji_inhale_exhale");
            Assert.Equal(0.5f, p.PullSpeedRatio);
            var v = BossPatterns.InhalePull(p, 6f, 0f, 1f, MoveBase);
            Assert.Equal(-MoveBase * 0.5f, v.x, 4);
            Assert.Equal(0f, v.y, 4);
            Assert.Equal((0f, 0f), BossPatterns.InhalePull(p, 10.5f, 0f, 1f, MoveBase));      // 반경 밖
            Assert.Equal((0f, 0f), BossPatterns.InhalePull(p, 6f, 0f, 0f, MoveBase));         // dt 0
            // 보스 몸에 닿으면 더 안 끌린다
            var near = BossPatterns.InhalePull(p, 1.2f, 0f, 1f, MoveBase, stopDistance: 1f);
            Assert.Equal(-0.2f, near.x, 4);
            Assert.Equal((0f, 0f), BossPatterns.InhalePull(p, 0.9f, 0f, 1f, MoveBase, stopDistance: 1f));
        }

        [Fact] public void 태극권_끝에_걸어_나가면_내뿜는_원을_벗어난다()
        {
            var p = P("hyeolheo", "taiji_inhale_exhale");
            float body = _d.Balance.Stats.PlayerBodyRadius;
            // 반경 끝에서 바깥으로 걸으면(기본 이동속도) 끌리는 것(절반)을 이겨 1.5초 뒤에도 내뿜는 원 밖
            float x = p.Radius;
            for (int i = 0; i < 90; i++) x += MoveBase / 60f + BossPatterns.InhalePull(p, x, 0f, 1f / 60f, MoveBase).x;
            Assert.False(BossPatterns.InBurst(p, x, 0f, body));
            Assert.True(BossPatterns.InBurst(p, p.BurstRadius, 0f, 0f));
            Assert.Equal(p.TelegraphSeconds + p.PullSeconds, BossPatterns.CastSeconds(p), 4);
            Assert.True(BossPatterns.CastSeconds(p) < _d.BossPatternSet("hyeolheo").Phase2IntervalSeconds);
        }

        // ---- 갈고리 사슬 ----

        [Fact] public void 갈고리_사슬은_초속으로_나가_길이에서_멈춘다()
        {
            var p = P("ma_cheonpae", "chain_hook_pull");
            float mps = BossPatterns.MetersPerSecond(p.ProjectileSpeed, MoveBase);
            Assert.Equal(400f / 100f * MoveBase, mps, 4);
            Assert.Equal(mps * 0.2f, BossPatterns.ChainReach(p, 0.2f, MoveBase), 4);
            Assert.Equal(p.Length, BossPatterns.ChainReach(p, 5f, MoveBase));
            float reach = BossPatterns.ChainReach(p, 0.5f, MoveBase);
            Assert.True(BossPatterns.InChain(p, reach - 0.1f, 0.3f, 0f, reach));
            Assert.False(BossPatterns.InChain(p, reach + 1f, 0f, 0f, reach));      // 아직 안 닿음
            Assert.False(BossPatterns.InChain(p, 3f, p.Width, 0f, reach));         // 줄 옆
            Assert.True(BossPatterns.InChain(p, 3f, p.Width, 0f, reach, _d.Balance.Stats.PlayerBodyRadius + 0.1f));
        }

        [Fact] public void 걸리면_보스_앞까지_곧게_끌려간다()
        {
            var p = P("ma_cheonpae", "chain_hook_pull");
            var start = BossPatterns.ChainPullOffset(p, 9f, 0f, 0f, 1f);
            Assert.Equal(9f, start.x, 4);
            var half = BossPatterns.ChainPullOffset(p, 9f, 0f, p.PullSeconds / 2, 1f);
            Assert.Equal(5f, half.x, 4);
            var end = BossPatterns.ChainPullOffset(p, 9f, 0f, p.PullSeconds * 3, 1f);
            Assert.Equal(1f, end.x, 4);
            Assert.Equal(0f, end.y, 4);
            Assert.True(BossPatterns.ChainPullDone(p, p.PullSeconds));
            Assert.False(BossPatterns.ChainPullDone(p, p.PullSeconds - 0.01f));
            var diag = BossPatterns.ChainPullOffset(p, 3f, 4f, p.PullSeconds, 1f);
            Assert.Equal(0.6f, diag.x, 4); Assert.Equal(0.8f, diag.y, 4);
            Assert.Equal((0.5f, 0f), BossPatterns.ChainPullOffset(p, 0.5f, 0f, 0.3f, 1f));   // 이미 보스 앞
        }

        // ---- 반쪽 독안개 ----

        [Fact] public void 반쪽_독안개는_처음엔_플레이어_쪽_그다음부터_번갈아()
        {
            var p = P("dang_sohye", "half_map_poison_fog");
            var fog = new HalfFieldFog(p);
            Assert.Equal(0, fog.LastSide);
            Assert.Equal(-1, fog.Choose(-3f));
            Assert.Equal(1, fog.Choose(-3f));     // 플레이어가 그대로 왼쪽이어도 번갈아
            Assert.Equal(-1, fog.Choose(5f));
            Assert.Equal(1, new HalfFieldFog(p).Choose(0f));   // 보스와 같은 줄이면 오른쪽
        }

        [Fact] public void 번갈지_않으면_시전마다_플레이어_쪽()
        {
            var p = new BossPatternDef { Id = "x", Shape = "half_field", Alternate = false };
            var fog = new HalfFieldFog(p);
            Assert.Equal(-1, fog.Choose(-3f));
            Assert.Equal(-1, fog.Choose(-1f));
            Assert.Equal(1, fog.Choose(2f));
        }

        [Fact] public void 독안개_안은_덮은_쪽과_경계에_걸친_몸()
        {
            Assert.True(HalfFieldFog.Covers(1, 3f));
            Assert.True(HalfFieldFog.Covers(1, 0f));
            Assert.False(HalfFieldFog.Covers(1, -0.5f));
            Assert.True(HalfFieldFog.Covers(1, -0.3f, 0.4f));
            Assert.True(HalfFieldFog.Covers(-1, -3f));
            Assert.False(HalfFieldFog.Covers(-1, 3f));
            Assert.False(HalfFieldFog.Covers(0, 3f));
        }

        [Fact] public void 기획서_태극권_줄에_끌리는_세기가_있다()
        {
            var row = GddTable(GddSection("혈허진인")).Single(r => r[0] == "태극권");
            Assert.Contains("기본 이동속도의 절반", row[4]);
        }
    }
}
