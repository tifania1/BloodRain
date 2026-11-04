#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>종장·엔딩 대사(DL-08·DL-09): dialogue.json final·endings 가 docs/story 표, 보스·회차 데이터와 맞는지.</summary>
    public class FinalEndingDialogueTests
    {
        static GameData G => Load();
        static DialogueDef D => G.Dialogue;
        static string Story(string file) => ReadRepoFile("docs", "story", file);

        static string Section(string doc, string headingStart)
        {
            int start = doc.IndexOf("\n## " + headingStart);
            Assert.True(start >= 0, $"'## {headingStart}' 절이 없습니다");
            int end = doc.IndexOf("\n## ", start + 4);
            return end < 0 ? doc.Substring(start) : doc.Substring(start, end - start);
        }

        /// <summary>"(목 노인) 오래 앓았구나." → ("목 노인", "오래 앓았구나."), "없음" → null.</summary>
        static (string? who, string text)? Cell(string cell)
        {
            if (cell == "없음") return null;
            var m = Regex.Match(cell, @"^\(([^)]*)\)\s*(.*)$");
            return m.Success ? (m.Groups[1].Value, m.Groups[2].Value) : (null, cell);
        }

        static void SameAsTable(List<string[]> rows, IList<CutDef> cuts, int textColumn)
        {
            Assert.Equal(rows.Count, cuts.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                var cell = Cell(rows[i][textColumn]);
                if (cell == null) { Assert.Empty(cuts[i].Lines); continue; }
                var line = Assert.Single(cuts[i].Lines);
                Assert.Equal(cell.Value.text, line.TextKo);
                if (cell.Value.who == "속삭임") { Assert.True(line.Whisper); Assert.Null(line.Speaker); }
                else if (cell.Value.who != null) Assert.Equal(cell.Value.who, D.Speaker(line.Speaker!).NameKo);
                else Assert.Null(line.Speaker);
            }
        }

        [Fact]
        public void 종장_입장_컷이_문서_표와_같다()
        {
            SameAsTable(GddTable(Section(Story("final.md"), "입장 컷")), D.Final.Enter.Cuts, 2);
        }

        [Fact]
        public void 종장_단계_줄은_보스_단계마다_하나씩_문서_표와_같다()
        {
            var stageIds = G.FinalBoss.Stages.Select(s => s.Id).ToList();
            Assert.Equal(stageIds, D.Final.Stages.Select(s => s.Id).ToList());
            var rows = GddTable(Section(Story("final.md"), "단계 한 줄"));
            Assert.Equal(stageIds.Count, rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                var st = D.Final.Stages[i];
                Assert.Contains($"`{st.Id}`", rows[i][0]);
                Assert.Equal(rows[i][2] == "서술" ? null : rows[i][2], st.Speaker == null ? null : D.Speaker(st.Speaker).NameKo);
                Assert.Equal(rows[i][3], st.TextKo);
                Assert.Equal(rows[i][4] == "없음" ? null : rows[i][4], st.BloodMoonKo);
            }
        }

        [Fact]
        public void 혈영_일곱은_땅의_주인_혈영_줄을_그대로_쓴다()
        {
            var shadows = G.FinalBoss.Shadows;
            Assert.Equal(7, shadows.Count);
            foreach (var id in shadows) Assert.False(string.IsNullOrEmpty(D.Lord(id).EchoKo), id);
        }

        [Fact]
        public void 엔딩은_회차_데이터와_Soul_의_id_셋이다()
        {
            var ids = D.Endings.List.Select(e => e.Id).ToList();
            Assert.Equal(G.Cheonha.Cycle.EndingsSecond, ids);
            Assert.Equal(new[] { Soul.EndingSeal, Soul.EndingInherit, Soul.EndingTrue }, ids);
            foreach (var first in G.Cheonha.Cycle.EndingsFirst) Assert.Contains(first, ids);
        }

        [Fact]
        public void 엔딩_고르는_화면과_컷이_문서_표와_같다()
        {
            var doc = Story("endings.md");
            var choice = GddTable(Section(doc, "고르는 화면"));
            Assert.Equal(D.Endings.List.Count, choice.Count);
            for (int i = 0; i < choice.Count; i++)
            {
                var e = D.Endings.List[i];
                Assert.Equal($"`{e.Id}`", choice[i][0]);
                Assert.Equal(choice[i][1], e.NameKo);
                Assert.Equal(choice[i][2], e.DescKo);
            }
            SameAsTable(GddTable(Section(doc, "봉인 `seal`")), D.Ending("seal").Cuts, 2);
            SameAsTable(GddTable(Section(doc, "계승 `inherit`")), D.Ending("inherit").Cuts, 2);
            SameAsTable(GddTable(Section(doc, "진엔딩 의심(醫心) `true`")), D.Ending("true").Cuts, 2);
        }

        [Fact]
        public void 기획서_문장을_받는_줄()
        {
            // 계승 속삭임은 4장 문장 그대로, 진엔딩 이름은 4장 '의심(醫心)'
            Assert.Contains("\"이 힘이면 다시는 아무도 잃지 않는다\"", Gdd);
            Assert.Equal("이 힘이면 다시는 아무도 잃지 않는다.", D.Ending("inherit").Cuts[0].Lines[0].TextKo);
            Assert.Contains("진엔딩 '" + D.Ending("true").NameKo + "'", Gdd);
            // 봉인 마지막 컷은 인트로 컷 4 를 되풀이한다(회귀)
            Assert.Equal(D.Intro.Cuts[3].Lines[0].TextKo, D.Ending("seal").Cuts.Last().Lines[0].TextKo);
        }
    }
}
