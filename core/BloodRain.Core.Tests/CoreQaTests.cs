using System;
using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Run;
using BloodRain.Core.Progression;
using BloodRain.Core.Spawning;
using BloodRain.Core.World;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>QA-01 Core 점검에서 찾아 고친 것들을 묶어 둔 테스트.</summary>
    public class CoreQaTests
    {
        readonly GameData _d = TestData.Load();

        Soul NewSoul()
        {
            var soul = new Soul(_d);
            soul.EndLife(soul.BeginLife(Rebirth.Prologue(_d)).Finish(LifeEndWay.Fallen, 0));
            return soul;
        }

        Life NewLife(string sect, params string[] traits) => NewLife(NewSoul(), sect, traits);

        Life NewLife(Soul soul, string sect, params string[] traits)
        {
            var c = _d.Cheonha;
            var cand = new RebirthCandidate(c.Sect(sect), c.Region(c.Sect(sect).Region), traits.Select(c.Trait).ToArray());
            return new Life(soul, Rebirth.Body(c, cand), new Random(1));
        }

        [Fact] public void 한_생의_진기에_몸의_심득이_곱해진다()
        {
            var plain = NewLife("shaolin");
            var dull = NewLife("shaolin", "dunjae");
            plain.Absorb(1);
            dull.Absorb(1);
            float growth = 1f + (float)_d.Cheonha.Trait("dunjae").Stats["Growth"];
            Assert.Equal(1.0, plain.Xp.Total, 6);
            Assert.Equal(growth, dull.Xp.Total, 4);
            Assert.True(dull.Xp.Total < plain.Xp.Total);
        }

        [Fact] public void 봉마곡에_서_있으면_n번째_정예_사건은_예외_없이_지나간다()
        {
            var ld = new LifeDirector(_d, new Random(1));
            var events = new System.Collections.Generic.List<LifeEvent>();
            for (int sec = 0; sec <= 30 * 60; sec++) events.AddRange(ld.Second(sec / 60f, 30, "bongmagok", 1000).Events);
            var all = ld.Schedule.Events.Where(e => e.At <= 30).ToList();
            int nthElites = all.Count(e => e.Def.Type == WaveEventKind.Elite && e.Def.Nth > 0);
            Assert.True(nthElites > 0);
            Assert.Equal(all.Count - nthElites, events.Count);
            Assert.DoesNotContain(events, e => e.Wave.Def.Type == WaveEventKind.Elite && e.Wave.Def.Nth > 0);
        }
        /// <summary>숭산에서 절정까지 올려 땅의 주인과 싸우고 쓰러뜨린다.</summary>
        BossKill KillSongshan(Life life, out bool wasShadow)
        {
            life.Absorb(new XpCurve(_d.Balance.XpCurve).CumulativeTo(35) * 2);
            for (int i = 0; i < 400 && life.Phase != LifePhase.BossFight; i++) life.Tick(0.1f, life.Regions.Center("songshan"), 0);
            Assert.Equal(LifePhase.BossFight, life.Phase);
            wasShadow = life.Bosses.Fight!.IsShadow;
            return life.DefeatBoss();
        }

        [Fact] public void 땅의_주인_처치_보상은_혼_장부와_같고_회귀하면_다시_첫_처치다()
        {
            var soul = NewSoul();
            var bosses = _d.Cheonha.Bosses;

            var life1 = NewLife(soul, "shaolin");
            Assert.Equal(0, life1.SealStones);
            var first = KillSongshan(life1, out bool shadow1);
            Assert.False(shadow1);
            Assert.True(first.First);
            Assert.NotNull(first.SealStone);
            Assert.Equal(1, life1.SealStones); // 생 도중에도 봉인석이 는다(일곱째면 그 생에서 봉마곡이 열린다)
            soul.EndLife(life1.End(LifeEndWay.Fallen, 0));

            // 같은 회차: 혈영
            var life2 = NewLife(soul, "shaolin");
            var again = KillSongshan(life2, out bool shadow2);
            Assert.True(shadow2);
            Assert.False(again.First);
            Assert.Null(again.SealStone);
            Assert.Equal(bosses.RepeatKill.BloodCrystal, again.BloodCrystal);
            soul.EndLife(life2.End(LifeEndWay.Fallen, 0));

            // 회귀(혈월 천하): 봉인석이 비었으니 진짜 땅의 주인이 다시 일어나고, 혈정에 혈월 배율이 붙는다
            soul.Regress();
            var life3 = NewLife(soul, "shaolin");
            var bloodmoon = KillSongshan(life3, out bool shadow3);
            Assert.False(shadow3);
            Assert.True(bloodmoon.First);
            Assert.NotNull(bloodmoon.SealStone);
            Assert.Null(bloodmoon.UnlockSect); // 문파 해금은 계정 첫 처치에만
            Assert.Equal(first.RebirthQi, bloodmoon.RebirthQi); // 보너스 값(배율은 정산 때)
            int expect = (int)Math.Round(bosses.FirstKill.BloodCrystal * _d.Cheonha.Cycle.Bloodmoon.Rewards, MidpointRounding.AwayFromZero);
            Assert.Equal(expect, bloodmoon.BloodCrystal);
            Assert.True(bloodmoon.BloodCrystal > first.BloodCrystal);
        }
        [Fact] public void 적_기혈과_피해는_음수_분을_0분으로_본다()
        {
            var s = new EnemyScaling(_d.Balance.EnemyScaling);
            Assert.Equal(s.HealthAt(10, 0, 1), s.HealthAt(10, -15, 1));
            Assert.Equal(s.DamageAt(10, 0, 1), s.DamageAt(10, -15, 1));
            Assert.True(s.HealthAt(10, -1000, 1) > 0);
        }

        [Fact] public void 이미_가진_무공과_심법을_새로_받으면_막는다()
        {
            var jin = _d.Character("jin_sowoon");
            var l = new Loadout(_d, jin);
            l.Apply(new Offer(OfferKind.UpgradeWeapon, jin.StartWeapon));
            Assert.Throws<InvalidOperationException>(() => l.Apply(new Offer(OfferKind.NewWeapon, jin.StartWeapon)));
            Assert.Equal(2, l.Weapons[jin.StartWeapon]); // 1단으로 돌아가지 않았다
            var p = _d.Passives[0].Id;
            l.Apply(new Offer(OfferKind.NewPassive, p));
            Assert.Throws<InvalidOperationException>(() => l.Apply(new Offer(OfferKind.NewPassive, p)));
        }

        [Fact] public void 한_프레임에_몇_초가_지나도_생성_초를_버리지_않는다()
        {
            var life = NewLife("shaolin");
            int seconds = 0;
            if (life.Tick(3.5f, life.Position, 0) != null) seconds++;
            for (int i = 0; i < 5; i++) if (life.Tick(0.01f, life.Position, 0) != null) seconds++;
            Assert.Equal(3, seconds);
        }
    }
}
