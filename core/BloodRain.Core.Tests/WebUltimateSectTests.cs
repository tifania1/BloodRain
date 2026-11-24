using System.Linq;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>WP-25: 웹 한 생 판이 몸 문파의 오의를 쓰고, 박자·숫자가 characters.json ultimates params 와 Core UltimateCast 를 따르는지 글로 본다.</summary>
    public class WebUltimateSectTests
    {
        static string Proto => ReadRepoFile("web", "prototype", "template.html");

        [Fact] public void 빌드가_문파_오의를_넣는다()
        {
            Assert.Contains("\"ultimate\": s.get(\"ultimate\")}", ReadRepoFile("tools", "build_web_prototype.py"));
        }

        [Fact] public void 박자는_Core_UltimateCast_와_같다()
        {
            var html = Proto;
            Assert.Contains("per > 0 ? Math.max(1, Math.ceil(dur / per - 1e-4)) : 1", html);
            Assert.Contains("u.mightMultiplier <= 0 ? 0", html);   // 배율 0 은 안 때림
            Assert.Contains("for (let c = 0; c < (G.stats.ultCasts || 1); c++) ultPulse(C.u, C, c)", html);   // 현경 2연속
            Assert.Contains("ultBaseHit() * u.mightMultiplier * G.stats.ultPower", html);
        }

        [Fact] public void 모양_숫자는_데이터에서()
        {
            var html = Proto;
            foreach (var k in new[] { "'strikeRadius'", "'shockSeconds'", "'strikes'", "'radius'", "'stunSeconds'", "'poisonSeconds'", "'freezeSeconds'", "'healthCostPerSecond'", "'lifesteal'", "'damageMultiplier'" })
                Assert.Contains(k, html);
            Assert.Contains("A.resetUltimate === u.id", html);   // 혈공은 천마해체를 쓸 때만 음양이 돌아온다
            foreach (var k in new[] { "'width'", "'directions'", "'volleys'", "'volleyGapSeconds'", "'shotSpeed'", "'shotRadius'", "'shotLifeSeconds'", "'knockback'", "'blocksDamage'" })
                Assert.Contains(k, html);
            Assert.Contains("if (G.taiji > 0) return;   // 태극무량: 받는 피해 무효", html);
            Assert.Contains("if (!b.refl && Math.hypot(p.x - b.x, p.y - b.y)", html);   // 되돌린 탄은 나를 안 맞힘
        }

        [Fact] public void 아홉_문파_오의가_모두_웹_오의_목록에_있다()
        {
            // WP-16: 시안(WebUltimateTests)이 보던 것을 합친 판에서도 — 목록에 없는 오의는 일검으로 바뀌어 버린다(bodyUlt)
            var html = Proto;
            var line = html.Split('\n').Single(l => l.StartsWith("const WEB_ULTS = new Set("));
            foreach (var sect in Load().Cheonha.Sects)
                Assert.Contains($"'{sect.Ultimate}'", line);
        }

        [Fact] public void 게이지는_처치_피격_정예와_태극_배율과_경지_보상을_따른다()
        {
            var html = Proto;
            foreach (var used in new[] { "addUlt(B.ultimateCharge.perHit)", "e.elite ? B.ultimateCharge.perElite : B.ultimateCharge.perKill",
                "B.yinyang.harmony.ultimateChargeBonus * harmonyMul()", "sectT().harmonyBonusMultiplier ?? 1",
                "t === 'double_ultimate'", "/^ultimate_power_(\\d+(?:\\.\\d+)?)$/" })
                Assert.Contains(used, html);
        }
    }
}
