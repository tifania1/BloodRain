using System;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using Xunit;

namespace BloodRain.Core.Tests
{
    public class DashTests
    {
        static DashDef Def => new DashDef { Distance = 4f, Duration = 0.25f, Iframes = 0.2f, Charges = 2, RechargeSeconds = 4f };

        static float Run(Dash d, float seconds, float step = 1f / 60f)
        {
            float x = 0f;
            for (float t = 0f; t < seconds - 1e-5f; t += step) x += d.Tick(Math.Min(step, seconds - t)).dx;
            return x;
        }

        [Fact]
        public void 한_번_대시는_정확히_거리만큼_간다()
        {
            var d = new Dash(Def);
            Assert.True(d.TryStart(1, 0));
            float x = Run(d, 0.5f);
            Assert.Equal(4f, x, 3);
            Assert.False(d.IsDashing);
        }

        [Fact]
        public void 무적은_iframes_동안만()
        {
            var d = new Dash(Def);
            d.TryStart(0, 1);
            d.Tick(0.19f);
            Assert.True(d.Invulnerable);
            d.Tick(0.02f);
            Assert.False(d.Invulnerable);
        }

        [Fact]
        public void 대시_중엔_다시_못_쓰고_충전은_둘()
        {
            var d = new Dash(Def);
            Assert.True(d.TryStart(1, 0));
            Assert.False(d.TryStart(1, 0));
            Run(d, 0.3f);
            Assert.True(d.TryStart(1, 0));
            Run(d, 0.3f);
            Assert.Equal(0, d.Charges);
            Assert.False(d.TryStart(1, 0));
        }

        [Fact]
        public void 충전은_recharge_초마다_하나()
        {
            var d = new Dash(Def);
            d.TryStart(1, 0); Run(d, 0.3f);
            d.TryStart(1, 0);
            Run(d, 3.6f); // 첫 대시부터 3.9초
            Assert.Equal(0, d.Charges);
            Run(d, 0.2f);
            Assert.Equal(1, d.Charges);
            Run(d, 4.1f);
            Assert.Equal(2, d.Charges);
            Assert.Equal(1f, d.RechargeProgress);
        }

        [Fact]
        public void 방향이_없으면_마지막_방향_대각선은_정규화()
        {
            var d = new Dash(Def);
            d.TryStart(3, 4);
            var (dx, dy) = d.Tick(0.1f);
            Assert.Equal(0.6f * 16f * 0.1f, dx, 3);
            Assert.Equal(0.8f * 16f * 0.1f, dy, 3);
            Run(d, 0.3f);
            d.TryStart(0, 0);
            Assert.Equal(0.6f, d.DirX, 3);
        }

        [Fact]
        public void 최대_충전이_늘면_바로_채운다()
        {
            var d = new Dash(Def, extraCharges: 1);
            Assert.Equal(3, d.MaxCharges);
            d.TryStart(1, 0);
            d.SetMaxCharges(4);
            Assert.Equal(3, d.Charges);
            d.SetMaxCharges(1);
            Assert.Equal(1, d.Charges);
        }

        [Fact]
        public void 데이터의_대시값은_기획서와_같다()
        {
            var b = TestData.Load().Balance.Dash;
            Assert.Equal(4f, b.Distance);
            Assert.Equal(0.25f, b.Duration);
            Assert.Equal(0.2f, b.Iframes);
            Assert.Equal(2, b.Charges);
        }
    }
}
