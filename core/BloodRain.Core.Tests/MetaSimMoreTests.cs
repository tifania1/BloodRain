using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BloodRain.Sim;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>메타 곡선(MetaSim, docs/design/sim/meta.md)이 맨 위 주석에 적은 가정대로 도는지,
    /// 보고서 표가 지금 데이터와 같은지 본다. 보고서 전체는 20초쯤 걸려 생 몇 개만 돌린다.</summary>
    public class MetaSimMoreTests
    {
        static readonly Lazy<SimData> Data = new Lazy<SimData>(() => SimData.Load(TestData.DataDir));
        static readonly Lazy<List<MetaLifeRow>> Eight = new Lazy<List<MetaLifeRow>>(() => MetaSim.Run(Data.Value, 8, null, maxLives: 8));

        [Fact] public void 버틴_분은_첫_생_분에_앞생까지_산_연무장_레벨마다_반_분()
        {
            var rows = Eight.Value;
            Assert.Equal(8, rows[0].Minutes);
            for (int i = 1; i < rows.Count; i++)
                Assert.Equal(Math.Min(CheonhaSim.Minutes, 8 + MetaSim.PerTrainingLevel * rows[i - 1].TrainingLevels), rows[i].Minutes, 6);
        }

        [Fact] public void 누적_시간은_서장에_생마다_버틴_분과_객잔_1분()
        {
            var rows = Eight.Value;
            double h = MetaSim.PrologueMinutes / 60;
            foreach (var r in rows)
            {
                h += (r.Minutes + MetaSim.InnMinutes) / 60;
                Assert.Equal(h, r.Hours, 6);
            }
        }

        [Fact] public void 버티는_분은_30분을_넘지_않는다()
        {
            var rows = MetaSim.Run(Data.Value, 40, null, maxLives: 2);
            Assert.All(rows, r => Assert.Equal(CheonhaSim.Minutes, r.Minutes));
        }

        [Fact] public void 봉인석과_연무장은_줄지_않고_땅의_주인을_잡아야_봉인석이_는다()
        {
            var rows = Eight.Value;
            int stones = 0, levels = 0;
            foreach (var r in rows)
            {
                Assert.True(r.SealStones >= stones);
                Assert.True(r.TrainingLevels >= levels);
                if (r.SealStones > stones) Assert.NotEmpty(r.Killed);
                stones = r.SealStones; levels = r.TrainingLevels;
                Assert.True(r.Qi > 0);
            }
        }

        [Fact] public void 같은_씨앗이면_같은_생이_나온다()
        {
            var a = MetaSim.Run(Data.Value, 8, null, maxLives: 3, seed: 7);
            var b = MetaSim.Run(Data.Value, 8, null, maxLives: 3, seed: 7);
            Assert.Equal(a.Select(Line), b.Select(Line));
        }

        [Fact] public void 봉인석_일곱이_모이면_그_생에서_멈춘다()
        {
            // meta.md: 지금 데이터, 첫 생 12분이면 15생에 봉인석 7.
            var rows = MetaSim.Run(Data.Value, 12, null);
            var need = Data.Value.Game.Cheonha.Bosses.FinalRequires.SealStones;
            Assert.Equal(need, rows.Last().SealStones);
            Assert.True(rows.Take(rows.Count - 1).All(r => r.SealStones < need) || rows.Count == 5);
        }

        [Fact] public void 보고서_생마다_표의_앞_여덟_줄이_지금_데이터와_같다()
        {
            var md = TestData.ReadRepoFile("docs", "design", "sim", "meta.md");
            int at = md.IndexOf("## 생마다(지금 데이터, 첫 생 8분)", StringComparison.Ordinal);
            Assert.True(at >= 0, "meta.md 에 '생마다(지금 데이터, 첫 생 8분)' 표가 없다");
            var lines = md.Substring(at).Split('\n')
                .Where(l => Regex.IsMatch(l, @"^\| \d+ \|")).Take(8).Select(l => l.TrimEnd('\r')).ToList();
            Assert.Equal(8, lines.Count);
            Assert.Equal(lines, Eight.Value.Select(Line));
        }

        static string Line(MetaLifeRow r) =>
            $"| {r.Life} | {r.Sect} | {r.Minutes:0.#} | {r.Realm} | {(r.Killed.Count == 0 ? "—" : string.Join(", ", r.Killed))} | {r.Qi:N0} | {r.TrainingLevels} | {r.SealStones} | {r.Hours:0.0} |";
    }
}
