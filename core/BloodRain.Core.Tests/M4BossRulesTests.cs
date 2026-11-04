using System;
using System.Linq;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>R4-02: 낙매검선의 검혼·빙혈 설무향·혈영회주 혁련휘 판정(꽃잎 나선, 매화검기 방사, 눈보라, 빙분신, 얼음 가시,
    /// 줄어드는 싸움터, 순간이동, 혈월). 수치는 기획서 6장 '땅의 주인 패턴 수치'.</summary>
    public class M4BossRulesTests
    {
        readonly GameData _d = Load();
        BossPatternDef P(string boss, string id) => _d.BossPatternSet(boss).Pattern(id);

        static float Dist(float x, float y) => (float)Math.Sqrt(x * x + y * y);

        // ---- 낙매검선의 검혼 ----

        [Fact] public void 꽃잎_나선은_세_갈래가_120도씩_벌어져_초당_120도_돈다()
        {
            var p = P("huashan_sword_soul", "petal_spiral");
            Assert.Equal(25, BossPatterns.SpiralShotsPerArm(p));   // 2.5초 동안 0.1초마다
            Assert.Equal(0f, BossPatterns.SpiralShotDegrees(p, 0f, 0, 0), 3);
            Assert.Equal(120f, BossPatterns.SpiralShotDegrees(p, 0f, 1, 0), 3);
            Assert.Equal(240f, BossPatterns.SpiralShotDegrees(p, 0f, 2, 0), 3);
            Assert.Equal(120f, BossPatterns.SpiralShotDegrees(p, 0f, 0, 10), 3);   // 1초 뒤
            Assert.Equal(2.4f, BossPatterns.SpiralShotSeconds(p, 24), 3);           // 마지막 발은 2.5초 전에
            Assert.Equal(p.TelegraphSeconds + p.LeaveSeconds, BossPatterns.CastSeconds(p), 4);
            Assert.True(BossPatterns.CastSeconds(p) < _d.BossPatternSet("huashan_sword_soul").Phase2IntervalSeconds);
        }

        [Fact] public void 매화검기_방사는_스물네_갈래이고_파마다_3점75도_엇갈린다()
        {
            var p = P("huashan_sword_soul", "twenty_four_petal_burst");
            var w0 = BossPatterns.RadialBurstDegrees(p, 0f, 0);
            var w1 = BossPatterns.RadialBurstDegrees(p, 0f, 1);
            Assert.Equal(24, w0.Length);
            Assert.Equal(15f, w0[1] - w0[0], 3);
            Assert.Equal(3.75f, w1[0] - w0[0], 3);
            // 네 파면 한 갈래 사이를 다 돌아 처음 자리와 같다
            static double[] Set(float[] w) => w.Select(a => Math.Round(a, 2) % 360).OrderBy(a => a).ToArray();
            Assert.Equal(Set(w0), Set(BossPatterns.RadialBurstDegrees(p, 0f, 4)));
            Assert.Contains($"파마다 {Num(p.WaveOffsetDegrees)}°씩 엇갈린다", Gdd);
        }

        // ---- 빙혈 설무향 ----

        [Fact] public void 눈보라는_6초_동안_보이는_범위를_0점6배로()
        {
            var p = P("seol_muhyang", "blizzard_narrow_view");
            Assert.Equal(0.6f, BossPatterns.ViewScaleAt(p, 0f));
            Assert.Equal(0.6f, BossPatterns.ViewScaleAt(p, 5.9f));
            Assert.Equal(1f, BossPatterns.ViewScaleAt(p, 6f));
            Assert.Equal(1f, BossPatterns.ViewScaleAt(p, -0.1f));
        }

        [Fact] public void 빙분신은_넷이_6m_안에_서고_진짜만_그림자가_있다()
        {
            var p = P("seol_muhyang", "ice_clones_x4");
            for (int seed = 0; seed < 50; seed++)
            {
                var c = new BossClones(p, new Random(seed));
                Assert.Equal(4, c.Offsets.Count);
                Assert.All(c.Offsets, o => Assert.True(Dist(o.x, o.y) <= p.Scatter + 1e-3f));
                Assert.Equal(1, Enumerable.Range(0, 4).Count(c.HasShadow));
            }
        }

        [Fact] public void 분신은_한_대에_깨지고_8초_뒤_사라지며_진짜는_남는다()
        {
            var p = P("seol_muhyang", "ice_clones_x4");
            var c = new BossClones(p, new Random(3));
            int real = c.RealIndex, fake = (real + 1) % 4, other = (real + 2) % 4;
            Assert.True(c.Standing(fake, 0f));
            Assert.True(c.Hit(fake));
            Assert.False(c.Standing(fake, 0.1f));
            Assert.False(c.Hit(fake));        // 이미 깨짐
            Assert.False(c.Hit(real));        // 진짜는 깨지지 않는다
            Assert.True(c.Standing(real, 0.1f));
            Assert.True(c.Standing(other, 7.9f));
            Assert.False(c.Standing(other, 8f));
            Assert.True(c.Standing(real, 30f));
        }

        [Fact] public void 얼음_가시는_보스_발밑에서_12m까지_여덟이_0점12초_간격으로_솟는다()
        {
            var p = P("seol_muhyang", "ice_spike_chain");
            var first = BossPatterns.ChainCircleOffset(p, 90f, 0);
            var last = BossPatterns.ChainCircleOffset(p, 90f, 7);
            Assert.Equal(0f, Dist(first.x, first.y), 3);
            Assert.Equal(0f, last.x, 3);
            Assert.Equal(12f, last.y, 3);
            Assert.Equal(0.84f, BossPatterns.ChainCircleSeconds(p, 7), 3);
            // 이웃 가시끼리 겹쳐 틈 없이 이어진다
            var second = BossPatterns.ChainCircleOffset(p, 90f, 1);
            Assert.True(second.y < 2 * p.Radius);
            Assert.True(BossPatterns.InChainCircle(p, 0f, 7, 12.5f, 0f));
            Assert.False(BossPatterns.InChainCircle(p, 0f, 7, 13.5f, 0f));
            Assert.Equal(0.8f + 0.84f, BossPatterns.CastSeconds(p), 3);
            Assert.True(BossPatterns.CastSeconds(p) < _d.BossPatternSet("seol_muhyang").Phase2IntervalSeconds);
        }

        [Fact] public void 싸움터는_20m에서_8m까지_48초에_줄고_모닥불_곁이면_96초()
        {
            var p = P("seol_muhyang", "freezing_edges");
            Assert.Equal(48f, BossPatterns.ArenaSeconds(p, false), 3);
            Assert.Equal(96f, BossPatterns.ArenaSeconds(p, true), 3);
            Assert.Equal(19.75f, BossPatterns.ArenaRadiusStep(p, p.Radius, 1f, false), 3);
            Assert.Equal(19.875f, BossPatterns.ArenaRadiusStep(p, p.Radius, 1f, true), 3);
            Assert.Equal(8f, BossPatterns.ArenaRadiusStep(p, 8.1f, 5f, false), 3);
            Assert.True(BossPatterns.OutsideArena(10f, 10.5f, 0f));
            Assert.False(BossPatterns.OutsideArena(10f, 10.5f, 0f, 0.6f));   // 몸이 걸치면 아직 안
            Assert.False(BossPatterns.OutsideArena(10f, 6f, 6f));
        }

        // ---- 혈영회주 혁련휘 ----

        [Fact] public void 순간이동은_5m_안_네_자리를_0점4초마다_옮긴_뒤_내려찍는다()
        {
            var p = P("hyeokryeon_hwi", "blink_x4_slam");
            for (int seed = 0; seed < 50; seed++)
            {
                var offs = BossPatterns.BlinkOffsets(p, new Random(seed));
                Assert.Equal(4, offs.Length);
                Assert.All(offs, o => Assert.True(Dist(o.x, o.y) <= p.Scatter + 1e-3f));
            }
            Assert.Equal(0f, BossPatterns.BlinkSeconds(p, 0));
            Assert.Equal(1.2f, BossPatterns.BlinkSeconds(p, 3), 3);
            Assert.Equal(1.8f, BossPatterns.BlinkSlamSeconds(p), 3);
            Assert.Equal(BossPatterns.BlinkSlamSeconds(p), BossPatterns.CastSeconds(p), 4);
            Assert.True(BossPatterns.CastSeconds(p) < _d.BossPatternSet("hyeokryeon_hwi").Phase2IntervalSeconds);
        }

        [Fact] public void 혈월이_켜지면_모든_적의_피해_1점3배_이동속도_1점2배()
        {
            var p = P("hyeokryeon_hwi", "blood_moon_empower");
            Assert.Equal(1.3f, BossPatterns.EmpowerDamage(p, true));
            Assert.Equal(1.2f, BossPatterns.EmpowerSpeed(p, true));
            Assert.Equal(1f, BossPatterns.EmpowerDamage(p, false));
            Assert.Equal(1f, BossPatterns.EmpowerSpeed(p, false));
            Assert.Contains($"모든 적의 피해 {Num(p.EnemyDamageMultiplier)}배, 이동속도 {Num(p.EnemySpeedMultiplier)}배", Gdd);
        }
    }
}
