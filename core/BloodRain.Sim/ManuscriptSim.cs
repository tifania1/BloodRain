// 혈월 천하 의서 일곱 권이 모이는 생 수(밸런스 시뮬레이션, R4-08 결정 돕기). 보고서: docs/design/sim/manuscript.md
//
// 기획서 7장: 은빛 등롱은 15분 이후 한 생에 한두 개, 혈월 천하에선 부수면 '낮은 확률'로 의서 한 권. 그 확률이 R4-08 사람 결정 대기다.
// 등롱 수는 chapters.json life 의 silver_lantern 사건을 그대로 쓴다(창 안 무작위 분, chance). 생이 그 분까지 가야 등롱이 나온다.
// 의서 수는 cheonha.json cycle.trueEndingManuscripts. 혈해마공 잔편(20장 비전 표)의 '의서 3권 이상' 조건도 같이 본다.
// 확률과 생 길이는 정해진 값이 없어 몇 가지를 나란히 둔다. 생 사이 시간(정산·환혼·메뉴)은 넣지 않았다.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BloodRain.Core.Data;

namespace BloodRain.Sim
{
    public static class ManuscriptSim
    {
        public static readonly double[] Chances = { 0.10, 0.15, 0.20, 0.25, 0.30 };
        /// <summary>생이 끝나는 게임 분. 16 은 첫 등롱 창 한가운데, 18 은 둘째 창 한가운데, 30 은 현경까지 가는 생.</summary>
        public static readonly double[] LifeMinutes = { 16, 18, 30 };
        public const int Trials = 20000;

        static string N(double v, string f = "0.0") => v.ToString(f, CultureInfo.InvariantCulture);

        public sealed class Lantern
        {
            public double From, To, Chance;
        }

        public static List<Lantern> Lanterns(GameData g)
        {
            var life = g.Chapters.Single(c => c.Id == "life");
            return life.Events.Where(e => e.Kind == "silver_lantern")
                .Select(e => new Lantern { From = e.EarliestMinute, To = e.LatestMinute, Chance = e.Chance })
                .ToList();
        }

        /// <summary>한 생에서 나오는 등롱 수. 창 안 무작위 분이 생 끝보다 앞이어야 한다.</summary>
        public static int LanternsInLife(IReadOnlyList<Lantern> ls, double lifeMinutes, Random rng)
        {
            int n = 0;
            foreach (var l in ls)
            {
                if (rng.NextDouble() >= l.Chance) continue;
                double at = l.From + rng.NextDouble() * (l.To - l.From);
                if (at < lifeMinutes) n++;
            }
            return n;
        }

        public sealed class Result
        {
            public double Chance, LifeMinutes, LanternsPerLife;
            public double[] Lives7 = Array.Empty<double>();
            public double[] Lives3 = Array.Empty<double>();
            public double Pct(double[] sorted, double q) => sorted[Math.Min(sorted.Length - 1, (int)Math.Floor(q * sorted.Length))];
        }

        public static Result Measure(IReadOnlyList<Lantern> ls, int need, double chance, double lifeMinutes, int seed, int trials = Trials)
        {
            var rng = new Random(seed);
            var l7 = new double[trials];
            var l3 = new double[trials];
            long lanterns = 0, lives = 0;
            for (int t = 0; t < trials; t++)
            {
                int got = 0, life = 0;
                l3[t] = double.NaN;
                while (got < need)
                {
                    life++;
                    if (life > 100000) break;
                    int n = LanternsInLife(ls, lifeMinutes, rng);
                    lanterns += n; lives++;
                    for (int i = 0; i < n && got < need; i++)
                        if (rng.NextDouble() < chance) got++;
                    if (got >= Math.Min(3, need) && double.IsNaN(l3[t])) l3[t] = life;
                }
                l7[t] = life;
            }
            Array.Sort(l7);
            Array.Sort(l3);
            return new Result { Chance = chance, LifeMinutes = lifeMinutes, LanternsPerLife = (double)lanterns / lives, Lives7 = l7, Lives3 = l3 };
        }

        public static string Report(SimData data, int seed)
        {
            var g = data.Game;
            var ls = Lanterns(g);
            int need = g.Cheonha.Cycle.TrueEndingManuscripts;
            var sb = new StringBuilder();
            void L(string s = "") => sb.AppendLine(s);

            var rows = new List<Result>();
            foreach (double m in LifeMinutes)
                foreach (double p in Chances)
                    rows.Add(Measure(ls, need, p, m, seed));

            L("# 혈월 천하 의서 일곱 권(R4-08)");
            L();
            L($"혈월 천하에서 은빛 등롱을 부수면 낮은 확률로 의서 한 권(7장). 그 확률이 R4-08 사람 결정 대기라, 확률과 생 길이를 바꿔 가며 " +
              $"의서 {need}권(cheonha.json `cycle.trueEndingManuscripts`)이 모이는 생 수를 {Trials:#,0}번 돌려 봤다. 시드 {seed}.");
            L();
            L("등롱은 chapters.json life 의 `silver_lantern` 사건 그대로: " +
              string.Join(", ", ls.Select(l => $"{N(l.From, "0")}~{N(l.To, "0")}분 창 안 무작위 분" + (l.Chance < 1 ? $"(나올 확률 {N(l.Chance * 100, "0")}%)" : ""))) +
              ". 생이 그 분까지 가야 등롱이 나온다고 봤다.");
            L();

            var rec = rows.First(r => r.LifeMinutes == 30 && Math.Abs(r.Chance - 0.15) < 1e-9);
            L("## 요약");
            L();
            L($"- 30분(현경) 생이면 한 생 등롱 {N(rec.LanternsPerLife, "0.00")}개. 추천안 15%면 일곱 권까지 가운데 **{N(rec.Pct(rec.Lives7, 0.5), "0")}생**" +
              $"(열에 하나는 {N(rec.Pct(rec.Lives7, 0.1), "0")}생 안에, 열에 하나는 {N(rec.Pct(rec.Lives7, 0.9), "0")}생 넘게), " +
              $"30분으로 치면 {N(rec.Pct(rec.Lives7, 0.5) * 0.5, "0")}시간({N(rec.Pct(rec.Lives7, 0.1) * 0.5, "0")}~{N(rec.Pct(rec.Lives7, 0.9) * 0.5, "0")}시간).");
            double midH = rec.Pct(rec.Lives7, 0.5) * 0.5, slowH = rec.Pct(rec.Lives7, 0.9) * 0.5;
            L($"- 추천안은 혈월 천하를 15~20시간(30분 생 30~40번)으로 잡았다. 가운데 {N(midH, "0")}시간은 " +
              (midH < 15 ? "그보다 짧다" : midH <= 20 ? "그 안에 든다" : "그보다 길다") +
              $". 느린 쪽 열에 하나는 {N(slowH, "0")}시간이라, 10장 '진엔딩 30~40시간'에서 혈월 천하 몫이 늘어나는 사람이 나올 수 있다.");
            var short16 = rows.First(r => r.LifeMinutes == 16 && Math.Abs(r.Chance - 0.15) < 1e-9);
            L($"- 생이 짧으면 크게 느려진다: 16분에 끝나는 생만 이어 가면 등롱이 한 생 {N(short16.LanternsPerLife, "0.00")}개라 15%로 가운데 {N(short16.Pct(short16.Lives7, 0.5), "0")}생. " +
              "등롱이 15분 이후에만 나오니, 혈월 천하에서 일찍 쓰러지는 사람은 의서를 거의 못 본다.");
            L($"- 혈해마공 잔편(의서 3권 이상에서 은빛 등롱)은 15%·30분 생이면 가운데 {N(rec.Pct(rec.Lives3, 0.5), "0")}생째에 조건이 찬다.");
            L();
            L("## 표: 일곱 권까지 생 수(가운데 · 열에 하나 빠른 쪽 ~ 느린 쪽)");
            L();
            L("| 생 길이 | 한 생 등롱 | " + string.Join(" | ", Chances.Select(p => $"{N(p * 100, "0")}%")) + " |");
            L("|---:|---:|" + string.Concat(Chances.Select(_ => "---:|")));
            foreach (double m in LifeMinutes)
            {
                var rs = rows.Where(r => r.LifeMinutes == m).ToList();
                L($"| {N(m, "0")}분 | {N(rs[0].LanternsPerLife, "0.00")} | " +
                  string.Join(" | ", rs.Select(r => $"{N(r.Pct(r.Lives7, 0.5), "0")} ({N(r.Pct(r.Lives7, 0.1), "0")}~{N(r.Pct(r.Lives7, 0.9), "0")})")) + " |");
            }
            L();
            L("## 표: 30분 생으로 친 시간(가운데 · 열에 하나 느린 쪽)");
            L();
            L("| 확률 | 일곱 권 가운데 | 일곱 권 느린 쪽 | 셋째 권(잔편 조건) 가운데 |");
            L("|---:|---:|---:|---:|");
            foreach (var r in rows.Where(r => r.LifeMinutes == 30))
                L($"| {N(r.Chance * 100, "0")}% | {N(r.Pct(r.Lives7, 0.5) * 0.5, "0.0")}시간 | {N(r.Pct(r.Lives7, 0.9) * 0.5, "0.0")}시간 | {N(r.Pct(r.Lives3, 0.5) * 0.5, "0.0")}시간 |");
            L();
            L("## 가정과 빠진 것");
            L();
            L("- 등롱은 다 부순다. 혈월 천하라고 등롱 수가 달라지지 않는다(7장에 따로 적힌 것이 없다).");
            L("- 생 길이는 한 사람이 늘 같다고 봤다. 실제로는 생마다 다르니 표의 두 줄 사이 어딘가다.");
            L("- 생 사이 시간(정산·환혼·객잔)은 시간에 넣지 않았다. 넣으면 시간이 조금 더 는다.");
            L("- 확률을 못 채운 만큼 다음에 올리는 장치(바닥 보장)는 기획서에 없어 넣지 않았다. 열에 하나 느린 쪽이 너무 길면 사람 결정으로 붙일 수 있다.");
            L();
            L("다시 만들기: `dotnet run --project core/BloodRain.Sim -c Release -- --manuscript docs/design/sim/manuscript.md`");
            return sb.ToString();
        }
    }
}
