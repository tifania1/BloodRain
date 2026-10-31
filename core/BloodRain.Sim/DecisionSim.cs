// 사람 결정 묶음(P-02·T-40·T-41). 보고서: docs/design/sim/decisions.md
//
// 세 결정이 서로 물려 있다. 처치 진기(P-02)를 줄이면 레벨·화력이 늦어져 땅의 주인 처치 시간(T-40)이 바뀌고,
// 혈공(T-41)은 진기에 농도를 곱하는 규칙이라 처치 진기의 크기를 그대로 따라 움직인다.
// 그래서 같은 시드로 '지금 데이터'와 '처치 진기 줄이기'를 나란히 돌려 세 표를 한 장에 놓는다.
//
// 새 가정은 없다. 천하 한 생은 CheonhaSim, 혈공은 BloodArtSim 그대로이고,
// T-41 (가) '진기 배율을 √농도로'는 Core BloodArt 에 넘기는 농도만 √농도로 바꿔 흉내 낸다(적 기혈 배율은 그 땅 그대로).
// 데이터는 건드리지 않는다.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace BloodRain.Sim
{
    public sealed class DecisionRow
    {
        public string ScenarioId = "", ScenarioKo = "";
        /// <summary>마교를 뺀 문파마다 출생지 땅의 주인 첫 싸움 걸린 초 ÷ 목표 초(시드 가운데값).</summary>
        public Dictionary<string, double> BossRatio = new Dictionary<string, double>();
        /// <summary>마교를 뺀 문파마다 첫 싸움 시작 분(시드 가운데값).</summary>
        public Dictionary<string, double> BossStart = new Dictionary<string, double>();
        public double TopMinuteMedian;
        public int TopReached, TopRuns;
        public BloodArtRow Base = null!, Blood = null!, BloodSqrt = null!;
    }

    public static class DecisionSim
    {
        public static readonly string[] ScenarioIds = { "baseline", "kill-xp" };

        public static DecisionRow Measure(string? dataDir, string scenarioId, int seeds)
        {
            var sc = Scenarios.Find(scenarioId);
            var data = sc.LoadData(dataDir);
            var g = data.Game;
            var row = new DecisionRow { ScenarioId = sc.Id, ScenarioKo = sc.NameKo };
            var top = g.Balance.Realms.OrderBy(r => r.Level).Last().Id;
            var tops = new List<double>();
            foreach (var s in g.Cheonha.Sects.Where(x => !string.IsNullOrEmpty(x.Region) && x.BloodArt == null))
            {
                var runs = Enumerable.Range(1, seeds).Select(seed => CheonhaSim.Run(data, s.Id, seed, sc.ApplyOptions)).ToList();
                tops.AddRange(runs.Select(r => r.RealmMinute.TryGetValue(top, out var m) ? m : double.NaN));
                var firsts = runs.Select(r => r.Bosses.Where(b => !double.IsNaN(b.EngagedAt)).OrderBy(b => b.EngagedAt).FirstOrDefault())
                    .Where(b => b != null).Select(b => b!).ToList();
                if (firsts.Count == 0) continue;
                row.BossRatio[s.NameKo] = YinYangSim.Median(firsts.Select(b => b.FightSeconds / b.TargetKillSeconds));
                row.BossStart[s.NameKo] = YinYangSim.Median(firsts.Select(b => b.EngagedAt));
            }
            // 30분 안에 못 닿은 판은 30분 뒤로 센다(가운데값이 닿은 판 쪽으로 쏠리지 않게).
            row.TopMinuteMedian = YinYangSim.Median(tops.Select(x => double.IsNaN(x) ? double.PositiveInfinity : x));
            row.TopReached = tops.Count(x => !double.IsNaN(x));
            row.TopRuns = tops.Count;

            var demonic = g.Cheonha.Sects.First(s => s.BloodArt != null);
            var home = g.Cheonha.Regions.First(r => r.Id == demonic.Region);
            var songshan = g.Cheonha.Regions.First(r => r.Chapter == "chapter1");
            string baekya = BloodArtSim.DemonicCharacter(g);
            row.Base = BloodArtSim.Measure(data, "기준", BloodArtSim.Baseline, songshan, false, seeds, apply: sc.ApplyOptions);
            row.Blood = BloodArtSim.Measure(data, "혈공", baekya, home, true, seeds, apply: sc.ApplyOptions);
            row.BloodSqrt = BloodArtSim.Measure(data, "혈공 √농도", baekya, home, true, seeds, apply: sc.ApplyOptions, xpDensity: Math.Sqrt(home.Density));
            return row;
        }

        static string M(double minute)
        {
            if (double.IsNaN(minute) || double.IsInfinity(minute)) return "—";
            int sec = (int)Math.Round(minute * 60);
            return $"{sec / 60}:{sec % 60:00}";
        }
        static string N(double x, string f = "0") => double.IsNaN(x) ? "—" : x.ToString(f, CultureInfo.InvariantCulture);

        public static string Report(string? dataDir, int seeds = 3)
        {
            var rows = ScenarioIds.Select(id => Measure(dataDir, id, seeds)).ToList();
            var g = Scenarios.Baseline.LoadData(dataDir).Game;
            var top = g.Balance.Realms.OrderBy(r => r.Level).Last();
            var home = g.Cheonha.Regions.First(r => r.Id == g.Cheonha.Sects.First(s => s.BloodArt != null).Region);
            var sects = rows[0].BossRatio.Keys.ToList();

            var sb = new StringBuilder();
            void L(string s = "") => sb.AppendLine(s);
            L("# 사람 결정 묶음: 처치 진기(P-02) · 땅의 주인(T-40) · 혈공(T-41)");
            L();
            L("> 자동 생성: `dotnet run --project core/BloodRain.Sim -c Release -- --decisions docs/design/sim/decisions.md`. 손으로 고치지 않는다.");
            L();
            L("셋 다 backlog 에서 사람 결정을 기다린다. 처치 진기를 줄이면 레벨·화력이 늦어져 땅의 주인 처치 시간이 바뀌고, 혈공은 진기에 농도를 곱하니 처치 진기 크기를 그대로 따른다. " +
              $"그래서 같은 시드(1~{seeds})로 '지금 데이터'와 '처치 진기 줄이기'(Scenarios.cs `kill-xp`, 강시·혈인 1, 목인나한·동갑시 2)를 나란히 돌렸다. 데이터는 바꾸지 않았다.");
            L();
            L("자세한 근거는 [cluster-xp.md](cluster-xp.md)(P-02), [cheonha.md](cheonha.md)(T-40), [blood-art.md](blood-art.md)(T-41).");
            L();

            L("## 한눈에");
            L();
            L($"| | {string.Join(" | ", rows.Select(r => r.ScenarioKo))} |");
            L("| --- | " + string.Join(" | ", rows.Select(_ => "---")) + " |");
            L($"| 천하 {top.NameKo} 도달(마교 뺀 여덟 문파, 가운데) | " + string.Join(" | ", rows.Select(r => $"{M(r.TopMinuteMedian)} ({r.TopReached}/{r.TopRuns}판 닿음)")) + " |");
            L("| 출생지 땅의 주인 첫 싸움 ÷ 목표 초(문파 가운데) | " + string.Join(" | ", rows.Select(r => N(YinYangSim.Median(r.BossRatio.Values), "0.0") + "배")) + " |");
            L($"| 혈공 {top.NameKo} − 기준 {top.NameKo} | " + string.Join(" | ", rows.Select(r => Diff(r.Blood, r.Base, top.Id))) + " |");
            L($"| 혈공 √농도일 때 − 기준 | " + string.Join(" | ", rows.Select(r => Diff(r.BloodSqrt, r.Base, top.Id))) + " |");
            L();
            L($"기준은 진소운이 숭산에서 혈공 없이 도는 한 판, 혈공은 백야가 {home.NameKo}(농도 {N(home.Density, "0.0")})에 머무는 한 판(blood-art.md 와 같은 무리 모형). '—'는 30분 안에 못 닿음.");
            L();

            L("## T-40 땅의 주인 첫 싸움(문파별)");
            L();
            L("| 문파 | " + string.Join(" | ", rows.Select(r => $"{r.ScenarioKo}: 시작 / 배")) + " |");
            L("| --- | " + string.Join(" | ", rows.Select(_ => "---")) + " |");
            foreach (var s in sects)
                L($"| {s} | " + string.Join(" | ", rows.Select(r => r.BossRatio.TryGetValue(s, out var v) ? $"{M(r.BossStart[s])} / {N(v, "0.0")}" : "—")) + " |");
            L();
            L("'배'는 걸린 초 ÷ 목표 초(60~80초). 1.0 근처가 목표다. 보스 기혈 식은 20분 화력 기준이라 화력이 늦게 크면 배수가 더 커진다.");
            L();

            L("## T-41 혈공(백야)");
            L();
            var realms = g.Balance.Realms.Skip(1).ToList();
            L("| 시험 | 판 | " + string.Join(" | ", realms.Select(r => r.NameKo)) + " | 30분 레벨 | 심마 횟수 |");
            L("| --- | --- | " + string.Join(" | ", realms.Select(_ => "---")) + " | --- | --- |");
            foreach (var r in rows)
                foreach (var (label, b) in new[] { ("기준(진소운·숭산)", r.Base), ($"혈공(진기 × {N(home.Density, "0.0")})", r.Blood), ($"(가) 혈공(진기 × √농도 {N(Math.Sqrt(home.Density), "0.00")})", r.BloodSqrt) })
                    L($"| {r.ScenarioKo} | {label} | " + string.Join(" | ", realms.Select(x => M(b.RealmMedian(x.Id)))) + $" | {N(b.LevelMedian(30))} | {N(b.StepsMean, "0.0")} |");
            L();

            L("## 읽을 것");
            L();
            var a = rows[0]; var k = rows[1];
            double ra = YinYangSim.Median(a.BossRatio.Values), rk = YinYangSim.Median(k.BossRatio.Values);
            L($"- 처치 진기를 줄이면 천하 {top.NameKo} 도달이 {M(a.TopMinuteMedian)} → {M(k.TopMinuteMedian)}({k.TopReached}/{k.TopRuns}판만 30분 안에 닿음). 땅의 주인 첫 싸움은 {N(ra, "0.0")}배 → {N(rk, "0.0")}배" +
              (rk > ra ? "로 더 나빠진다. P-02 만 정하면 T-40 은 저절로 풀리지 않고, 기혈 식이나 깨어나는 경지 쪽을 같이 손봐야 한다." : "로 줄어든다. T-40 의 배수 일부는 P-02 로 풀린다."));
            L($"- 혈공은 지금 데이터에서 기준보다 {Diff(a.Blood, a.Base, top.Id)}, 처치 진기를 줄이면 {Diff(k.Blood, k.Base, top.Id)}. " +
              $"√농도로 바꾸면 각각 {Diff(a.BloodSqrt, a.Base, top.Id)}, {Diff(k.BloodSqrt, k.Base, top.Id)}. " +
              (Gap(k.Blood, k.Base, top.Id) < Gap(a.Blood, a.Base, top.Id)
                ? "처치 진기를 줄이면 기준 판이 혈공 판보다 더 많이 늦어져 격차가 오히려 벌어진다. T-41 은 P-02 를 정한 뒤 그 값으로 다시 재야 하는 이유다. "
                : "처치 진기를 줄이면 격차가 줄어든다. ") +
              "기획서 5장 '숭산 출생과 비슷하게'에 가까운 쪽을 고르면 된다.");
            L("- 세 결정을 한 번에 정할 때 이 표를 다시 만들면 된다. 다른 조합(예: 처치 진기 줄이기 + 적 기혈 증가)을 보려면 `DecisionSim.ScenarioIds` 에 Scenarios.cs id 를 더한다.");
            return sb.ToString();
        }

        static double Gap(BloodArtRow x, BloodArtRow b, string realmId) => x.RealmMedian(realmId) - b.RealmMedian(realmId);

        static string Diff(BloodArtRow x, BloodArtRow b, string realmId)
        {
            double d = x.RealmMedian(realmId) - b.RealmMedian(realmId);
            if (double.IsNaN(d)) return x.RealmReached(realmId) == 0 ? "못 닿음" : "기준이 못 닿음";
            return (d >= 0 ? "+" : "-") + M(Math.Abs(d));
        }
    }
}
