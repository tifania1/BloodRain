// 한 생에 모이는 은자(밸런스 시뮬레이션, RM-06). 보고서: docs/design/sim/silver.md
//
// RunSimulator(숭산 무리 모형)의 분당 누적 처치에 은자 규칙을 얹는다. 은자는 몇 번이고 기댓값으로 센다(시드 가운데값 × 기댓값).
//   잡몹: 처치마다 balance.json drops.enemySilverChance 로 은자 주머니(drops.silverBag 범위 가운데).
//   등롱: 웹 프로토타입처럼 33초(22 × 1.5)마다 하나 생긴다고 보고 모두 부순다. drops.lantern 의 은자 주머니 몫.
//   금두꺼비: chapters.json life 의 toad 창(10~12분) 안에 한 번, enemies.json golden_toad reward.silver. 잡는다고 본다.
//   빠진 것: 은자 카드(무명검, 카드가 모두 새 무공일 때만), 탐욕(연무장·성질), 혈월 1.5배, 보부상에서 쓴 은자.
// 처치 수는 무리 모형의 한 마리 단위다. 천하 판의 덩이(여러 사람이 한 몸)에서 주머니가 덩이마다인지 사람마다인지는 정하지 않았다.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BloodRain.Core.Data;

namespace BloodRain.Sim
{
    public static class SilverSim
    {
        public const double Minutes = 20;
        public const double LanternEverySeconds = 22 * 1.5;
        static readonly int[] Marks = { 5, 10, 12, 15, 20 };

        static string N(double v, string f = "#,0") => v.ToString(f, CultureInfo.InvariantCulture);

        public sealed class Row
        {
            public string NameKo = "";
            public Dictionary<int, double> Kills = new Dictionary<int, double>();
            public Dictionary<int, double> Enemy = new Dictionary<int, double>();
            public Dictionary<int, double> Lantern = new Dictionary<int, double>();
            public Dictionary<int, double> Toad = new Dictionary<int, double>();
            public double Total(int m) => Enemy[m] + Lantern[m] + Toad[m];
        }

        public static Row Measure(SimData data, Scenario scenario, int seeds, string? dataDir)
        {
            var d = scenario.LoadData(dataDir);
            var g = d.Game;
            var drops = g.Balance.Drops;
            double bag = (drops.SilverBag[0] + drops.SilverBag[1]) / 2.0;
            double lanternShare = drops.Lantern.Where(l => l.Kind == Core.Build.Drops.SilverBag).Sum(l => l.Weight) / drops.Lantern.Sum(l => l.Weight);
            var life = g.Chapters.Single(c => c.Id == "life");
            var toadEvent = life.Events.FirstOrDefault(e => e.Kind == "toad");
            double toadEnd = toadEvent?.Window != null && toadEvent.Window.Length == 2 ? toadEvent.Window[1] : double.PositiveInfinity;
            var toad = g.Enemies.FirstOrDefault(e => e.Id == "golden_toad");
            double toadSilver = toad?.Reward?.Silver ?? 0;

            var runs = Enumerable.Range(1, seeds)
                .Select(s => RunSimulator.Run(d, scenario.ApplyOptions(new SimOptions { Policy = "average", Seed = s, Minutes = Minutes })))
                .ToList();
            var row = new Row { NameKo = scenario.NameKo };
            foreach (int m in Marks)
            {
                double kills = YinYangSim.Median(runs.Select(r => (double)r.Minutes.First(x => x.Minute == m).Kills));
                row.Kills[m] = kills;
                row.Enemy[m] = kills * drops.EnemySilverChance * bag;
                row.Lantern[m] = Math.Floor(m * 60 / LanternEverySeconds) * lanternShare * bag;
                row.Toad[m] = m >= toadEnd ? toadSilver : 0;
            }
            return row;
        }

        public static string Report(SimData data, int seeds, string? dataDir)
        {
            var g = data.Game;
            var drops = g.Balance.Drops;
            var rows = new[] { Scenarios.Baseline, Scenarios.Find("kill-xp") }.Select(s => Measure(data, s, seeds, dataDir)).ToList();
            var gdd = TargetFromGdd(data);
            var sb = new StringBuilder();
            void L(string s = "") => sb.AppendLine(s);

            L("# 한 생의 은자(RM-06)");
            L();
            L($"숭산 무리 모형(`RunSimulator`)을 {Minutes:0}분, 정책 average, 시드 1~{seeds}의 가운데 처치 수에 은자 규칙을 기댓값으로 얹었다. " +
              $"10장 화폐 표의 목표는 **{gdd}**.");
            L();
            var b = rows[0];
            var k = rows[1];
            L("## 요약");
            L();
            L($"- 지금 데이터로 20분 은자는 약 **{N(b.Total(20))}냥**, 목표의 {N(b.Total(20) / TargetHigh(gdd), "0.0")}배(위쪽 끝 기준). " +
              $"거의 다 잡몹 주머니({N(b.Enemy[20] / b.Total(20) * 100, "0")}%)다: 처치 {N(b.Kills[20])} × {N(drops.EnemySilverChance * 100, "0.0")}% × 주머니 {N((drops.SilverBag[0] + drops.SilverBag[1]) / 2.0)}냥.");
            L($"- 처치 진기 줄이기(P-02 안)로도 20분 처치가 {N(k.Kills[20])}마리로 {N(k.Total(20))}냥. 은자는 처치 진기와 따로 맞춰야 한다.");
            L($"- 등롱({N(b.Lantern[20])}냥)과 금두꺼비({N(b.Toad[20])}냥)만 치면 {N(b.Lantern[20] + b.Toad[20])}냥으로 목표 아래쪽 끝의 {N((b.Lantern[20] + b.Toad[20]) / TargetLow(gdd) * 100, "0")}%다. " +
              "'잡몹이 1.2% 확률로 은자 주머니'(8장 드롭 표)는 웹 프로토타입에서 돌던 값이고, 화면 적이 수백인 무리 모형에선 처치가 2만을 넘어 주머니가 너무 많이 나온다.");
            double mid = (TargetLow(gdd) + TargetHigh(gdd)) / 2;
            double bagMid = (drops.SilverBag[0] + drops.SilverBag[1]) / 2.0;
            double chance = (mid - b.Lantern[20] - b.Toad[20]) / (b.Kills[20] * bagMid);
            L($"- 목표 가운데({N(mid)}냥)에 맞추려면 잡몹 주머니 확률이 약 {N(chance * 100, "0.000")}%(지금의 {N(chance / drops.EnemySilverChance * 100, "0")}%)여야 한다. 맞추는 안은 backlog RM-06a(사람 결정 대기).");
            L();
            L("## 분마다(가운데값, 냥)");
            L();
            foreach (var r in rows)
            {
                L($"### {r.NameKo}");
                L();
                L("| 분 | 누적 처치 | 잡몹 주머니 | 등롱 | 금두꺼비 | 합 |");
                L("|---:|---:|---:|---:|---:|---:|");
                foreach (int m in Marks)
                    L($"| {m} | {N(r.Kills[m])} | {N(r.Enemy[m])} | {N(r.Lantern[m])} | {N(r.Toad[m])} | {N(r.Total(m))} |");
                L();
            }
            L("## 가정");
            L();
            L("| 항목 | 값 | 출처 |");
            L("|---|---|---|");
            L($"| 잡몹 주머니 확률 | {N(drops.EnemySilverChance * 100, "0.0")}% | balance.json `drops.enemySilverChance` |");
            L($"| 주머니 은자 | {drops.SilverBag[0]}~{drops.SilverBag[1]}(가운데 {N((drops.SilverBag[0] + drops.SilverBag[1]) / 2.0)}) | balance.json `drops.silverBag` |");
            L($"| 등롱 | {N(LanternEverySeconds)}초마다 하나, 다 부숨. 은자 주머니 {N(drops.Lantern.Where(l => l.Kind == Core.Build.Drops.SilverBag).Sum(l => l.Weight) / drops.Lantern.Sum(l => l.Weight) * 100, "0")}% | 웹 프로토타입 `template.html` 등롱 생성, balance.json `drops.lantern` |");
            L("| 금두꺼비 | 10~12분 창에 한 번, 잡는다고 봄 | chapters.json life `toad`, enemies.json `golden_toad.reward.silver` |");
            L("| 처치 | 무리 모형 한 마리 단위 | `RunSimulator`(level-curve.md 와 같은 모형) |");
            L();
            L("빠진 것: 은자 카드, 탐욕(연무장 +10%/레벨·성질), 혈월 1.5배, 보부상에서 쓴 은자, 천하 판 덩이(사람 수) 셈.");
            L();
            L("다시 만들기: `dotnet run --project core/BloodRain.Sim -c Release -- --silver docs/design/sim/silver.md`");
            return sb.ToString();
        }

        static string TargetFromGdd(SimData data)
        {
            try
            {
                var root = data.DataDir;
                var path = System.IO.Path.Combine(root, "..", "..", "..", "..", "..", "docs", "design", "gdd.md");
                foreach (var line in System.IO.File.ReadLines(path))
                    if (line.StartsWith("| 은자(銀子) |"))
                        return line.Split('|')[4].Trim().Replace("\\~", "~");
            }
            catch (Exception) { /* 기획서를 못 읽으면 아래 값 */ }
            return "20분 600~900";
        }

        static double TargetLow(string target)
        {
            var nums = System.Text.RegularExpressions.Regex.Matches(target, @"\d[\d,]*").Select(m => double.Parse(m.Value.Replace(",", ""), CultureInfo.InvariantCulture)).ToList();
            return nums.Count > 1 ? nums[nums.Count - 2] : 600;
        }

        static double TargetHigh(string target)
        {
            var nums = System.Text.RegularExpressions.Regex.Matches(target, @"\d[\d,]*").Select(m => double.Parse(m.Value.Replace(",", ""), CultureInfo.InvariantCulture)).ToList();
            return nums.Count > 0 ? nums.Last() : 900;
        }
    }
}
