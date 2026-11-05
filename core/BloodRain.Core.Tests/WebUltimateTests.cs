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
    /// 오의 모양 숫자(R-15b·R-05b)도 시안이 따로 적지 않고 params 에서 읽는다.
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

        [Fact] public void 오의_모양은_시안이_따로_적지_않고_데이터에서_읽는다()
        {
            var html = Cheonha;
            Assert.DoesNotContain("ULT_TMP", html);
            foreach (var used in new[] { "UP('taiji_muryang').radius", "UP('cheonroe_rak').strikeRadius", "UP('saja_hu').spreadSeconds",
                "UP('mancheon_hwau').poisonSeconds", "UP('palbang_pungu').shotSpeed", "UP('palbang_pungu').shotRadius",
                "UP('palbang_pungu').shotLifeSeconds", "UP('palbang_pungu').volleyGapSeconds", "UP('palbang_pungu').knockback", "UP('yeokryu').width" })
                Assert.Contains(used, html);
        }

        [Fact] public void 오의_모양_숫자는_같은_몸의_무공과_마천패_파도에서_왔다()
        {
            var d = Load();
            UltimateDef U(string id) => d.Ultimates.Single(u => u.Id == id);
            var thunder = Weapon("thunder_talisman");
            Assert.Equal(thunder.GetProperty("strikeRadius").GetSingle(), U("cheonroe_rak").Param("strikeRadius"), 3);
            var palm = Weapon("palm_wind");
            var shot = palm.GetProperty("shot");
            var pb = U("palbang_pungu");
            Assert.Equal(shot.GetProperty("speed").GetSingle(), pb.Param("shotSpeed"), 3);
            Assert.Equal(shot.GetProperty("radius").GetSingle(), pb.Param("shotRadius"), 3);
            Assert.Equal(shot.GetProperty("lifeSeconds").GetSingle(), pb.Param("shotLifeSeconds"), 3);
            Assert.Equal(palm.GetProperty("knockback").GetSingle() * 3, pb.Param("knockback"), 3);
            var mist = Weapon("poison_mist");
            Assert.Equal(mist.GetProperty("damageLv1").GetSingle(), U("mancheon_hwau").Param("poisonDamage"), 3);
            Assert.Equal(mist.GetProperty("tickSeconds").GetSingle(), U("mancheon_hwau").Param("poisonTickSeconds"), 3);
            var wave = d.BossPatterns.Values.SelectMany(b => b.Phase2).Single(p => p.Id == "crimson_reverse_wave");
            Assert.Equal(wave.Width, U("yeokryu").Param("width"), 3);
            Assert.Equal(wave.ProjectileSpeed, U("yeokryu").Param("waveSpeed"), 3);
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

        [Fact] public void 마교_혈공은_Core_BloodArt_와_같은_셈이다()
        {
            var html = Cheonha;
            // R4-14: 진기 × 농도^지수(레벨만), 더 받은 진기 extraXpPerStep 마다 음 yinPerStep, 천마해체로 시작값·심마환영 기준 다시
            foreach (var used in new[] { "Math.pow(Math.max(0, G.dens), ba.xpDensityExponent)", "Math.floor(G.simma.extra / ba.extraXpPerStep)",
                "(steps - G.simma.steps) * ba.yinPerStep", "G.simma.base = G.simma.steps", "G.xp += bloodArtXp(v)", "G.orbSum += v * G.dens" })
                Assert.Contains(used, html);
            Assert.Contains("'inner_demon_might']", html);   // 심마환영이 붙는 깨달음 목록에
        }
    }
}
