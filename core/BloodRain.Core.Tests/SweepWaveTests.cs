using System;
using System.IO;
using BloodRain.Core.Combat;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>마천패 2막 역류 파도(sweep_wave). 웹 lordContinuous 'sweep_wave' 와 같은 규칙인지 본다.</summary>
    public class SweepWaveTests
    {
        static Data.BossPatternDef Wave() => Load().BossPatternSet("ma_cheonpae").Pattern("crimson_reverse_wave");

        [Fact]
        public void 파는_화면_끝에서_생겨_예고_뒤_플레이어를_한_번만_맞힌다()
        {
            var p = Wave();
            Assert.Equal("sweep_wave", p.Shape);
            float diag = 20f, move = 3f;
            var w = new SweepWave(p, 0f, 0f, 0f, diag, move);
            Assert.Equal(-diag * SweepWave.StartViewMultiple, w.X, 3);
            Assert.True(w.Warning);
            Assert.False(w.Covers(w.X, 0f));

            float speed = BossPatterns.MetersPerSecond(p.ProjectileSpeed, move);
            int hits = 0;
            float t = 0f, hitAt = -1f;
            while (w.Step(0.01f))
            {
                t += 0.01f;
                if (w.TryHit(0f, 5f, 0.35f)) { hits++; hitAt = t; }
            }
            Assert.Equal(1, hits);
            // 띠 앞줄이 플레이어 몸 가장자리에 닿는 때: 예고 + (출발 거리 - 폭/2 - 몸 반경) / 속력
            float expect = SweepWave.WarnSeconds + (diag * SweepWave.StartViewMultiple - p.Width / 2f - 0.35f) / speed;
            Assert.InRange(hitAt, expect - 0.02f, expect + 0.02f);
            Assert.True(w.Done);
            Assert.Equal(diag * SweepWave.StartViewMultiple, w.X, 2);
        }

        [Fact]
        public void 띠는_진행_방향에_수직이고_옆으로_멀어도_맞는다()
        {
            var p = Wave();
            var w = new SweepWave(p, 0f, 0f, 90f, 10f, 3f);
            Assert.Equal(-5.5f, w.Y, 3);
            Assert.Equal(0f, w.X, 3);
            w.Step(SweepWave.WarnSeconds);
            Assert.True(w.Covers(100f, -5.5f));
            Assert.False(w.Covers(0f, -5.5f + p.Width));
        }

        [Fact]
        public void 파_간격과_방향_네_갈래()
        {
            var p = Wave();
            Assert.Equal(0f, SweepWave.WaveSeconds(p, 0));
            Assert.Equal(p.WaveIntervalSeconds * 3, SweepWave.WaveSeconds(p, 3));
            var rng = new Random(7);
            for (int i = 0; i < 50; i++) Assert.Contains(SweepWave.RandomDegrees(rng), new[] { 0f, 90f, 180f, 270f });
        }

        [Fact]
        public void 웹과_같은_상수를_쓴다()
        {
            string html = File.ReadAllText(Path.Combine(RepoRoot, "web", "prototype", "template.html"));
            int at = html.IndexOf("P.shape === 'sweep_wave'", StringComparison.Ordinal);
            Assert.True(at > 0);
            string body = html.Substring(at, 1400);
            Assert.Contains("viewDiag() * 0.55", body);
            Assert.Contains("warm: 0.6", body);
            Assert.Contains("P.width / 2 + 0.35", body);
        }
    }
}
