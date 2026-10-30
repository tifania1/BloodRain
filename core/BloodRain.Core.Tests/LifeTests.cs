using System;
using System.Linq;
using System.Numerics;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Progression;
using BloodRain.Core.Run;
using BloodRain.Core.World;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>한 생을 묶는 상태(기획서 7장 '한 생의 흐름'). 가짜 시계로 30분을 돌린다.</summary>
    public class LifeTests
    {
        readonly GameData _d = TestData.Load();

        (Soul soul, Life life) NewLife(string sect = "shaolin", int seed = 1)
        {
            var soul = new Soul(_d);
            soul.EndLife(soul.BeginLife(Rebirth.Prologue(_d)).Finish(LifeEndWay.Fallen, 0)); // 서장을 마친 혼
            var c = _d.Cheonha;
            var cand = new RebirthCandidate(c.Sect(sect), c.Region(c.Sect(sect).Region), Array.Empty<BirthTraitDef>());
            return (soul, new Life(soul, Rebirth.Body(c, cand), new Random(seed)));
        }

        /// <summary>11장 목표(분당 레벨 3, 30분 Lv90)대로 진기를 먹이며 0.1초씩 30분을 돈다.</summary>
        static void RunThirtyMinutes(GameData d, Life life, Action<float>? each = null)
        {
            var curve = new XpCurve(d.Balance.XpCurve);
            double fed = 0;
            int guard = 0;
            while (life.Minutes < 30f && guard++ < 200000)
            {
                var s = life.Tick(0.1f, life.Position, 100);
                int targetLevel = Math.Min(90, 1 + (int)Math.Floor(life.Minutes * 3));
                double need = curve.CumulativeTo(targetLevel) - fed;
                if (need > 0) { life.Absorb(need); fed += need; }
                each?.Invoke(life.Minutes);
            }
        }

        [Fact] public void 태어난_자리에서_시작하고_태어난_땅을_밟은_것으로_센다()
        {
            var (_, life) = NewLife();
            Assert.Equal("songshan", life.RegionId);
            Assert.Equal(new[] { "songshan" }, life.Log.VisitedRegions);
            Assert.Equal(LifePhase.Roaming, life.Phase);
            Assert.Equal(0, life.Realm);
        }

        [Fact] public void 가짜_시계_30분_돌파_여섯_번_현경과_땅의_주인_깨어남()
        {
            var (soul, life) = NewLife();
            int breakthroughs = 0, held = 0;
            bool wokeAtJeoljeong = false;
            var curve = new XpCurve(_d.Balance.XpCurve);
            double fed = 0;
            int guard = 0;
            while (life.Minutes < 30f && guard++ < 200000)
            {
                var s = life.Tick(0.1f, life.Position, 100);
                if (s != null && s.Held) held++;
                int targetLevel = Math.Min(90, 1 + (int)Math.Floor(life.Minutes * 3));
                double need = curve.CumulativeTo(targetLevel) - fed;
                if (need <= 0) continue;
                var a = life.Absorb(need);
                fed += need;
                breakthroughs += a.Breakthroughs.Count;
                if (a.WokenBosses.Count > 0 && a.Xp.Breakthroughs.Any(r => r.Id == "jeoljeong")) wokeAtJeoljeong = true;
            }
            Assert.Equal(6, breakthroughs);
            Assert.Equal(90, life.Level);
            Assert.Equal("hyeongyeong", life.Log.HighestRealm);
            Assert.True(wokeAtJeoljeong);
            Assert.True(held > 0); // 돌파 연출 1.4초 동안 생성이 멈춘 초가 있다
            // 돌파 연출 동안 세상 시간이 느려져 실제 시간은 30분보다 조금 길다
            Assert.InRange(life.Minutes, 30f, 30.1f);
        }

        [Fact] public void 돌파_연출_중엔_세상_시간이_느려진다()
        {
            var (_, life) = NewLife();
            var curve = new XpCurve(_d.Balance.XpCurve);
            var a = life.Absorb(curve.CumulativeTo(10));
            Assert.Single(a.Breakthroughs);
            Assert.True(a.Breakthroughs[0].FirstTime);
            Assert.True(life.Breakthrough.IsActive);
            Assert.Equal(_d.Cheonha.Breakthrough.TimeScale, life.WorldTimeScale, 3);
            float before = life.Minutes;
            life.Tick(0.5f, life.Position, 0);
            Assert.Equal(before + 0.5f * _d.Cheonha.Breakthrough.TimeScale / 60f, life.Minutes, 5);
        }

        [Fact] public void 돌파_연출_도중_또_돌파해도_멈추지_않고_끝만_옮긴다()
        {
            var (_, life) = NewLife();
            var curve = new XpCurve(_d.Balance.XpCurve);
            life.Absorb(curve.CumulativeTo(10));
            life.Tick(0.3f, life.Position, 0);
            Assert.True(life.Breakthrough.IsActive);
            var a = life.Absorb(curve.CumulativeTo(20) - curve.CumulativeTo(10));
            Assert.Single(a.Breakthroughs);
            Assert.Equal(2, life.Breakthrough.ToRealm);
            Assert.Equal(0, life.Breakthrough.FromRealm);
            for (int i = 0; i < 20; i++) life.Tick(0.1f, life.Position, 0);
            Assert.False(life.Breakthrough.IsActive);
            Assert.Equal(2, life.Realm);
        }

        [Fact] public void 다른_지역으로_가면_처음_밟은_땅으로_적힌다()
        {
            var (_, life) = NewLife();
            var to = life.Regions.Center("janggang");
            life.Tick(0.1f, to, 0);
            Assert.Equal("janggang", life.RegionId);
            Assert.Contains("janggang", life.Log.VisitedRegions);
        }

        [Fact] public void 땅의_주인_싸움이면_목표_0점4배이고_쓰러뜨리면_일지에_남는다()
        {
            var (_, life) = NewLife();
            var curve = new XpCurve(_d.Balance.XpCurve);
            life.Absorb(curve.CumulativeTo(35));
            Assert.Equal(BossRegionState.Awake, life.Bosses.State("songshan"));
            // 태어난 숭산 한가운데서 절정에 올라 이미 혈운 기둥 원 안이다: 5초 경고 뒤 싸움(T-29)
            life.Tick(0.1f, life.Regions.Center("songshan"), 0);
            Assert.Equal(LifePhase.Roaming, life.Phase);
            Assert.Equal("songshan", life.Bosses.GraceRegionId);
            for (int i = 0; i < 400 && life.Phase != LifePhase.BossFight; i++) life.Tick(0.1f, life.Regions.Center("songshan"), 0);
            Assert.Equal(LifePhase.BossFight, life.Phase);
            Assert.True(life.Director.BossFight);
            Assert.Null(life.Bosses.GraceRegionId);

            var kill = life.DefeatBoss();
            Assert.Equal("songshan", kill.RegionId);
            Assert.Contains("songshan", life.Log.BossesKilled);
            Assert.Equal(LifePhase.Roaming, life.Phase);
            Assert.False(life.Director.BossFight);
        }

        [Fact] public void 쓰러지면_정산_기록이_나오고_혼에_옮길_수_있다()
        {
            var (soul, life) = NewLife();
            RunThirtyMinutes(_d, life);
            var rec = life.End(LifeEndWay.Fallen, 40);
            Assert.Equal(LifePhase.Ended, life.Phase);
            Assert.Null(life.Tick(0.1f, life.Position, 0));
            Assert.Throws<InvalidOperationException>(() => life.End(LifeEndWay.Zuohwa, 0));
            Assert.Throws<InvalidOperationException>(() => life.Absorb(1));
            var sum = soul.EndLife(rec);
            Assert.True(sum.RebirthQi > 0);
            Assert.Contains("hyeongyeong", sum.NewRealms);
            Assert.Equal(2, soul.Lives); // 서장 + 이번 생
        }

        [Fact] public void 혈마의_그림자는_30분_현경에서_봉인석이_없으면_나온다()
        {
            var (_, life) = NewLife();
            RunThirtyMinutes(_d, life);
            Assert.Equal(1, life.ShadowStep);
        }

        [Fact] public void 마교_몸은_진기가_농도배이고_다른_몸은_그대로()
        {
            var (_, demon) = NewLife("demonic");
            Assert.NotNull(demon.BloodArt);
            float density = demon.Regions.Density(demon.Position, 0);
            demon.Absorb(10);
            Assert.Equal(10 * density, demon.Xp.Total, 2);
            var (_, monk) = NewLife("shaolin");
            Assert.Null(monk.BloodArt);
            monk.Absorb(10);
            Assert.Equal(10, monk.Xp.Total, 3);
        }

        [Fact] public void 모르는_끝은_예외()
        {
            var (_, life) = NewLife();
            Assert.Throws<ArgumentException>(() => life.End("explode", 0));
        }
    }
}
