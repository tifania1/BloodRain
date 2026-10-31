// 마교 몸의 혈공(기획서 5장 '마교 몸의 성장: 혈공')을 한 판 무리 모형으로 잰다. 보고서: docs/design/sim/blood-art.md
//
// RunSimulator 를 30분 돌리되, 그 땅의 적 기혈 배율(Chapter)과 혈공(BloodArtDensity, Core BloodArt 그대로)을 같이 얹는다.
//   - 진기는 구슬 값 × 농도. 적이 농도만큼 단단하니 화력이 같으면 처치가 줄어 서로 얼마나 메워지는지가 이 보고서의 질문이다.
//   - 심마(더 받은 진기 300마다 음 -1)는 무공·심법 레벨업으로 움직이는 게이지에 그대로 더해진다. 천마해체를 쓰면 시작값으로.
//   - 기획서 5장은 "십만대산 출생의 경지 도달 시각이 숭산 출생과 비슷한지 맞춘다"고 적었다. 기준은 진소운이 숭산에서 혈공 없이 도는 판.
// 빠진 것: 천마해체의 피해 2배·흡혈·기혈 소모(오의를 쓴 시각만 센다), 마공 깨달음, 적 피해(√농도), 지역 고유 적·기믹.
// CheonhaSim(cheonha.md)은 숭산 진기 흐름에 농도를 곱하기만 해서 마교가 더 빠르게 나온다. 적이 단단해지는 쪽까지 넣은 값은 이 보고서다.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BloodRain.Core.Data;

namespace BloodRain.Sim
{
    public sealed class BloodArtRow
    {
        public string Label = "", Character = "", Chapter = "";
        public double Density;
        public bool BloodArt;
        public List<SimResult> Runs = new List<SimResult>();

        public double RealmMedian(string realmId) =>
            YinYangSim.Median(Runs.Select(r => r.RealmMinute.TryGetValue(realmId, out var m) ? m : double.NaN).Where(x => !double.IsNaN(x)));
        public int RealmReached(string realmId) => Runs.Count(r => r.RealmMinute.ContainsKey(realmId));
        public double LevelMedian(int minute) => YinYangSim.Median(Runs.Select(r => (double)r.LevelAt(minute)));
        public double GaugeMedian(int minute) => YinYangSim.Median(Runs.Select(r => (double)r.At(minute).Gauge));
        public double StepsMean => Runs.Average(r => r.BloodArtSteps);
        public double UltMean => Runs.Average(r => r.UltimateMinutes.Count);
        public double ExtremeShare(double minutes) => Runs.Average(r => r.BandSeconds[2]) / (minutes * 60);
        public double BacklashMean => Runs.Average(r => r.BacklashMinutes.Count);
        public double KillsMedian(int minute) => YinYangSim.Median(Runs.Select(r => (double)r.At(minute).Kills));
    }

    public static class BloodArtSim
    {
        public const double Minutes = 30;
        public const string Policy = "average";
        public const string Baseline = "jin_sowoon";
        public static readonly int[] LevelMarks = { 10, 20, 30 };

        /// <summary>혈공 몸의 캐릭터: 시작 오의가 혈공을 되돌리는 오의(천마해체)인 사람. 지금 데이터에선 백야.</summary>
        public static string DemonicCharacter(GameData g)
        {
            var art = g.Cheonha.Sects.Select(s => s.BloodArt).First(a => a != null)!;
            return g.Characters.First(c => c.Ultimate == art.ResetUltimate).Id;
        }

        public static BloodArtRow Measure(SimData data, string label, string character, RegionDef region, bool bloodArt, int seeds, double minutes = Minutes, bool reset = true,
            Func<SimOptions, SimOptions>? apply = null, double? xpDensity = null)
        {
            string chapter = region.Chapter ?? "chapter1";
            var row = new BloodArtRow { Label = label, Character = character, Chapter = chapter, Density = region.Density, BloodArt = bloodArt };
            for (int s = 1; s <= seeds; s++)
            {
                var opt = new SimOptions
                {
                    Character = character, Policy = Policy, Seed = s, Minutes = minutes, Chapter = chapter,
                    BloodArtDensity = bloodArt ? (xpDensity ?? region.Density) : (double?)null, BloodArtReset = reset,
                };
                row.Runs.Add(RunSimulator.Run(data, apply != null ? apply(opt) : opt));
            }
            return row;
        }

        static string M(double minute)
        {
            if (double.IsNaN(minute)) return "—";
            int sec = (int)Math.Round(minute * 60);
            return $"{sec / 60}:{sec % 60:00}";
        }
        static string N(double x, string f = "0") => double.IsNaN(x) ? "—" : x.ToString(f, CultureInfo.InvariantCulture);
        static string G(double x) => double.IsNaN(x) ? "—" : x.ToString("+0;-0;0", CultureInfo.InvariantCulture);

        public static string Report(SimData data, int seeds = 5)
        {
            var g = data.Game;
            var art = g.Cheonha.Sects.Select(s => s.BloodArt).First(a => a != null)!;
            var demonicSect = g.Cheonha.Sects.First(s => s.BloodArt != null);
            string baekya = DemonicCharacter(g);
            var bk = g.Character(baekya);
            var home = g.Cheonha.Regions.First(r => r.Id == demonicSect.Region);
            var songshan = g.Cheonha.Regions.First(r => r.Chapter == "chapter1");
            var realms = g.Balance.Realms.Skip(1).ToList();
            var top = realms.Last();

            var baseRow = Measure(data, $"{g.Character(Baseline).NameKo} · {songshan.NameKo}, 혈공 없음(기준)", Baseline, songshan, false, seeds);
            var burden = Measure(data, $"{bk.NameKo} · {home.NameKo}, 혈공 없음", baekya, home, false, seeds);
            var real = Measure(data, $"{bk.NameKo} · {home.NameKo}, 혈공", baekya, home, true, seeds);
            var bkSong = Measure(data, $"{bk.NameKo} · {songshan.NameKo}, 혈공(농도 {N(songshan.Density, "0.0")}라 그대로)", baekya, songshan, true, seeds);
            var noReset = Measure(data, $"{bk.NameKo} · {home.NameKo}, 혈공, 천마해체로 안 되돌림", baekya, home, true, seeds, reset: false);
            var main = new[] { baseRow, bkSong, burden, real, noReset };

            var sweepRegions = g.Cheonha.Regions.Where(r => r.Chapter != null && r.Chapter.StartsWith("chapter"))
                .OrderBy(r => r.Density).ToList();
            var sweep = sweepRegions.Select(r => r == home ? real : r == songshan ? bkSong
                : Measure(data, r.NameKo, baekya, r, true, seeds)).ToList();

            var sb = new StringBuilder();
            void L(string s = "") => sb.AppendLine(s);
            L("# 마교 몸의 혈공 시뮬레이션");
            L();
            L("> 자동 생성: `dotnet run --project core/BloodRain.Sim -c Release -- --blood-art docs/design/sim/blood-art.md`. 손으로 고치지 않는다.");
            L();
            L($"한 판 무리 모형(`RunSimulator`)을 {Minutes:0}분, 정책 {Policy}, 시드 1~{seeds}의 가운데값. 땅마다 적 기혈 배율(balance.json `chapterMultiplier`)을 얹고, " +
              $"혈공 줄은 Core `BloodArt` 를 그대로 돌린다(cheonha.json `{demonicSect.Id}.bloodArt`: 진기 × 농도, 더 받은 진기 {N(art.ExtraXpPerStep)}마다 음 -{art.YinPerStep}, " +
              $"오의 {g.Ultimates.FirstOrDefault(u => u.Id == art.ResetUltimate)?.NameKo ?? art.ResetUltimate}를 쓰면 시작값 {G(bk.YinyangStart)}로).");
            L();
            L("빠진 것: 천마해체의 피해 2배·흡혈·기혈 소모(쓴 시각만 센다), 마공 깨달음, 적이 때리는 쪽(√농도배, contact.md), 지역 고유 적·기믹, 땅의 주인. " +
              "적 구성과 사건은 숭산 옛 1장 표 그대로이고 처치 진기도 지금 데이터(P-02 미정)라 레벨이 기획 목표보다 빠르다. 그래서 절대 시각보다 **기준 줄과의 차이**를 본다.");
            L();
            L("[cheonha.md](cheonha.md) 의 마교 줄은 숭산 진기 흐름에 농도를 곱하기만 해서(적이 단단해지는 쪽이 없다) 다른 문파보다 훨씬 빠르게 나온다. 적 기혈까지 넣은 값은 이 표다.");
            L();

            L("## 경지 도달 시각");
            L();
            L("| 판 | 농도 | " + string.Join(" | ", realms.Select(r => r.NameKo)) + " | " + string.Join(" | ", LevelMarks.Select(m => $"{m}분 레벨")) + " |");
            L("| --- | --- | " + string.Join(" | ", realms.Select(_ => "---")) + " | " + string.Join(" | ", LevelMarks.Select(_ => "---")) + " |");
            foreach (var r in main)
                L($"| {r.Label} | {N(r.Density, "0.0")} | " + string.Join(" | ", realms.Select(x => M(r.RealmMedian(x.Id)))) + " | " +
                  string.Join(" | ", LevelMarks.Select(m => N(r.LevelMedian(m)))) + " |");
            L();
            L("닿지 못한 판이 있으면 닿은 판만 모은 가운데값이다(아래 '읽을 것'에 판 수).");
            L();

            L("## 심마와 음양");
            L();
            L("| 판 | 심마 횟수(평균) | 천마해체(평균) | " + string.Join(" | ", LevelMarks.Select(m => $"{m}분 게이지"))+ " | 극성 시간 | 역류(평균) |");
            L("| --- | --- | --- | " + string.Join(" | ", LevelMarks.Select(_ => "---")) + " | --- | --- |");
            foreach (var r in main.Skip(1))
                L($"| {r.Label} | {N(r.StepsMean, "0.0")} | {N(r.UltMean, "0.0")} | " + string.Join(" | ", LevelMarks.Select(m => G(r.GaugeMedian(m)))) +
                  $" | {N(r.ExtremeShare(Minutes) * 100)}% | {N(r.BacklashMean, "0.0")} |");
            L();
            L($"게이지는 무공·심법 레벨업(무공 ±{g.Balance.Yinyang.WeaponPerLevel}, 심법 ±{g.Balance.Yinyang.PassivePerLevel})과 심마가 함께 움직인 값. 극성은 ±{g.Balance.Yinyang.ExtremeFrom}부터.");
            L();

            L("## 머무는 땅에 따라(백야, 혈공)");
            L();
            L($"| 땅 | 농도 | {realms[2].NameKo} | {top.NameKo} | 30분 레벨 | 심마 횟수 | 30분 게이지 | 극성 시간 |");
            L("| --- | --- | --- | --- | --- | --- | --- | --- |");
            foreach (var r in sweep)
            {
                string name = sweepRegions[sweep.IndexOf(r)].NameKo;
                L($"| {name} | {N(r.Density, "0.0")} | {M(r.RealmMedian(realms[2].Id))} | {M(r.RealmMedian(top.Id))} ({r.RealmReached(top.Id)}/{r.Runs.Count}) | {N(r.LevelMedian(30))} | " +
                  $"{N(r.StepsMean, "0.0")} | {G(r.GaugeMedian(30))} | {N(r.ExtremeShare(Minutes) * 100)}% |");
            }
            L();
            L("한 생 내내 그 땅에 머문다고 본 값. 실제로는 걸어 다니며 농도가 바뀐다.");
            L();

            L("## 읽을 것");
            L();
            double b30 = baseRow.LevelMedian(30), r30 = real.LevelMedian(30), u30 = burden.LevelMedian(30);
            var j = realms[2];
            L($"- 혈공이 없으면 {home.NameKo}(농도 {N(home.Density, "0.0")})에서 30분 레벨 {N(u30)}, 기준({songshan.NameKo} 진소운) {N(b30)}의 {N(u30 / b30 * 100)}%. " +
              $"혈공을 얹으면 {N(r30)}({N(r30 / b30 * 100)}%). {j.NameKo} 도달은 기준 {M(baseRow.RealmMedian(j.Id))}, 혈공 {M(real.RealmMedian(j.Id))}, 혈공 없음 {M(burden.RealmMedian(j.Id))}.");
            double diffTop = real.RealmMedian(top.Id) - baseRow.RealmMedian(top.Id);
            L($"- {top.NameKo}: 기준 {M(baseRow.RealmMedian(top.Id))}({baseRow.RealmReached(top.Id)}/{seeds}판), 혈공 {M(real.RealmMedian(top.Id))}({real.RealmReached(top.Id)}/{seeds}판)" +
              (double.IsNaN(diffTop) ? "." : $", 차이 {(diffTop >= 0 ? "+" : "-")}{M(Math.Abs(diffTop))}.") +
              $" 적이 {N(home.Density, "0.0")}배 단단해도 10분 처치는 기준 {N(baseRow.KillsMedian(10))}, 혈공 없음 {N(burden.KillsMedian(10))}로 " +
              $"기준의 {N(burden.KillsMedian(10) / baseRow.KillsMedian(10) * 100)}%로 줄 뿐이다(범위 무공이 무리를 한꺼번에 치고, 화력이 남는 지금 데이터라서). " +
              $"그 위에 진기를 {N(home.Density, "0.0")}배로 받으니 레벨이 빨라지고, 빨라진 레벨이 화력을 키워 처치도 기준보다 많아진다(혈공 {N(real.KillsMedian(10))}).");
            double perUlt = real.UltMean > 0 ? real.StepsMean / real.UltMean : double.NaN;
            L($"- 심마는 {home.NameKo} 30분에 평균 {N(real.StepsMean)}번(모두 더하면 음 쪽 {N(real.StepsMean * art.YinPerStep)}), 그사이 천마해체를 {N(real.UltMean, "0.0")}번 써서 한 번 쓸 때까지 {N(perUlt, "0.0")}만큼 기운다. " +
              $"30분 게이지 가운데값은 {G(real.GaugeMedian(30))}, 극성에 머문 시간 {N(real.ExtremeShare(Minutes) * 100)}%. " +
              $"천마해체가 처치 {N(g.Balance.UltimateCharge.Max / g.Balance.UltimateCharge.PerKill)}마리쯤마다 차서 심마가 쌓일 틈이 없다. " +
              $"되돌리지 않으면 30분 게이지 {G(noReset.GaugeMedian(30))}, 극성 시간 {N(noReset.ExtremeShare(Minutes) * 100)}%, 역류 평균 {N(noReset.BacklashMean, "0.0")}번.");
            var fastest = sweep.OrderBy(r => r.RealmMedian(top.Id)).First();
            var near = sweep.OrderBy(r => Math.Abs(r.RealmMedian(top.Id) - baseRow.RealmMedian(top.Id))).First();
            L($"- 머무는 땅을 바꿔 보면 {top.NameKo} 도달이 가장 빠른 곳은 {sweepRegions[sweep.IndexOf(fastest)].NameKo}({M(fastest.RealmMedian(top.Id))}). " +
              $"농도가 짙을수록 빨라지니 기획서 5장 '짙은 땅에 머물수록 빨리 큰다'는 맞다. 기준({M(baseRow.RealmMedian(top.Id))})과 가장 가까운 곳은 " +
              $"{sweepRegions[sweep.IndexOf(near)].NameKo}({M(near.RealmMedian(top.Id))}).");
            L("- 숫자(300, 음 -1)를 바꿀 때는 이 보고서와 yinyang.md 를 같이 다시 만든다. 사람이 볼 것: 혈공 줄이 기준보다 얼마나 빠르거나 느려야 하는지(지금 기획은 '비슷하게').");
            return sb.ToString();
        }
    }
}
