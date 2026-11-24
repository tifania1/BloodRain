using System;
using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Progression;
using BloodRain.Core.Run;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>병기를 한 생에 이어 붙이기(backlog GU-10b3): Life 의 병기 칸, 떨어뜨리기, 땅의 주인 등급 오름·유품, 레벨업 무게.</summary>
    public class GearLifeTests
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

        Life FightSongshan(Life life)
        {
            life.Absorb(new XpCurve(_d.Balance.XpCurve).CumulativeTo(35));
            for (int i = 0; i < 400 && life.Phase != LifePhase.BossFight; i++) life.Tick(0.1f, life.Regions.Center("songshan"), 0);
            Assert.Equal(LifePhase.BossFight, life.Phase);
            return life;
        }

        [Fact]
        public void 숭산_정예에서_금강_염주가_떨어지고_주우면_배율이_붙는다()
        {
            var life = NewLife();
            Assert.True(life.GearSlot.Empty);
            var id = life.RollGearDrop("gyeyul_monk", 0, new Random(2));
            Assert.Equal("vajra_beads", id);
            Assert.Equal(0, life.PickGear(id!));
            Assert.Equal(1.05f, life.GearDamageMultiplier("nahan_fist"), 4);
            Assert.Equal(1f, life.GearAreaMultiplier("nahan_fist"), 4);
            Assert.Equal(1f, life.GearDamageMultiplier("flying_dagger"), 4);
        }

        [Fact]
        public void 서장에는_병기가_없다()
        {
            var soul = new Soul(_d);
            var life = new Life(soul, Rebirth.Prologue(_d), new Random(1));
            Assert.Null(life.RollGearDrop("gyeyul_monk", 0, new Random(2)));
        }

        [Fact]
        public void 원각을_쓰러뜨리면_염주가_오르고_계정_첫_처치에_무명_철검이_떨어진다()
        {
            var life = FightSongshan(NewLife());
            life.PickGear("vajra_beads");
            var kill = life.DefeatBoss();
            Assert.True(kill.GearGradedUp);
            Assert.Equal(1, life.GearSlot.Grade);
            Assert.True(kill.FirstOnAccount);
            Assert.Equal("mumyeong_iron_sword", kill.GearDrop);
        }

        [Fact]
        public void 빈손이면_땅의_주인이_올리지_않는다()
        {
            var kill = FightSongshan(NewLife()).DefeatBoss();
            Assert.False(kill.GearGradedUp);
        }

        [Fact]
        public void 신병에_절기면_6단이다()
        {
            var life = NewLife();
            for (int i = 0; i < 4; i++) life.PickGear("vajra_beads");
            Assert.False(life.IsSixth("nahan_fist"));
            life.Loadout.Evolve(_d.Evolutions.First(e => e.Weapon == "nahan_fist"));
            Assert.True(life.IsSixth("nahan_fist"));
        }

        [Fact]
        public void 명병부터_그_문파_카드가_더_잘_뜨고_범병이면_난수_순서가_같다()
        {
            var slot = new GearSlot();
            Assert.Equal(1.0, Gear.OfferWeight(_d, null, "nahan_fist"));
            slot.Pick(_d, "vajra_beads");
            Assert.Equal(1.0, Gear.OfferWeight(_d, slot, "nahan_fist"));
            slot.Pick(_d, "vajra_beads"); slot.Pick(_d, "vajra_beads");
            Assert.Equal(1.5, Gear.OfferWeight(_d, slot, "nahan_fist"));
            Assert.Equal(1.5, Gear.OfferWeight(_d, slot, "geumgang"));
            Assert.Equal(1.0, Gear.OfferWeight(_d, slot, "flying_dagger"));

            // 범병(무게 1)은 병기가 없을 때와 같은 카드를 같은 순서로 뽑는다
            var unlocked = new System.Collections.Generic.HashSet<string> { "start" };
            var low = new GearSlot(); low.Pick(_d, "vajra_beads");
            var a = new Loadout(_d, _d.Characters[0]).RollLevelUp(new Random(5), 0, unlocked);
            var b = new Loadout(_d, _d.Characters[0]).RollLevelUp(new Random(5), 0, unlocked, gear: low);
            Assert.Equal(a.Select(o => o.ToString()), b.Select(o => o.ToString()));
        }

        [Fact]
        public void 이어하기_저장에_병기_칸이_든다()
        {
            var life = NewLife();
            life.PickGear("vajra_beads");
            life.PickGear("vajra_beads");
            var snap = Newtonsoft.Json.JsonConvert.DeserializeObject<LifeSnapshot>(Newtonsoft.Json.JsonConvert.SerializeObject(LifeSnapshot.Capture(life)))!;
            Assert.Equal("vajra_beads", snap.Gear);
            Assert.Equal(1, snap.GearGrade);
            var back = NewLife();
            back.Restore(snap);
            Assert.Equal("vajra_beads", back.GearSlot.GearId);
            Assert.Equal(1, back.GearSlot.Grade);
        }

        [Fact]
        public void 옛_저장은_빈손으로_돌아온다()
        {
            var back = NewLife();
            back.Restore(LifeSnapshot.Capture(NewLife()));
            Assert.True(back.GearSlot.Empty);
            Assert.Equal(-1, back.GearSlot.Grade);
        }
    }
}
