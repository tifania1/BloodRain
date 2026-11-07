using System.Linq;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>웹 프로토타입 비전(proto: true)이 synergies.json 값과 Core 셈(RunRecord.Tick, SecretEffects)을 따르는지 글로 본다.</summary>
    public class WebSecretTests
    {
        static string Proto => ReadRepoFile("web", "prototype", "template.html");

        [Fact] public void proto_비전은_웹이_연다()
        {
            var html = Proto;
            foreach (var s in Load().Synergies.Secrets.Where(s => s.Proto))
                Assert.Contains($"unlockSecret('{s.Id}')", html);
        }

        [Fact] public void 무명검_값은_데이터에서()
        {
            var html = Proto;
            Assert.Contains("w.id === SECRET.mumyeong.target) d *= 1 + SECRET.mumyeong.params.damageBonus", html);
            Assert.Contains("SECRET.mumyeong.params.ultimateChargeMultiplier", html);
            Assert.DoesNotContain("secrets.has('mumyeong')) d *= 1.5", html);
        }

        [Fact] public void 무극_판정과_효과가_Core_와_같은_셈()
        {
            var html = Proto;
            // RunRecord.Tick: 0 이 아니면 0, 방금 0 으로 돌아왔으면 0 부터, 이어서 0 이면 더함
            Assert.Contains("S.zeroT = G.yy === 0 && S.zeroLast === 0 ? (S.zeroT || 0) + dt : 0; S.zeroLast = G.yy;", html);
            Assert.Contains("Object.entries(mg.levels).every(([id, lv]) => lvOf(id) >= lv) && S.zeroT >= mg.hold.seconds", html);
            // SecretEffects.AspectDamageBonus: 음·양 무공 모두, 속성 없는 무공은 그대로
            Assert.Contains("if (G.syn.secrets.has('mugeuk')) return aspect === 'neutral' ? 1 : 1 + SECRET.mugeuk.params.aspectDamageBonus;", html);
            // 게이지가 사라짐: 움직이지 않고 HUD 를 숨김
            Assert.Contains("if (G.syn.secrets.has('mugeuk')) return;   // 무극", html);
        }
        [Fact] public void 취선_판정과_효과가_Core_와_같은_셈()
        {
            var html = Proto;
            // 보갑을 연 순간의 사진(RunRecord.OpenChest): 레벨·음식 수·기혈 비율 이하
            Assert.Contains("(G.food || 0) >= cr.counts.foodEaten && p.hp <= p.maxHp * cr.event.maxHpRatio) G.syn.drunkPend = true;", html);
            // SecretEffects.FoodMightBonus: 찾은 뒤 먹은 수 × mightPerFood 를 공력에 더함
            Assert.Contains("&& G.syn.secrets.has('chwiseon')) G.drunkFood = (G.drunkFood || 0) + 1;", html);
            Assert.Contains("function drunkMight() { return (G.drunkFood || 0) * SECRET.chwiseon.params.mightPerFood; }", html);
            Assert.Contains("+ (G.tombMight || 0) + drunkMight())", html);
            // SecretEffects.Evades: 기혈역류(self)는 흘리지 않음
            Assert.Contains("kind !== 'self' && G.syn.secrets.has('chwiseon') && Math.random() < SECRET.chwiseon.params.evadeChance", html);
        }
    }
}
