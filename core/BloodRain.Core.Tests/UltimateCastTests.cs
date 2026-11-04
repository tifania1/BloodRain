using System;
using System.Linq;
using System.Numerics;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Run;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>오의가 흘러가는 박자와 다섯 오의(천뢰낙·사자후·만천화우·태극무량·팔방풍우) 효과(R-15) ↔ 기획서 5장 '오의 상세'.</summary>
    public class UltimateCastTests
    {
        readonly GameData _d = TestData.Load();

        UltimateCast Cast(string id) => new UltimateCast(_d.Ultimates.Single(u => u.Id == id));

        string Effect(string id) =>
            TestData.GddTable(TestData.GddSection("오의 상세")).Single(r => r[0] == _d.Ultimates.Single(u => u.Id == id).NameKo)[1];

        Life NewLife(string sect)
        {
            var soul = new Soul(_d);
            soul.EndLife(soul.BeginLife(Rebirth.Prologue(_d)).Finish(LifeEndWay.Fallen, 0));
            var c = _d.Cheonha;
            var cand = new RebirthCandidate(c.Sect(sect), c.Region(c.Sect(sect).Region), Array.Empty<BirthTraitDef>());
            return new Life(soul, Rebirth.Body(c, cand), new Random(1));
        }

        [Fact] public void 때리는_오의는_쓰는_순간_한_번만_때린다()
        {
            foreach (var id in new[] { "il_geom", "cheonroe_rak", "saja_hu", "yeokryu", "palbang_pungu", "cheonseol_bu" })
            {
                var c = Cast(id);
                Assert.Equal(1, c.TotalPulses);
                Assert.Equal(1, c.Advance(0f));
                Assert.Equal(0, c.Advance(5f));
                Assert.False(c.IsActive);
            }
        }

        [Fact] public void 만천화우는_3초_동안_1초마다_세_번_때린다()
        {
            var c = Cast("mancheon_hwau");
            Assert.Contains($"{c.DurationSeconds}초 동안", Effect(c.Id));
            Assert.Contains("초당 공력 x", Effect(c.Id));
            Assert.Equal(3, c.TotalPulses);

            int total = 0;
            var times = new System.Collections.Generic.List<float>();
            for (int frame = 0; frame <= 300; frame++) // 1/60초씩 5초
            {
                int p = c.Advance(frame == 0 ? 0f : 1f / 60f);
                for (int i = 0; i < p; i++) times.Add(c.Elapsed);
                total += p;
            }
            Assert.Equal(3, total);
            Assert.Equal(0f, times[0], 3);
            Assert.Equal(1f, times[1], 1);
            Assert.Equal(2f, times[2], 1);
            Assert.False(c.IsActive);
            Assert.True(c.Poisons);
        }

        [Fact] public void 한_프레임이_길어도_박자를_잃지_않는다()
        {
            var c = Cast("mancheon_hwau");
            Assert.Equal(3, c.Advance(10f));
            Assert.Equal(0, c.Advance(1f));
        }

        [Fact] public void 태극무량은_5초_동안_받는_피해가_0이고_탄을_되돌린다()
        {
            var c = Cast(UltimateCast.TaijiMuryang);
            Assert.Contains("받는 피해 무효", Effect(c.Id));
            Assert.Equal(0, c.TotalPulses);
            Assert.Equal(0, c.Advance(0f));
            Assert.Equal(0f, c.DamageTakenMultiplier);
            Assert.True(c.ReflectsProjectiles);
            c.Advance(4.9f);
            Assert.Equal(0f, c.DamageTakenMultiplier);
            c.Advance(0.2f);
            Assert.Equal(1f, c.DamageTakenMultiplier);
            Assert.False(c.ReflectsProjectiles);
            Assert.False(c.IsActive);
        }

        [Fact] public void 다른_오의는_받는_피해를_바꾸지_않는다()
        {
            foreach (var u in _d.Ultimates.Where(u => u.Id != UltimateCast.TaijiMuryang))
            {
                var c = new UltimateCast(u);
                Assert.Equal(1f, c.DamageTakenMultiplier);
                Assert.False(c.ReflectsProjectiles);
            }
        }

        [Fact] public void 사자후는_반경_6m_에_축척을_곱해_3초_기절()
        {
            var c = Cast("saja_hu");
            Assert.Contains("반경 6m", Effect(c.Id));
            Assert.Contains("3초 기절", Effect(c.Id));
            Assert.Equal(3f, c.StunSeconds);
            Assert.True(c.InRadius(6f));
            Assert.False(c.InRadius(6.01f));
            Assert.True(c.InRadius(13f, 2.25f));
            Assert.False(Cast("palbang_pungu").InRadius(0f)); // 반경이 없는 오의
        }

        [Fact] public void 천뢰낙은_화면_안_12곳에_떨어지고_1초_감전()
        {
            var c = Cast("cheonroe_rak");
            Assert.Equal(1f, c.ShockSeconds);
            var min = new Vector2(-8f, -4.5f);
            var max = new Vector2(8f, 4.5f);
            var a = c.StrikePoints(min, max, new Random(7));
            var b = c.StrikePoints(min, max, new Random(7));
            Assert.Equal(12, a.Length);
            Assert.Equal(a, b); // 같은 시드면 같은 자리
            Assert.All(a, p => Assert.True(p.X >= min.X && p.X <= max.X && p.Y >= min.Y && p.Y <= max.Y));
            Assert.True(a.Select(p => p.X).Distinct().Count() > 1);
        }

        [Fact] public void 팔방풍우는_바라보는_쪽부터_45도마다_8방향_3연타()
        {
            var c = Cast("palbang_pungu");
            Assert.Equal(3, c.Volleys);
            var dirs = c.Directions(new Vector2(0f, 2f));
            Assert.Equal(8, dirs.Length);
            Assert.Equal(0f, dirs[0].X, 4);
            Assert.Equal(1f, dirs[0].Y, 4);
            for (int i = 0; i < 8; i++)
            {
                Assert.Equal(1f, dirs[i].Length(), 4);
                float dot = Vector2.Dot(dirs[i], dirs[(i + 1) % 8]);
                Assert.Equal(MathF.Cos(MathF.PI / 4f), dot, 4);
            }
            Assert.Equal(8, c.Directions(Vector2.Zero).Length); // 서 있으면 오른쪽부터
            Assert.Equal(1, Cast("saja_hu").Volleys);
        }

        [Fact] public void 숫자가_없는_효과는_0이다()
        {
            var c = Cast("il_geom");
            Assert.Equal(0f, c.StunSeconds);
            Assert.Equal(0f, c.ShockSeconds);
            Assert.False(c.Poisons);
            Assert.Equal(0f, c.DurationSeconds);
        }

        [Fact] public void Life_가_오의를_쓰면_다음_Tick_에_첫_박자를_알린다()
        {
            var life = NewLife("wudang"); // 태극무량
            life.Charge.AddFlat(100f);
            Assert.Null(life.Ultimate);
            Assert.True(life.UseUltimate());
            Assert.Equal(UltimateCast.TaijiMuryang, life.Ultimate!.Id);
            float before = life.DamageTakenMultiplier;
            Assert.Equal(0f, before);
            life.Tick(1f, Vector2.Zero, 0);
            Assert.Equal(0, life.UltimatePulses); // 태극무량은 때리지 않는다
            for (int i = 0; i < 5; i++) life.Tick(1f, Vector2.Zero, 0);
            Assert.True(life.DamageTakenMultiplier > 0f);
            life.Tick(1f, Vector2.Zero, 0);
            Assert.Null(life.Ultimate);
        }

        [Fact] public void Life_만천화우는_Tick_마다_박자를_센다()
        {
            var life = NewLife("tang"); // 만천화우
            life.Charge.AddFlat(100f);
            Assert.True(life.UseUltimate());
            int total = 0;
            for (int i = 0; i < 300; i++)
            {
                life.Tick(1f / 60f, Vector2.Zero, 0);
                total += life.UltimatePulses;
            }
            Assert.Equal(3, total);
        }
    }
}
