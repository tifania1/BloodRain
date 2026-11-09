using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>바닥에 깔린 지역 기믹(물길·태극 바닥·모닥불·탑림, WP-13b). 1장 프로토타입(template.html)이
    /// 천하 시안(cheonha.html)의 배치·판정·그림 함수를 글자 그대로 옮겨 쓰는지 본다. 시안을 고치면 프로토타입도 같이 고쳐야 통과한다.</summary>
    public class CheonhaGimmickTests
    {
        static readonly string Sian = TestData.ReadRepoFile("web", "lookdev", "cheonha.html");
        static readonly string Proto = TestData.ReadRepoFile("web", "prototype", "template.html");

        // 들여쓰기만 다를 수 있다(프로토타입은 CHG 안이라 두 칸 들여 씀)
        static string Trim(string s) => string.Join("\n", s.Split('\n').Select(l => l.Trim()));

        static string Func(string html, string name, string indent)
        {
            var m = Regex.Match(html, "^" + indent + "function " + Regex.Escape(name) + @"\(.*?\n" + indent + @"\}$", RegexOptions.Singleline | RegexOptions.Multiline);
            Assert.True(m.Success, $"{name} 를 못 찾음");
            return Trim(m.Value);
        }

        static string Line(string html, string prefix)
        {
            var m = Regex.Match(html, @"^\s*(" + Regex.Escape(prefix) + ".*)$", RegexOptions.Multiline);
            Assert.True(m.Success, prefix);
            return m.Groups[1].Value.TrimEnd();
        }

        [Theory]
        [InlineData("nearestTwo")]
        [InlineData("inWater")]
        [InlineData("pagodaLayout")]
        [InlineData("pagodaPatchAt")]
        [InlineData("pushOutOfPagodas")]
        [InlineData("taijiPads")]
        [InlineData("campfires")]
        [InlineData("drawCampfires")]
        [InlineData("drawWaterways")]
        [InlineData("drawTaijiPads")]
        [InlineData("drawPagodas")]
        public void 프로토타입_바닥_기믹_함수는_시안과_글자가_같다(string name)
        {
            Assert.Equal(Func(Sian, name, ""), Func(Proto, name, "  "));
        }

        [Theory]
        [InlineData("const regionHas = ")]
        [InlineData("const regionHalfGap = ")]
        [InlineData("const nearCampfire = ")]
        [InlineData("const sx = x => ")]
        public void 프로토타입_바닥_기믹_한_줄짜리도_시안과_같다(string prefix)
        {
            Assert.Equal(Line(Sian, prefix), Line(Proto, prefix));
        }

        [Fact]
        public void 놓는_간격은_시안_LD_놀이판_값과_같다()
        {
            // gimmicks.json 에 배치 값이 아직 없어(R-10·R4-07) 프로토타입은 시안 LD 값을 CHG_LD 로 옮겨 쓴다. 둘이 어긋나면 안 된다
            var ld = Regex.Match(Proto, @"const CHG_LD = \{(.*?)\};");
            Assert.True(ld.Success);
            var pairs = Regex.Matches(ld.Groups[1].Value, @"(\w+): ([\d.]+)");
            Assert.Equal(7, pairs.Count);
            foreach (Match p in pairs)
            {
                var key = p.Groups[1].Value;
                var sian = Regex.Match(Sian, @"^\s+(?:\w+: [\d.]+, )*" + key + @": ([\d.]+)", RegexOptions.Multiline);
                Assert.True(sian.Success, $"시안 LD 에 {key} 가 없다");
                Assert.Equal(double.Parse(sian.Groups[1].Value, CultureInfo.InvariantCulture), double.Parse(p.Groups[2].Value, CultureInfo.InvariantCulture));
            }
        }

        [Fact]
        public void 효과는_gimmicks_json_값을_읽는다()
        {
            // 물(걸음 배율·수상비·수로채 무시·나는 적과 수귀 뺌), 태극 바닥(반대 속성·시간·쿨·피해), 모닥불(회복·빙강시 감속·이동 범위 축소 늦춤), 탑림(몸·적)
            foreach (var key in new[]
            {
                "or1(gw.moveSpeedMultiplier)", "p.walkT = GIMW.water.waterWalkSeconds", "!sectT().ignoresWater", "e.type !== 'water_ghost' && !(E_BY_ID[e.type] || {}).ignoresTerrain && inWaterC(e.x, e.y)",
                "const aspect = gv > 0 ? 'yin' : 'yang'", "G.taijiCd[pad.key] = G.t + tj.cooldownSeconds; G.bless = { aspect, t: tj.durationSeconds }", "aspectMult(def.yinyang) * taijiBlessMul(def.yinyang)", "1 + GIMW.taijiFloor.oppositeAspectDamageBonus",
                "cf.healPerSecondMaxHealthRatio * dt * otherHeal()", "(E_BY_ID[e.type] || {}).family === GIMW.campfire.slowedFamily && nearCampfireC(e.x, e.y)", "or1(GIMW.campfire.slowedMoveSpeedMultiplier)", "P.slowedNear === 'campfire' && G.campOn && nearCampfireC(G.player.x, G.player.y) ? P.slowedNearMultiplier : 1",
                "pushPagodaC(p, B.stats.playerBodyRadius, false)", "pushPagodaC(e, e.r, !!(E_BY_ID[e.type] || {}).ignoresTerrain)", "drawGroundGimmicks(now);",
            })
                Assert.Contains(key, Proto);
            var build = TestData.ReadRepoFile("tools", "build_web_prototype.py");
            Assert.Contains("\"water\", \"taijiFloor\", \"campfire\", \"pagodaForest\", \"bell\"", build);
        }
    }
}
