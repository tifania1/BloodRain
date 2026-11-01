using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>기획서 9장 '공격 무공 16종' 표의 분류·음양·동작 반경·Lv1 피해와 쿨다운·Lv8까지 성장·짝 심법 칸이
    /// weapons.json 과 같은지(해금 칸은 DataIntegrityTests), 그리고 성장 값을 읽는 WeaponGrowth.
    /// 성장 칸의 낱말을 모르면 테스트가 실패한다. 새 낱말을 쓰면 Growth 표에 같이 적는다.</summary>
    public class WeaponGrowthTests
    {
        readonly GameData _d = TestData.Load();

        static readonly Dictionary<string, string> Category = new Dictionary<string, string>
        {
            ["검법"] = "sword", ["권법"] = "fist", ["암기술"] = "hidden", ["장법"] = "palm", ["지법"] = "finger",
            ["독공"] = "poison", ["궤도형"] = "orbit", ["봉법"] = "staff", ["기문병기"] = "exotic", ["조법"] = "claw",
            ["음공"] = "sound", ["창법"] = "spear", ["도술"] = "talisman",
        };

        static readonly Dictionary<string, string> Aspect = new Dictionary<string, string> { ["양"] = "yang", ["음"] = "yin", ["중"] = "neutral" };

        /// <summary>성장 칸 'a→b' 앞 낱말 → 데이터 쌍.</summary>
        static readonly Dictionary<string, Func<WeaponDef, List<float>?>> Growth = new Dictionary<string, Func<WeaponDef, List<float>?>>
        {
            ["피해"] = w => new List<float> { w.DamageLv1, w.DamageLv8 },
            ["연타"] = w => w.Hits, ["휘두르기"] = w => w.Hits, ["찌르기"] = w => w.Hits,
            ["비도"] = w => w.Projectiles, ["폭탄"] = w => w.Projectiles, ["검"] = w => w.Projectiles, ["사슬"] = w => w.Projectiles,
            ["갈래"] = w => w.Projectiles, ["낙뢰"] = w => w.Projectiles, ["꽃잎"] = w => w.Projectiles,
            ["관통"] = w => w.Pierce, ["화상 초당"] = w => w.BurnPerSecond, ["둔화"] = w => w.Slow, ["지속"] = w => w.Duration,
            ["흡혈"] = w => w.Lifesteal, ["기절"] = w => w.StunChance,
            ["반경"] = w => w.RangeLv8.HasValue ? new List<float> { w.Range, w.RangeLv8.Value } : null,
        };

        /// <summary>숫자 없이 글로만 적힌 성장(데이터에 값이 없다). 새로 생기면 여기에 적는다.</summary>
        static readonly HashSet<string> WordsOnly = new HashSet<string> { "넉백 증가" };

        static float Num(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        List<(WeaponDef w, string[] r)> Rows()
        {
            var rows = TestData.GddTable(TestData.GddSection("공격 무공 16종"));
            Assert.Equal(_d.Weapons.Count, rows.Count);
            return rows.Select(r => (_d.Weapons.Single(x => x.NameKo == r[0]), r)).ToList();
        }

        [Fact] public void 분류_음양_짝_심법이_기획서와_같다()
        {
            foreach (var (w, r) in Rows())
            {
                Assert.True(Category.TryGetValue(r[1], out var cat), $"{w.NameKo}: 모르는 분류 '{r[1]}'");
                Assert.Equal(cat, w.Category);
                Assert.Equal(Aspect[r[2]], w.Yinyang);
                Assert.Equal(r[6], _d.Passive(w.PairPassive).NameKo);
            }
        }

        [Fact] public void 동작_칸의_반경이_데이터와_같다()
        {
            int n = 0;
            foreach (var (w, r) in Rows())
            {
                var m = Regex.Match(r[3], @"(\d+(?:\.\d+)?)m");
                if (!m.Success) continue;
                Assert.True(Num(m.Groups[1].Value) == w.Range, $"{w.NameKo}: 기획서 {m.Value}, 데이터 range {w.Range}");
                n++;
            }
            Assert.True(n >= 9, $"반경이 적힌 무공이 {n}개뿐");
        }

        /// <summary>'12 / 1.2초', '8x3 / 1.0초', '5 / 초당 4발', '초당 6 / 상시'.</summary>
        [Fact] public void Lv1_피해와_쿨다운이_데이터와_같다()
        {
            foreach (var (w, r) in Rows())
            {
                var m = Regex.Match(r[4], @"^(?:초당 )?(\d+(?:\.\d+)?)(?:x(\d+))? / (?:(\d+(?:\.\d+)?)초|초당 (\d+)발|상시)$");
                Assert.True(m.Success, $"{w.NameKo}: Lv1 칸 '{r[4]}'");
                Assert.Equal(Num(m.Groups[1].Value), w.DamageLv1);
                if (m.Groups[2].Success) Assert.Equal(Num(m.Groups[2].Value), WeaponGrowth.At(w.Hits, 1));
                if (m.Groups[3].Success) Assert.Equal(Num(m.Groups[3].Value), w.Cooldown);
                else if (m.Groups[4].Success) Assert.Equal(1f / Num(m.Groups[4].Value), WeaponGrowth.Cooldown(w, 1), 4);
                else Assert.Equal(0f, w.Cooldown);
            }
        }

        [Fact] public void Lv8까지_성장_칸이_데이터와_같다()
        {
            foreach (var (w, r) in Rows())
            {
                var milestones = new Dictionary<string, string>();
                bool area = false;
                foreach (var part in r[5].Split(", "))
                {
                    string where = $"{w.NameKo} '{part}'";
                    if (WordsOnly.Contains(part)) continue;
                    var lv = Regex.Match(part, @"^(.+)\(Lv(\d)\)$");
                    if (lv.Success) { milestones[lv.Groups[2].Value] = lv.Groups[1].Value; continue; }

                    var pct = Regex.Match(part, @"^(범위|크기|반경) \+(\d+)%$");
                    if (pct.Success)
                    {
                        Assert.True(w.AreaLv8.HasValue, $"{where}: 데이터에 areaLv8 이 없음");
                        Assert.Equal(1f + Num(pct.Groups[2].Value) / 100f, w.AreaLv8!.Value, 4);
                        area = true;
                        continue;
                    }

                    var g = Regex.Match(part, @"^(.+?) (\d+(?:\.\d+)?)→(\d+(?:\.\d+)?)(%|m|회|자루|발|개|초|곳)?$");
                    Assert.True(g.Success, $"{where}: 성장 칸을 못 읽음");
                    string label = g.Groups[1].Value, unit = g.Groups[4].Value;
                    float scale = unit == "%" ? 0.01f : 1f;
                    List<float>? pair;
                    if (label == "초당") pair = unit == "발" ? w.ShotsPerSecond : new List<float> { w.DamageLv1, w.DamageLv8 };
                    else
                    {
                        Assert.True(Growth.TryGetValue(label, out var get), $"{where}: 모르는 낱말 '{label}'");
                        pair = get!(w);
                    }
                    Assert.True(pair != null && pair.Count == 2, $"{where}: 데이터에 쌍이 없음");
                    Assert.Equal(Num(g.Groups[2].Value) * scale, pair![0], 4);
                    Assert.Equal(Num(g.Groups[3].Value) * scale, pair[1], 4);
                }
                if (!area) Assert.Null(w.AreaLv8);
                var data = w.Milestones ?? new Dictionary<string, string>();
                foreach (var kv in milestones)
                    Assert.True(data.TryGetValue(kv.Key, out var name) && name == kv.Value, $"{w.NameKo}: 기획서 Lv{kv.Key} '{kv.Value}' 가 milestones 에 없음");
            }
        }

        [Fact] public void 레벨_사이는_선형으로_잇고_개수는_반올림한다()
        {
            var dagger = _d.Weapon("flying_dagger");
            Assert.Equal(1, WeaponGrowth.Projectiles(dagger, 1));
            Assert.Equal(4, WeaponGrowth.Projectiles(dagger, 8));
            // 1 + 3 × 3/7 = 2.29 → 2, 1 + 3 × 4/7 = 2.71 → 3
            Assert.Equal(2, WeaponGrowth.Projectiles(dagger, 4));
            Assert.Equal(3, WeaponGrowth.Projectiles(dagger, 5));
            Assert.Equal(3, WeaponGrowth.Pierce(dagger, 8));
            Assert.Equal(4, WeaponGrowth.Projectiles(dagger, 12)); // Lv8 너머는 Lv8 값

            var staff = _d.Weapon("drunken_staff");
            Assert.Equal(1, WeaponGrowth.Hits(staff, 1));
            Assert.Equal(3, WeaponGrowth.Hits(staff, 8));
            Assert.Equal(1.3f, WeaponGrowth.Area(staff, 8), 4);
            Assert.Equal(1f + 0.3f * 3f / 7f, WeaponGrowth.Area(staff, 4), 4);

            var mist = _d.Weapon("poison_mist");
            Assert.Equal(2f, WeaponGrowth.Range(mist, 1));
            Assert.Equal(3.5f, WeaponGrowth.Range(mist, 8), 4);
            Assert.Equal(1f, WeaponGrowth.Area(mist, 8)); // 반경이 직접 자라는 무공은 배율이 없다

            var finger = _d.Weapon("flash_finger");
            Assert.Equal(0.25f, WeaponGrowth.Cooldown(finger, 1), 4);
            Assert.Equal(1f / 9f, WeaponGrowth.Cooldown(finger, 8), 4);

            var sword = _d.Weapon("samjae_sword");
            Assert.Equal(1.2f, WeaponGrowth.Cooldown(sword, 5));
            Assert.Equal(1, WeaponGrowth.Hits(sword, 8)); // 쌍이 없으면 한 번
            Assert.Equal(0, WeaponGrowth.Pierce(sword, 8));
            Assert.False(WeaponGrowth.HasMilestone(sword, 3, "앞뒤 베기"));
            Assert.True(WeaponGrowth.HasMilestone(sword, 4, "앞뒤 베기"));
            Assert.Throws<KeyNotFoundException>(() => WeaponGrowth.HasMilestone(sword, 8, "없는 동작"));
        }

        /// <summary>무공 동작별로 쓰는 성장 값이 데이터에 다 있는지(Unity WeaponRunner·웹 프로토타입이 읽는 것).</summary>
        [Fact] public void 동작이_쓰는_성장_값이_데이터에_있다()
        {
            var need = new Dictionary<string, Func<WeaponDef, bool>>
            {
                ["melee_combo"] = w => w.Hits != null,
                ["homing_dagger"] = w => w.Projectiles != null && w.Pierce != null,
                ["random_rapid"] = w => w.ShotsPerSecond != null,
                ["random_bomb"] = w => w.Projectiles != null && w.BurnPerSecond != null && w.AreaLv8 != null,
                ["aura"] = w => w.RangeLv8 != null && w.Slow != null,
                ["random_arc"] = w => w.Hits != null && w.AreaLv8 != null,
                ["slow_projectile"] = w => w.AreaLv8 != null,
                ["orbit_slash"] = w => w.AreaLv8 != null,
            };
            foreach (var w in _d.Weapons)
                if (need.TryGetValue(w.Behavior, out var ok)) Assert.True(ok(w), $"{w.Id}({w.Behavior}) 성장 값이 빠짐");
        }
    }
}
