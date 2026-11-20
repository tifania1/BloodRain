using System;
using System.IO;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>환혼 직접 고르기와 무작위 후보의 덕 자리(backlog GU-11g, 천하 확장 설계안 정한 것 7).</summary>
    public class RebirthDirectTests
    {
        readonly GameData _d = TestData.Load();
        static readonly string[] Unlocked = { "shaolin", "gaebang", "namgung", "tang" };

        [Fact] public void 해금한_문파는_직접_고를_수_있고_성질과_덕이_없다()
        {
            var draw = new RebirthDraw(_d.Cheonha, Unlocked, 3, 0, false, new Random(1));
            Assert.Equal(_d.Cheonha.Sects.Where(s => Unlocked.Contains(s.Id)).Select(s => s.Id), draw.DirectChoices.Select(s => s.Id));
            var body = draw.ChooseDirect("tang");
            Assert.Equal("tang", body.Sect!.Id);
            Assert.Equal(_d.Cheonha.Sect("tang").Region, body.Region.Id);
            Assert.Empty(body.Traits);
            Assert.Null(body.Boon);
            Assert.True(draw.ChooseDirect("gaebang", female: true).Female);
        }

        [Fact] public void 해금_안_한_문파나_첫_환혼은_직접_못_고른다()
        {
            var draw = new RebirthDraw(_d.Cheonha, Unlocked, 3, 0, false, new Random(1));
            Assert.Throws<ArgumentException>(() => draw.ChooseDirect("demonic"));
            Assert.Throws<ArgumentException>(() => draw.ChooseDirect("shaolin", female: true)); // 소림은 늘 남자 몸
            var first = new RebirthDraw(_d.Cheonha, Unlocked, 3, 0, true, new Random(1));
            Assert.Empty(first.DirectChoices);
            Assert.Throws<InvalidOperationException>(() => first.ChooseDirect("shaolin"));
        }

        [Fact] public void 덕_목록이_비면_후보와_난수가_예전과_같다()
        {
            var files = GameData.FileNames.ToDictionary(n => n, n => File.ReadAllText(Path.Combine(TestData.DataDir, n + ".json")));
            files["cheonha"] = System.Text.RegularExpressions.Regex.Replace(files["cheonha"], "\"boons\": \\[[^\\]]*\\]", "\"boons\": []");
            var c = GameData.FromJson(files).Cheonha;
            Assert.Empty(c.Rebirth.Boons);
            var a = new RebirthDraw(c, Unlocked, 3, 1, false, new Random(7));
            var b = Rebirth.Roll(c, Unlocked, 3, new Random(7));
            Assert.Equal(b.Select(x => x.Sect.Id + string.Join(",", x.Traits.Select(t => t.Id)) + x.Female), a.Candidates.Select(x => x.Sect.Id + string.Join(",", x.Traits.Select(t => t.Id)) + x.Female));
            Assert.All(a.Candidates, x => Assert.Null(x.Boon));
        }

        [Fact] public void 덕은_복덕_하나고_그_생_윤회의_기_25퍼센트()
        {
            // 2026-11-20 GU-11g1(decisions #63 추천대로): 무작위 후보에만, 직접 고른 몸엔 없음
            var boon = Assert.Single(_d.Cheonha.Rebirth.Boons);
            Assert.Equal(0.25f, boon.RebirthQi);
            var draw = new RebirthDraw(_d.Cheonha, Unlocked, 3, 1, false, new Random(5));
            Assert.All(draw.Candidates, x => Assert.Equal(boon.Id, x.Boon));
            var body = draw.Choose(0);
            float traits = draw.Candidates[0].Traits.Sum(t => t.RebirthQi);
            Assert.Equal(traits + 0.25f, body.RebirthQiBonus, 4);
            var direct = draw.ChooseDirect("shaolin");
            Assert.Null(direct.Boon);
            Assert.Equal(0f, direct.RebirthQiBonus);
        }

        [Fact] public void 덕이_있으면_무작위_후보마다_하나씩_붙고_몸까지_간다()
        {
            var files = GameData.FileNames.ToDictionary(n => n, n => File.ReadAllText(Path.Combine(TestData.DataDir, n + ".json")));
            files["cheonha"] = System.Text.RegularExpressions.Regex.Replace(files["cheonha"], "\"boons\": \\[[^\\]]*\\]", "\"boons\": [{\"id\": \"test_a\", \"nameKo\": \"시험 덕\"}, {\"id\": \"test_b\", \"nameKo\": \"시험 덕 둘\"}]");
            var c = GameData.FromJson(files).Cheonha;
            var draw = new RebirthDraw(c, Unlocked, 3, 1, false, new Random(3));
            Assert.All(draw.Candidates, x => Assert.Contains(x.Boon, new[] { "test_a", "test_b" }));
            Assert.Equal(draw.Candidates[0].Boon, draw.Choose(0).Boon);
            Assert.All(draw.Reroll(), x => Assert.NotNull(x.Boon));
            Assert.Null(draw.ChooseDirect("shaolin").Boon);
            // 첫 환혼은 고정이라 덕도 없다
            Assert.All(new RebirthDraw(c, Unlocked, 3, 0, true, new Random(3)).Candidates, x => Assert.Null(x.Boon));
        }
    }
}
