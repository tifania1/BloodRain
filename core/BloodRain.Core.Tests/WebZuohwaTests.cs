using System.Linq;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>
    /// WP-26: 웹 한 생 판 좌화(7장 '생이 끝나는 세 가지') — 멈춤 화면에서 생을 닫고 쓰러졌을 때와 같은 정산,
    /// 자막은 정산 서술 좌화 줄, 다음 귀환에 목 노인 좌화·은거기인 줄(Core InnDialogue 와 같은 고르기).
    /// </summary>
    public class WebZuohwaTests
    {
        static string Proto => ReadRepoFile("web", "prototype", "template.html");

        [Fact] public void 멈춤_화면에_좌화_단추가_있고_한_생_판에서만_보인다()
        {
            var html = Proto;
            Assert.Contains("id=\"zuohwa\" type=\"button\" data-s=\"ui.settle.end.zuohwa\"", html);
            Assert.Contains("$('zuohwa').hidden = !G.body;", html);
            Assert.Contains("G.endWay = 'zuohwa'; endRun(false);", html);
            // 한 생 판에서 '객잔으로'도 좌화처럼 정산(15장: 한 생 도중 저장은 없다)
            Assert.Contains("G && G.body && running ? zuohwa() : toTitle()", html);
        }

        [Fact] public void 정산_자막과_귀환_줄을_데이터에서_읽는다()
        {
            var html = Proto;
            Assert.Contains("\"settleCaps\": drop_notes(load(\"dialogue\")[\"settlement\"][\"lines\"])", ReadRepoFile("tools", "build_web_prototype.py"));
            Assert.Contains("(DATA.settleCaps || []).find(l => l.when === 'zuohwa')", html);
            Assert.Contains("m.lastEnd = st.endWay || 'fallen'", html);
            Assert.Contains("'zuohwa_repeat' : 'zuohwa_first'", html);
            Assert.Contains("whenOf(l).kind === 'hermit'", html);
            // 데이터에 그 줄이 있어야 한다
            var d = Load(); var mok = d.Dialogue.Mok.Lines.Select(l => l.Id).ToList();
            Assert.Contains("zuohwa_first", mok);
            Assert.Contains("zuohwa_repeat", mok);
            Assert.Contains(d.Dialogue.Settlement.Lines, l => l.Kind == "zuohwa");
        }
    }
}
