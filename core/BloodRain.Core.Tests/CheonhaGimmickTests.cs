using System;
using BloodRain.Core.Data;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>바닥에 깔린 지역 기믹(물길·태극 바닥·모닥불·탑림·범종 격자, WP-13b). 원래는 천하 시안(cheonha.html)이 원본이고
    /// 합친 판(template.html)이 글자 그대로 옮겨 썼는지 봤다. 2026-11-25(WP-16)부터 합친 판이 원본이다 — 시안을 줄여도 이 테스트는 안 깨진다.</summary>
    public class CheonhaGimmickTests
    {
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
        public void 프로토타입에_바닥_기믹_함수가_있다(string name)
        {
            // CHG 안이라 두 칸 들여 쓴다. 몸이 비어 있으면 안 된다
            var f = Func(Proto, name, "  ");
            Assert.True(f.Split('\n').Length >= 3, $"{name} 몸이 비었다");
        }

        [Theory]
        [InlineData("const regionHas = ")]
        [InlineData("const regionHalfGap = ")]
        [InlineData("const nearCampfire = ")]
        [InlineData("const sx = x => ")]
        public void 프로토타입에_바닥_기믹_한_줄짜리가_있다(string prefix)
        {
            Assert.True(Line(Proto, prefix).Length > prefix.Length + 3, prefix);
        }

        [Fact]
        public void 놓는_간격_기본값은_gimmicks_json_layout_과_같다()
        {
            // 프로토타입 CHG_LD 는 gimmicks.json layout 을 읽고(R-10, 2026-11-20), 데이터가 없을 때의 기본값도 그 값과 같아야 한다(옛 시안 LD 값에서 온 것)
            Assert.Contains("const L = (DATA.gimmicks || {}).layout || {}", Proto);
            var ld = Regex.Match(Proto, @"const CHG_LD = \(\(\) => \{ const L = \(DATA\.gimmicks \|\| \{\}\)\.layout \|\| \{\}, d = \{(.*?)\};");
            var layout = TestData.Load().Gimmicks.Layout;
            Assert.True(ld.Success);
            var pairs = Regex.Matches(ld.Groups[1].Value, @"(\w+): ([\d.]+)");
            Assert.Equal(9, pairs.Count);   // LG-04 대숲 간격 bambooSpacing 더함
            foreach (Match p in pairs)
            {
                var key = p.Groups[1].Value;
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
        public void 프로토타입_운해와_역류_파도는_gimmicks_값과_Core_박자를_따른다()
        {
            // LG-01 운해·LG-02 역류 파도: 빌드가 두 값을 넣고, 웹 박자 식이 Core BloomsBetween·StartsBetween 과 같은 꼴
            var build = TestData.ReadRepoFile("tools", "build_web_prototype.py");
            Assert.Contains("\"cloudSea\", \"reverseWave\", \"driftingBoats\", \"bambooThicket\"", build);
            foreach (var key in new[]
            {
                "const up = Math.floor((b - first) / period) + 1, bef = a < first ? 0 : Math.floor((a - first) / period) + 1",
                "cs.driftSpeed / 100 * MOVE", "cs.driftDegrees * Math.PI / 180", "rand(-1, 1) * cs.scatter", "<= cs.radius",
                "since < cs.telegraphSeconds", "cs.telegraphSeconds + cs.leaveSeconds", "G.cloudHide && G.cloudLast && !e.boss", "!(G.cloudHide && !e.boss)",
                "rw.projectileSpeed / 100 * MOVE", "rw.directionDegrees * Math.PI / 180", "rw.playerDamageMaxHealthRatio", "rw.eliteDamageMaxHealthRatio",
                "G.t - w.t0 - rw.telegraphSeconds", "!(p.dashT > 0) && !onBoatW(p.x, p.y)", "cloudSeaStep(dt); reverseWaveStep(dt);",
                // LG-03 배: 간격·속도·크기는 데이터, 물길 가운데 줄은 drawWaterways 와 같은 굽이 식, 배 위면 물 감속 없음
                "db.spacing", "db.speed * G.t", "db.length / 2", "db.width / 2", "cy + j * sp - W * Math.sin((x - cx) / L)",
                "CHG.inWater(ax, ay) && !onBoatW(p.x, p.y)", "inWaterC(e.x, e.y) && !onBoatW(e.x, e.y)", "boatsStep(dt);",
                // LG-04 대나무 덤불: 탑림 놓기(포기 반경 stalkRadius), 걷는 것만 막음, 맞은 수·다시 서는 시각·음식은 데이터
                "LD.bambooSpacing * k", "pagodaRadius: def.stalkRadius", "flying && !def.blocksFlying", "Math.max(1, def.hitsToBreak)",
                "G.t + def.regrowSeconds", "def.dropChance", "G.player.maxHp * def.healMaxHealthRatio", "if (w && G.bambooOn) cutBambooC(",
            })
                Assert.Contains(key, Proto);
            // 박자 값이 Core 와 같은지(웹 식을 그대로 C# 으로): 운해 12초마다, 역류 120초마다
            static int Times(float first, float period, float a, float b)
            {
                if (!(period > 0) || b < first) return 0;
                int up = (int)Math.Floor((b - first) / period) + 1, bef = a < first ? 0 : (int)Math.Floor((a - first) / period) + 1;
                return Math.Max(0, up - bef);
            }
            var g = TestData.Load().Gimmicks;
            foreach (var (a, b) in new[] { (0f, 11.9f), (11.9f, 12f), (12f, 23.9f), (23.95f, 24.01f), (100f, 160f) })
                Assert.Equal(BloodRain.Core.World.CloudSeaGimmick.BloomsBetween(g.CloudSea, a, b), Times(g.CloudSea.FirstSeconds, g.CloudSea.PeriodSeconds, a, b));
            foreach (var (a, b) in new[] { (0f, 119.9f), (119.9f, 120f), (120f, 239f), (239.9f, 240.1f), (0f, 600f) })
                Assert.Equal(BloodRain.Core.World.ReverseWaveGimmick.StartsBetween(g.ReverseWave, a, b), Times(g.ReverseWave.FirstSeconds, g.ReverseWave.PeriodSeconds, a, b));
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
