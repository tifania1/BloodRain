using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Progression;
using BloodRain.Core.Run;
using Newtonsoft.Json;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>침방 대사 고르기(DL-04, Core/Meta/InnDialogue BedroomTalk). 규칙은 docs/story/inn_screens.md '침방', mok.md 첫 환혼·문파 첫 환생.</summary>
    public class BedroomTalkTests
    {
        readonly GameData _d = Load();
        CheonhaDef C => _d.Cheonha;
        DialogueDef D => _d.Dialogue;
        List<string> AllSects => C.Sects.Select(s => s.Id).ToList();

        static string[] Ids(IEnumerable<DialogueLineDef> lines) => lines.Select(l => l.Id).ToArray();

        /// <summary>본 줄을 미리 채운 진행(저장 파일로 만든다).</summary>
        static DialogueProgress With(params string[] seen) =>
            DialogueProgress.FromJson(JsonConvert.SerializeObject(new { Version = 1, Seen = seen }));

        DialogueProgress AllSectLinesSeen(params string[] more) =>
            With(AllSects.Select(s => "bedroom.sect_" + s).Concat(more).ToArray());

        /// <summary>조건에 맞는 후보가 나오는 시드를 찾아 침방을 연다(Open·Show 까지). index 는 그 후보 자리.</summary>
        (BedroomTalk talk, int index, IReadOnlyList<DialogueLineDef> shown) Visit(DialogueProgress p, Func<RebirthCandidate, bool> want,
            IEnumerable<string>? sects = null, int count = 3, int rerolls = 0, int start = 0)
        {
            var list = (sects ?? AllSects).ToList();
            for (int seed = start; seed < start + 2000; seed++)
            {
                var draw = new RebirthDraw(C, list, count, rerolls, first: false, new Random(seed));
                int i = draw.Candidates.ToList().FindIndex(c => want(c));
                if (i < 0) continue;
                var talk = InnDialogue.Bedroom(_d, draw, p, new Random(seed));
                Assert.Empty(talk.Open());
                var shown = talk.Show();
                return (talk, i, shown);
            }
            throw new InvalidOperationException("맞는 후보가 나오는 시드가 없습니다");
        }

        [Fact]
        public void 첫_환혼은_네_줄과_고른_문파_줄을_차례로()
        {
            var p = new DialogueProgress();
            var draw = new RebirthDraw(C, AllSects, 3, 0, first: true, new Random(5));
            var talk = InnDialogue.Bedroom(_d, draw, p, new Random(5));
            Assert.Equal(new[] { "first_open" }, Ids(talk.Open()));
            Assert.Equal(new[] { "first_candidates" }, Ids(talk.Show()));
            int i = draw.Candidates.ToList().FindIndex(c => c.Sect.Id == "gaebang");
            // 문파 줄 → 침 줄. 여자 몸·성질·무작위 줄은 첫 환혼 줄에 막힌다.
            Assert.Equal(new[] { "sect_gaebang", "first_needle" }, Ids(talk.Pick(i)));
            Assert.Equal(new[] { "first_send" }, Ids(talk.Send()));
            Assert.NotNull(talk.Chosen);
            Assert.Equal("gaebang", talk.Chosen!.Sect!.Id);
            Assert.True(p.HasSeen("bedroom.sect_gaebang"));
            Assert.False(p.HasSeen("bedroom.female_first"));
        }

        [Fact]
        public void 문파_첫_환생_줄은_늘_나오고_그_침방엔_다른_줄이_없다()
        {
            var p = With("bedroom.sect_shaolin");
            // 여자 몸에 성질까지 붙은 처음 고르는 문파 후보
            var (talk, i, shown) = Visit(p, c => c.Sect.Id != "shaolin" && c.Female && c.Traits.Count > 0);
            Assert.Empty(shown);
            var sect = talk.Draw.Candidates[i].Sect.Id;
            Assert.Equal(new[] { "sect_" + sect }, Ids(talk.Pick(i)));
            Assert.False(p.HasSeen("bedroom.female_first"));   // 자리가 없어 못 한 줄은 다음에
            Assert.False(p.HasSeen("bedroom.trait_first"));
            Assert.Empty(talk.Send());
        }

        [Fact]
        public void 여자_몸_줄은_처음_한_번_자리가_없었으면_다음에()
        {
            var p = AllSectLinesSeen();
            var (talk, i, _) = Visit(p, c => c.Female && c.Traits.Count > 0);
            Assert.Equal(new[] { "female_first" }, Ids(talk.Pick(i)));   // 여자 몸이 성질보다 먼저, 한 마디만

            var (talk2, j, _) = Visit(p, c => c.Female && c.Traits.Count == 0);
            var said = talk2.Pick(j);
            Assert.Single(said);
            Assert.Equal("show", said[0].Kind);   // 여자 몸 줄은 다시 안 하고 무작위 한 마디
        }

        [Fact]
        public void 성질_줄은_소개_먼저_그다음_그_성질_줄()
        {
            var p = AllSectLinesSeen("bedroom.female_first");
            var (t1, i1, _) = Visit(p, c => c.Traits.Any(t => t.Id == "yakgol"));
            Assert.Equal(new[] { "trait_first" }, Ids(t1.Pick(i1)));
            var (t2, i2, _) = Visit(p, c => c.Traits.Any(t => t.Id == "yakgol"));
            Assert.Equal(new[] { "trait_yakgol" }, Ids(t2.Pick(i2)));
            var (t3, i3, _) = Visit(p, c => c.Traits.Count == 1 && c.Traits[0].Id == "yakgol");
            Assert.Equal("show", t3.Pick(i3).Single().Kind);   // 한 번 한 성질 줄은 다시 안 한다
        }

        [Fact]
        public void 무작위_줄은_고른_뒤_다른_줄이_없을_때_셋을_전제한_줄은_후보가_셋일_때만()
        {
            var p = AllSectLinesSeen("bedroom.female_first", "bedroom.trait_first", "bedroom.fourth_first");
            var seen3 = new HashSet<string>();
            var seen4 = new HashSet<string>();
            string? last = null;
            for (int n = 0; n < 40; n++)
            {
                var (t, i, shown) = Visit(p, c => !c.Female && c.Traits.Count == 0, count: 3 + n % 2, start: n * 101);
                Assert.Empty(shown);   // 보여 줄 때는 무작위 줄을 하지 않는다
                var said = t.Pick(i).Single();
                Assert.Equal("show", said.Kind);
                (n % 2 == 0 ? seen3 : seen4).Add(said.Id);
                if (n % 2 == 0 && last != null) Assert.NotEqual(last, said.Id);   // 셋 가운데서는 연달아 같은 줄 없음
                if (n % 2 == 0) last = said.Id;
                else last = "show_2";
            }
            Assert.Equal(new[] { "show_1", "show_2", "show_3" }, seen3.OrderBy(x => x));
            Assert.Equal(new[] { "show_2" }, seen4);   // '셋이다', '둘 얼굴' 줄은 넷일 때 안 쓴다
        }

        [Fact]
        public void 넷째_몸을_산_뒤_첫_침방은_보여_줄_때_넷_줄_그_침방은_그걸로_끝()
        {
            var p = AllSectLinesSeen();
            var (t, i, shown) = Visit(p, c => c.Female, count: 4);
            Assert.Equal(new[] { "fourth_first" }, Ids(shown));
            Assert.Empty(t.Pick(i));   // 한 번 환혼에 한 마디
            var (_, _, shown2) = Visit(p, c => true, count: 4);
            Assert.Empty(shown2);
        }

        [Fact]
        public void 다시_고르기는_처음_한_번_그리고_한_침방에_셋째일_때()
        {
            var p = AllSectLinesSeen();
            var (t, _, _) = Visit(p, c => true, rerolls: 3);
            Assert.Equal(new[] { "reroll_first" }, Ids(t.Reroll()));
            Assert.Empty(t.Reroll());
            Assert.Empty(t.Reroll());   // 셋째지만 이번 침방은 이미 한 마디 했다
            Assert.Equal(3, t.Rerolls);
            Assert.Empty(t.Pick(0));

            var (t2, _, _) = Visit(p, c => true, rerolls: 3);
            Assert.Empty(t2.Reroll());
            Assert.Empty(t2.Reroll());
            Assert.Equal(new[] { "reroll_many" }, Ids(t2.Reroll()));
            Assert.Throws<InvalidOperationException>(() => t2.Reroll());   // 남은 횟수 없음
        }

        [Fact]
        public void 같은_문파가_두_장이면_그_줄()
        {
            var p = AllSectLinesSeen("bedroom.reroll_first");
            var two = new[] { "shaolin", "gaebang" };
            var (t, _, shown) = Visit(p, c => true, sects: two, rerolls: 1);
            Assert.Equal(new[] { "same_sect" }, Ids(shown));
            Assert.Empty(t.Reroll());   // 한 마디 했으니 다시 고른 뒤 겹쳐도 없음
        }

        [Fact]
        public void 문파_첫_환생_줄은_보여_줄_때_한_마디가_있어도_나온다()
        {
            var p = With("bedroom.sect_shaolin");
            var (t, i, shown) = Visit(p, c => c.Sect.Id == "gaebang", sects: new[] { "shaolin", "gaebang" });
            Assert.Equal(new[] { "same_sect" }, Ids(shown));
            Assert.Equal(new[] { "sect_gaebang" }, Ids(t.Pick(i)));
        }

        [Fact]
        public void 순서가_어긋나면_예외()
        {
            var p = new DialogueProgress();
            var draw = new RebirthDraw(C, AllSects, 3, 1, first: false, new Random(1));
            var t = InnDialogue.Bedroom(_d, draw, p, new Random(1));
            Assert.Throws<InvalidOperationException>(() => t.Show());
            Assert.Throws<InvalidOperationException>(() => t.Pick(0));
            t.Open();
            Assert.Throws<InvalidOperationException>(() => t.Reroll());
            t.Show();
            Assert.Throws<InvalidOperationException>(() => t.Send());
            t.Pick(0);
            Assert.Throws<InvalidOperationException>(() => t.Pick(1));
            Assert.Throws<InvalidOperationException>(() => t.Reroll());
            t.Send();
            Assert.Throws<InvalidOperationException>(() => t.Send());
        }

        [Fact]
        public void 카드_글은_같은_문파면_서로_다른_묘사_여자_몸이면_여자_칸()
        {
            var two = new[] { "gaebang", "namgung" };
            for (int seed = 0; seed < 200; seed++)
            {
                var draw = new RebirthDraw(C, two, 4, 0, first: false, new Random(seed));
                var cards = BedroomTalk.CardTexts(D, draw.Candidates, new Random(seed));
                Assert.Equal(draw.Candidates.Count, cards.Count);
                foreach (var g in cards.GroupBy(c => c.Sect))
                    Assert.Equal(g.Count(), g.Select(c => c.BodyIndex).Distinct().Count());
                for (int i = 0; i < cards.Count; i++)
                {
                    var cand = draw.Candidates[i];
                    var card = D.Card(cand.Sect.Id);
                    Assert.Equal(cand.Sect.Id, cards[i].Sect);
                    Assert.Equal(card.IntroKo, cards[i].Intro);
                    var expect = cand.Female ? card.FemaleBodiesKo[cards[i].BodyIndex] : card.BodiesKo[cards[i].BodyIndex];
                    Assert.Equal(expect, cards[i].Body);
                }
            }
        }

        [Fact]
        public void 카드_글_키는_현지화_목록에_있고_다시_고르면_새로_뽑는다()
        {
            var cat = StringCatalog.FromData(ReadFiles(), ReadText(Path.Combine(DataDir, "strings", "ko.json")));
            var p = AllSectLinesSeen();
            var (t, _, _) = Visit(p, c => c.Female, rerolls: 1);
            foreach (var c in t.Cards)
            {
                Assert.Equal(c.Intro, cat.Korean[c.IntroKey]);
                Assert.Equal(c.Body, cat.Korean[c.BodyKey]);
            }
            var before = t.Draw.Candidates;
            t.Reroll();
            Assert.Equal(t.Draw.Candidates.Select(c => c.Sect.Id), t.Cards.Select(c => c.Sect));
            Assert.NotSame(before, t.Draw.Candidates);
        }

        [Fact]
        public void 침방_무작위_줄_기억은_저장된다()
        {
            var p = AllSectLinesSeen("bedroom.female_first", "bedroom.trait_first");
            var (t, i, _) = Visit(p, c => !c.Female && c.Traits.Count == 0);
            var said = t.Pick(i).Single();
            var back = DialogueProgress.FromJson(p.ToJson());
            Assert.Equal(said.Id, back.LastBedroomChat);
            Assert.True(back.HasSeen("bedroom.sect_shaolin"));
        }

        [Fact]
        public void 소울로_연_첫_환혼도_같다()
        {
            var p = new DialogueProgress();
            var s = new Soul(_d);
            var rec = new LifeRecord(null, LifeEndWay.Fallen, new RebirthQiLedger(C.RebirthQi).Settle(0), Array.Empty<string>(),
                new[] { "cheongun" }, "ilryu", Array.Empty<string>());
            s.EndLife(rec);
            var t = InnDialogue.Bedroom(_d, s.OpenRebirth(new Random(2)), p, new Random(2));
            Assert.True(t.Draw.IsFirst);
            Assert.Equal(new[] { "first_open" }, Ids(t.Open()));
        }
    }
}
