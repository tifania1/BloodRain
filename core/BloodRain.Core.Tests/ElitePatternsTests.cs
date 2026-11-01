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

        [Fact] public void 숭산_정예_셋_표가_데이터와_같다()
        {
            var section = GddSection("정예 패턴 수치");
            var rows = GddTable(section).Where(r => r.Length == 6).ToList();
            foreach (var id in new[] { "dongin", "gyeyul_monk", "acting_abbot" })
            {
                var set = _d.ElitePatternSet(id);
                Assert.Equal(3f, set.FirstCastSeconds);
                Assert.Equal(3.75f, set.IntervalSeconds);
                var p = set.Pattern(Assert.Single(set.Order));
                var name = _d.Enemies.Single(e => e.Id == id).NameKo;
                var row = rows.Single(r => r[0] == name);
                Assert.Equal(p.NameKo, row[1]);
                Assert.Equal($"{Num(p.TelegraphSeconds)}초", row[2]);
                Assert.Equal(Num(p.BaseDamage), row[4]);
                // 정예 수치 표의 '하는 일' 과 이름이 같다
                Assert.Equal(p.NameKo, GddTable(GddSection("정예 수치")).Single(r => r[1] == name)[5]);
            }
            var slam = _d.ElitePatternSet("dongin").Pattern("body_slam_x3");
            Assert.Equal("jump_slam", slam.Shape);
            Assert.Equal(3, slam.Count);
            Assert.Contains($"반경 {Num(slam.Radius)}m", rows.Single(r => r[1] == slam.NameKo)[3]);
            Assert.Contains($"{Num(slam.WaveIntervalSeconds)}초", rows.Single(r => r[1] == slam.NameKo)[5]);
            var palm = _d.ElitePatternSet("gyeyul_monk").Pattern("palm_triple");
            Assert.Equal($"세 갈래 {Num(palm.AngleDegrees)}°, {Num(palm.Length)}m", rows.Single(r => r[1] == palm.NameKo)[3]);
            Assert.Equal(3, palm.Count);
            var bell = _d.ElitePatternSet("acting_abbot").Pattern("bell_stun");
            Assert.Equal("self", bell.Aim);
            Assert.True(bell.BossHolds);
            Assert.Contains($"반경 {Num(bell.Radius)}m", rows.Single(r => r[1] == bell.NameKo)[3]);
            Assert.Contains($"{Num(bell.StunSeconds)}초 기절", rows.Single(r => r[1] == bell.NameKo)[5]);
        }

        [Fact] public void 정예_한_시전은_간격_안에_끝난다()
        {
            foreach (var set in _d.ElitePatterns.Values)
                foreach (var p in set.Patterns)
                {
                    int n = p.Shape == "jump_slam" ? p.Count : System.Math.Max(1, p.Waves);
                    float gap = p.WaveIntervalSeconds;
                    float cast = p.Shape == "jump_slam" ? n * p.TelegraphSeconds + (n - 1) * gap : p.TelegraphSeconds + (n - 1) * gap;
                    Assert.True(cast < set.IntervalSeconds, $"{p.Id}: {cast}초");
                }
        }
    }
}
