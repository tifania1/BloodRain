using System;
using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Run;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>기연을 만난 뒤의 셈(WP-39, 기획서 20장 '기연 사건 8'). 웹 한 생 판 WP-27~32 와 같은 규칙.</summary>
    public class EncounterBoonsTests
    {
        readonly GameData _d = TestData.Load();

        Life NewLife(string sect = "shaolin")
        {
            var soul = new Soul(_d);
            soul.EndLife(soul.BeginLife(Rebirth.Prologue(_d)).Finish(LifeEndWay.Fallen, 0));
            var c = _d.Cheonha;
            var cand = new RebirthCandidate(c.Sect(sect), c.Region(c.Sect(sect).Region), Array.Empty<BirthTraitDef>());
            return new Life(soul, Rebirth.Body(c, cand), new Random(1));
        }

        [Fact] public void 기획서_20장_숫자가_params_에_있다()
        {
            var s = _d.Synergies;
            Assert.Equal(6f, s.Encounter("hermit").Param("staySeconds"));
            Assert.Contains("6초", s.Encounter("hermit").How);
            Assert.Equal(1f, s.Encounter("relic").Param("bowSeconds"));
            Assert.Contains("1초", s.Encounter("relic").How);
            Assert.Equal(0.5f, s.Encounter("whisper").Param("healingMultiplier"));
            Assert.Contains("회복 절반", s.Encounter("whisper").Reward);
            Assert.Equal(0.03f, s.Encounter("sword_tomb").Param("mightPerLevel"), 5);
            Assert.Contains("+3%", s.Encounter("sword_tomb").Reward);
            Assert.Contains(s.Schools, x => x.Id == EncounterBoons.WhisperSchool);
        }

        [Fact] public void 영약은_오의_가득_음양_0()
        {
            var life = NewLife();
            life.Loadout.Gauge.ResetTo(30);
            life.TakeElixir();
            Assert.True(life.Charge.IsFull);
            Assert.Equal(0, life.Loadout.Gauge.Value);
            Assert.True(life.Encounters.HasMet(EncounterBoons.Elixir));
        }

        [Fact] public void 영물은_영약_효과와_정예_하나_몫_혈정_정예_수에는_안_든다()
        {
            var life = NewLife();
            life.Loadout.Gauge.ResetTo(-30);
            int got = life.CatchSpiritBeast();
            Assert.Equal(_d.Balance.Drops.EliteBloodCrystal, got);
            Assert.True(life.Charge.IsFull);
            Assert.Equal(0, life.Loadout.Gauge.Value);
            Assert.Equal(got, life.Log.BloodCrystal);
            life.KillElite();
            Assert.Equal(2 * _d.Balance.Drops.EliteBloodCrystal, life.Log.BloodCrystal);
        }

        [Fact] public void 고수의_유해는_그_문파_진전을_하나_더()
        {
            var life = NewLife();
            Assert.False(life.Encounters.RelicReady(0.9f));
            Assert.True(life.Encounters.RelicReady(1f));
            int before = life.Body.ComputeSynergy(_d, life.Loadout, life.Encounters.SchoolExtra).School("tang").Count;
            life.Encounters.HonorRelic("tang");
            Assert.Equal(before + 1, life.Body.ComputeSynergy(_d, life.Loadout, life.Encounters.SchoolExtra).School("tang").Count);
            Assert.Throws<ArgumentException>(() => life.Encounters.HonorRelic(""));
        }

        [Fact] public void 은거기인은_6초_머물러야()
        {
            var b = NewLife().Encounters;
            Assert.False(b.HermitReady(5.9f));
            Assert.True(b.HermitReady(6f));
            b.MeetHermit();
            Assert.Equal(new[] { EncounterBoons.Hermit }, b.Met);
        }

        [Fact] public void 속삭임은_마도_진전_하나와_회복_절반_한_번만()
        {
            var life = NewLife();
            var b = life.Encounters;
            Assert.Equal(1f, b.HealingMultiplier);
            int before = life.Body.ComputeSynergy(_d, life.Loadout, b.SchoolExtra).School("demonic").Count;
            Assert.True(b.AcceptWhisper());
            Assert.False(b.AcceptWhisper());
            Assert.True(b.WhisperTaken);
            Assert.Equal(0.5f, b.HealingMultiplier);
            Assert.Equal(0.5f, life.HealingMultiplier);
            Assert.Equal(before + 1, life.Body.ComputeSynergy(_d, life.Loadout, b.SchoolExtra).School("demonic").Count);
        }

        [Fact] public void 마교_몸이_속삭임을_받으면_회복이_4분의_1()
        {
            var life = NewLife("demonic");
            Assert.Equal(0.5f, life.HealingMultiplier);
            life.Encounters.AcceptWhisper();
            Assert.Equal(0.25f, life.HealingMultiplier);
        }

        [Fact] public void 검총은_버린_무공_레벨_곱하기_3퍼센트를_공력에_더한다()
        {
            var life = NewLife();
            string start = life.Loadout.Weapons.Keys.Single();
            Assert.False(EncounterBoons.CanPlantSword(life.Loadout, start));   // 하나뿐이면 못 꽂음
            Assert.Throws<InvalidOperationException>(() => life.Encounters.PlantSword(life.Loadout, start));

            string other = _d.Weapons.First(w => w.Id != start).Id;
            life.Loadout.Apply(new Offer(OfferKind.NewWeapon, other));
            for (int i = 0; i < 4; i++) life.Loadout.Apply(new Offer(OfferKind.UpgradeWeapon, other));   // Lv5
            float mightBefore = life.CurrentStats().Might;

            Assert.True(EncounterBoons.CanPlantSword(life.Loadout, other));
            float bonus = life.Encounters.PlantSword(life.Loadout, other);
            Assert.Equal(5 * 0.03f, bonus, 5);
            Assert.False(life.Loadout.Weapons.ContainsKey(other));
            Assert.Equal(mightBefore + bonus, life.CurrentStats().Might, 4);
            // 버린 무공은 레벨업 후보로 다시 안 나온다
            Assert.DoesNotContain(life.Loadout.Candidates(new System.Collections.Generic.HashSet<string>(_d.Weapons.Select(w => w.Id))),
                o => o.Id == other);
        }

        [Fact] public void 검총은_여러_번_꽂으면_더한다()
        {
            var life = NewLife();
            string start = life.Loadout.Weapons.Keys.Single();
            var others = _d.Weapons.Where(w => w.Id != start).Take(2).Select(w => w.Id).ToArray();
            foreach (var id in others) life.Loadout.Apply(new Offer(OfferKind.NewWeapon, id));
            life.Encounters.PlantSword(life.Loadout, others[0]);
            life.Encounters.PlantSword(life.Loadout, others[1]);
            Assert.Equal(2 * 0.03f, life.Encounters.WeaponMightBonus, 5);
            Assert.False(EncounterBoons.CanPlantSword(life.Loadout, start));
        }
    }
}
