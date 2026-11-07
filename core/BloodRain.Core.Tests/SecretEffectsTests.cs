using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>비전을 찾은 뒤 얻는 것(synergies.json secrets[].effect·target·params, Build/SecretEffects.cs, 기획서 20장 비전 8 표).</summary>
    public class SecretEffectsTests
    {
        readonly GameData _d = TestData.Load();

        SecretEffects Of(params string[] ids) => SecretEffects.Of(_d, ids);
        SecretDef S(string id) => _d.Synergies.Secrets.First(s => s.Id == id);

        [Fact] public void 비전마다_아는_효과가_하나씩()
        {
            foreach (var s in _d.Synergies.Secrets)
                Assert.True(SecretEffects.Known.Contains(s.Effect), $"{s.Id}: 모르는 effect '{s.Effect}'");
            Assert.Equal(_d.Synergies.Secrets.Count, _d.Synergies.Secrets.Select(s => s.Effect).Distinct().Count());
            Assert.Equal(SecretEffects.Known.Count, _d.Synergies.Secrets.Count);
        }

        [Fact] public void target_은_있는_무공()
        {
            foreach (var s in _d.Synergies.Secrets.Where(s => s.Target != null))
                Assert.Contains(_d.Weapons, w => w.Id == s.Target);
            // 무명검 target 은 판정의 onlyWeapon 과 같은 무공
            Assert.Equal(S("mumyeong").Require.OnlyWeapon, S("mumyeong").Target);
        }

        /// <summary>params 값이 기획서 20장 '얻는 것' 칸의 숫자와 같다.</summary>
        [Theory]
        [InlineData("nakmae_geomseon", "ultimateBends", 3f, "세 번")]
        [InlineData("eogang", "seconds", 30f, "30초")]
        [InlineData("eogang", "maxAllies", 10f, "열 마리")]
        [InlineData("maehwa_samnong", "everyWaves", 3f, "세 번째")]
        [InlineData("mugeuk", "aspectDamageBonus", 0.4f, "+40%")]
        [InlineData("chwiseon", "mightPerFood", 0.03f, "+3%")]
        [InlineData("chwiseon", "evadeChance", 0.2f, "20%")]
        [InlineData("mumyeong", "damageBonus", 0.5f, "+50%")]
        [InlineData("mumyeong", "ultimateChargeMultiplier", 2f, "2배")]
        public void params_가_기획서_얻는_것과_같다(string id, string key, float value, string inGdd)
        {
            Assert.Equal(value, S(id).Param(key), 4);
            var gdd = TestData.Gdd;
            var row = gdd.Split('\n').Single(l => l.StartsWith($"| {S(id).NameKo}("));
            var got = row.Split('|')[4];
            Assert.Contains(inGdd, got);
        }

        [Fact] public void 무명검_삼재검법만_세지고_오의_두배()
        {
            var e = Of("mumyeong");
            Assert.Equal(1.5f, e.WeaponDamageMultiplier("samjae_sword"), 4);
            Assert.Equal(1f, e.WeaponDamageMultiplier("changcheon_qi"), 4);
            Assert.Equal(2f, e.UltimateChargeMultiplier, 4);
            Assert.Equal(1f, SecretEffects.None.UltimateChargeMultiplier, 4);
            Assert.Equal(1f, SecretEffects.None.WeaponDamageMultiplier("samjae_sword"), 4);
        }

        [Fact] public void 역천은_역류를_끈다()
        {
            Assert.True(Of("yeokcheon").NoBacklash);
            Assert.False(Of("mumyeong").NoBacklash);
        }

        [Fact] public void 무극은_음양_무공_모두에_보너스()
        {
            var e = Of("mugeuk");
            Assert.True(e.GaugeRemoved);
            Assert.Equal(0.4f, e.AspectDamageBonus(YinYang.Yin)!.Value, 4);
            Assert.Equal(0.4f, e.AspectDamageBonus(YinYang.Yang)!.Value, 4);
            Assert.Equal(0f, e.AspectDamageBonus(YinYang.Neutral)!.Value, 4);
            Assert.Null(SecretEffects.None.AspectDamageBonus(YinYang.Yin));
            // 무극 값은 극성 보너스와 같다(표의 +40%)
            Assert.Equal(_d.Balance.Yinyang.Extreme.AspectDamageBonus, e.AspectDamageBonus(YinYang.Yin)!.Value, 4);
        }

        [Fact] public void 취선은_음식마다_공력이_쌓이고_흘린다()
        {
            var e = Of("chwiseon");
            Assert.Equal(0f, e.FoodMightBonus(0), 4);
            Assert.Equal(0.15f, e.FoodMightBonus(5), 4);
            Assert.True(e.Evades(0.19));
            Assert.False(e.Evades(0.21));
            Assert.False(SecretEffects.None.Evades(0));
        }

        [Fact] public void 어강술은_열_마리까지()
        {
            var e = Of("eogang");
            Assert.Equal((30f, 10), e.Thrall!.Value);
            Assert.True(e.CanTakeThrall(9));
            Assert.False(e.CanTakeThrall(10));
            Assert.False(SecretEffects.None.CanTakeThrall(0));
        }

        [Fact] public void 매화삼롱은_음공_세_번째마다()
        {
            var e = Of("maehwa_samnong");
            Assert.False(e.IsPlumStormWave("sonic_qin", 2));
            Assert.True(e.IsPlumStormWave("sonic_qin", 3));
            Assert.True(e.IsPlumStormWave("sonic_qin", 6));
            Assert.False(e.IsPlumStormWave("falling_petals", 3));
            Assert.False(e.IsPlumStormWave("sonic_qin", 0));
        }

        [Fact] public void 낙매검선과_혈해마공()
        {
            var e = Of("nakmae_geomseon", "hyeolhae_janpyeon");
            Assert.True(e.IsPlumBranchSword("falling_petals"));
            Assert.Equal(3, e.UltimateBends);
            Assert.True(e.WhisperGuaranteed);
            Assert.Equal(0, SecretEffects.None.UltimateBends);
            Assert.False(SecretEffects.None.WhisperGuaranteed);
        }
    }
}
