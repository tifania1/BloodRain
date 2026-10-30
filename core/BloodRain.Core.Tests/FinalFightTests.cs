using System;
using System.Linq;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>종장 혈마의 차례(T-14 뼈대, 기획서 6장 '종장 보스').</summary>
    public class FinalFightTests
    {
        readonly GameData _d = Load();

        [Fact] public void 혈영은_땅의_주인_일곱이고_모두_패턴이_있다()
        {
            var f = _d.FinalBoss;
            var regionBosses = _d.Cheonha.BossRegions.Select(r => r.Boss!).OrderBy(x => x);
            Assert.Equal(regionBosses, f.Shadows.OrderBy(x => x));
            Assert.All(f.Shadows, id => Assert.NotNull(_d.BossPatternSet(id)));
            Assert.Equal(0.4f, f.ShadowHealthRatio);
            Assert.Contains("혈영(체력 40%판)", GddSection("종장 보스"));
        }

        [Fact] public void 단계_셋의_기혈이_enemies_json과_기획서와_같다()
        {
            var f = _d.FinalBoss;
            var boss = _d.Bosses.Single(b => b.Id == f.Boss);
            Assert.Equal(boss.Health, f.Stages[0].Health);
            var sec = GddSection("종장 보스");
            foreach (var st in f.Stages)
                Assert.Contains($"**{st.NameKo}({st.Hanja})** · 체력 {st.Health:N0}", sec);
            Assert.Equal(boss.Patterns, f.Stages[0].Patterns);
            Assert.Contains("체력 10%에서 목 노인", sec);
            Assert.Equal(0.1f, f.Stages.Last().TrueEndingCutAt);
        }

        [Fact] public void 혈영_일곱_뒤_세_단계를_차례로_넘긴다()
        {
            var ff = new FinalFight(_d);
            Assert.Equal(10, ff.Count);
            for (int i = 0; i < 7; i++)
            {
                Assert.True(ff.IsShadow);
                var b = _d.Bosses.Single(x => x.Id == ff.BossId);
                Assert.Equal(b.Health * 0.4f, ff.BaseHealth, 1);
                Assert.NotNull(ff.Patterns);
                ff.Defeat();
            }
            Assert.Equal(new[] { "hyeoleui", "hyeolhae", "jinhyeolma" }, Enumerable.Range(0, 3).Select(_ =>
            {
                Assert.False(ff.IsShadow);
                Assert.Equal("danmok_hyeon", ff.BossId);
                var id = ff.Stage!.Id;
                ff.Defeat();
                return id;
            }));
            Assert.True(ff.Done);
            Assert.False(ff.CutToTrueEnding);
            Assert.Throws<InvalidOperationException>(() => ff.Defeat());
        }

        [Fact] public void 진엔딩_조건이면_진혈마_10퍼센트에서_컷으로()
        {
            var ff = new FinalFight(_d);
            for (int i = 0; i < 9; i++) ff.Defeat();
            Assert.Equal("jinhyeolma", ff.Stage!.Id);
            Assert.False(ff.CheckTrueEnding(0.05f, trueEndingMet: false));
            Assert.False(ff.CheckTrueEnding(0.2f, trueEndingMet: true));
            Assert.True(ff.CheckTrueEnding(0.1f, trueEndingMet: true));
            Assert.True(ff.Done);
            Assert.True(ff.CutToTrueEnding);
        }

        [Fact] public void 앞_단계에선_진엔딩_컷이_없다()
        {
            var ff = new FinalFight(_d);
            for (int i = 0; i < 7; i++) ff.Defeat();
            Assert.False(ff.CheckTrueEnding(0.01f, true));
        }
    }
}
