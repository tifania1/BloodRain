// 음양 게이지가 한 판 동안 어디에 머무는지(밸런스 시뮬레이션). 보고서: docs/design/sim/yinyang.md
//
// RunSimulator(숭산 무리 모형)를 30분 돌리며 Core YinYangGauge·Backlash 를 그대로 쓴다.
//   게이지는 레벨업 카드(무공 ±5, 심법 ±8·±12)로만 움직인다. 캐릭터 시작값·태극 구간 넓이는 characters.json 그대로.
//   기혈역류는 경공으로 끊지 않는다고 본다(끊어도 다음 역류까지 60초는 그대로라 횟수는 같다).
//   빠진 것: 마교 몸의 혈공(심마로 음 쪽으로 끌림, 천마해체로 되돌림), 영약·태극 바닥·역근·태극환 같은 되돌림, 다시 고르기·파기.
//   정책은 게이지를 보지 않고 고른다(지금 정책 셋 모두 음양을 따지지 않는다).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BloodRain.Core.Data;
using BloodRain.Core.Progression;

namespace BloodRain.Sim
{
    public sealed class YinYangRow
    {
        public string Character = "", NameKo = "", Policy = "";
        public int Start, Runs;
        /// <summary>태극·편향·극성에 머문 시간 비율(판 평균).</summary>
        public double Harmony, Bias, Extreme;
        /// <summary>처음 태극을 벗어난 분·처음 극성에 든 분의 가운데값(든 판만). 든 판이 없으면 NaN.</summary>
        public double FirstOffMedian, FirstExtremeMedian;
        public int OffRuns, ExtremeRuns;
        public double BacklashMean;
        public int BacklashMax;
        /// <summary>분 → 게이지 가운데값.</summary>
        public Dictionary<int, double> GaugeMedian = new Dictionary<int, double>();
    }

    public static class YinYangSim
    {
        public const double Minutes = 30;
        public static readonly string[] PolicyIds = { "average", "greedy", "slow" };
        public static readonly int[] Marks = { 0, 5, 10, 15, 20, 25, 30 };

        public static YinYangRow Measure(SimData data, string character, string policy, int seeds, double minutes = Minutes)
        {
            var ch = data.Game.Character(character);
            var runs = Enumerable.Range(1, seeds)
                .Select(s => RunSimulator.Run(data, new SimOptions { Character = character, Policy = policy, Seed = s, Minutes = minutes }))
                .ToList();
            double total = minutes * 60;
            var row = new YinYangRow
            {
                Character = character, NameKo = ch.NameKo, Policy = policy, Start = ch.YinyangStart, Runs = seeds,
                Harmony = runs.Average(r => r.BandSeconds[0]) / total,
                Bias = runs.Average(r => r.BandSeconds[1]) / total,
                Extreme = runs.Average(r => r.BandSeconds[2]) / total,
                OffRuns = runs.Count(r => !double.IsNaN(r.FirstOffHarmonyMinute)),
                ExtremeRuns = runs.Count(r => !double.IsNaN(r.FirstExtremeMinute)),
                FirstOffMedian = Median(runs.Select(r => r.FirstOffHarmonyMinute).Where(x => !double.IsNaN(x))),
                FirstExtremeMedian = Median(runs.Select(r => r.FirstExtremeMinute).Where(x => !double.IsNaN(x))),
                BacklashMean = runs.Average(r => r.BacklashMinutes.Count),
                BacklashMax = runs.Max(r => r.BacklashMinutes.Count),
            };
            foreach (int m in Marks.Where(m => m <= minutes))
                row.GaugeMedian[m] = Median(runs.Select(r => (double)r.At(m).Gauge));
            return row;
        }

        public static double Median(IEnumerable<double> xs)
        {
            var a = xs.OrderBy(x => x).ToArray();
            if (a.Length == 0) return double.NaN;
            return a.Length % 2 == 1 ? a[a.Length / 2] : (a[a.Length / 2 - 1] + a[a.Length / 2]) / 2;
        }

        static string P(double x) => (x * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
        static string M(double x) => double.IsNaN(x) ? "-" : x.ToString("0.0", CultureInfo.InvariantCulture);
        static string G(double x) => double.IsNaN(x) ? "-" : x.ToString("+0;-0;0", CultureInfo.InvariantCulture);

        public static string Report(SimData data, int seeds = 10)
        {
            var g = data.Game;
            var yy = g.Balance.Yinyang;
            var rows = new List<YinYangRow>();
            foreach (var ch in g.Characters)
                foreach (var p in PolicyIds)
                    rows.Add(Measure(data, ch.Id, p, seeds));

            var sb = new StringBuilder();
            sb.AppendLine("# 음양 게이지 흐름 시뮬레이션");
            sb.AppendLine();
            sb.AppendLine("> 자동 생성: `dotnet run --project core/BloodRain.Sim -c Release -- --yinyang docs/design/sim/yinyang.md`. 손으로 고치지 않는다.");
            sb.AppendLine();
            sb.AppendLine($"숭산 무리 모형(`RunSimulator`)을 {Minutes:0}분 돌리며 Core `YinYangGauge`·`Backlash` 로 게이지가 어느 구간에 머무는지 잰다. " +
                          $"캐릭터 {g.Characters.Count}명 × 정책 {PolicyIds.Length}개 × 시드 1~{seeds}.");
            sb.AppendLine();
            sb.AppendLine($"규칙(balance.json `yinyang`): 무공 레벨당 ±{yy.WeaponPerLevel}, 심법 레벨당 ±{yy.PassivePerLevel}(센 심법은 데이터 값), " +
                          $"태극 ±{yy.HarmonyRange}(무당 운화는 characters.json 값), 극성 ±{yy.ExtremeFrom}부터, 극성에 {yy.Extreme.BacklashIntervalSeconds:0}초 머물 때마다 기혈역류.");
            sb.AppendLine();
            sb.AppendLine("빠진 것: 마교 몸의 혈공(심마), 영약·태극 바닥·역근·태극환 같은 되돌림, 다시 고르기·파기. 정책은 음양을 보지 않고 고른다. " +
                          "그래서 이 표는 '음양을 신경 쓰지 않는 사람'의 게이지다. 역류는 경공으로 끊지 않는다고 셌다(끊어도 다음 역류까지 60초는 같아서 횟수는 같다).");
            sb.AppendLine();

            sb.AppendLine("## 구간별 머문 시간");
            sb.AppendLine();
            sb.AppendLine("| 캐릭터 | 시작 | 정책 | 태극 | 편향 | 극성 | 처음 태극을 벗어난 분 | 처음 극성 분 | 역류(평균 / 최대) |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
            foreach (var r in rows)
                sb.AppendLine($"| {r.NameKo} | {G(r.Start)} | {r.Policy} | {P(r.Harmony)} | {P(r.Bias)} | {P(r.Extreme)} | " +
                              $"{M(r.FirstOffMedian)} ({r.OffRuns}/{r.Runs}판) | {M(r.FirstExtremeMedian)} ({r.ExtremeRuns}/{r.Runs}판) | " +
                              $"{r.BacklashMean.ToString("0.0", CultureInfo.InvariantCulture)} / {r.BacklashMax} |");
            sb.AppendLine();
            sb.AppendLine("처음 벗어난 분·극성 분은 그런 판만 모은 가운데값, 괄호는 그런 판 수.");
            sb.AppendLine();

            sb.AppendLine("## 게이지 가운데값(average 정책)");
            sb.AppendLine();
            sb.AppendLine("| 캐릭터 | " + string.Join(" | ", Marks.Select(m => m + "분")) + " |");
            sb.AppendLine("|---|" + string.Concat(Marks.Select(_ => "---|")));
            foreach (var r in rows.Where(r => r.Policy == "average"))
                sb.AppendLine($"| {r.NameKo} | " + string.Join(" | ", Marks.Select(m => G(r.GaugeMedian[m]))) + " |");
            sb.AppendLine();

            sb.AppendLine("## 읽은 것");
            sb.AppendLine();
            var all = rows;
            double harm = all.Average(r => r.Harmony), ext = all.Average(r => r.Extreme);
            sb.AppendLine($"- 모든 줄을 고르게 평균하면 태극 {P(harm)}, 편향 {P(all.Average(r => r.Bias))}, 극성 {P(ext)}. " +
                          $"한 판 역류는 평균 {all.Average(r => r.BacklashMean).ToString("0.0", CultureInfo.InvariantCulture)}번, 많은 판은 {all.Max(r => r.BacklashMax)}번.");
            var most = all.OrderByDescending(r => r.Extreme).First();
            var least = all.OrderBy(r => r.Extreme).First();
            sb.AppendLine($"- 극성에 가장 오래 머문 줄: {most.NameKo}({G(most.Start)}) {most.Policy} {P(most.Extreme)}. 가장 짧은 줄: {least.NameKo}({G(least.Start)}) {least.Policy} {P(least.Extreme)}.");
            var avg = all.Where(r => r.Policy == "average").ToList();
            var offs = avg.Where(r => !double.IsNaN(r.FirstOffMedian)).Select(r => r.FirstOffMedian).ToList();
            if (offs.Count > 0)
                sb.AppendLine($"- average 정책에서 처음 태극을 벗어나는 분(캐릭터별 가운데값)은 {M(offs.Min())}~{M(offs.Max())}분. " +
                              "기획서 4장 '서장 상세 흐름'은 7:30에 '음양 게이지가 처음 기울며 짧은 설명'을 둔다. " +
                              "다만 이 시뮬레이터는 레벨이 기획 목표보다 빠른 지금 처치 진기(P-02 미정, level-curve.md)로 돈다. 벗어나는 분은 레벨업 횟수를 따라가니 P-02 가 정해지면 늦어진다.");
            var pool = g.Weapons.Where(w => w.Unlock == "start").ToList();
            int yang = pool.Count(w => w.Aspect == YinYang.Yang), yin = pool.Count(w => w.Aspect == YinYang.Yin);
            int pYang = g.Passives.Count(p => p.Aspect == YinYang.Yang), pYin = g.Passives.Count(p => p.Aspect == YinYang.Yin);
            var lean = avg.Where(r => r.Start < 0 && r.GaugeMedian[(int)Minutes] > 0).Select(r => $"{r.NameKo}({G(r.Start)}→{G(r.GaugeMedian[(int)Minutes])})").ToList();
            sb.AppendLine($"- 처음부터 열린 무공 {pool.Count}종은 양 {yang}·음 {yin}·중 {pool.Count - yang - yin}, 심법 {g.Passives.Count}종은 양 {pYang}·음 {pYin}·중 {g.Passives.Count - pYang - pYin}. " +
                          "카드가 양 무공을 더 자주 내밀어 게이지가 대개 양 쪽으로 간다." +
                          (lean.Count > 0 ? $" 음에서 시작한 몸도 average 로는 {string.Join(", ", lean)}로 넘어간다." : ""));
            var still = avg.Where(r => r.GaugeMedian[20] == r.GaugeMedian[(int)Minutes]).Select(r => r.NameKo).ToList();
            if (still.Count > 0)
                sb.AppendLine($"- 20분부터는 칸이 차고 레벨이 다 올라 게이지가 거의 멈춘다(average 가운데값이 20분과 {Minutes:0}분에 같은 캐릭터 {still.Count}/{avg.Count}). 그때 극성에 있으면 판 끝까지 60초마다 역류가 온다. 멈추는 분도 레벨 속도(P-02)를 따라 움직인다.");
            sb.AppendLine("- 정책이 음양을 보지 않으니 극성 비율은 '모르고 해도 이만큼은 미친다'는 아래쪽 값이다. 일부러 한쪽으로 모으면 더 오래, 맞추려 들면 더 짧아진다.");
            return sb.ToString();
        }
    }
}
