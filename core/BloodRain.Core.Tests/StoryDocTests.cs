using System.Collections.Generic;
using System.Linq;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>대사 풀(`docs/story/`)이 데이터와 어긋나지 않는지. 대사는 아직 JSON 이 아니라 문서에 있어서,
    /// 땅의 주인·대표 인물·문파가 바뀌면 문서가 따라오지 않기 쉽다. 이름과 id, 서로 받는 줄만 본다(문장은 사람 몫).</summary>
    public class StoryDocTests
    {
        static string Story(string file) => ReadRepoFile("docs", "story", file);

        /// <summary>"## 제목" 부터 다음 "## " 전까지(### 는 안에 포함).</summary>
        static string Section(string doc, string headingStart)
        {
            int start = doc.IndexOf("\n## " + headingStart);
            Assert.True(start >= 0, $"'## {headingStart}' 절이 없습니다");
            int end = doc.IndexOf("\n## ", start + 4);
            return end < 0 ? doc.Substring(start) : doc.Substring(start, end - start);
        }

        static IEnumerable<(string id, string name)> Lords()
        {
            var d = Load();
            foreach (var r in d.Cheonha.BossRegions)
                yield return (r.Boss!, d.Bosses.First(b => b.Id == r.Boss).NameKo);
        }

        [Fact]
        public void BossCutsHaveEverySevenLords()
        {
            var doc = Story("boss_cuts.md");
            var lords = Lords().ToList();
            Assert.Equal(7, lords.Count);
            foreach (var (id, name) in lords)
                Assert.Contains($"\n## {name} `{id}`", doc);
        }

        [Fact]
        public void FightLinesCoverEveryLordAndEchoTheirOwnCut()
        {
            var doc = Story("boss_cuts.md");
            var rows = GddTable(Section(doc, "싸움 중 한 줄"));
            foreach (var (id, name) in Lords())
            {
                var row = rows.SingleOrDefault(r => r[0] == $"{name} `{id}`");
                Assert.True(row != null, $"싸움 중 한 줄에 {name} 줄이 없습니다");
                Assert.Equal(4, row!.Length);
                Assert.All(row, c => Assert.False(string.IsNullOrWhiteSpace(c)));

                // 혈영 줄은 그 땅의 주인 컷 대사를 끊어 쓴 메아리다. 컷 대사를 고치면 같이 고친다.
                var cuts = Section(doc, $"{name} `{id}`");
                foreach (var piece in row[2].Split('…').Select(p => p.Trim(' ', '.', '?', '!')).Where(p => p.Length > 0))
                    Assert.True(cuts.Contains(piece), $"{name} 혈영 줄의 '{piece}' 가 컷 대사에 없습니다");
            }
        }

        [Fact]
        public void MokHasALineForEveryLord()
        {
            var rows = GddTable(Section(Story("mok.md"), "땅의 주인을 처음 쓰러뜨린 뒤"));
            Assert.Equal(Lords().Select(l => l.name).OrderBy(n => n), rows.Select(r => r[0]).OrderBy(n => n));
        }

        [Fact]
        public void LeaderPoolMatchesSectLeaders()
        {
            var d = Load();
            var doc = Story("leaders.md");
            var leaders = d.Cheonha.Sects.Where(s => s.Leader != null).Select(s => s.Leader!).Distinct().ToList();
            foreach (var id in leaders)
            {
                var name = d.Character(id).NameKo;
                Assert.Contains($"\n## {name} `{id}`", doc);
            }
        }

        [Fact]
        public void SisterLinesCoverEveryLeaderWhoseSectHasWomen()
        {
            var d = Load();
            var doc = Story("leaders.md");
            var rows = GddTable(Section(doc, "사매판"));
            var femaleSects = d.Cheonha.Sects.Where(s => s.Leader != null && !d.Cheonha.Rebirth.MaleOnlySects.Contains(s.Id));
            foreach (var sect in femaleSects)
            {
                var name = d.Character(sect.Leader!).NameKo;
                var mine = rows.Where(r => r[0] == name).ToList();
                Assert.True(mine.Count > 0, $"{name} 사매판이 없습니다");

                // 바꿀 줄이 그 인물 표에 실제로 있고, 사매판은 원래 줄과 다르다.
                var own = GddTable(Section(doc, $"{name} `{sect.Leader}`"));
                foreach (var r in mine)
                {
                    var original = own.SingleOrDefault(o => o[0] == r[1] || o[0].StartsWith(r[1] + "("));
                    Assert.True(original != null, $"{name} 의 '{r[1]}' 줄이 인물 표에 없습니다");
                    Assert.NotEqual(original![1], r[2]);
                }
            }
            // 소림처럼 남자 몸만 나오는 문파의 대표 인물은 사매판이 없어야 맞다.
            foreach (var sid in d.Cheonha.Rebirth.MaleOnlySects)
            {
                var leader = d.Cheonha.Sect(sid).Leader;
                if (leader != null) Assert.DoesNotContain(rows, r => r[0] == d.Character(leader).NameKo);
            }
        }

        [Fact]
        public void PrologueQuotesMatchTheDesignDoc()
        {
            var doc = Story("prologue.md");
            var gdd = Gdd;
            foreach (var quote in new[] { "그건 네 것이 아니다. 네 것으로 만들고 싶으면, 다시 걸어라.", "살아라… 그리고 막아라." })
            {
                Assert.Contains(quote, gdd);
                Assert.Contains(quote, doc);
            }
            var queue = ReadRepoFile("art_queue", "queue.jsonl");
            foreach (var art in new[] { "sb_prologue_burnout", "sb_prologue_needle" })
            {
                Assert.Contains($"`{art}`", doc);
                Assert.Contains($"\"id\": \"{art}\"", queue);
            }
        }

        [Fact]
        public void InnFacilityLinesUseFacilitiesFromTheDesignDoc()
        {
            var facilities = GddTable(GddSection("객잔 시설")).Select(r => r[0]).ToList();
            var rows = GddTable(Section(Story("unnang.md"), "시설이 열릴 때"));
            Assert.NotEmpty(rows);
            foreach (var r in rows)
            {
                var name = r[0].Split('(')[0].Trim();
                Assert.True(facilities.Contains(name), $"운낭 시설 줄의 '{name}' 이 기획서 10장 객잔 시설 표에 없습니다");
            }
        }

        [Fact]
        public void BedroomTraitLinesNameRealBirthTraits()
        {
            var traits = Load().Cheonha.BirthTraits.List.Select(t => t.NameKo).ToList();
            var rows = GddTable(Section(Story("inn_screens.md"), "침방"));
            // "약골 몸을 처음 골랐을 때" 처럼 한 낱말 + 꼬리. 여자 몸·'타고난 성질이 붙은 몸' 줄은 성질 이름이 아니다.
            const string tail = " 몸을 처음 골랐을 때";
            var names = rows.Select(r => r[0]).Where(c => c.EndsWith(tail)).Select(c => c.Substring(0, c.Length - tail.Length))
                .Where(n => !n.Contains(' ') && n != "여자").ToList();
            Assert.NotEmpty(names);
            foreach (var name in names)
            {
                Assert.True(traits.Contains(name), $"침방 줄의 성질 '{name}' 이 cheonha.json 에 없습니다");
            }
        }
    }
}
