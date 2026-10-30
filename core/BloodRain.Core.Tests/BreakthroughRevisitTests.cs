using System;
using BloodRain.Core.World;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>돌파 연출의 '다시 오른 경지' 줄임(cheonha.json breakthrough.revisitSeconds)과 잘못 부른 Start. 기획서 7장 '돌파 연출'.</summary>
    public class BreakthroughRevisitTests
    {
        readonly Data.GameData _d = TestData.Load();

        [Fact] public void 다시_오른_경지는_같은_순서를_줄여서_보여_준다()
        {
            var def = _d.Cheonha.Breakthrough;
            float k = def.RevisitSeconds / def.ZoomToSeconds; // 0.7 / 1.4 = 0.5
            var b = new Breakthrough(_d);
            b.Start(9, 10, revisit: true);
            Assert.True(b.IsRevisit);
            Assert.Equal(def.RevisitSeconds, b.DurationSeconds, 4);

            Assert.Equal(BreakthroughCue.Stamp, b.Tick(0f));
            Assert.Equal(BreakthroughCue.None, b.Tick(def.ZoomFromSeconds * k - 0.005f));
            Assert.Equal(BreakthroughCue.ZoomStart, b.Tick(0.01f));
            Assert.Equal(BreakthroughCue.Sweep, b.Tick(_d.Cheonha.Crush.SweepAtSeconds * k - def.ZoomFromSeconds * k));
            Assert.True(b.IsActive);
            Assert.True(b.SpawnHeld);
            Assert.Equal(def.TimeScale, b.WorldTimeScale);
            Assert.Equal(BreakthroughCue.End, b.Tick(def.RevisitSeconds - _d.Cheonha.Crush.SweepAtSeconds * k));
            Assert.False(b.IsActive);
            Assert.False(b.SpawnHeld); // 생성 멈춤도 같이 줄어든다
        }

        [Fact] public void 줄인_연출도_쓸림_반경과_끝_화면은_처음과_같다()
        {
            var a = new Breakthrough(_d);
            var b = new Breakthrough(_d);
            a.Start(34, 35);
            b.Start(34, 35, revisit: true);
            a.Tick(5f);
            b.Tick(5f);
            Assert.Equal(a.SweepRadius, b.SweepRadius, 3);
            Assert.Equal(a.ViewDiagonal, b.ViewDiagonal, 3);
            Assert.Equal(a.ToRealm, b.ToRealm);
        }

        [Fact] public void 줄인_다음에_처음_오르는_경지는_다시_제_길이로_간다()
        {
            var def = _d.Cheonha.Breakthrough;
            var b = new Breakthrough(_d);
            b.Start(9, 10, revisit: true);
            b.Tick(5f);
            b.Start(19, 20);
            Assert.False(b.IsRevisit);
            Assert.Equal(def.ZoomToSeconds, b.DurationSeconds, 4);
            b.Tick(def.RevisitSeconds + 0.01f);
            Assert.True(b.IsActive);
        }

        [Fact] public void 진행_중에_Start_를_또_불러도_돌던_연출은_그대로다()
        {
            var def = _d.Cheonha.Breakthrough;
            var b = new Breakthrough(_d);
            b.Start(19, 20);
            b.Tick(0.3f);
            Assert.Throws<InvalidOperationException>(() => b.Start(69, 70, revisit: true));
            Assert.False(b.IsRevisit);
            Assert.Equal(def.ZoomToSeconds, b.DurationSeconds, 4);
            Assert.Equal(19f, b.FromLevel);
            Assert.Equal(20f, b.ToLevel);
            Assert.Equal(0.3f, b.Elapsed, 4);
            b.Tick(0.6f); // 0.9초: 줄인 시계로 바뀌었다면 이미 끝났을 때
            Assert.True(b.IsActive);
        }

        [Fact] public void 시작하기_전_Tick_은_아무_일도_없다()
        {
            var b = new Breakthrough(_d);
            Assert.Equal(BreakthroughCue.None, b.Tick(1f));
            Assert.False(b.IsActive);
            Assert.False(b.SpawnHeld);
            Assert.Equal(1f, b.WorldTimeScale);
        }

        [Fact] public void 음수_시간은_시계를_되돌리지_않는다()
        {
            var b = new Breakthrough(_d);
            b.Start(9, 10);
            b.Tick(0.5f);
            b.Tick(-1f);
            Assert.Equal(0.5f, b.Elapsed, 4);
        }
    }
}
