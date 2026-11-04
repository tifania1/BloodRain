using System;
using System.Linq;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Run;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>QA-09(2026-11-04 Core 점검 3): 오늘 들어온 Core 코드에서 찾아 고친 것들.</summary>
    public class CoreQa3Tests
    {
        readonly GameData _d = TestData.Load();
        CheonhaDef C => _d.Cheonha;

        static GameData Fresh() => GameData.FromJson(TestData.ReadFiles());

        static RebirthQiResult NoQi(GameData d) => new RebirthQiLedger(d.Cheonha.RebirthQi).Settle(0);

        Soul AfterPrologue()
        {
            var s = new Soul(_d);
            var life = s.BeginLife(Rebirth.Prologue(_d));
            s.EndLife(life.Finish("fallen", 0));
            return s;
        }

        LifeRecord Sealed(string sect, int manuscripts = 0) =>
            new LifeRecord(sect, "sealed", NoQi(_d), Array.Empty<string>(), Array.Empty<string>(), "hyeongyeong", Array.Empty<string>(), manuscripts);

        // ---- 운낭 시설 대사 ----

        [Fact] public void 운낭_시설_대사는_객잔_시설_id_를_쓴다()
        {
            var ids = C.Inn.Facilities.Select(f => f.Id).ToHashSet();
            var lines = _d.Dialogue.Unnang.Lines.Where(l => l.Kind == "facility").ToList();
            Assert.Equal(4, lines.Count);
            Assert.All(lines, l => Assert.Contains(l.Args[0], ids));
            var opens = _d.Dialogue.Unnang.Lines.Concat(_d.Dialogue.Mok.Lines).Where(l => l.Opens != null).ToList();
            Assert.All(opens, l => Assert.Contains(l.Opens!, ids));
        }

        [Fact] public void 데이터_검사가_없는_시설_대사를_잡는다()
        {
            var d = Fresh();
            d.Dialogue.Unnang.Lines.First(l => l.Kind == "facility").When = "facility:jangyeonggak";
            Assert.Contains(DataCheck.Problems(d), p => p.Contains("facility:jangyeonggak"));
        }

        [Fact] public void 장경각이_열린_귀환에_운낭이_장경각_줄을_한다()
        {
            var p = new DialogueProgress();
            var s = new Soul(_d);
            var pro = s.BeginLife(Rebirth.Prologue(_d)).Finish("fallen", 0);
            InnDialogue.OnReturn(_d, s, pro, s.EndLife(pro), p, new ReturnFacts(), new Random(1));
            InnReturn? hit = null;
            for (int i = 0; i < 4 && hit == null; i++)
            {
                var rec = new LifeRecord("shaolin", LifeEndWay.Fallen, NoQi(_d), i == 2 ? new[] { "songshan" } : Array.Empty<string>(),
                    Array.Empty<string>(), "iryu", Array.Empty<string>());
                var summary = s.EndLife(rec);
                var facts = new ReturnFacts();
                facts.NewFacilities.AddRange(summary.NewFacilities);
                var r = InnDialogue.OnReturn(_d, s, rec, summary, p, facts, new Random(1));
                if (summary.NewFacilities.Contains("library")) hit = r;
            }
            Assert.NotNull(hit);
            // 첫·둘째 귀환 줄이 먼저면 밀려 다음에 나온다. 어쨌든 차례에 들어가야 한다.
            Assert.True(hit!.Unnang.Any(l => l.Id == "facility_jangyeonggak") || p.Pending.Contains("unnang.facility_jangyeonggak"));
        }

        // ---- 엔딩·모드 ----

        [Fact] public void 봉인_엔딩을_고르면_혈월_천하와_보스_연전이_새로_열렸다고_알린다()
        {
            var s = AfterPrologue();
            s.EndLife(Sealed("shaolin"));
            var opened = s.ChooseEnding(Soul.EndingSeal, true);
            Assert.Contains("bloodmoon", opened);
            Assert.Contains("boss_rush", opened);
            Assert.Empty(Modes.Refresh(s));   // 한 번만 알린다
        }

        [Fact] public void 종장_도중_진엔딩_조건은_이번_생을_더해_싸움_뒤와_같은_답()
        {
            var s = AfterPrologue();
            s.EndLife(Sealed("shaolin"));
            s.ChooseEnding(Soul.EndingSeal, true);
            s.EndLife(Sealed("gaebang", manuscripts: 4));
            s.ChooseEnding(Soul.EndingSeal, true);
            s.EndLife(Sealed("namgung", manuscripts: 2));
            s.ChooseEnding(Soul.EndingSeal, true);
            // 넷째 문파(tang)로 종장을 싸우는 중, 이번 생에 의서 한 권.
            Assert.False(s.TrueEndingMet(true));                              // 혼 장부만 보면 아직
            Assert.True(s.TrueEndingMetDuringFinal(true, "tang", 1));
            Assert.False(s.TrueEndingMetDuringFinal(true, "shaolin", 1));     // 이미 센 문파
            Assert.False(s.TrueEndingMetDuringFinal(true, "tang", 0));        // 의서 여섯 권
            Assert.False(s.TrueEndingMetDuringFinal(false, "tang", 1));
            s.EndLife(Sealed("tang", manuscripts: 1));
            Assert.Contains(Soul.EndingTrue, s.EndingsOpen(true));
        }

        [Fact] public void 대사_진행_저장에_빈_목록이_null_이어도_읽는다()
        {
            var p = DialogueProgress.FromJson("{\"Version\":1,\"Seen\":null,\"Pending\":null,\"Joined\":null,\"PendingJoins\":null,\"InnNext\":null}");
            Assert.Empty(p.Pending);
        }

        // ---- 땅의 주인 패턴 시간 ----

        static System.Collections.Generic.List<BossPatternEvent> Run(BossPatterns b, float until, float dt, Action<float>? each = null)
        {
            var all = new System.Collections.Generic.List<BossPatternEvent>();
            while (b.Elapsed < until)
            {
                all.AddRange(b.Tick(dt, 1f));
                each?.Invoke(b.Elapsed);
            }
            return all;
        }

        [Fact] public void 태극권은_빨아들이는_동안에도_보스가_멈춰_선다()
        {
            var set = _d.BossPatternSet("hyeolheo");
            var b = new BossPatterns(set);
            var taiji = set.Pattern("taiji_inhale_exhale");
            float? at = null;
            var events = Run(b, 30f, 0.05f, t =>
            {
                if (at is float a && t > a + taiji.TelegraphSeconds + 0.1f && t < a + BossPatterns.CastSeconds(taiji) - 0.1f)
                    Assert.True(b.Holding, $"{t - a:0.00}초에 멈춰 서 있지 않음");
            });
            at = events.First(e => e.Kind == BossPatternEventKind.Telegraph && e.Pattern?.Id == taiji.Id).At;
            b = new BossPatterns(set);
            Run(b, at.Value + BossPatterns.CastSeconds(taiji), 0.05f, t =>
            {
                if (t > at.Value + taiji.TelegraphSeconds + 0.1f && t < at.Value + BossPatterns.CastSeconds(taiji) - 0.1f)
                    Assert.True(b.Holding, $"{t - at.Value:0.00}초에 멈춰 서 있지 않음");
            });
        }

        [Fact] public void 순간이동_내려찍기는_마지막_자리에서_찍을_때_판정한다()
        {
            var set = _d.BossPatternSet("hyeokryeon_hwi");
            var blink = set.Pattern("blink_x4_slam");
            var b = new BossPatterns(set);
            var events = Run(b, 60f, 0.05f);
            var tel = events.First(e => e.Kind == BossPatternEventKind.Telegraph && e.Pattern?.Id == blink.Id);
            var res = events.First(e => e.Kind == BossPatternEventKind.Resolve && e.Pattern?.Id == blink.Id && e.Cast == tel.Cast);
            Assert.Equal(BossPatterns.BlinkSlamSeconds(blink), res.At - tel.At, 3);
            Assert.True(BossPatterns.BlinkSlamSeconds(blink) > blink.TelegraphSeconds);
        }

        [Fact] public void 맥_짚는_손은_최소_반경에_닿는_순간이_마지막이다()
        {
            var p = _d.FinalBoss.Stages.SelectMany(st => st.Set.Patterns).First(x => x.Id == "pulse_ring");
            float close = BossPatterns.ClosingSeconds(p);
            Assert.True(BossPatterns.InClosingRing(p, close, 0f, 0f));
            Assert.True(BossPatterns.ClosingRingAlive(p, close));
            Assert.False(BossPatterns.InClosingRing(p, close + 0.5f, 0f, 0f));
            Assert.False(BossPatterns.InClosingRing(p, 10f, 0.5f, 0f));
            Assert.False(BossPatterns.ClosingRingAlive(p, close + 0.01f));
        }

        [Fact] public void 데이터_검사가_줄지_않는_고리와_끌지_않는_태극권을_잡는다()
        {
            var d = Fresh();
            d.FinalBoss.Stages.SelectMany(st => st.Set.Patterns).First(x => x.Id == "pulse_ring").ShrinkPerSecond = 0f;
            d.BossPatternSet("hyeolheo").Pattern("taiji_inhale_exhale").PullSeconds = 0f;
            var problems = DataCheck.Problems(d);
            Assert.Contains(problems, x => x.Contains("pulse_ring.shrinkPerSecond"));
            Assert.Contains(problems, x => x.Contains("taiji_inhale_exhale.pullSeconds"));
        }

        // ---- 오의 ----

        [Fact] public void 천마해체는_음수_프레임_시간으로_늘어나지_않는다()
        {
            var d = new DemonDissolution(_d.Ultimates.Single(u => u.Id == UltimateEffects.CheonmaHaeche));
            d.Start();
            d.Tick(5f);
            float left = d.SecondsLeft;
            d.Tick(-1f);
            Assert.Equal(left, d.SecondsLeft);
        }
    }
}
