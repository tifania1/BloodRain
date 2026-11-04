using System.Collections.Generic;
using System.IO;
using System.Linq;
using BloodRain.Core.Data;
using Newtonsoft.Json.Linq;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>dialogue.json(DL-02)이 대사 원문(docs/story)과 같고, 조건이 가리키는 경지·문파·땅의 주인·성질이 데이터에 있는지.</summary>
    public class DialogueDataTests
    {
        static DialogueDef D => Load().Dialogue;

        static string StoryText() =>
            string.Join("\n", Directory.GetFiles(Path.Combine(RepoRoot, "docs", "story"), "*.md").OrderBy(f => f).Select(ReadText));

        static IEnumerable<DialogueLineDef> AllLines() =>
            D.Prologue.Lines.Concat(D.Mok.Lines).Concat(D.Bedroom.Lines).Concat(D.Unnang.Lines).Concat(D.Training.Lines).Concat(D.Settlement.Lines);

        static IEnumerable<CutDef> AllCuts() => D.Intro.Cuts.Concat(D.Prologue.Cuts).Concat(D.Lords.List.SelectMany(l => l.Cuts))
            .Concat(D.Final.Enter.Cuts).Concat(D.Endings.List.SelectMany(e => e.Cuts));

        [Fact]
        public void 모든_문장이_대사_문서에_그대로_있다()
        {
            var story = StoryText();
            var json = JToken.Parse(File.ReadAllText(Path.Combine(DataDir, "dialogue.json")));
            var missing = new List<string>();
            foreach (var v in json.SelectTokens("$..*").OfType<JValue>().Where(v => v.Type == JTokenType.String))
            {
                var prop = (v.Parent as JProperty)?.Name;
                if (prop == null) prop = ((v.Parent?.Parent) as JProperty)?.Name;   // 문자열 배열(마당 줄)
                if (prop == null || prop.StartsWith("_") || prop == "nameKo" || prop == "when" || prop == "id") continue;
                var parentProp = (v.Parent as JProperty)?.Parent?.Parent as JProperty;
                bool text = prop.EndsWith("Ko") || parentProp?.Name == "sister" || (v.Parent is JArray && (v.Parent.Parent as JProperty)?.Name is "yardUnknown" or "yardKnown" or "inn")
                            || prop is "yardBloodMoon" or "join";
                if (!text) continue;
                var s = (string)v!;
                if (!story.Contains(s)) missing.Add($"{v.Path}: {s}");
            }
            Assert.True(missing.Count == 0, "docs/story 에 없는 문장(문서와 JSON 을 같이 고친다):\n" + string.Join("\n", missing));
        }

        [Fact]
        public void 말하는_사람은_목록에_있다()
        {
            var ids = D.Speakers.Select(s => s.Id).ToHashSet();
            Assert.Equal(D.Speakers.Count, ids.Count);
            foreach (var l in D.Prologue.Lines) Assert.Contains(l.Speaker!, ids);
            foreach (var c in AllCuts())
                foreach (var line in c.Lines.Concat(c.Alt?.Lines ?? new List<CutLineDef>()))
                    if (line.Speaker != null) Assert.Contains(line.Speaker, ids);
        }

        [Fact]
        public void 줄_id_는_묶음_안에서_겹치지_않는다()
        {
            foreach (var group in new[] { D.Prologue.Lines, D.Mok.Lines, D.Bedroom.Lines, D.Unnang.Lines, D.Training.Lines, D.Settlement.Lines })
                Assert.Equal(group.Count, group.Select(l => l.Id).Distinct().Count());
        }

        [Fact]
        public void 조건이_가리키는_것이_데이터에_있다()
        {
            var d = Load();
            var realms = d.Balance.Realms.Select(r => r.Id).ToHashSet();
            var sects = d.Cheonha.Sects.Select(s => s.Id).ToHashSet();
            var lords = d.Cheonha.BossRegions.Select(r => r.Boss!).ToHashSet();
            var stoneRegions = d.Cheonha.Regions.Where(r => r.SealStone != null).Select(r => r.Id).ToHashSet();
            var traits = d.Cheonha.BirthTraits.List.Select(t => t.Id).ToHashSet();
            var enemies = d.Enemies.Select(e => e.Id).ToHashSet();
            int stoneCount = d.Cheonha.Bosses.FinalRequires.SealStones;

            foreach (var l in AllLines())
            {
                var a = l.Args;
                switch (l.Kind)
                {
                    case "realm": Assert.Contains(a[0], realms); break;
                    case "sect": Assert.Contains(a[0], sects); break;
                    case "lord": Assert.Contains(a[0], lords); break;
                    case "stones": Assert.InRange(int.Parse(a[0]), 1, stoneCount); break;
                    case "elite_spawn":
                    case "elite_kill": Assert.Contains(a[0], enemies); break;
                    case "pick" when a.Length == 2 && a[0] == "trait": Assert.Contains(a[1], traits); break;
                }
            }
            foreach (var c in AllCuts().Where(c => c.Alt != null))
            {
                var w = c.Alt!.When.Split(':');
                Assert.Equal("stone", w[0]);
                Assert.Contains(w[1], stoneRegions);
            }
            // 현경 둘은 봉인석 수로 갈린다(mok.md).
            Assert.Contains(D.Mok.Lines, l => l.When == "realm:hyeongyeong:short");
            Assert.Contains(D.Mok.Lines, l => l.When == "realm:hyeongyeong:sealed");
            Assert.DoesNotContain(D.Mok.Lines, l => l.When == "realm:samryu");   // 삼류는 처음부터라 줄이 없다
        }

        [Fact]
        public void 귀환_갈래는_순서표에_있다()
        {
            Assert.All(D.Mok.Lines, l => Assert.Contains(l.Kind, D.Returns.MokOrder));
            Assert.All(D.Unnang.Lines, l => Assert.Contains(l.Kind, D.Returns.UnnangOrder));
            Assert.True(D.Returns.MokMax >= 1 && D.Returns.UnnangMax >= 1 && D.Returns.LeaderMax >= 1);
        }

        [Fact]
        public void 봉인석_줄은_하나부터_일곱까지_하나씩()
        {
            int n = Load().Cheonha.Bosses.FinalRequires.SealStones;
            Assert.Equal(Enumerable.Range(1, n).Select(i => "stones:" + i), D.Mok.Lines.Where(l => l.Kind == "stones").Select(l => l.When));
        }

        [Fact]
        public void 처음_오른_경지는_삼류_다음부터_다_있다()
        {
            var realms = Load().Balance.Realms.Select(r => r.Id).Skip(1).ToList();
            var lines = D.Mok.Lines.Where(l => l.Kind == "realm").Select(l => l.Args[0]).Distinct().ToList();
            Assert.Equal(realms, lines);
        }

        [Fact]
        public void 문파마다_첫_환생_줄이_있다()
        {
            var sects = Load().Cheonha.Sects.Select(s => s.Id);
            Assert.Equal(sects, D.Bedroom.Lines.Where(l => l.Kind == "sect").Select(l => l.Args[0]));
        }

        [Fact]
        public void 문파마다_후보_카드_글이_있고_여자_몸_칸은_남자만_문파에_없다()
        {
            var d = Load();
            Assert.Equal(d.Cheonha.Sects.Select(s => s.Id), D.Cards.List.Select(c => c.Id));
            foreach (var c in D.Cards.List)
            {
                Assert.False(string.IsNullOrEmpty(c.IntroKo));
                Assert.Equal(3, c.BodiesKo.Count);
                bool maleOnly = d.Cheonha.Rebirth.MaleOnlySects.Contains(c.Id);
                Assert.Equal(maleOnly ? 0 : c.BodiesKo.Count, c.FemaleBodiesKo.Count);
            }
        }

        [Fact]
        public void 침방_줄_갈래는_고르는_코드가_아는_것만()
        {
            var known = new[] { "first", "sect", "show", "reroll", "fourth", "same_sect", "pick" };
            Assert.All(D.Bedroom.Lines, l => Assert.Contains(l.Kind, known));
            Assert.Equal(new[] { "open", "candidates", "needle", "send" }, D.Bedroom.Lines.Where(l => l.Kind == "first").Select(l => l.Args[0]));
            Assert.All(D.Bedroom.Lines.Where(l => l.Kind == "show"), l => Assert.True(l.Repeat));
        }

        [Fact]
        public void 대표_인물과_땅의_주인이_빠짐없다()
        {
            var d = Load();
            var leaders = d.Cheonha.Sects.Where(s => s.Leader != null).Select(s => s.Leader!).ToList();
            Assert.Equal(leaders.OrderBy(x => x), D.Leaders.List.Select(l => l.Id).OrderBy(x => x));
            foreach (var l in D.Leaders.List)
            {
                Assert.Equal(d.Cheonha.Sects.First(s => s.Leader == l.Id).Id, l.Sect);
                Assert.NotEmpty(l.YardKnown);
                Assert.False(string.IsNullOrEmpty(l.YardBloodMoon));
                Assert.False(string.IsNullOrEmpty(l.Join));
                Assert.Equal(3, l.Inn.Count);
                // 1회차에 처음부터 '안 뒤'로 만나는 문파(땅의 주인을 쓰러뜨려야 열림)는 모를 때 줄이 없다.
                bool startSect = d.Cheonha.Sect(l.Sect).Unlock == "start";
                Assert.Equal(startSect, l.YardUnknown.Count > 0);
            }
            var lords = d.Cheonha.BossRegions.Select(r => r.Boss!).ToList();
            Assert.Equal(lords, D.Lords.List.Select(l => l.Id));
            foreach (var l in D.Lords.List)
            {
                Assert.Equal(new[] { "meet1", "meet2", "kill1", "kill2", "kill3", "kill4" }, l.Cuts.Select(c => c.Id));
                Assert.False(string.IsNullOrEmpty(l.Phase2Ko));
                Assert.False(string.IsNullOrEmpty(l.EchoKo));
                Assert.False(string.IsNullOrEmpty(l.BloodMoonKo));
            }
        }

        [Fact]
        public void 사매판_키는_있는_칸을_가리키고_남자만_문파엔_없다()
        {
            var d = Load();
            foreach (var l in D.Leaders.List)
            {
                foreach (var kv in l.Sister)
                {
                    var parts = kv.Key.Split('.');
                    int idx = parts.Length > 1 ? int.Parse(parts[1]) : -1;
                    string original = parts[0] switch
                    {
                        "yardUnknown" => l.YardUnknown[idx],
                        "yardKnown" => l.YardKnown[idx],
                        "inn" => l.Inn[idx],
                        "yardBloodMoon" => l.YardBloodMoon,
                        "join" => l.Join,
                        _ => throw new KeyNotFoundException(kv.Key),
                    };
                    Assert.NotEqual(original, kv.Value);
                }
                if (d.Cheonha.Rebirth.MaleOnlySects.Contains(l.Sect)) Assert.Empty(l.Sister);
                else Assert.NotEmpty(l.Sister);
            }
        }

        [Fact]
        public void 정산_자리표시는_번호로()
        {
            var lord = D.Settlement.Lines.Single(l => l.Id == "lord_first");
            Assert.Contains("{0}", lord.TextKo);
            Assert.Contains("{1}", lord.TextKo);
            Assert.Equal("prologue", D.Settlement.Lines[0].When);
            Assert.Equal("any", D.Settlement.Lines.Last().When);
        }

        [Fact]
        public void 대사도_현지화_목록에_오른다()
        {
            var cat = StringCatalog.FromData(ReadFiles(), ReadText(Path.Combine(DataDir, "strings", "ko.json")));
            Assert.Equal(D.Mok.Lines.First(l => l.Id == "lives_1").TextKo, cat.Korean["dialogue.mok.lines.lives_1.text"]);
            Assert.Equal(D.Leader("hyegong").YardUnknown[0], cat.Korean["dialogue.leaders.list.hyegong.yardUnknown.0"]);
            Assert.Equal(D.Speaker("mok").NameKo, cat.Korean["dialogue.speakers.mok.name"]);
        }
    }
}
