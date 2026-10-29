using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Spawning;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>기연 사건 추첨(S-03, 기획서 20장).</summary>
    public class EncounterTests
    {
        readonly GameData _d = TestData.Load();

        EncounterDef E(string id) => _d.Synergies.Encounter(id);
        EncounterRoller R(int seed = 1) => new EncounterRoller(_d, new Random(seed));

        static EncounterContext Ctx(string? chapter = "chapter1", string? school = "shaolin", float minute = 0, float luck = 0,
            Dictionary<string, double>? bonus = null) =>
            new EncounterContext { Chapter = chapter, ChapterSchool = school, Minute = minute, Luck = luck, BonusChance = bonus };

        static Dictionary<string, double> Always(params string[] ids) => ids.ToDictionary(id => id, _ => 1.0);
        static Dictionary<string, double> Never(params string[] ids) => ids.ToDictionary(id => id, _ => -1.0);

        // ---- 확률 ----

        [Fact] public void 확률은_기본에_기연_곱하기_0_005를_더한다()
        {
            var r = R();
            Assert.Equal(0.4, r.Chance(E("hermit"), Ctx()), 9);
            Assert.Equal(0.5, r.Chance(E("hermit"), Ctx(luck: 20)), 9);
            Assert.Equal(0.35 + 0.05, r.Chance(E("cliff"), Ctx("chapter3", "waterfort", luck: 10)), 9);
        }

        [Fact] public void 확률은_0과_1_사이로_자른다()
        {
            var r = R();
            Assert.Equal(1.0, r.Chance(E("hermit"), Ctx(luck: 1000)));
            Assert.Equal(0.0, r.Chance(E("hermit"), Ctx(luck: -1000)));
        }

        [Fact] public void 진전_같은_데서_오는_확률을_더한다()
        {
            var bonus = new Dictionary<string, double> { ["whisper"] = 0.1 };
            Assert.Equal(0.25, R().Chance(E("whisper"), Ctx(minute: 15, bonus: bonus)), 9);
            Assert.Equal(0.4, R().Chance(E("hermit"), Ctx(minute: 15, bonus: bonus)), 9);
        }

        // ---- 어디서·언제 ----

        [Theory]
        [InlineData("prologue", true)]
        [InlineData("chapter1", false)]
        [InlineData("chapter2", false)]
        [InlineData("chapter3", true)]
        [InlineData("chapter4", true)]
        [InlineData("chapter5", true)]
        [InlineData("chapter6", false)]
        [InlineData(null, false)]
        public void 벼랑_기연은_벽이_있는_장에서만(string? chapter, bool expected)
        {
            Assert.Equal(expected, EncounterRoller.CanAppear(E("cliff"), Ctx(chapter)));
        }

        [Fact] public void 어느_장이든_나오는_사건은_장이_아닌_곳에서도_나온다()
        {
            Assert.True(EncounterRoller.CanAppear(E("hermit"), Ctx(null, null)));
            Assert.True(EncounterRoller.CanAppear(E("elixir"), Ctx("chapter7", "demonic")));
        }

        [Fact] public void 혈마의_속삭임은_게임_시간_15분부터()
        {
            var r = R();
            Assert.False(EncounterRoller.CanAppear(E("whisper"), Ctx(minute: 14.9f)));
            Assert.Equal(0, r.Chance(E("whisper"), Ctx(minute: 14.9f)));
            Assert.True(EncounterRoller.CanAppear(E("whisper"), Ctx(minute: 15)));
            Assert.Equal(0.15, r.Chance(E("whisper"), Ctx(minute: 15)), 9);
        }

        [Fact] public void 고수의_유해는_문파가_있는_땅에서만()
        {
            Assert.False(EncounterRoller.CanAppear(E("relic"), Ctx("prologue", null)));
            Assert.True(EncounterRoller.CanAppear(E("relic"), Ctx("chapter2", "tang")));
        }

        [Fact] public void 검총은_5장에만_영물은_4장과_6장에만()
        {
            Assert.True(EncounterRoller.CanAppear(E("sword_tomb"), Ctx("chapter5", "huashan")));
            Assert.False(EncounterRoller.CanAppear(E("sword_tomb"), Ctx("chapter4", "wudang")));
            Assert.True(EncounterRoller.CanAppear(E("spiritbeast"), Ctx("chapter4", "wudang")));
            Assert.True(EncounterRoller.CanAppear(E("spiritbeast"), Ctx("chapter6", "kunlun")));
            Assert.False(EncounterRoller.CanAppear(E("spiritbeast"), Ctx("chapter5", "huashan")));
        }

        // ---- 굴리기 ----

        [Fact] public void 사건마다_판에_한_번만_굴린다()
        {
            var r = R();
            var ctx = Ctx(bonus: Always("hermit"));
            Assert.True(r.Roll("hermit", ctx));
            Assert.False(r.Roll("hermit", ctx));
            Assert.True(r.HasRolled("hermit"));
            Assert.Equal(new[] { "hermit" }, r.Appeared);

            var miss = R();
            Assert.False(miss.Roll("hermit", Ctx(bonus: Never("hermit"))));
            Assert.False(miss.Roll("hermit", ctx)); // 안 나왔다고 다시 굴리지 않는다
            Assert.Empty(miss.Appeared);
        }

        [Fact] public void 아직_나올_수_없을_때는_굴리지_않고_남겨_둔다()
        {
            var r = R();
            var always = Always("whisper");
            Assert.False(r.Roll("whisper", Ctx(minute: 10, bonus: always)));
            Assert.False(r.HasRolled("whisper"));
            Assert.True(r.Roll("whisper", Ctx(minute: 15, bonus: always)));
        }

        [Fact] public void 한꺼번에_굴리면_지금_나올_수_있는_것만_데이터_순서로()
        {
            var r = R();
            var always = Always(_d.Synergies.Encounters.Select(e => e.Id).ToArray());
            Assert.Equal(new[] { "hermit", "relic", "elixir", "letter" }, r.RollAll(Ctx(minute: 0, bonus: always)).Select(e => e.Id));
            Assert.Equal(new[] { "whisper" }, r.RollAll(Ctx(minute: 15, bonus: always)).Select(e => e.Id));
            Assert.Empty(r.RollAll(Ctx(minute: 16, bonus: always)));
            Assert.False(r.HasRolled("cliff"));
        }

        [Fact] public void 반드시_나오게_하면_앞서_안_나왔어도_나온다()
        {
            var r = R();
            Assert.False(r.Roll("whisper", Ctx(minute: 15, bonus: Never("whisper"))));
            Assert.True(r.Force("whisper"));
            Assert.True(r.HasAppeared("whisper"));
            Assert.False(r.Force("whisper"));
            Assert.Equal(1, r.Appeared.Count(id => id == "whisper"));
            Assert.Throws<ArgumentException>(() => r.Force("no_such_event"));
        }

        [Fact] public void 같은_시드면_같은_사건이_나온다()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var a = EncounterRoller.SimulateRun(_d, new Random(seed), "chapter5", "huashan", 10);
                var b = EncounterRoller.SimulateRun(_d, new Random(seed), "chapter5", "huashan", 10);
                Assert.Equal(a, b);
            }
        }

        [Fact] public void 같은_시드에서_기연을_올리면_나오는_사건이_줄지_않는다()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                var low = EncounterRoller.SimulateRun(_d, new Random(seed), "chapter4", "wudang", 0);
                var high = EncounterRoller.SimulateRun(_d, new Random(seed), "chapter4", "wudang", 30);
                Assert.True(low.All(high.Contains), $"시드 {seed}: {string.Join(",", low)} ⊄ {string.Join(",", high)}");
            }
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(20f)]
        public void 여러_판을_굴리면_확률대로_나온다(float luck)
        {
            const int runs = 20000;
            var rng = new Random(2026);
            var seen = _d.Synergies.Encounters.ToDictionary(e => e.Id, _ => 0);
            for (int i = 0; i < runs; i++)
                foreach (var id in EncounterRoller.SimulateRun(_d, rng, "chapter5", "huashan", luck)) seen[id]++;

            foreach (var e in _d.Synergies.Encounters)
            {
                double expected = e.Id == "spiritbeast" ? 0 : Math.Min(1, e.Chance + luck * _d.Synergies.Luck.ChancePerLuck);
                Assert.True(Math.Abs(seen[e.Id] / (double)runs - expected) < 0.015, $"{e.NameKo}: {seen[e.Id] / (double)runs:0.000} / 기대 {expected:0.000}");
            }
        }
    }
}
