using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>진전 단계의 effect + params(synergies.json, Build/SynergyEffects.cs).</summary>
    public class SynergyEffectsTests
    {
        readonly GameData _d = TestData.Load();

        WeaponDef W(string id) => _d.Weapons.First(w => w.Id == id);
        EnemyDef E(string id) => _d.Enemies.First(e => e.Id == id);
        SynergyEffects Of(params (string, int)[] items) => SynergyEffects.Of(Synergy.Compute(_d, TestData.Build("jin_sowoon", items)));

        [Fact] public void 데이터의_effect_는_모두_코드가_읽는_이름이고_한_번씩만_쓴다()
        {
            var used = _d.Synergies.Schools.SelectMany(s => s.Tiers).Where(t => t.Effect != null).Select(t => t.Effect!).ToList();
            Assert.Equal(used.Count, used.Distinct().Count());
            Assert.All(used, e => Assert.Contains(e, SynergyEffects.Known));
            Assert.Equal(SynergyEffects.Known.OrderBy(x => x), used.OrderBy(x => x));
        }

        [Theory]
        [InlineData("sword_damage", "damageBonus", "+{0}%", 100)]
        [InlineData("low_health_might", "damageBonus", "+{0}%", 100)]
        [InlineData("low_health_might", "healthThreshold", "{0}%", 100)]
        [InlineData("jiangshi_damage", "damageBonus", "+{0}%", 100)]
        [InlineData("dash_drag", "distanceBonus", "+{0}%", 100)]
        [InlineData("harmony_deflect", "damageCut", "-{0}%", 100)]
        public void 숫자는_설명과_같다(string effect, string key, string format, int scale)
        {
            var t = _d.Synergies.Schools.SelectMany(s => s.Tiers).Single(x => x.Effect == effect);
            Assert.Contains(string.Format(format, (int)System.Math.Round(t.Param(key) * scale)), t.Desc);
        }

        [Fact] public void 모양_효과의_숫자도_설명과_같다()
        {
            SchoolTierDef T(string e) => _d.Synergies.Schools.SelectMany(s => s.Tiers).Single(x => x.Effect == e);
            Assert.Equal(3f, T("crit_petals").Param("petals"));
            Assert.Contains("세 장", T("crit_petals").Desc);
            Assert.Equal(3f, T("sky_sword").Param("everyAttacks"));
            Assert.Contains("세 번째", T("sky_sword").Desc);
            Assert.Contains($"{T("poison_spread").Param("radius"):0}m", T("poison_spread").Desc);
            Assert.Contains($"{T("needle_rain").Param("intervalSeconds"):0}초마다", T("needle_rain").Desc);
            foreach (var k in new[] { "damage", "speed", "radius", "lifeSeconds" }) Assert.True(T("crit_petals").Param(k) > 0);
            foreach (var k in new[] { "damageMultiplier", "radius", "delaySeconds", "searchRange" }) Assert.True(T("sky_sword").Param(k) > 0);
            foreach (var k in new[] { "seconds", "damagePerSecond" }) Assert.True(T("poison_spread").Param(k) > 0);
            foreach (var k in new[] { "damage", "poisonSeconds" }) Assert.True(T("needle_rain").Param(k) > 0);
            foreach (var k in new[] { "dragRadius", "dragFactor" }) Assert.True(T("dash_drag").Param(k) > 0);
        }

        [Fact] public void 켜진_단계만_Tier_로_params_를_준다()
        {
            var off = Of(("samjae_sword", 1), ("falling_petals", 1));
            Assert.Null(off.Tier("crit_petals"));
            var hs = _d.Synergies.Schools.First(s => s.Id == "huashan");
            var items = new[] { ("samjae_sword", 1) }.Concat(hs.Members.Select(m => (m, 1))).ToArray();
            var on = Of(items);
            Assert.Equal(3f, on.Tier("crit_petals")!.Param("petals"));
        }

        [Fact] public void 남궁_둘이면_검만_12퍼센트()
        {
            var e = Of(("samjae_sword", 1), ("changcheon_qi", 1), ("noejeong", 1));
            Assert.Equal(1.12f, e.WeaponDamageMultiplier(W("changcheon_qi"), 1f), 4);
            Assert.Equal(1f, e.WeaponDamageMultiplier(W("palm_wind"), 1f));
        }

        [Fact] public void 개방_셋이면_기혈_30퍼센트_아래에서만_공력_30퍼센트()
        {
            var e = Of(("samjae_sword", 1), ("drunken_staff", 1), ("juseon", 1), ("palm_wind", 1));
            Assert.Equal(1f, e.WeaponDamageMultiplier(W("palm_wind"), 0.3f));
            Assert.Equal(1.3f, e.WeaponDamageMultiplier(W("palm_wind"), 0.29f), 4);
        }

        [Fact] public void 곤륜_셋이면_강시에게만_25퍼센트_정신부는_두_배()
        {
            var e = Of(("samjae_sword", 1), ("thunder_talisman", 1), ("domun", 1), ("hwaryong", 1));
            Assert.Equal(1.25f, e.EnemyDamageMultiplier(E("sigang")), 4);
            var other = _d.Enemies.First(x => x.Family != "jiangshi");
            Assert.Equal(1f, e.EnemyDamageMultiplier(other));
            Assert.Equal(2f, e.FreezeMultiplier);
        }

        [Fact] public void 마도_둘이면_흡혈_두_배_음식_절반()
        {
            var e = Of(("samjae_sword", 1), ("blood_claw", 1), ("hyeolhae", 1));
            Assert.Equal(2f, e.LifestealMultiplier);
            Assert.Equal(0.5f, e.FoodHealMultiplier);
        }

        [Fact] public void 사량발천근은_태극_구간에서만_20퍼센트를_줄이고_열_배를_되돌린다()
        {
            var e = Of(("samjae_sword", 1), ("taiji_sword", 1), ("yangui", 1), ("unryong", 1));
            var (taken, reflect) = e.Deflect(10f, true);
            Assert.Equal(8f, taken, 4);
            Assert.Equal(20f, reflect, 4);
            Assert.Equal((10f, 0f), e.Deflect(10f, false));
        }

        [Fact] public void 아무_단계도_없으면_모두_1()
        {
            var e = SynergyEffects.None;
            Assert.Equal(1f, e.WeaponDamageMultiplier(W("samjae_sword"), 0.1f));
            Assert.Equal(1f, e.EnemyDamageMultiplier(E("sigang")));
            Assert.Equal(1f, e.DashDistanceMultiplier);
            Assert.Equal(1f, e.FreezeMultiplier);
            Assert.Equal((5f, 0f), e.Deflect(5f, true));
        }
    }
}
