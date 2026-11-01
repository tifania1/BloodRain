using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Build;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// T-43: 서장 첫 레벨업은 새 무공 카드 한 장뿐(4장 '고르기만 하면 됨').
    public class LoadoutFirstLevelUpTests
    {
        static readonly Data.GameData D = TestData.Load();

        static ISet<string> AllUnlocked() => new HashSet<string>(D.Weapons.Select(w => w.Id).Concat(D.Passives.Select(p => p.Id)));

        [Fact] public void 서장_첫_레벨업은_새_무공_한_장이고_은자_카드가_없다()
        {
            var prologue = D.Chapter("prologue");
            Assert.Equal(1, prologue.FirstLevelUpChoices);
            for (int seed = 1; seed <= 30; seed++)
            {
                var l = new Loadout(D, D.Character(D.Cheonha.PrologueBody.Character));
                var offers = l.RollFirstLevelUp(new Random(seed), 100f, AllUnlocked(), prologue);
                Assert.Single(offers);
                Assert.Equal(OfferKind.NewWeapon, offers[0].Kind);
            }
        }

        [Fact] public void 첫_레벨업_값이_없는_흐름은_보통_레벨업과_같다()
        {
            var other = D.Chapters.First(c => c.FirstLevelUpChoices == 0);
            var a = new Loadout(D, D.Character(D.Cheonha.PrologueBody.Character)).RollFirstLevelUp(new Random(7), 0f, AllUnlocked(), other);
            var b = new Loadout(D, D.Character(D.Cheonha.PrologueBody.Character)).RollLevelUp(new Random(7), 0f, AllUnlocked());
            Assert.Equal(b.Select(o => (o.Kind, o.Id)), a.Select(o => (o.Kind, o.Id)));
        }

        [Fact] public void 기획서_서장_표에_첫_레벨업_한_장이_적혀_있다()
        {
            Assert.Contains("선택지가 무공 하나뿐", TestData.GddSection("서장 상세 흐름"));
        }
    }
}
