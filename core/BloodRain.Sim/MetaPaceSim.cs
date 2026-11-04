// 메타 진행 속도 조정안 비교(R-02). 보고서: docs/design/sim/meta-pace.md
//
// meta.md 는 봉인석 7개가 첫 생 8분 기준 6.5시간에 모인다(목표 15~20시간, 10장). 무엇 때문에 빠른지 보고,
// 데이터 한두 곳을 바꾼 안을 MetaSim 그대로 돌려 견준다. 가정(생의 길이, 연무장 사는 법, 몸 고르기)은 MetaSim 맨 위와 같다.
//   - 깨어나는 경지: cheonha.json bosses.awakenFrom 을 바꾼다.
//   - 봉인석 요구: 봉인석 하나를 같은 땅의 주인 N번 처치로 모은다(첫 처치 + 혈영). Core 에는 아직 없는 규칙이라 시뮬레이터에서만 센다.
//   - 연무장 비용: balance.json training.costGrowthPerBought 를 바꾼다.
// 데이터는 안마다 새로 읽어 고친다(다른 보고서에 번지지 않게).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BloodRain.Core.Data;

namespace BloodRain.Sim
{
    public static class MetaPaceSim
    {
        public sealed class Variant
        {
            public string Id = "", NameKo = "", Change = "";
            public string? AwakenFrom;
            public float? CostGrowth;
            public int KillsPerStone = 1;
        }

        public static readonly Variant[] Variants =
        {
            new Variant { Id = "now", NameKo = "지금 데이터", Change = "—" },
            new Variant { Id = "awaken", NameKo = "가. 초절정에 깨어남", Change = "cheonha.json bosses.awakenFrom 절정 → 초절정", AwakenFrom = "chojeoljeong" },
            new Variant { Id = "stone2", NameKo = "나. 봉인석 조각 둘", Change = "봉인석 하나 = 같은 땅의 주인 두 번(첫 처치 + 혈영 한 번)", KillsPerStone = 2 },
            new Variant { Id = "stone3", NameKo = "나'. 봉인석 조각 셋", Change = "봉인석 하나 = 같은 땅의 주인 세 번", KillsPerStone = 3 },
            new Variant { Id = "cost2", NameKo = "다'. 연무장 비용 조금 가파르게", Change = "balance.json training.costGrowthPerBought 0.1 → 0.2", CostGrowth = 0.2f },
            new Variant { Id = "cost", NameKo = "다. 연무장 비용 가파르게", Change = "balance.json training.costGrowthPerBought 0.1 → 0.3", CostGrowth = 0.3f },
            new Variant { Id = "stone2cost", NameKo = "나 + 다", Change = "조각 둘 + 비용 0.3", KillsPerStone = 2, CostGrowth = 0.3f },
        };

        public const int MaxLives = 80;

        public static SimData Apply(Variant v, string? dataDir)
        {
            var data = SimData.Load(dataDir);
            if (v.AwakenFrom != null) data.Game.Cheonha.Bosses.AwakenFrom = v.AwakenFrom;
            if (v.CostGrowth is float cg) data.Game.Balance.Training.CostGrowthPerBought = cg;
            // 조각 안에서는 Soul 의 봉인석(첫 처치)이 7개여도 봉마곡이 열리면 안 된다. 셈한 봉인석이 모이기 전엔 감춰 둔다.
            if (v.KillsPerStone > 1)
                foreach (var r in data.Game.Cheonha.Regions.Where(r => r.HiddenUntilSealStones > 0)) r.HiddenUntilSealStones = int.MaxValue;
            return data;
        }

        public static List<MetaLifeRow> Run(Variant v, double baseSurvive, string? dataDir) =>
            MetaSim.Run(Apply(v, dataDir), baseSurvive, null, MaxLives, 1, v.KillsPerStone);

        static readonly string[] Peak = { "절정", "초절정", "화경", "현경" };

        static string At(IEnumerable<MetaLifeRow> rows, Func<MetaLifeRow, bool> hit)
        {
            var r = rows.FirstOrDefault(hit);
            return r == null ? "—" : $"{r.Life}생({r.Hours:0.0}시간)";
        }

        public static string Report(SimData baseData)
        {
            var g = baseData.Game;
            string dir = baseData.DataDir;
            int need = g.Cheonha.Bosses.FinalRequires.SealStones;
            var sb = new StringBuilder();
            sb.AppendLine("# 메타 진행 속도 조정안(R-02)");
            sb.AppendLine();
            sb.AppendLine("`dotnet run --project core/BloodRain.Sim -- --meta-pace docs/design/sim/meta-pace.md` 로 다시 만든다. 코드는 `core/BloodRain.Sim/MetaPaceSim.cs`, 생을 잇는 방식은 `MetaSim`(meta.md)과 같다.");
            sb.AppendLine();
            sb.AppendLine($"목표(10장): 첫 절정 2~4생, 첫 봉인석 3~6생, 봉인 엔딩(봉인석 {need} + 종장) 15~20시간. 시간은 서장 10분 + 생마다 버틴 분 + 객잔 1분만 센 것이라 실제 플레이는 이보다 길다(메뉴·대화·실패한 보스전·종장은 빠짐). 한 안에 {MaxLives}생까지 돌린다.");
            sb.AppendLine();

            // 원인: 지금 데이터, 첫 생 8분
            var now = Run(Variants[0], 8, dir);
            var firstStone = now.FirstOrDefault(r => r.SealStones >= 1);
            var firstHwa = now.FirstOrDefault(r => r.Realm == "화경" || r.Realm == "현경");
            var seven = now.FirstOrDefault(r => r.SealStones >= need);
            var bestLife = now.OrderByDescending(r => r.Killed.Count).First();
            int stonesAfterHwa = firstHwa == null ? 0 : now.Last().SealStones - (now.LastOrDefault(r => r.Life < firstHwa.Life)?.SealStones ?? 0);
            sb.AppendLine("## 왜 빠른가(지금 데이터, 첫 생 8분)");
            sb.AppendLine();
            sb.AppendLine($"- 봉인석은 땅의 주인을 처음 쓰러뜨릴 때 바로 하나씩 나온다. 한 생에 여럿을 잡으면 한꺼번에 모인다. 가장 많이 잡은 생은 {bestLife.Life}생({bestLife.Realm})의 {bestLife.Killed.Count}명이다.");
            if (firstHwa != null)
                sb.AppendLine($"- 화경 장막에는 일곱 땅이 모두 든다(7장). 처음 화경에 오른 {firstHwa.Life}생({firstHwa.Hours:0.0}시간) 뒤로 봉인석 {stonesAfterHwa}개가 나머지 생에서 모였다. 봉인석이 모이는 속도는 '언제 화경·현경에 닿는가'가 정한다.");
            sb.AppendLine($"- 연무장을 생마다 다 사서 버티는 분이 0.5분씩 늘고(가정), 버틴 분이 길수록 경지가 오른다. 봉인석 {need}개 때 연무장 레벨은 {seven?.TrainingLevels.ToString() ?? "—"}.");
            sb.AppendLine($"- 첫 봉인석은 {(firstStone == null ? "—" : $"{firstStone.Life}생")}으로 목표(3~6생) 안이다. 앞쪽은 맞고 뒤쪽(4개째부터)이 빠르다.");
            sb.AppendLine();

            sb.AppendLine("## 안마다");
            sb.AppendLine();
            sb.AppendLine("| 안 | 바꾸는 것 | 첫 생 버팀 | 첫 절정 | 첫 봉인석 | 봉인석 4 | 봉인석 7 | 그때 연무장 레벨 |");
            sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- |");
            var eight = new List<(Variant, List<MetaLifeRow>)>();
            foreach (var v in Variants)
                foreach (var b in MetaSim.BaseSurviveOptions)
                {
                    var rows = b == 8 && v.Id == "now" ? now : Run(v, b, dir);
                    if (b == 8) eight.Add((v, rows));
                    var s7 = rows.FirstOrDefault(r => r.SealStones >= need);
                    sb.AppendLine($"| {v.NameKo} | {v.Change} | {b:0}분 | {At(rows, r => Peak.Contains(r.Realm))} | {At(rows, r => r.SealStones >= 1)} | {At(rows, r => r.SealStones >= 4)} | {At(rows, r => r.SealStones >= need)} | {s7?.TrainingLevels.ToString() ?? "—"} |");
                }
            sb.AppendLine();
            sb.AppendLine("— 는 그 안에서 " + MaxLives + "생 안에 닿지 못함.");
            sb.AppendLine();

            sb.AppendLine("## 생마다 봉인석(첫 생 8분)");
            sb.AppendLine();
            sb.AppendLine("| 생 | " + string.Join(" | ", eight.Select(e => e.Item1.NameKo)) + " |");
            sb.AppendLine("| --- | " + string.Join(" | ", eight.Select(_ => "---")) + " |");
            int maxRows = eight.Max(e => e.Item2.Count);
            for (int i = 0; i < maxRows; i += 2)
            {
                var cells = eight.Select(e => i < e.Item2.Count ? $"{e.Item2[i].SealStones}({e.Item2[i].Hours:0.0}h, {e.Item2[i].Realm})" : "");
                sb.AppendLine($"| {i + 1} | " + string.Join(" | ", cells) + " |");
            }
            sb.AppendLine();
            sb.AppendLine("칸은 봉인석 수(누적 시간, 그 생의 경지). 두 생마다 한 줄.");
            return sb.ToString();
        }
    }
}
