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
    /// <summary>서장 보스 강시 포쾌 장삼의 패턴(기획서 6장 '강시 포쾌 장삼', bosspatterns.json jang_sam). 땅의 주인과 같은 틀에 2막만 없다.</summary>
    public class JangSamPatternsTests
    {
        readonly GameData _d = Load();
        BossPatternSetDef W => _d.BossPatternSet("jang_sam");

        /// <summary>장삼은 서장 9분, 청운진에서 싸운다.</summary>
        float AtPrologue(float baseDamage)
        {
            var chapters = JObject.Parse(File.ReadAllText(Path.Combine(DataDir, "chapters.json")));
            var boss = chapters.Descendants().OfType<JObject>().Single(o => (string?)o["kind"] == "boss" && (string?)o["enemy"] == "jang_sam");
            float minute = (float)boss["at"]!;
            var cheonha = JObject.Parse(File.ReadAllText(Path.Combine(DataDir, "cheonha.json")));
            float density = (float)cheonha.Descendants().OfType<JObject>().Single(o => (string?)o["id"] == "cheongun")["density"]!;
            return BossPatterns.ScaleDamage(baseDamage, minute, density, _d.Balance.EnemyScaling.DamagePerMinute);
        }

        [Fact] public void 패턴_순서가_enemies_json과_같고_2막이_없다()
        {
            var boss = _d.Bosses.Single(b => b.Id == "jang_sam");
            Assert.Equal(boss.Patterns, W.Order);
            Assert.Equal(W.Order.OrderBy(x => x), W.Patterns.Select(p => p.Id).OrderBy(x => x));
            Assert.Empty(W.Phase2);
            var enemies = JObject.Parse(File.ReadAllText(Path.Combine(DataDir, "enemies.json")));
            var b = enemies["bosses"]!.Children<JObject>().Single(x => (string)x["id"]! == "jang_sam");
            Assert.Null(b["phase2At"]);
            Assert.Null(b["phase2Patterns"]);
        }

        [Fact] public void 기혈이_바닥나도_2막이_오지_않는다()
        {
            Assert.False(BossPatterns.IsPhase2(W, 0f));
            var b = new BossPatterns(W);
            var events = new List<BossPatternEvent>();
            for (int i = 0; i < 60 * 30; i++) events.AddRange(b.Tick(1f / 60f, i < 600 ? 1f : 0f));
            Assert.DoesNotContain(events, e => e.Kind == BossPatternEventKind.Phase2);
            Assert.False(b.InPhase2);
            Assert.Contains(events, e => e.Kind == BossPatternEventKind.Resolve && e.Pattern!.Id == "triple_slam");
        }

        [Fact] public void 시간표는_첫_패턴_뒤_간격마다_차례로()
        {
            var plan = BossPatterns.Plan(W, 40f);
            Assert.True(plan.Count >= 8); // 목표 처치 40초 안에 세 패턴이 두 바퀴 넘게 돈다
            for (int i = 0; i < plan.Count; i++)
            {
                Assert.Equal(W.Order[i % W.Order.Count], plan[i].Pattern.Id);
                Assert.Equal(W.FirstCastSeconds + W.IntervalSeconds * i, plan[i].TelegraphAt, 3);
            }
        }

        [Fact] public void 내려찍기_세_번과_포승줄_묶임이_간격_안에_끝난다()
        {
            var slam = W.Pattern("triple_slam");
            Assert.Equal(3, slam.Count);
            Assert.True(slam.Count * slam.TelegraphSeconds + (slam.Count - 1) * slam.WaveIntervalSeconds < W.IntervalSeconds);
            var rope = W.Pattern("rope_throw");
            Assert.Equal(1f, rope.RootSeconds);
            Assert.True(rope.BossHolds);
            Assert.True(rope.TelegraphSeconds + rope.Length / (rope.ProjectileSpeed / 100f * _d.Balance.Stats.MoveBase) + rope.RootSeconds < W.IntervalSeconds);
            var whistle = W.Pattern("whistle_summon");
            Assert.Contains(_d.Enemies, e => e.Id == whistle.Summon);
        }

        [Fact] public void 한_방은_원각보다_가볍다()
        {
            var wongak = _d.BossPatternSet("wongak");
            float wongakMin = wongak.Patterns.Where(p => p.BaseDamage > 0).Min(p => p.BaseDamage);
            Assert.All(W.Patterns.Where(p => p.BaseDamage > 0), p => Assert.True(p.BaseDamage < wongakMin));
            Assert.True(W.ContactDamage < wongak.ContactDamage);
        }

        [Fact] public void 수치_표가_데이터와_같다()
        {
            var boss = _d.Bosses.Single(b => b.Id == "jang_sam");
            var section = GddSection(boss.NameKo);
            Assert.Contains($"첫 패턴 {Num(W.FirstCastSeconds)}초, 그 뒤 {Num(W.IntervalSeconds)}초마다 "
                + string.Join(" → ", W.Order.Select(x => W.Pattern(x).NameKo)), section);
            Assert.Contains($"몸에 닿으면 {Num(W.ContactDamage)}({Num(AtPrologue(W.ContactDamage))}), 이동속도 {Num(W.MoveSpeed)}", section);
            var rows = GddTable(section);
            Assert.Equal(W.Patterns.Count, rows.Count);
            foreach (var p in W.Patterns)
            {
                var r = rows.Single(x => x[0] == p.NameKo);
                Assert.Equal($"{Num(p.TelegraphSeconds)}초", r[1]);
                Assert.StartsWith(p.BaseDamage > 0 ? $"{Num(p.BaseDamage)}({Num(AtPrologue(p.BaseDamage))})" : "없음", r[3]);
                if (p.Scatter > 0) Assert.Contains($"{Num(p.Scatter)}m", r[2]);
                if (p.Radius > 0) Assert.Contains($"반경 {Num(p.Radius)}m", r[2]);
                if (p.Length > 0) Assert.Contains($"길이 {Num(p.Length)}m", r[2]);
            }
            Assert.Contains($"{Num(W.Pattern("rope_throw").RootSeconds)}초 동안 걷지 못한다", rows.Single(x => x[0] == "포승줄")[4]);
        }

        [Fact] public void 장삼_기혈은_enemies_json_그대로이고_기획서와_같다()
        {
            var boss = _d.Bosses.Single(b => b.Id == "jang_sam");
            Assert.Equal(boss.Health, BloodRain.Core.Run.Prologue.BossMaxHealth(boss));
            var row = GddTable(GddSection("땅의 주인(지역 보스)")).Single(r => r[0] == boss.NameKo);
            Assert.Equal(boss.Health.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture), row[2]);
            Assert.Contains(boss.Health.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture) + " 그대로", GddSection(boss.NameKo));
            // 10분 화력 목표 300으로 각성(기혈 10%)까지 18초
            float floor = _d.Cheonha.PrologueBody.BossHealthFloor;
            Assert.Contains($"각성까지 {Num(boss.Health * (1 - floor) / 300f)}초쯤", GddSection(boss.NameKo));
        }

        [Fact] public void 패턴_이름이_보스_표의_설명과_대응한다()
        {
            var row = GddTable(GddSection("땅의 주인(지역 보스)")).Single(r => r[0] == "강시 포쾌 장삼");
            var parts = row[4].Split(" / ");
            Assert.Equal(W.Order.Count, parts.Length);
            Assert.Contains("포승줄", parts[0]);
            Assert.Contains("호루라기", parts[1]);
            Assert.Contains("내려찍기", parts[2]);
        }
    }
}
