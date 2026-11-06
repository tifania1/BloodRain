using System;
using BloodRain.Core.Combat;
using Xunit;

namespace BloodRain.Core.Tests
{
    // 기획서 12장 '화면 편의 규칙' 조준, 웹 aimAngle(WP-52)과 같은 규칙인지.
    public class AimTests
    {
        static readonly float[] Xs = { 5f, -2f, 0f };
        static readonly float[] Ys = { 0f, 0f, 3f };

        [Fact]
        public void 가까운_적_방식이면_닿는_거리_안_가장_가까운_적_쪽()
        {
            // 오른쪽으로 걷는 중이어도 왼쪽 2m 적을 노린다
            float a = Aim.Angle(AimMode.Nearest, 0f, 0f, 1f, 0f, Xs, Ys, 10f);
            Assert.Equal(Math.PI, a, 4);
        }

        [Fact]
        public void 닿는_거리_밖이면_바라보는_쪽()
        {
            float a = Aim.Angle(AimMode.Nearest, 0f, 0f, 0f, -1f, Xs, Ys, 1.5f);
            Assert.Equal(-Math.PI / 2, a, 4);
            Assert.Equal(-1, Aim.Nearest(0f, 0f, Xs, Ys, 1.5f));
        }

        [Fact]
        public void 이동_방향_방식이면_적이_있어도_바라보는_쪽()
        {
            float a = Aim.Angle(AimMode.Move, 0f, 0f, 1f, 0f, Xs, Ys, 10f);
            Assert.Equal(0f, a, 4);
        }

        [Fact]
        public void 스틱을_기울이는_동안은_스틱_쪽이_먼저()
        {
            Assert.Equal(Math.PI / 2, Aim.Angle(AimMode.Nearest, 0f, 0f, 1f, 0f, Xs, Ys, 10f, 0f, 1f), 4);
            Assert.Equal(Math.PI / 2, Aim.Angle(AimMode.Move, 0f, 0f, 1f, 0f, Xs, Ys, 10f, 0f, 1f), 4);
        }

        [Fact]
        public void 같은_거리면_앞_번호이고_바라보는_쪽이_없으면_오른쪽()
        {
            Assert.Equal(0, Aim.Nearest(0f, 0f, new[] { 1f, -1f }, new[] { 0f, 0f }, 5f));
            Assert.Equal(0f, Aim.Angle(AimMode.Move, 0f, 0f, 0f, 0f, false, 0f, 0f));
        }

        [Fact]
        public void 닿는_거리는_근접_사거리x1_25_탄은_탄속x수명()
        {
            Assert.Equal(5f, Aim.MeleeReach(4f), 4);
            Assert.Equal(8.1f, Aim.ProjectileReach(9f, 0.9f), 4);
            Assert.Equal(AimMode.Move, Aim.Parse("move"));
            Assert.Equal(AimMode.Nearest, Aim.Parse(null));
            Assert.Equal("near", Aim.ToKey(AimMode.Nearest));
        }

        [Fact]
        public void 웹_프로토타입_aimAngle_과_같은_닿는_거리를_쓴다()
        {
            var html = TestData.ReadRepoFile("web", "prototype", "template.html");
            Assert.Contains("function aimAngle(reach)", html);
            Assert.Contains("aimAngle(R * 1.25)", html);
            Assert.Contains("aimAngle(ep.projectileSpeed * ep.projectileLifeSeconds)", html);
            Assert.Equal(1.25f, Aim.MeleeReachScale);
        }
    }
}
