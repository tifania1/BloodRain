using System;
using System.Linq;
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
            Assert.Contains("DAILY = { key: dailyKey(), seed: dailySeed(), snap: JSON.stringify(soulLoad()) };", html);
            Assert.Contains("if (G.body && G.daily) {", html);      // 정산(recordLife)보다 먼저 갈라진다
            Assert.Contains("dailyRestore(); led.hidden = true;", html);
            Assert.True(html.IndexOf("if (G.body && G.daily) {", StringComparison.Ordinal) < html.IndexOf("const st = lifeSettle(), m = recordLife(st)", StringComparison.Ordinal));
        }

        [Fact] public void 보스_연전은_현경까지_레벨업을_다_골라_주인을_데이터_차례대로_잡몹_없이()
        {
            var html = Proto;
            foreach (var used in new[]
            {
                $"(m.unlocks || []).includes('mode:{BossRush.ModeId}')",
                "const rushOrder = () => CH_REGS.filter(r => r.boss && !r.final);",          // Core CheonhaDef.BossRegions
                "REALMS.find(x => x.id === BR.startRealm)",
                "if (BR.pickLevelsToRealm) G.pendingLevels += R.level - lv0;",             // Core BossRush.StartLevelUps
                "e.hp = e.maxHp = base * lordHpMult(BR.healthMinute);",                     // Core BossRush.Health
                "G.pendingLevels += BR.levelUpsPerKill; G.player.hp = G.player.maxHp;",      // Core BossRush.Defeat
                "if (G.spawnAcc >= 1 && !G.rush) {",
                "if (X.done && (best.s == null || X.clear < best.s)) { mr.bossRushBest = X.clear; soulSave(mr); }",   // Core Soul.RecordBossRush
            })
                Assert.Contains(used, html);
        }

        // DD-02: 웹 dailyBody 는 .NET System.Random 수열을 옮긴 netRandom 으로 Core Rebirth.Roll(아홉 문파, 1명)과 같은 차례로 뽑는다.
        //   2026-11-26 세션에서 node 로 웹 dailyBody 를 2026-11-01~12-10 사십 날 돌려 Core DailyChallenge.Body 와 문파·성질·성별·LifeRandom 첫 수까지 다 같음을 봤다.
        //   아래 날들은 그때 본 값 — Core 뽑기 차례가 바뀌면 여기가 깨지고, 그러면 웹 netRandom 쪽 차례도 같이 고친다.
        [Theory]
        [InlineData(2026, 11, 25, "namgung", "eumche", true)]
        [InlineData(2026, 11, 28, "shaolin", "daesik,danmyeong", false)]
        [InlineData(2026, 12, 4, "wudang", "cheonsaeng,danmyeong", true)]
        [InlineData(2026, 12, 7, "wudang", "", false)]
        public void 일일_도전_몸이_웹과_Core_가_같다(int y, int mo, int d, string sect, string traits, bool female)
        {
            var c = Load().Cheonha;
            var body = DailyChallenge.Body(c, DailyChallenge.Seed(new DateTime(y, mo, d, 0, 0, 0, DateTimeKind.Utc)));
            Assert.Equal(sect, body.Sect.Id);
            Assert.Equal(traits, string.Join(",", body.Traits.Select(t => t.Id)));
            Assert.Equal(female, body.Female);

            var html = Proto;
            foreach (var used in new[]
            {
                "let mj = 161803398 - (seed === -2147483648 ? MBIG : Math.abs(seed | 0)), mk = 1, ii;",   // .NET Random 시드 펼치기
                "for (let i = 1; i < 56; i++) { a[i] -= a[1 + (i + 30) % 55]; if (a[i] < 0) a[i] += MBIG; }",
                "if (++inext >= 56) inext = 1; if (++inextp >= 56) inextp = 1;",
                "const c = { sect: all[R.next(0, all.length)], traits: rollTraitsNet(R) };",
                "c.female = !(RB.maleOnlySects || []).includes(c.sect) && R.nextDouble() < (RB.femaleRatio || 0);",
                "EV_RND = DAILY ? netRandom(DAILY.seed ^ 0x5bd1e995).nextDouble : Math.random;",       // Core DailyChallenge.LifeRandom
            })
                Assert.Contains(used, html);
        }
    }
}
