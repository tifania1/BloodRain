using System.Linq;
using BloodRain.Core.Data;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>정예 데이터(T-32, 기획서 6장 '정예 수치').</summary>
    public class EliteDataTests
    {
        readonly GameData _d = Load();

        [Fact] public void 정예_표의_모든_정예가_enemies에_있고_elite_계열이다()
        {
            foreach (var id in _d.Elites.Values.SelectMany(x => x))
            {
                var e = _d.Enemy(id);
                Assert.Equal("elite", e.Family);
                Assert.True(e.Health > 0 && e.Speed > 0 && e.ContactDamage > 0);
                Assert.False(string.IsNullOrEmpty(e.Behavior));
                Assert.Null(e.Chapter); // 장 이름이 붙으면 지역 고유 적으로 섞여 버린다
            }
        }

        [Fact] public void 정예_수치_표가_데이터와_같다()
        {
            var rows = GddTable(GddSection("정예 수치"));
            Assert.Equal(_d.Elites.Values.Sum(v => v.Count), rows.Count);
            foreach (var id in _d.Elites.Values.SelectMany(x => x))
            {
                var e = _d.Enemy(id);
                var r = rows.Single(x => x[1] == e.NameKo);
                Assert.Equal(e.Health.ToString("N0", System.Globalization.CultureInfo.InvariantCulture), r[2]);
                Assert.Equal(Num(e.Speed), r[3]);
                Assert.Equal(Num(e.ContactDamage), r[4]);
            }
        }

        [Fact] public void 숭산_정예는_웹_프로토타입이_데이터에서_읽는다()
        {
            // 2026-11-01 부터 프로토타입은 숫자를 박지 않고 enemies.json 의 정예 id 를 가리킨다.
            var html = ReadRepoFile("web", "prototype", "template.html");
            Assert.Contains("['dongin', 'dongin', 'slam'], ['gyeyul', 'gyeyul_monk', 'palm'], ['abbot', 'acting_abbot', 'bell']", html);
            Assert.DoesNotContain("hp: 700, speed: 55", html);
            Assert.Equal(new[] { "dongin", "gyeyul_monk", "acting_abbot" }, _d.Elites["chapter1"]);
            Assert.Equal(700, _d.Enemy("dongin").Health);
            Assert.Equal(1500, _d.Enemy("gyeyul_monk").Health);
            Assert.Equal(2600, _d.Enemy("acting_abbot").Health);
        }

        [Fact] public void 차례가_뒤일수록_단단하다()
        {
            foreach (var (ch, list) in _d.Elites.Select(kv => (kv.Key, kv.Value)))
                for (int i = 1; i < list.Count; i++)
                    Assert.True(_d.Enemy(list[i]).Health > _d.Enemy(list[i - 1]).Health, $"{ch}: {list[i]}");
        }
        [Fact] public void 지역별_고유_적_표의_모든_줄에_데이터가_있고_수치가_같다()
        {
            var rows = GddTable(GddSection("지역별 고유 적"));
            foreach (var r in rows)
            {
                var region = _d.Cheonha.Regions.Single(g => g.NameKo == r[0]);
                var e = _d.Enemies.Single(x => x.Chapter == region.Chapter && r[1].StartsWith(x.NameKo));
                Assert.Equal(Num(e.Health), r[3]);
                Assert.Equal(Num(e.Speed), r[4]);
                Assert.Equal(Num(e.ContactDamage), r[5]);
            }
            Assert.Equal(_d.Enemies.Count(e => e.Chapter != null), rows.Count);
        }

        [Fact] public void 땅의_주인이_있는_일곱_땅_모두_고유_적이_있다()
        {
            var regions = new BloodRain.Core.World.Regions(_d);
            foreach (var r in _d.Cheonha.BossRegions) Assert.NotEmpty(regions.LocalEnemies(r.Id));
            Assert.Equal("ice", _d.Enemy("ice_gangsi").Family);
            Assert.Equal(_d.Gimmicks.Campfire.SlowedFamily, _d.Enemy("ice_gangsi").Family);
        }
    }
}
