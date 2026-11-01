using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>evolutions.json params(웹 프로토타입 절기 수치)가 기획서 9장 '절기 16종' 효과 칸의 숫자와 같은지.
    /// 기획서와 다른 곳은 아래 '다른 곳' 테스트에 적어 두고 backlog E-02 에서 사람이 정한다. 어느 쪽이든 맞추면 그 줄을 지운다.</summary>
    public class EvolutionParamsTests
    {
        readonly GameData _d = TestData.Load();

        EvolutionDef Of(string weapon) => _d.Evolutions.Single(e => e.Weapon == weapon);

        Dictionary<string, string> Effects() => TestData.GddTable(TestData.GddSection("절기 16종"))
            .ToDictionary(r => System.Text.RegularExpressions.Regex.Replace(r[0], @"\(.*\)", "").Replace(" ", ""), r => r[2]);

        string Effect(string weapon) => Effects()[Of(weapon).NameKo.Replace(" ", "")];

        [Fact] public void 프로토타입이_그리는_절기_아홉은_params_가_있다()
        {
            var protoBehaviors = new[] { "front_arc", "melee_combo", "homing_dagger", "orbit_slash", "slow_projectile", "random_rapid", "random_bomb", "aura", "random_arc" };
            foreach (var w in _d.Weapons.Where(w => protoBehaviors.Contains(w.Behavior)))
                Assert.True(Of(w.Id).Params != null, $"{Of(w.Id).NameKo} params 가 없음");
        }

        [Fact] public void 효과_칸의_숫자가_params_와_같다()
        {
            Assert.Contains($"{Of("samjae_sword").Param("waveEvery")}번째 공격마다 관통 검기", Effect("samjae_sword"));
            Assert.Contains("3방향 동시", Effect("nahan_fist"));
            Assert.Equal(3, Of("nahan_fist").Param("projectiles"));
            Assert.Contains($"적 사이를 {Of("flying_dagger").Param("bounces")}번 튕김", Effect("flying_dagger"));
            Assert.Contains($"검기 원 {Of("changcheon_qi").Param("sweeps")}겹", Effect("changcheon_qi"));
            // 섬광지 Lv8 초당 발 수 × 절기 배율 = 기획서 '초당 20발'(9 x 2.2 = 19.8)
            var finger = _d.Weapon("flash_finger");
            Assert.Equal(20, (int)System.Math.Round(finger.ShotsPerSecond![1] * Of("flash_finger").Param("fireRateMultiplier")));
            Assert.Contains("초당 20발", Effect("flash_finger"));
            Assert.Contains($"폭발이 {Of("thunder_bomb").Param("chain")}번 연쇄", Effect("thunder_bomb"));
            Assert.Contains($"안에서 {Of("poison_mist").Param("executeSeconds")}초를 버틴 일반 적 즉사", Effect("poison_mist"));
            var staff = Of("drunken_staff");
            Assert.Contains($"봉 분신 {staff.Param("directions")}개", Effect("drunken_staff"));
            Assert.Contains($"{staff.Param("stunSeconds")}초 기절", Effect("drunken_staff"));
        }

        /// <summary>9장 '절기로 바뀐 뒤 날아가는 것' 표 = params. 프로토타입·시뮬레이터가 이 값을 읽는다.</summary>
        [Fact] public void 절기로_바뀐_뒤_날아가는_것_표가_params_와_같다()
        {
            var rows = TestData.GddTable(TestData.GddSection("절기로 바뀐 뒤 날아가는 것")).ToDictionary(r => r[0], r => r);
            string Num(float v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
            void Row(string weapon, string speed, string radius, string life, string damage)
            {
                var r = rows[Of(weapon).NameKo];
                Assert.Equal(speed, r[2]); Assert.Equal(radius, r[3]); Assert.Equal(life, r[4]); Assert.Equal(damage, r[5]);
            }
            var w = Of("samjae_sword");
            Row("samjae_sword", $"{Num(w.Param("waveSpeed"))}m", $"{Num(w.Param("waveRadius"))}m", $"{Num(w.Param("waveLifeSeconds"))}초", $"{Num(w.Param("waveDamage"))}배");
            Assert.Contains($"{Num(w.Param("waveEvery"))}번째 공격마다", rows[w.NameKo][6]);
            var f = Of("nahan_fist");
            Row("nahan_fist", $"{Num(f.Param("projectileSpeed"))}m", $"{Num(f.Param("projectileRadius"))}m", $"{Num(f.Param("projectileLifeSeconds"))}초", $"{Num(f.Param("projectileDamage"))}배");
            Assert.Contains($"갈래 사이 {Num(f.Param("projectileSpread"))}라디안", rows[f.NameKo][6]);
            Assert.Contains($"{Num(Of("flying_dagger").Param("bounceRange"))}m 안", rows[Of("flying_dagger").NameKo][6]);
            foreach (var id in new[] { "palm_wind", "flash_finger" })
                Row(id, "-", $"{Num(Of(id).Param("explodeRadius"))}m", "-", $"{Num(Of(id).Param("explodeDamage"))}배");
            Assert.Contains($"{Num(Of("flash_finger").Param("explodeChance") * 100)}% 확률", rows[Of("flash_finger").NameKo][6]);
            var b = Of("thunder_bomb");
            Row("thunder_bomb", "-", $"{Num(b.Param("chainShrink"))}배씩", "-", $"{Num(b.Param("chainDamage"))}배씩");
            Assert.Contains($"{Num(b.Param("chainDelaySeconds"))}초 뒤", rows[b.NameKo][6]);
        }

        [Fact] public void 프로토타입이_절기_숫자를_박아_두지_않는다()
        {
            var html = TestData.ReadRepoFile("web", "prototype", "template.html");
            foreach (var old in new[] { "vx: fx * 13", "[-0.5, 0, 0.5]", "Math.sin(a) * 11, r: 0.6", "{ r: 1.1, life: 0.25 }", "pr.dmg * 0.5",
                                        "nearestEnemies(pr.x, pr.y, 1, 6,", "delay: 0.2, apply: () => bomb", "R * 0.9, dmg * 0.8" })
                Assert.DoesNotContain(old, html);
            foreach (var used in new[] { "ep.waveSpeed", "ep.projectileSpread", "pr.bounceRange", "X.explodeRadius", "ep.chainShrink" })
                Assert.Contains(used, html);
        }

        /// <summary>기획서와 프로토타입이 다른 곳(backlog E-02). 한쪽을 고치면 여기가 실패하니 그 줄을 지우고 E-02 를 닫는다.</summary>
        [Fact] public void 기획서와_다른_곳()
        {
            Assert.Contains("독안개 반경 3배", Effect("poison_mist"));
            Assert.Equal(2f, Of("poison_mist").Param("rangeMultiplier"));                 // 프로토타입·시뮬레이터 2배
            Assert.Contains("5초짜리 불바다", Effect("thunder_bomb"));
            Assert.Throws<KeyNotFoundException>(() => Of("thunder_bomb").Param("fireSeconds")); // 불바다 없음
            Assert.Contains("낙뢰 연쇄", Effect("changcheon_qi"));
            Assert.Throws<KeyNotFoundException>(() => Of("changcheon_qi").Param("lightning")); // 두 바퀴 + 반경 1.3배뿐, 낙뢰 없음
            Assert.Contains("치명타 시 작은 폭발", Effect("flash_finger"));
            Assert.Equal(0.25f, Of("flash_finger").Param("explodeChance"));               // 치명타가 아니라 25% 확률
            // (5) 프로토타입은 모든 절기 피해 2배인데 기획서 효과 칸은 천지인 삼검에만 '피해 2배'
            Assert.Equal(2f, _d.Balance.Evolution.DamageMultiplier);
            Assert.Contains("피해 2배", Effect("samjae_sword"));
            Assert.DoesNotContain("피해 2배", Effect("nahan_fist"));
            Assert.Contains("if (w.evolved) d *= B.evolution.damageMultiplier", TestData.ReadRepoFile("web", "prototype", "template.html"));
        }
    }
}
