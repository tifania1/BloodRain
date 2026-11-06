using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>WP-23: 웹 한 생 판 마교 몸의 혈공·마공 깨달음이 데이터 값과 Core BloodArt·InsightEffects 규칙을 따르는지 글로 본다.</summary>
    public class WebBloodArtTests
    {
        static string Proto => ReadRepoFile("web", "prototype", "template.html");

        [Fact] public void 빌드가_혈공_값을_넣는다()
        {
            var build = ReadRepoFile("tools", "build_web_prototype.py");
            Assert.Contains("\"bloodArt\": drop_notes(s[\"bloodArt\"]) if s.get(\"bloodArt\") else None", build);
        }

        [Fact] public void 진기는_농도_네제곱근이고_윤회의_기는_구슬_값이다()
        {
            var html = Proto;
            Assert.Contains("v * Math.pow(Math.max(0, G.dens || 1), A.xpDensityExponent)", html);
            Assert.Contains("Math.floor(G.bExtra / A.extraXpPerStep)", html);
            Assert.Contains("(steps - (G.bSteps || 0)) * A.yinPerStep", html);
            Assert.Contains("G.orbV += o.v; G.orbW += o.v * G.dens; } gainXp(bloodOrbXp(o.v))", html);
        }

        [Fact] public void 마공은_앞자리에_데이터_수만큼_나온다()
        {
            var html = Proto;
            Assert.Contains("Math.min(n, ba.demonicInsightsPerBreakthrough || 0)", html);
            Assert.Contains("const opts = dem.concat(", html);
            Assert.Contains("const n = (G.body ? DATA.insights.choices.base : INSIGHT_CHOICES) + (more || 0);", html);
        }

        [Fact] public void 마공_효과가_데이터_값을_쓴다()
        {
            var html = Proto;
            Assert.Contains("s.might += IP.hyeolhae_gong.mightBonus; s.maxHealth -= IP.hyeolhae_gong.maxHealthPenalty;", html);
            Assert.Contains("s.lifesteal += IP.heupyeol.lifesteal", html);
            Assert.Contains("healMul() * (has('heupyeol') ? 0 : 1)", html);
            Assert.Contains("hr < IP.hyeolgwang.healthThreshold) v += IP.hyeolgwang.cooldownReduction", html);
            Assert.Contains("taken *= 1 + IP.hyeolgwang.damageTaken", html);
            Assert.Contains("Math.min(IP.simma.mightMax, Math.max(0, (G.bSteps || 0) - (G.simmaBase || 0)) * IP.simma.mightPerStep)", html);
            Assert.Contains("if (o.id === 'simma') G.simmaBase = G.bSteps || 0;", html);
        }
    }
}
