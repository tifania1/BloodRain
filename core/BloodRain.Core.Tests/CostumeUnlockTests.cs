using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Progression;
using BloodRain.Core.Run;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>WP-36: 기획서 10장 해금 표 '태극 구간만 지키며 원각 처치 → 운화 의상'. 조건만(그림·이름은 아트 뒤).
    /// Life 가 땅의 주인 싸움 동안 음양 띠를 보고, 처치 때 혼에 costume:unhwa_harmony 를 남긴다.</summary>
    public class CostumeUnlockTests
    {
        readonly GameData _d = TestData.Load();

        (Soul soul, Life life) InWongakFight()
        {
            var soul = new Soul(_d);
            soul.EndLife(soul.BeginLife(Rebirth.Prologue(_d)).Finish("fallen", 0));
            var C = _d.Cheonha;
            var life = new Life(soul, Rebirth.Body(C, new RebirthCandidate(C.Sect("shaolin"), C.Region("songshan"), System.Array.Empty<BirthTraitDef>())), new System.Random(1));
            life.Absorb(new XpCurve(_d.Balance.XpCurve).CumulativeTo(35));
            for (int i = 0; i < 400 && life.Phase != LifePhase.BossFight; i++) life.Tick(0.1f, life.Regions.Center("songshan"), 0);
            Assert.Equal(LifePhase.BossFight, life.Phase);
            return (soul, life);
        }

        [Fact] public void 데이터가_기획서_10장_해금_표와_같다()
        {
            Assert.Contains("| 태극 구간만 지키며 원각 처치 | 운화 의상 |", TestData.Gdd);
            var c = _d.Cheonha.Costumes.Single();
            Assert.Equal(("unhwa_harmony", "unhwa", CostumeDef.BossInHarmony, "wongak"), (c.Id, c.Character, c.When, c.Boss));
            Assert.Equal("costume:unhwa_harmony", c.Token);
            Assert.Equal("wongak", _d.Cheonha.Region("songshan").Boss);
            Assert.Contains(_d.Characters, ch => ch.Id == "unhwa");
        }

        [Fact] public void 싸우는_내내_태극이면_의상이_열린다()
        {
            var (soul, life) = InWongakFight();
            life.Loadout.Gauge.ResetTo(0);
            for (int i = 0; i < 10; i++) life.Tick(0.1f, life.Position, 0);
            life.DefeatBoss();
            Assert.True(soul.IsUnlocked("costume:unhwa_harmony"));
        }

        [Fact] public void 한_번이라도_태극을_벗어나면_안_열린다()
        {
            var (soul, life) = InWongakFight();
            life.Loadout.Gauge.ResetTo(50);
            life.Tick(0.1f, life.Position, 0);
            life.Loadout.Gauge.ResetTo(0);
            for (int i = 0; i < 10; i++) life.Tick(0.1f, life.Position, 0);
            life.DefeatBoss();
            Assert.False(soul.IsUnlocked("costume:unhwa_harmony"));
        }

        [Fact] public void 쓰러뜨리는_순간_태극_밖이어도_안_열린다()
        {
            var (soul, life) = InWongakFight();
            life.Loadout.Gauge.ResetTo(0);
            life.Tick(0.1f, life.Position, 0);
            life.Loadout.Gauge.ResetTo(-60);
            life.DefeatBoss();
            Assert.False(soul.IsUnlocked("costume:unhwa_harmony"));
        }
    }
}
