using System;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Progression;
using BloodRain.Core.Run;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>땅의 주인 컷 고르기(DL-06, InnDialogue.LordMeet·LordKill). 규칙은 docs/story/boss_cuts.md.</summary>
    public class LordCutTests
    {
        readonly GameData _d = Load();
        CheonhaDef C => _d.Cheonha;

        Soul AfterPrologue()
        {
            var s = new Soul(_d);
            s.EndLife(new LifeRecord(null, LifeEndWay.Fallen, new RebirthQiLedger(C.RebirthQi).Settle(0), Array.Empty<string>(),
                new[] { "cheongun" }, "ilryu", Array.Empty<string>()));
            return s;
        }

        LifeLog Begin(Soul s, string sect = "shaolin") =>
            s.BeginLife(Rebirth.Body(C, new RebirthCandidate(C.Sect(sect), C.Region(C.Sect(sect).Region), Array.Empty<BirthTraitDef>())));

        static void End(Soul s, LifeLog log) => s.EndLife(log.Finish(LifeEndWay.Fallen, 0));

        [Fact]
        public void 만남_처치_컷과_한_줄의_현지화_키가_모두_목록에_있다()
        {
            // DL-06b Unity 가 Strings.Get(키, 한국어) 로 띄운다(2026-11-11)
            var cat = StringCatalog.FromData(ReadFiles(), ReadText(System.IO.Path.Combine(DataDir, "strings", "ko.json")));
            void Has(string? key, string ko)
            {
                Assert.NotNull(key);
                Assert.True(cat.Korean.TryGetValue(key!, out var v), key + " 가 목록에 없다");
                Assert.Equal(ko, v);
            }
            void Cuts(System.Collections.Generic.IReadOnlyList<LordCut> cuts)
            {
                foreach (var c in cuts)
                    for (int n = 0; n < c.Lines.Count; n++) Has(c.LineKey(n), c.Lines[n].TextKo);
            }
            int alts = 0;
            foreach (var r in C.Regions.Where(r => r.Boss != null && r.Id != "bongmagok"))
            {
                var s = AfterPrologue();
                var p = new DialogueProgress();
                var log = Begin(s);
                foreach (var other in C.Regions.Where(o => o.Boss != null && o.Id != r.Id && o.Id != "bongmagok")) log.KillBoss(other.Id); // alt 컷 조건(stone:…)이 서게
                var e = InnDialogue.LordMeet(_d, s, log, r.Id, p);
                Assert.NotEmpty(e.Cuts);
                Cuts(e.Cuts);
                var lord = _d.Dialogue.Lord(r.Boss!);
                Has(e.Phase2Key, lord.Phase2Ko);
                Has(e.EchoKey, lord.EchoKo);
                if (lord.BloodMoonKo != null) Has(e.BloodMoonKey, lord.BloodMoonKo);
                var kill = InnDialogue.LordKill(_d, log, log.KillBoss(r.Id), p);
                Assert.NotEmpty(kill);
                Cuts(kill);
                alts += kill.Count(c => c.Alt);
            }
            Assert.Equal(2, alts);   // 혈허진인·낙매검선의 검혼 kill3
        }

        [Fact]
        public void 처음_만나면_만남_두_컷_다시_만나면_컷_없이_2막_줄만()
        {
            var s = AfterPrologue();
            var p = new DialogueProgress();
            var log = Begin(s);
            var e = InnDialogue.LordMeet(_d, s, log, "songshan", p);
            Assert.Equal("wongak", e.Lord);
            Assert.Equal(new[] { "meet1", "meet2" }, e.Cuts.Select(c => c.Id));
            Assert.Null(e.BloodMoon);
            Assert.Null(e.Echo);
            Assert.Equal("물러서라… 이 빛은 내가 아니다!", e.Phase2);
            End(s, log);   // 못 이기고 쓰러짐

            var e2 = InnDialogue.LordMeet(_d, s, Begin(s), "songshan", p);
            Assert.Empty(e2.Cuts);
            Assert.Null(e2.Echo);
            Assert.Equal(e.Phase2, e2.Phase2);
        }

        [Fact]
        public void 계정_첫_처치에_처치_네_컷_그_뒤_생에는_혈영_줄()
        {
            var s = AfterPrologue();
            var p = new DialogueProgress();
            var log = Begin(s);
            InnDialogue.LordMeet(_d, s, log, "songshan", p);
            var cuts = InnDialogue.LordKill(_d, log, log.KillBoss("songshan"), p);
            Assert.Equal(new[] { "kill1", "kill2", "kill3", "kill4" }, cuts.Select(c => c.Id));
            Assert.Empty(cuts.Single(c => c.Id == "kill3").Lines);   // 천추석 조각만 보이는 컷
            End(s, log);

            var log2 = Begin(s);
            var e = InnDialogue.LordMeet(_d, s, log2, "songshan", p);
            Assert.Empty(e.Cuts);
            Assert.Equal("봉인은… 안에서…", e.Echo);
            Assert.Empty(InnDialogue.LordKill(_d, log2, log2.KillBoss("songshan"), p));   // 혈영 처치는 컷 없음
        }

        [Fact]
        public void 혈월_회차_첫_만남은_컷과_혈월_줄_처치_컷은_다시_안_나온다()
        {
            var s = AfterPrologue();
            var p = new DialogueProgress();
            var log = Begin(s);
            InnDialogue.LordMeet(_d, s, log, "songshan", p);
            InnDialogue.LordKill(_d, log, log.KillBoss("songshan"), p);
            End(s, log);
            s.Regress();

            var log2 = Begin(s);
            var e = InnDialogue.LordMeet(_d, s, log2, "songshan", p);   // 봉인석이 비었으니 혈영이 아니다
            Assert.Equal(new[] { "meet1", "meet2" }, e.Cuts.Select(c => c.Id));
            Assert.Equal("이 마당에 전에도 선 적이 있느냐. 범종이 그리 우는구나.", e.BloodMoon);
            Assert.Null(e.Echo);
            Assert.Empty(InnDialogue.LordKill(_d, log2, log2.KillBoss("songshan"), p));
            Assert.NotNull(InnDialogue.LordMeet(_d, s, log2, "danggata", p).BloodMoon);   // 1회차에 못 만난 땅도 혈월 줄
        }

        [Fact]
        public void 무당과_화산은_먼저_해방한_쪽_봉인석으로_자막을_바꾼다()
        {
            var s = AfterPrologue();
            var p = new DialogueProgress();
            var log = Begin(s);
            var wudang = InnDialogue.LordKill(_d, log, log.KillBoss("wudangsan"), p);
            Assert.False(wudang.Single(c => c.Id == "kill3").Alt);   // 화산 봉인석이 아직 없다
            var huashan = InnDialogue.LordKill(_d, log, log.KillBoss("huashan"), p);   // 같은 생에 무당 다음
            var k3 = huashan.Single(c => c.Id == "kill3");
            Assert.True(k3.Alt);
            Assert.Equal("혈마가 본래 의원이었다면, 고치라는 말도 빈말이 아니었다.", k3.Lines.Single().TextKo);
        }

        [Fact]
        public void 땅의_주인이_없는_지역은_예외()
        {
            var s = AfterPrologue();
            Assert.Throws<ArgumentException>(() => InnDialogue.LordMeet(_d, s, Begin(s), "cheongun", new DialogueProgress()));
        }
    }
}
