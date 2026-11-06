using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using Newtonsoft.Json.Linq;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>WP-33: 기획서 10장 해금 표 '금두꺼비 누적 10마리 → 탐욕 최대 레벨 +2'.
    /// 한 생 기록은 LifeLog.CatchGoldenToad, 누적은 Soul.GoldenToadsTotal, 상한은 balance.json training bonusMax.</summary>
    public class GoldenToadTrainingTests
    {
        readonly GameData _d = TestData.Load();
        CheonhaDef C => _d.Cheonha;

        RebirthBody Body(string sect) => Rebirth.Body(C, new RebirthCandidate(C.Sect(sect), C.Region(C.Sect(sect).Region), new List<BirthTraitDef>()));

        Soul AfterPrologue()
        {
            var s = new Soul(_d);
            s.EndLife(s.BeginLife(Rebirth.Prologue(_d)).Finish("fallen", 0));
            return s;
        }

        void Live(Soul s, int toads)
        {
            var life = s.BeginLife(Body("shaolin"));
            for (int i = 0; i < toads; i++) life.CatchGoldenToad();
            s.EndLife(life.Finish("fallen", 0));
        }

        [Fact] public void 데이터가_기획서_10장_해금_표와_같다()
        {
            var greed = _d.Balance.Training.Items.Single(i => i.Stat == "Greed");
            Assert.Contains("| 금두꺼비 누적 10마리 | 탐욕 최대 레벨 +2 |", TestData.Gdd);
            Assert.NotNull(greed.BonusMax);
            Assert.Equal((10, 2), (greed.BonusMax!.GoldenToadsTotal, greed.BonusMax.Levels));
            Assert.All(_d.Balance.Training.Items.Where(i => i.Stat != "Greed"), i => Assert.Null(i.BonusMax));
        }

        [Fact] public void 열마리부터_탐욕을_일곱까지_산다()
        {
            var s = AfterPrologue();
            Live(s, 4);
            Live(s, 5);
            Assert.Equal(9, s.GoldenToadsTotal);
            Assert.Equal(5, s.TrainingMax("Greed"));
            s.AddRebirthQi(1_000_000);
            for (int i = 0; i < 5; i++) Assert.True(s.TryBuyTraining("Greed"));
            Assert.False(s.TryBuyTraining("Greed"));
            Assert.Null(s.TrainingCost("Greed"));
            Live(s, 1);
            Assert.Equal(7, s.TrainingMax("Greed"));
            Assert.NotNull(s.TrainingCost("Greed"));
            Assert.True(s.TryBuyTraining("Greed"));
            Assert.True(s.TryBuyTraining("Greed"));
            Assert.False(s.TryBuyTraining("Greed"));
            Assert.Equal(0.70f, s.TrainingBonus()[StatKind.Greed], 3);
            Assert.Equal(5, s.TrainingMax("Might"));
        }

        [Fact] public void 누적은_저장_왕복하고_옛_저장은_0이다()
        {
            var s = AfterPrologue();
            Live(s, 3);
            var json = s.ToJson();
            Assert.Equal(3, (int)JObject.Parse(json)["goldenToads"]!);
            var back = Soul.FromJson(_d, json);
            Assert.Equal(3, back.GoldenToadsTotal);
            Assert.Equal(json, back.ToJson());
            var old = JObject.Parse(json); old.Remove("goldenToads");
            Assert.Equal(0, Soul.FromJson(_d, old.ToString()).GoldenToadsTotal);
        }

        [Fact] public void 연무장_상한을_넘는_레벨은_해금_전엔_잘린다()
        {
            var t = _d.Balance.Training;
            var lv = new Dictionary<string, int> { ["Greed"] = 7 };
            Assert.Equal(0.5f, Stats.FromTraining(t, lv)[StatKind.Greed], 3);
            Assert.Equal(0.7f, Stats.FromTraining(t, lv, 10)[StatKind.Greed], 3);
        }

        [Fact] public void 웹_한_생_판도_같은_규칙()
        {
            string html = TestData.ReadRepoFile("web", "prototype", "template.html");
            Assert.Contains("const trainMax = (m, it) => it.maxLevel + (it.bonusMax && (m.toads || 0) >= it.bonusMax.goldenToadsTotal ? it.bonusMax.levels : 0);", html);
            Assert.Contains("if (lv >= trainMax(m, it)) return null;", html);
            Assert.Contains("const lv = Math.min(m.train[it.stat] || 0, trainMax(m, it));", html);
            Assert.Contains("m.toads = (m.toads || 0) + (st.toads || 0);", html);
            Assert.Contains("if (e.type === 'golden_toad') { G.toads = (G.toads || 0) + 1;", html);
        }
    }
}
