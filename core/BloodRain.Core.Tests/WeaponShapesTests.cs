using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using BloodRain.Core.Combat;
using Xunit;

namespace BloodRain.Core.Tests
{
    // 기획서 9장 M3 무공 일곱의 모양 — 웹 프로토타입 fireWeapon 과 같은 규칙인지(unity_tasks R-12Ua·R-12Ub).
    public class WeaponShapesTests
    {
        [Fact]
        public void Trial_상수가_웹_TRIAL_W_와_같다()
        {
            var html = TestData.ReadRepoFile("web", "prototype", "template.html");
            var m = Regex.Match(html, @"const TRIAL_W = \{([^}]*)\}");
            Assert.True(m.Success);
            var web = m.Groups[1].Value.Split(',').Select(p => p.Split(':')).ToDictionary(p => p[0].Trim(), p => float.Parse(p[1].Trim(), CultureInfo.InvariantCulture));
            var core = new Dictionary<string, float>
            {
                ["bladeTurnPerSecond"] = WeaponShapes.Trial.BladeTurnPerSecond, ["bladeRadius"] = WeaponShapes.Trial.BladeRadius,
                ["bladeRehitSeconds"] = WeaponShapes.Trial.BladeRehitSeconds, ["pullTo"] = WeaponShapes.Trial.PullTo,
                ["clawSpreadRad"] = WeaponShapes.Trial.ClawSpreadRad, ["clawWidth"] = WeaponShapes.Trial.ClawWidth,
                ["sonicStunSeconds"] = WeaponShapes.Trial.SonicStunSeconds, ["sonicSecondRingDelay"] = WeaponShapes.Trial.SonicSecondRingDelay,
                ["spearWidth"] = WeaponShapes.Trial.SpearWidth, ["petalSpreadRad"] = WeaponShapes.Trial.PetalSpreadRad,
            };
            Assert.Equal(web.Keys.OrderBy(k => k), core.Keys.OrderBy(k => k));
            foreach (var kv in core) Assert.Equal(web[kv.Key], kv.Value, 4);
            // 웹에 글자로 박힌 셋도 같은 값
            Assert.Contains("(dx * ca + dy * sa) >= -0.1 * d", html);
            Assert.Contains("delay: s * 0.15", html);
            Assert.Contains("along > -0.2 && along < R + e.r && perp < TRIAL_W.spearWidth / 2 + e.r", html);
        }

        [Fact]
        public void 취선봉_원호는_앞쪽만_절기는_여덟_갈래()
        {
            Assert.True(WeaponShapes.ArcHits(2f, 0f, 0.3f, 0f, 2f));
            Assert.False(WeaponShapes.ArcHits(-2f, 0f, 0.3f, 0f, 3f));
            Assert.True(WeaponShapes.ArcHits(-0.05f, 1f, 0.3f, 0f, 3f));   // 옆구리 조금 뒤는 맞음
            Assert.Equal(8, WeaponShapes.ArcDirections(true, 8, () => 0.5).Length);
            Assert.Equal((float)Math.PI, WeaponShapes.ArcDirections(false, 8, () => 0.5)[0], 4);
        }

        [Fact]
        public void 태극검은_고르게_돌고_같은_적은_0_5초마다()
        {
            Assert.Equal((float)Math.PI, WeaponShapes.BladeAngle(0f, 0f, 1, 2), 4);
            Assert.Equal((float)(2 * Math.PI * 1.2), WeaponShapes.BladeAngle(0f, 1f, 0, 2), 3);
            Assert.False(WeaponShapes.BladeCanRehit(1f, 1.4f));
            Assert.True(WeaponShapes.BladeCanRehit(1f, 1.5f));
            Assert.True(WeaponShapes.BladeCanRehit(null, 0f));
            Assert.False(WeaponShapes.BladePushes(true, 1));
            Assert.True(WeaponShapes.BladePushes(false, 1));
        }

        [Fact]
        public void 쇄겸술은_먼_적부터_몸_앞_1_5m_로()
        {
            var xs = new[] { 1f, 5f, 3f, 9f }; var ys = new[] { 0f, 0f, 0f, 0f };
            Assert.Equal(new[] { 1, 2 }, WeaponShapes.FarthestWithin(0f, 0f, xs, ys, 6f, 2));
            var (x, y) = WeaponShapes.PulledTo(0f, 0f, 5f, 0f);
            Assert.Equal(1.5f, x, 4); Assert.Equal(0f, y, 4);
            Assert.Equal(1f, WeaponShapes.PulledTo(0f, 0f, 1f, 0f).x, 4);   // 이미 가까우면 그대로
        }

        [Fact]
        public void 흑룡탄은_몸_앞_1_5m_로_0_1초씩_모은다()
        {
            Assert.Equal(1.5f, WeaponShapes.GatherCenter(0f, 0f, 1f, 0f).x, 4);
            Assert.Equal(6, WeaponShapes.GatherSteps(0.6f));
            Assert.Equal(0.15f, WeaponShapes.GatherFraction(1, 6), 4);
            Assert.Equal(0.9f, WeaponShapes.GatherFraction(6, 6), 4);
            var html = TestData.ReadRepoFile("web", "prototype", "template.html");
            Assert.Contains("steps = Math.max(1, Math.round(ep.gatherSeconds / 0.1))", html);
            Assert.Contains("const f = 1 / (steps - k + 1); e.x += (cx - e.x) * f * 0.9", html);
        }

        [Fact]
        public void 혈조_갈래는_조준_각_가운데로_벌어진다()
        {
            var a = WeaponShapes.ClawAngles(0f, 3);
            Assert.Equal(new[] { -0.35f, 0f, 0.35f }, a.Select(v => (float)Math.Round(v, 4)));
            Assert.True(WeaponShapes.ClawHits(1.5f, 0.3f, 0.2f, 0f, 2f));
            Assert.False(WeaponShapes.ClawHits(1.5f, 0.6f, 0.2f, 0f, 2f));
            Assert.False(WeaponShapes.ClawHits(-0.5f, 0f, 0.2f, 0f, 2f));
        }

        [Fact]
        public void 음공_두_겹과_기절()
        {
            Assert.Equal(new[] { 0f, 0.3f }, WeaponShapes.RingDelays(true));
            Assert.Single(WeaponShapes.RingDelays(false));
            Assert.False(WeaponShapes.RingStuns(true, 1f, 0.0));
            Assert.True(WeaponShapes.RingStuns(false, 0.3f, 0.29));
        }

        [Fact]
        public void 이화창_방향과_폭()
        {
            Assert.Single(WeaponShapes.ThrustDirections(0f, false, 0, false));
            Assert.Equal(4, WeaponShapes.ThrustDirections(0f, false, 0, true).Length);
            Assert.Equal(6, WeaponShapes.ThrustDirections(0f, true, 6, true).Length);
            Assert.True(WeaponShapes.ThrustHits(3f, 0.4f, 0.2f, 0f, 3f));
            Assert.False(WeaponShapes.ThrustHits(3f, 0.6f, 0.2f, 0f, 3f));
            Assert.True(WeaponShapes.ThrustHits(-0.1f, 0f, 0.2f, 0f, 3f));
        }

        [Fact]
        public void 낙화검우는_부채꼴_절기는_한_바퀴()
        {
            var p = WeaponShapes.PetalAngles(0f, 2, false, () => 0.0);
            Assert.All(p, v => Assert.Equal(-0.6f, v, 4));
            var e = WeaponShapes.PetalAngles(0f, 4, true, () => 0.0);
            Assert.Equal((float)(Math.PI / 2), e[1], 4);
        }
    }
}
