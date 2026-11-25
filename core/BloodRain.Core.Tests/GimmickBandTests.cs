using System.Linq;
using System.Numerics;
using BloodRain.Core.World;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>굽이 띠(장강 물길·곤륜 빙판)와 엇갈린 격자(무당 태극 바닥) 자리 함수 — 웹 합친 판 CHG.inWater·inIce·boatsW·taijiPads 와 같은 셈(2026-11-26).</summary>
    public class GimmickBandTests
    {
        readonly Data.GameData _d = TestData.Load();
        Regions R => new Regions(_d);
        Data.GimmicksDef G => _d.Gimmicks;
        Data.GimmickLayoutDef L => _d.Gimmicks.Layout;

        [Fact] public void 물길_띠는_한가운데를_지나고_반폭_밖은_물이_아니다()
        {
            var r = R; var cj = r.Region("janggang"); var o = r.Center(cj);
            Assert.True(GimmickLayout.InWater(r, G, 1f, o, 0));
            Assert.False(GimmickLayout.InWater(r, G, 1f, o + new Vector2(0, L.WaterSpacing / 2f), 0));
            // 다음 띠(j=1) 가운데 줄 위 아무 x 나 물
            for (float dx = -30; dx <= 30; dx += 2.5f)
            {
                float y = GimmickLayout.BandCenterY(o, 1, L.WaterSpacing, L.WaterWave, L.WaterWaveLen, o.X + dx);
                var p = new Vector2(o.X + dx, y);
                Assert.True(GimmickLayout.InBand(o, L.WaterSpacing, L.WaterHalf, L.WaterWave, L.WaterWaveLen, p));
                Assert.False(GimmickLayout.InBand(o, L.WaterSpacing, L.WaterHalf, L.WaterWave, L.WaterWaveLen, p + new Vector2(0, L.WaterHalf + 0.1f)));
            }
            // 물 없는 땅(숭산) 한가운데는 물이 아니다
            Assert.False(GimmickLayout.InWater(r, G, 1f, r.Center("songshan"), 0));
        }

        [Fact] public void 경지_축척을_곱하면_띠도_같이_커진다()
        {
            var r = R; var cj = r.Region("janggang"); var o = r.Center(cj);
            var p = o + new Vector2(0, L.WaterHalf * 1.5f);
            Assert.False(GimmickLayout.InWater(r, G, 1f, p, 0));
            Assert.True(GimmickLayout.InWater(r, G, 2.4f, p, 0));
        }

        [Fact] public void 빙판은_곤륜에만_같은_꼴로()
        {
            var r = R; var k = r.Center("kunlun");
            Assert.True(GimmickLayout.InIce(r, G, 1f, k, 0));
            Assert.False(GimmickLayout.InIce(r, G, 1f, k + new Vector2(0, L.IceSpacing / 2f), 0));
            Assert.False(GimmickLayout.InIce(r, G, 1f, r.Center("songshan"), 0));
        }

        [Fact] public void 굽이_배는_물_위에서_줄을_따라_기울고_흐른다()
        {
            var r = R; var cj = r.Region("janggang"); var o = r.Center(cj);
            var a = GimmickLayout.CurvedBoats(r, G, cj, 1f, 0f, o, 60f);
            Assert.NotEmpty(a);
            Assert.All(a, b => Assert.True(GimmickLayout.InWater(r, G, 1f, b.Pos, 0)));
            Assert.All(a, b => Assert.Equal(G.DriftingBoats.Speed, System.Math.Abs(b.Velocity.X), 3));
            // 1초 뒤엔 속도만큼 x 가 밀린다(간격 안에서)
            var b1 = GimmickLayout.CurvedBoats(r, G, cj, 1f, 1f, o, 60f);
            float sgn = System.Math.Sign(a[0].Velocity.X);
            Assert.Contains(b1, b => System.Math.Abs(b.Pos.X - (a[0].Pos.X + sgn * G.DriftingBoats.Speed)) < 0.01f);
            // 배 한가운데는 배 위, 반폭 넘으면 아님
            var boat = a[0];
            Assert.True(GimmickLayout.OnCurvedBoat(G.DriftingBoats, boat.Pos, boat.Angle, boat.Pos));
            var side = new Vector2(-(float)System.Math.Sin(boat.Angle), (float)System.Math.Cos(boat.Angle)) * (G.DriftingBoats.Width * 0.5f + 0.1f);
            Assert.False(GimmickLayout.OnCurvedBoat(G.DriftingBoats, boat.Pos, boat.Angle, boat.Pos + side));
        }

        [Fact] public void 태극_바닥은_엇갈린_격자고_한가운데에_하나()
        {
            var r = R; var wd = r.Region("wudangsan"); var o = r.Center(wd);
            var pads = GimmickLayout.StaggeredNear(r, wd, L.TaijiSpacing, o, 40f);
            Assert.Contains(pads, p => p.I == 0 && p.J == 0 && Vector2.Distance(p.Pos, o) < 0.01f);
            // 홀수 줄은 반 칸 비킴
            var odd = pads.First(p => p.J == 1);
            Assert.Equal(L.TaijiSpacing / 2f, ((odd.Pos.X - o.X) % L.TaijiSpacing + L.TaijiSpacing) % L.TaijiSpacing, 3);
            Assert.Equal(o.Y + L.TaijiSpacing, odd.Pos.Y, 3);
            Assert.All(pads, p => Assert.Equal("wudangsan", r.Nearest(p.Pos, 0).Id));
            // 칸 번호는 자리마다 하나
            Assert.Equal(pads.Count, pads.Select(p => (p.I, p.J)).Distinct().Count());
        }
    }
}
