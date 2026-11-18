// 회귀 가속 시제품(backlog GU-11f). 보고서: docs/design/sim/acceleration.md
//
// 한 판 시뮬레이터(RunSimulator, 진소운·평균 정책·숭산 적 값·30분)에 Core PastLifePower 를 넣고,
// 마친 생 수마다 일류·절정·현경 도달 시각을 설계안 '목표 곡선'과 견준다. 값은 고치지 않는다.
//
// 가정(이 파일에서 새로 정함):
//   - 지난 생은 모두 현경까지 올랐다고 친다(cheonha.md 에서 첫 생 현경이 20분쯤). 그러면 화경 → 현경 구간만 제 속도이고 나머지는 줄어든다.
//   - 연무장(윤회의 기)은 0 으로 둔다. 가속에서 '빠르기'만 떼어 보려는 것이다(설계안: 기는 세기, 공력은 빠르기).
//   - 감소율·상한을 바꾼 안 몇 개를 나란히 둔다. 설계안 추천은 4%·30%.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BloodRain.Core.Progression;

namespace BloodRain.Sim
{
    public static class AccelerationSim
    {
        public static readonly int[] Lives = { 0, 5, 10, 30, 60, 100 };

        /// <summary>설계안 '목표 곡선'(마친 생 → 일류·절정·현경 분). 1~5 는 6:40·11:40, 현경은 '대개 못 감'.</summary>
        public static readonly IReadOnlyDictionary<int, (string ilryu, string jeol, string hyeon)> Target = new Dictionary<int, (string, string, string)>
        {
            [0] = ("6:40", "11:40", "대개 못 감"),
            [5] = ("6:40", "11:40", "대개 못 감"),
            [10] = ("5:00", "9:30", "27:00"),
            [30] = ("3:30", "7:00", "21:00"),
            [60] = ("2:15", "5:00", "16:00"),
            [100] = ("1:30", "3:30", "12:00"),
        };

        static readonly (string name, float per, float max)[] Variants =
        {
            ("설계안 4%·상한 30%", PastLifePower.DefaultCutPerCrossing, PastLifePower.DefaultMaxCut),
            ("4%·상한 60%", 0.04f, 0.60f),
            ("1%·상한 80%", 0.01f, 0.80f),
        };

        static string M(double minute)
        {
            if (double.IsNaN(minute)) return "—";
            int sec = (int)Math.Round(minute * 60);
            return $"{sec / 60}:{sec % 60:00}";
        }

        static double Median(IEnumerable<double> xs)
        {
            var a = xs.OrderBy(x => double.IsNaN(x) ? double.MaxValue : x).ToList();
            return a[a.Count / 2];
        }

        public static (double ilryu, double jeol, double hyeon) Reach(SimData data, int lives, float per, float max, int seeds)
        {
            var realms = data.Game.Balance.Realms;
            var power = PastLifePower.AfterLives(realms, lives, "hyeongyeong", per, max);
            var runs = Enumerable.Range(1, seeds).Select(seed =>
                RunSimulator.Run(data, new SimOptions { Seed = seed, Minutes = 30, PastLife = power })).ToList();
            double At(SimResult r, string id) => r.RealmMinute.TryGetValue(id, out var m) ? m : double.NaN;
            return (Median(runs.Select(r => At(r, "ilryu"))), Median(runs.Select(r => At(r, "jeoljeong"))), Median(runs.Select(r => At(r, "hyeongyeong"))));
        }

        public static string Report(SimData data, int seeds = 30)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# 회귀 가속 시뮬레이션(GU-11f)");
            sb.AppendLine();
            sb.AppendLine("`dotnet run --project core/BloodRain.Sim -c Release -- --acceleration docs/design/sim/acceleration.md` 로 다시 만든다. 코드는 `core/BloodRain.Sim/AccelerationSim.cs`, 규칙은 Core `Progression.PastLifePower`(설계안 `docs/design/cheonha_expansion_full.md` '전생의 공력 (추천 규칙)').");
            sb.AppendLine();
            sb.AppendLine($"한 판 시뮬레이터(진소운, 정책 average, 숭산 적 값, 30분)를 시드 1~{seeds} 돌린 가운데값. 지난 생은 모두 현경까지 올랐다고 쳐서 화경 → 현경 구간만 제 속도다. 연무장은 0(공력의 '빠르기'만 떼어 봄). 값은 고치지 않았다.");
            sb.AppendLine();

            var rows = new List<(string name, int lives, (double i, double j, double h) r)>();
            foreach (var v in Variants)
                foreach (var n in Lives)
                    rows.Add((v.name, n, Reach(data, n, v.per, v.max, seeds)));

            var baseRow = rows.First(r => r.lives == 0).r;
            var design = rows.Where(r => r.name == Variants[0].name).ToList();
            double floorIlryu = design.Last().r.i;
            sb.AppendLine("## 읽을 것");
            sb.AppendLine();
            sb.AppendLine($"- 설계안 규칙(4%·상한 30%)이면 마친 생 8번째쯤 상한에 닿아 그 뒤로는 더 빨라지지 않는다. 일류 {M(baseRow.i)} → {M(floorIlryu)}로 멈추고, 목표 곡선의 30·60·100 생(3:30·2:15·1:30)에는 못 미친다. 필요 진기를 30% 줄여도 시간이 30% 주는 것은 아니다(앞부분은 화력이 진기 흐름을 정하고, 레벨이 빨리 올라도 처치가 그만큼 늘지 않는다).");
            sb.AppendLine("- 목표 곡선(100생에 일류 1:30)을 진기 감소만으로 맞추려면 상한이 훨씬 높아야 한다. 아래 두 안(상한 60%·80%)은 얼마나 가까워지는지 보려고 둔 것이고 정한 값이 아니다. 설계안 '네 층'의 나머지(시작 경지 고르기 — 30번 넘은 구간은 그 경지로 깨어남)가 100생 곡선의 큰 몫을 맡아야 한다.");
            sb.AppendLine($"- 지금 데이터는 첫 생부터 일류 {M(baseRow.i)}·절정 {M(baseRow.j)}로 7장 목표(6:40·11:40)보다 이르다(처치 진기, P-02). 그래서 '마친 생 0' 줄이 목표 곡선 1~5 줄보다 앞서 있다.");
            sb.AppendLine("- 감소율·상한을 무엇으로 할지는 사람 결정 대기(backlog GU-11f1).");
            sb.AppendLine();

            foreach (var v in Variants)
            {
                sb.AppendLine($"## {v.name}");
                sb.AppendLine();
                sb.AppendLine("| 마친 생 | 일류 | 절정 | 현경 | 목표 일류 | 목표 절정 | 목표 현경 | 일류(첫 생 대비) |");
                sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- |");
                foreach (var row in rows.Where(r => r.name == v.name))
                {
                    var t = Target[row.lives];
                    sb.AppendLine($"| {row.lives} | {M(row.r.i)} | {M(row.r.j)} | {M(row.r.h)} | {t.ilryu} | {t.jeol} | {t.hyeon} | {row.r.i / baseRow.i:0%} |");
                }
                sb.AppendLine();
            }
            sb.AppendLine("'—'는 30분 안에 못 닿음. 목표 곡선은 설계안의 추천(시뮬레이션 전) 값이다.");
            return sb.ToString();
        }
    }
}
