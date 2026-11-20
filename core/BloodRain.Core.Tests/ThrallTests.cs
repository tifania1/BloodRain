using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>SC-06a(2026-11-20 사람이 추천대로 — decisions #50): 어강술 내 편 강시.</summary>
    public class ThrallTests
    {
        readonly GameData _d = TestData.Load();
        SecretEffects On => SecretEffects.Of(_d, new[] { "eogang" });
        float Interval => _d.Synergies.Secrets.First(s => s.Id == "eogang").Param("hitIntervalSeconds");

        [Fact] public void 값은_결정대로()
        {
            Assert.Equal((30f, 10), On.Thrall!.Value);
            Assert.Equal(0.5f, Interval);
        }

        [Fact] public void 어강술이_없으면_못_들이고_열까지만()
        {
            var band = new ThrallBand();
            Assert.Null(band.TryTake(SecretEffects.None, "sigang", Vector2.Zero, 2f, 5f, 0.3f, Interval));
            for (int i = 0; i < 12; i++) band.TryTake(On, "sigang", new Vector2(i, 0), 2f, 5f, 0.3f, Interval);
            Assert.Equal(10, band.Count);
        }

        [Fact] public void 가장_가까운_적을_쫓아_닿으면_0_5초마다_때리고_30초_뒤_사라진다()
        {
            var band = new ThrallBand();
            var t = band.TryTake(On, "sigang", Vector2.Zero, 2f, 5f, 0.3f, Interval)!;
            var enemy = new Vector2(3f, 0f);
            ThrallTarget Near(Vector2 p) => new ThrallTarget(77, enemy, 0.3f);
            var hits = new List<ThrallHit>();
            float time = 0f;
            for (int i = 0; i < 100; i++) { hits.AddRange(band.Tick(0.05f, Near, Interval)); time += 0.05f; }   // 5초
            Assert.True(Vector2.Distance(t.Pos, enemy) <= 0.6f + 1e-3f);
            Assert.All(hits, h => { Assert.Equal(77, h.TargetId); Assert.Equal(5f, h.Damage); });
            // 3m 를 초속 2m 로 가 닿는 데 1.2초쯤, 남은 3.8초에 0.5초마다 → 7~8번
            Assert.InRange(hits.Count, 7, 9);
            var gone = new List<int>();
            for (int i = 0; i < 520; i++) band.Tick(0.05f, Near, Interval, gone);
            Assert.Equal(0, band.Count);
            Assert.Equal(new[] { t.Id }, gone);
            Assert.Equal(1, band.Taken);
        }

        [Fact] public void 적이_없으면_가만히_있는다()
        {
            var band = new ThrallBand();
            var t = band.TryTake(On, "sigang", new Vector2(1, 1), 2f, 5f, 0.3f, Interval)!;
            Assert.Empty(band.Tick(1f, _ => default, Interval));
            Assert.Equal(new Vector2(1, 1), t.Pos);
        }
    }
}
