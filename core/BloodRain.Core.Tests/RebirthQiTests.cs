using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Progression;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>윤회의 기 정산(기획서 10장 '윤회의 기', cheonha.json rebirthQi).</summary>
    public class RebirthQiTests
    {
        readonly GameData _d = TestData.Load();
        RebirthQiDef Def => _d.Cheonha.RebirthQi;

        static int Parse(string s) => int.Parse(s.Replace(",", ""), CultureInfo.InvariantCulture);

        /// <summary>'약 20,300' 처럼 끝자리를 0으로 쓴 숫자와 같은 자리에서 반올림해 비교한다.</summary>
        static void AssertAbout(string stated, double actual)
        {
            var digits = stated.Replace(",", "");
            int zeros = digits.Length - digits.TrimEnd('0').Length;
            double unit = Math.Pow(10, zeros);
            Assert.True(Parse(stated) == Math.Round(actual / unit, MidpointRounding.AwayFromZero) * unit,
                $"기획서 '약 {stated}' 인데 계산은 {actual}");
        }

        [Fact] public void 기획서_10장_진기와_윤회의_기_문장이_진기_곡선과_맞는다()
        {
            var sec = TestData.GddSection("윤회의 기");
            var m = Regex.Match(sec, @"레벨 ([\d·]+)까지 모은 진기가 약 ([\d,·]+)이라, 혈운 농도 1에서 (.+?)이 남습니다");
            Assert.True(m.Success, "10장 진기 곡선 문장을 못 찾음");
            var levels = m.Groups[1].Value.Split('·').Select(Parse).ToList();
            var xps = m.Groups[2].Value.Split('·').ToList();
            var qis = Regex.Matches(m.Groups[3].Value, @"(\d+)분 약 ([\d,]+)").Select(x => (minute: Parse(x.Groups[1].Value), qi: x.Groups[2].Value)).ToList();
            Assert.Equal(new[] { 15, 30, 60, 90 }, levels);
            Assert.Equal(levels.Count, xps.Count);
            Assert.Equal(levels.Count, qis.Count);

            var curve = new XpCurve(_d.Balance.XpCurve);
            for (int i = 0; i < levels.Count; i++)
            {
                long xp = curve.CumulativeTo(levels[i]);
                AssertAbout(xps[i], xp);

                // 혈운 농도 1에서 그 진기를 다 먹고 보너스·은자 없이 끝난 생
                var ledger = new RebirthQiLedger(Def);
                ledger.AddOrb(xp, 1f);
                var result = ledger.Settle(0);
                AssertAbout(qis[i].qi, result.Total);
                Assert.Equal(result.FromOrbs, result.Total);

                // 데이터 목표와 같고, 분은 11장 목표 곡선(분당 3레벨)대로다
                var target = Def.Targets[i];
                Assert.Equal(qis[i].minute, (int)target.Minute);
                Assert.Equal(levels[i], target.Level);
                Assert.Equal(Parse(qis[i].qi), target.Qi);
                Assert.Equal(levels[i], qis[i].minute * 3);
            }
        }

        [Fact] public void 식_그대로_더하고_성질_배율을_전체에_곱한다()
        {
            var ledger = new RebirthQiLedger(Def, traitBonus: 0.3f);
            ledger.AddOrb(100, 1.0f);
            ledger.AddOrb(50, 2.2f);
            Assert.Equal(300, ledger.AddBonus(RebirthQiBonusKind.BossFirstKill, "songshan"));
            Assert.Equal(50, ledger.AddBonus(RebirthQiBonusKind.FirstVisitRegion, "songshan"));
            ledger.AddBonus(RebirthQiBonusKind.FirstVisitRegion, "wudangsan");
            Assert.Equal(100, ledger.AddBonus(RebirthQiBonusKind.FirstReachRealm, "jeoljeong"));
            var r = ledger.Settle(701);

            Assert.Equal(8, r.FromOrbs);           // 0.04 × (100 + 50 × 2.2) = 8.4
            Assert.Equal(500, r.FromBonuses);
            Assert.Equal(351, r.FromSilver);       // 0.5 × 701 = 350.5
            Assert.Equal(859, r.Subtotal);
            Assert.Equal(258, r.FromTraits);       // 859 × 0.3 = 257.7
            Assert.Equal(1117, r.Total);
            // 기획서 식을 소수 그대로 계산한 값과 1 안쪽으로 같다
            double exact = 1.3 * (0.04 * (100 + 50 * 2.2) + 500 + 0.5 * 701);
            Assert.InRange(r.Total, exact - 1, exact + 1);

            Assert.Equal(2, r.OrbCount);
            Assert.Equal(150, r.OrbValue, 6);
            Assert.Equal(100, r.BonusOf(RebirthQiBonusKind.FirstVisitRegion));
            Assert.Equal(0, r.BonusOf(RebirthQiBonusKind.BossRepeatKill));
        }

        [Fact] public void 정산_내역은_진기_보너스_종류별_은자_성질_순이고_더하면_합계다()
        {
            var ledger = new RebirthQiLedger(Def, traitBonus: 0.2f);
            ledger.AddOrb(1000, 1.3f);
            ledger.AddBonus(RebirthQiBonusKind.FirstReachRealm, "iryu");
            ledger.AddBonus(RebirthQiBonusKind.BossRepeatKill, "danggata");
            ledger.AddBonus(RebirthQiBonusKind.FirstReachRealm, "ilryu");
            var r = ledger.Settle(90);
            var lines = r.Lines;
            Assert.Equal(new[] { RebirthQiLineKind.Orbs, RebirthQiLineKind.BossRepeatKill, RebirthQiLineKind.FirstReachRealm, RebirthQiLineKind.Silver, RebirthQiLineKind.Traits },
                lines.Select(l => l.Kind));
            Assert.Equal(2, lines.Single(l => l.Kind == RebirthQiLineKind.FirstReachRealm).Count);
            Assert.Equal(200, lines.Single(l => l.Kind == RebirthQiLineKind.FirstReachRealm).Amount);
            Assert.Equal(90, lines.Single(l => l.Kind == RebirthQiLineKind.Silver).Count);
            Assert.Equal(r.Total, lines.Sum(l => l.Amount));

            // 성질 보너스가 없으면 성질 줄도 없다. 아무것도 없어도 진기·은자 줄은 있다
            var empty = new RebirthQiLedger(Def).Settle(0);
            Assert.Equal(new[] { RebirthQiLineKind.Orbs, RebirthQiLineKind.Silver }, empty.Lines.Select(l => l.Kind));
            Assert.Equal(0, empty.Total);

            // 아무렇게나 넣어도 줄을 더하면 합계
            var rng = new Random(5);
            for (int i = 0; i < 300; i++)
            {
                var led = new RebirthQiLedger(Def, (float)(rng.NextDouble() * 0.5));
                for (int k = rng.Next(50); k > 0; k--) led.AddOrb(rng.NextDouble() * 30, 0.8f + (float)rng.NextDouble() * 4.2f);
                for (int k = rng.Next(4); k > 0; k--) led.AddBonus((RebirthQiBonusKind)rng.Next(4), "x");
                var res = led.Settle(rng.Next(1500));
                Assert.Equal(res.Total, res.Lines.Sum(x => x.Amount));
                Assert.Equal(res.Subtotal + res.FromTraits, res.Total);
            }
        }

        [Fact] public void 구슬_값은_심득_배율_전_값을_쓴다()
        {
            var xp = new RunXp(new XpCurve(_d.Balance.XpCurve), new Realms(_d.Balance.Realms), growth: 1.5f);
            var ledger = new RebirthQiLedger(Def);
            for (int i = 0; i < 100; i++)
            {
                xp.Absorb(25);
                ledger.AddOrb(25, 1f);
            }
            Assert.Equal(3750, xp.Total, 6);           // 심득은 레벨을 올린다
            Assert.Equal(100, ledger.Settle(0).Total); // 윤회의 기는 0.04 × 2,500
        }

        [Fact] public void 혈운이_짙은_땅에서_먹은_구슬은_농도만큼_더_남는다()
        {
            Assert.Equal(400, Qi(1f));
            Assert.Equal(1720, Qi(4.3f));
            Assert.Equal(320, Qi(0.8f));

            int Qi(float density)
            {
                var l = new RebirthQiLedger(Def);
                for (int i = 0; i < 1000; i++) l.AddOrb(10, density);
                return l.Settle(0).Total;
            }
        }

        [Fact] public void 장부는_정산해도_그대로고_이상한_값은_받지_않는다()
        {
            var l = new RebirthQiLedger(Def);
            l.AddOrb(500, 1f);
            Assert.Equal(l.Settle(10).Total, l.Settle(10).Total);
            Assert.Equal(20, l.Settle(0).Total);
            Assert.Equal(0, l.Settle(-50).FromSilver);
            Assert.Throws<ArgumentOutOfRangeException>(() => l.AddOrb(-1, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => l.AddOrb(1, 0f));
            Assert.Equal(new[] { Def.BossFirstKill, Def.BossRepeatKill, Def.FirstVisitRegion, Def.FirstReachRealm },
                Enum.GetValues(typeof(RebirthQiBonusKind)).Cast<RebirthQiBonusKind>().Select(l.AmountOf));
            Assert.Equal(new[] { 300, 100, 50, 100 }, new[] { Def.BossFirstKill, Def.BossRepeatKill, Def.FirstVisitRegion, Def.FirstReachRealm });
        }
    }
}
