// 돌파 깨달음 일곱이 한 판에서 얼마나 보이고, 고르면 얼마나 값이 나가는지(밸런스 시뮬레이션). 보고서: docs/design/sim/insights.md
//
// 후보: Core Insights.Roll 그대로. 노리는 깨달음이 보이면 바로 고르고, 안 보이면 나머지 가운데 아무거나 고른다고 본다.
// 돌파 시각: RunSimulator(숭산 무리 모형) 30분, 캐릭터 8명 × 시드. 깨달음을 고르는 경지는 balance.json 보상에 insight_choice 가 붙은 것.
// 값어치: Core 는 깨달음 효과를 아직 능력치로 바꾸지 않는다(Insights 는 고르기만). 그래서 판 흐름은 깨달음 없이 돌리고,
//   기록된 피해·처치 수에 효과를 얹어서 '이만큼 늘었을 것'을 셈한다. 고른 뒤 판이 빨라져서 생기는 되먹임은 빠진다.
//   일검파만: 시작 무공(첫 칸)이 넣은 피해 몫 × 40%. 오의는 시작 무공 피해를 쓰지만 무공이 아니라서 뺀다.
//   살기충천: 이류를 돌파한 뒤의 처치 수로 공력을 쌓는다(그전 처치는 안 센다). 공력은 모든 피해에 곱한다고 본다.
//   이기어검: 사거리 +50% 가 맞히는 수를 얼마나 늘릴지는 무공 모양에 달려서 근접 무공 몫만 잰다.
//   금종조·운기조식: 숫자로만(10초에 한 대, 가만히 선 1분 동안 회복량). 맞는 쪽은 contact.md.
//   천리추종·역근은 이 시뮬레이터에 자석 범위·음양 되돌림이 없어 빠진다.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BloodRain.Core.Build;
using BloodRain.Core.Data;

namespace BloodRain.Sim
{
    public sealed class InsightValueRow
    {
        public string Character = "", NameKo = "";
        public int Runs;
        /// <summary>경지 id → 돌파 분 가운데값(돌파한 판만). 돌파한 판이 없으면 NaN.</summary>
        public Dictionary<string, double> RealmMinute = new Dictionary<string, double>();
        /// <summary>30분 동안 깨달음을 고른 횟수 가운데값.</summary>
        public double Picks;
        /// <summary>시작 무공이 넣은 피해 몫(판 평균, 0~1).</summary>
        public double StartShare;
        /// <summary>근접 무공(이기어검 대상)이 넣은 피해 몫.</summary>
        public double MeleeShare;
        /// <summary>분 → 살기충천 공력(이류부터 쌓음, 가운데값).</summary>
        public Dictionary<int, double> SalgiAt = new Dictionary<int, double>();
        /// <summary>살기충천이 상한에 닿은 분 가운데값(닿은 판만). 없으면 NaN.</summary>
        public double SalgiCapMinute;
        public int SalgiCapRuns;
    }

    public static class InsightSim
    {
        public const double Minutes = 30;
        public static readonly int[] Marks = { 5, 10, 20, 30 };

        /// <summary>노리는 깨달음 하나가 돌파 n번 안에 한 번이라도 후보에 보일 확률. 보이면 고르고, 안 보이면 다른 것 하나를 고른다.</summary>
        public static double SeenWithin(Insights insights, string target, int choiceCount, int breakthroughs, int trials, int seed)
        {
            var rng = new Random(seed);
            int seen = 0;
            for (int t = 0; t < trials; t++)
            {
                var taken = new List<string>();
                for (int b = 0; b < breakthroughs; b++)
                {
                    var offer = insights.Roll(taken, choiceCount, rng);
                    if (offer.Count == 0) break;
                    if (offer.Any(i => i.Id == target)) { seen++; break; }
                    taken.Add(offer[rng.Next(offer.Count)].Id);
                }
            }
            return (double)seen / trials;
        }

        /// <summary>섞지 않고 바로 셈한 값(검산용): 깨달음 n개, 후보 c장, 돌파 breakthroughs 번.
        /// 노리는 것을 못 본 채 k번 돌파했으면 남은 풀은 n-k 개이고, 그 c장에 노리는 것이 안 들 확률은 C(n-k-1, c)/C(n-k, c) = (n-k-c)/(n-k).</summary>
        public static double SeenWithinExact(int n, int c, int breakthroughs)
        {
            double miss = 1;
            for (int k = 0; k < breakthroughs; k++)
            {
                int pool = n - k;
                if (pool <= c) return 1;
                miss *= (double)(pool - c) / pool;
            }
            return 1 - miss;
        }

        public static List<string> MeleeBehaviors(GameData g) =>
            g.Insights.FirstOrDefault(i => i.Effect == "melee_range")?.WeaponBehaviors ?? new List<string>();

        public static double SalgiMight(GameData g, int killsSinceTaken)
        {
            var d = g.Insights.First(i => i.Effect == "kill_stack_might");
            int per = (int)d.Param("killsPerStack");
            return Math.Min(d.Param("mightMax"), Math.Max(0, killsSinceTaken) / per * d.Param("mightPerStack"));
        }

        public static IReadOnlyList<RealmDef> InsightRealms(GameData g)
        {
            var ins = Insights.From(g);
            return g.Balance.Realms.Where(ins.OffersAt).ToList();
        }

        public static InsightValueRow Measure(SimData data, string character, int seeds, double minutes = Minutes)
        {
            var g = data.Game;
            var ch = g.Character(character);
            var melee = new HashSet<string>(MeleeBehaviors(g));
            var realms = InsightRealms(g);
            string first = realms.Count > 0 ? realms[0].Id : "";
            var runs = Enumerable.Range(1, seeds)
                .Select(s => RunSimulator.Run(data, new SimOptions { Character = character, Policy = "average", Seed = s, Minutes = minutes }))
                .ToList();
            var row = new InsightValueRow { Character = character, NameKo = ch.NameKo, Runs = seeds };
            foreach (var r in realms)
                row.RealmMinute[r.Id] = YinYangSim.Median(runs.Where(x => x.RealmMinute.ContainsKey(r.Id)).Select(x => x.RealmMinute[r.Id]));
            row.Picks = YinYangSim.Median(runs.Select(x => (double)realms.Count(r => x.RealmMinute.ContainsKey(r.Id))));
            double Share(SimResult x, Func<string, bool> pick)
            {
                double all = x.DamageBySource.Values.Sum();
                return all <= 0 ? 0 : x.DamageBySource.Where(kv => pick(kv.Key)).Sum(kv => kv.Value) / all;
            }
            bool IsWeapon(string id) => g.Weapons.Any(w => w.Id == id);
            row.StartShare = runs.Average(x => Share(x, id => id == ch.StartWeapon));
            row.MeleeShare = runs.Average(x => Share(x, id => IsWeapon(id) && melee.Contains(g.Weapon(id).Behavior)));

            double cap = g.Insights.First(i => i.Effect == "kill_stack_might").Param("mightMax");
            var capMinutes = new List<double>();
            var perRun = new List<Dictionary<int, double>>();
            foreach (var x in runs)
            {
                var at = new Dictionary<int, double>();
                if (!x.RealmMinute.TryGetValue(first, out var takenMin)) { foreach (int m in Marks) at[m] = 0; perRun.Add(at); continue; }
                // 분 단위 기록이라 돌파한 분의 시작 처치 수를 기준으로 삼는다
                int baseKills = x.At((int)Math.Floor(takenMin)).Kills;
                foreach (int m in Marks.Where(m => m <= minutes)) at[m] = SalgiMight(g, x.At(m).Kills - baseKills);
                var hit = x.Minutes.FirstOrDefault(s => s.Minute >= takenMin && SalgiMight(g, s.Kills - baseKills) >= cap - 1e-6);
                if (hit != null) capMinutes.Add(hit.Minute);
                perRun.Add(at);
            }
            foreach (int m in Marks.Where(m => m <= minutes)) row.SalgiAt[m] = YinYangSim.Median(perRun.Select(p => p[m]));
            row.SalgiCapRuns = capMinutes.Count;
            row.SalgiCapMinute = YinYangSim.Median(capMinutes);
            return row;
        }

        static string P(double x) => double.IsNaN(x) ? "-" : (x * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
        static string M(double x) => double.IsNaN(x) ? "-" : x.ToString("0.0", CultureInfo.InvariantCulture);

        public static string Report(SimData data, int seeds = 5)
        {
            var g = data.Game;
            var ins = Insights.From(g);
            var realms = InsightRealms(g);
            int n = g.Insights.Count;
            int baseC = g.InsightChoices.Base;
            var counts = new List<int> { baseC };
            counts.AddRange(g.InsightChoices.ByTrait.Values.Where(v => v != baseC).Distinct().OrderBy(v => v));
            var sb = new StringBuilder();
            sb.AppendLine("# 돌파 깨달음: 얼마나 보이고 얼마나 값이 나가나");
            sb.AppendLine();
            sb.AppendLine("`dotnet run --project core/BloodRain.Sim -c Release -- --insights docs/design/sim/insights.md` 로 다시 쓴다. 코드 `core/BloodRain.Sim/InsightSim.cs`.");
            sb.AppendLine($"후보 추첨은 Core `Insights.Roll` 그대로, 돌파 시각과 피해·처치 기록은 RunSimulator(숭산 무리 모형, 평균 정책) 30분 × 캐릭터 {g.Characters.Count}명 × 시드 {seeds}.");
            sb.AppendLine("Core 는 아직 깨달음 효과를 능력치로 바꾸지 않아서 판은 깨달음 없이 돌리고, 기록에 효과를 얹어 셈했다. 고른 뒤 판이 빨라지는 되먹임은 빠졌다.");
            sb.AppendLine();

            sb.AppendLine("## 1. 노리는 깨달음이 보일 확률");
            sb.AppendLine();
            sb.AppendLine($"깨달음 {n}개, 고르는 경지 {realms.Count}곳({string.Join("·", realms.Select(r => r.NameKo))}). 노리는 것이 보이면 고르고 안 보이면 다른 것을 고른다고 보고, 그 돌파까지 한 번이라도 보일 확률(시행 20,000번, 바로 셈한 값과 1%p 안).");
            sb.AppendLine();
            sb.Append("| 후보 수 |");
            for (int k = 1; k <= realms.Count; k++) sb.Append($" {realms[k - 1].NameKo}까지 |");
            sb.AppendLine();
            sb.Append("|---|");
            for (int k = 1; k <= realms.Count; k++) sb.Append("---|");
            sb.AppendLine();
            string target = g.Insights[0].Id;
            foreach (int c in counts)
            {
                sb.Append($"| {c}장{(c != baseC ? "(진소운)" : "")} |");
                for (int k = 1; k <= realms.Count; k++) sb.Append($" {P(SeenWithin(ins, target, c, k, 20000, 7))} |");
                sb.AppendLine();
            }
            sb.AppendLine();

            var rows = g.Characters.Select(ch => Measure(data, ch.Id, seeds)).ToList();
            sb.AppendLine("## 2. 깨달음을 고르는 시각");
            sb.AppendLine();
            sb.AppendLine("돌파 분 가운데값. '-' 는 30분 안에 못 오른 것.");
            sb.AppendLine();
            sb.AppendLine("| 캐릭터 | " + string.Join(" | ", realms.Select(r => r.NameKo)) + " | 30분 동안 고른 수 |");
            sb.AppendLine("|---|" + string.Concat(realms.Select(_ => "---|")) + "---|");
            foreach (var r in rows)
                sb.AppendLine($"| {r.NameKo} | " + string.Join(" | ", realms.Select(x => M(r.RealmMinute[x.Id]))) + $" | {M(r.Picks)} |");
            sb.AppendLine();

            var ilgeom = g.Insights.First(i => i.Effect == "first_weapon_damage").Param("damageBonus");
            double cap = g.Insights.First(i => i.Effect == "kill_stack_might").Param("mightMax");
            sb.AppendLine("## 3. 공격 깨달음 값어치");
            sb.AppendLine();
            sb.AppendLine($"일검파만 = 시작 무공 피해 몫 × {ilgeom * 100:0}%(판 전체 피해에 대해). 살기충천 = 이류를 돌파한 뒤 처치 수로 쌓은 공력(상한 {cap * 100:0}%). 이기어검은 근접 무공 몫만(사거리 +50% 가 맞히는 수를 얼마나 늘리는지는 무공 모양에 달렸다).");
            sb.AppendLine();
            sb.AppendLine("| 캐릭터 | 시작 무공 몫 | 일검파만 | 살기충천 5분 | 10분 | 20분 | 30분 | 상한 닿는 분 | 근접 무공 몫 |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
            foreach (var r in rows)
                sb.AppendLine($"| {r.NameKo} | {P(r.StartShare)} | +{P(r.StartShare * ilgeom)} | +{P(r.SalgiAt[5])} | +{P(r.SalgiAt[10])} | +{P(r.SalgiAt[20])} | +{P(r.SalgiAt[30])} | {M(r.SalgiCapMinute)}({r.SalgiCapRuns}/{r.Runs}) | {P(r.MeleeShare)} |");
            sb.AppendLine();

            var block = g.Insights.First(i => i.Effect == "periodic_block");
            var still = g.Insights.First(i => i.Effect == "still_regen");
            double interval = g.Balance.Contact.SameEnemyIntervalSeconds;
            double hits10 = ContactSim.SurroundCount / interval * block.Param("intervalSeconds");
            double regenMin = (60 - still.Param("stillSeconds")) * still.Param("maxHealthPerSecond");
            sb.AppendLine("## 4. 지키는 깨달음(숫자만)");
            sb.AppendLine();
            sb.AppendLine($"- 금종조: {block.Param("intervalSeconds"):0}초에 {block.Param("blocks"):0}대 무효. 둘러싸여 {ContactSim.SurroundCount}마리가 {interval:0.##}초마다 치면 그 사이 {hits10:0}대를 맞으니 {P(block.Param("blocks") / hits10)}만 막는다. 한두 마리에 가끔 긁히는 때에만 값이 있다.");
            sb.AppendLine($"- 운기조식: {still.Param("stillSeconds"):0}초 멈춘 뒤 초당 기혈 {still.Param("maxHealthPerSecond") * 100:0}%. 1분 내내 서 있으면 {P(regenMin)}. 회복 기믹(gimmicks.md, 모닥불 안 1분 60%)보다 크지만 서 있는 동안 적이 닿으면 의미가 없다.");
            sb.AppendLine();

            var startShares = rows.Select(r => r.StartShare * ilgeom).OrderBy(x => x).ToList();
            var capMin = rows.Select(r => r.SalgiCapMinute).Where(x => !double.IsNaN(x)).OrderBy(x => x).ToList();
            string Range(IReadOnlyList<double> xs, Func<double, string> f) => xs.Count == 0 ? "-" : f(xs[0]) == f(xs[xs.Count - 1]) ? f(xs[0]) : f(xs[0]) + "~" + f(xs[xs.Count - 1]);
            sb.AppendLine("## 읽는 법");
            sb.AppendLine();
            sb.AppendLine($"- 일검파만은 캐릭터마다 +{Range(startShares, x => P(x).Replace("%", "")) }%. 시작 무공이 판 끝까지 주력인 몸일수록 크다. 레벨업으로 무공이 여섯 칸까지 차면 첫 칸 몫이 줄어든다.");
            sb.AppendLine($"- 살기충천은 이류에서 고르면 {Range(capMin, M)}분에 상한 +{P(cap)}에 닿는다(처치 {(int)(cap / g.Insights.First(i => i.Effect == "kill_stack_might").Param("mightPerStack")) * (int)g.Insights.First(i => i.Effect == "kill_stack_might").Param("killsPerStack"):N0}마리). 모든 피해에 붙으니 10분부터는 모든 캐릭터에서 일검파만보다 크다(5분에는 시작 무공 몫이 큰 {string.Join("·", rows.Where(r => r.StartShare * ilgeom > r.SalgiAt[5]).Select(r => r.NameKo))}만 일검파만이 앞선다). 처치 수는 숭산 무리 모형 것이라 무리 가정이 바뀌면 상한 닿는 분도 바뀐다.");
            sb.AppendLine("- 공격 깨달음 둘의 격차가 의도인지는 사람이 볼 일이다. 손잡이는 살기충천 처치 수(100)·상한(30%), 일검파만 배율(40%).");
            sb.AppendLine("- 빠진 것: 천리추종(자석 범위)과 역근(음양 되돌림)은 이 시뮬레이터에 없다. 역근이 끊어 줄 역류 횟수는 yinyang.md 를 본다.");
            return sb.ToString();
        }
    }
}
