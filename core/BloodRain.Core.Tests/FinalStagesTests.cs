using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using BloodRain.Core.World;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>T-39: 종장 혈마 세 단계의 패턴 수치와 혈마의 그림자 세기.</summary>
    public class FinalStagesTests
    {
        readonly GameData _d = Load();
        float Dpm => _d.Balance.EnemyScaling.DamagePerMinute;
        static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        [Fact] public void 단계_패턴_id가_시간표와_같다()
        {
            foreach (var st in _d.FinalBoss.Stages)
            {
                var set = st.Set;
                Assert.NotEmpty(set.Order);
                Assert.Equal(st.Patterns.OrderBy(x => x), set.Order.Concat(set.Phase2.Select(p => p.Id)).OrderBy(x => x));
                Assert.Equal(set.Order.OrderBy(x => x), set.Patterns.Select(p => p.Id).OrderBy(x => x));
                Assert.All(set.Phase2, p => Assert.True(p.Continuous));
                Assert.All(set.Patterns.Concat(set.Phase2), p => Assert.True(p.TelegraphSeconds > 0, p.Id));
                Assert.True(set.IntervalSeconds > 0 && set.Phase2IntervalSeconds > 0);
            }
        }

        [Fact] public void FinalFight_가_단계마다_그_시간표를_준다()
        {
            var ff = new FinalFight(_d);
            for (int i = 0; i < 7; i++) ff.Defeat();
            foreach (var st in _d.FinalBoss.Stages)
            {
                Assert.Same(st.Set, ff.Patterns);
                ff.Defeat();
            }
            Assert.Null(ff.Patterns);
        }

        [Fact] public void 혈해는_시작하자마자_피바다가_차오르고_혈영이_솟는다()
        {
            var st = _d.FinalBoss.Stages.Single(s => s.Id == "hyeolhae");
            var bp = new BossPatterns(st.Set);
            var first = bp.Tick(0.01f, 1f);
            Assert.Contains(first, e => e.Kind == BossPatternEventKind.Phase2);
            Assert.Contains(first, e => e.Kind == BossPatternEventKind.Telegraph && e.Pattern!.Id == "rising_blood_sea");
            var later = bp.Tick(st.Set.FirstCastSeconds, 1f);
            Assert.Contains(later, e => e.Kind == BossPatternEventKind.Telegraph && e.Pattern!.Id == "shadow_rise");
        }

        [Fact] public void 혈의와_진혈마는_처음엔_2막이_없다()
        {
            foreach (var id in new[] { "hyeoleui", "jinhyeolma" })
            {
                var bp = new BossPatterns(_d.FinalBoss.Stages.Single(s => s.Id == id).Set);
                Assert.DoesNotContain(bp.Tick(0.01f, 1f), e => e.Kind == BossPatternEventKind.Phase2);
            }
        }

        [Fact] public void 혈영_솟음은_shadows_차례로_돌고_기혈은_10퍼센트()
        {
            var ff = new FinalFight(_d);
            var p = _d.FinalBoss.Stages.Single(s => s.Id == "hyeolhae").Set.Pattern("shadow_rise");
            for (int n = 0; n < 9; n++)
            {
                var id = ff.RisingShadow(n);
                Assert.Equal(_d.FinalBoss.Shadows[n % 7], id);
                Assert.Equal(_d.Bosses.Single(b => b.Id == id).Health * 0.1f, ff.RisingShadowHealth(n, p), 1);
            }
        }

        [Fact] public void 피바다_섬은_90초쯤에_바닥에_닿는다()
        {
            var p = _d.FinalBoss.Stages.Single(s => s.Id == "hyeolhae").Set.Pattern("rising_blood_sea");
            Assert.InRange((p.Radius - p.MinRadius) / p.ShrinkPerSecond, 85f, 95f);
        }

        [Fact] public void 단계_피해가_혁련휘보다_세다()
        {
            var hw = _d.BossPatternSet("hyeokryeon_hwi");
            var stages = _d.FinalBoss.Stages.Select(s => s.Set).ToList();
            Assert.True(stages[0].ContactDamage > hw.ContactDamage);
            Assert.True(stages[1].ContactDamage > stages[0].ContactDamage);
            Assert.True(stages[0].IntervalSeconds <= hw.IntervalSeconds);
        }

        [Fact] public void 단계_패턴_표가_기획서와_같다()
        {
            var sec = GddSection("종장 보스");
            var rows = GddTable(sec);
            var all = _d.FinalBoss.Stages.SelectMany(st => st.Set.Patterns.Concat(st.Set.Phase2).Select(p => (st, p))).ToList();
            Assert.Equal(all.Count, rows.Count);
            foreach (var (st, p) in all)
            {
                var row = rows.Single(r => r[1] == p.NameKo);
                Assert.Equal(st.NameKo, row[0]);
                if (p.BaseDamage > 0)
                {
                    var at20 = BossPatterns.ScaleDamage(p.BaseDamage, 20f, 1f, Dpm);
                    Assert.Contains($"{F(p.BaseDamage)}({F(at20)})", row[4]);
                }
            }
            foreach (var st in _d.FinalBoss.Stages.Where(s => s.Set.ContactDamage > 0))
                Assert.Contains($"몸에 닿으면 {F(st.Set.ContactDamage)}({F(BossPatterns.ScaleDamage(st.Set.ContactDamage, 20f, 1f, Dpm))})", sec);
        }

        // ---- 혈마의 그림자 ----

        BloodShadowDef Sh => _d.Cheonha.LifeEnd.Shadow;

        [Fact] public void 그림자_0단계는_없다()
        {
            var s = BloodShadow.Strength(Sh, 0);
            Assert.False(s.Active);
            Assert.Equal(1f, s.EnemyDamageMultiplier);
        }

        [Fact] public void 그림자는_단계마다_세지고_속도는_상한에서_멈춘다()
        {
            var prev = BloodShadow.Strength(Sh, 1);
            Assert.Equal(Sh.ContactDamage, prev.ContactDamage);
            Assert.True(prev.MoveSpeed < 100f);
            for (int step = 2; step <= 12; step++)
            {
                var s = BloodShadow.Strength(Sh, step);
                Assert.Equal(prev.ContactDamage * Sh.DamageGrowth, s.ContactDamage, 2);
                Assert.True(s.MoveSpeed >= prev.MoveSpeed && s.MoveSpeed <= Sh.MaxSpeed);
                Assert.True(s.EnemyDamageMultiplier > prev.EnemyDamageMultiplier);
                prev = s;
            }
            Assert.Equal(Sh.MaxSpeed, prev.MoveSpeed);
            Assert.Equal(3, BloodShadow.FirstStepFasterThanPlayer(Sh));
        }

        [Fact] public void 그림자_표가_기획서와_같다()
        {
            var rows = GddTable(GddSection("생이 끝나는 세 가지")).Where(r => r.Length == 5 && int.TryParse(r[0], out _)).ToList();
            Assert.Equal(4, rows.Count);
            foreach (var r in rows)
            {
                int step = int.Parse(r[0]);
                var s = BloodShadow.Strength(Sh, step);
                Assert.Equal($"{_d.Cheonha.LifeEnd.ShadowFromMinute + _d.Cheonha.LifeEnd.ShadowStepMinutes * (step - 1):0}분", r[1]);
                Assert.Equal(s.ContactDamage.ToString("0.#", CultureInfo.InvariantCulture), r[2]);
                Assert.Equal(F(s.MoveSpeed), r[3]);
                Assert.Equal($"{s.EnemyDamageMultiplier:0.0}배", r[4]);
            }
        }
    }
}
