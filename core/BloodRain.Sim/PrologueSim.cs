// 서장(청운진 객잔 마당 10분)이 기획서 4장 '서장 상세 흐름' 표의 시각대로 흘러가는지(밸런스 시뮬레이션). 보고서: docs/design/sim/prologue.md
//
// RunSimulator 를 SimOptions.Prologue 로 돌린다. 적 종류·수·사건은 Core PrologueDirector(chapters.json prologue) 그대로,
// 적 기혈은 청운진 혈운 농도(cheonha.json, 0.8)로 셈한다. 진기·무공·오의·음양·돌파는 숭산 무리 모형과 같다.
// 9:00 장삼이 나오는 데까지 돌리고, 장삼전은 그 순간 단일 대상 초당 피해로 '각성까지 몇 초'만 잰다.
//
// 가정(이 파일에서 새로 정함):
//   - 장삼전 동안 화력은 9분 값 그대로(장삼전 중 레벨업은 넣지 않는다). 패턴 피하느라 못 치는 몫은 CheonhaSim.BossUptime(0.7).
//   - 장삼 기혈은 enemies.json health 그대로(Core Prologue.BossMaxHealth). 각성은 기혈이 bossHealthFloor(10%)에 닿을 때.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BloodRain.Core.Run;

namespace BloodRain.Sim
{
    public sealed class PrologueRow
    {
        public string Policy = "";
        public int Runs;
        public double FirstLevelUp, FirstUltimate, Iryu, Ilryu, OffHarmony, EliteKilled, Boss;
        public int IryuReached, IlryuReached, EliteKilledRuns, OffHarmonyRuns;
        public double Level9, SingleDps9, AwakenSeconds;
        /// <summary>9분에 이미 절정(다음 경지)에 닿은 판 수. 서장은 일류까지만 겪게 한다.</summary>
        public int JeoljeongBefore9;
    }

    public static class PrologueSim
    {
        /// <summary>장삼이 나오는 시각(분). chapters.json prologue boss 사건과 같다(테스트가 확인).</summary>
        public const double BossMinute = 9;

        /// <summary>기획서 4장 표의 시각(분). 2026-11-01 T-43 에서 이 시뮬레이션에 맞춰 고쳤다(테스트가 표와 같은지 본다).</summary>
        public const double TargetFirstLevelUp = 15 / 60.0, TargetUltimate = 2, TargetIryu = 2.75, TargetIlryu = 37 / 6.0, TargetOffHarmony = 10 / 3.0;
        /// <summary>6장 장삼 문단의 '9분 화력 약 210이면 각성까지 35초쯤'.</summary>
        public const double TargetJangSamDps = 210;

        /// <summary>6장 문단의 각성까지 초: 깎을 기혈 ÷ (화력 210 × 치는 시간), 5초 단위로 반올림.</summary>
        public static double JangSamTargetSeconds(double toCut) => Math.Round(toCut / (TargetJangSamDps * CheonhaSim.BossUptime) / 5) * 5;

        public static SimResult RunOne(SimData data, int seed, string policy = "average") =>
            RunSimulator.Run(data, new SimOptions { Prologue = true, Seed = seed, Policy = policy, Minutes = BossMinute, Character = data.Game.Cheonha.PrologueBody.Character });

        public static PrologueRow Measure(SimData data, string policy, int seeds) => Measure(data, policy, seeds, o => o);

        /// <summary>apply 로 조정 시험(Scenarios)의 옵션을 얹는다.</summary>
        public static PrologueRow Measure(SimData data, string policy, int seeds, Func<SimOptions, SimOptions> apply)
        {
            var g = data.Game;
            var runs = Enumerable.Range(1, seeds)
                .Select(s => RunSimulator.Run(data, apply(new SimOptions { Prologue = true, Seed = s, Policy = policy, Minutes = BossMinute, Character = g.Cheonha.PrologueBody.Character })))
                .ToList();
            double floor = g.Cheonha.PrologueBody.BossHealthFloor;
            var boss = g.Bosses.First(b => b.Id == g.Cheonha.PrologueBody.Boss);
            double toCut = Prologue.BossMaxHealth(boss) * (1 - floor);
            var r = new PrologueRow { Policy = policy, Runs = seeds };
            r.FirstLevelUp = Median(runs.Select(x => x.FirstLevelUpMinute));
            r.FirstUltimate = Median(runs.Select(x => x.FirstUltimateFullMinute));
            r.Iryu = Realm(runs, "iryu", out r.IryuReached);
            r.Ilryu = Realm(runs, "ilryu", out r.IlryuReached);
            r.OffHarmony = Median(runs.Select(x => x.FirstOffHarmonyMinute), out r.OffHarmonyRuns);
            r.EliteKilled = Median(runs.Select(x => x.EliteKilledMinute), out r.EliteKilledRuns);
            r.Boss = Median(runs.Select(x => x.BossMinute));
            r.Level9 = Median(runs.Select(x => (double)x.LevelAt((int)BossMinute)));
            r.SingleDps9 = Median(runs.Select(x => x.At((int)BossMinute).SingleDps));
            r.AwakenSeconds = Median(runs.Select(x => toCut / (x.At((int)BossMinute).SingleDps * CheonhaSim.BossUptime)));
            r.JeoljeongBefore9 = runs.Count(x => x.RealmMinute.ContainsKey("jeoljeong"));
            return r;
        }

        static double Realm(List<SimResult> runs, string id, out int reached)
        {
            var xs = runs.Where(x => x.RealmMinute.ContainsKey(id)).Select(x => x.RealmMinute[id]).ToList();
            reached = xs.Count;
            return Median(xs);
        }

        /// <summary>첫 몇 분 동안 화면 위 적 수: 기획서 11장 '화면 위 적 수 목표'(balance.json spawnTarget) 대 시뮬레이션(VF-02).</summary>
        public const int DensityMinutes = 3;

        /// <summary>분마다 (목표, 살아 있는 적 가운데값, 무리에 닿은 적 가운데값, 누적 처치 가운데값). 평균 정책.</summary>
        public static List<(int Minute, double Target, double Alive, double InCrowd, double Kills)> Density(SimData data, int seeds)
        {
            var g = data.Game;
            var curve = Core.Spawning.WaveTable.From(g).Curve(Core.Spawning.WaveTable.Prologue, g.Balance.SpawnTarget);
            var runs = Enumerable.Range(1, seeds).Select(s => RunOne(data, s)).ToList();
            var list = new List<(int, double, double, double, double)>();
            for (int m = 1; m <= DensityMinutes; m++)
            {
                var snaps = runs.Select(r => r.Minutes.First(x => x.Minute == m)).ToList();
                list.Add((m, curve.TargetAt(m), Median(snaps.Select(x => (double)x.Alive)), Median(snaps.Select(x => (double)x.InCrowd)), Median(snaps.Select(x => (double)x.Kills))));
            }
            return list;
        }

        static void AppendDensity(StringBuilder sb, SimData data, int seeds)
        {
            var g = data.Game;
            int cluster = g.Cheonha.RealmScale.First().ClusterSize;
            sb.AppendLine($"## 첫 {DensityMinutes}분 화면 위 적 수(평균 정책, 가운데값)");
            sb.AppendLine();
            sb.AppendLine($"목표는 기획서 11장 '화면 위 적 수 목표'(balance.json `spawnTarget.points`). 서장은 삼류라 덩이 하나가 {cluster}명이다. '살아 있는 적'은 생성 디렉터가 세는 수(목표에 모자라면 매초 부족분의 {(g.Balance.SpawnTarget.SpawnFractionPerSecond * 100).ToString("0", CultureInfo.InvariantCulture)}%를 만든다), '무리에 닿은 적'은 생긴 뒤 다가와 맞을 수 있게 된 적. 적 기혈은 청운진 혈운 농도를 곱한다. 값은 바꾸지 않고 견주기만 한다(VF-02).");
            sb.AppendLine();
            sb.AppendLine("| 분 | 목표 | 살아 있는 적 | 목표 대비 | 무리에 닿은 적 | 누적 처치 |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var r in Density(data, seeds))
                sb.AppendLine($"| {r.Minute}:00 | {F0(r.Target)} | {F0(r.Alive)} | {(r.Target > 0 ? (r.Alive / r.Target * 100).ToString("0", CultureInfo.InvariantCulture) + "%" : "-")} | {F0(r.InCrowd)} | {F0(r.Kills)} |");
            sb.AppendLine();
            var rows = Density(data, seeds);
            var st = g.Balance.SpawnTarget;
            string P(double v) => (v * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
            sb.AppendLine($"- 살아 있는 적은 목표의 {P(rows.Min(r => r.Alive / r.Target))}~{P(rows.Max(r => r.Alive / r.Target))}. 시강이 금방 죽어서(3분까지 {F0(rows.Last().Kills)}마리) 매초 부족분의 일부만 채우는 셈이 늘 한 박자 늦다.");
            sb.AppendLine($"- 무리에 닿은 적은 그보다 훨씬 적다. 새 적은 화면 바깥 고리(대각선 {st.RingMinDiagonal.ToString("0.0#", CultureInfo.InvariantCulture)}~{st.RingMaxDiagonal.ToString("0.0#", CultureInfo.InvariantCulture)}배)에서 걸어오는데 닿자마자 베인다. 화면이 비어 보인다면 이 몫이다. 값을 바꿀지는 사람 결정(decisions.md VF-02a).");
            sb.AppendLine();
        }

        static double Median(IEnumerable<double> xs) => Median(xs, out _);

        static double Median(IEnumerable<double> xs, out int count)
        {
            var a = xs.Where(x => !double.IsNaN(x)).OrderBy(x => x).ToList();
            count = a.Count;
            if (a.Count == 0) return double.NaN;
            return a.Count % 2 == 1 ? a[a.Count / 2] : (a[a.Count / 2 - 1] + a[a.Count / 2]) / 2;
        }

        /// <summary>분을 m:ss 로.</summary>
        public static string Clock(double minute)
        {
            if (double.IsNaN(minute)) return "-";
            int s = (int)Math.Round(minute * 60);
            return $"{s / 60}:{s % 60:00}";
        }

        static string Cell(double minute, int reached, int runs) =>
            reached == 0 ? "-" : reached == runs ? Clock(minute) : $"{Clock(minute)} ({reached}/{runs})";

        static string F0(double x) => double.IsNaN(x) ? "-" : x.ToString("0", CultureInfo.InvariantCulture);

        /// <summary>scenarios 면 조정 시험(Scenarios.All)마다 dataDir 에서 데이터를 새로 읽어 평균 정책으로 나란히 놓는다(dataDir null 이면 기본 데이터 폴더).</summary>
        public static string Report(SimData data, int seeds = 30, string? dataDir = null, bool scenarios = false)
        {
            var g = data.Game;
            var rows = new[] { "average", "greedy", "slow" }.Select(p => Measure(data, p, seeds)).ToList();
            var boss = g.Bosses.First(b => b.Id == g.Cheonha.PrologueBody.Boss);
            double floor = g.Cheonha.PrologueBody.BossHealthFloor;
            double density = g.Cheonha.Region(g.Cheonha.PrologueBody.Region).Density;
            var sb = new StringBuilder();
            sb.AppendLine("# 서장 흐름 시각");
            sb.AppendLine();
            sb.AppendLine("`dotnet run --project core/BloodRain.Sim -c Release -- --prologue docs/design/sim/prologue.md` 로 다시 쓴다. 코드 `core/BloodRain.Sim/PrologueSim.cs`.");
            sb.AppendLine($"RunSimulator 를 서장 모드로 9:00(장삼 등장)까지 × 정책 셋 × 시드 {seeds}. 적 종류·수·사건은 Core `PrologueDirector`(chapters.json prologue: 시강만, 3:30 혈인 돌진, 5:00 강시 도부수, 8:30 생성 멈춤), 적 기혈은 청운진 혈운 농도 {density.ToString("0.0#", CultureInfo.InvariantCulture)}. 진기·무공·오의·음양·돌파는 숭산 무리 모형(level-curve.md)과 같다.");
            sb.AppendLine();
            sb.AppendLine("## 기획서 4장 표와 나란히(가운데값, m:ss)");
            sb.AppendLine();
            sb.AppendLine("일부 판만 닿았으면 (닿은 판/전체).");
            sb.AppendLine();
            sb.AppendLine("| 일 | 기획서 | " + string.Join(" | ", rows.Select(r => r.Policy)) + " |");
            sb.AppendLine("|---|---|" + string.Concat(rows.Select(_ => "---|")));
            void Line(string name, double target, Func<PrologueRow, string> cell) =>
                sb.AppendLine($"| {name} | {Clock(target)} | " + string.Join(" | ", rows.Select(cell)) + " |");
            Line("첫 레벨업", TargetFirstLevelUp, r => Clock(r.FirstLevelUp));
            Line("오의 게이지 처음 참", TargetUltimate, r => Clock(r.FirstUltimate));
            Line("이류 돌파", TargetIryu, r => Cell(r.Iryu, r.IryuReached, r.Runs));
            Line("강시 도부수 처치(5:00 등장)", 5, r => Cell(r.EliteKilled, r.EliteKilledRuns, r.Runs));
            Line("일류 돌파", TargetIlryu, r => Cell(r.Ilryu, r.IlryuReached, r.Runs));
            Line("음양 처음 기욺(태극 벗어남)", TargetOffHarmony, r => Cell(r.OffHarmony, r.OffHarmonyRuns, r.Runs));
            sb.AppendLine();
            sb.AppendLine("## 장삼전(9:00)");
            sb.AppendLine();
            sb.AppendLine($"장삼 기혈 {boss.Health:0}(혈운 농도·시간 배율 없음, Core `Prologue.BossMaxHealth`). 기혈이 {floor * 100:0}%에 닿으면 각성한다. 그때까지 깎을 {boss.Health * (1 - floor):0} ÷ (9분 단일 대상 초당 피해 × 치는 시간 {CheonhaSim.BossUptime:0.0}). 기획서 6장은 '9분 화력 약 {F0(TargetJangSamDps)}이면 각성까지 {F0(JangSamTargetSeconds(boss.Health * (1 - floor)))}초쯤'(이 시뮬레이션에 맞춤, 치는 시간 {CheonhaSim.BossUptime:0.0} 포함).");
            sb.AppendLine();
            sb.AppendLine("| 정책 | 9분 레벨 | 9분 단일 대상 초당 피해 | 각성까지(초) | 9분 전에 절정 |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var r in rows)
                sb.AppendLine($"| {r.Policy} | {F0(r.Level9)} | {F0(r.SingleDps9)} | {F0(r.AwakenSeconds)} | {r.JeoljeongBefore9}/{r.Runs} |");
            sb.AppendLine();
            if (scenarios)
            {
                sb.AppendLine("## 조정 시험을 얹으면(평균 정책)");
                sb.AppendLine();
                sb.AppendLine("level-curve.md·decisions.md 와 같은 조정 시험(`Scenarios.cs`). 서장 모양을 표에 맞추려면 어느 손잡이가 듣는지 보려고 붙였다. 처치 진기 줄이기는 시강·혈인을 1 그대로 두니 서장에는 거의 안 듣는다.");
                sb.AppendLine();
                sb.AppendLine("| 조정 시험 | 첫 레벨업 | 오의 | 이류 | 일류 | 9분 레벨 | 장삼 각성까지(초) |");
                sb.AppendLine("|---|---|---|---|---|---|---|");
                sb.AppendLine($"| 기획서 4장 | {Clock(TargetFirstLevelUp)} | {Clock(TargetUltimate)} | {Clock(TargetIryu)} | {Clock(TargetIlryu)} | - | {F0(JangSamTargetSeconds(boss.Health * (1 - floor)))} (화력 {F0(TargetJangSamDps)}) |");
                foreach (var sc in Scenarios.All)
                {
                    var d = sc.LoadData(dataDir);
                    var r = Measure(d, "average", seeds, sc.ApplyOptions);
                    sb.AppendLine($"| {sc.NameKo} | {Clock(r.FirstLevelUp)} | {Clock(r.FirstUltimate)} | {Cell(r.Iryu, r.IryuReached, r.Runs)} | {Cell(r.Ilryu, r.IlryuReached, r.Runs)} | {F0(r.Level9)} | {F0(r.AwakenSeconds)} |");
                }
                sb.AppendLine();
            }
            AppendDensity(sb, data, seeds);
            sb.AppendLine("## 읽는 법");
            sb.AppendLine();
            var avg = rows[0];
            sb.AppendLine($"- 평균 정책 기준 이류 {Clock(avg.Iryu)}(기획서 {Clock(TargetIryu)}), 일류 {Clock(avg.Ilryu)}({Clock(TargetIlryu)}). 돌파 레벨은 balance.json realms 이고 진기는 숭산과 같은 처치 진기(시강 1)를 쓴다.");
            sb.AppendLine($"- 첫 레벨업 {Clock(avg.FirstLevelUp)}(기획서 {Clock(TargetFirstLevelUp)}), 오의 게이지 {Clock(avg.FirstUltimate)}({Clock(TargetUltimate)}). 오의 게이지는 처치마다 차서 서장처럼 시강만 나오는 판에서는 처치 수가 곧 시각이다.");
            sb.AppendLine($"- 음양은 {avg.OffHarmonyRuns}/{avg.Runs}판에서 9분 안에 태극을 벗어났다. 기획서는 설명 문구를 시각이 아니라 '처음 기울 때'(가운데 {Clock(TargetOffHarmony)}쯤)에 붙인다.");
            sb.AppendLine($"- 장삼전 각성까지 평균 정책 {F0(avg.AwakenSeconds)}초. 서장 장삼은 처치 목표 {boss.TargetKillSeconds:0}초(enemies.json targetKillSeconds)인데, 각성 전 90%를 깎는 데 이만큼 걸리고 각성 뒤 남은 10%를 절기로 마저 친다.");
            if (scenarios)
                sb.AppendLine("- 조정 시험 가운데 서장 시각을 움직이는 것은 생성 채움 비율(0.3 → 0.1)뿐이다. 이류가 3:20 근처로 늦어지고 일류도 1분쯤 늦어지며 장삼 각성도 길어진다. 서장만 따로 바꾸려면 chapters.json prologue 에 채움 비율을 따로 두는 길이 있다(지금은 balance.json 하나를 같이 쓴다).");
            sb.AppendLine("- 빠진 것: 장삼전 동안 레벨업·잡몹 진기, 장삼 패턴(포승줄 묶임 1초·호루라기 시강 소환), 덩이(이류부터 4명)의 그림. 정책은 사람이 아니라 정해진 규칙으로 고른다.");
            return sb.ToString();
        }
    }
}
