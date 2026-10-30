using System;
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
    /// <summary>땅의 주인 패턴 둘째·셋째 묶음(T-12: 당소혜·마천패, T-13: 혈허진인·낙매검선의 검혼). 원각(BossPatternsTests)과 같은 틀을 보스마다 확인한다.</summary>
    public class BossPatternsMoreTests
    {
        readonly GameData _d = Load();
        float Dpm => _d.Balance.EnemyScaling.DamagePerMinute;
        float At20(float baseDamage) => BossPatterns.ScaleDamage(baseDamage, 20f, 1f, Dpm);

        /// <summary>아직 enemies.json 에 없는 소환 적. T-33에서 넣으면 여기서 뺀다.</summary>
        static readonly HashSet<string> PendingSummons = new HashSet<string> { "water_ghost", "plum_sword_ghost" };

        public static IEnumerable<object[]> Bosses => new[] { "dang_sohye", "ma_cheonpae", "hyeolheo", "huashan_sword_soul" }.Select(x => new object[] { x });

        [Theory, MemberData(nameof(Bosses))]
        public void 패턴_순서와_2막이_enemies_json과_같다(string id)
        {
            var w = _d.BossPatternSet(id);
            var boss = _d.Bosses.Single(b => b.Id == id);
            Assert.Equal(boss.Patterns, w.Order);
            Assert.Equal(w.Order.OrderBy(x => x), w.Patterns.Select(p => p.Id).OrderBy(x => x));
            var enemies = JObject.Parse(File.ReadAllText(Path.Combine(DataDir, "enemies.json")));
            var b = enemies["bosses"]!.Children<JObject>().Single(x => (string)x["id"]! == id);
            Assert.Equal((float)b["phase2At"]!, w.Phase2At);
            Assert.Equal(b["phase2Patterns"]!.Select(t => (string)t!), w.Phase2.Select(p => p.Id));
            Assert.All(w.Phase2, p => Assert.True(p.Continuous));
        }

        [Theory, MemberData(nameof(Bosses))]
        public void 소환하는_적이_enemies_json에_있다(string id)
        {
            var w = _d.BossPatternSet(id);
            foreach (var p in w.Patterns.Concat(w.Phase2))
                if (p.Summon != "" && !PendingSummons.Contains(p.Summon))
                    Assert.Contains(_d.Enemies, e => e.Id == p.Summon);
        }

        [Fact] public void 미뤄_둔_소환_적은_아직_없을_때만_목록에_남긴다()
        {
            foreach (var s in PendingSummons) Assert.DoesNotContain(_d.Enemies, e => e.Id == s);
        }

        [Theory, MemberData(nameof(Bosses))]
        public void 패턴_이름이_기획서_땅의_주인_표와_대응한다(string id)
        {
            var w = _d.BossPatternSet(id);
            var boss = _d.Bosses.Single(b => b.Id == id);
            var row = GddTable(GddSection("땅의 주인(지역 보스)")).Single(r => r[0] == boss.NameKo);
            var parts = row[4].Split(" / ");
            Assert.Equal(w.Order.Count + w.Phase2.Count, parts.Length);
            for (int i = 0; i < w.Order.Count; i++) Assert.Contains(w.Pattern(w.Order[i]).NameKo, parts[i]);
            for (int i = 0; i < w.Phase2.Count; i++)
            {
                var part = parts[w.Order.Count + i];
                Assert.StartsWith($"{Num(w.Phase2At * 100)}%:", part);
                Assert.Contains(w.Phase2[i].NameKo, part);
            }
        }

        [Theory, MemberData(nameof(Bosses))]
        public void 패턴_수치_표가_데이터와_같다(string id)
        {
            var w = _d.BossPatternSet(id);
            var boss = _d.Bosses.Single(b => b.Id == id);
            var section = GddSection(boss.NameKo);
            Assert.Contains($"첫 패턴 {Num(w.FirstCastSeconds)}초, 그 뒤 {Num(w.IntervalSeconds)}초마다(2막 {Num(w.Phase2IntervalSeconds)}초) "
                + string.Join(" → ", w.Order.Select(x => w.Pattern(x).NameKo)), section);
            Assert.Contains($"몸에 닿으면 {Num(w.ContactDamage)}({Num(At20(w.ContactDamage))}), 이동속도 {Num(w.MoveSpeed)}", section);

            var rows = GddTable(section);
            Assert.Equal(w.Patterns.Count + w.Phase2.Count, rows.Count);
            foreach (var p in w.Patterns.Concat(w.Phase2))
            {
                bool p2 = w.Phase2.Contains(p);
                var r = rows.Single(x => x[0] == (p2 ? "2막: " : "") + p.NameKo);
                Assert.Equal($"{Num(p.TelegraphSeconds)}초", r[1]);
                Assert.StartsWith(p.BaseDamage > 0 ? $"{Num(p.BaseDamage)}({Num(At20(p.BaseDamage))})" : "없음", r[3]);
                if (p.Scatter > 0 && p.Shape != "split") Assert.Contains($"{Num(p.Scatter)}m", r[2]);
            }
        }

        [Theory, MemberData(nameof(Bosses))]
        public void 시간표는_첫_패턴_뒤_간격마다_차례로(string id)
        {
            var w = _d.BossPatternSet(id);
            var plan = BossPatterns.Plan(w, 30f);
            for (int i = 0; i < plan.Count; i++)
            {
                Assert.Equal(w.Order[i % w.Order.Count], plan[i].Pattern.Id);
                Assert.Equal(w.FirstCastSeconds + w.IntervalSeconds * i, plan[i].TelegraphAt, 3);
            }
        }

        [Theory, MemberData(nameof(Bosses))]
        public void 기혈_30퍼센트에서_2막_패턴이_한_번_켜진다(string id)
        {
            var w = _d.BossPatternSet(id);
            var b = new BossPatterns(w);
            var events = new List<BossPatternEvent>();
            for (int i = 0; i < 60 * 20; i++) events.AddRange(b.Tick(1f / 60f, b.Elapsed < 10f ? 1f : 0.25f));
            Assert.Single(events, e => e.Kind == BossPatternEventKind.Phase2);
            var p2 = w.Phase2.Single();
            Assert.Contains(events, e => e.Kind == BossPatternEventKind.Resolve && e.Pattern == p2);
        }

        [Fact] public void 당소혜_분열은_셋이_기혈을_나눠_갖고_독안개는_번갈아_깔린다()
        {
            var w = _d.BossPatternSet("dang_sohye");
            var split = w.Phase2.Single();
            Assert.Equal(3, split.Count);
            Assert.Equal(1f, split.Count * split.HealthShare, 2);
            Assert.True(split.RejoinSeconds > 0);
            var fog = w.Pattern("half_map_poison_fog");
            Assert.True(fog.Alternate);
            Assert.True(fog.LeaveSeconds < w.IntervalSeconds * w.Order.Count); // 다음 독안개 전에 걷힌다
            var fan = w.Pattern("dart_fan_x3");
            Assert.Equal(3, fan.Waves);
            Assert.True(fan.Waves * fan.WaveIntervalSeconds < w.IntervalSeconds);
        }

        [Fact] public void 마천패_사슬은_간격_안에_끝나고_파도는_빛줄기처럼_이어진다()
        {
            var w = _d.BossPatternSet("ma_cheonpae");
            var chain = w.Pattern("chain_hook_pull");
            Assert.True(chain.PullSeconds > 0 && chain.TelegraphSeconds + chain.PullSeconds < w.IntervalSeconds);
            Assert.True(chain.BossHolds);
            var wave = w.Phase2.Single();
            Assert.True(wave.WaveIntervalSeconds > 0);
            Assert.True(wave.Continuous);
        }
        [Fact] public void 혈허진인_흑백은_반대_색에서_피해_2배이고_2막에서_돈다()
        {
            var w = _d.BossPatternSet("hyeolheo");
            Assert.Equal(2f, w.Pattern("yin_yang_split_field").DamageTakenMultiplier);
            var p2 = w.Phase2.Single();
            Assert.Equal(2f, p2.DamageTakenMultiplier);
            Assert.True(p2.TurnDegreesPerSecond > 0);
            var dipper = w.Pattern("seven_star_sword_drop");
            Assert.Equal("big_dipper", dipper.Arrangement);
            Assert.Equal(7, dipper.Count);
            var taiji = w.Pattern("taiji_inhale_exhale");
            Assert.True(taiji.BurstRadius < taiji.Radius);
        }

        [Fact] public void 검혼_일섬_뒤_2초_경직이_다음_패턴_전에_끝난다()
        {
            var w = _d.BossPatternSet("huashan_sword_soul");
            var slash = w.Pattern("screen_slash");
            Assert.Equal(2f, slash.RecoverySeconds);
            Assert.True(slash.TelegraphSeconds + slash.RecoverySeconds < w.Phase2IntervalSeconds);
            var burst = w.Phase2.Single();
            Assert.Equal(24, burst.Count);
            var spiral = w.Pattern("petal_spiral");
            Assert.True(spiral.TelegraphSeconds + spiral.LeaveSeconds < w.IntervalSeconds);
        }
    }
}
