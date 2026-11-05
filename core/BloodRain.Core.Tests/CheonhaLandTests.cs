using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>중원 한 장의 땅 윤곽·강(cheonha.json world.land·rivers, WP-12).
    /// 두 웹 판이 같은 값을 그리도록 HTML 에 박지 않고 데이터에서 읽는지도 본다.</summary>
    public class CheonhaLandTests
    {
        readonly JObject _c = JObject.Parse(File.ReadAllText(Path.Combine(TestData.DataDir, "cheonha.json")));
        JObject World => (JObject)_c["world"]!;

        static List<(double x, double y)> Points(JToken t) =>
            ((JArray)t).Select(p => ((double)p[0]!, (double)p[1]!)).ToList();

        static bool Inside(List<(double x, double y)> poly, double x, double y)
        {
            bool c = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                var (xi, yi) = poly[i]; var (xj, yj) = poly[j];
                if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) c = !c;
            }
            return c;
        }

        [Fact]
        public void 땅은_비율_좌표_다각형이고_모든_지역이_땅_안에_있다()
        {
            var land = Points(World["land"]!);
            Assert.True(land.Count >= 3);
            Assert.All(land, p => Assert.InRange(p.x, 0, 1));
            Assert.All(land, p => Assert.InRange(p.y, 0, 1));
            foreach (var r in _c["regions"]!.Children<JObject>())
            {
                var pos = (JArray)r["pos"]!;
                Assert.True(Inside(land, (double)pos[0]!, (double)pos[1]!), $"{r["id"]} 이 땅 밖에 있다");
            }
        }

        [Fact]
        public void 강은_두_점_이상_꺾은선이고_비율_좌표다()
        {
            var rivers = ((JArray)World["rivers"]!).ToList();
            Assert.NotEmpty(rivers);
            foreach (var r in rivers)
            {
                var pts = Points(r);
                Assert.True(pts.Count >= 2);
                Assert.All(pts, p => Assert.True(p.x >= 0 && p.x <= 1 && p.y >= 0 && p.y <= 1));
            }
        }

        [Fact]
        public void 두_웹_판이_땅_윤곽을_데이터에서_읽는다()
        {
            var sian = TestData.ReadRepoFile("web", "lookdev", "cheonha.html");
            Assert.Contains("const LAND = WD.land.map", sian);
            Assert.Contains("const RIVERS = WD.rivers.map", sian);
            Assert.DoesNotContain("const LAND = [[", sian);
            var proto = TestData.ReadRepoFile("web", "prototype", "template.html");
            Assert.Contains("WD.land", proto);
            Assert.Contains("r.chapter === 'chapter1'", proto);
            // 봉마곡(final)은 지명도 밟은 땅 판정도 빼고, 밟은 땅은 가장 가까운 지역(시안 trackRegion 과 같은 규칙)
            Assert.Contains("filter(r => !r.final)", proto);
            Assert.Contains("function trackChRegion()", proto);
            // 다른 땅 고유 적은 한 생 비율(chapters.json life.localShare)을 빌드가 넣은 값으로 섞는다
            Assert.Contains("DATA.lifeLocalShare", proto);
            Assert.Contains("lf = localFoe(); if (lf) spawnLocalGroup(lf, x, y); else spawnEnemy(pickType(", proto);
            // 고유 적 동작 넷은 enemies.json 값(zoneRadius·shoot·dash·auraBonus)을 읽는다
            foreach (var key in new[] { "D.zoneDamagePerSecond", "sh.interval", "ds.restMin", "D.auraBonus", "else if (LOCAL_BEH[e.behavior])", "D.group.flankAngle", "D.twin.reviveHealth", "group.commitRange", "hk.bindSeconds", "D.onDeath === 'poison_pool'", "fg.healthLossPerSecond", "fr.playerDamageMaxHealthRatio", "GIMW.poisonFog.qiMultiplier", "GIMW.blizzard.viewScale", "GIMW.redMoon.enemyMoveSpeedMultiplier", "const ek = regionEliteKey(ev.type); spawnElite(ek);", "!fb.regions.includes(r.id)" })
                Assert.Contains(key, proto);
            var build = TestData.ReadRepoFile("tools", "build_web_prototype.py");
            Assert.Contains("\"land\", \"rivers\"", build);
        }

        static string Body(string html, string pattern)
        {
            var m = System.Text.RegularExpressions.Regex.Match(html, pattern, System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.Multiline);
            Assert.True(m.Success, pattern);
            // 들여쓰기만 다를 수 있다
            return string.Join("\n", m.Groups[1].Value.Split('\n').Select(l => l.Trim()));
        }

        [Fact]
        public void 프로토타입_소품은_시안과_같은_코드로_같은_자리에_놓인다()
        {
            var sian = TestData.ReadRepoFile("web", "lookdev", "cheonha.html");
            var proto = TestData.ReadRepoFile("web", "prototype", "template.html");
            const string build = @"\(function buildProps\(\) \{\n(.*?)\n\s*\}\)\(\);";
            const string sprite = @"^\s*(function propSprite\(kind, v\) \{\n.*?\n\s*return c;\n\s*\})";
            const string rng = @"^\s*(function rng\(seed\).*?)$";
            Assert.Equal(Body(sian, build), Body(proto, build));
            Assert.Equal(Body(sian, sprite), Body(proto, sprite));
            Assert.Equal(Body(sian, rng), Body(proto, rng));
        }
    }
}
