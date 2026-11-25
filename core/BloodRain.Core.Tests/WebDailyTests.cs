using System;
using BloodRain.Core.Meta;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>MO-01 웹 몫: 웹 한 생 판의 일일 도전이 Core DailyChallenge 와 같은 시드 셈·같은 '혼에 안 남김' 규칙인지 글로 본다.</summary>
    public class WebDailyTests
    {
        static string Proto => ReadRepoFile("web", "prototype", "template.html");

        [Fact] public void 시드_셈이_Core_와_같은_lowbias32_다()
        {
            var html = Proto;
            foreach (var used in new[]
            {
                "d.getUTCFullYear() * 10000 + (d.getUTCMonth() + 1) * 100 + d.getUTCDate()",
                "x = Math.imul(x, 0x7feb352d) >>> 0",
                "x = Math.imul(x, 0x846ca68b) >>> 0",
                "return (x & 0x7fffffff) >>> 0",
            })
                Assert.Contains(used, html);
            // 2026-11-25 세션에서 node 로 웹 dailySeed 를 돌려 본 값(303744310)과 Core 가 같다
            Assert.Equal(303744310, DailyChallenge.Seed(new DateTime(2026, 11, 25, 15, 0, 0, DateTimeKind.Utc)));
        }

        [Fact] public void 봉인석_셋에_열리고_혼은_시작_때로_되돌린다()
        {
            var html = Proto;
            Assert.Contains($"const dailyOpen = m => (m.unlocks || []).includes('mode:{DailyChallenge.ModeId}');", html);
            Assert.Contains("DAILY = { key: dailyKey(), snap: JSON.stringify(soulLoad()) };", html);
            Assert.Contains("if (G.body && G.daily) {", html);      // 정산(recordLife)보다 먼저 갈라진다
            Assert.Contains("dailyRestore(); led.hidden = true;", html);
            Assert.True(html.IndexOf("if (G.body && G.daily) {", StringComparison.Ordinal) < html.IndexOf("const st = lifeSettle(), m = recordLife(st)", StringComparison.Ordinal));
        }

        [Fact] public void 보스_연전은_현경_Lv8_로_주인을_데이터_차례대로_잡몹_없이()
        {
            var html = Proto;
            foreach (var used in new[]
            {
                $"(m.unlocks || []).includes('mode:{BossRush.ModeId}')",
                "const rushOrder = () => CH_REGS.filter(r => r.boss && !r.final);",          // Core CheonhaDef.BossRegions
                "REALMS.find(x => x.id === BR.startRealm)",
                "Math.min(BR.startWeaponLevel, B.levels.weaponMax)",
                "e.hp = e.maxHp = base * lordHpMult(BR.healthMinute);",                     // Core BossRush.Health
                "G.pendingLevels += BR.levelUpsPerKill; G.player.hp = G.player.maxHp;",      // Core BossRush.Defeat
                "if (G.spawnAcc >= 1 && !G.rush) {",
                "if (X.done && (best.s == null || X.clear < best.s)) { mr.bossRushBest = X.clear; soulSave(mr); }",   // Core Soul.RecordBossRush
            })
                Assert.Contains(used, html);
        }
    }
}
