using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>
    /// 웹 한 생 판의 혈마의 그림자(7장 '생의 끝', T-42)가 Core LifeDirector.ShadowStep·BloodShadow.Strength 와 같은 셈을 cheonha.json lifeEnd 에서 읽는지 글로 본다.
    /// </summary>
    public class WebShadowTests
    {
        static string Proto => ReadRepoFile("web", "prototype", "template.html");

        [Fact] public void 빌드가_lifeEnd_를_넣는다()
        {
            var build = ReadRepoFile("tools", "build_web_prototype.py");
            Assert.Contains("\"lifeEnd\": drop_notes(load(\"cheonha\")[\"lifeEnd\"])", build);
        }

        [Fact] public void 단계와_세기가_Core_와_같은_식()
        {
            var html = Proto;
            // ShadowStep: 현경 아래·봉인석 다 모음·shadowFromMinute 전이면 0, 아니면 1 + floor((분 - 시작) / 간격)
            Assert.Contains("if (top < 0 || tierNow() < top || sealHeld() >= LR.finalRequires.sealStones || G.gm < LE.shadowFromMinute) return 0;", html);
            Assert.Contains("1 + Math.floor((G.gm - LE.shadowFromMinute) / LE.shadowStepMinutes + 1e-4)", html);
            // Strength: 피해 = contactDamage × damageGrowth^(n-1), 속도 = moveSpeed + speedPerStep × (n-1)(상한은 있으면), 적 피해 = 1 + enemyDamagePerStep × (n-1)
            Assert.Contains("dmg: d.contactDamage * Math.pow(d.damageGrowth, k)", html);
            Assert.Contains("let speed = d.moveSpeed + d.speedPerStep * k; if (d.maxSpeed > 0) speed = Math.min(d.maxSpeed, speed);", html);
            Assert.Contains("enemyMul: 1 + d.enemyDamagePerStep * k", html);
            // TeleportsOn: 2단계부터 오를 때, 이미 더 가까우면 그대로
            Assert.Contains("if (gap > 0 && S.step > 0 && Math.hypot(S.x - p.x, S.y - p.y) > gap)", html);
            // 그림자 자신·기혈역류는 적 피해 배율을 받지 않는다
            Assert.Contains("if (G.shadow && kind !== 'shadow' && kind !== 'self') dmg *= shadowStrength(G.shadow.step).enemyMul;", html);
        }

        [Fact] public void 쓰러지면_정산_shadow_줄_좌화면_귀환_shadow_줄()
        {
            var html = Proto;
            Assert.Contains("(DATA.settleCaps || []).find(l => l.when === 'shadow')", html);
            Assert.Contains("m.lastShadow && !m.seen.includes('mok.zuohwa_shadow') ? 'zuohwa_shadow'", html);
            var d = Load();
            Assert.Contains(d.Dialogue.Mok.Lines, l => l.Id == "zuohwa_shadow");
        }
    }
}
