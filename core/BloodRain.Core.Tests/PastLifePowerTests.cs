using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Progression;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>전생의 공력(backlog GU-11f, 설계안 '전생의 공력 (추천 규칙)').</summary>
    public class PastLifePowerTests
    {
        readonly GameData _d = TestData.Load();
        List<RealmDef> Realms => _d.Balance.Realms.OrderBy(r => r.Level).ToList();
        int LevelOf(string id) => Realms.Single(r => r.Id == id).Level;

        [Fact] public void 첫_생은_아무것도_줄지_않는다()
        {
            var p = PastLifePower.None(Realms);
            for (int lv = 1; lv <= 100; lv++) Assert.Equal(1f, p.RequiredScale(lv));
            Assert.Equal(1f, PastLifePower.AfterLives(Realms, 0, "hyeongyeong").RequiredScale(5));
        }

        [Fact] public void 넘을_때마다_4퍼센트씩_줄고_60퍼센트에서_멈춘다()
        {
            // 2026-11-20 GU-11f1 추천대로(decisions #62): 4%·상한 60%
            Assert.Equal(0.04f, _d.Cheonha.PastLife.CutPerCrossing);
            Assert.Equal(0.6f, _d.Cheonha.PastLife.MaxCut);
            Assert.Equal(PastLifePower.WakeCrossings, _d.Cheonha.PastLife.WakeCrossings);
            for (int n = 0; n <= 20; n++)
            {
                var p = PastLifePower.AfterLives(Realms, n, "hyeongyeong");
                Assert.Equal(1f - Math.Min(0.60f, 0.04f * n), p.RequiredScale(LevelOf("iryu")), 4); // 이류 → 일류 구간
            }
        }

        [Fact] public void 가장_높이_오른_경지로_가는_구간과_그_위는_제_속도()
        {
            var c = new Dictionary<string, int> { ["iryu"] = 5, ["ilryu"] = 5, ["jeoljeong"] = 5 };
            var p = new PastLifePower(Realms, c, "jeoljeong");
            Assert.Equal(0.8f, p.RequiredScale(LevelOf("samryu")), 4);          // 삼류 → 이류
            Assert.Equal(0.8f, p.RequiredScale(LevelOf("ilryu") - 1), 4);       // 이류 → 일류 끝
            Assert.Equal(1f, p.RequiredScale(LevelOf("ilryu")));                // 일류 → 절정: 맨 앞
            Assert.Equal(1f, p.RequiredScale(LevelOf("jeoljeong")));            // 넘어 본 적 없음
            Assert.Equal(1f, p.RequiredScale(LevelOf("hyeongyeong") + 5));      // 현경 위는 구간 없음
        }

        [Fact] public void RunXp_필요_진기에_곱해진다()
        {
            var curve = new XpCurve(_d.Balance.XpCurve);
            var xp = new RunXp(curve, new Realms(_d.Balance.Realms));
            int plain = xp.Required;
            xp.PastLife = PastLifePower.AfterLives(Realms, 10, "hyeongyeong");
            Assert.Equal((int)Math.Ceiling(plain * 0.6 - 1e-6), xp.Required);
            Assert.True(xp.Required >= 1);
        }

        [Fact] public void 잘못된_값은_막는다()
        {
            Assert.Throws<ArgumentException>(() => new PastLifePower(Realms, new Dictionary<string, int> { ["nowhere"] = 1 }, null));
            Assert.Throws<ArgumentException>(() => new PastLifePower(Realms, new Dictionary<string, int>(), "nowhere"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PastLifePower(Realms, new Dictionary<string, int> { ["iryu"] = -1 }, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PastLifePower(Realms, new Dictionary<string, int>(), null, 0.04f, 1f));
        }

        [Fact] public void 육십분_보고서가_5분마다_60분까지_선다()
        {
            var md = BloodRain.Sim.LongLifeSim.Report(BloodRain.Sim.SimData.Load(TestData.DataDir), seeds: 1);
            Assert.Contains("| 60 |", md);
            Assert.Equal(12, md.Split('\n').Count(l => System.Text.RegularExpressions.Regex.IsMatch(l, @"^\| \d+ \| \d+ \|")));
        }

        [Fact] public void 가속_보고서가_목표_곡선과_나란히_선다()
        {
            var md = BloodRain.Sim.AccelerationSim.Report(BloodRain.Sim.SimData.Load(TestData.DataDir), seeds: 1);
            Assert.Contains("## 정한 값 4%·상한 60%", md);
            Assert.Contains("| 100 |", md);
            Assert.Contains("1:30", md);
        }
    }
}
