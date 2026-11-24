using BloodRain.Core.Data;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>바닥에 깔린 지역 기믹(물길·태극 바닥·모닥불·탑림·범종 격자, WP-13b). 1장 프로토타입(template.html)이
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
        [InlineData("songBells")]
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
            // 프로토타입 CHG_LD 는 gimmicks.json layout 을 읽고(R-10, 2026-11-20), 데이터가 없을 때의 기본값은 시안 LD 값이다. 셋이 어긋나면 안 된다
            Assert.Contains("const L = (DATA.gimmicks || {}).layout || {}", Proto);
            var ld = Regex.Match(Proto, @"const CHG_LD = \(\(\) => \{ const L = \(DATA\.gimmicks \|\| \{\}\)\.layout \|\| \{\}, d = \{(.*?)\};");
            var layout = TestData.Load().Gimmicks.Layout;
            Assert.True(ld.Success);
            var pairs = Regex.Matches(ld.Groups[1].Value, @"(\w+): ([\d.]+)");
            Assert.Equal(8, pairs.Count);
            foreach (Match p in pairs)
            {
                var key = p.Groups[1].Value;
                var sian = Regex.Match(Sian, @"^\s+(?:\w+: [\d.]+, )*" + key + @": ([\d.]+)", RegexOptions.Multiline);
                Assert.True(sian.Success, $"시안 LD 에 {key} 가 없다");
                Assert.Equal(double.Parse(sian.Groups[1].Value, CultureInfo.InvariantCulture), double.Parse(p.Groups[2].Value, CultureInfo.InvariantCulture));
                var prop = typeof(GimmickLayoutDef).GetField(char.ToUpperInvariant(key[0]) + key.Substring(1));
                Assert.True(prop != null, $"gimmicks.json layout 에 {key} 가 없다");
                Assert.Equal(double.Parse(p.Groups[2].Value, CultureInfo.InvariantCulture), (double)(float)prop!.GetValue(layout)!, 3);
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
                "pushPagodaC(p, B.stats.playerBodyRadius, false)", "G.bellCd[b.key] = G.t + bg.cooldownSeconds", "e.boss ? bg.bossStunSeconds : bg.stunSeconds", "bells: mode === 'prologue' || SCALE_MODE ? [] :", "pushPagodaC(e, e.r, !!(E_BY_ID[e.type] || {}).ignoresTerrain)", "drawGroundGimmicks(now);",
            })
                Assert.Contains(key, Proto);
            // 수귀(WP-13c): 물에서만 솟고(onlyInWater), 물 위 수귀가 pull 값으로 끌어당긴다(한 걸음에 한 번)
            foreach (var key in new[] { "E_BY_ID[type].onlyInWater && !inWaterC(x, y)", "D.pull && d < D.pull.range && p.dashT <= 0", "G.pullStamp !== G.t && inWaterC(e.x, e.y)", "const v = D.pull.speed * dt" })
                Assert.Contains(key, Proto);
            var build = TestData.ReadRepoFile("tools", "build_web_prototype.py");
            Assert.Contains("\"water\", \"taijiFloor\", \"campfire\", \"pagodaForest\", \"bell\", \"bloodAltars\"", build);
        }

        [Fact]
        public void 프로토타입_혈제단은_gimmicks_값과_Core_LitCount_를_따른다()
        {
            // 자리(layout.altarRing 고리, 북동부터 시계 방향), 켜진 수(Core BloodAltarGimmick.LitCount 와 같은 식), 강화(걸음·피해), 기혈(적 기혈 식), 부수면 보갑
            foreach (var key in new[]
            {
                "L.altarRing ?? 12", "L.altarOrderFromDegrees ?? 45", "(a0 - 90 * i) * Math.PI / 180",
                "t < d.firstLightSeconds ? 0 : Math.min(d.count, 1 + (d.lightEverySeconds > 0 ? Math.floor((t - d.firstLightSeconds) / d.lightEverySeconds) : d.count))",
                "GIMW.bloodAltars.enemyMoveSpeedMultiplier", "src && src.altarBuffed) dmg *= GIMW.bloodAltars.enemyDamageMultiplier", "d.health * foeHpMul()",
                "if (e.altar) return altarDown(e);", "GIMW.bloodAltars.chestOnBreak", "st.ratio = st.e.hp / st.e.maxHp",
            })
                Assert.Contains(key, Proto);
            // 웹 식이 Core 와 같은 값을 내는지: 120초 0개 미만, 120초 1, 239초 1, 240초 2, 아주 나중에 상한 4
            var d = TestData.Load().Gimmicks.BloodAltars;
            Assert.Equal(0, BloodRain.Core.World.BloodAltarGimmick.LitCount(d, 119f));
            Assert.Equal(1, BloodRain.Core.World.BloodAltarGimmick.LitCount(d, 120f));
            Assert.Equal(2, BloodRain.Core.World.BloodAltarGimmick.LitCount(d, 240f));
            Assert.Equal(d.Count, BloodRain.Core.World.BloodAltarGimmick.LitCount(d, 9999f));
        }

        [Fact]
        public void 프로토타입은_적_정예_땅의_주인_원화가_있으면_그것으로_그린다()
        {
            // WP-13c·d·e '제 그림': 빌드가 Art/Enemies·Art/Bosses 를 foeArt 로 넣고, drawFoe 가 파일 이름 규칙(enemy_·elite_·boss_ + id)으로 고른다
            var build = TestData.ReadRepoFile("tools", "build_web_prototype.py");
            Assert.Contains("\"foeArt\": foe_art(),", build);
            foreach (var key in new[] { "k = e.lordBoss ? 'boss_' + e.lordBoss : e.jang ? 'boss_jang_sam'", "'elite_' + ELITES[e.eliteKey].id", "k = 'enemy_' + e.type;", "const spr = foeArtName(e);", "get('art') !== '0'" })
                Assert.Contains(key, Proto);
            // 데이터의 정예·땅의 주인 id 가운데 원화 파일 이름과 맞는 것이 있어야 규칙이 산다
            var art = System.IO.Path.Combine(TestData.RepoRoot, "unity", "BloodRain", "Assets", "Art");
            Assert.True(System.IO.File.Exists(System.IO.Path.Combine(art, "Enemies", "elite_poison_toad_king.png")));
            Assert.True(System.IO.File.Exists(System.IO.Path.Combine(art, "Bosses", "boss_hyeolheo.png")));
        }
    }
}
