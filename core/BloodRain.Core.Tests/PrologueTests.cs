using System;
using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Run;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>서장 끝 순서(기획서 4장 '서장 상세 흐름', 21장 '서장의 절기 맛보기와 첫 환혼').</summary>
    public class PrologueTests
    {
        readonly GameData _d = TestData.Load();

        Loadout NewLoadout() => Rebirth.Prologue(_d).NewLoadout(_d);
        Awakening NewAwakening() => new Awakening(_d.Balance.Awakening);

        [Fact] public void 데이터_서장_보스는_장삼_고정_각성은_삼재검법의_천지인_삼검()
        {
            var p = _d.Cheonha.PrologueBody;
            Assert.Contains(_d.Bosses, e => e.Id == p.Boss);
            var evo = new Prologue(_d).Evolution;
            Assert.Equal("cheonjiin", evo.Id);
            Assert.Equal(_d.Character(p.Character).StartWeapon, evo.Weapon);
            Assert.Equal(new[] { "sb_prologue_burnout", "sb_prologue_needle" }, p.EndCuts);
        }

        [Fact] public void 컷_id는_art_queue에_있다()
        {
            var q = TestData.ReadRepoFile("art_queue", "queue.jsonl");
            var p = _d.Cheonha.PrologueBody;
            foreach (var id in p.EndCuts.Append(p.AwakenCut))
                Assert.Contains($"\"id\": \"{id}\"", q);
        }

        [Fact] public void 장삼_전에_쓰러지면_처음부터()
        {
            var pr = new Prologue(_d);
            Assert.Equal(PrologueDown.Restart, pr.PlayerDown());
            pr.Restart();
            Assert.Equal(PrologueStep.Fight, pr.Step);
            Assert.Equal(1, pr.Restarts);
        }

        [Fact] public void 장삼전에서_기혈이_바닥나면_긴_연출로_깨어나고_그_뒤엔_안_쓰러진다()
        {
            var pr = new Prologue(_d);
            pr.BossAppeared("jang_sam");
            Assert.Equal(PrologueDown.Awaken, pr.PlayerDown());
            Assert.Equal(PrologueStep.Awakening, pr.Step);
            Assert.Equal("sb_prologue_awaken", pr.CurrentCut);

            var lo = NewLoadout();
            var aw = NewAwakening();
            pr.StartAwakening(aw, lo);
            Assert.True(aw.IsActive);
            Assert.True(aw.IsLong); // 설정이 짧은 연출이어도
            Assert.Contains("samjae_sword", lo.Evolved);
            Assert.Equal(PrologueDown.Hold, pr.PlayerDown());

            pr.AwakeningEnded();
            Assert.Equal(PrologueStep.Finish, pr.Step);
            Assert.Equal(PrologueDown.Hold, pr.PlayerDown());
            Assert.False(pr.AwakenedByBossFloor);
        }

        [Fact] public void 각성_전에는_장삼이_쓰러지지_않고_바닥에_닿으면_각성()
        {
            var pr = new Prologue(_d);
            pr.BossAppeared("jang_sam");
            float max = 6000, hp = max;
            hp = pr.ApplyBossDamage(hp, max, 3000);
            Assert.Equal(3000, hp);
            Assert.Equal(PrologueStep.Boss, pr.Step);
            hp = pr.ApplyBossDamage(hp, max, 99999);
            Assert.Equal(600, hp, 3);
            Assert.Equal(PrologueStep.Awakening, pr.Step);
            Assert.True(pr.AwakenedByBossFloor);
            // 연출 중에도 바닥 아래로 안 내려간다
            Assert.Equal(600, pr.ApplyBossDamage(hp, max, 500), 3);
            pr.StartAwakening(NewAwakening(), NewLoadout());
            pr.AwakeningEnded();
            Assert.Equal(0, pr.ApplyBossDamage(hp, max, 1000));
        }

        [Fact] public void 각성_전에_장삼_처치는_순서_오류()
        {
            var pr = new Prologue(_d);
            pr.BossAppeared("jang_sam");
            Assert.Throws<InvalidOperationException>(() => pr.BossKilled());
            Assert.Throws<ArgumentException>(() => pr.BossAppeared("wongak"));
        }

        [Fact] public void 장삼을_쓰러뜨리면_몸이_다함_회혼침_컷_뒤_첫_환혼은_소림_개방_남궁()
        {
            var pr = new Prologue(_d);
            pr.BossAppeared("jang_sam");
            pr.PlayerDown();
            pr.StartAwakening(NewAwakening(), NewLoadout());
            pr.AwakeningEnded();
            pr.BossKilled();
            Assert.Equal(PrologueStep.Cutscene, pr.Step);
            Assert.Equal("sb_prologue_burnout", pr.CurrentCut);
            pr.NextCut();
            Assert.Equal("sb_prologue_needle", pr.CurrentCut);
            pr.NextCut();
            Assert.Equal(PrologueStep.FirstRebirth, pr.Step);
            Assert.Null(pr.CurrentCut);

            var cands = pr.FirstCandidates(new Random(1));
            Assert.Equal(new[] { "shaolin", "gaebang", "namgung" }, cands.Select(c => c.Sect.Id).OrderBy(x => x == "shaolin" ? 0 : x == "gaebang" ? 1 : 2));
            Assert.All(cands, c => Assert.Empty(c.Traits));
            var body = pr.ChooseFirst(cands[0]);
            Assert.Equal(PrologueStep.Done, pr.Step);
            Assert.False(body.IsPrologue);
        }
    }
}
