using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Progression;
using BloodRain.Core.Run;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>서장 몸으로 만든 Life 가 서장 10분을 묶는지(기획서 4장 '서장 상세 흐름', 21장, backlog M1-02). 가짜 시계로 돈다.</summary>
    public class PrologueLifeTests
    {
        readonly GameData _d = TestData.Load();

        (Soul soul, Life life) NewPrologue(int seed = 1)
        {
            var soul = new Soul(_d);
            return (soul, new Life(soul, Rebirth.Prologue(_d), new Random(seed)));
        }

        /// <summary>4장 표 시각쯤에 경지를 넘도록 진기를 먹이며 0.1초씩 until 분까지 돈다(이류 2:45, 일류 6:10).</summary>
        static List<(float minute, Spawning.LifeSecond s)> Run(GameData d, Life life, float until)
        {
            var curve = new XpCurve(d.Balance.XpCurve);
            var seconds = new List<(float, Spawning.LifeSecond)>();
            double fed = 0;
            int guard = 0;
            while (life.Minutes < until && guard++ < 100000)
            {
                var s = life.Tick(0.1f, life.Position, 40);
                if (s != null) seconds.Add((life.Minutes, s));
                float m = life.Minutes;
                int target = m < 2.75f ? 1 + (int)(m / 2.75f * 9) : m < 37f / 6f ? 10 + (int)((m - 2.75f) / (37f / 6f - 2.75f) * 10) : 20 + (int)((m - 37f / 6f));
                double need = curve.CumulativeTo(Math.Min(target, 34)) - fed;
                if (need > 0) { life.Absorb(need); fed += need; }
            }
            return seconds;
        }

        [Fact] public void 서장_몸이면_서장_차례와_서장_생성이_붙는다()
        {
            var (_, life) = NewPrologue();
            Assert.True(life.IsPrologue);
            Assert.NotNull(life.Prologue);
            Assert.NotNull(life.PrologueSpawns);
            Assert.Equal("cheongun", life.RegionId);
            Assert.Equal(PrologueStep.Fight, life.Prologue!.Step);
        }

        [Fact] public void 한_생_몸에는_서장이_없다()
        {
            var soul = new Soul(_d);
            soul.EndLife(soul.BeginLife(Rebirth.Prologue(_d)).Finish(LifeEndWay.Fallen, 0));
            var c = _d.Cheonha;
            var cand = new RebirthCandidate(c.Sect("shaolin"), c.Region(c.Sect("shaolin").Region), Array.Empty<BirthTraitDef>());
            var life = new Life(soul, Rebirth.Body(c, cand), new Random(1));
            Assert.False(life.IsPrologue);
            Assert.Null(life.PrologueSpawns);
            Assert.Throws<InvalidOperationException>(() => life.PlayerDown());
        }

        [Fact] public void 가짜_시계_9분_서장_표대로_생성하고_사건이_제때_나온다()
        {
            var (_, life) = NewPrologue();
            var seconds = Run(_d, life, 9.05f);
            var prologue = _d.Chapters.First(c => c.Id == "prologue");

            // 서장 가중치표는 시강뿐
            var kinds = seconds.SelectMany(x => x.s.Spawns).Select(x => x.Enemy).Distinct().ToList();
            Assert.Equal(new[] { "sigang" }, kinds);

            // 8:30~9:00 은 새 적을 만들지 않는다
            var stopped = seconds.Where(x => x.minute > prologue.StopSpawningAtMinute!.Value + 0.01f && x.minute < 8.99f).ToList();
            Assert.NotEmpty(stopped);
            Assert.All(stopped, x => { Assert.Equal(0f, x.s.Target); Assert.Empty(x.s.Spawns); });

            // 사건: 3:30 돌진, 5:00 도부수, 9:00 장삼. 한 번씩, 제 시각에
            var events = seconds.SelectMany(x => x.s.Events.Select(e => (x.minute, e))).ToList();
            foreach (var e in prologue.Events)
            {
                var hit = Assert.Single(events, x => x.e.Wave.Def.Type == e.Type && x.e.Enemy == e.Enemy);
                Assert.InRange(hit.minute, e.At, e.At + 1.5f / 60f);
            }

            // 장삼이 나오면 차례가 Boss 로
            Assert.Equal(PrologueStep.Boss, life.Prologue!.Step);
            Assert.Equal("ilryu", life.Log.HighestRealm);
        }

        [Fact] public void 서장_돌파는_서장_생성을_멈춘다()
        {
            var (_, life) = NewPrologue();
            var curve = new XpCurve(_d.Balance.XpCurve);
            var r = life.Absorb(curve.CumulativeTo(10));
            Assert.Single(r.Breakthroughs);
            Assert.Equal(_d.Cheonha.Breakthrough.SpawnHoldSeconds, life.PrologueSpawns!.HoldRemaining, 3);
        }

        [Fact] public void 장삼_전에_쓰러지면_처음부터_혼에는_아무것도_안_남는다()
        {
            var (soul, life) = NewPrologue();
            Run(_d, life, 4f);
            Assert.Equal(PrologueDown.Restart, life.PlayerDown());
            var again = life.RestartPrologue(new Random(2));
            Assert.Equal(0f, again.Minutes);
            Assert.Equal(1, again.Level);
            Assert.Equal(1, again.Prologue!.Restarts);
            Assert.Equal(PrologueStep.Fight, again.Prologue.Step);
            Assert.False(soul.PrologueDone);
            Assert.Equal(0, soul.Lives);
            Assert.Equal(0, soul.RebirthQi);
        }

        [Fact] public void 장삼전에서_바닥나면_고정_긴_각성이_저절로_시작되고_끝나면_마저_친다()
        {
            var (_, life) = NewPrologue();
            Run(_d, life, 9.05f);
            Assert.Equal(PrologueDown.Awaken, life.PlayerDown());
            Assert.True(life.Awakening.IsActive);
            Assert.True(life.Awakening.IsLong);
            Assert.Equal(_d.Cheonha.PrologueBody.FixedAwakening, life.Awakening.Evolution!.Id);
            Assert.Contains("samjae_sword", life.Loadout.Evolved); // Evolved 는 무공 id(천지인 삼검 = 삼재검법)

            int guard = 0;
            while (life.Awakening.IsActive && guard++ < 1000) life.Tick(0.05f, life.Position, 40);
            Assert.Equal(PrologueStep.Finish, life.Prologue!.Step);
            Assert.Equal(PrologueDown.Hold, life.PlayerDown());
        }

        [Fact] public void 장삼_기혈이_바닥에_닿아도_각성이_시작된다()
        {
            var (_, life) = NewPrologue();
            Run(_d, life, 9.05f);
            var boss = _d.Bosses.First(b => b.Id == "jang_sam");
            float max = Prologue.BossMaxHealth(boss);
            float hp = life.DamagePrologueBoss(max, max, max * 2);
            Assert.Equal(max * _d.Cheonha.PrologueBody.BossHealthFloor, hp, 2);
            Assert.True(life.Prologue!.AwakenedByBossFloor);
            Assert.True(life.Awakening.IsActive);
            // 각성 중에 또 맞아도 두 번 시작하지 않는다
            Assert.Equal(hp, life.DamagePrologueBoss(hp, max, 100f), 2);
        }

        [Fact] public void 장삼을_쓰러뜨리면_정산하고_혼에_옮기면_첫_환혼_셋()
        {
            var (soul, life) = NewPrologue();
            Run(_d, life, 9.05f);
            life.PlayerDown();
            int guard = 0;
            while (life.Awakening.IsActive && guard++ < 1000) life.Tick(0.05f, life.Position, 40);
            var boss = _d.Bosses.First(b => b.Id == "jang_sam");
            float max = Prologue.BossMaxHealth(boss);
            Assert.Equal(0f, life.DamagePrologueBoss(max * 0.1f, max, max));

            var record = life.KillPrologueBoss();
            Assert.Null(record.Sect);
            Assert.Equal(LifeEndWay.Fallen, record.EndWay);
            Assert.Equal(LifePhase.Ended, life.Phase);
            Assert.Null(life.Tick(0.1f, life.Position, 0));
            // 서장 각성은 혼 기록에 안 남는다(첫 생에 맛본 절기를 다시 깨우는 것이 목표)
            Assert.Empty(record.AwakenedEvolutions);

            Assert.Equal(PrologueStep.Cutscene, life.Prologue!.Step);
            Assert.Equal(_d.Cheonha.PrologueBody.EndCuts[0], life.Prologue.CurrentCut);
            foreach (var _ in _d.Cheonha.PrologueBody.EndCuts) life.Prologue.NextCut();
            Assert.Equal(PrologueStep.FirstRebirth, life.Prologue.Step);

            soul.EndLife(record);
            Assert.True(soul.PrologueDone);
            var draw = soul.OpenRebirth(new Random(3));
            Assert.True(draw.IsFirst);
            Assert.Equal(_d.Cheonha.Rebirth.FirstRebirth.OrderBy(x => x), draw.Candidates.Select(c => c.Sect.Id).OrderBy(x => x));
            var body = life.Prologue.ChooseFirst(draw.Candidates[0]);
            Assert.Equal(PrologueStep.Done, life.Prologue.Step);
            Assert.False(body.IsPrologue);
        }
    }
}
