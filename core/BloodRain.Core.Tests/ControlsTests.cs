using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Meta;
using Xunit;

namespace BloodRain.Core.Tests
{
    // 기획서 8장 '조작' 표·12장 '조작 재설정'.
    public class ControlsTests
    {
        [Fact]
        public void 기본값이_8장_조작_표와_같다()
        {
            var c = Controls.Defaults();
            Assert.Equal(new[] { "<Keyboard>/w", "<Keyboard>/upArrow" }, c.Paths(ControlAction.Up));
            Assert.Equal(new[] { "<Keyboard>/space", "<Keyboard>/leftShift", "<Gamepad>/buttonSouth" }, c.Paths(ControlAction.Dash));
            Assert.Equal(new[] { "<Keyboard>/e", "<Mouse>/rightButton", "<Gamepad>/rightTrigger" }, c.Paths(ControlAction.Ultimate));
            Assert.Equal(new[] { "<Keyboard>/escape", "<Keyboard>/tab", "<Gamepad>/start" }, c.Paths(ControlAction.Pause));
            Assert.Empty(c.Conflicts());

            var gdd = TestData.ReadRepoFile("docs", "design", "gdd.md");
            Assert.Contains("| 이동 | WASD / 방향키 | 왼쪽 스틱 |", gdd);
            Assert.Contains("| 경공 | Space / Shift | A(아래 버튼) |", gdd);
            Assert.Contains("| 오의 | E / 마우스 오른쪽 | RT |", gdd);
            Assert.Contains("| 일시정지·무공 정보 | Esc / Tab | Start |", gdd);
        }

        [Fact]
        public void 레벨업_단축키는_웹과_같은_R_X_Z()
        {
            var c = Controls.Defaults();
            Assert.Equal("<Keyboard>/r", c.Get(ControlAction.Reroll, 0));
            Assert.Equal("<Keyboard>/x", c.Get(ControlAction.Banish, 0));
            Assert.Equal("<Keyboard>/z", c.Get(ControlAction.Skip, 0));
            var html = TestData.ReadRepoFile("web", "prototype", "template.html");
            Assert.Contains("e.code === 'KeyR' && !e.repeat) rerollOffers()", html);
            Assert.Contains("e.code === 'KeyX' && !e.repeat) toggleBanish()", html);
            Assert.Contains("e.code === 'KeyZ' && !e.repeat) skipOffer()", html);
            // 버튼 글자는 조작 재설정한 키를 {1} 로 받는다(2026-11-09 WP-69U — 전엔 R·X·Z 를 글로 박아 둠)
            foreach (var lang in new[] { "ko", "en", "zh-Hans", "zh-Hant" })
            {
                var s = TestData.ReadRepoFile("unity", "BloodRain", "Assets", "Resources", "Data", "strings", lang + ".json");
                foreach (var key in new[] { "reroll", "banish", "banish_cancel", "skip" })
                    Assert.Matches("\"ui\\.levelup\\." + key + "\": \"[^\"]*  \\{1\\}\"", s);
            }
        }

        [Fact]
        public void 쓰는_키를_고르면_맞바꾼다()
        {
            var c = Controls.Defaults();
            Assert.True(c.Rebind(ControlAction.Dash, 0, "<Keyboard>/e"));
            Assert.Equal("<Keyboard>/e", c.Get(ControlAction.Dash, 0));
            Assert.Equal("<Keyboard>/space", c.Get(ControlAction.Ultimate, 0));
            Assert.Empty(c.Conflicts());

            // 같은 행동의 다른 칸끼리도
            Assert.True(c.Rebind(ControlAction.Up, 0, "<Keyboard>/upArrow"));
            Assert.Equal(new[] { "<Keyboard>/upArrow", "<Keyboard>/w" }, c.Paths(ControlAction.Up));

            // 빈 칸을 가진 행동과 바꾸면 그쪽이 빈다
            Assert.True(c.Rebind(ControlAction.Reroll, 1, "<Keyboard>/x"));
            Assert.Null(c.Get(ControlAction.Banish, 0));
            Assert.Empty(c.Conflicts());
        }

        [Fact]
        public void 못_고르는_키와_칸이_안_맞는_키는_거절한다()
        {
            var c = Controls.Defaults();
            Assert.False(c.Rebind(ControlAction.Dash, 0, "<Keyboard>/1"));
            Assert.False(c.Rebind(ControlAction.Dash, 0, "<Keyboard>/enter"));
            Assert.False(c.Rebind(ControlAction.Dash, 0, "<Gamepad>/buttonEast"));
            Assert.False(c.Rebind(ControlAction.Dash, Controls.Pad, "<Keyboard>/q"));
            Assert.Equal("<Keyboard>/space", c.Get(ControlAction.Dash, 0));
            Assert.True(c.Rebind(ControlAction.Dash, Controls.Pad, "<Gamepad>/buttonEast"));
            Assert.Throws<System.ArgumentException>(() => c.Rebind("jump", 0, "<Keyboard>/q"));
        }

        [Fact]
        public void 저장은_바꾼_칸만_남고_다시_읽으면_같다()
        {
            var c = Controls.Defaults();
            c.Rebind(ControlAction.Dash, 1, "<Keyboard>/q");
            c.Rebind(ControlAction.Ultimate, 1, null);
            var box = new Dictionary<string, string>();
            c.Save((k, v) => box[k] = v, k => box.Remove(k));
            Assert.Equal(new[] { "bind.dash.1", "bind.ult.1" }, box.Keys.OrderBy(k => k));
            Assert.Equal("", box["bind.ult.1"]);

            var back = Controls.Load(k => box.TryGetValue(k, out var v) ? v : null);
            foreach (var a in ControlAction.All)
                for (int i = 0; i <= Controls.Pad; i++) Assert.Equal(c.Get(a, i), back.Get(a, i));

            c.Reset(ControlAction.Ultimate);
            Assert.Equal("<Mouse>/rightButton", c.Get(ControlAction.Ultimate, 1));
        }

        [Fact]
        public void 못_읽는_저장은_버린다()
        {
            var c = Controls.Load(k => k == "bind.dash.0" ? "<Keyboard>/1" : k == "bind.pause.pad" ? "<Keyboard>/p" : null);
            Assert.Equal("<Keyboard>/space", c.Get(ControlAction.Dash, 0));
            Assert.Equal("<Gamepad>/start", c.Get(ControlAction.Pause, Controls.Pad));
            // 저장 파일 속 같은 키 둘은 맞바꾸기로 풀린다
            var d = Controls.Load(k => k == "bind.dash.0" ? "<Keyboard>/w" : k == "bind.up.0" ? "<Keyboard>/w" : null);
            Assert.Empty(d.Conflicts());
        }
    }
}
