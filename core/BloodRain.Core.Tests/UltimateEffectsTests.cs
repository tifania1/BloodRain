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
    /// <summary>천설부·천마해체 효과(R4-10) ↔ 기획서 5장 '오의 상세'·마교 혈공 문단.</summary>
    public class UltimateEffectsTests
    {
        readonly GameData _d = TestData.Load();

        UltimateDef U(string id) => _d.Ultimates.Single(u => u.Id == id);

        string Effect(string nameKo) =>
            TestData.GddTable(TestData.GddSection("오의 상세")).Single(r => r[0] == nameKo)[1];

        Life NewLife(string sect)
        {
            var soul = new Soul(_d);
            soul.EndLife(soul.BeginLife(Rebirth.Prologue(_d)).Finish(LifeEndWay.Fallen, 0));
            var c = _d.Cheonha;
            var cand = new RebirthCandidate(c.Sect(sect), c.Region(c.Sect(sect).Region), Array.Empty<BirthTraitDef>());
            return new Life(soul, Rebirth.Body(c, cand), new Random(1));
        }

        [Fact] public void 천설부는_모든_적을_3초_얼리고_강시_계열에만_피해가_들어간다()
        {
            var u = U(UltimateEffects.CheonseolBu);
            Assert.Contains("모든 적을 3초 얼림", Effect(u.NameKo));
            Assert.Contains($"강시 계열에는 공력 x{u.MightMultiplier}", Effect(u.NameKo));
            Assert.Equal(3f, UltimateEffects.FreezeSeconds(u));

            var jiangshi = _d.Enemies.First(e => e.Family == UltimateEffects.JiangshiFamily);
            var other = _d.Enemies.First(e => e.Family != UltimateEffects.JiangshiFamily);
            Assert.Equal(10f * 8f * 1.25f, UltimateEffects.HitDamageOn(u, jiangshi.Family, 10f, 1.25f), 3);
            Assert.Equal(0f, UltimateEffects.HitDamageOn(u, other.Family, 10f, 1.25f));
        }

        [Fact] public void 다른_오의는_얼리지_않고_계열을_가리지_않는다()
        {
            foreach (var u in _d.Ultimates.Where(u => u.Id != UltimateEffects.CheonseolBu))
            {
                Assert.Equal(0f, UltimateEffects.FreezeSeconds(u));
                Assert.True(UltimateEffects.Damages(u, "bandit"));
                Assert.True(UltimateEffects.Damages(u, UltimateEffects.JiangshiFamily));
            }
        }

        [Fact] public void 천마해체는_8초_동안_피해_2배_흡혈_10퍼센트_초당_기혈_2퍼센트()
        {
            var d = new DemonDissolution(U(UltimateEffects.CheonmaHaeche));
            Assert.False(d.IsActive);
            Assert.Equal(1f, d.DamageMultiplier);
            Assert.Equal(0f, d.LifestealBonus);
            Assert.Equal(0f, d.HealthCostPerSecond(90f));

            d.Start();
            Assert.Equal(8f, d.SecondsLeft);
            Assert.Equal(2f, d.DamageMultiplier);
            Assert.Equal(0.1f, d.LifestealBonus, 5);
            Assert.Equal(1.8f, d.HealthCostPerSecond(90f), 4);

            d.Tick(7.9f);
            Assert.True(d.IsActive);
            d.Tick(0.2f);
            Assert.False(d.IsActive);
            Assert.Equal(0f, d.SecondsLeft);
            Assert.Equal(1f, d.DamageMultiplier);
        }

        [Fact] public void 천마해체를_또_쓰면_8초로_되돌리고_겹치지_않는다()
        {
            var d = new DemonDissolution(U(UltimateEffects.CheonmaHaeche));
            d.Start();
            d.Tick(5f);
            d.Start();
            d.Start(); // 현경 오의 2연속
            Assert.Equal(8f, d.SecondsLeft);
        }

        [Fact] public void 흡혈에는_마교_회복_절반과_마도_흡혈_두_배가_곱해진다()
        {
            var d = new DemonDissolution(U(UltimateEffects.CheonmaHaeche));
            d.Start();
            float healing = _d.Cheonha.Sect("demonic").TraitEffect!.HealingMultiplier;
            Assert.Equal(0.5f, healing);
            // 피해 100, 흡혈 10%, 마도 진전 2배, 마교 회복 절반 → 10
            Assert.Equal(10f, UltimateEffects.LifestealHeal(100f, d.LifestealBonus, 2f, healing), 4);
            Assert.Equal(0f, UltimateEffects.LifestealHeal(100f, 0f));
            Assert.Equal(0f, UltimateEffects.LifestealHeal(0f, 0.1f));
        }

        [Fact] public void 마교_몸은_오의를_쓰면_천마해체가_켜지고_세상_시간으로_꺼진다()
        {
            var life = NewLife("demonic");
            Assert.NotNull(life.Dissolution);
            Assert.False(life.Dissolution!.IsActive);
            life.Charge.AddFlat(100);
            Assert.True(life.UseUltimate());
            Assert.True(life.Dissolution.IsActive);
            for (int i = 0; i < 81; i++) life.Tick(0.1f, life.Position, 0);
            Assert.False(life.Dissolution.IsActive);
        }

        [Fact] public void 다른_몸은_천마해체가_없다()
        {
            Assert.Null(NewLife("kunlun").Dissolution);
            Assert.Null(NewLife("shaolin").Dissolution);
        }
    
        [Fact] public void 낙매일검_길은_세_번_꺾이고_길이는_곧은_일검과_같다()
        {
            var path = UltimateEffects.PlumBranchPath(0f, 0f, 0f, 20f, 3);
            Assert.Equal(5, path.Length);
            float total = 0f;
            for (int i = 0; i + 1 < path.Length; i++)
            {
                float dx = path[i + 1].x - path[i].x, dy = path[i + 1].y - path[i].y;
                total += (float)System.Math.Sqrt(dx * dx + dy * dy);
                // +30, -30 번갈아
                float deg = (float)(System.Math.Atan2(dy, dx) * 180 / System.Math.PI);
                Assert.Equal(i % 2 == 0 ? UltimateEffects.PlumBendDegrees : -UltimateEffects.PlumBendDegrees, deg, 3);
            }
            Assert.Equal(20f, total, 3);
            Assert.Equal(0f, path[4].y, 3);   // 짝수 토막이면 바라보는 줄로 돌아온다
            // 꺾지 않으면 곧은 한 줄
            var straight = UltimateEffects.PlumBranchPath(1f, 2f, 0f, 10f, 0);
            Assert.Equal(2, straight.Length);
            Assert.Equal(11f, straight[1].x, 3);
        }

        [Fact] public void 낙매일검_맞기는_토막_어디든()
        {
            var path = UltimateEffects.PlumBranchPath(0f, 0f, 0f, 20f, 3);
            // 첫 토막 가운데(꺾인 위쪽)
            var mid = ((path[0].x + path[1].x) / 2, (path[0].y + path[1].y) / 2);
            Assert.True(UltimateEffects.PlumBranchHits(path, mid.Item1, mid.Item2, 0.3f, 0.9f));
            // 곧은 일검 줄 위지만 꺾인 길에서 먼 곳은 안 맞음
            Assert.False(UltimateEffects.PlumBranchHits(path, 5f, -3f, 0.3f, 0.9f));
            // 뒤쪽은 안 맞음
            Assert.False(UltimateEffects.PlumBranchHits(path, -3f, 0f, 0.3f, 0.9f));
        }

        [Fact] public void 낙매검선의_피_꺾임_수는_데이터에서()
        {
            var d = TestData.Load();
            var fx = BloodRain.Core.Build.SecretEffects.Of(d, new[] { "nakmae_geomseon" });
            Assert.Equal(3, fx.UltimateBends);
        }
    }
}
