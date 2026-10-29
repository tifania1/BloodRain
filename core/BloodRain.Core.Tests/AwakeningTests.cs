using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>절기 각성(J-01): 연출 시간표, 세상 시간 배율, 파동 판정, 여운·적 생성 멈춤·무적.</summary>
    public class AwakeningTests
    {
        readonly GameData _d = Load();
        AwakeningDef A => _d.Balance.Awakening;
        EvolutionDef Cheonjiin => _d.Evolutions.First(e => e.Id == "cheonjiin");
        EvolutionDef Other => _d.Evolutions.First(e => e.Id != "cheonjiin");

        Awakening Begin(bool firstOnAccount = true, AwakeningLength setting = AwakeningLength.Auto)
        {
            var a = new Awakening(A);
            a.Start(Cheonjiin, firstOnAccount, setting);
            return a;
        }

        /// <summary>프로토타입 프레임 루프처럼 민다: 연출 시계는 실제 시간, 게임은 실제 시간 × 세상 배율.
        /// 지나간 장면과 그때의 실제 시각을 돌려준다.</summary>
        static List<(AwakeningCue Cue, float At)> Run(Awakening a, float seconds, float dt = 1f / 60f)
        {
            var log = new List<(AwakeningCue, float)>();
            void OnCue(AwakeningCue c) => log.Add((c, a.Elapsed));
            a.Cue += OnCue;
            for (float t = 0f; t < seconds; t += dt)
            {
                a.Tick(dt);
                a.TickWorld(dt * a.WorldTimeScale);
            }
            a.Cue -= OnCue;
            return log;
        }

        /// <summary>연출이 끝나는 프레임까지만 Run 처럼 민다.</summary>
        static void RunToEnd(Awakening a, float dt = 1f / 60f)
        {
            for (int i = 0; a.IsActive && i < 100000; i++)
            {
                a.Tick(dt);
                a.TickWorld(dt * a.WorldTimeScale);
            }
        }

        /// <summary>연출 시계를 정확히 그 시각으로 옮긴다(세상 시간은 밀지 않는다).</summary>
        static AwakeningCue AdvanceTo(Awakening a, float t) => a.Tick(t - a.Elapsed);

        // ---- 긴 연출 / 짧은 연출 ----

        [Fact] public void 처음_깨우는_절기는_긴_연출_3점5초()
        {
            var a = Begin(firstOnAccount: true);
            Assert.True(a.IsActive);
            Assert.True(a.IsLong);
            Assert.Equal(3.5f, a.Timeline.DurationSeconds);
            Assert.Equal(2.3f, a.Timeline.ShockSeconds);
            Assert.Same(Cheonjiin, a.Evolution);
        }

        [Fact] public void 이미_깨운_절기는_짧은_연출_1점5초()
        {
            var a = Begin(firstOnAccount: false);
            Assert.False(a.IsLong);
            Assert.Equal(1.5f, a.Timeline.DurationSeconds);
            Assert.Equal(0.64f, a.Timeline.ShockSeconds);
        }

        [Theory]
        [InlineData(AwakeningLength.Auto, true, true)]
        [InlineData(AwakeningLength.Auto, false, false)]
        [InlineData(AwakeningLength.Long, false, true)]
        [InlineData(AwakeningLength.Short, true, false)]
        public void 설정이_자동보다_앞선다(AwakeningLength setting, bool firstOnAccount, bool expectedLong)
        {
            Assert.Equal(expectedLong, Awakening.ChooseLong(setting, firstOnAccount));
            Assert.Equal(expectedLong, Begin(firstOnAccount, setting).IsLong);
        }

        [Fact] public void 서장_고정_각성은_설정과_상관없이_길게()
        {
            var a = new Awakening(A);
            a.Start(Cheonjiin, firstOnAccount: false, AwakeningLength.Short, forceLong: true);
            Assert.True(a.IsLong);
            var b = new Awakening(A);
            b.Start(Cheonjiin, firstOnAccount: true, AwakeningLength.Long, forceLong: false);
            Assert.False(b.IsLong);
        }

        // ---- 장면 시간표 ----

        [Fact] public void 긴_연출은_문턱_균열_이름_낙관_파동_끝_순서로_온다()
        {
            const float dt = 1f / 60f;
            var a = Begin(firstOnAccount: true);
            var log = Run(a, 4f, dt);
            Assert.Equal(new[] { AwakeningCue.Glyph, AwakeningCue.Crack, AwakeningCue.Name, AwakeningCue.Stamp, AwakeningCue.Shock, AwakeningCue.End },
                log.Select(x => x.Cue).ToArray());
            var expected = new[] { 0.45f, 0.95f, 1.15f, 1.8f, 2.3f, 3.5f };
            for (int i = 0; i < expected.Length; i++)
                Assert.InRange(log[i].At, expected[i], expected[i] + dt * 1.01f); // 그 시각을 넘긴 첫 프레임
            Assert.False(a.IsActive);
        }

        [Fact] public void 짧은_연출에는_문턱과_균열이_없다()
        {
            const float dt = 1f / 60f;
            var a = Begin(firstOnAccount: false);
            var log = Run(a, 2f, dt);
            Assert.Equal(new[] { AwakeningCue.Name, AwakeningCue.Stamp, AwakeningCue.Shock, AwakeningCue.End },
                log.Select(x => x.Cue).ToArray());
            var expected = new[] { 0.1f, 0.42f, 0.64f, 1.5f };
            for (int i = 0; i < expected.Length; i++)
                Assert.InRange(log[i].At, expected[i], expected[i] + dt * 1.01f);
        }

        [Fact] public void 긴_프레임_하나에_지나간_장면은_한꺼번에_순서대로_알린다()
        {
            var a = Begin(firstOnAccount: true);
            var order = new List<AwakeningCue>();
            a.Cue += c => order.Add(c);
            var cues = a.Tick(5f);
            Assert.Equal(AwakeningCue.Glyph | AwakeningCue.Crack | AwakeningCue.Name | AwakeningCue.Stamp | AwakeningCue.Shock | AwakeningCue.End, cues);
            Assert.Equal(new[] { AwakeningCue.Glyph, AwakeningCue.Crack, AwakeningCue.Name, AwakeningCue.Stamp, AwakeningCue.Shock, AwakeningCue.End }, order);
            Assert.False(a.IsActive);
            Assert.Equal(1.5f, a.DamageMultiplier(Cheonjiin.Weapon)); // 파동을 건너뛰어도 여운은 걸린다
            Assert.True(a.SpawnPaused);
        }

        [Fact] public void 같은_장면은_두_번_오지_않는다()
        {
            var a = Begin();
            Assert.Equal(AwakeningCue.Glyph, AdvanceTo(a, 0.5f));
            Assert.Equal(AwakeningCue.None, a.Tick(0.1f));
            Assert.Equal(AwakeningCue.None, a.Tick(0f));
        }

        [Fact] public void 연출이_없으면_아무_일도_없다()
        {
            var a = new Awakening(A);
            Assert.Equal(AwakeningCue.None, a.Tick(1f));
            a.TickWorld(1f);
            Assert.False(a.IsActive);
            Assert.Equal(1f, a.WorldTimeScale);
            Assert.False(a.IsPlayerInvulnerable);
            Assert.False(a.SpawnPaused);
            Assert.False(a.DefersLevelUpAndChest);
            Assert.Equal(1f, a.DamageMultiplier(Cheonjiin.Weapon));
        }

        // ---- 세상 시간 ----

        [Fact] public void 세상_시간은_0점04배로_느려졌다가_파동_뒤_0점25초에_돌아온다()
        {
            var a = Begin(firstOnAccount: true);
            Assert.Equal(1f, a.WorldTimeScale);                // 시작 순간
            AdvanceTo(a, 0.09f);
            Assert.Equal(0.52f, a.WorldTimeScale, 3);          // 0.18초에 걸쳐 느려지는 중간
            AdvanceTo(a, 1.0f);
            Assert.Equal(0.04f, a.WorldTimeScale, 4);
            AdvanceTo(a, 2.29f);
            Assert.Equal(0.04f, a.WorldTimeScale, 4);          // 파동 직전까지
            Assert.False(a.ShockDone);
            AdvanceTo(a, 2.3f + 0.125f);
            Assert.True(a.ShockDone);
            Assert.Equal(0.52f, a.WorldTimeScale, 3);          // 파동 뒤 0.25초의 절반
            AdvanceTo(a, 2.6f);
            Assert.Equal(1f, a.WorldTimeScale, 4);
            AdvanceTo(a, 3.6f);
            Assert.False(a.IsActive);
            Assert.Equal(1f, a.WorldTimeScale);
        }

        [Fact] public void 짧은_연출도_파동까지_0점04배()
        {
            var a = Begin(firstOnAccount: false);
            AdvanceTo(a, 0.5f);
            Assert.Equal(0.04f, a.WorldTimeScale, 4);
            AdvanceTo(a, 0.64f + 0.125f);
            Assert.Equal(0.52f, a.WorldTimeScale, 3);
        }

        // ---- 무적과 미루기 ----

        [Fact] public void 연출_동안_플레이어는_무적이고_레벨업_창은_미룬다()
        {
            var a = Begin();
            Assert.True(a.IsPlayerInvulnerable);
            Assert.True(a.DefersLevelUpAndChest);
            AdvanceTo(a, 3f);
            Assert.True(a.IsPlayerInvulnerable);
            Assert.True(a.DefersLevelUpAndChest);
            AdvanceTo(a, 3.5f);
            Assert.False(a.DefersLevelUpAndChest);
        }

        [Fact] public void 무적은_연출_길이_더하기_0점8초를_세상_시간으로_센다()
        {
            // 프로토타입: player.iframe = max(iframe, dur + 0.8), 세상 시간으로 줄어든다
            var a = Begin(firstOnAccount: true);
            AdvanceTo(a, 4f);                    // 연출은 끝났지만 세상 시간은 아직 하나도 안 흘렀다
            Assert.False(a.IsActive);
            Assert.True(a.IsPlayerInvulnerable);
            a.TickWorld(4.2f);
            Assert.True(a.IsPlayerInvulnerable);
            a.TickWorld(0.2f);                   // 3.5 + 0.8 = 4.3
            Assert.False(a.IsPlayerInvulnerable);
        }

        [Fact] public void 프레임마다_밀면_연출이_끝난_뒤에도_잠깐_무적이_남는다()
        {
            var a = Begin(firstOnAccount: true);
            Run(a, 3.6f);
            Assert.False(a.IsActive);
            Assert.True(a.IsPlayerInvulnerable);  // 연출 동안 세상 시간은 1.3초 남짓만 흘렀다
            a.TickWorld(3.5f);
            Assert.False(a.IsPlayerInvulnerable);
        }

        // ---- 파동 판정 ----

        [Fact] public void 파동_반경은_화면_대각선의_0점62()
        {
            var a = new Awakening(A);
            Assert.Equal(3.1f, a.ShockRadius(3f, 4f), 4);
            Assert.Equal((float)Math.Sqrt(16 * 16 + 9 * 9) * 0.62f, a.ShockRadius(16f, 9f), 4);
        }

        [Fact] public void 파동은_반경_안_잡몹을_쓸고_정예_15퍼센트_보스_4퍼센트()
        {
            var a = Begin();
            var targets = new List<ShockTarget>
            {
                new ShockTarget(ShockTargetKind.Normal, 20f, 3f),         // 0 안: 처치
                new ShockTarget(ShockTargetKind.Normal, 20f, 12.5f),      // 1 바깥
                new ShockTarget(ShockTargetKind.Elite, 1200f, 6f),        // 2 안: 180
                new ShockTarget(ShockTargetKind.Boss, 180000f, 9f),       // 3 안: 7200
                new ShockTarget(ShockTargetKind.Normal, 234f, 10f),       // 4 경계 위: 맞는다
                new ShockTarget(ShockTargetKind.Elite, 1200f, 10.001f),   // 5 경계 바로 밖
            };
            var r = a.ResolveShock(targets, 10f);

            Assert.Equal(new[] { 0, 2, 3, 4 }, r.Hits.Select(h => h.Index).ToArray());
            Assert.Equal(2, r.Kills);

            var normal = r.Hits[0];
            Assert.True(normal.Kill);
            Assert.Equal(20f, normal.Damage);      // 기록용: 최대 기혈만큼
            Assert.Equal(0f, normal.Knockback);

            var elite = r.Hits[1];
            Assert.False(elite.Kill);
            Assert.Equal(ShockTargetKind.Elite, elite.Kind);
            Assert.Equal(180f, elite.Damage, 3);
            Assert.Equal(8f, elite.Knockback);

            var boss = r.Hits[2];
            Assert.False(boss.Kill);
            Assert.Equal(7200f, boss.Damage, 1);
            Assert.Equal(0f, boss.Knockback);      // 보스는 밀리지 않는다

            Assert.True(r.Hits[3].Kill);
        }

        [Fact] public void 파동은_적_탄을_모두_지우고_그_절기_대기를_돌린다()
        {
            var r = Begin().ResolveShock(new List<ShockTarget>(), 10f);
            Assert.Empty(r.Hits);
            Assert.True(r.ClearEnemyProjectiles);
            Assert.True(r.ResetCooldown);
        }

        // ---- 여운과 적 생성 멈춤 ----

        [Fact] public void 여운_8초_동안_깨어난_그_절기만_피해_1점5배()
        {
            var a = Begin();
            AdvanceTo(a, 2.2f);
            Assert.Equal(1f, a.DamageMultiplier(Cheonjiin.Weapon));   // 파동 전에는 아직
            AdvanceTo(a, 2.4f);
            Assert.Same(Cheonjiin, a.AfterglowEvolution);
            Assert.Equal(1.5f, a.DamageMultiplier(Cheonjiin.Weapon));
            Assert.Equal(1f, a.DamageMultiplier(Other.Weapon));
            a.TickWorld(7.5f);
            Assert.Equal(1.5f, a.DamageMultiplier(Cheonjiin.Weapon));
            a.TickWorld(0.5f);
            Assert.Equal(1f, a.DamageMultiplier(Cheonjiin.Weapon));
            Assert.Null(a.AfterglowEvolution);
        }

        [Fact] public void 파동_뒤_2점5초_동안_적이_새로_몰려오지_않는다()
        {
            var a = Begin();
            AdvanceTo(a, 2f);
            Assert.False(a.SpawnPaused);
            AdvanceTo(a, 2.5f);
            Assert.True(a.SpawnPaused);
            a.TickWorld(2.25f);
            Assert.True(a.SpawnPaused);
            a.TickWorld(0.25f);
            Assert.False(a.SpawnPaused);
        }

        [Fact] public void 여운과_생성_멈춤은_세상_시간으로_줄어든다()
        {
            // 파동에서 연출 끝까지 실제로는 1.2초지만, 앞의 0.25초는 시간이 느려서 세상 시간은 1.08초쯤
            var a = Begin(firstOnAccount: true);
            RunToEnd(a);
            Assert.InRange(a.AfterglowRemaining, 6.85f, 7.0f);
            Assert.True(a.SpawnPaused);
        }

        [Fact] public void 다음_각성의_파동이_오면_여운이_새_절기로_넘어간다()
        {
            var a = Begin(firstOnAccount: true);
            a.Tick(4f);
            a.TickWorld(1f);
            a.Start(Other, firstOnAccount: false);
            AdvanceTo(a, 0.3f);                                        // 짧은 연출, 파동 전
            Assert.Equal(1.5f, a.DamageMultiplier(Cheonjiin.Weapon));
            AdvanceTo(a, 0.7f);
            Assert.Same(Other, a.AfterglowEvolution);
            Assert.Equal(1f, a.DamageMultiplier(Cheonjiin.Weapon));
            Assert.Equal(1.5f, a.DamageMultiplier(Other.Weapon));
            Assert.Equal(8f, a.AfterglowRemaining);
        }

        // ---- 이어서 시작하기 ----

        [Fact] public void 연출_중에_또_시작하면_예외()
        {
            var a = Begin();
            Assert.Throws<InvalidOperationException>(() => a.Start(Other, true));
        }

        [Fact] public void 끝_신호를_받은_자리에서_다음_각성을_시작할_수_있다()
        {
            var a = Begin(firstOnAccount: false);
            a.Cue += c => { if (c == AwakeningCue.End && a.Evolution == Cheonjiin) a.Start(Other, firstOnAccount: true); };
            a.Tick(2f);
            Assert.True(a.IsActive);
            Assert.Same(Other, a.Evolution);
            Assert.True(a.IsLong);
            Assert.Equal(0f, a.Elapsed);
        }

        // ---- 데이터 ----

        [Fact] public void 각성_수치를_balance_json에서_읽는다()
        {
            Assert.Null(A.Short.GlyphSeconds);   // 짧은 연출엔 문턱·균열 장면이 없다(JSON null)
            Assert.Null(A.Short.CrackSeconds);
            Assert.Equal(0.45f, A.Long.GlyphSeconds);
            Assert.Equal(0.95f, A.Long.CrackSeconds);
            Assert.Equal(0.04f, A.WorldTimeScale);
            Assert.Equal(0.62f, A.ShockRadiusViewDiagonal);
            Assert.Equal(0.15f, A.EliteMaxHealthRatio);
            Assert.Equal(0.04f, A.BossMaxHealthRatio);
            Assert.Equal(2.5f, A.SpawnPauseSeconds);
            Assert.Equal(8f, A.AfterglowSeconds);
            Assert.Equal(1.5f, A.AfterglowDamageMultiplier);
        }

        [Fact] public void 장면_시각은_순서대로이고_시간은_연출_안에서_돌아온다()
        {
            foreach (var p in new[] { A.Long, A.Short })
            {
                var times = new[] { p.GlyphSeconds, p.CrackSeconds, p.NameSeconds, p.StampSeconds, p.ShockSeconds, p.DurationSeconds }
                    .Where(x => x != null).Select(x => x!.Value).ToArray();
                for (int i = 1; i < times.Length; i++) Assert.True(times[i - 1] < times[i], $"장면 시각이 거꾸로: {times[i - 1]} ≥ {times[i]}");
                Assert.True(A.TimeScaleInSeconds < p.ShockSeconds, "파동 전에 다 느려져야 한다");
                Assert.True(p.ShockSeconds + A.TimeScaleOutSeconds <= p.DurationSeconds, "연출이 끝나기 전에 시간이 돌아와야 한다");
            }
            Assert.True(A.Long.DurationSeconds > A.Short.DurationSeconds);
        }

        [Fact] public void 각성_연출_표가_기획서와_같다()
        {
            var gdd = ReadRepoFile("docs", "design", "gdd.md");
            int start = gdd.IndexOf("**각성 연출**");
            Assert.True(start >= 0);
            var section = gdd.Substring(start, gdd.IndexOf("### ", start) - start);
            Assert.Contains($"처음 깨우는 절기는 {Num(A.Long.DurationSeconds)}초, 그다음부터 {Num(A.Short.DurationSeconds)}초", section);

            var rows = section.Split('\n')
                .Where(l => l.StartsWith("| ") && !l.StartsWith("| 시각") && !l.StartsWith("| ---"))
                .Select(l => l.Split('|').Select(c => c.Trim()).ToArray())
                .ToDictionary(c => c[2], c => c);
            string Cell(float? v) => v == null ? "없음" : Num(v.Value);
            string At(Func<AwakeningTimelineDef, float?> f) => $"{Cell(f(A.Long))} / {Cell(f(A.Short))}";

            Assert.Equal("0 / 0", rows["흡기"][1]);
            Assert.Equal(At(p => p.GlyphSeconds), rows["문턱"][1]);
            Assert.Equal(At(p => p.CrackSeconds), rows["균열"][1]);
            Assert.Equal(At(p => p.NameSeconds), rows["이름"][1]);
            Assert.Equal(At(p => p.StampSeconds), rows["낙관"][1]);
            Assert.Equal(At(p => p.ShockSeconds), rows["파동"][1]);
            Assert.Equal($"{Num(A.Long.ShockSeconds)}\\~{Num(A.Long.DurationSeconds)} / {Num(A.Short.ShockSeconds)}\\~{Num(A.Short.DurationSeconds)}", rows["여운"][1]);

            Assert.Contains($"시간 {Num(A.WorldTimeScale)}배", rows["흡기"][3]);
            Assert.Contains($"정예는 기혈 {Num(A.EliteMaxHealthRatio * 100)}%, 보스는 {Num(A.BossMaxHealthRatio * 100)}%를 잃음", rows["파동"][3]);
            Assert.Contains("적 탄을 지움", rows["파동"][3]);
            Assert.Contains($"금빛 기운 {Num(A.AfterglowSeconds)}초(그 절기 피해 {Num(A.AfterglowDamageMultiplier)}배), {Num(A.SpawnPauseSeconds)}초 동안 적이 새로 몰려오지 않음", rows["여운"][3]);
        }

        [Fact] public void 웹_프로토타입의_각성_숫자와_같다()
        {
            // 프로토타입(template.html)은 같은 규칙을 먼저 만든 참고 구현이다. 어느 한쪽만 고치면 여기서 걸린다.
            var html = ReadRepoFile("web", "prototype", "template.html");
            float Grab(string text, string pattern, int group = 1)
            {
                var m = Regex.Match(text, pattern);
                Assert.True(m.Success, $"template.html 에서 /{pattern}/ 를 못 찾았습니다. 프로토타입 코드가 바뀌었으면 이 패턴도 같이 고친다");
                return float.Parse(m.Groups[group].Value, CultureInfo.InvariantCulture);
            }
            float? GrabOrNull(string text, string field)
            {
                var m = Regex.Match(text, $@"\b{field}:\s*(null|\[?\s*([\d.]+))");
                Assert.True(m.Success, $"AW 에서 {field} 를 못 찾았습니다");
                return m.Groups[1].Value == "null" ? (float?)null : float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            }

            var aw = Regex.Match(html, @"const AW = \{\s*long:\s*\{([^}]*)\},\s*short:\s*\{([^}]*)\}");
            Assert.True(aw.Success, "template.html 에서 const AW 를 못 찾았습니다");
            foreach (var (text, p) in new[] { (aw.Groups[1].Value, A.Long), (aw.Groups[2].Value, A.Short) })
            {
                Assert.Equal(p.DurationSeconds, GrabOrNull(text, "dur"));
                Assert.Equal(p.GlyphSeconds, GrabOrNull(text, "glyph"));   // [시작, 끝] 중 시작
                Assert.Equal(p.CrackSeconds, GrabOrNull(text, "crack"));
                Assert.Equal(p.NameSeconds, GrabOrNull(text, "name"));
                Assert.Equal(p.StampSeconds, GrabOrNull(text, "stamp"));
                Assert.Equal(p.ShockSeconds, GrabOrNull(text, "shock"));
            }

            // tickAwaken: 세상 시간 배율
            Assert.Equal(A.WorldTimeScale, Grab(html, @"lerp\(1,\s*([\d.]+),\s*clamp\(t / ([\d.]+)"));
            Assert.Equal(A.TimeScaleInSeconds, Grab(html, @"lerp\(1,\s*([\d.]+),\s*clamp\(t / ([\d.]+)", 2));
            Assert.Equal(A.WorldTimeScale, Grab(html, @"lerp\(([\d.]+),\s*1,\s*clamp\(\(t - P\.shock\) / ([\d.]+)"));
            Assert.Equal(A.TimeScaleOutSeconds, Grab(html, @"lerp\(([\d.]+),\s*1,\s*clamp\(\(t - P\.shock\) / ([\d.]+)", 2));

            // startAwaken: 무적
            Assert.Equal(A.InvulnerableExtraSeconds, Grab(html, @"iframe,\s*P\.dur \+ ([\d.]+)\)"));

            // awakenShock: 반경, 정예·보스, 여운, 생성 멈춤
            Assert.Equal(A.ShockRadiusViewDiagonal, Grab(html, @"Math\.hypot\(viewW\(\), viewH\(\)\) \* ([\d.]+)"));
            Assert.Equal(A.BossMaxHealthRatio, Grab(html, @"if \(e\.boss\) hurtEnemy\(e, e\.maxHp \* ([\d.]+)"));
            Assert.Equal(A.EliteMaxHealthRatio, Grab(html, @"else if \(e\.elite\) hurtEnemy\(e, e\.maxHp \* ([\d.]+), A\.w, dx / d, dy / d, ([\d.]+)\)"));
            Assert.Equal(A.EliteKnockback, Grab(html, @"else if \(e\.elite\) hurtEnemy\(e, e\.maxHp \* ([\d.]+), A\.w, dx / d, dy / d, ([\d.]+)\)", 2));
            Assert.Equal(A.AfterglowSeconds, Grab(html, @"G\.glow = \{ w: A\.w, t: ([\d.]+) \}"));
            Assert.Equal(A.SpawnPauseSeconds, Grab(html, @"G\.spawnHold = ([\d.]+)"));

            // weaponDamage: 여운 배율
            Assert.Equal(A.AfterglowDamageMultiplier, Grab(html, @"G\.glow\.w === w\) d \*= ([\d.]+)"));
        }
    }
}
