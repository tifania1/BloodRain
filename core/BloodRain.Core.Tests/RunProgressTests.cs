using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Combat;
using BloodRain.Core.Progression;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>한 판 진행(RunXp)과 오의 게이지(UltimateCharge).</summary>
    public class RunProgressTests
    {
        readonly Data.GameData _d = TestData.Load();

        RunXp NewRun(float growth = 1f, string mode = "normal") =>
            new RunXp(new XpCurve(_d.Balance.XpCurve), new Realms(_d.Balance.Realms), mode, growth);

        UltimateCharge NewCharge(float bonus = 0f) =>
            new UltimateCharge(_d.Balance.UltimateCharge, _d.Balance.Yinyang.Harmony, bonus);

        // ---- RunXp ----

        [Fact] public void 첫_레벨업은_진기_5()
        {
            var run = NewRun();
            Assert.Equal(0, run.Absorb(4).LevelUps);
            var r = run.Absorb(1);
            Assert.Equal(1, r.LevelUps);
            Assert.Equal(2, run.Level);
            Assert.Equal(0, run.Xp, 6);
        }

        [Fact] public void 큰_구슬_하나로_여러_번_오르고_남은_진기는_이월()
        {
            var run = NewRun();
            var r = run.Absorb(25); // 5 + 15 = 20 → Lv3, 5 남음
            Assert.Equal(2, r.LevelUps);
            Assert.Equal(3, run.Level);
            Assert.Equal(5, run.Xp, 6);
        }

        [Fact] public void 녹색_구슬로_레벨60까지_돌파는_네_번()
        {
            var run = NewRun();
            var seen = new List<string>();
            int levelUps = 0;
            while (run.Level < 60)
            {
                var r = run.Absorb(5); // 녹색 구슬
                levelUps += r.LevelUps;
                seen.AddRange(r.Breakthroughs.Select(b => b.NameKo));
            }
            Assert.Equal(new[] { "이류", "일류", "절정", "초절정" }, seen);
            Assert.Equal(60 - 1, levelUps);
            Assert.Equal("초절정", run.Realm.NameKo);
        }

        [Fact] public void 한_번에_여러_경지를_넘으면_순서대로_돌려준다()
        {
            var run = NewRun();
            var r = run.Absorb(new XpCurve(_d.Balance.XpCurve).CumulativeTo(36));
            Assert.Equal(36, run.Level);
            Assert.Equal(new[] { "이류", "일류", "절정" }, r.Breakthroughs.Select(b => b.NameKo).ToArray());
        }

        [Fact] public void 심득_배율을_곱한다()
        {
            var run = NewRun(growth: 1.5f);
            run.Absorb(10);
            Assert.Equal(15, run.Total, 6);
        }

        [Fact] public void 어느_생이든_레벨90에서_현경으로_돌파한다()
        {
            long to95 = new XpCurve(_d.Balance.XpCurve).CumulativeTo(95);
            foreach (var mode in new[] { "normal", "bloodmoon" })
            {
                var run = NewRun(mode: mode);
                var names = run.Absorb(to95).Breakthroughs.Select(b => b.NameKo).ToArray();
                Assert.Equal(new[] { "이류", "일류", "절정", "초절정", "화경", "현경" }, names);
                Assert.Equal("현경", run.Realm.NameKo);
            }
        }

        // ---- UltimateCharge ----

        [Fact] public void 처치_250마리면_가득_찬다()
        {
            var u = NewCharge();
            u.OnKill(YinYangBand.Bias, 249);
            Assert.False(u.IsFull);
            u.OnKill(YinYangBand.Bias);
            Assert.True(u.IsFull);
        }

        [Fact] public void 피격과_정예()
        {
            var u = NewCharge();
            u.OnHit(YinYangBand.Extreme);
            u.OnEliteKill(YinYangBand.Extreme);
            Assert.Equal(18f, u.Value, 3);
        }

        [Fact] public void 태극이면_25퍼센트_더_찬다()
        {
            var u = NewCharge();
            u.OnEliteKill(YinYangBand.Harmony);
            Assert.Equal(18.75f, u.Value, 3);
        }

        [Fact] public void 연무장_보너스와_태극은_곱한다()
        {
            var u = NewCharge(bonus: 0.2f);
            u.OnHit(YinYangBand.Harmony);
            Assert.Equal(3f * 1.2f * 1.25f, u.Value, 3);
        }

        [Fact] public void 정진과_역류_끊기는_배율_없이_더한다()
        {
            var u = NewCharge(bonus: 0.5f);
            u.OnSkip();
            u.AddFlat(50); // 역류 끊기(B-05에서 balance.json extreme 값으로)
            Assert.Equal(70f, u.Value, 3);
        }

        [Fact] public void 최대치를_넘지_않고_쓰면_비운다()
        {
            var u = NewCharge();
            Assert.False(u.TryUse());
            u.AddFlat(130);
            Assert.Equal(100f, u.Value);
            Assert.True(u.TryUse());
            Assert.Equal(0f, u.Value);
        }
    }
}
