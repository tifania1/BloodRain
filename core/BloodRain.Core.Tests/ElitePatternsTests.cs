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
                    float cast = BossPatterns.CastSeconds(p) + BossPatterns.ClosingSeconds(p) + p.RecoverySeconds;
                    Assert.True(cast < set.IntervalSeconds, $"{p.Id}: {cast}초");
                }
        }

        static readonly string[] M3Regions = { "chapter2", "chapter3", "chapter4" };

        [Fact] public void 당가타_장강_무당산_정예_아홉_표가_데이터와_같다()
        {
            var section = GddSection("정예 패턴 수치");
            var rows = GddTable(section).Where(r => r.Length == 6).ToList();
            var stats = GddTable(GddSection("정예 수치"));
            var ids = M3Regions.SelectMany(c => _d.Elites[c]).ToList();
            Assert.Equal(9, ids.Count);
            foreach (var id in ids)
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
                // 정예 수치 표의 '하는 일' 은 패턴 이름으로 시작한다
                Assert.StartsWith(p.NameKo, stats.Single(r => r[1] == name)[5]);
                if (p.Radius > 0 && p.Shape != "closing_ring") Assert.Contains($"반경 {Num(p.Radius)}m", row[3]);
                if (p.Length > 0) Assert.Contains($"{Num(p.Length)}m", row[3]);
                if (p.PoolRadius > 0)
                {
                    Assert.True(p.LeaveSeconds > 0 && p.PoolDamagePerSecond > 0, id);
                    Assert.Contains($"반경 {Num(p.PoolRadius)}m", row[5]);
                    Assert.Contains($"{Num(p.LeaveSeconds)}초 남아 초당 {Num(p.PoolDamagePerSecond)}", row[5]);
                }
                if (p.ProjectileSpeed > 0) Assert.Contains($"속도 {Num(p.ProjectileSpeed)}", row[5]);
            }
        }

        [Fact] public void 아홉은_같은_차례_숭산_정예에_맞췄다()
        {
            // 첫째: 동인 뛰어 덮치기의 한 방·예고·사이 / 둘째: 승려의 예고 / 셋째: 방장 대리의 예고. 둘째·셋째 한 방은 몸 피해 그대로.
            var song = _d.Elites["chapter1"].Select(id => _d.ElitePatternSet(id)).ToList();
            var slam = song[0].Pattern(song[0].Order[0]);
            foreach (var c in M3Regions)
            {
                var list = _d.Elites[c];
                for (int i = 0; i < 3; i++)
                {
                    var set = _d.ElitePatternSet(list[i]);
                    var p = set.Pattern(set.Order[0]);
                    var mate = song[i].Pattern(song[i].Order[0]);
                    if (p.Shape == "jump_slam")
                    {
                        Assert.Equal(0, i);
                        Assert.Equal(slam.TelegraphSeconds, p.TelegraphSeconds);
                        Assert.Equal(slam.WaveIntervalSeconds, p.WaveIntervalSeconds);
                        Assert.Equal(slam.Radius, p.Radius);
                    }
                    if (i == 0 && p.BaseDamage > 0) Assert.Equal(slam.BaseDamage, p.BaseDamage);
                    if (i == 1 && p.Shape != "summon") Assert.Equal(mate.TelegraphSeconds, p.TelegraphSeconds); // 소환 예고는 장삼·당소혜 소환과 같은 1초
                    if (i == 2) Assert.Equal(mate.TelegraphSeconds, p.TelegraphSeconds);
                    if (i >= 1 && p.BaseDamage > 0) Assert.Equal(set.ContactDamage, p.BaseDamage);
                }
            }
        }

        [Fact] public void 장판_값은_같은_땅_적에서_왔다()
        {
            var toad = _d.Enemy("poison_toad");
            var hop = _d.ElitePatternSet("poison_toad_king").Pattern("poison_hop_x3");
            var raw = JObject.Parse(File.ReadAllText(Path.Combine(DataDir, "enemies.json")))["enemies"]!.Children<JObject>().ToDictionary(o => (string)o["id"]!);
            Assert.Equal((float)raw["poison_toad"]!["death"]!["radius"]!, hop.PoolRadius);
            Assert.Equal((float)raw["poison_toad"]!["death"]!["seconds"]!, hop.LeaveSeconds);
            Assert.Equal((float)raw["poison_toad"]!["death"]!["perSecond"]!, hop.PoolDamagePerSecond);
            Assert.Equal("chapter2", toad.Chapter);
            var fog = _d.ElitePatternSet("dogin_elder").Pattern("fog_expand");
            Assert.Equal(_d.Enemy("dogin").ZoneDamagePerSecond, fog.PoolDamagePerSecond);
            Assert.Equal("self", fog.Aim);
            var pool = _d.ElitePatternSet("sulsa_elder").Pattern("great_blood_pool");
            var sulsa = _d.Enemy("hyeol_sulsa");
            Assert.Equal(sulsa.ZoneDamagePerSecond, pool.PoolDamagePerSecond);
            Assert.Equal((float)raw["hyeol_sulsa"]!["pool"]!["seconds"]!, pool.LeaveSeconds);
            Assert.Equal(2 * (float)raw["hyeol_sulsa"]!["pool"]!["radius"]!, pool.PoolRadius);
            // 수적 두목 갈고리는 수적 길이·폭·예고와 같다
            var hook = _d.ElitePatternSet("pirate_chief").Pattern("hook_x3");
            Assert.Equal("bind_line", hook.Shape);
            Assert.Equal(3, hook.Waves);
            Assert.True(hook.RootSeconds > 0 && hook.StunSeconds == 0, "세 번 이어 맞아도 경공으로 끊을 수 있어야 한다");
            // 수귀 부르기는 enemies.json 의 수귀를 부른다
            var call = _d.ElitePatternSet("water_ghost_chief").Pattern("call_water_ghosts");
            Assert.Equal("chapter3", _d.Enemy(call.Summon).Chapter);
        }

        [Fact] public void 검진은_검_사이로_걸어_나갈_수_있고_2초에_다_조인다()
        {
            var p = _d.ElitePatternSet("sword_array_taoist").Pattern("five_sword_array");
            Assert.Equal("closing_ring", p.Shape);
            Assert.Equal(p.Radius, BossPatterns.ClosingRadius(p, 0f));
            Assert.Equal(p.MinRadius, BossPatterns.ClosingRadius(p, 99f));
            Assert.Equal(2f, BossPatterns.ClosingSeconds(p), 3);
            Assert.Equal(p.Radius - p.ShrinkPerSecond, BossPatterns.ClosingRadius(p, 1f), 3);
            // 처음 자리에서 검과 검 사이 틈(현의 길이에서 검 폭을 뺀 것)이 몸 너비(0.8m)보다 넓다
            var ring = BossPatterns.RingOffsets(p, p.Radius);
            Assert.Equal(5, ring.Length);
            double chord = System.Math.Sqrt(System.Math.Pow(ring[1].x - ring[0].x, 2) + System.Math.Pow(ring[1].y - ring[0].y, 2));
            Assert.True(chord - p.Width > 0.8, $"{chord}");
        }

        [Fact] public void 장판_판정과_고리_자리()
        {
            var p = new BossPatternDef { PoolRadius = 1.2f, Count = 4 };
            Assert.True(BossPatterns.InPool(p, 1.2f, 0f));
            Assert.False(BossPatterns.InPool(p, 1.3f, 0f));
            Assert.True(BossPatterns.InPool(p, 1.5f, 0f, 0.4f));
            Assert.False(BossPatterns.InPool(new BossPatternDef(), 0f, 0f));
            var ring = BossPatterns.RingOffsets(p, 2f, 90f);
            Assert.Equal(0f, ring[0].x, 4);
            Assert.Equal(2f, ring[0].y, 4);
            Assert.Equal(-2f, ring[1].x, 4);
        }

        [Fact] public void 한_시전_길이()
        {
            var slam = _d.ElitePatternSet("dongin").Pattern("body_slam_x3");
            Assert.Equal(3 * 0.6f + 2 * 0.3f, BossPatterns.CastSeconds(slam), 4);
            var hook = _d.ElitePatternSet("pirate_chief").Pattern("hook_x3");
            Assert.Equal(0.5f + 2 * 0.5f, BossPatterns.CastSeconds(hook), 4);
            var bell = _d.ElitePatternSet("acting_abbot").Pattern("bell_stun");
            Assert.Equal(bell.TelegraphSeconds, BossPatterns.CastSeconds(bell));
        }
    }
}
