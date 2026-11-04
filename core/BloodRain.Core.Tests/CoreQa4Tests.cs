using System;
using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Run;
using BloodRain.Core.World;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>QA-10(2026-11-04): QA-09 에서 남긴 가드·회복 배율·오의 데이터.</summary>
    public class CoreQa4Tests
    {
        readonly GameData _d = TestData.Load();
        CheonhaDef C => _d.Cheonha;

        static RebirthQiResult NoQi(GameData d) => new RebirthQiLedger(d.Cheonha.RebirthQi).Settle(0);

        RebirthBody Body(string sect) =>
            Rebirth.Body(C, new RebirthCandidate(C.Sect(sect), C.Region(C.Sect(sect).Region), new System.Collections.Generic.List<BirthTraitDef>()));

        Soul AfterPrologue()
        {
            var s = new Soul(_d);
            var life = s.BeginLife(Rebirth.Prologue(_d));
            s.EndLife(life.Finish("fallen", 0));
            return s;
        }

        LifeRecord Sealed(string sect, int manuscripts = 0) =>
            new LifeRecord(sect, "sealed", NoQi(_d), Array.Empty<string>(), Array.Empty<string>(), "hyeongyeong", Array.Empty<string>(), manuscripts);

        // ---- 엔딩을 고를 차례·진엔딩 뒤에는 새 생이 없다 ----

        [Fact] public void 종장을_깨고_엔딩을_안_골랐으면_환혼도_새_생도_없다()
        {
            var s = AfterPrologue();
            s.EndLife(Sealed("shaolin"));
            Assert.True(s.EndingPending);
            Assert.Throws<InvalidOperationException>(() => s.OpenRebirth(new Random(1)));
            Assert.Throws<InvalidOperationException>(() => s.BeginLife(Body("shaolin")));
            s.ChooseEnding(Soul.EndingSeal, true);
            Assert.NotNull(s.OpenRebirth(new Random(1)));
            Assert.NotNull(s.BeginLife(Body("shaolin")));
        }

        [Fact] public void 진엔딩_뒤에는_환혼도_새_생도_없다()
        {
            var s = AfterPrologue();
            s.EndLife(Sealed("shaolin"));
            s.ChooseEnding(Soul.EndingSeal, true);
            s.EndLife(Sealed("gaebang", manuscripts: 4));
            s.ChooseEnding(Soul.EndingSeal, true);
            s.EndLife(Sealed("namgung", manuscripts: 3));
            s.ChooseEnding(Soul.EndingSeal, true);
            s.EndLife(Sealed("tang"));
            Assert.Contains(Soul.EndingTrue, s.EndingsOpen(true));
            s.ChooseEnding(Soul.EndingTrue, true);
            Assert.True(s.JourneyOver);
            Assert.Throws<InvalidOperationException>(() => s.OpenRebirth(new Random(1)));
            Assert.Throws<InvalidOperationException>(() => s.BeginLife(Body("shaolin")));
        }

        [Fact] public void 회귀하면_고를_엔딩이_남지_않는다()
        {
            var s = AfterPrologue();
            s.EndLife(Sealed("shaolin"));
            s.Regress();
            Assert.False(s.EndingPending);
            Assert.NotNull(s.BeginLife(Body("shaolin")));
        }

        // ---- 서장 ----

        [Fact] public void 서장은_장삼_전에_End_로_끝낼_수_없고_처음부터_한다()
        {
            var soul = new Soul(_d);
            var life = new Life(soul, Rebirth.Prologue(_d), new Random(1));
            Assert.Throws<InvalidOperationException>(() => life.End(LifeEndWay.Fallen, 0));
            foreach (var way in C.LifeEnd.Ways)
                Assert.Throws<InvalidOperationException>(() => life.End(way, 0));
            var again = life.RestartPrologue(new Random(2));
            Assert.Equal(0, again.Prologue!.Restarts);   // 그만두기는 쓰러진 횟수에 안 든다
            Assert.False(soul.PrologueDone);
        }

        [Fact] public void 한_프레임에_두_번_쓰러져도_처음부터는_한_번으로_센다()
        {
            var soul = new Soul(_d);
            var life = new Life(soul, Rebirth.Prologue(_d), new Random(1));
            Assert.Equal(PrologueDown.Restart, life.PlayerDown());
            Assert.Equal(PrologueDown.Restart, life.PlayerDown());
            Assert.Equal(1, life.Prologue!.Restarts);
            var again = life.RestartPrologue(new Random(2));
            Assert.False(again.Prologue!.RestartPending);
            Assert.Equal(PrologueDown.Restart, again.PlayerDown());
            Assert.Equal(2, again.Prologue.Restarts);
        }

        // ---- 마교 회복 절반 ----

        [Fact] public void 마교는_회복_능력치_음식_모닥불도_절반()
        {
            var demonic = SectTrait.Of(C.Sect("demonic"));
            var shaolin = SectTrait.Of(C.Sect("shaolin"));
            Assert.Equal(0.5f, demonic.HealingMultiplier);

            var b = new StatBonus();
            b.Add(StatKind.Recovery, 2f);
            var sheet = new StatSheet(_d.Characters[0], _d.Balance.Stats, b);
            Assert.Equal(1f, demonic.RecoveryHeal(sheet, 1f), 4);
            Assert.Equal(2f, shaolin.RecoveryHeal(sheet, 1f), 4);
            Assert.Equal(0f, demonic.RecoveryHeal(sheet, -1f));

            var drops = _d.Balance.Drops;
            Assert.Equal(drops.BunHeal * 0.5f, Drops.FoodHeal(drops, Drops.Bun, 1f, demonic.HealingMultiplier), 4);
            Assert.Equal(drops.DuckHeal * 1.5f * 0.5f, Drops.FoodHeal(drops, Drops.Duck, 1.5f, demonic.HealingMultiplier), 4);
            Assert.Throws<ArgumentOutOfRangeException>(() => Drops.FoodHeal(drops, Drops.Bun, 1f, -1f));

            var fire = _d.Gimmicks.Campfire;
            Assert.Equal(CampfireGimmick.Heal(fire, 0f, 100, 1f) * 0.5f, CampfireGimmick.Heal(fire, 0f, 100, 1f, 1f, demonic.HealingMultiplier), 4);
            Assert.Equal(0f, CampfireGimmick.Heal(fire, 0f, 100, -1f));
            Assert.Equal(3f, demonic.Heal(6f));
            Assert.Equal(0f, demonic.Heal(-6f));
        }

        // ---- 오의 박자·태극무량은 데이터에서 ----

        [Fact] public void 오의_박자와_태극무량_효과는_params_에서_읽는다()
        {
            var hwau = _d.Ultimates.Single(u => u.Id == "mancheon_hwau");
            Assert.Equal(1f, hwau.Param("pulseSeconds"));
            var taiji = _d.Ultimates.Single(u => u.Id == UltimateCast.TaijiMuryang);
            Assert.Equal(1f, taiji.Param("blocksDamage"));
            Assert.Equal(1f, taiji.Param("reflectsProjectiles"));

            // 박자를 바꾸면 때리는 수가 따라간다(데이터를 고쳐 본다)
            var d = GameData.FromJson(TestData.ReadFiles());
            var hw = d.Ultimates.Single(u => u.Id == "mancheon_hwau");
            hw.Params!["pulseSeconds"] = 0.5f;
            Assert.Equal(6, new UltimateCast(hw).TotalPulses);

            var tj = d.Ultimates.Single(u => u.Id == UltimateCast.TaijiMuryang);
            tj.Params!.Remove("reflectsProjectiles");
            var cast = new UltimateCast(tj);
            cast.Advance(0f);
            Assert.False(cast.ReflectsProjectiles);
            Assert.Equal(0f, cast.DamageTakenMultiplier);
            cast.Advance(tj.Param("durationSeconds") + 0.1f);
            Assert.Equal(1f, cast.DamageTakenMultiplier);
        }
    }
}
