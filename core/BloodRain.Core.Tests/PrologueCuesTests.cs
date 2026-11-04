using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Progression;
using BloodRain.Core.Run;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>서장 안내·대사 차례(backlog M1-03, docs/story/prologue.md '쓰는 법').</summary>
    public class PrologueCuesTests
    {
        readonly GameData _d = Load();
        PrologueDialogueDef P => _d.Dialogue.Prologue;

        static readonly string[] KnownKinds =
        {
            PrologueCue.Start, PrologueCue.StartReply, PrologueCue.FirstLevelUp, PrologueCue.UltimateReady, PrologueCue.YinyangTilt,
            PrologueCue.FirstCharge, PrologueCue.BossWarning, PrologueCue.BossSpawn, PrologueCue.Awaken, PrologueCue.AwakenAfter,
            PrologueCue.BossKill, "realm", "elite_spawn", "elite_kill",
        };

        [Fact] public void 대사와_안내의_when_은_모두_아는_사건이다()
        {
            foreach (var w in P.Lines.Select(l => l.When).Concat(P.Hints.Select(h => h.When)))
                Assert.Contains(w.Split(':')[0], KnownKinds);
            Assert.Contains(P.Hints, h => h.When == PrologueCue.UltimateReady && h.TextKo == "오의를 써라"); // 4장 표 2:00
            Assert.Contains(P.Hints, h => h.When == PrologueCue.YinyangTilt);                                // 4장 표 3:20
            Assert.Equal(P.Hints.Count, P.Hints.Select(h => h.Id).Distinct().Count());
        }

        [Fact] public void 안내_문구와_간격이_prologue_md_에_있다()
        {
            var doc = ReadRepoFile("docs", "story", "prologue.md");
            foreach (var h in P.Hints) Assert.Contains(h.TextKo, doc);
            Assert.Contains($"{P.LineSeconds:0.#}초", doc);
            Assert.Contains($"{P.HintFirstDelaySeconds:0.#}초", doc);
        }

        [Fact] public void 시작은_두_줄_이어서_한_번만()
        {
            var c = new PrologueCues(_d);
            var r = c.Start();
            Assert.Equal(new[] { PrologueCue.Start, PrologueCue.StartReply }, r.Select(x => x.When));
            Assert.Equal(0f, r[0].Delay);
            Assert.Equal(P.LineSeconds, r[1].Delay);
            Assert.True(r[0].Line!.Offscreen);
            Assert.Empty(c.Start());
        }

        [Fact] public void 안내가_먼저_대사는_늦게()
        {
            var c = new PrologueCues(_d);
            var r = c.Fire(PrologueCue.UltimateReady);
            Assert.True(r[0].IsHint);
            Assert.Equal("오의를 써라", r[0].TextKo);
            Assert.Equal(0f, r[0].Delay);
            var line = Assert.Single(r, x => !x.IsHint);
            Assert.Equal(P.HintFirstDelaySeconds, line.Delay);
        }

        [Fact] public void 서장_한_판에서_모든_줄과_안내가_한_번씩_차례대로()
        {
            var soul = new Soul(_d);
            var life = new Life(soul, Rebirth.Prologue(_d), new Random(4));
            var cues = new PrologueCues(_d);
            var fired = new List<PrologueCueItem>(cues.Start());
            var curve = new XpCurve(_d.Balance.XpCurve);
            double fed = 0;
            int guard = 0;
            bool tilted = false, eliteDown = false;
            var boss = _d.Bosses.First(b => b.Id == "jang_sam");
            float max = Prologue.BossMaxHealth(boss), hp = max;
            while (life.Record == null && guard++ < 20000)
            {
                var s = life.Tick(0.1f, life.Position, 40);
                fired.AddRange(cues.OnSecond(s));
                float m = life.Minutes;
                int target = Math.Min(22, 1 + (int)(m * 3));
                double need = curve.CumulativeTo(target) - fed;
                if (need > 0) { life.Absorb(need); fed += need; }
                if (m > 2f) life.Charge.OnKill(life.Loadout.Gauge.Band, 50);
                if (!tilted && m > 3.3f) { life.Loadout.Gauge.Shift(40); tilted = true; }
                if (!eliteDown && m > 5.5f) { fired.AddRange(cues.OnEliteKilled("gangsi_axeman")); eliteDown = true; }
                if (life.Prologue!.Step == PrologueStep.Boss && m > 9.2f) life.PlayerDown();
                if (life.Prologue.Step == PrologueStep.Finish) { hp = life.DamagePrologueBoss(hp, max, max); if (hp <= 0) life.KillPrologueBoss(); }
                fired.AddRange(cues.Watch(life));
            }
            Assert.NotNull(life.Record);

            var whens = fired.Where(x => !x.IsHint).Select(x => x.Line!.Id).ToList();
            Assert.Equal(P.Lines.Select(l => l.Id), whens); // 데이터 순서가 곧 서장 차례
            Assert.Equal(P.Hints.Select(h => h.Id).OrderBy(x => x), fired.Where(x => x.IsHint).Select(x => x.Hint!.Id).OrderBy(x => x));
        }
    }
}
