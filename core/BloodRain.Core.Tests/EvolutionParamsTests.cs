using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>evolutions.json params(웹 프로토타입 절기 수치)가 기획서 9장 '절기 16종' 효과 칸의 숫자와 같은지.
    /// (E-02 로 기획서와 다른 곳은 2026-11-01 모두 맞춤)</summary>
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
            Assert.Contains($"독안개 반경 {Of("poison_mist").Param("rangeMultiplier")}배", Effect("poison_mist"));   // E-02: 기획서를 프로토타입 2배로
            var staff = Of("drunken_staff");
            Assert.Contains($"봉 분신 {staff.Param("directions")}개", Effect("drunken_staff"));
            Assert.Contains($"{staff.Param("stunSeconds")}초 기절", Effect("drunken_staff"));
            // R-09: 프로토타입에 아직 없는 절기 다섯도 효과 칸의 숫자만은 params 로
            string Pct(float v) => System.Math.Round(v * 100).ToString(System.Globalization.CultureInfo.InvariantCulture);
            string Num(float v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var taiji = Of("taiji_sword");
            Assert.Contains($"흑백 검 {taiji.Param("swords")}자루", Effect("taiji_sword"));
            Assert.Contains($"태극 구간이면 피해 +{Pct(taiji.Param("taijiDamageBonus"))}%", Effect("taiji_sword"));
            var claw = Of("blood_claw");
            Assert.Contains($"흡혈 {Pct(claw.Param("lifesteal"))}%", Effect("blood_claw"));
            Assert.Contains($"극음이면 범위 {Num(claw.Param("extremeYinRangeMultiplier"))}배", Effect("blood_claw"));
            var qin = Of("sonic_qin");
            Assert.Contains($"최대 {Num(qin.Param("stackDamageMax"))}배", Effect("sonic_qin"));
            Assert.Contains($"기절 {Pct(qin.Param("stunChance"))}%", Effect("sonic_qin"));
            Assert.Contains($"상시 {Of("pear_spear").Param("directions")}방향 찌르기", Effect("pear_spear"));
            Assert.Contains($"기혈 {Num(Of("falling_petals").Param("healPerHit"))} 회복", Effect("falling_petals"));
        }

        /// <summary>흑룡탄(R-17, 2026-11-05 사람이 추천대로): 모으는 반경 = 쇄겸술 사거리, 0.6초, 폭발 2.5m·한 방의 3배.</summary>
        [Fact] public void 흑룡탄은_쇄겸술_사거리_안을_모아_터진다()
        {
            var e = Of("chain_sickle");
            string Num(float v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(_d.Weapon("chain_sickle").Range, e.Param("gatherRadius"));
            Assert.Contains($"주변 {Num(e.Param("gatherRadius"))}m 안 적을 {Num(e.Param("gatherSeconds"))}초 동안", Effect("chain_sickle"));
            Assert.Contains($"반경 {Num(e.Param("explosionRadius"))}m 폭발(한 방의 {Num(e.Param("damageMultiplier"))}배)", Effect("chain_sickle"));
            var row = TestData.GddTable(TestData.GddSection("절기로 바뀐 뒤 날아가는 것")).Single(r => r[0] == "흑룡탄");
            Assert.Equal($"{Num(e.Param("explosionRadius"))}m", row[3]);
            Assert.Equal($"{Num(e.Param("damageMultiplier"))}배", row[5]);
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
            Assert.Equal(1f, Of("flash_finger").Param("explodeOnCrit"));
            Assert.Contains("치명타로 맞을 때만", rows[Of("flash_finger").NameKo][6]);
            var b = Of("thunder_bomb");
            Row("thunder_bomb", "-", $"{Num(b.Param("chainShrink"))}배씩", "-", $"{Num(b.Param("chainDamage"))}배씩");
            Assert.Contains($"{Num(b.Param("chainDelaySeconds"))}초 뒤", rows[b.NameKo][6]);
            var fire = rows[b.NameKo + " 불바다"];
            Assert.Equal($"마지막 폭발의 {Num(b.Param("fireRadiusMultiplier"))}배", fire[3]);
            Assert.Equal($"{Num(b.Param("fireSeconds"))}초", fire[4]);
            var c = Of("changcheon_qi");
            var bolt = rows[c.NameKo + " 낙뢰"];
            Assert.Equal($"{Num(c.Param("lightningRange"))}m 안 다음 적", bolt[3]);
            Assert.Equal($"{Num(c.Param("lightningDamage"))}배, 옮을 때마다 {Num(c.Param("lightningFalloff"))}배", bolt[5]);
            Assert.Contains($"{Num(c.Param("lightningChance") * 100)}% 확률, 곁의 적으로 {Num(c.Param("lightningChains"))}번", bolt[6]);
            var t = Of("thunder_talisman");
            var five = rows[t.NameKo];
            Assert.Equal($"뇌부 낙뢰의 {Num(t.Param("radiusMultiplier"))}배, {Num(t.Param("lightningRange"))}m 안 다음 적", five[3]);
            Assert.Equal($"1배, 옮을 때마다 {Num(t.Param("lightningFalloff"))}배", five[5]);
            Assert.Contains($"사거리 {Num(t.Param("rangeMultiplier"))}배 안 서로 다른 적 {Num(t.Param("strikes"))}곳 동시", five[6]);
            Assert.Contains($"곁의 적으로 {Num(t.Param("lightningChains"))}번", five[6]);
            Assert.Contains($"화면 {Num(t.Param("strikes"))}곳 동시 대낙뢰", Effect("thunder_talisman"));
        }

        /// <summary>E-02: 기획서 효과 칸의 불바다·낙뢰 연쇄·치명타 폭발을 프로토타입이 그린다.</summary>
        [Fact] public void 기획서_효과를_프로토타입이_그린다()
        {
            Assert.Contains("5초짜리 불바다", Effect("thunder_bomb"));
            Assert.Contains("낙뢰 연쇄", Effect("changcheon_qi"));
            Assert.Contains("치명타 시 작은 폭발", Effect("flash_finger"));
            var html = TestData.ReadRepoFile("web", "prototype", "template.html");
            foreach (var used in new[] { "ep.fireSeconds", "lp.lightningChance", "lp.lightningChains", "pr.explode.explodeOnCrit" })
                Assert.Contains(used, html);
            Assert.DoesNotContain("explodeChance", html);
        }

        /// <summary>모든 절기 피해 2배는 기획서 9장 '절기 16종' 머리말과 balance.json 이 같다.</summary>
        [Fact] public void 절기_피해_배율이_기획서와_같다()
        {
            Assert.Contains($"한 방 피해가 모두 {_d.Balance.Evolution.DamageMultiplier}배", TestData.GddSection("절기 16종"));
            Assert.Contains("if (w.evolved) d *= B.evolution.damageMultiplier", TestData.ReadRepoFile("web", "prototype", "template.html"));
            Assert.DoesNotContain("피해 2배", Effect("samjae_sword"));
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
    }
}
