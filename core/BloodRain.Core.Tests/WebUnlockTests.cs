using System.Linq;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>
    /// WP-17: 웹 프로토타입 한 생 판이 무공 해금(기획서 9장 해금 칸·10장 도전 과제)을 Core Soul 과 같은 규칙으로 하는지 글로 본다.
    /// </summary>
    public class WebUnlockTests
    {
        static string Proto => ReadRepoFile("web", "prototype", "template.html");

        [Fact] public void 도전_과제_토큰_모양이_Core_와_같다()
        {
            Assert.Contains("const CHALLENGE_RE = /^(kills|dash|seal_talisman)_(\\d+)_(one_run|total)$/;", Proto);
            var core = ReadRepoFile("unity", "BloodRain", "Assets", "Scripts", "Core", "Data", "Definitions.cs");
            Assert.Contains("@\"^(kills|dash|seal_talisman)_(\\d+)_(one_run|total)$\"", core);
        }

        [Fact] public void 데이터의_해금_토큰을_웹이_다_읽을_수_있다()
        {
            // start · boss:<땅의 주인이 있는 지역> · 도전 과제, 이 셋 말고 다른 모양이 생기면 웹 soulUnlocked 도 고쳐야 한다
            var data = Load();
            var bossRegions = data.Cheonha.Regions.Where(r => r.Boss != null).Select(r => r.Id).ToHashSet();
            foreach (var w in data.Weapons)
            {
                var t = w.Unlock;
                bool ok = t == "start" || (t.StartsWith("boss:") && bossRegions.Contains(t.Substring(5)))
                    || BloodRain.Core.Data.UnlockToken.ParseChallenge(t) != null;
                Assert.True(ok, $"{w.Id} 해금 '{t}' 을 웹이 모른다");
            }
        }

        [Fact] public void 한_생_판은_혼_기록으로_레벨업_풀을_정하고_정산에서_연다()
        {
            var html = Proto;
            Assert.Contains("G.wpool = weaponPoolOf(G.soul0);", html);
            Assert.Contains("for (const id of (G.wpool || START_WEAPONS))", html);
            Assert.Contains("G.dashes = (G.dashes || 0) + 1;", html);
            Assert.Contains("G.talis = (G.talis || 0) + 1;", html);
            Assert.Contains("m.talismans = (m.talismans || 0) + (st.talis || 0);", html);
            Assert.Contains("for (const t of st.newUnlocks || []) if (!m.unlocks.includes(t)) m.unlocks.push(t);", html);
            Assert.Contains("'새로 얻은 무공 · 다음 생부터'", html);
        }

        [Fact] public void 뇌부가_웹에도_있고_데이터_값을_쓴다()
        {
            // WP-18: 곤륜 시작 무공 뇌부(random_lightning)가 웹 한 생 판·레벨업 풀에 있다. 숫자는 weapons.json·evolutions.json 칸에서 읽는다
            var html = Proto;
            Assert.Contains("case 'random_lightning':", html);
            Assert.Contains("'petal_scatter', 'random_lightning']);", html);
            Assert.Contains("'random_lightning', 'ring_wave', 'slow_projectile']);", html);
            Assert.Contains("def.strikeRadius * S.area * (w.evolved ? ep.radiusMultiplier : 1)", html);
            Assert.Contains("delay: def.fuseSeconds", html);
            Assert.Contains("e.stun = Math.max(e.stun || 0, def.shockSeconds)", html);
            Assert.Contains("ep.lightningChains", html);
            var five = Load().Evolutions.First(e => e.Id == "five_thunders");
            foreach (var k in new[] { "strikes", "rangeMultiplier", "radiusMultiplier", "lightningChains", "lightningRange", "lightningFalloff" })
                Assert.True(five.Params!.ContainsKey(k), $"오뢰정법 params 에 {k} 가 없다");
        }
        [Fact] public void 절기_여섯_모양이_params_를_읽는다()
        {
            // WP-19: R-17 여섯 무공의 절기(태극양의검·흑룡탄·혈마수·파천금음·백화난무창·매화만개) 모양이 evolutions.json params 칸을 쓴다
            var html = Proto;
            var d = Load();
            var uses = new (string evo, string[] keys)[]
            {
                ("taiji_liangyi", new[] { "swords", "taijiDamageBonus" }),
                ("black_dragon", new[] { "gatherRadius", "gatherSeconds", "explosionRadius", "damageMultiplier" }),
                ("blood_demon_hand", new[] { "lifesteal", "extremeYinRangeMultiplier" }),
                ("patian_qin", new[] { "stunChance" }),
                ("hundred_flowers", new[] { "directions" }),
                ("plum_full_bloom", new[] { "healPerHit" }),
            };
            foreach (var (evo, keys) in uses)
            {
                var e = d.Evolutions.First(x => x.Id == evo);
                foreach (var k in keys)
                {
                    Assert.True(e.Params!.ContainsKey(k), $"{evo} params 에 {k} 가 없다");
                    Assert.Contains("ep." + k, html);
                }
            }
        }
    }
}
