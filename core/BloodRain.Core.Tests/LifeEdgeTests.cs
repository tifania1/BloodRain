using System;
using System.Linq;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Run;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>한 생(Life) 안의 절기 각성과 그림자 읽기. LifeTests 가 안 보는 쪽만 모았다.</summary>
    public class LifeEdgeTests
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

        EvolutionDef Baekbo => _d.Evolutions.First(e => e.Id == "baekbo_fist");

        [Fact] public void 각성하면_무공이_절기가_되고_세상이_느려진다()
        {
            var life = NewLife();
            Assert.Equal(1f, life.WorldTimeScale);
            var info = life.Awaken(Baekbo);
            Assert.Equal("baekbo_fist", info.Evolution);
            Assert.True(info.FirstTime);
            Assert.Contains(Baekbo.Weapon, life.Loadout.Evolved);
            Assert.True(life.Awakening.IsActive);
            // 처음 0.18초에 걸쳐 느려지므로 시작 순간엔 아직 1이다.
            Assert.Equal(1f, life.WorldTimeScale);
            life.Tick(0.2f, life.Position, 0);
            Assert.True(life.WorldTimeScale < 0.5f);
        }

        /// <summary>각성 충격파가 터지면 생성 디렉터가 spawnPauseSeconds 동안 쉰다.</summary>
        [Fact] public void 충격파가_터지면_생성을_잠깐_멈춘다()
        {
            var life = NewLife();
            for (int i = 0; i < 600; i++) life.Tick(0.1f, life.Position, 0); // 1분 돌려 생성이 도는 상태로
            life.Awaken(Baekbo, AwakeningLength.Short);
            bool held = false;
            for (int i = 0; i < 600 && !held; i++)
            {
                var s = life.Tick(1f / 60f, life.Position, 0);
                if (s != null && s.Held) held = true;
            }
            Assert.True(held, "충격파 뒤 생성이 멈춘 초가 없다");
        }

        [Fact] public void 각성이_끝나면_세상_시간이_돌아온다()
        {
            var life = NewLife();
            life.Awaken(Baekbo, AwakeningLength.Short);
            for (int i = 0; i < 60 * 30 && life.Awakening.IsActive; i++) life.Tick(1f / 60f, life.Position, 0);
            Assert.False(life.Awakening.IsActive);
            Assert.Equal(1f, life.WorldTimeScale);
        }

        [Fact] public void 처음엔_그림자가_없다()
        {
            var life = NewLife();
            Assert.Equal(0, life.ShadowStep);
            Assert.False(life.Shadow.Active);
        }
    }
}
