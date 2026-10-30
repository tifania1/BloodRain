using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using BloodRain.Core.World;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>천하 축척(World.Scale)과 돌파 연출(World.Breakthrough). 기획서 7장 '경지와 축척'·'돌파 연출'.</summary>
    public class ScaleTests
    {
        readonly Data.GameData _d = TestData.Load();
        Scale NewScale() => new Scale(_d);
        static float Num(string s) => float.Parse(s.Replace(",", "").TrimEnd('m'), CultureInfo.InvariantCulture);

        // ---- 배율과 화면 대각선 ----

        [Fact] public void 경지_배율은_2_25의_거듭제곱이다()
        {
            var s = NewScale();
            Assert.Equal(1f, s.Factor(0), 5);
            Assert.Equal(2.25f, s.Factor(1), 5);
            Assert.Equal(11.390625f, s.Factor(3), 4);
            Assert.Equal(20.25f, s.ToWorld(4f, 2), 4);    // 일류(순번 2)의 경공 4m = 세계 20.25m
            Assert.Equal(4f, s.ToRealm(20.25f, 2), 4);
            Assert.Equal(_d.Cheonha.World.ScalePerRealm, s.Factor(1), 5);
        }

        [Fact] public void 경지와_축척_표의_화면_대각선과_장막과_덩이를_계산으로_다시_맞춘다()
        {
            var s = NewScale();
            var rows = TestData.GddTable(TestData.GddSection("경지와 축척"));
            Assert.Equal(s.Realms.Count, rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                var realm = s.Realms[i];
                Assert.Equal(realm.NameKo, rows[i][0]);
                Assert.Equal(i, s.RealmIndex(realm.Level));
                // 경지에 막 오른 순간의 대각선이 표의 값
                Assert.Equal(Num(rows[i][3]), (float)Math.Round(s.ViewDiagonal(realm.Level)));
                Assert.Equal(Num(rows[i][3]), (float)Math.Round(s.ViewDiagonalAtRealm(i)));
                var boundary = s.BoundaryRadius(i);
                if (rows[i][4] == "없음") Assert.Null(boundary);
                else Assert.Equal(Num(rows[i][4]), (float)Math.Round(boundary!.Value));
                Assert.EndsWith($" {s.ClusterSize(i)}", rows[i][6]);
            }
            Assert.False(s.HasBoundary(s.RealmIndex("hyeongyeong")));
            Assert.True(s.HasBoundary(s.RealmIndex("hwagyeong")));
        }

        [Fact] public void 경지_안에서_레벨이_오르면_조금씩_물러난다()
        {
            var s = NewScale();
            float share = _d.Cheonha.World.ViewInRealmShare;
            Assert.Equal(1f, s.ViewExponent(10), 4);                    // 이류에 막 오름
            Assert.Equal(1f + share * 0.5f, s.ViewExponent(15), 4);     // 이류 10 → 일류 20의 절반
            Assert.Equal(36f * (float)Math.Pow(2.25, 1 + share * 0.5), s.ViewDiagonal(15), 2);
            Assert.Equal(5f + share * 19f / 20f, s.ViewExponent(89), 4); // 화경 70 → 현경 90
            Assert.Equal(6f, s.ViewExponent(90), 4);
            Assert.Equal(6f, s.ViewExponent(120), 4);                   // 현경은 진행 0
            Assert.Equal(0f, s.ProgressInRealm(99));
            // 레벨이 오르는 동안 화면은 한 번도 다가오지 않는다
            for (int lv = 1; lv < 100; lv++) Assert.True(s.ViewDiagonal(lv + 1) >= s.ViewDiagonal(lv), $"Lv{lv}");
            // 돌파 직전 0.35단계까지 물러나 있고, 돌파 때 남은 0.65단계를 한 번에 물러난다(7장 본문)
            Assert.Equal(1f + share, s.ViewExponent(19.9999f), 3);
            Assert.Equal(1f - share, s.ViewExponent(20) - s.ViewExponent(19.9999f), 3);
            Assert.Contains($"남은 {TestData.Num(1f - share)}단계", TestData.GddSection("경지와 축척"));
        }

        // ---- 혈우 장막 ----

        [Fact] public void 장막_밖으로_나가면_출생지_쪽으로_장막_위까지_되돌린다()
        {
            var s = NewScale();
            var home = new Vector2(2400, 962); // 숭산
            float r0 = s.BoundaryRadius(0)!.Value;
            Assert.Equal(86.4f, r0, 3);

            var inside = home + new Vector2(50, -30);
            Assert.True(s.IsInside(inside, home, 0));
            Assert.Equal(inside, s.KeepInside(inside, home, 0));

            var outside = home + new Vector2(120, 0);
            Assert.False(s.IsInside(outside, home, 0));
            var back = s.KeepInside(outside, home, 0);
            Assert.Equal(home.X + r0, back.X, 3);
            Assert.Equal(home.Y, back.Y, 3);

            var diag = s.KeepInside(home + new Vector2(300, 400), home, 0); // 방향은 그대로, 거리만 반경으로
            Assert.Equal(r0, Vector2.Distance(diag, home), 2);
            Assert.Equal(0.6f, (diag.X - home.X) / r0, 3);

            // 이류로 오르면 장막이 넓어져 같은 자리가 안이 된다. 현경엔 장막이 없다.
            Assert.True(s.IsInside(outside, home, 1));
            int top = s.RealmIndex("hyeongyeong");
            var far = new Vector2(0, 0);
            Assert.True(s.IsInside(far, home, top));
            Assert.Equal(far, s.KeepInside(far, home, top));
        }

        // ---- 밟힘과 쓸림 ----

        [Fact] public void 아래_경지에서_생긴_적은_닿으면_밟히고_피해가_없다()
        {
            var s = NewScale();
            Assert.True(s.Crushes(0, 1));
            Assert.False(s.DealsContactDamage(0, 1));
            Assert.True(s.Crushes(2, 5));
            Assert.False(s.Crushes(1, 1));
            Assert.True(s.DealsContactDamage(1, 1));
            Assert.False(s.Crushes(3, 2));
            Assert.False(s.Crushes(0, 4, boss: true));                 // 땅의 주인은 밟히지 않는다
            Assert.Equal(_d.Cheonha.Crush.XpPerCrushed, s.CrushXp);
            Assert.Equal(0.3f, s.CrushXp, 4);
        }

        [Fact] public void 돌파_쓸림은_반경_안의_아래_경지_적만_고른다()
        {
            var s = NewScale();
            var foes = new List<ScaledFoe>
            {
                new ScaledFoe(0, 10),             // 0: 고른다
                new ScaledFoe(1, 10),             // 1: 새 경지(이류=1)에서 생김 → 안 고른다
                new ScaledFoe(0, 50),             // 2: 반경 경계 → 고른다
                new ScaledFoe(0, 50.01f),         // 3: 반경 밖
                new ScaledFoe(0, 5, boss: true),  // 4: 땅의 주인
            };
            Assert.Equal(new[] { 0, 2 }, s.SelectSweep(foes, 1, 50));
        }

        // ---- 적 한 덩이 ----

        [Theory]
        [InlineData(16, 1.0f, 16)]
        [InlineData(16, 0.5f, 8)]
        [InlineData(16, 0.25f, 4)]   // 딱 떨어지면 올리지 않는다
        [InlineData(16, 0.26f, 5)]
        [InlineData(16, 0.01f, 1)]   // 살아 있으면 적어도 하나
        [InlineData(16, 0f, 0)]
        [InlineData(16, 1.3f, 16)]
        [InlineData(1, 0.2f, 1)]
        [InlineData(64, 0.7f, 45)]   // ceil(44.8)
        public void 덩이에_그릴_사람_수는_남은_기혈_비율만큼(int cluster, float ratio, int expected)
        {
            Assert.Equal(expected, Scale.MembersToDraw(cluster, ratio));
        }

        [Fact] public void 덩이_크기와_처치_수는_생긴_경지를_따른다()
        {
            var s = NewScale();
            Assert.Equal(_d.Cheonha.RealmScale.Select(r => r.ClusterSize), Enumerable.Range(0, s.Realms.Count).Select(s.ClusterSize));
            int peak = s.RealmIndex("jeoljeong");
            Assert.Equal(16, s.KillCount(peak));
            Assert.Equal(8, s.DrawMembers(peak, 0.5f));
            Assert.Equal(1, s.KillCount(0));
        }

        // ---- 돌파 연출 ----

        [Fact] public void 돌파_연출_시간표와_세상_시간()
        {
            var b = new Breakthrough(_d);
            var s = NewScale();
            var def = _d.Cheonha.Breakthrough;
            b.Start(9, 10);
            Assert.Equal(0, b.FromRealm);
            Assert.Equal(1, b.ToRealm);
            Assert.Equal(BreakthroughCue.Stamp, b.Tick(0f));
            Assert.Equal(def.TimeScale, b.WorldTimeScale);
            Assert.Equal(0.05f, b.WorldTimeScale, 4);
            Assert.True(b.SpawnHeld);

            Assert.Equal(BreakthroughCue.None, b.Tick(def.ZoomFromSeconds - 0.01f));
            Assert.Equal(s.ViewDiagonal(9), b.ViewDiagonal, 3);         // 아직 그대로
            Assert.Equal(BreakthroughCue.ZoomStart, b.Tick(0.01f));

            // 0.8초는 물러나기(0.2~1.4초)의 한가운데: 천천히 출발·멈춤이라도 가운데는 절반, 배율로는 두 대각선의 기하 평균
            Assert.Equal(BreakthroughCue.Sweep, b.Tick(_d.Cheonha.Crush.SweepAtSeconds - def.ZoomFromSeconds));
            Assert.True(b.SweepDone);
            Assert.Equal(0.5f, b.ZoomProgress, 3);
            Assert.Equal((float)Math.Sqrt(s.ViewDiagonal(9) * s.ViewDiagonal(10)), b.ViewDiagonal, 2);
            Assert.Equal(b.ViewDiagonal * _d.Cheonha.Crush.SweepViewMultiple, b.SweepRadius, 3);
            Assert.True(b.IsActive);

            Assert.Equal(BreakthroughCue.End, b.Tick(def.ZoomToSeconds - _d.Cheonha.Crush.SweepAtSeconds));
            Assert.False(b.IsActive);
            Assert.Equal(1f, b.WorldTimeScale);
            Assert.False(b.SpawnHeld);
            Assert.Equal(81f, (float)Math.Round(b.ViewDiagonal));        // 이류 첫 화면
            Assert.Equal(s.BoundaryRadius(0), b.FromBoundaryRadius);
            Assert.Equal(s.BoundaryRadius(1), b.ToBoundaryRadius);
            Assert.Equal(BreakthroughCue.None, b.Tick(1f));
        }

        [Fact] public void 물러나기는_천천히_출발해_천천히_멈춘다()
        {
            var b = new Breakthrough(_d);
            var def = _d.Cheonha.Breakthrough;
            float len = def.ZoomToSeconds - def.ZoomFromSeconds;
            float early = b.ZoomProgressAt(def.ZoomFromSeconds + len * 0.1f);
            float late = b.ZoomProgressAt(def.ZoomFromSeconds + len * 0.9f);
            Assert.True(early < 0.1f, $"{early}");
            Assert.True(late > 0.9f, $"{late}");
            Assert.Equal(0f, b.ZoomProgressAt(0f));
            Assert.Equal(1f, b.ZoomProgressAt(def.ZoomToSeconds + 1f));
        }

        [Fact] public void 한_번에_건너뛰어도_신호는_순서대로_모두_온다()
        {
            var b = new Breakthrough(_d);
            var seen = new List<BreakthroughCue>();
            b.Cue += c => seen.Add(c);
            b.Start(30, 36); // 일류 → 절정
            var all = b.Tick(5f);
            Assert.Equal(BreakthroughCue.Stamp | BreakthroughCue.ZoomStart | BreakthroughCue.Sweep | BreakthroughCue.End, all);
            Assert.Equal(new[] { BreakthroughCue.Stamp, BreakthroughCue.ZoomStart, BreakthroughCue.Sweep, BreakthroughCue.End }, seen);
            Assert.Equal(new Scale(_d).ViewDiagonal(36), b.ViewDiagonal, 2);
            // 끝난 뒤 다시 시작할 수 있다
            b.Start(49, 50);
            Assert.True(b.IsActive);
            Assert.Throws<InvalidOperationException>(() => b.Start(69, 70));
        }

        [Fact] public void 쓸림은_새_경지보다_아래에서_생긴_적이_대상이다()
        {
            var b = new Breakthrough(_d);
            b.Start(34, 35); // 일류(2) → 절정(3)
            b.Tick(5f);
            float r = b.SweepRadius;
            var foes = new[] { new ScaledFoe(2, r * 0.5f), new ScaledFoe(3, r * 0.5f), new ScaledFoe(2, r * 1.1f), new ScaledFoe(0, r) };
            Assert.Equal(new[] { 0, 3 }, b.SelectSweep(foes));
        }

        [Fact] public void 돌파_연출_표의_시각이_데이터와_같다()
        {
            var sec = TestData.GddSection("돌파 연출");
            var rows = TestData.GddTable(sec);
            var def = _d.Cheonha.Breakthrough;
            var crush = _d.Cheonha.Crush;
            Assert.Equal($"0~{TestData.Num(def.ZoomFromSeconds)}초", rows[0][0]);
            Assert.Equal($"{TestData.Num(def.ZoomFromSeconds)}~{TestData.Num(def.ZoomToSeconds)}초", rows[1][0]);
            Assert.Equal($"{TestData.Num(crush.SweepAtSeconds)}초", rows[2][0]);
            Assert.Equal($"{TestData.Num(def.ZoomToSeconds)}초 뒤", rows[3][0]);
            Assert.Contains($"진기 {TestData.Num(crush.XpPerCrushed)}", rows[2][1]);
            Assert.Equal(def.ZoomToSeconds, def.SpawnHoldSeconds, 3); // 생성 멈춤은 물러나기가 끝날 때까지
        }
    }
}
