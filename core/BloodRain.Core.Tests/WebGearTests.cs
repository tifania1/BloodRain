using System.Linq;
using BloodRain.Core.Build;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>GX-01~04: 웹 한 생 판의 문파 병기가 Core Build.Gear·GearSlot·Life.DefeatBoss 와 같은 규칙인지 글로 본다.
    /// 웹 셈이 바뀌면(또는 Core 가 바뀌면) 여기서 걸린다.</summary>
    public class WebGearTests
    {
        static string Proto => ReadRepoFile("web", "prototype", "template.html");
        static string Build => ReadRepoFile("tools", "build_web_prototype.py");

        [Fact] public void 빌드가_gear_json_을_넣는다()
        {
            Assert.Contains("\"gear\": drop_notes(load(\"gear\"))", Build);
        }

        [Fact] public void 떨어뜨리기와_줍기가_Core_와_같다()
        {
            var html = Proto;
            foreach (var used in new[]
            {
                "Math.max(0, Math.min(1, GEAR.drop.chance + Math.max(0, luck) * GEAR.drop.luckBonus))",   // Gear.DropChance
                "if (!G.body || isPro() || !G.chRegion) return null;",                                     // Life.RollGearDrop: 서장 없음
                "if (g.guaranteed.includes(enemyId)) return g.id;",                                       // 확정 정예는 난수 안 씀
                "if (!g.droppers.includes(enemyId)) return null;",
                "G.gear.grade = Math.min(G.gear.grade + 1, GEAR.grades.length - 1)",                       // GearSlot.Pick 같은 병기
                "else G.gear = { id, grade: 0 };",                                                         // 다른 병기는 범병부터
                "gearDropAt(e.x, e.y, gearRollDrop(",                                                      // killEnemy
                "else if (dr.kind === 'gear')",                                                            // pickup
            })
                Assert.Contains(used, html);
        }

        [Fact] public void 보너스_무게_6단_땅의_주인이_Core_와_같다()
        {
            var html = Proto;
            foreach (var used in new[]
            {
                "return !!sc && sc.members.includes(id) && !!W_BY_ID[id];",          // Gear.AppliesTo: 문파 무공 전부(심법 빼고)
                "d *= gearDamageMul(w.id);",                                          // Life.GearDamageMultiplier
                "area: G.stats.area * gam",                                           // Life.GearAreaMultiplier
                "if (w <= 0 || w === 1) return 1;",                                   // Gear.OfferWeight
                "return sc && sc.members.includes(id) ? w : 1;",
                "* gearOfferWeight(c.id)",                                            // Loadout.RollLevelUp gear:
                "w.evolved && gearGrade().sixth && GEAR_BY[G.gear.id].weapon === w.id", // Gear.IsSixth
                "if (gearGradeUpIfSect(r.sect))",                                     // DefeatBoss GradeUpIfSect
                "GEAR.gear.find(g => g.source === 'relic:' + r.id)",                  // 유품 병기 계정 첫 처치
            })
                Assert.Contains(used, html);
        }

        [Fact] public void 웹이_쓰는_병기_칸_이름이_데이터에_있다()
        {
            var data = Load();
            Assert.NotEmpty(data.Gear.Grades);
            Assert.All(data.Gear.Gear, g => Assert.False(string.IsNullOrEmpty(g.Hanja)));   // 병기 칸·떨어진 병기에 한자 끝 글자를 씀
            Assert.All(data.Gear.Grades, g => Assert.False(string.IsNullOrEmpty(g.Hanja)));
            Assert.Contains(data.Gear.Gear, g => g.Source == "relic:songshan");
            Assert.True(data.Gear.Gear.Count(g => g.Sect != null) >= 9);
        }
    }
}
