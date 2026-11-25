using System;
using System.Collections.Generic;
using BloodRain.Core.Progression;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>WX-01: 웹 한 생 판의 전생의 공력이 Core PastLifePower·RunXp.Required 와 같은 셈인지.</summary>
    public class WebPastLifeTests
    {
        static string Proto => ReadRepoFile("web", "prototype", "template.html");

        [Fact] public void 웹_필요_진기가_Core_와_같다()
        {
            var d = Load();
            var curve = new XpCurve(d.Balance.XpCurve);
            var pl = new PastLifePower(d.Balance.Realms,
                new Dictionary<string, int> { ["iryu"] = 20, ["ilryu"] = 5, ["jeoljeong"] = 2 }, "jeoljeong",
                d.Cheonha.PastLife.CutPerCrossing, d.Cheonha.PastLife.MaxCut);
            // 2026-11-26 세션에서 node 로 웹 pastLifeOf·xpReq 를 같은 혼(reached 이류·일류·절정, crossings 20·5·2)으로 돌린 값
            var web = new Dictionary<int, int> { [1] = 2, [9] = 34, [10] = 76, [19] = 148, [20] = 195, [34] = 377, [35] = 390, [49] = 599 };
            foreach (var kv in web)
                Assert.Equal(kv.Value, Math.Max(1, (int)Math.Ceiling(curve.Required(kv.Key) * pl.RequiredScale(kv.Key) - 1e-6)));
        }

        [Fact] public void 웹이_같은_규칙을_쓴다()
        {
            var html = Proto;
            foreach (var used in new[]
            {
                "const PL = DATA.pastLife || { cutPerCrossing: 0, maxCut: 0, wakeCrossings: 30 };",
                "seg <= 0 || seg >= RL_ALL.length || seg >= hi ? 0 : Math.min(PL.maxCut, PL.cutPerCrossing * (cr[RL_ALL[seg].id] || 0))",   // 가장 높은 경지로 가는 구간은 제 속도
                "return G && G.past ? Math.max(1, Math.ceil(base * G.past.scale(level) - 1e-6)) : base;",                                     // RunXp.Required 올림
                "G.past = pastLifeOf(G.soul0);",
                "for (const r of B.realms.slice(1, st.top + 1)) m.crossings[r.id] = (m.crossings[r.id] || 0) + 1;",                         // Soul.EndLife
                "for (const r of B.realms.slice(1, top + 1)) m.crossings[r.id] = (m.crossings[r.id] || 0) + 1;",                            // 서장 생도 한 번
            })
                Assert.Contains(used, html);
            Assert.Contains("\"pastLife\": drop_notes(load(\"cheonha\").get(\"pastLife\", {}))", ReadRepoFile("tools", "build_web_prototype.py"));
        }

        [Fact] public void 웹_천운_덕이_Core_WithBoons_Body_와_같은_규칙이다()
        {
            var d = Load();
            Assert.Single(d.Cheonha.Rebirth.Boons);   // 지금은 천운 하나 — 무작위 후보는 모두 천운
            var html = Proto;
            foreach (var used in new[]
            {
                "if (m.lives > 0 && BOONS.length) for (const c of out) c.boon = BOONS[Math.floor(rnd() * BOONS.length)].id;",   // 첫 환혼은 덕 없음(Core RebirthDraw first)
                "if (boon && BOON_BY[boon]) qiBonus += BOON_BY[boon].rebirthQi || 0;",                                           // Rebirth.Body: 성질과 같은 칸
                "G.body = lifeBody(LS, NEXT_LIFE ? NEXT_LIFE.traits : [], NEXT_LIFE ? NEXT_LIFE.boon : null);",
            })
                Assert.Contains(used, html);
            Assert.Contains("\"maleOnlySects\", \"boons\")", ReadRepoFile("tools", "build_web_prototype.py"));
        }
    }
}
