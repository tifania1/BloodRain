using System.Linq;
using System.Text.Json;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>
    /// WP-20: 웹 한 생 판이 문파 고유 능력(cheonha.json sects traitEffect, Core SectTrait)을 데이터에서 읽는지 글로 본다.
    /// 데이터에 새 traitEffect 칸이 생기면 웹이 그 칸을 모른다고 여기서 막힌다.
    /// </summary>
    public class WebSectTraitTests
    {
        static string Proto => ReadRepoFile("web", "prototype", "template.html");

        // 웹이 읽는 traitEffect 칸과 그 칸을 읽는 글(template.html 안)
        static readonly (string key, string uses)[] Known =
        {
            ("stats", "sect.stats"),   // 빌드가 traitEffect.stats 를 sects[].stats 로 꺼낸다
            ("aboveHealthRatio", "T.aboveHealthRatio"), ("aboveHealthStats", "T.aboveHealthStats"),
            ("movingStats", "T.movingStats"),
            ("lowHealthCooldownReduction", "T.lowHealthCooldownReduction"), ("lowHealthFullAt", "T.lowHealthFullAt"),
            ("healingMultiplier", "sectT().healingMultiplier"),
            ("harmonyBonusMultiplier", "sectT().harmonyBonusMultiplier"),
            ("nearEnemySlow", "T.nearEnemySlow"), ("nearEnemySlowRadius", "T.nearEnemySlowRadius"),
            ("poisonImmune", "sectT().poisonImmune"),
            ("poisonedTargetDamage", "sectT().poisonedTargetDamage"),
            ("ignoresWater", null!),   // 물길(WP-13b)이 아직 웹에 없다 — 물길을 옮길 때 같이
        };

        [Fact] public void 빌드가_고유_능력과_음양_시작을_넣는다()
        {
            var build = ReadRepoFile("tools", "build_web_prototype.py");
            Assert.Contains("\"trait\": {k: v for k, v in (s.get(\"traitEffect\") or {}).items() if k != \"stats\"}", build);
            Assert.Contains("\"yinyangStart\": s.get(\"yinyangStart\", 0), \"harmonyRange\": s.get(\"harmonyRange\")", build);
        }

        [Fact] public void 데이터의_고유_능력_칸을_웹이_다_안다()
        {
            using var doc = JsonDocument.Parse(ReadRepoFile("unity", "BloodRain", "Assets", "Resources", "Data", "cheonha.json"));
            var keys = doc.RootElement.GetProperty("sects").EnumerateArray()
                .Where(s => s.TryGetProperty("traitEffect", out _))
                .SelectMany(s => s.GetProperty("traitEffect").EnumerateObject().Select(p => p.Name))
                .Distinct().ToList();
            var html = Proto;
            foreach (var k in keys)
            {
                var hit = Known.FirstOrDefault(x => x.key == k);
                Assert.True(hit.key != null, $"웹이 모르는 traitEffect 칸: {k}");
                if (hit.uses != null) Assert.Contains(hit.uses, html);
            }
        }

        [Fact] public void 그때그때_바뀌는_효과가_싸움에_들어간다()
        {
            var html = Proto;
            Assert.Contains("d *= (G.stats.might + sectBonus('Might'))", html);   // 남궁
            Assert.Contains("S.cdr + sectBonus('CooldownReduction')", html);   // 마교 쾌속, 상한은 합친 값에
            Assert.Contains("const ev = sectBonus('Evade')", html);   // 수로채 회피는 피격 직전에 굴림(Core Evade.Roll)
            Assert.Contains("B.yinyang.harmony.cooldownReduction * harmonyMul()", html);   // 무당
            Assert.Contains("B.yinyang.harmony.ultimateChargeBonus * harmonyMul()", html);
            Assert.Contains("regen * dt * healMul()", html);   // 마교 회복 절반
            Assert.Contains("const ns = nearSlow(d)", html);   // 곤륜
            Assert.Contains("!sectT().poisonImmune) p.hp -=", html);   // 당가 독안개
            Assert.Contains("G.yy = clamp(G.body.yy || 0, B.yinyang.min, B.yinyang.max)", html);   // 음양 시작
        }
    }
}
