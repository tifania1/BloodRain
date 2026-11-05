using System.Linq;
using System.Text.RegularExpressions;
using BloodRain.Core.Data;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>'앞뒤 베기(Lv4)' 같은 새 동작이 붙는 레벨을 웹 프로토타입·시뮬레이터가 weapons.json milestones 에서 읽는지.
    /// 레벨 숫자를 다시 박아 두면 데이터를 고쳐도 한쪽만 바뀐다.</summary>
    public class WeaponMilestoneTests
    {
        readonly GameData _d = TestData.Load();

        [Fact]
        public void 프로토타입이_부르는_동작_이름은_데이터에_있다()
        {
            var html = TestData.ReadRepoFile("web", "prototype", "template.html");
            var names = Regex.Matches(html, @"atMs\(w, '([^']+)'\)").Select(m => m.Groups[1].Value).Distinct().ToList();
            Assert.Equal(8, names.Count); // 음파 2겹·회전 찌르기·관통 +1 은 R-17(2026-11-05)
            var all = _d.Weapons.SelectMany(w => w.Milestones?.Values ?? Enumerable.Empty<string>()).ToList();
            foreach (var n in names) Assert.Contains(n, all);
            Assert.DoesNotMatch(@"w\.lv >= \d", html);
        }

        [Fact]
        public void 시뮬레이터가_레벨_숫자를_박아_두지_않는다()
        {
            var src = TestData.ReadRepoFile("core", "BloodRain.Sim", "WeaponModel.cs");
            Assert.DoesNotMatch(@"level >= \d", src);
            foreach (Match m in Regex.Matches(src, @"HasMilestone\(def, level, ""([^""]+)""\)"))
                Assert.Contains(_d.Weapons, w => w.Milestones != null && w.Milestones.ContainsValue(m.Groups[1].Value));
        }
    }
}
