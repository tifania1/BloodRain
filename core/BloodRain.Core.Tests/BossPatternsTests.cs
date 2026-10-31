using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using Newtonsoft.Json.Linq;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>땅의 주인 패턴(T-11): 원각의 패턴 데이터가 기획서·enemies.json·웹 프로토타입과 맞고, 시간표와 2막 전환이 규칙대로인지.</summary>
    public class BossPatternsTests
    {
        readonly GameData _d = Load();
        BossPatternSetDef W => _d.BossPatternSet("wongak");
        BossDef Boss => _d.Bosses.Single(b => b.Id == "wongak");
        float Dpm => _d.Balance.EnemyScaling.DamagePerMinute;
        float At20(float baseDamage) => BossPatterns.ScaleDamage(baseDamage, 20f, 1f, Dpm);

        static List<BossPatternEvent> Run(BossPatterns b, float seconds, Func<float, float> health, float dt = 1f / 60f)
        {
            var all = new List<BossPatternEvent>();
            int frames = (int)Math.Round(seconds / dt);
            for (int i = 0; i < frames; i++) all.AddRange(b.Tick(dt, health(b.Elapsed)));
            return all;
        }

        static List<BossPatternEvent> Of(IEnumerable<BossPatternEvent> events, BossPatternEventKind kind, bool phase2 = false) =>
            events.Where(e => e.Kind == kind && (e.Cast < 0) == phase2 && e.Pattern != null).ToList();

        // ---- 데이터 ----

        [Fact] public void 패턴_순서와_2막이_enemies_json과_같다()
        {
            Assert.Equal(Boss.Patterns, W.Order);
            Assert.Equal(W.Order.OrderBy(x => x), W.Patterns.Select(p => p.Id).OrderBy(x => x));

            var enemies = JObject.Parse(File.ReadAllText(Path.Combine(DataDir, "enemies.json")));
            var boss = enemies["bosses"]!.Children<JObject>().Single(b => (string)b["id"]! == "wongak");
            Assert.Equal((float)boss["phase2At"]!, W.Phase2At);
            Assert.Equal(boss["phase2Patterns"]!.Select(t => (string)t!), W.Phase2.Select(p => p.Id));
            Assert.All(W.Phase2, p => Assert.True(p.Continuous));
        }

        [Fact] public void 패턴이_있는_보스는_모두_enemies_json에_있다()
        {
            Assert.NotEmpty(_d.BossPatterns);
            foreach (var id in _d.BossPatterns.Keys) Assert.Contains(_d.Bosses, b => b.Id == id);
        }

        [Fact] public void 소환하는_적이_enemies_json에_있다()
        {
            foreach (var p in W.Patterns.Concat(W.Phase2))
                if (p.Summon != "") Assert.Contains(_d.Enemies, e => e.Id == p.Summon);
            Assert.Equal("mogin_nahan", W.Pattern("eighteen_arhat_ring").Summon);
        }

        [Fact] public void 패턴_이름이_기획서_땅의_주인_표와_대응한다()
        {
            var row = GddTable(GddSection("땅의 주인(지역 보스)")).Single(r => r[0] == Boss.NameKo);
            var parts = row[4].Split(" / ");
            Assert.Equal(W.Order.Count + W.Phase2.Count, parts.Length);
            for (int i = 0; i < W.Order.Count; i++)
                Assert.Contains(W.Pattern(W.Order[i]).NameKo, parts[i]);
            for (int i = 0; i < W.Phase2.Count; i++)
            {
                var part = parts[W.Order.Count + i];
                Assert.StartsWith($"{Num(W.Phase2At * 100)}%:", part);
                Assert.Contains(W.Phase2[i].NameKo, part);
            }
            Assert.Contains("범종 낙하 3곳", row[4]);
            Assert.Equal(3, W.Pattern("bell_drop_x3").Count);
            Assert.Contains("18기", row[4]);
            Assert.Equal(18, W.Pattern("eighteen_arhat_ring").Count);
        }

        [Fact] public void 패턴_수치_표가_데이터와_같다()
        {
            var section = GddSection("땅의 주인 패턴 수치");
            Assert.Contains($"남은 기혈이 {Num(W.Phase2At * 100)}% 이하가 되면 2막", section);
            Assert.Contains($"첫 패턴 {Num(W.FirstCastSeconds)}초, 그 뒤 {Num(W.IntervalSeconds)}초마다(2막 {Num(W.Phase2IntervalSeconds)}초) "
                + string.Join(" → ", W.Order.Select(id => W.Pattern(id).NameKo)), section);
            Assert.Contains($"몸에 닿으면 {Num(W.ContactDamage)}({Num(At20(W.ContactDamage))}), 이동속도 {Num(W.MoveSpeed)}", section);

            var rows = GddTable(section);
            Assert.Equal(W.Patterns.Count + W.Phase2.Count, rows.Count);
            string[] Row(BossPatternDef p, bool phase2) => rows.Single(r => r[0] == (phase2 ? "2막: " : "") + p.NameKo);
            string Dmg(BossPatternDef p) => p.BaseDamage > 0 ? $"{Num(p.BaseDamage)}({Num(At20(p.BaseDamage))})" : "없음";

            foreach (var p in W.Patterns.Concat(W.Phase2))
            {
                var r = Row(p, W.Phase2.Contains(p));
                Assert.Equal($"{Num(p.TelegraphSeconds)}초", r[1]);
                Assert.StartsWith(Dmg(p), r[3]);
            }

            var cone = W.Pattern("vajra_cone");
            Assert.Equal($"부채꼴 반경 {Num(cone.Radius)}m, {Num(cone.AngleDegrees)}°", Row(cone, false)[2]);
            var ring = W.Pattern("eighteen_arhat_ring");
            Assert.Equal($"반경 {Num(ring.Radius)}m 원 위 목인나한 {ring.Count}기", Row(ring, false)[2]);
            Assert.Contains($"속도 {Num(ring.SummonSpeed)}", Row(ring, false)[4]);
            var bells = W.Pattern("bell_drop_x3");
            Assert.Equal($"반경 {Num(bells.Radius)}m 세 곳(플레이어 둘레 ±{Num(bells.Scatter)}m)", Row(bells, false)[2]);
            Assert.Contains($"{Num(bells.LeaveSeconds)}초 동안 남고, 한 번 치면", Row(bells, false)[4]);
            Assert.Equal(1, bells.LeaveUses);
            var beams = W.Phase2.Single();
            Assert.Equal($"길이 {Num(beams.Length)}m, 폭 {Num(beams.Width)}m 네 줄", Row(beams, true)[2]);
            Assert.Equal(4, beams.Count);
            Assert.Equal($"{Dmg(beams)}, {Num(beams.HitIntervalSeconds)}초마다", Row(beams, true)[3]);
            Assert.Contains($"{Num(beams.StartOffsetDegrees)}° 비켜난 데서 시작해 초당 {Num(beams.TurnDegreesPerSecond)}°로", Row(beams, true)[4]);
        }

        [Fact] public void 웹_프로토타입의_원각_숫자와_같다()
        {
            // 프로토타입(template.html)의 bossAI 가 먼저 만든 참고 구현이다. 피해는 프로토타입이 20분 값을 박아 두어서 20분 배율을 곱해 비교한다.
            var html = ReadRepoFile("web", "prototype", "template.html");
            float Grab(string pattern, int group = 1)
            {
                var m = Regex.Match(html, pattern, RegexOptions.Singleline);
                Assert.True(m.Success, $"template.html 에서 /{pattern}/ 를 못 찾았습니다. 프로토타입 코드가 바뀌었으면 이 패턴도 같이 고친다");
                return float.Parse(m.Groups[group].Value, CultureInfo.InvariantCulture);
            }
            // 프로토타입은 이동속도 100을 balance.json stats.moveBase(MOVE)로 쓴다(speed: 0.45 * MOVE = 45).
            // spawnBoss
            const string spawn = @"speed: ([\d.]+) \* MOVE, dmg: ([\d.]+), r: KIND_SIZE\.boss[^\n]*\n\s*pattern: 'boss', timer: ([\d.]+)";
            Assert.Equal(W.MoveSpeed / 100f, Grab(spawn, 1), 3);
            Assert.Equal(At20(W.ContactDamage), Grab(spawn, 2), 3);
            Assert.Equal(W.FirstCastSeconds, Grab(spawn, 3));

            // bossAI: 2막 문턱, 간격, 순서
            Assert.Equal(W.Phase2At, Grab(@"!e\.phase2 && e\.hp < e\.maxHp \* ([\d.]+)"));
            Assert.Equal(W.Phase2IntervalSeconds, Grab(@"e\.timer = e\.phase2 \? ([\d.]+) : ([\d.]+);", 1));
            Assert.Equal(W.IntervalSeconds, Grab(@"e\.timer = e\.phase2 \? ([\d.]+) : ([\d.]+);", 2));
            Assert.Contains("['cone', 'arhat', 'bells'][e.cycle++ % 3]", html);
            Assert.Equal(new[] { "vajra_cone", "eighteen_arhat_ring", "bell_drop_x3" }, W.Order);

            // 금강장
            var cone = W.Pattern("vajra_cone");
            Assert.Equal(cone.TelegraphSeconds, Grab(@"e\.cur = 'cone'; e\.windup = ([\d.]+)"));
            const string coneHit = @"if \(dd < ([\d.]+) && ang < ([\d.]+)\) hurtPlayer\(([\d.]+)\)";
            Assert.Equal(cone.Radius, Grab(coneHit, 1));
            Assert.Equal(cone.AngleDegrees * 0.5f, Grab(coneHit, 2) * 180f / (float)Math.PI, 0);
            Assert.Equal(At20(cone.BaseDamage), Grab(coneHit, 3), 3);

            // 십팔나한진
            var ring = W.Pattern("eighteen_arhat_ring");
            const string ringSpawn = @"i < (\d+); i\+\+\) \{ const a = i / \d+ \* TAU; spawnEnemy\('mogin_nahan', p\.x \+ Math\.cos\(a\) \* ([\d.]+),[^\n]*speed: ([\d.]+) \* MOVE";
            Assert.Equal(ring.Count, (int)Grab(ringSpawn, 1));
            Assert.Equal(ring.Radius, Grab(ringSpawn, 2));
            Assert.Equal(ring.SummonSpeed / 100f, Grab(ringSpawn, 3), 3);

            // 범종 낙하
            var bells = W.Pattern("bell_drop_x3");
            const string drop = @"i < (\d+); i\+\+\) \{ const bx = p\.x \+ rand\(-([\d.]+), ([\d.]+)\)[^\n]*delay: ([\d.]+), mark: \{ x: bx, y: by, r: ([\d.]+) \}[^\n]*hurtPlayer\(([\d.]+)\)";
            Assert.Equal(bells.Count, (int)Grab(drop, 1));
            Assert.Equal(bells.Scatter, Grab(drop, 2));
            Assert.Equal(bells.TelegraphSeconds, Grab(drop, 4));
            Assert.Equal(bells.Radius, Grab(drop, 5));
            Assert.Equal(At20(bells.BaseDamage), Grab(drop, 6), 3);

            // 2막 빛줄기
            var beams = W.Phase2.Single();
            Assert.Equal(beams.TurnDegreesPerSecond, Grab(@"e\.beamA \+= dt \* ([\d.]+);") * 180f / (float)Math.PI, 0);
            Assert.Equal(beams.Count, (int)Grab(@"for \(let k = 0; k < (\d+); k\+\+\) \{\s*const a = e\.beamA"));
            const string beamHit = @"along > 0 && along < ([\d.]+) && perp < ([\d.]+) && e\.contact <= 0\) \{ e\.contact = ([\d.]+); hurtPlayer\(([\d.]+)\)";
            Assert.Equal(beams.Length, Grab(beamHit, 1));
            Assert.Equal(beams.Width * 0.5f, Grab(beamHit, 2), 3);
            Assert.Equal(beams.HitIntervalSeconds, Grab(beamHit, 3));
            Assert.Equal(_d.Balance.Contact.SameEnemyIntervalSeconds, beams.HitIntervalSeconds);
            Assert.Equal(At20(beams.BaseDamage), Grab(beamHit, 4), 3);
        }

        // ---- 시간표 ----

        [Fact] public void 첫_패턴은_2초반_뒤_그_뒤로_4초2마다_차례로_돈다()
        {
            var plan = BossPatterns.Plan(W, 30f);
            Assert.Equal(2.5f, plan[0].TelegraphAt);
            for (int i = 0; i < plan.Count; i++)
            {
                Assert.Equal(i, plan[i].Index);
                Assert.Equal(W.Order[i % 3], plan[i].Pattern.Id);
                Assert.Equal(2.5f + 4.2f * i, plan[i].TelegraphAt, 3);
                Assert.Equal(plan[i].TelegraphAt + plan[i].Pattern.TelegraphSeconds, plan[i].ResolveAt, 5);
                Assert.False(plan[i].InPhase2);
            }
            Assert.Equal(7, plan.Count); // 2.5, 6.7 … 27.7
        }

        [Fact] public void 프레임마다_밀어도_시간표와_같은_시각에_예고와_판정이_온다()
        {
            var b = new BossPatterns(W);
            var events = Run(b, 40f, _ => 1f);
            var plan = BossPatterns.Plan(W, b.Elapsed);
            var tele = Of(events, BossPatternEventKind.Telegraph);
            Assert.Equal(plan.Select(c => c.TelegraphAt), tele.Select(e => e.At));
            Assert.Equal(plan.Select(c => c.Pattern.Id), tele.Select(e => e.Pattern!.Id));
            var res = Of(events, BossPatternEventKind.Resolve);
            Assert.Equal(plan.Where(c => c.ResolveAt <= b.Elapsed).Select(c => c.ResolveAt), res.Select(e => e.At));
            Assert.DoesNotContain(events, e => e.Kind == BossPatternEventKind.Phase2);
            // 모두 시각 순서
            for (int i = 1; i < events.Count; i++) Assert.True(events[i - 1].At <= events[i].At);
        }

        [Fact] public void 한_번에_크게_밀어도_빠짐없이_시각_순서대로_돌려준다()
        {
            var b = new BossPatterns(W);
            var events = b.Tick(28f, 1f);
            var plan = BossPatterns.Plan(W, 28f);
            Assert.Equal(7, plan.Count);
            Assert.Equal(plan.Count, Of(events, BossPatternEventKind.Telegraph).Count);
            Assert.Equal(plan.Count(c => c.ResolveAt <= 28f), Of(events, BossPatternEventKind.Resolve).Count);
            for (int i = 1; i < events.Count; i++)
            {
                Assert.True(events[i - 1].At <= events[i].At);
                if (events[i - 1].At == events[i].At) Assert.True(events[i - 1].Kind <= events[i].Kind);
            }
            // 일곱째 패턴(27.7초 금강장)은 아직 예고 중이다
            Assert.Single(b.Pending);
            Assert.Equal(27.7f, b.Pending[0].TelegraphAt, 3);
            Assert.Equal(W.Order[7 % 3], b.NextCast.Pattern.Id);
        }

        [Fact] public void 금강장을_예고하는_동안_멈춰_선다()
        {
            var b = new BossPatterns(W);
            b.Tick(2.4f, 1f);
            Assert.False(b.Holding);
            b.Tick(0.2f, 1f); // 2.6초: 금강장 예고 중
            Assert.True(b.Holding);
            b.Tick(0.8f, 1f); // 3.4초: 판정 끝
            Assert.False(b.Holding);
            b.Tick(4.0f, 1f); // 7.4초: 십팔나한진 예고 중이어도 움직인다
            Assert.Single(b.Pending);
            Assert.Equal("eighteen_arhat_ring", b.Pending[0].Pattern.Id);
            Assert.False(b.Holding);
        }

        // ---- 2막 ----

        [Fact] public void 남은_기혈이_30퍼센트_이하가_되면_2막이_된다()
        {
            Assert.False(BossPatterns.IsPhase2(W, 0.31f));
            Assert.True(BossPatterns.IsPhase2(W, 0.30f));
            Assert.True(BossPatterns.IsPhase2(W, 0f));

            var b = new BossPatterns(W);
            Assert.Empty(b.Tick(1f, 0.31f));
            Assert.False(b.InPhase2);
            var events = b.Tick(0.1f, 0.30f);
            Assert.True(b.InPhase2);
            Assert.Equal(1f, b.Phase2StartedAt);
            Assert.Equal(BossPatternEventKind.Phase2, events[0].Kind);
            var beams = W.Phase2.Single();
            Assert.Equal(beams, Of(events, BossPatternEventKind.Telegraph, phase2: true).Single().Pattern);
            Assert.Null(b.ActiveSeconds(beams));

            // 예고 1초가 지나면 켜지고 끝까지 간다
            var on = b.Tick(1.0f, 0.25f);
            var resolve = Of(on, BossPatternEventKind.Resolve, phase2: true).Single();
            Assert.Equal(1f + beams.TelegraphSeconds, resolve.At, 4);
            Assert.Equal(0.1f, b.ActiveSeconds(beams)!.Value, 4);
            b.Tick(10f, 0.2f);
            Assert.Equal(10.1f, b.ActiveSeconds(beams)!.Value, 3);
        }

        [Fact] public void 원각_2막은_한_번만_오고_기혈이_다시_올라도_돌아가지_않는다()
        {
            var b = new BossPatterns(W);
            var events = Run(b, 20f, t => t < 5f ? 1f : t < 8f ? 0.2f : 0.9f);
            Assert.Single(events, e => e.Kind == BossPatternEventKind.Phase2);
            Assert.Single(Of(events, BossPatternEventKind.Telegraph, phase2: true));
            Assert.Single(Of(events, BossPatternEventKind.Resolve, phase2: true));
            Assert.True(b.InPhase2);
        }

        [Fact] public void 원각이_2막이_되면_잡혀_있던_다음_패턴은_그대로_그다음부터_3초2_간격()
        {
            // 5초에 2막: 2.5초 금강장은 1막에 시작했으니 다음(6.7초)은 그대로, 그 뒤 9.9, 13.1 …
            var plan = BossPatterns.Plan(W, 20f, phase2StartedAt: 5f);
            Assert.Equal(new[] { 2.5f, 6.7f, 9.9f, 13.1f, 16.3f, 19.5f }, plan.Select(c => (float)Math.Round(c.TelegraphAt, 3)));
            Assert.Equal(new[] { false, true, true, true, true, true }, plan.Select(c => c.InPhase2));

            var b = new BossPatterns(W);
            var events = Run(b, 20f, t => t < 5f ? 1f : 0.2f);
            Assert.InRange(b.Phase2StartedAt!.Value, 5f - 1e-3f, 5f + 1f / 60f);
            var expected = BossPatterns.Plan(W, b.Elapsed, b.Phase2StartedAt);
            Assert.Equal(expected.Select(c => c.TelegraphAt), Of(events, BossPatternEventKind.Telegraph).Select(e => e.At));
        }

        [Fact] public void 처음부터_기혈이_낮으면_바로_2막이고_첫_패턴_시각은_같다()
        {
            var b = new BossPatterns(W);
            var events = b.Tick(10f, 0.1f);
            Assert.Equal(0f, b.Phase2StartedAt);
            var tele = Of(events, BossPatternEventKind.Telegraph).Select(e => (float)Math.Round(e.At, 3));
            Assert.Equal(new[] { 2.5f, 5.7f, 8.9f }, tele);
        }

        [Fact] public void 기절한_동안_Tick을_건너뛰면_시간표가_그만큼_밀린다()
        {
            // 범종 기믹은 보스를 0.6초 기절시킨다(gimmicks.json). Unity는 그동안 Tick 을 부르지 않는다.
            var b = new BossPatterns(W);
            b.Tick(2f, 1f);
            // (0.6초 기절: 부르지 않음)
            var events = b.Tick(0.5f, 1f);
            Assert.Equal(2.5f, Of(events, BossPatternEventKind.Telegraph).Single().At);
            Assert.Equal(2.5f, b.Elapsed); // 실제로는 3.1초가 흘렀다
        }

        // ---- 피해 ----

        [Fact] public void 피해는_싸움을_건_분과_혈운_농도로_11장_식을_따른다()
        {
            var cone = W.Pattern("vajra_cone");
            Assert.Equal(cone.BaseDamage, BossPatterns.ScaleDamage(cone.BaseDamage, 0f, 1f, Dpm));
            Assert.Equal(26f, At20(cone.BaseDamage), 3);
            Assert.Equal(cone.BaseDamage * (1f + Dpm * 12f), BossPatterns.ScaleDamage(cone.BaseDamage, 12f, 1f, Dpm), 4);
            Assert.Equal(26f * (float)Math.Sqrt(2.2), BossPatterns.ScaleDamage(cone.BaseDamage, 20f, 2.2f, Dpm), 3);
            Assert.Equal(0f, W.Pattern("eighteen_arhat_ring").BaseDamage);
        }

        // ---- 모양 ----

        [Fact] public void 금강장은_반경_9m_63도_부채꼴로_맞힌다()
        {
            var cone = W.Pattern("vajra_cone");
            Assert.True(BossPatterns.InCone(cone, 8.9f, 0f, 0f));
            Assert.False(BossPatterns.InCone(cone, 9.2f, 0f, 0f));
            Assert.True(BossPatterns.InCone(cone, 9.2f, 0f, 0f, targetRadius: 0.4f));
            double rad30 = 30 * Math.PI / 180, rad33 = 33 * Math.PI / 180;
            Assert.True(BossPatterns.InCone(cone, 5f * (float)Math.Cos(rad30), 5f * (float)Math.Sin(rad30), 0f));
            Assert.False(BossPatterns.InCone(cone, 5f * (float)Math.Cos(rad33), 5f * (float)Math.Sin(rad33), 0f));
            Assert.False(BossPatterns.InCone(cone, -3f, 0f, 0f));
            // 방향이 359도여도 0도 근처를 맞힌다
            Assert.True(BossPatterns.InCone(cone, 5f, 0.5f, 359f));
            Assert.True(BossPatterns.InCone(cone, 0f, 0f, 90f));
        }

        [Fact] public void 범종_낙하는_세_곳을_플레이어_둘레_안에서_고르고_동그라미로_맞힌다()
        {
            var bells = W.Pattern("bell_drop_x3");
            var a = BossPatterns.ScatterOffsets(bells, new Random(7));
            var again = BossPatterns.ScatterOffsets(bells, new Random(7));
            Assert.Equal(3, a.Length);
            Assert.Equal(a, again);
            Assert.All(a, o => { Assert.InRange(o.x, -2.5f, 2.5f); Assert.InRange(o.y, -2.5f, 2.5f); });
            Assert.True(BossPatterns.InCircle(bells.Radius, 1.8f, 0f));
            Assert.False(BossPatterns.InCircle(bells.Radius, 1.3f, 1.3f));
        }

        [Fact] public void 십팔나한진은_반경_8m_원_위에_고르게_선다()
        {
            var ring = W.Pattern("eighteen_arhat_ring");
            var spots = BossPatterns.RingOffsets(ring);
            Assert.Equal(18, spots.Length);
            Assert.All(spots, s => Assert.Equal(8f, (float)Math.Sqrt(s.x * s.x + s.y * s.y), 3));
            double gap = Math.Sqrt(Math.Pow(spots[1].x - spots[0].x, 2) + Math.Pow(spots[1].y - spots[0].y, 2));
            Assert.Equal(2 * 8 * Math.Sin(Math.PI / 18), gap, 3); // 원을 18등분한 현
        }

        [Fact] public void 빛줄기는_플레이어에서_45도_비켜나_시작해_초당_40도로_돈다()
        {
            var beams = W.Phase2.Single();
            Assert.Equal(45f, BossPatterns.BeamDegrees(beams, 0f, 0f, 0), 3);
            Assert.Equal(135f, BossPatterns.BeamDegrees(beams, 0f, 0f, 1), 3);
            Assert.Equal(315f, BossPatterns.BeamDegrees(beams, 0f, 0f, 3), 3);
            Assert.Equal(85f, BossPatterns.BeamDegrees(beams, 0f, 1f, 0), 3);
            Assert.Equal(25f, BossPatterns.BeamDegrees(beams, 340f, 0f, 0), 3);

            // 켜진 순간 플레이어(0도 쪽)는 어느 줄에도 닿지 않는다
            Assert.All(Enumerable.Range(0, 4), k => Assert.False(BossPatterns.InBeam(beams, 5f, 0f, BossPatterns.BeamDegrees(beams, 0f, 0f, k), 0.4f)));
            // 줄 위, 줄 끝 너머, 몸 뒤
            Assert.True(BossPatterns.InBeam(beams, 10f, 0.4f, 0f));
            Assert.False(BossPatterns.InBeam(beams, 10f, 0.5f, 0f));
            Assert.False(BossPatterns.InBeam(beams, 11.5f, 0f, 0f));
            Assert.False(BossPatterns.InBeam(beams, -2f, 0f, 0f));
            // 플레이어 쪽으로 돌아오기까지 45도 / 40도 = 1.125초
            Assert.True(BossPatterns.InBeam(beams, 5f, 0f, BossPatterns.BeamDegrees(beams, 0f, 1.125f, 3)));
        }
    }
}
