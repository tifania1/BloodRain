using System;
using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Progression;
using BloodRain.Core.Run;
using Newtonsoft.Json.Linq;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>레벨업 다시 고르기·파기·정진 남은 횟수(기획서 8장 레벨업 규칙, 10장 연무장, 6장 진소운 특성).</summary>
    public class LevelUpChargesTests
    {
        readonly GameData _d = TestData.Load();

        CharacterDef Jin => _d.Characters.First(c => c.Id == "jin_sowoon");

        Soul NewSoul(int qi = 0)
        {
            var soul = new Soul(_d);
            soul.EndLife(soul.BeginLife(Rebirth.Prologue(_d)).Finish(LifeEndWay.Fallen, 0));
            if (qi == 0) return soul;
            var j = JObject.Parse(soul.ToJson());
            var key = j.Property("RebirthQi") != null ? "RebirthQi" : "rebirthQi";
            j[key] = qi;
            return Soul.FromJson(_d, j.ToString());
        }

        Life NewLife(Soul soul, string sect = "shaolin")
        {
            var c = _d.Cheonha;
            var cand = new RebirthCandidate(c.Sect(sect), c.Region(c.Sect(sect).Region), Array.Empty<BirthTraitDef>());
            return new Life(soul, Rebirth.Body(c, cand), new Random(1));
        }

        [Fact] public void 진소운_특성은_다시_고르기_2회_기획서_6장과_같다()
        {
            Assert.Equal(2, LevelUpCharges.TraitRerolls(_d.Balance.LevelUp, Jin));
            Assert.Contains("레벨업 다시 고르기 +2회", TestData.Gdd);
            // 웹 프로토타입이 2로 박지 않고 데이터를 읽는지
            var html = TestData.ReadRepoFile("web", "prototype", "template.html");
            Assert.Contains("traitRerolls", html);
        }

        [Fact] public void 특성_몫이_없는_몸은_0에서_시작한다()
        {
            var life = NewLife(NewSoul());
            Assert.Equal(0, life.Charges.Rerolls);
            Assert.Equal(0, life.Charges.Banishes);
            Assert.Equal(0, life.Charges.Skips);
            Assert.False(life.Charges.Can(LevelUpAction.Reroll));
            Assert.Throws<InvalidOperationException>(() => life.Charges.Reroll());
        }

        [Fact] public void 진소운_서장은_다시_고르기_2회를_들고_시작한다()
        {
            var c = LevelUpCharges.Start(_d.Balance.LevelUp, Jin, new StatBonus());
            Assert.Equal(2, c.Rerolls);
            c.Reroll(); c.Reroll();
            Assert.Equal(0, c.Rerolls);
            Assert.Throws<InvalidOperationException>(() => c.Reroll());
        }

        [Fact] public void 연무장_세_항목은_레벨만큼_횟수가_된다()
        {
            var soul = NewSoul(qi: 100_000);
            foreach (var stat in new[] { "Rerolls", "Rerolls", "Banishes", "Skips", "Skips", "Skips" })
                Assert.True(soul.TryBuyTraining(stat), stat);
            var life = NewLife(soul);
            Assert.Equal(2, life.Charges.Rerolls);
            Assert.Equal(1, life.Charges.Banishes);
            Assert.Equal(3, life.Charges.Skips);
        }

        [Fact] public void 연무장과_진소운_특성은_더한다()
        {
            var b = new StatBonus();
            b.Add(StatKind.Rerolls, 5); // 연무장 최대
            Assert.Equal(7, LevelUpCharges.Start(_d.Balance.LevelUp, Jin, b).Rerolls);
        }

        [Fact] public void 파기는_횟수를_쓰고_그_선택지를_판에서_뺀다()
        {
            var life = NewLife(NewSoul());
            life.Charges.Add(LevelUpAction.Banish);
            var none = _d.Weapons.Select(w => w.Id).Concat(_d.Passives.Select(p => p.Id)).ToHashSet();
            var first = life.Loadout.RollLevelUp(new Random(5), 0, none);
            var target = first[0].Id;
            life.Charges.Banish(life.Loadout, target);
            Assert.Equal(0, life.Charges.Banishes);
            for (int s = 0; s < 200; s++)
                Assert.DoesNotContain(life.Loadout.RollLevelUp(new Random(s), 0, none), o => o.Id == target);
            Assert.Throws<InvalidOperationException>(() => life.Charges.Banish(life.Loadout, first[1].Id));
        }

        [Fact] public void 정진은_횟수를_쓰고_진기_게이지_20을_배율_없이_얻는다()
        {
            var life = NewLife(NewSoul());
            life.Charges.Add(LevelUpAction.Skip, 2);
            life.Charge.ChargeBonus = 0.5f;
            life.Charges.Skip(life.Charge);
            Assert.Equal(_d.Balance.UltimateCharge.PerSkip, life.Charge.Value);
            Assert.Equal(20f, _d.Balance.UltimateCharge.PerSkip);
            Assert.Equal(1, life.Charges.Skips);
        }

        [Fact] public void 판_안에서_얻은_다시_고르기_권은_더해진다()
        {
            var c = new LevelUpCharges(0, 0, 0);
            c.Add(LevelUpAction.Reroll);
            Assert.True(c.Can(LevelUpAction.Reroll));
            Assert.Equal(1, c.Left(LevelUpAction.Reroll));
            Assert.Throws<ArgumentOutOfRangeException>(() => c.Add(LevelUpAction.Reroll, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LevelUpCharges(-1, 0, 0));
        }

        [Fact] public void 특성_몫_키는_모두_있는_캐릭터_특성이다()
        {
            var traits = _d.Characters.Select(c => c.Trait).ToHashSet();
            foreach (var k in _d.Balance.LevelUp.TraitRerolls.Keys) Assert.Contains(k, traits);
        }
    }
}
