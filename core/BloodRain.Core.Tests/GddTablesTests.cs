using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>기획서 표 가운데 지금까지 데이터와 대조하는 테스트가 없던 것(2026-11-01 주말 세션):
    /// 9장 심법 16종·절기 16종, 10장 연무장, 11장 경험치 곡선·적 능력치 증가.
    /// 값을 바꾸면 JSON 과 기획서 표를 같이 고쳐야 여기가 통과한다.</summary>
    public class GddTablesTests
    {
        readonly Data.GameData _d = TestData.Load();

        /// <summary>칸 안의 '+5%', '-2.5%', '초당 +0.1', '0.4%' 같은 수치. 부호도 %도 없는 숫자(Lv1·3·5, 2배)는 뺀다.
        /// %가 붙으면 100으로 나눈다. 부호는 버린다(쾌속은 데이터에 양수로 둔다).</summary>
        static List<float> Values(string cell)
        {
            var list = new List<float>();
            foreach (Match m in Regex.Matches(cell, @"[+-][\d,]+(?:\.\d+)?%?|\d+(?:\.\d+)?%"))
            {
                string s = m.Value.TrimStart('+', '-').Replace(",", "");
                bool pct = s.EndsWith("%");
                float v = float.Parse(pct ? s.TrimEnd('%') : s, CultureInfo.InvariantCulture);
                list.Add(pct ? v / 100f : v);
            }
            return list;
        }

        static readonly Dictionary<string, string> TrainingLabel = new Dictionary<string, string>
        {
            ["공력"] = "Might", ["기혈"] = "MaxHealth", ["호신"] = "Armor", ["회복"] = "Recovery",
            ["쾌속"] = "CooldownReduction", ["기세"] = "Area", ["탄속"] = "ProjectileSpeed", ["지속"] = "Duration",
            ["분광"] = "Amount", ["신법"] = "MoveSpeed", ["흡기"] = "Magnet", ["심득"] = "Growth", ["기연"] = "Luck",
            ["탐욕"] = "Greed", ["진기 충전"] = "UltimateCharge", ["경공 충전"] = "DashCharges", ["재기"] = "Revival",
            ["재고"] = "Rerolls", ["파기"] = "Banishes", ["정진"] = "Skips",
        };

        [Fact] public void 연무장_표가_balance_training_과_같다()
        {
            var sec = TestData.GddSection("연무장(영구 강화)");
            var rows = TestData.GddTable(sec);
            var items = _d.Balance.Training.Items;
            Assert.Equal(items.Count, rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                string label = Regex.Replace(r[0], @"\(.*\)$", "");
                Assert.True(TrainingLabel.TryGetValue(label, out var stat), $"모르는 연무장 항목 '{r[0]}'");
                var it = items[i];
                Assert.True(stat == it.Stat, $"{i + 1}번째 줄: 기획서 {r[0]}, 데이터 {it.Stat}");
                Assert.Equal(new[] { Math.Abs(it.PerLevel) }, Values(r[1]).ToArray(), new FloatEq());
                Assert.Equal(it.MaxLevel.ToString(CultureInfo.InvariantCulture), r[2]);
                Assert.Equal(it.BaseCost, int.Parse(r[3].Replace(",", ""), CultureInfo.InvariantCulture));
            }
            Assert.Contains($"(1 + {TestData.Num(_d.Balance.Training.CostGrowthPerBought)} x 지금까지 산 총 레벨 수)", sec);
        }

        [Fact] public void 심법_표가_passives_json_과_같다()
        {
            var rows = TestData.GddTable(TestData.GddSection("심법 16종"));
            Assert.Equal(_d.Passives.Count, rows.Count);
            Assert.Equal(16, rows.Count);
            foreach (var r in rows)
            {
                var p = _d.Passives.Single(x => x.NameKo == r[0]);
                string where = p.NameKo;

                // 음양: 중/양/음, 혈해심법처럼 '(레벨당 -12)' 가 붙으면 그 값이 덮어쓰기
                var yy = Regex.Match(r[3], @"^(중|양|음)(?:\(레벨당 [+-]?(\d+)\))?$");
                Assert.True(yy.Success, $"{where}: 음양 칸 '{r[3]}'");
                string aspect = yy.Groups[1].Value == "중" ? "neutral" : yy.Groups[1].Value == "양" ? "yang" : "yin";
                Assert.True(aspect == p.Yinyang, $"{where}: 음양 {r[3]} 대 {p.Yinyang}");
                if (yy.Groups[2].Success) Assert.Equal(int.Parse(yy.Groups[2].Value), Math.Abs(p.YinyangPerLevelOverride ?? 0));
                else Assert.Null(p.YinyangPerLevelOverride);

                // 짝 무공
                Assert.True(_d.Weapon(p.PairWeapon).NameKo == r[4], $"{where}: 짝 무공 {r[4]} 대 {p.PairWeapon}");

                // 레벨당 효과: 칸의 수치가 효과 순서대로. 특정 레벨에만 오르는 효과(천수공)는 그 값
                var per = p.Effects.Select(e => e.AtLevels != null ? e.AtLevels.Values.First() : Math.Abs(e.PerLevel)).ToArray();
                Assert.True(per.SequenceEqual(Values(r[1]).ToArray(), new FloatEq()),
                    $"{where}: 레벨당 '{r[1]}' 대 [{string.Join(", ", per)}]");

                // 최대(Lv5): 칸의 수치마다 어느 효과의 Lv5 합과 같다. 덧붙인 말(넉백 면역 등)은 maxBonus 에서
                var max = p.Effects.Select(e => e.AtLevels != null ? e.AtLevels.Values.Sum() : Math.Abs(e.PerLevel) * 5).ToArray();
                string maxCell = r[2];
                if (!string.IsNullOrEmpty(p.MaxBonus))
                {
                    Assert.True(maxCell.Contains(p.MaxBonus), $"{where}: 최대 칸에 '{p.MaxBonus}' 없음");
                    maxCell = maxCell.Replace(p.MaxBonus, "");
                }
                var shown = Values(maxCell);
                Assert.NotEmpty(shown);
                foreach (var v in shown)
                    Assert.True(max.Any(m => Math.Abs(m - v) < 1e-4), $"{where}: 최대 '{r[2]}' 의 {v} 가 어느 효과 Lv5 와도 다름");
            }
        }

        [Fact] public void 절기_표가_evolutions_json_과_같다()
        {
            var rows = TestData.GddTable(TestData.GddSection("절기 16종"));
            Assert.Equal(_d.Evolutions.Count, rows.Count);
            foreach (var r in rows)
            {
                var name = Regex.Match(r[0], @"^(.+)\((.+)\)$");
                Assert.True(name.Success, $"절기 이름 칸 '{r[0]}'");
                var e = _d.Evolutions.Single(x => x.NameKo.Replace(" ", "") == name.Groups[1].Value.Replace(" ", ""));
                Assert.Equal(e.Hanja, name.Groups[2].Value);
                var parts = r[1].Split('+').Select(s => s.Trim()).ToArray();
                Assert.Equal(2, parts.Length);
                Assert.True(_d.Weapon(e.Weapon).NameKo == parts[0], $"{e.NameKo}: 무공 {parts[0]} 대 {e.Weapon}");
                Assert.True(_d.Passives.Single(p => p.Id == e.Passive).NameKo == parts[1], $"{e.NameKo}: 심법 {parts[1]} 대 {e.Passive}");
                // 재료 심법의 짝 무공이 그 절기의 무공(한 쌍이 한 절기)
                Assert.Equal(e.Weapon, _d.Passives.Single(p => p.Id == e.Passive).PairWeapon);
            }
        }

        [Fact] public void 경험치_곡선_식이_balance_xpCurve_와_같다()
        {
            var sec = TestData.GddSection("경험치(진기) 곡선");
            var segs = _d.Balance.XpCurve.Segments;
            Assert.Equal(3, segs.Count);
            for (int i = 0; i < segs.Count; i++)
            {
                var s = segs[i];
                string range = i == 0 ? $"n \\le {s.ToLevel}"
                    : i == segs.Count - 1 ? $"n \\ge {s.FromLevel}"
                    : $"{s.FromLevel} \\le n \\le {s.ToLevel}";
                string expr = $"{s.Base} + {s.Step}(n-{s.Anchor}) & {range}";
                Assert.True(sec.Contains(expr), $"기획서 식에 '{expr}' 없음");
                if (i > 0) Assert.Equal(segs[i - 1].ToLevel + 1, s.FromLevel);
            }
        }

        static readonly Dictionary<string, string> ChapterHead = new Dictionary<string, string>
        {
            ["서장"] = "prologue", ["1장"] = "chapter1", ["2장"] = "chapter2", ["3장"] = "chapter3", ["4장"] = "chapter4",
            ["5장"] = "chapter5", ["6장"] = "chapter6", ["7장"] = "chapter7", ["종장"] = "final",
        };

        [Fact] public void 적_능력치_증가_식과_장_배율이_balance_와_같다()
        {
            var sec = TestData.GddSection("적 능력치 증가");
            var s = _d.Balance.EnemyScaling;
            Assert.Contains($"(1 + {TestData.Num(s.HealthPerMinute)} \\times \\text{{경과 분}})", sec);
            Assert.Contains($"(1 + {TestData.Num(s.DamagePerMinute)} \\times \\text{{경과 분}})", sec);

            var head = sec.Split('\n').First(l => l.StartsWith("| 장 |")).Split('|').Select(c => c.Trim()).Where(c => c.Length > 0).Skip(1).ToArray();
            var row = TestData.GddTable(sec).Single(r => r[0] == "장 배율");
            Assert.Equal(head.Length, row.Length - 1);
            Assert.Equal(s.ChapterMultiplier.Count, head.Length);
            for (int i = 0; i < head.Length; i++)
            {
                Assert.True(ChapterHead.TryGetValue(head[i], out var key), $"모르는 장 '{head[i]}'");
                Assert.True(TestData.Num(s.ChapterMultiplier[key]) == row[i + 1].Replace(".0", ""),
                    $"{head[i]}: 기획서 {row[i + 1]}, 데이터 {s.ChapterMultiplier[key]}");
            }
        }

        string RealmKo(string id) => _d.Balance.Realms.Single(r => r.Id == id).NameKo;

        [Fact] public void 레벨업_규칙_글이_balance_와_같다()
        {
            var sec = TestData.GddSection("레벨업 규칙");
            var b = _d.Balance;
            Assert.Contains($"레벨업마다 선택지 {b.LevelUp.BaseChoices}개, 기연이 높으면 {b.LevelUp.MaxChoices}개까지", sec);
            Assert.Contains($"공격 무공 {b.Slots.Weapons}칸, 심법 {b.Slots.Passives}칸. {RealmKo(b.Slots.ExtraSlotAtRealm)} 돌파 때 둘 중 한쪽 {b.Slots.Weapons + 1}칸째", sec);
            Assert.Equal(b.Slots.Weapons, b.Slots.Passives); // '한쪽 7칸째' 가 양쪽에 맞으려면
            Assert.Contains($"공격 무공은 Lv1~{b.Levels.WeaponMax}, 심법은 Lv1~{b.Levels.PassiveMax}", sec);
            Assert.Contains($"진기 게이지 {TestData.Num(b.UltimateCharge.PerSkip)}을 얻습니다", sec);
            int jin = b.LevelUp.TraitRerolls[_d.Character("jin_sowoon").Trait];
            Assert.Contains($"진소운은 다시 고르기를 {jin}회 더", sec);
        }

        [Fact] public void 절기_그릇_글이_balance_evolution_과_같다()
        {
            var sec = TestData.GddSection("절기 그릇과 각성");
            var ev = _d.Balance.Evolution;
            Assert.Contains($"공격 무공 Lv{_d.Balance.Levels.WeaponMax} + 짝 심법 Lv{ev.PairPassiveMinLevel} 이상", sec);
            // 표기 순서는 경지 순서(balance realms)
            var ordered = _d.Balance.Realms.Where(r => ev.Vessels.ContainsKey(r.Id)).ToList();
            Assert.Equal(ev.Vessels.Count, ordered.Count);
            string list = string.Join(", ", ordered.Select(r => $"{r.NameKo} {ev.Vessels[r.Id]}"));
            Assert.Contains(list + ".", sec);
            // 그릇은 경지마다 늘기만 한다
            for (int i = 1; i < ordered.Count; i++) Assert.True(ev.Vessels[ordered[i].Id] > ev.Vessels[ordered[i - 1].Id]);
            // '일류까지는 0' — 그릇 표에 없는 경지는 첫 그릇 경지보다 앞이다
            int first = _d.Balance.Realms.FindIndex(r => r.Id == ordered[0].Id);
            Assert.Equal("일류", _d.Balance.Realms[first - 1].NameKo);
            Assert.Contains("일류까지는 0", sec);
        }

        [Fact] public void 피격_글이_balance_contact_와_같다()
        {
            var sec = TestData.GddSection("피격과 사망");
            var c = _d.Balance.Contact;
            Assert.Contains($"같은 적에게는 {TestData.Num(c.SameEnemyIntervalSeconds)}초마다 한 번만", sec);
            Assert.Contains($"받는 피해 = max({TestData.Num(c.MinDamage)}, 적 피해 - 호신)", sec);
        }

        [Fact] public void 성능_상한_표가_balance_limits_와_같다()
        {
            var rows = TestData.GddTable(TestData.GddSection("성능 상한")).ToDictionary(r => r[0], r => r[1]);
            var l = _d.Balance.Limits;
            string N(int v) => v.ToString("#,0", CultureInfo.InvariantCulture);
            Assert.Equal($"{N(l.EnemiesPc)}(PC), {N(l.EnemiesMobile)}(모바일)", rows["동시 적 수"]);
            Assert.Equal(N(l.Projectiles), rows["동시 투사체"]);
            Assert.Equal($"{N(l.Orbs)}(넘으면 합침)", rows["화면 위 진기 구슬"]);
            Assert.Equal(N(l.DamageNumbers), rows["동시 피해 숫자 표시"]);
            Assert.Equal(4, rows.Count);
        }

        /// <summary>8장 능력치 표 '주로 올리는 곳' 칸: 적힌 심법이 그 능력치를 올리고, 연무장이 적혔으면 연무장 항목이 있고
        /// 안 적혔으면 없다. 영약은 데이터가 아직 없어 보지 않는다.</summary>
        [Fact] public void 능력치_표_올리는_곳이_심법_연무장과_같다()
        {
            var rows = TestData.GddTable(TestData.GddSection("능력치"));
            Assert.NotEmpty(rows);
            foreach (var r in rows)
            {
                Assert.True(TrainingLabel.TryGetValue(r[0], out var stat), $"모르는 능력치 '{r[0]}'");
                var where = r[3].Split(',').Select(s => Regex.Replace(s.Trim(), @"\(.*\)$", "")).ToList();
                bool training = _d.Balance.Training.Items.Any(i => i.Stat == stat);
                Assert.True(training == where.Contains("연무장"), $"{r[0]}: 기획서 '{r[3]}', 연무장 항목 {(training ? "있음" : "없음")}");
                foreach (var name in where.Where(w => w != "연무장" && w != "영약"))
                {
                    var p = _d.Passives.SingleOrDefault(x => x.NameKo == name);
                    Assert.True(p != null, $"{r[0]}: '{name}' 은 심법이 아님");
                    Assert.True(p!.Effects.Any(e => e.Stat == stat), $"{r[0]}: {name} 이 {stat} 를 안 올림");
                }
                // 이 능력치를 올리는 심법은 모두 칸에 적혀 있다
                foreach (var p in _d.Passives.Where(x => x.Effects.Any(e => e.Stat == stat)))
                    Assert.True(where.Contains(p.NameKo), $"{r[0]}: {p.NameKo} 가 칸에 없음");
            }
        }

        sealed class FloatEq : IEqualityComparer<float>
        {
            public bool Equals(float a, float b) => Math.Abs(a - b) < 1e-4;
            public int GetHashCode(float v) => 0;
        }
    }
}
