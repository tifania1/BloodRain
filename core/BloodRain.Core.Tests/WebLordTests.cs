using System.Linq;
using System.Text.RegularExpressions;
using BloodRain.Core.Meta;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>
    /// WP-13e 첫 조각: 1장 프로토타입 한 생 판에 땅의 주인(7장, Core BossAwakening)을 옮긴 것이
    /// 데이터 값을 읽고 Core 와 같은 규칙(깨어남·싸움 원·기혈 배율·처치 보상)을 쓰는지 글로 본다.
    /// </summary>
    public class WebLordTests
    {
        static string Proto => ReadRepoFile("web", "prototype", "template.html");

        [Fact] public void 빌드가_땅의_주인_규칙과_패턴과_지역_보스를_넣는다()
        {
            var build = ReadRepoFile("tools", "build_web_prototype.py");
            Assert.Contains("\"lordRules\": drop_notes(load(\"cheonha\")[\"bosses\"])", build);
            Assert.Contains("\"lordPatterns\":", build);
            Assert.Contains("\"boss\": r.get(\"boss\"), \"sealStone\": r.get(\"sealStone\")", build);
        }

        [Fact] public void 규칙은_데이터_값을_쓴다()
        {
            var html = Proto;
            Assert.Contains("LR.awakenFrom", html);
            Assert.Contains("LR.engageViewMultiple", html);
            Assert.Contains("LR.awakenGraceSeconds", html);
            Assert.Contains("LR.spawnTargetScale", html);
            Assert.Contains("LR.healthCurve", html);
            Assert.Contains("LR.firstKill : LR.repeatKill", html);
            // 장막 반경은 축척 모드 장막과 같은 식, 기혈 배율은 Core HealthMultiplier 와 같은 꼴(마지막 두 점 기울기로 늘림)
            Assert.Contains("WD.view0Meters * Math.pow(WD.scalePerRealm, t) * WD.boundaryViewMultiple", html);
            Assert.Contains("return b[1] + (b[1] - a[1]) / (b[0] - a[0]) * (m - b[0]);", html);
        }

        [Fact] public void 정산_줄과_혼_기록에_봉인석과_처치가_들어간다()
        {
            var html = Proto;
            Assert.Contains("땅의 주인 첫 처치", html);
            Assert.Contains("혈영 다시 처치", html);
            Assert.Contains("m.bossKills[k.boss] = (m.bossKills[k.boss] || 0) + 1;", html);
            Assert.Contains("m.stones.push(k.stone)", html);
            Assert.Contains("m.unlocked.push(k.sect)", html);
            Assert.Contains("s.unlock === 'boss:' + r.id", html);
        }

        [Fact] public void 종장을_깨면_회차대로_엔딩을_고르고_혼_기록에_새긴다()
        {
            var build = ReadRepoFile("tools", "build_web_prototype.py");
            Assert.Contains("\"endings\": {\"list\": drop_notes(load(\"dialogue\")[\"endings\"][\"list\"])", build);
            Assert.Contains("(\"endingsFirst\", \"endingsSecond\", \"trueEndingDistinctSects\", \"trueEndingManuscripts\", \"resets\")", build);
            var html = Proto;
            // 고를 수 있는 엔딩은 Core Soul.Endings 처럼 회차로, 진엔딩은 조건 셋
            Assert.Contains("(m.cycle || 1) >= 2 ? END.endingsSecond : END.endingsFirst", html);
            Assert.Contains("END.trueEndingManuscripts", html);
            Assert.Contains("END.trueEndingDistinctSects", html);
            // 봉인은 회귀(cycle.resets 의 봉인석만 지우고 회차 +1), 계승은 혈마 모드 토큰(Soul.BloodDemonModeToken)
            Assert.Contains("if (k === 'sealStones') m.stones = []; m.cycle = (m.cycle || 1) + 1;", html);
            Assert.Contains("'mode:blood_demon'", html);
            Assert.Equal(Soul.BloodDemonModeToken, "mode:blood_demon");
            Assert.Equal(new[] { "sealStones" }, Load().Cheonha.Cycle.Resets.ToArray());
            // 혈영은 이번 회차 봉인석으로 본다(Core KillBoss firstInCycle) — 회귀 뒤 다시 첫 처치
            Assert.Contains("const lordShadow = r => r.sealStone ? (G.soul0.stones || []).includes(r.sealStone)", html);
        }

        [Fact] public void 혈월_회차는_데이터_배율을_적과_보상에_곱한다()
        {
            var build = ReadRepoFile("tools", "build_web_prototype.py");
            Assert.Contains("\"bloodmoon\": drop_notes(load(\"cheonha\")[\"cycle\"][\"bloodmoon\"])", build);
            var html = Proto;
            Assert.Contains("G.moon = (G.soul0.cycle || 1) >= 2", html);   // Core Soul.IsBloodMoon => Cycle >= 2
            Assert.Contains("e.hp *= BM.enemyHealth; e.maxHp *= BM.enemyHealth; e.speed *= BM.enemySpeed;", html);
            // 윤회의 기 혈월 줄은 Core RebirthQiLedger.Settle 의 fromMoon 과 같은 꼴: (줄 합 + 성질) × (배율 − 1) 반올림
            Assert.Contains("Math.round(rows.reduce((a, r) => a + r[2], 0) * (BM.rewards - 1))", html);
            Assert.Contains("* (1 + (synStats().Greed || 0)) * moonR()", html);
        }

        [Fact] public void 꽃잎_나선_셈은_시안과_글자_그대로()
        {
            var sian = ReadRepoFile("web", "lookdev", "cheonha.html");
            foreach (var line in new[] {
                "const spiralShotsPerArm = pt => pt.hitIntervalSeconds > 0 ? Math.max(1, Math.ceil(pt.leaveSeconds / pt.hitIntervalSeconds - 1e-4)) : 1;",
                "const spiralShotDegrees = (pt, start, arm, shot) => start + 360 * arm / Math.max(1, pt.count) + pt.turnDegreesPerSecond * shot * pt.hitIntervalSeconds;" })
            {
                Assert.Contains(line, Proto);
                Assert.Contains(line, sian);
            }
        }

        /// <summary>종장을 뺀 땅의 주인 패턴은 1막·2막 모두 프로토타입이 그린다: 차례로 도는 것은 ELITE_SHAPES, 2막 continuous 는 lordContinuous 가 안다.</summary>
        [Fact] public void 땅의_주인_패턴은_모두_프로토타입이_안다()
        {
            var html = Proto;
            var m = Regex.Match(html, @"const ELITE_SHAPES = new Set\(\[([^\]]*)\]\)");
            var known = Regex.Matches(m.Groups[1].Value, @"'([a-z_]+)'").Select(x => x.Groups[1].Value).ToHashSet();
            var cont = html.Substring(html.IndexOf("function lordContinuous(e, P)"));
            cont = cont.Substring(0, cont.IndexOf("\n}\n"));
            var data = Load();
            foreach (var r in data.Cheonha.BossRegions)
            {
                var set = data.BossPatterns[r.Boss!];
                foreach (var p in set.Patterns.Concat(set.Phase2))
                {
                    bool ok = p.Continuous ? cont.Contains($"P.shape === '{p.Shape}'") : known.Contains(p.Shape);
                    Assert.True(ok, $"{r.Boss} {p.Id}({p.Shape}) 를 프로토타입이 모른다");
                }
            }
        }

        /// <summary>프로토타입은 아는 패턴 모양(ELITE_SHAPES)만 쓴다. 종장을 뺀 땅의 주인마다 1막에 적어도 하나는 있어야 싸움이 된다.</summary>
        [Fact] public void 땅의_주인마다_프로토타입이_아는_1막_패턴이_있다()
        {
            var m = Regex.Match(Proto, @"const ELITE_SHAPES = new Set\(\[([^\]]*)\]\)");
            Assert.True(m.Success);
            var known = Regex.Matches(m.Groups[1].Value, @"'([a-z_]+)'").Select(x => x.Groups[1].Value).ToHashSet();
            var data = Load();
            foreach (var r in data.Cheonha.BossRegions)
            {
                Assert.True(data.BossPatterns.TryGetValue(r.Boss!, out var set), r.Boss);
                Assert.True(set!.Patterns.Any(p => known.Contains(p.Shape)),
                    $"{r.Boss} 에 프로토타입이 아는 1막 패턴이 없다");
            }
        }


        [Fact] public void 종장은_봉인석과_경지를_갖추면_깨어나고_혈영_일곱이_차례로()
        {
            var build = ReadRepoFile("tools", "build_web_prototype.py");
            Assert.Contains("\"finalBoss\": {k: drop_notes(load(\"bosspatterns\")[\"final\"][k]) for k in (\"boss\", \"shadows\", \"shadowHealthRatio\", \"stages\")}", build);
            var html = Proto;
            // Core BossAwakening.ShouldBeAwake: 봉인석 finalRequires.sealStones 와 경지 finalRequires.realm
            Assert.Contains("if (sealHeld() < LR.finalRequires.sealStones || realmT < 0 || tierNow() < realmT) return;", html);
            // Core FinalFight.BaseHealth: 그 보스 기준 체력 × shadowHealthRatio(실제는 healthCurve 를 더 곱함)
            Assert.Contains("hp = base * FB.shadowHealthRatio * k", html);
            Assert.Contains("if (next < FB.shadows.length) return finalShadow(r, next, { x: e.x, y: e.y });", html);
            // 혈영 일곱이 모두 땅의 주인이고 프로토타입이 아는 패턴이 있다
            var data = Load();
            Assert.Equal(7, data.FinalBoss.Shadows.Count);
            Assert.All(data.FinalBoss.Shadows, id => Assert.Contains(data.Cheonha.BossRegions, r => r.Boss == id));
            // 혈마 세 단계: 단계 health × healthCurve, 단계 패턴 모양을 다 안다(피바다 shrinking_islands 는 싸움터 크기 R4-07 뒤)
            Assert.Contains("hp = st.health * k", html);
            foreach (var st in data.FinalBoss.Stages)
                foreach (var p in st.Set.Patterns)
                    Assert.Contains($"'{p.Shape}'", html);
        }
    }
}
