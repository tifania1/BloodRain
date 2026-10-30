using System;
using System.Numerics;
using BloodRain.Core.World;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>혈우 장막 밀어내기(World.Scale.PushStrength·PushVelocity). 기획서 7장 '경지와 축척' 혈우 장막 문단.</summary>
    public class BoundaryPushTests
    {
        readonly Data.GameData _d = TestData.Load();
        Scale NewScale() => new Scale(_d);
        static readonly Vector2 Home = new Vector2(1000, 1000);

        [Fact] public void 띠_너비는_그_경지_첫_화면_대각선의_0_25배다()
        {
            var s = NewScale();
            for (int i = 0; i < s.TopRealm; i++)
                Assert.Equal(s.ViewDiagonalAtRealm(i) * _d.Cheonha.World.PushBandViewMultiple, s.PushBand(i), 3);
            Assert.Equal(0f, s.PushBand(s.TopRealm)); // 현경은 장막이 없다
            Assert.True(s.PushBand(0) < s.BoundaryRadius(0)!.Value);
        }

        [Fact] public void 띠_밖은_0_장막_위와_밖은_1_사이는_곧게_오른다()
        {
            var s = NewScale();
            float r = s.BoundaryRadius(1)!.Value, band = s.PushBand(1);
            Assert.Equal(0f, s.PushStrength(Home, Home, 1));
            Assert.Equal(0f, s.PushStrength(Home + new Vector2(r - band, 0), Home, 1), 4);
            Assert.Equal(0.5f, s.PushStrength(Home + new Vector2(0, r - band / 2), Home, 1), 3);
            Assert.Equal(1f, s.PushStrength(Home + new Vector2(-r, 0), Home, 1), 4);
            Assert.Equal(1f, s.PushStrength(Home + new Vector2(r * 3, 0), Home, 1));
        }

        [Fact] public void 미는_속도는_출생지_쪽이고_장막_위에선_걸음의_1_25배다()
        {
            var s = NewScale();
            float r = s.BoundaryRadius(2)!.Value;
            var pos = Home + new Vector2(r * 0.6f, r * 0.8f);
            float speed = s.ToWorld(5f, 2);
            var v = s.PushVelocity(pos, Home, 2, speed);
            Assert.Equal(speed * _d.Cheonha.World.PushMoveShare, v.Length(), 2);
            var dir = Vector2.Normalize(Home - pos);
            Assert.Equal(1f, Vector2.Dot(Vector2.Normalize(v), dir), 4);
        }

        [Fact] public void 미는_속도가_0인_경우들()
        {
            var s = NewScale();
            float r = s.BoundaryRadius(0)!.Value;
            Assert.Equal(Vector2.Zero, s.PushVelocity(Home, Home, 0, 5f));                          // 출생지 자리
            Assert.Equal(Vector2.Zero, s.PushVelocity(Home + new Vector2(r, 0), Home, 0, 0f));    // 서 있으면(속도 0) 밀 기준이 없다
            Assert.Equal(Vector2.Zero, s.PushVelocity(Home + new Vector2(r / 2, 0), Home, 0, 5f)); // 띠 안쪽
            Assert.Equal(Vector2.Zero, s.PushVelocity(Home + new Vector2(1e6f, 0), Home, s.TopRealm, 5f)); // 현경
            Assert.Equal(0f, s.PushStrength(Home + new Vector2(1e6f, 0), Home, s.TopRealm));
        }

        [Fact] public void 밖으로_곧장_걸으면_띠의_80퍼센트에서_멈춰_서고_장막은_못_넘는다()
        {
            var s = NewScale();
            for (int realm = 0; realm < s.TopRealm; realm++)
            {
                float r = s.BoundaryRadius(realm)!.Value, band = s.PushBand(realm);
                float speed = s.ToWorld(5f, realm), dt = 1f / 60f;
                var pos = Home;
                for (int f = 0; f < 60 * 600; f++)
                {
                    pos += (new Vector2(1, 0) * speed + s.PushVelocity(pos, Home, realm, speed)) * dt;
                    pos = s.KeepInside(pos, Home, realm);
                }
                float d = Vector2.Distance(pos, Home);
                float stall = s.StallDistance(realm)!.Value;
                Assert.Equal(r - band * 0.2f, stall, 1);
                Assert.InRange(d, stall - band * 0.01f, stall + band * 0.01f);
                Assert.True(d < r);
            }
            Assert.Null(s.StallDistance(s.TopRealm));
        }

        [Fact] public void 대시로_띠를_뚫어도_KeepInside_가_장막_위에서_멈추고_다음_프레임엔_안으로_밀린다()
        {
            var s = NewScale();
            float r = s.BoundaryRadius(0)!.Value;
            var pos = s.KeepInside(Home + new Vector2(r + 3f, 0), Home, 0);
            Assert.Equal(r, Vector2.Distance(pos, Home), 3);
            var next = pos + s.PushVelocity(pos, Home, 0, 5f) * (1f / 60f);
            Assert.True(Vector2.Distance(next, Home) < r);
        }

        [Fact] public void 기획서_장막_문단의_숫자가_데이터와_같다()
        {
            var w = _d.Cheonha.World;
            var sec = TestData.GddSection("경지와 축척");
            Assert.Contains($"첫 화면 대각선의 {TestData.Num(w.PushBandViewMultiple)}배", sec);
            Assert.Contains($"걸음의 {TestData.Num(w.PushMoveShare)}배", sec);
            Assert.Contains($"띠의 {Math.Round(100 / w.PushMoveShare)}% 자리", sec);
        }
    }
}
