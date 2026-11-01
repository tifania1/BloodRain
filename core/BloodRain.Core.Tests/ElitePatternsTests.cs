using System.Collections.Generic;
using System.IO;
using System.Linq;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using Newtonsoft.Json.Linq;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>정예 패턴(기획서 6장 '정예 패턴 수치', bosspatterns.json elites). 보스와 같은 틀에 2막이 없다.</summary>
    public class ElitePatternsTests
    {
        readonly GameData _d = Load();

        [Fact] public void 정예_패턴은_enemies_json_정예에_있고_몸_피해와_걸음이_같다()
        {
            Assert.NotEmpty(_d.ElitePatterns);
            foreach (var (id, set) in _d.ElitePatterns.Select(kv => (kv.Key, kv.Value)))
            {
                var e = _d.Enemies.Single(x => x.Id == id);
                Assert.Equal("elite", e.Family);
                Assert.Equal(e.ContactDamage, set.ContactDamage);
                Assert.Equal(e.Speed, set.MoveSpeed);
                Assert.Empty(set.Phase2);
                Assert.Equal(set.Order.OrderBy(x => x), set.Patterns.Select(p => p.Id).OrderBy(x => x));
            }
        }

        [Fact] public void 서장_정예_강시_도부수는_패턴이_있다()
        {
            var chapters = JObject.Parse(File.ReadAllText(Path.Combine(DataDir, "chapters.json")));
            var prologue = chapters["chapters"]!.Children<JObject>().Single(c => (string?)c["id"] == "prologue");
            var ev = prologue["events"]!.Children<JObject>().Single(o => (string?)o["kind"] == "elite");
            var set = _d.ElitePatternSet((string)ev["enemy"]!);
            var b = new BossPatterns(set);
            var events = new List<BossPatternEvent>();
            for (int i = 0; i < 60 * 20; i++) events.AddRange(b.Tick(1f / 60f, i < 600 ? 1f : 0.05f));
            Assert.DoesNotContain(events, e => e.Kind == BossPatternEventKind.Phase2);
            Assert.True(events.Count(e => e.Kind == BossPatternEventKind.Resolve) >= 4);
        }

        [Fact] public void 수치_표가_데이터와_같다()
        {
            var set = _d.ElitePatternSet("gangsi_axeman");
            var section = GddSection("정예 패턴 수치");
            Assert.Contains($"첫 패턴 {Num(set.FirstCastSeconds)}초, 그 뒤 {Num(set.IntervalSeconds)}초마다 {set.Pattern(set.Order[0]).NameKo}", section);
            var p = set.Pattern("axe_swing");
            var row = GddTable(section).Single(r => r[0] == p.NameKo);
            Assert.Equal($"{Num(p.TelegraphSeconds)}초", row[1]);
            Assert.Equal($"부채꼴 반경 {Num(p.Radius)}m, {Num(p.AngleDegrees)}°", row[2]);
            float at5 = BossPatterns.ScaleDamage(p.BaseDamage, 5f, 0.8f, _d.Balance.EnemyScaling.DamagePerMinute);
            Assert.Equal($"{Num(p.BaseDamage)}({Num(at5)})", row[3]);
            // 정예 수치 표의 '하는 일' 과 이름이 같다
            var stats = GddTable(GddSection("정예 수치")).Single(r => r[1] == "강시 도부수");
            Assert.Equal(p.NameKo, stats[5]);
        }
    }
}
