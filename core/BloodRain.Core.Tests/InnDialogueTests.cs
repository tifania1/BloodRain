using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Progression;
using BloodRain.Core.Run;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>객잔 대사 고르기(DL-03, Core/Meta/InnDialogue). 규칙은 docs/story 각 파일 '쓰는 법'.</summary>
    public class InnDialogueTests
    {
        readonly GameData _d = Load();
        CheonhaDef C => _d.Cheonha;
        DialogueDef D => _d.Dialogue;

        Soul AfterPrologue(DialogueProgress p)
        {
            var s = new Soul(_d);
            var rec = new LifeRecord(null, LifeEndWay.Fallen, new RebirthQiLedger(C.RebirthQi).Settle(0), Array.Empty<string>(),
                new[] { "cheongun" }, "ilryu", Array.Empty<string>());
            var r = InnDialogue.OnReturn(_d, s, rec, s.EndLife(rec), p, new ReturnFacts(), new Random(1));
            Assert.Empty(r.Mok);   // 서장 뒤에는 침방 첫 환혼 줄로 넘어간다
            Assert.Empty(r.Unnang);
            Assert.Null(r.Leader);
            return s;
        }

        InnReturn Live(Soul s, DialogueProgress p, string sect, string realm = "iryu", string[]? bosses = null,
            string end = LifeEndWay.Fallen, ReturnFacts? facts = null, int seed = 1)
        {
            var rec = new LifeRecord(sect, end, new RebirthQiLedger(C.RebirthQi).Settle(0), bosses ?? Array.Empty<string>(),
                new[] { C.Sect(sect).Region }, realm, Array.Empty<string>());
            return InnDialogue.OnReturn(_d, s, rec, s.EndLife(rec), p, facts ?? new ReturnFacts(), new Random(seed));
        }

        static string[] Ids(IEnumerable<DialogueLineDef> lines) => lines.Select(l => l.Id).ToArray();

        RebirthBody Body(string sect, bool female = false) =>
            Rebirth.Body(C, new RebirthCandidate(C.Sect(sect), C.Region(C.Sect(sect).Region), Array.Empty<BirthTraitDef>(), female));

        [Fact]
        public void 첫_귀환은_목_노인_첫_생_줄과_운낭_첫_귀환_줄()
        {
            var p = new DialogueProgress();
            var s = AfterPrologue(p);
            var r = Live(s, p, "shaolin");
            // 1번째 생 줄 + 처음 오른 경지(서장에서 이미 이류·일류에 올랐다)
            Assert.Equal(new[] { "realm_iryu", "realm_ilryu" }, Ids(r.Mok));
            Assert.Contains("mok.lives_1", p.Pending);   // 경지가 생 횟수보다 앞이라 밀렸다
            Assert.Equal(new[] { "return_1" }, Ids(r.Unnang));
            Assert.Null(r.Leader);

            var r2 = Live(s, p, "shaolin");
            Assert.Equal(new[] { "lives_1" }, Ids(r2.Mok));
            Assert.Equal(new[] { "return_2" }, Ids(r2.Unnang));
        }

        [Fact]
        public void 목_노인은_두_마디까지_땅의_주인_봉인석_경지_순으로_밀린_줄은_다음에()
        {
            var p = new DialogueProgress();
            var s = AfterPrologue(p);
            Live(s, p, "shaolin");   // 이류·일류
            Live(s, p, "shaolin");   // 1번째 생
            var r = Live(s, p, "shaolin", "jeoljeong", new[] { "songshan" });
            Assert.Equal(new[] { "lord_wongak", "stones_1" }, Ids(r.Mok));
            Assert.Contains("mok.realm_jeoljeong", p.Pending);
            var r2 = Live(s, p, "shaolin");
            Assert.Equal(new[] { "realm_jeoljeong", "lives_3" }, Ids(r2.Mok));
            var r3 = Live(s, p, "shaolin");
            Assert.Empty(r3.Mok);   // 다 했다. 한 번 한 줄은 다시 하지 않는다
        }

        [Fact]
        public void 의서_한_권에_목_노인_한_줄_일곱째를_보면_대화를_끝까지()
        {
            Assert.Equal(Enumerable.Range(1, C.Cycle.TrueEndingManuscripts).Select(i => "manuscript:" + i),
                D.Mok.Lines.Where(l => l.Kind == "manuscript").Select(l => l.When));
            Assert.Equal(D.Returns.MokOrder.IndexOf("lord") + 1, D.Returns.MokOrder.IndexOf("manuscript"));

            var p = new DialogueProgress();
            var s = AfterPrologue(p);
            Live(s, p, "shaolin");   // 이류·일류
            Live(s, p, "shaolin");   // 1번째 생
            InnReturn Book(int n) {
                var rec = new LifeRecord("shaolin", LifeEndWay.Fallen, new RebirthQiLedger(C.RebirthQi).Settle(0), Array.Empty<string>(),
                    new[] { C.Sect("shaolin").Region }, "iryu", Array.Empty<string>(), n);
                return InnDialogue.OnReturn(_d, s, rec, s.EndLife(rec), p, new ReturnFacts(), new Random(1));
            }
            Assert.Equal(new[] { "manuscript_1" }, Ids(Book(1).Mok).Where(id => id.StartsWith("manuscript")));
            Assert.False(s.MokTalkDone(p.MokManuscriptLinesSeen));
            Book(C.Cycle.TrueEndingManuscripts - 1);   // 나머지를 한 생에 다 모아도 한 번에 두 마디까지
            for (int i = 0; i < 5 && p.MokManuscriptLinesSeen < C.Cycle.TrueEndingManuscripts; i++) Live(s, p, "shaolin");
            Assert.Equal(C.Cycle.TrueEndingManuscripts, p.MokManuscriptLinesSeen);
            Assert.True(s.MokTalkDone(p.MokManuscriptLinesSeen));
        }

        [Fact]
        public void 현경은_봉인석이_모자란_생과_일곱을_지닌_생이_따로()
        {
            var p = new DialogueProgress();
            var s = AfterPrologue(p);
            Live(s, p, "shaolin", "hyeongyeong");
            for (int i = 0; i < 4; i++) Live(s, p, "shaolin");   // 밀린 경지 줄을 다 꺼낸다
            Assert.True(p.HasSeen("mok.realm_hyeongyeong_short"));
            Assert.False(p.HasSeen("mok.realm_hyeongyeong_sealed"));

            var all = C.BossRegions.Select(r => r.Id).ToArray();
            Live(s, p, "shaolin", "hyeongyeong", all);
            for (int i = 0; i < 12; i++) Live(s, p, "shaolin");
            Assert.True(p.HasSeen("mok.realm_hyeongyeong_sealed"));
            Assert.True(p.HasSeen("mok.stones_7"));
            Assert.Empty(p.Pending);
        }

        [Fact]
        public void 좌화는_처음_한_번_그다음은_되풀이_줄_그림자는_따로()
        {
            var p = new DialogueProgress();
            var s = AfterPrologue(p);
            Live(s, p, "shaolin"); Live(s, p, "shaolin");   // 첫 귀환 줄들
            var r = Live(s, p, "shaolin", end: LifeEndWay.Zuohwa);
            Assert.Contains("zuohwa_first", Ids(r.Mok));
            var r2 = Live(s, p, "shaolin", end: LifeEndWay.Zuohwa);
            Assert.Contains("zuohwa_repeat", Ids(r2.Mok));
            var r3 = Live(s, p, "shaolin", end: LifeEndWay.Zuohwa, facts: new ReturnFacts { ShadowChased = true });
            Assert.Contains("zuohwa_shadow", Ids(r3.Mok));
            var r4 = Live(s, p, "shaolin", end: LifeEndWay.Zuohwa);
            Assert.Contains("zuohwa_repeat", Ids(r4.Mok));
            Assert.DoesNotContain("mok.zuohwa_repeat", p.Pending);   // 되풀이 줄은 밀리지 않는다
        }

        [Fact]
        public void 혜공은_누구_몸이든_원각을_쓰러뜨리면_합류하고_객잔_줄을_차례로()
        {
            var p = new DialogueProgress();
            var s = AfterPrologue(p);
            var r = Live(s, p, "namgung", "jeoljeong", new[] { "songshan" });
            Assert.Equal("hyegong", r.Leader!.Leader);
            Assert.Equal("join", r.Leader.Slot);
            Assert.Equal(D.Leader("hyegong").Join, r.Leader.Text);
            Assert.True(p.HasJoined("hyegong"));
            // 남궁 몸으로 잡았으니 남궁현도 합류 대기다. 합류 줄이 객잔 줄보다 먼저.
            var r1 = Live(s, p, "shaolin");
            Assert.Equal(("namgung_hyeon", "join"), (r1.Leader!.Leader, r1.Leader.Slot));
            // 그 뒤로는 덜 말한 사람부터, 같으면 먼저 합류한 사람부터 객잔 줄을 차례로
            var order = new List<(string, string)>();
            for (int i = 0; i < 6; i++) { var ri = Live(s, p, "shaolin"); order.Add((ri.Leader!.Leader, ri.Leader.Slot)); }
            Assert.Equal(new[] { ("hyegong", "inn.0"), ("namgung_hyeon", "inn.0"), ("hyegong", "inn.1"), ("namgung_hyeon", "inn.1"),
                ("hyegong", "inn.2"), ("namgung_hyeon", "inn.2") }, order);
            Assert.Null(Live(s, p, "shaolin").Leader);   // 객잔 줄이 바닥나면 말이 없다
            Assert.Equal(D.Leader("hyegong").Inn[2], D.Leader("hyegong").Inn.Last());
        }

        [Fact]
        public void 객잔에_머무는_사람은_합류한_사람과_같다()
        {
            // QA-10b(2026-11-05 사람): 마당·대사 모두 DialogueProgress.Joined 하나로.
            var p = new DialogueProgress();
            var s = AfterPrologue(p);
            Assert.Empty(Inn.Residents(s, p));
            Live(s, p, "shaolin", "jeoljeong", new[] { "songshan", "huashan" });
            Assert.Equal(new[] { "hyegong" }, Inn.Residents(s, p));   // 화산은 대표 인물이 없다
            Assert.Equal(p.Joined, Inn.Residents(s, p));
            s.Regress();
            Assert.Empty(Inn.Residents(s, p));   // 회귀하면 기억을 잃어 객잔을 떠난다
            Live(s, p, "shaolin", "jeoljeong", new[] { "songshan" });
            Assert.Equal(new[] { "hyegong" }, Inn.Residents(s, p));
        }

        [Fact]
        public void 마칠과_남궁현은_그_문파_몸으로_땅의_주인을_잡아야_합류()
        {
            var p = new DialogueProgress();
            var s = AfterPrologue(p);
            Live(s, p, "shaolin", "jeoljeong", new[] { "songshan" });
            Assert.False(p.HasJoined("machil"));
            Assert.False(p.HasJoined("namgung_hyeon"));
            var r = Live(s, p, "namgung", "jeoljeong", new[] { "songshan" }, facts: new ReturnFacts { FemaleBody = true });
            Assert.True(p.HasJoined("namgung_hyeon"));
            Assert.False(p.HasJoined("machil"));
            Assert.Equal(("namgung_hyeon", "join"), (r.Leader!.Leader, r.Leader.Slot));
            Assert.False(r.Leader.Sister);   // 남궁현 합류 줄은 남녀 같은 말이라 사매판이 없다
        }

        [Fact]
        public void 합류_줄은_객잔_줄보다_먼저_여자_몸이면_사매판()
        {
            var p = new DialogueProgress();
            var s = AfterPrologue(p);
            var r = Live(s, p, "gaebang", "jeoljeong", new[] { "danggata" }, facts: new ReturnFacts { FemaleBody = true });
            // 당가타는 당연화(자기 땅) 합류, 개방 몸이라 마칠도 합류. 문파 순서(dialogue.json leaders)대로 마칠이 먼저.
            Assert.Equal("machil", r.Leader!.Leader);
            Assert.Equal("join", r.Leader.Slot);
            Assert.True(r.Leader.Sister);
            Assert.Equal(D.Leader("machil").Sister["join"], r.Leader.Text);
            var r2 = Live(s, p, "gaebang");
            Assert.Equal("dang_yeonhwa", r2.Leader!.Leader);
            Assert.Equal("join", r2.Leader.Slot);
            Assert.False(r2.Leader.Sister);   // 당가 몸이 아니니 사매판이 아니다
        }

        [Fact]
        public void 운낭은_마천패_뒤에_세_번에_걸쳐_털어놓고_셋째에_하오문_게시판이_열린다()
        {
            var p = new DialogueProgress();
            var s = AfterPrologue(p);
            Live(s, p, "shaolin"); Live(s, p, "shaolin");   // return_1, return_2
            var said = new List<string>();
            var opens = new List<string>();
            var r = Live(s, p, "shaolin", "jeoljeong", new[] { "janggang" });
            said.AddRange(Ids(r.Unnang)); opens.AddRange(r.Opens);
            for (int i = 0; i < 2; i++)
            {
                var ri = Live(s, p, "shaolin");
                said.AddRange(Ids(ri.Unnang)); opens.AddRange(ri.Opens);
            }
            Assert.Equal(new[] { "confess_1", "confess_2", "confess_3" }, said);
            Assert.Equal(new[] { "haomun_board" }, opens);
        }

        [Fact]
        public void 운낭은_할_말이_없으면_잡담을_연달아_같은_줄_없이()
        {
            var p = new DialogueProgress();
            var s = AfterPrologue(p);
            Live(s, p, "shaolin"); Live(s, p, "shaolin");
            string? last = null;
            for (int i = 0; i < 20; i++)
            {
                var r = Live(s, p, "shaolin", seed: i);
                var u = Assert.Single(r.Unnang);
                if (u.Kind == "lives") continue;
                Assert.Equal("chat:before", u.When);
                Assert.NotEqual(last, u.Id);
                last = u.Id;
            }
        }

        [Fact]
        public void 시설과_하오문_밀서는_게임이_알려_준_생에만()
        {
            var p = new DialogueProgress();
            var s = AfterPrologue(p);
            Live(s, p, "shaolin"); Live(s, p, "shaolin");
            var r = Live(s, p, "shaolin", facts: new ReturnFacts { NewFacilities = { "library" }, PickedHaomunLetter = true });
            Assert.Equal(new[] { "facility_jangyeonggak" }, Ids(r.Unnang));
            var r2 = Live(s, p, "shaolin");
            Assert.Equal(new[] { "letter_before" }, Ids(r2.Unnang));   // 밀린 줄
            Assert.DoesNotContain("unnang.letter_after", p.Pending);
        }

        [Fact]
        public void 마당_줄은_처음엔_1번_그_뒤로_무작위_합류_뒤엔_안_뒤_줄()
        {
            var p = new DialogueProgress();
            var s = AfterPrologue(p);
            var hye = D.Leader("hyegong");
            var first = InnDialogue.YardLine(_d, s, Body("shaolin"), p, new Random(3))!;
            Assert.Equal("yardUnknown.0", first.Slot);
            Assert.Equal(hye.YardUnknown[0], first.Text);
            var seen = new HashSet<string>();
            for (int i = 0; i < 40; i++) seen.Add(InnDialogue.YardLine(_d, s, Body("shaolin"), p, new Random(i))!.Slot);
            Assert.Equal(hye.YardUnknown.Count, seen.Count);   // 셋 다 나온다

            Live(s, p, "shaolin", "jeoljeong", new[] { "songshan" });
            var known = InnDialogue.YardLine(_d, s, Body("shaolin"), p, new Random(1))!;
            Assert.Equal("yardKnown.0", known.Slot);
            Assert.Null(InnDialogue.YardLine(_d, s, Body("huashan"), p, new Random(1)));   // 화산은 대표 인물이 없다
        }

        [Fact]
        public void 여자_몸이면_사매판이_있는_줄만_바꿔_쓴다()
        {
            var p = new DialogueProgress();
            var s = AfterPrologue(p);
            var nam = D.Leader("namgung_hyeon");
            var first = InnDialogue.YardLine(_d, s, Body("namgung", female: true), p, new Random(1))!;
            Assert.True(first.Sister);
            Assert.Equal(nam.Sister["yardUnknown.0"], first.Text);
            for (int i = 0; i < 30; i++)
            {
                var l = InnDialogue.YardLine(_d, s, Body("namgung", female: true), p, new Random(i))!;
                Assert.Equal(nam.Sister.ContainsKey(l.Slot), l.Sister);
                if (l.Slot == "yardUnknown.2") Assert.Equal(nam.YardUnknown[2], l.Text);   // 사매판이 없는 줄은 그대로
            }
            Assert.False(InnDialogue.YardLine(_d, s, Body("namgung"), p, new Random(1))!.Sister);
        }

        [Fact]
        public void 혈월_회차엔_합류가_풀리고_그_문파로_처음_태어나면_혈월_줄()
        {
            var p = new DialogueProgress();
            var s = AfterPrologue(p);
            Live(s, p, "shaolin", "jeoljeong", new[] { "songshan" });
            Assert.True(p.HasJoined("hyegong"));
            s.Regress();

            var bm = InnDialogue.YardLine(_d, s, Body("shaolin"), p, new Random(1))!;
            Assert.Equal("yardBloodMoon", bm.Slot);
            Assert.False(p.HasJoined("hyegong"));   // 기억을 잃었다
            Assert.StartsWith("yardUnknown.", InnDialogue.YardLine(_d, s, Body("shaolin"), p, new Random(1))!.Slot);

            // 당가처럼 모를 때 줄이 없는 문파는 혈월 줄을 되풀이한다(1회차에 당소혜를 잡아 열어 둔다)
            var p2 = new DialogueProgress();
            var s2 = AfterPrologue(p2);
            Live(s2, p2, "shaolin", "jeoljeong", new[] { "danggata" });
            s2.Regress();
            for (int i = 0; i < 3; i++)
                Assert.Equal("yardBloodMoon", InnDialogue.YardLine(_d, s2, Body("tang"), p2, new Random(i))!.Slot);

            // 이번 회차에 봉인석을 다시 가져오면 다시 합류
            Live(s, p, "shaolin", "jeoljeong", new[] { "songshan" });
            Assert.True(p.HasJoined("hyegong"));
        }

        [Fact]
        public void 연무장_줄은_조건마다_한_번()
        {
            var p = new DialogueProgress();
            Assert.Equal(D.Training.Lines[0].TextKo, InnDialogue.Training(_d, p, "open")!.TextKo);
            Assert.Null(InnDialogue.Training(_d, p, "open"));
            Assert.NotNull(InnDialogue.Training(_d, p, "first_buy"));
            Assert.Null(InnDialogue.Training(_d, p, "nope"));
        }

        [Fact]
        public void 진행은_저장했다_읽어도_같다()
        {
            var p = new DialogueProgress();
            var s = AfterPrologue(p);
            Live(s, p, "gaebang", "jeoljeong", new[] { "songshan", "danggata" });
            Live(s, p, "gaebang");
            InnDialogue.YardLine(_d, s, Body("shaolin"), p, new Random(1));
            var json = p.ToJson();
            var q = DialogueProgress.FromJson(json);
            Assert.Equal(json, q.ToJson());
            Assert.Equal(p.Joined, q.Joined);
            Assert.Equal(p.Pending, q.Pending);
            Assert.Throws<NotSupportedException>(() => DialogueProgress.FromJson("{\"Version\": 99}"));
        }

        [Fact]
        public void 은거기인을_만난_생마다_목_노인_은거기인_줄을_차례로_하나씩()
        {
            var p = new DialogueProgress();
            var s = AfterPrologue(p);
            var said = new List<string>();
            for (int i = 0; i < 10; i++)
                said.AddRange(Ids(Live(s, p, "shaolin", facts: new ReturnFacts { MetHermit = i < 5 }).Mok).Where(id => id.StartsWith("hermit_")));
            Assert.Equal(new[] { "hermit_1", "hermit_2", "hermit_3" }, said);   // 다섯 번 만나도 셋까지, 한 번씩

            var p2 = new DialogueProgress();
            var s2 = AfterPrologue(p2);
            for (int i = 0; i < 6; i++)
                Assert.DoesNotContain(Ids(Live(s2, p2, "shaolin").Mok), id => id.StartsWith("hermit_"));   // 안 만나면 없다
        }
    }
}
