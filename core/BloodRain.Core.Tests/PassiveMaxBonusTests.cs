using System;
using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Run;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>심법 최대 레벨 덧붙임(기획서 9장 심법 표 '최대' 칸, PM-01).</summary>
    public class PassiveMaxBonusTests
    {
        readonly GameData _d = TestData.Load();

        Life NewLife()
        {
            var soul = new Soul(_d);
            soul.EndLife(soul.BeginLife(Rebirth.Prologue(_d)).Finish(LifeEndWay.Fallen, 0));
            var c = _d.Cheonha;
            var cand = new RebirthCandidate(c.Sect("shaolin"), c.Region(c.Sect("shaolin").Region), Array.Empty<BirthTraitDef>());
            return new Life(soul, Rebirth.Body(c, cand), new Random(1));
        }

        static void Max(Life life, string passive, int level)
        {
            life.Loadout.Apply(new Offer(OfferKind.NewPassive, passive));
            for (int i = 1; i < level; i++) life.Loadout.Apply(new Offer(OfferKind.UpgradePassive, passive));
        }

        [Fact] public void 데이터의_글을_다_읽는다()
        {
            foreach (var p in _d.Passives.Where(p => !string.IsNullOrEmpty(p.MaxBonus)))
                PassiveMaxBonus.Parse(p.MaxBonus!);
            Assert.Throws<FormatException>(() => PassiveMaxBonus.Parse("하늘을 난다"));
        }

        [Fact] public void 최대_전엔_없다()
        {
            var life = NewLife();
            Max(life, "geumgang", _d.Balance.Levels.PassiveMax - 1);
            Assert.False(life.PassiveMax.KnockbackImmune);
        }

        [Fact] public void 금강체_만독_운룡()
        {
            var life = NewLife();
            int max = _d.Balance.Levels.PassiveMax;
            Max(life, "geumgang", max);
            Max(life, "mandok", max);
            Assert.True(life.PassiveMax.KnockbackImmune);
            Assert.True(life.PassiveMax.PoisonImmune);
            Assert.Equal(0, life.PassiveMax.DashCharges);
            var st = life.CurrentStats();
            int before = life.DashMaxCharges(st);
            Max(life, "unryong", max);
            Assert.Equal(1, life.PassiveMax.DashCharges);
            Assert.Equal(before + 1, life.DashMaxCharges(life.CurrentStats()));
        }

        [Fact] public void 기획서_심법_표와_같다()
        {
            foreach (var id in new[] { "geumgang", "mandok", "unryong" })
            {
                var p = _d.Passive(id);
                Assert.Contains($"| {p.NameKo} |", TestData.Gdd);
                Assert.Contains($", {p.MaxBonus} |", TestData.Gdd);
            }
        }
    }
}
