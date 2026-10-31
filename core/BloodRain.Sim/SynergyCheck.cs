// S-05 연환·진전 화력 상한 확인.
// 무공 6칸 조합(16개 중 6개, 8,008가지)을 모두 Lv8로 두고, 심법 6칸을 화력에 도움 되는 진전 쪽으로 가장 잘 채웠을 때
// 연환·진전이 보태는 보스 한 마리 초당 피해가 시너지 없을 때의 몇 배인지 잰다.
// 평균 빌드가 기획서 11장 화력 목표(100%)에 맞는다고 보고, 이 배수가 1.3(130%)을 넘으면 너무 센 조합이다.
// 시너지 효과는 synergies.json 에 글로만 있어서, 화력으로 바꾸는 식은 아래 Gain 에 하나씩 적었다(가정).
//   dotnet run --project core/BloodRain.Sim -- --synergy docs/design/sim/synergy.md
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using BloodRain.Core.Progression;

namespace BloodRain.Sim
{
    public static class SynergyCheck
    {
        public const double Limit = 1.3;

        /// <summary>글로 적힌 효과 중 화력이 큰 셋. 보고서의 '낮춰 본 안'에서만 바꾼다(데이터는 그대로).</summary>
        public sealed class Knobs
        {
            public double Ssanggeom = 0.6;          // 쌍검합벽: 삼재검법 피해 +60%
            public double? JeomhyeolCrit = null;     // 점혈: null 이면 '반드시 치명', 값이면 치명 확률 +값
            public double Gaebang3 = 0.3;           // 개방 3단계: 기혈 30% 아래 공력 +30%
        }
        static Knobs K = new Knobs();

        /// <summary>Eval 이 화력으로 바꾸는 연환. synergies.json links 에 새 연환이 생기면 둘 중 한쪽에 넣어야 한다(테스트가 본다).</summary>
        public static readonly string[] DamageLinks = { "ssanggeom", "geomhwa", "gwonjang", "amgi", "punghwa", "jeomhyeol", "noehwa", "geommyeong", "chwijung" };
        /// <summary>범위·제어·흡혈이라 보스 한 마리 화력엔 0으로 세는 연환.
        /// 인뢰검(원 안 감전)과 혈창(3초 출혈)은 보스에게도 조금 들어가지만 식을 정하지 않아 0이다. 그래서 이 보고서의 배수는 조금 낮게 나온 값이다.</summary>
        public static readonly string[] ZeroLinks = { "geumganghu", "dokbi", "inroe", "hyeoldok", "charyeok", "gyeonin", "hyeolchang" };
        /// <summary>Eval 이 이름으로 바로 찾는 무공.</summary>
        public static readonly string[] NamedWeapons = { "samjae_sword", "falling_petals", "nahan_fist", "palm_wind", "flash_finger", "flying_dagger", "pear_spear", "thunder_bomb", "thunder_talisman", "sonic_qin" };

        /// <summary>무공 하나의 Lv8 보스 상대 값.</summary>
        sealed class W
        {
            public string Id = "";
            public bool Sword;
            public double Dps, Rate, Hit, Dot, HitsPerSec;
        }

        public sealed class Row
        {
            public string[] Weapons = Array.Empty<string>();
            public Dictionary<string, int> Schools = new Dictionary<string, int>();
            public double Base, Gain, CondGain;
            public List<(string Name, double Dps)> Parts = new List<(string, double)>();
            public double Ratio => (Base + Gain) / Base;
            public double CondRatio => (Base + Gain + CondGain) / Base;
        }

        // 화력에 닿는 진전 단계(나머지는 생존·제어·자원이라 보스 화력에 0).
        static readonly string[] DamageSchools = { "huashan", "tang", "namgung", "gaebang", "kunlun" };

        public static List<Row> Run(SimData data, Knobs? knobs = null)
        {
            K = knobs ?? new Knobs();
            var g = data.Game;
            int max = g.Balance.Levels.WeaponMax;
            var ch = g.Character("jin_sowoon");
            var sheet = Stats.Compute(g, ch, new Dictionary<string, int>(), new Dictionary<string, int>());
            var b = new BuildStats
            {
                Sheet = sheet,
                Cdr = Math.Min(g.Balance.Stats.CooldownReductionCap, sheet.Bonus[StatKind.CooldownReduction] + g.Balance.Yinyang.Harmony.CooldownReduction),
                Band = YinYangBand.Harmony,
                Leaning = YinYang.Neutral,
                Crit = sheet.Bonus[StatKind.CritChance],
            };
            var ws = new Dictionary<string, W>();
            foreach (var def in g.Weapons)
            {
                var ex = data.Extras.TryGetValue(def.Id, out var x) ? x : null;
                var st = WeaponModel.Strikes(def, ex, max, false, false, b, max);
                double iv = WeaponModel.Interval(def, ex, max, false, b.Cdr, max);
                ws[def.Id] = new W
                {
                    Id = def.Id,
                    Sword = def.Category == "sword",
                    Dps = WeaponModel.SingleTargetDps(st, iv),
                    Rate = 1 / iv,
                    Hit = WeaponModel.HitDamage(def, max, false, false, b, max),
                    Dot = st.Sum(s => s.SingleDot) / iv,
                    HitsPerSec = st.Sum(s => s.SingleHits) / iv,
                };
            }
            double crit0 = b.Crit;
            var syn = g.Synergies;
            var ids = g.Weapons.Select(w => w.Id).ToArray();
            int slotsW = g.Balance.Slots.Weapons, slotsP = g.Balance.Slots.Passives;
            var weaponSet = new HashSet<string>(ids);

            var rows = new List<Row>();
            foreach (var combo in Combos(ids.Length, slotsW))
            {
                var pick = combo.Select(i => ws[ids[i]]).ToArray();
                var have = new HashSet<string>(pick.Select(p => p.Id));
                double baseDps = pick.Sum(p => p.Dps);
                var links = syn.Links.Where(l => have.Contains(l.A) && have.Contains(l.B)).ToList();

                // 진전: 무공으로 채운 수 + 심법으로 더 채울 수 있는 수.
                var fromW = new Dictionary<string, int>();
                var room = new Dictionary<string, int>();
                foreach (var s in syn.Schools)
                {
                    fromW[s.Id] = s.Members.Count(have.Contains);
                    room[s.Id] = s.Members.Count(m => !weaponSet.Contains(m));
                }
                bool common = have.Contains(syn.Common.Weapon);

                Row? best = null;
                // Eval 은 진전 수를 단계(Tier)로만 본다. 단계가 같은 배치는 화력도 같아서 먼저 나온 것만 셈한다(결과는 그대로, 열 배쯤 빠름).
                var seen = new HashSet<int>();
                foreach (var add in PassivePlans(syn, room, slotsP))
                {
                    var bonusChoices = common ? DamageSchools.Append("").ToArray() : new[] { "" };
                    foreach (var bonus in bonusChoices)
                    {
                        var counts = new Dictionary<string, int>();
                        foreach (var s in syn.Schools)
                            counts[s.Id] = fromW[s.Id] + (add.TryGetValue(s.Id, out var a) ? a : 0) + (s.Id == bonus ? 1 : 0);
                        int key = 0;
                        foreach (var id in DamageSchools) key = key * 16 + Tier(syn, counts, id);
                        if (!seen.Add(key)) continue;
                        var row = Eval(pick, have, links, counts, syn, ws, crit0);
                        row.Base = baseDps;
                        if (best == null || row.Gain > best.Gain || (row.Gain == best.Gain && row.CondGain > best.CondGain)) best = row;
                    }
                }
                best!.Weapons = pick.Select(p => p.Id).ToArray();
                rows.Add(best);
            }
            return rows;
        }

        // 문파별 단계 수(오름차순). Run 이 돌 때 한 번 채운다. Eval 안에서 수천만 번 불려서 LINQ 를 안 쓴다.
        static SynergyDefs? _tierSyn;
        static Dictionary<string, int[]> _tiers = new Dictionary<string, int[]>();

        static int Tier(SynergyDefs syn, Dictionary<string, int> counts, string school)
        {
            if (!ReferenceEquals(_tierSyn, syn))
            {
                _tiers = syn.Schools.ToDictionary(x => x.Id, x => x.Tiers.Select(t => t.Count).OrderBy(c => c).ToArray());
                _tierSyn = syn;
            }
            int n = counts[school], best = 0;
            foreach (var c in _tiers[school]) if (c <= n && c > best) best = c;
            return best;
        }

        static Row Eval(W[] pick, HashSet<string> have, List<LinkDef> links, Dictionary<string, int> counts,
            SynergyDefs syn, Dictionary<string, W> ws, double crit0)
        {
            var row = new Row { Schools = counts.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value) };
            void Add(string name, double v) { if (v > 0) { row.Gain += v; row.Parts.Add((name, v)); } }
            void Cond(string name, double v) { if (v > 0) { row.CondGain += v; row.Parts.Add((name + "(조건부)", v)); } }

            double baseDps = pick.Sum(p => p.Dps);
            double hits = pick.Sum(p => p.HitsPerSec);
            double meanHit = hits > 0 ? pick.Sum(p => p.Dps - p.Dot) / hits : 0;
            int hua = Tier(syn, counts, "huashan");
            double crit = crit0 + (hua >= 2 ? 0.05 : 0);

            foreach (var l in links)
            {
                W a = ws[l.A], bb = ws[l.B];
                switch (l.Id)
                {
                    case "ssanggeom": Add("쌍검합벽", K.Ssanggeom * ws["samjae_sword"].Dps); break; // 창천검기는 늘 돈다고 본다
                    case "geomhwa": Add("검화", ws["samjae_sword"].Rate * ws["falling_petals"].Hit); break; // 꽃잎 셋 중 하나가 보스에
                    case "gwonjang": Add("권장", ws["nahan_fist"].Rate * 0.5 * ws["palm_wind"].Hit); break;
                    case "amgi": Add("암기", ws["flash_finger"].Rate * crit * ws["flying_dagger"].Hit); break;
                    case "punghwa": Add("풍화", 0.3 * ws["palm_wind"].Dps); break; // 크기 30%와 화상을 합쳐 3할
                    case "jeomhyeol": Add("점혈", ws["pear_spear"].Dps * ((K.JeomhyeolCrit is double jc ? 1 + Math.Min(1, crit + jc) : 2) / (1 + crit) - 1)); break; // 늘 치명
                    case "noehwa": Add("뇌화", Math.Min(ws["thunder_bomb"].Rate, ws["thunder_talisman"].Rate) * ws["thunder_bomb"].Hit); break;
                    case "geommyeong": Add("검명", 0.5 * ws["sonic_qin"].Dps); break;
                    case "chwijung": Add("취중", 0.3 * ws["falling_petals"].Dps); break;
                    default: break; // 범위·제어·흡혈: 보스 한 마리 화력엔 0
                }
            }

            if (hua >= 2) Add("화산 2", baseDps * ((1 + crit) / (1 + crit0) - 1));
            if (hua >= 3) Add("화산 3", crit * hits * (have.Contains("falling_petals") ? ws["falling_petals"].Hit : 0.5 * meanHit));
            int tang = Tier(syn, counts, "tang");
            if (tang >= 2) Add("당가 2", 0.2 * pick.Sum(p => p.Dot));
            if (tang >= 5) Add("당가 5", ws["flying_dagger"].Hit / 20);
            int nam = Tier(syn, counts, "namgung");
            double swordDps = pick.Where(p => p.Sword).Sum(p => p.Dps);
            if (nam >= 2) Add("남궁 2", 0.12 * swordDps);
            if (nam >= 3)
            {
                var swords = pick.Where(p => p.Sword).ToArray();
                double sh = swords.Sum(p => p.HitsPerSec);
                if (sh > 0) Add("남궁 3", swords.Sum(p => p.Rate) / 3 * (swords.Sum(p => p.Dps - p.Dot) / sh));
            }
            int gae = Tier(syn, counts, "gaebang");
            if (gae >= 4) Add("개방 4", baseDps * 3 * (1.0 / 6) * (10.0 / 180)); // 제자 하나 = 내 화력의 1/6
            if (gae >= 3) Cond("개방 3", K.Gaebang3 * baseDps); // 기혈 30% 아래
            if (Tier(syn, counts, "kunlun") >= 3) Cond("곤륜 3", 0.25 * baseDps); // 강시 계열 상대
            return row;
        }

        /// <summary>화력 진전 다섯 문파에 심법을 몇 개씩 넣을지, 칸 수 안의 모든 경우.</summary>
        static IEnumerable<Dictionary<string, int>> PassivePlans(SynergyDefs syn, Dictionary<string, int> room, int slots)
        {
            var list = DamageSchools.Select(id => (id, room[id])).ToArray();
            var cur = new int[list.Length];
            IEnumerable<Dictionary<string, int>> Rec(int i, int left)
            {
                if (i == list.Length)
                {
                    var d = new Dictionary<string, int>();
                    for (int k = 0; k < list.Length; k++) if (cur[k] > 0) d[list[k].id] = cur[k];
                    yield return d;
                    yield break;
                }
                for (int n = 0; n <= Math.Min(list[i].Item2, left); n++)
                {
                    cur[i] = n;
                    foreach (var r in Rec(i + 1, left - n)) yield return r;
                }
                cur[i] = 0;
            }
            return Rec(0, slots);
        }

        static IEnumerable<int[]> Combos(int n, int k)
        {
            var c = Enumerable.Range(0, k).ToArray();
            while (true)
            {
                yield return (int[])c.Clone();
                int i = k - 1;
                while (i >= 0 && c[i] == n - k + i) i--;
                if (i < 0) yield break;
                c[i]++;
                for (int j = i + 1; j < k; j++) c[j] = c[j - 1] + 1;
            }
        }

        public static string Report(SimData data)
        {
            var rows = Run(data);
            var g = data.Game;
            string Name(string id) => g.Weapon(id).NameKo;
            string F(double x) => x.ToString("0.00", CultureInfo.InvariantCulture);
            var top = rows.OrderByDescending(r => r.Ratio).Take(10).ToList();
            var topC = rows.OrderByDescending(r => r.CondRatio).Take(5).ToList();
            int over = rows.Count(r => r.Ratio > Limit), overC = rows.Count(r => r.CondRatio > Limit);
            var sb = new StringBuilder();
            sb.AppendLine("# 연환·진전 화력 상한(S-05)");
            sb.AppendLine();
            sb.AppendLine("`dotnet run --project core/BloodRain.Sim -- --synergy docs/design/sim/synergy.md` 로 다시 쓴다. 손으로 고치지 않는다.");
            sb.AppendLine();
            sb.AppendLine("## 어떻게 쟀나");
            sb.AppendLine($"- 무공 {g.Balance.Slots.Weapons}칸 조합 {rows.Count:N0}가지를 모두 Lv{g.Balance.Levels.WeaponMax}(절기 전)로 두고, 보스 한 마리에게 들어가는 초당 피해를 웹 프로토타입 동작 식(WeaponModel)으로 계산했다. 프로토에 동작이 없는 무공 8종은 시뮬레이터의 대충 값이다.");
            sb.AppendLine($"- 심법 {g.Balance.Slots.Passives}칸은 화력 진전(화산·당가·남궁·개방·곤륜) 쪽으로 가장 세게 채운 경우를 골랐다. 심법 자체 능력치는 양쪽에 똑같이 들어간다고 보고 뺐다. 삼재검법이 있으면 '기본의 극' 한 칸을 가장 이로운 문파에 준다.");
            sb.AppendLine("- 배수 = (시너지 없는 화력 + 연환·진전이 보탠 화력) ÷ 시너지 없는 화력. 평균 빌드가 11장 화력 목표에 맞는다고 보면 이 배수가 곧 목표 대비 %다.");
            sb.AppendLine("- 연환·진전 효과는 글로만 적혀 있어 화력으로 바꾸는 식을 스스로 정했다(`SynergyCheck.cs` Eval). 범위·제어·흡혈·생존 효과는 보스 한 마리 화력에 0으로 셌다. 인뢰검(원 안 감전)과 혈창(3초 출혈)은 보스에게도 들어가는 피해지만 식을 정하지 않아 0으로 셌으니, 두 연환이 든 조합은 실제보다 조금 낮게 나온 값이다.");
            sb.AppendLine("- '조건부'는 늘 켜져 있지 않은 것: 개방 3단계(기혈 30% 아래 공력 +30%), 곤륜 3단계(강시 계열에 +25%).");
            sb.AppendLine();
            sb.AppendLine("## 결과");
            sb.AppendLine($"- 늘 켜진 효과만: 가장 센 조합 {F(top[0].Ratio)}배. {Limit}배를 넘는 조합 {over:N0}개.");
            sb.AppendLine($"- 조건부까지 다 켜면: 가장 센 조합 {F(topC[0].CondRatio)}배. {Limit}배를 넘는 조합 {overC:N0}개.");
            sb.AppendLine();
            sb.AppendLine("### 늘 켜진 효과 기준 상위 10");
            sb.AppendLine("| 배수 | 조건부 포함 | 무공 | 진전(개수) | 크게 보탠 것 |");
            sb.AppendLine("| --- | --- | --- | --- | --- |");
            foreach (var r in top) sb.AppendLine(Line(r));
            sb.AppendLine();
            sb.AppendLine("### 조건부 포함 상위 5");
            sb.AppendLine("| 배수 | 조건부 포함 | 무공 | 진전(개수) | 크게 보탠 것 |");
            sb.AppendLine("| --- | --- | --- | --- | --- |");
            foreach (var r in topC) sb.AppendLine(Line(r));
            sb.AppendLine();
            sb.AppendLine("### 시너지 하나가 보탠 화력(그 시너지가 켜진 조합 중 가장 큰 값, 그 조합 기본 화력 대비)");
            sb.AppendLine("| 시너지 | 최대 보탬 |");
            sb.AppendLine("| --- | --- |");
            var parts = new Dictionary<string, double>();
            foreach (var r in rows)
                foreach (var (n, v) in r.Parts)
                    parts[n] = Math.Max(parts.TryGetValue(n, out var p) ? p : 0, v / r.Base);
            foreach (var kv in parts.OrderByDescending(kv => kv.Value))
                sb.AppendLine($"| {kv.Key} | +{kv.Value * 100:0}% |");

            // 낮춰 본 안: 가장 큰 셋만 줄였을 때.
            var soft = new Knobs { Ssanggeom = 0.3, JeomhyeolCrit = 0.25, Gaebang3 = 0.15 };
            var rs = Run(data, soft);
            K = new Knobs();
            var bs = rs.OrderByDescending(r => r.Ratio).First();
            var bc = rs.OrderByDescending(r => r.CondRatio).First();
            sb.AppendLine();
            sb.AppendLine("## 낮춰 본 안(데이터는 안 바꿈, 결정 대기)");
            sb.AppendLine("쌍검합벽 +60% → +30%, 점혈 '반드시 치명' → '치명 확률 +25%', 개방 3단계 공력 +30% → +15% 로 바꿔 같은 계산을 다시 돌린 값이다.");
            sb.AppendLine($"- 늘 켜진 효과만: 가장 센 조합 {F(bs.Ratio)}배({string.Join("·", bs.Weapons.Select(Name))}). {Limit}배를 넘는 조합 {rs.Count(r => r.Ratio > Limit):N0}개.");
            sb.AppendLine($"- 조건부까지: 가장 센 조합 {F(bc.CondRatio)}배. {Limit}배를 넘는 조합 {rs.Count(r => r.CondRatio > Limit):N0}개.");
            return sb.ToString();

            string Line(Row r)
            {
                var sch = string.Join(", ", r.Schools.Where(kv => kv.Value >= 2).Select(kv => $"{g.Synergies.Schools.First(s => s.Id == kv.Key).NameKo} {kv.Value}"));
                var big = string.Join(", ", r.Parts.OrderByDescending(p => p.Dps).Take(3).Select(p => $"{p.Name} +{p.Dps / r.Base * 100:0}%"));
                return $"| {F(r.Ratio)} | {F(r.CondRatio)} | {string.Join("·", r.Weapons.Select(Name))} | {sch} | {big} |";
            }
        }
    }
}
