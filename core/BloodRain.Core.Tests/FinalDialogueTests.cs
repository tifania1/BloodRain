using System;
using System.Linq;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>종장·엔딩·인트로 컷 고르기(DL-10, FinalDialogue). 규칙은 docs/story/final.md·endings.md.</summary>
    public class FinalDialogueTests
    {
        readonly GameData _d = Load();

        Soul AfterPrologue()
        {
            var s = new Soul(_d);
            var life = s.BeginLife(Rebirth.Prologue(_d));
            s.EndLife(life.Finish("fallen", 0));
            return s;
        }

        LifeRecord Sealed(string sect) =>
            new LifeRecord(sect, "sealed", new RebirthQiLedger(_d.Cheonha.RebirthQi).Settle(0), Array.Empty<string>(), Array.Empty<string>(), "hyeongyeong", Array.Empty<string>());

        Soul SecondCycle()
        {
            var s = AfterPrologue();
            s.EndLife(Sealed("shaolin"));
            s.ChooseEnding(Soul.EndingSeal, false);
            return s;
        }

        [Fact]
        public void 인트로는_열두_컷_그대로()
        {
            var cuts = FinalDialogue.Intro(_d);
            Assert.Equal(_d.Dialogue.Intro.Cuts.Select(c => c.Id), cuts.Select(c => c.Id));
            Assert.Equal("sb_intro_01", cuts[0].Art);
        }

        [Fact]
        public void 입장_컷은_회차마다_처음_한_번()
        {
            var s = AfterPrologue();
            var p = new DialogueProgress();
            Assert.Equal(new[] { "enter1", "enter2" }, FinalDialogue.FinalEnter(_d, s, p).Select(c => c.Id));
            Assert.Empty(FinalDialogue.FinalEnter(_d, s, p));
            s.EndLife(Sealed("shaolin"));
            s.ChooseEnding(Soul.EndingSeal, false);   // 회귀 → 2회차
            Assert.Equal(2, FinalDialogue.FinalEnter(_d, s, p).Count);
        }

        [Fact]
        public void 혈영_일곱은_혈영_줄_그다음_단계_줄()
        {
            var s = AfterPrologue();
            var fight = new FinalFight(_d);
            foreach (var lord in _d.FinalBoss.Shadows)
            {
                var line = FinalDialogue.FinalOpponent(_d, s, fight)!;
                Assert.True(line.Shadow);
                Assert.Equal(_d.Dialogue.Lord(lord).EchoKo, line.Text);
                Assert.Equal($"dialogue.lords.list.{lord}.echo", line.Key);
                fight.Defeat();
            }
            foreach (var st in _d.FinalBoss.Stages)
            {
                var line = FinalDialogue.FinalOpponent(_d, s, fight)!;
                Assert.False(line.Shadow);
                Assert.Equal(_d.Dialogue.Final.Stage(st.Id).TextKo, line.Text);
                Assert.Equal($"dialogue.final.stages.{st.Id}.text", line.Key);
                Assert.Null(line.BloodMoon);   // 1회차
                fight.Defeat();
            }
            Assert.Null(FinalDialogue.FinalOpponent(_d, s, fight));
        }

        [Fact]
        public void 혈월_회차엔_혈의_단계에_혈월_줄을_더한다()
        {
            var s = SecondCycle();
            var fight = new FinalFight(_d);
            for (int i = 0; i < _d.FinalBoss.Shadows.Count; i++) fight.Defeat();
            var first = FinalDialogue.FinalOpponent(_d, s, fight)!;
            Assert.Equal("danmok_hyeon", first.Speaker);
            Assert.Equal(_d.Dialogue.Final.Stage("hyeoleui").BloodMoonKo, first.BloodMoon);
            Assert.Equal("dialogue.final.stages.hyeoleui.bloodMoon", first.BloodMoonKey);
            fight.Defeat();
            Assert.Null(FinalDialogue.FinalOpponent(_d, s, fight)!.BloodMoon);   // 혈해는 혈월 줄 없음
        }

        [Fact]
        public void 고르는_화면은_열린_엔딩만_차례대로()
        {
            var s = AfterPrologue();
            Assert.Empty(FinalDialogue.EndingChoices(_d, s, false));   // 종장 전
            s.EndLife(Sealed("shaolin"));
            Assert.Equal(new[] { "seal" }, FinalDialogue.EndingChoices(_d, s, false).Select(e => e.Id));
            s.ChooseEnding(Soul.EndingSeal, false);
            s.EndLife(Sealed("tang"));
            Assert.Equal(new[] { "seal", "inherit" }, FinalDialogue.EndingChoices(_d, s, false).Select(e => e.Id));   // 진엔딩 조건 못 채움
            Assert.Equal("계승", FinalDialogue.EndingChoices(_d, s, false)[1].NameKo);
        }

        [Fact]
        public void 엔딩_컷은_엔딩마다_통째로()
        {
            Assert.Equal(4, FinalDialogue.EndingCuts(_d, Soul.EndingSeal).Count);
            Assert.Equal(4, FinalDialogue.EndingCuts(_d, Soul.EndingInherit).Count);
            var t = FinalDialogue.EndingCuts(_d, Soul.EndingTrue);
            Assert.Equal(6, t.Count);
            Assert.Equal("mok", t[0].Lines[0].Speaker);
            Assert.Throws<InvalidOperationException>(() => FinalDialogue.EndingCuts(_d, "nope"));
        }
    }
}
