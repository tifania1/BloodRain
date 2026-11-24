// 문파 병기: 한 생에 병기를 몇 번 줍고 어느 등급까지 가나(backlog GU-10c). 보고서: docs/design/sim/gear.md
//
// CheonhaSim 의 한 생(평균 정책, 서장만 마친 혼, 성질 없음, 30분)을 문파마다 시드 1~N 으로 돌린 뒤,
// 그 생의 분마다 서 있던 지역·처치 수·땅의 주인 처치 시각을 받아 병기 칸(Core GearSlot)을 따로 굴린다.
//
// 가정(이 파일에서 새로 정함):
//   - 정예는 5·10·15·20·25분에 나오고 모두 쓰러뜨린다. 무엇이 나오는지는 Core RegionSpawns.Elite(그때 서 있던 지역, n).
//     떠도는 정예(강시 도부수)는 병기와 상관없어 뺀다.
//   - 기연 0(처음 혼). 무명 철검 유품(원각 계정 첫 처치)은 뺀다 — 혼마다 한 번뿐이라 보통 생의 분포를 흐린다(아래 '따로 볼 것').
//   - '강시가 된 그 문파 제자' 일반 적(decisions #60 추천)은 아직 데이터에 없다. 시험 C 는 그 문파 땅에서 쓰러뜨린 잡몹 가운데
//     혈강시 몫(그 분 생성 띠의 hyeol_gangsi 무게 ÷ 전체 무게)을 그 적으로 본다(추천안이 '기혈·피해·움직임은 혈강시 값 그대로'라서).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BloodRain.Core.Build;
using BloodRain.Core.Spawning;

namespace BloodRain.Sim
{
    public static class GearSim
    {
        public static readonly string[] Sects = { "shaolin", "gaebang", "namgung" };
        static readonly int[] EliteMinutes = { 5, 10, 15, 20, 25 };
        public const string StandIn = "hyeol_gangsi";

        public sealed class Scenario
        {
            public string Name = "";
            /// <summary>정예 확정 드랍을 한 생에 한 번만.</summary>
            public bool GuaranteedOnce;
            /// <summary>그 문파 땅 혈강시 몫을 '강시가 된 제자'로 보고 이 확률로 떨어뜨린다. 0 이면 끔.</summary>
            public double StandInChance;
        }

        public sealed class LifeGear
        {
            public int Picks, FinalGrade = -1, BossGradeUps;
            public double StandInKills;
            public string? GearId;
            public double FirstPickMinute = double.NaN, SinMinute = double.NaN;
        }

        /// <summary>한 생의 기록에 시험 하나를 얹어 병기 칸을 굴린다.</summary>
        public static LifeGear Roll(SimData data, CheonhaLifeResult life, Scenario sc, int seed)
        {
            var g = data.Game;
            var spawns = new RegionSpawns(g);
            var flow = g.Chapters.First(c => c.Id == Core.Spawning.WaveTable.Life);
            var slot = new GearSlot();
            var rng = new Random(seed * 7919 + 17);
            var got = new HashSet<string>();
            var res = new LifeGear();

            string RegionAt(int minute) =>
                life.RegionByMinute.Count == 0 ? "" : life.RegionByMinute[Math.Min(minute, life.RegionByMinute.Count - 1)];
            string? SectOf(string regionId) => g.Cheonha.Regions.FirstOrDefault(r => r.Id == regionId)?.Sect;

            void Pick(string gearId, double minute)
            {
                slot.Pick(g, gearId);
                res.Picks++;
                if (double.IsNaN(res.FirstPickMinute)) res.FirstPickMinute = minute;
                if (slot.Grade == g.Gear.Grades.Count - 1 && double.IsNaN(res.SinMinute)) res.SinMinute = minute;
            }

            var bossKills = life.Bosses.Where(b => !double.IsNaN(b.KilledAt)).OrderBy(b => b.KilledAt).ToList();
            int bi = 0;
            int nth = 0;
            for (int m = 0; m < life.KillsByMinute.Count; m++)
            {
                string region = RegionAt(m);
                string? sect = SectOf(region);
                // 그 분 잡몹 가운데 강시가 된 제자 몫
                if (sc.StandInChance > 0 && sect != null && Gear.OfSect(g, sect) != null)
                {
                    var band = flow.Bands.FirstOrDefault(b => m >= b.FromMinute && m < b.ToMinute) ?? flow.Bands.Last();
                    double share = band.TotalWeight > 0 ? band.WeightOf(StandIn) / band.TotalWeight : 0;
                    int n = (int)Math.Round(life.KillsByMinute[m] * share);
                    res.StandInKills += n;
                    var gear = Gear.OfSect(g, sect)!;
                    for (int i = 0; i < n; i++)
                        if (rng.NextDouble() < sc.StandInChance) Pick(gear.Id, m + (i + 0.5) / Math.Max(1, n));
                }
                // 분이 끝날 때까지의 땅의 주인 처치
                while (bi < bossKills.Count && bossKills[bi].KilledAt < m + 1)
                {
                    var bs = SectOf(bossKills[bi].RegionId);
                    if (slot.GradeUpIfSect(g, bs))
                    {
                        res.BossGradeUps++;
                        if (slot.Grade == g.Gear.Grades.Count - 1 && double.IsNaN(res.SinMinute)) res.SinMinute = bossKills[bi].KilledAt;
                    }
                    bi++;
                }
                // 정예(그 분 끝에 나온다고 본다: 5분째 = m 4 끝)
                if (EliteMinutes.Contains(m + 1))
                {
                    string at = RegionAt(m + 1);
                    if (spawns.HasElites(at))
                    {
                        nth++;
                        string elite = spawns.Elite(at, nth);
                        var gear = Gear.OfSect(g, SectOf(at));
                        if (gear != null && gear.Guaranteed.Contains(elite) && !(sc.GuaranteedOnce && got.Contains(gear.Id)))
                        {
                            got.Add(gear.Id);
                            Pick(gear.Id, m + 1);
                        }
                    }
                }
            }
            res.GearId = slot.GearId;
            res.FinalGrade = slot.Grade;
            return res;
        }

        public static string Report(SimData data, int seeds = 30)
        {
            var g = data.Game;
            var scenarios = new List<Scenario>
            {
                new Scenario { Name = "A 지금 데이터(정예 확정 매번, 일반 적 없음)" },
                new Scenario { Name = "B 정예 확정은 한 생에 한 번(sinbyeong 글)", GuaranteedOnce = true },
            };
            foreach (var p in new[] { 0.0003, 0.0005, 0.001, 0.0015 })
                scenarios.Add(new Scenario { Name = $"C B + 강시가 된 제자 {p * 100:0.##}%", GuaranteedOnce = true, StandInChance = p });

            var lives = new Dictionary<string, List<CheonhaLifeResult>>();
            foreach (var s in Sects)
                lives[s] = Enumerable.Range(1, seeds).Select(seed => CheonhaSim.Run(data, s, seed)).ToList();

            var sb = new StringBuilder();
            sb.AppendLine("# 문파 병기 시뮬레이션 (GU-10c)");
            sb.AppendLine();
            sb.AppendLine($"`dotnet run --project core/BloodRain.Sim -c Release -- --gear docs/design/sim/gear.md` 로 다시 쓴다. 문파마다 시드 1~{seeds}, CheonhaSim 한 생(평균 정책, 처음 혼, 30분).");
            sb.AppendLine("가정은 `core/BloodRain.Sim/GearSim.cs` 머리말. 설계는 `docs/design/sinbyeong.md`(운이 보통이면 양병~명병, 잘 풀린 생에만 신병).");
            sb.AppendLine();
            sb.AppendLine("## 한 생에 서 있던 땅과 처치");
            sb.AppendLine();
            sb.AppendLine("| 몸 | 30분 처치(가운데) | 제 문파 땅에 선 분(가운데) | 문파 땅(어느 문파든) 혈강시 몫 처치(가운데) | 제 땅 정예 넷째까지 본 생 |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var s in Sects)
            {
                var sect = g.Cheonha.Sect(s);
                var ls = lives[s];
                var kills = ls.Select(l => l.KillsByMinute.Sum()).OrderBy(x => x).ToList();
                var home = ls.Select(l => l.RegionByMinute.Skip(1).Count(r => r == sect.Region)).OrderBy(x => x).ToList();
                var standIn = ls.Select(l => Roll(data, l, new Scenario { StandInChance = 1e-12, GuaranteedOnce = true }, l.Seed).StandInKills).OrderBy(x => x).ToList();
                int homeElites = ls.Count(l => EliteMinutes.Count(em => em < l.RegionByMinute.Count && l.RegionByMinute[em] == sect.Region) >= 4);
                sb.AppendLine($"| {sect.NameKo} | {kills[kills.Count / 2]:N0} | {home[home.Count / 2]} | {standIn[standIn.Count / 2]:N0} | {homeElites}/{seeds} |");
            }
            sb.AppendLine();
            sb.AppendLine("## 끝 등급 분포");
            sb.AppendLine();
            sb.AppendLine("칸은 그 등급으로 생을 마친 비율. '신병' 은 6단의 상한이다(6단은 대표 무공이 절기로 깨어 있어야 함).");
            sb.AppendLine();
            var grades = g.Gear.Grades;
            sb.AppendLine("| 시험 | 몸 | 빈손 | " + string.Join(" | ", grades.Select(x => x.NameKo)) + " | 줍기(평균) | 신병 된 분(가운데) |");
            sb.AppendLine("|---|---|---|" + string.Concat(grades.Select(_ => "---|")) + "---|---|");
            foreach (var sc in scenarios)
                foreach (var s in Sects)
                {
                    var rolls = lives[s].Select(l => Roll(data, l, sc, l.Seed)).ToList();
                    string Pct(int grade) => $"{100.0 * rolls.Count(r => r.FinalGrade == grade) / rolls.Count:0}%";
                    var sinAt = rolls.Where(r => !double.IsNaN(r.SinMinute)).Select(r => r.SinMinute).OrderBy(x => x).ToList();
                    string sin = sinAt.Count == 0 ? "-" : $"{sinAt[sinAt.Count / 2]:0.0}";
                    sb.AppendLine($"| {sc.Name} | {g.Cheonha.Sect(s).NameKo} | {Pct(-1)} | " + string.Join(" | ", Enumerable.Range(0, grades.Count).Select(Pct))
                        + $" | {rolls.Average(r => r.Picks):0.0} | {sin} |");
                }
            sb.AppendLine();
            sb.AppendLine("## 읽기");
            sb.AppendLine();
            sb.AppendLine("- 세 몸의 처치 수가 같은 것은 CheonhaSim 이 개방·남궁 출생도 숭산(chapter1) 적 값으로 돌리기 때문이다(cheonha.md 가정). 몸마다 다른 것은 어느 땅에 서 있었나뿐이다.");
            sb.AppendLine("- 시험 A(지금 데이터): 병기는 숭산 정예 둘째(계율원 강시 승려)·셋째(강시 방장 대리)에게서만 나온다. 생 대부분이 깨어난 땅의 주인을 찾아 숭산을 떠나 넷째·다섯째 정예는 다른 땅에서 보니, 금강 염주 양병~명병에서 멈추고 신병은 0. 6단은 지금 데이터로는 나오지 않는다.");
            sb.AppendLine("- 개방·남궁 땅에는 병기 `guaranteed`·`droppers` 가 비어 있어 제 땅에서는 병기가 나오지 않는다. 숭산으로 걸어가 만난 정예가 떨군 금강 염주만 쥔다(소림 무공에만 듦). 남궁 몸은 숭산에서 멀어 80% 가 빈손.");
            sb.AppendLine("- 시험 B: 기획서 8장은 '정예는 확정', sinbyeong.md 는 '한 번 확정' 이라 다르다. 지금 Core 는 매번(A). 한 번으로 하면 정예만으로는 양병이 끝이다.");
            sb.AppendLine($"- 시험 C: 문파 땅에서 혈강시 몫이 한 생에 1만 마리 남짓이라 설계안이 짐작한 1,200마리의 아홉 배쯤이다. 그래서 0.15% 는 한 생에 열여섯 번쯤 줍고 절반 넘게 신병이 된다. '운이 보통이면 양병~명병, 잘 풀린 생에만 신병'에 가까운 것은 0.03~0.05%.");
            sb.AppendLine("- 시험 C 의 확률 줄은 decisions #60 이 추천대로 정해졌을 때 고를 값의 폭이다.");
            sb.AppendLine();
            sb.AppendLine("## 따로 볼 것");
            sb.AppendLine();
            sb.AppendLine("- 무명 철검 유품: 원각 계정 첫 처치에 떨어진다. 칸이 하나라 금강 염주를 쥔 소림 생이 그것을 주우면 범병 철검으로 바뀐다(Core `GearSlot.Pick`). 화면에서 자동으로 줍는다면 그 생은 병기를 잃는 셈이니 Unity GU-10U 에서 '쥔 것보다 낮으면 묻고 줍기'나 '유품은 문파 없는 몸만' 같은 길이 필요하다.");
            return sb.ToString();
        }
    }
}
