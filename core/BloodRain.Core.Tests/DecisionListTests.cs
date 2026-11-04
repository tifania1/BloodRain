using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>사람 결정 모음(docs/decisions.md, DC-01)이 backlog 와 어긋나지 않는지.
    /// 사람은 아침에 decisions.md 한 장만 보고 정하니, backlog 에 새 '사람 결정 대기'가 생기면 거기 '한눈에' 표에도 한 줄 더하고,
    /// 정해져서 backlog 항목을 풀면 표에서 지운다(아래 '정한 것'으로 옮김).</summary>
    public class DecisionListTests
    {
        // id 바로 뒤에 '사람 결정 대기'가 오는 열린 줄만 센다(다른 항목 설명 속 '사람 결정 대기로' 같은 말은 빼려고).
        static readonly Regex OpenDecision = new Regex(@"^- \[ \] ([A-Z][A-Z0-9]*-[0-9A-Za-z]+)\s+(?:\*\*)?사람 결정 대기", RegexOptions.Multiline);
        static readonly Regex TableRow = new Regex(@"^\| \d+ \| ([A-Z][A-Z0-9]*-[0-9A-Za-z]+) \|", RegexOptions.Multiline);

        static string[] BacklogOpen() =>
            OpenDecision.Matches(ReadRepoFile("docs", "backlog.md")).Cast<Match>().Select(m => m.Groups[1].Value).ToArray();

        static string[] Listed()
        {
            var md = ReadRepoFile("docs", "decisions.md");
            var start = md.IndexOf("## 한눈에");
            var end = md.IndexOf("\n## ", start + 1);
            Assert.True(start >= 0 && end > start, "decisions.md 에 '## 한눈에' 절이 있어야 한다");
            return TableRow.Matches(md.Substring(start, end - start)).Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
        }

        [Fact]
        public void backlog_의_열린_사람_결정_대기가_모두_decisions_표에_있다()
        {
            var missing = BacklogOpen().Except(Listed()).ToList();
            Assert.True(missing.Count == 0,
                "backlog 에 '사람 결정 대기'로 열렸는데 docs/decisions.md '한눈에' 표에 없는 항목: " + string.Join(", ", missing)
                + ". 표 맨 아래에 '| 번호 | id | 한 줄 질문 | 추천 | 급함 |' 한 줄과 '하나씩' 절에 질문·추천·다른 안·근거를 더한다.");
        }

        [Fact]
        public void decisions_표의_항목은_backlog_에서_아직_결정_대기다()
        {
            var stale = Listed().Except(BacklogOpen()).ToList();
            Assert.True(stale.Count == 0,
                "docs/decisions.md 표에 있는데 backlog 에선 닫혔거나 '사람 결정 대기'가 풀린 항목: " + string.Join(", ", stale)
                + ". 표와 '하나씩' 절에서 지우고 '정한 것' 절에 날짜·id·무엇으로 한 줄을 남긴다.");
        }

        [Fact]
        public void decisions_표의_번호와_id_는_겹치지_않는다()
        {
            var ids = Listed();
            Assert.NotEmpty(ids);
            Assert.Equal(ids.Length, ids.Distinct().Count());
        }
    }
}
