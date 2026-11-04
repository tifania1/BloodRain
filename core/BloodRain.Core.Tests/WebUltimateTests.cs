using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using BloodRain.Core.Data;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>
    /// R-16: 웹 천하 시안의 문파 오의가 데이터·Core 셈을 따르는지 글로 본다(브라우저 없이).
    /// 게이지 숫자는 balance.json ultimateCharge, 오의 숫자는 characters.json params 를 빌드가 넣는다.
    /// R-15b·R-05b 가 정해지기 전 임시 값(ULT_TMP)은 backlog 추천안대로 같은 무공 데이터에서 가져온 것인지 본다.
    /// </summary>
    public class WebUltimateTests
    {
        static string Cheonha => ReadRepoFile("web", "lookdev", "cheonha.html");
        static string Build => ReadRepoFile("tools", "build_web_prototype.py");

        static JsonElement Weapon(string id)
        {
            using var doc = JsonDocument.Parse(ReadRepoFile("unity", "BloodRain", "Assets", "Resources", "Data", "weapons.json"));
            return doc.RootElement.GetProperty("weapons").EnumerateArray().Single(w => w.GetProperty("id").GetString() == id).Clone();
        }

        static float TmpValue(string html, string key)
        {
            var m = Regex.Match(html, @"\b" + key + @":\s*([0-9.]+)");
            Assert.True(m.Success, key);
            return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        static string UltTmp(string html)
        {
            int at = html.IndexOf("const ULT_TMP = {");
            Assert.True(at >= 0);
            return html.Substring(at, html.IndexOf("\n};", at) - at);
        }

        [Fact] public void 빌드가_오의_숫자와_게이지를_넣는다()
        {
            Assert.Contains("\"mightMultiplier\", \"summary\", \"params\"", Build);
            Assert.Contains("\"ultimateCharge\")", Build);
        }

        [Fact] public void 아홉_문파_오의를_모두_그린다()
        {
            var html = Cheonha;
            var d = Load();
            foreach (var sect in d.Cheonha.Sects)
                Assert.Contains($"u.id === '{sect.Ultimate}'", html);
        }

        [Fact] public void 박자와_연속과_위력은_Core_UltimateCast_와_같은_셈이다()
        {
            var html = Cheonha;
            // 쓰는 순간 한 번, durationSeconds·mightPerSecond 면 1초마다, 배율 0 은 안 때림
            Assert.Contains("total: u.mightMultiplier <= 0 ? 0 : per > 0 ? Math.max(1, Math.ceil(dur / per - 1e-4)) : 1", html);
            Assert.Contains("rewardAt('double_ultimate')", html);
            Assert.Contains("rewardAt('ultimate_power_')", html);
            // 게이지: 처치·피격·정예, 태극 구간 배율에 무당 조화 보너스 배수
            foreach (var used in new[] { "addUlt(UC.perHit)", "UC.perElite : UC.perKill", "harmonyBonusMultiplier || 1" })
                Assert.Contains(used, html);
        }

        [Fact] public void 임시_값은_backlog_추천안대로_같은_무공_데이터에서_왔다()
        {
            var tmp = UltTmp(Cheonha);
            var thunder = Weapon("thunder_talisman");
            Assert.Equal(thunder.GetProperty("strikeRadius").GetSingle(), TmpValue(tmp, "strikeRadius"), 3);
            var palm = Weapon("palm_wind");
            var shot = palm.GetProperty("shot");
            int windAt = tmp.IndexOf("wind:");
            var wind = tmp.Substring(windAt, tmp.IndexOf('}', windAt) - windAt);
            Assert.Equal(shot.GetProperty("speed").GetSingle(), TmpValue(wind, "speed"), 3);
            Assert.Equal(shot.GetProperty("radius").GetSingle(), TmpValue(wind, "radius"), 3);
            Assert.Equal(shot.GetProperty("lifeSeconds").GetSingle(), TmpValue(wind, "life"), 3);
            Assert.Equal(palm.GetProperty("knockback").GetSingle() * 3, TmpValue(wind, "knockback"), 3);
        }

        [Fact] public void 혈영_분신은_시안_무공_바탕_값을_플레이어와_같이_읽는다()
        {
            var html = Cheonha;
            // R4-11: 무공 셋의 바탕 값은 ART 하나. 플레이어 무공 줄에 옛 숫자가 남지 않는다
            Assert.Contains("const ART = {", html);
            foreach (var old in new[] { "const dmg = 18 * power()", "G.cd = 0.8 * (1 - cdr)", "R = 1.9 * s * M.area", "hurt(f, dmg * 1.4," })
                Assert.DoesNotContain(old, html);
            Assert.Contains("D.behavior === 'mimic'", html);
            Assert.Contains("if (pt.mimic) G.foes[i].mimic = pt.mimic;", html);   // Core Mimicry.For: 소환 패턴 mimic 이 먼저
        }
    }
}
