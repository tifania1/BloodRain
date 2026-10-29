// 보고서(docs/design/sim/level-curve.md)를 만든다. 숫자는 전부 이 자리에서 돌린 결과이고,
// 문장 속 숫자도 계산해서 넣는다. 데이터가 바뀌면 --report 로 다시 만들면 된다.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BloodRain.Core.Progression;

namespace BloodRain.Sim
{
    public static class Report
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>테스트와 보고서가 같이 쓰는 허용 띠(목표 레벨 ±25%). 근거는 보고서 '가정 민감도'.</summary>
        public const double Tolerance = 0.25;

        /// <summary>추천안(보고서 '조정 제안' 1+2). 민감도와 테스트도 이걸 쓴다.</summary>
        public const string RecommendedId = "kill-xp+pair2";

        /// <summary>기획서 11장 '플레이어 화력 목표' 1장 열. 데이터 파일에는 없는 숫자다.</summary>
        static readonly (int Minute, double Dps)[] DpsTargets = { (5, 80), (10, 300), (15, 800), (20, 1500) };
        /// <summary>기획서 11장 경지 도달 시점(분)과 8장 절기 시점.</summary>
        static readonly (string Id, string NameKo, double Minute)[] RealmTargets =
        {
            ("iryu", "이류", 3.5), ("ilryu", "일류", 7), ("jeoljeong", "절정", 11.5), ("chojeoljeong", "초절정", 16.5), ("hwagyeong", "화경", double.NaN),
        };
        static readonly double[] AwakenTargets = { 11.5, 16.5, double.NaN };

        static readonly (string NameKo, Func<SimOptions, SimOptions> Change)[] Sensitivity =
        {
            ("기본값", o => o),
            ("무리 반경 6m", o => o.With(x => x.CrowdRadius = 6)),
            ("무리 반경 11m", o => o.With(x => x.CrowdRadius = 11)),
            ("도착 5초", o => o.With(x => x.ArrivalSeconds = 5)),
            ("도착 10초", o => o.With(x => x.ArrivalSeconds = 10)),
            ("최대 밀도 0.6", o => o.With(x => x.MaxDensity = 0.6)),
        };

        // ───────── 한 판, 여러 판 요약(콘솔용) ─────────

        public static string SingleRun(SimResult r)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"정책 {r.Options.Policy}, 시드 {r.Options.Seed}");
            sb.AppendLine();
            sb.AppendLine("| 분 | Lv | 경지 | 누적 처치 | 화면 적 | 무리 안 | 화력(단일) | 무리 피해/초 | 절기 | 빌드 |");
            sb.AppendLine("|---:|---:|---|---:|---:|---:|---:|---:|---:|---|");
            foreach (var m in r.Minutes)
                sb.AppendLine(string.Format(Inv, "| {0} | {1} | {2} | {3:N0} | {4} | {5} | {6:N0} | {7:N0} | {8} | {9} |",
                    m.Minute, m.Level, m.RealmKo, m.Kills, m.Alive, m.InCrowd, m.SingleDps, m.CrowdDamagePerSecond, m.Evolved, m.Build));
            sb.AppendLine();
            foreach (var a in r.Awakenings)
                sb.AppendLine(string.Format(Inv, "절기 {0}: {1:0.0}분 Lv{2} ({3})", a.NameKo, a.Minute, a.Level, a.Trigger));
            foreach (var kv in r.RealmMinute)
                sb.AppendLine(string.Format(Inv, "경지 {0}: {1:0.0}분", kv.Key, kv.Value));
            sb.AppendLine($"오의 {r.Ultimates}번");
            return sb.ToString();
        }

        public static string Summary(IReadOnlyList<SimResult> runs)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"정책 {runs[0].Options.Policy}, 시드 {runs.Count}개");
            foreach (int min in new[] { 5, 10, 15, 20 })
                sb.AppendLine(string.Format(Inv, "{0}분: {1}, 화력(단일) {2:N0}, 화면 적 {3:N0}, 처치 {4:N0}",
                    min, LevelCell(runs, min), Median(runs.Select(r => r.At(min).SingleDps)),
                    Median(runs.Select(r => (double)r.At(Math.Min(min, 19)).Alive)), Median(runs.Select(r => (double)r.At(min).Kills))));
            foreach (var t in RealmTargets)
                sb.AppendLine($"{t.NameKo}: {MinuteText(RealmMinutes(runs, t.Id))}");
            for (int i = 0; i < 3; i++)
                sb.AppendLine($"절기 {i + 1}: {MinuteText(AwakenMinutes(runs, i))}");
            return sb.ToString();
        }

        // ───────── 보고서 ─────────
        // 문장 속 숫자 바로 뒤에는 조사를 붙이지 않는다(숫자에 따라 '로/으로'가 갈려서). 숫자는 괄호나 쉼표 뒤로 뺀다.

        public static string Build(string? dataDir, int seeds = 30, int scenarioSeeds = 20, int sensitivitySeeds = 10)
        {
            var baseData = SimData.Load(dataDir);
            var g = baseData.Game;
            var targets = baseData.LevelTargets;
            var realms = new Realms(g.Balance.Realms);
            var spawn = new BloodRain.Core.Spawning.SpawnCurve(g.Balance.SpawnTarget);

            List<SimResult> RunMany(Scenario sc, string policy, int n, Func<SimOptions, SimOptions>? change = null)
            {
                var data = sc.LoadData(dataDir);
                var list = new List<SimResult>();
                for (int s = 1; s <= n; s++)
                {
                    var opt = sc.ApplyOptions(new SimOptions { Policy = policy, Seed = s });
                    if (change != null) opt = change(opt);
                    list.Add(RunSimulator.Run(data, opt));
                }
                return list;
            }

            var byPolicy = Policies.All.ToDictionary(p => p, p => RunMany(Scenarios.Baseline, p, seeds));
            var avg = byPolicy["average"];
            var byScenario = Scenarios.All.Where(s => s.Id != Scenarios.Baseline.Id)
                .ToDictionary(s => s.Id, s => RunMany(s, "average", scenarioSeeds));
            var rec = Scenarios.Find(RecommendedId);
            var recRuns = byScenario[RecommendedId];
            var recByPolicy = Policies.All.ToDictionary(p => p, p => p == "average" ? recRuns : RunMany(rec, p, scenarioSeeds));
            var sens = Sensitivity.Select(v => (v.NameKo,
                Base: RunMany(Scenarios.Baseline, "average", sensitivitySeeds, v.Change),
                Rec: RunMany(rec, "average", sensitivitySeeds, v.Change))).ToList();

            var sb = new StringBuilder();
            void L(string s = "") => sb.AppendLine(s);
            string Lv4(IReadOnlyList<SimResult> runs) => "Lv" + string.Join("·", targets.Select(t => MedianLevel(runs, t.Minute)));
            string Minutes4 = string.Join("·", targets.Select(t => t.Minute)) + "분";

            L("# 레벨 곡선 시뮬레이션 (B-07)");
            L();
            L("> 이 문서는 `core/BloodRain.Sim` 이 만든다. 손으로 고치지 말고 데이터나 가정을 바꾼 뒤 다시 만든다.");
            L("> `dotnet run --project core/BloodRain.Sim -c Release -- --report docs/design/sim/level-curve.md`");
            L($"> 진소운, 1장, 처음 열린 무공 8종, 연무장 없음. 정책마다 {seeds}판(시드 1~{seeds}), 조정 시험은 {scenarioSeeds}판, 민감도는 {sensitivitySeeds}판의 중앙값.");
            L("> 돌리는 법과 모형 설명: [README.md](README.md). 가정 값의 원본: `core/BloodRain.Sim/Assumptions.cs`.");
            L();

            // ── 요약
            L("## 요약");
            L();
            L($"- 지금 데이터(처치 진기는 프로토 값)로 평균 빌드의 레벨은 {Minutes4} {Lv4(avg)}. " +
              $"목표(Lv{string.Join("·", targets.Select(t => t.Level))}) 대비 {string.Join(" / ", targets.Select(t => Pct(MedianLevel(avg, t.Minute), t.Level)))}.");
            var outside = targets.Where(t => !InBand(MedianLevel(avg, t.Minute), t.Level)).Select(t => $"{t.Minute}분").ToList();
            L(outside.Count == 0
                ? $"- 네 시점 모두 목표 ±{Tolerance * 100:0}% 안이다."
                : $"- 목표 ±{Tolerance * 100:0}% 띠를 벗어나는 시점: {string.Join(", ", outside)}. 세 정책 모두 비슷하게 높다.");
            var mid = Enumerable.Range(10, 9).ToList(); // 10~18분
            var fills = mid.Select(m => Median(avg.Select(r => (double)r.At(m).Alive)) / spawn.TargetAt(m)).ToList();
            var crowds = mid.Select(m => Median(avg.Select(r => (double)r.At(m).InCrowd))).ToList();
            L(string.Format(Inv, "- 10~18분 동안 살아 있는 적은 생성 목표의 {0:0}~{1:0}%에 머물고, 그중 플레이어 곁에 닿아 맞을 수 있는 적은 {2:0}~{3:0}마리뿐이다. " +
                "생성 목표는 늘어나는데({4:0} → {5:0}) 곁의 적은 늘지 않는다. 닿는 족족 잡는다는 뜻이고, 그러면 레벨 속도는 무공 세기보다 " +
                "적이 다시 채워지는 속도(`spawnFractionPerSecond`)와 마리당 진기가 정한다.",
                fills.Min() * 100, fills.Max() * 100, crowds.Min(), crowds.Max(), spawn.TargetAt(mid[0]), spawn.TargetAt(mid[^1])));
            if (byScenario.TryGetValue("health", out var hp))
                L($"- 그래서 적 기혈 증가를 0.08 → 0.2로 올려도 20분 레벨은 거의 그대로다(Lv{MedianLevel(avg, 20)} → Lv{MedianLevel(hp, 20)}).");
            double kps19 = Median(avg.Select(r => (r.At(19).Kills - r.At(18).Kills) / 60.0));
            float frac = g.Balance.SpawnTarget.SpawnFractionPerSecond;
            L(string.Format(Inv, "- 19분 화면 위 적은 중앙 {0:N0}마리다(생성 목표 {1:N0}). 생성은 매초 '목표 − 지금 수'의 {2:0}%만 채우므로, " +
                "초당 K마리를 잡으면 화면 적은 목표보다 약 K÷{3:0.0#}마리 적은 데서 멈춘다. 평균 빌드는 18~19분에 초당 {4:0}마리를 잡는다({1:0} − {4:0}÷{3:0.0#} ≈ {5:0}).",
                Median(avg.Select(r => (double)r.At(19).Alive)), spawn.TargetAt(19), frac * 100, frac, kps19, spawn.TargetAt(19) - kps19 / frac));
            L($"- 검산: 웹 프로토타입 자동 판 기록(`web/prototype/README.md`: 5분 Lv15 안팎, 원각까지 간 판은 20분 Lv75~100)과 " +
              $"지금 데이터 결과(5분 Lv{MedianLevel(avg, 5)}, 20분 Lv{MinLevel(avg, 20)}~{MaxLevel(avg, 20)})가 비슷하다. 화면을 그리지 않는 모형치고 크게 빗나가지 않는다.");
            L($"- 처치 진기를 새로 정하고(P-02) 짝 심법 기준을 Lv3 → Lv2로 내리면 기획서 목표에 붙는다. " +
              $"레벨 {Minutes4} {Lv4(recRuns)}, 절정 {MedianText(RealmMinutes(recRuns, "jeoljeong"))}·초절정 {MedianText(RealmMinutes(recRuns, "chojeoljeong"))}, " +
              $"첫 절기 {MedianText(AwakenMinutes(recRuns, 0))}·둘째 {MedianText(AwakenMinutes(recRuns, 1))}(아래 '조정 제안').");
            L();

            // ── 목표와 비교
            L("## 목표와 비교");
            L();
            L("### 레벨(중앙값, 괄호는 최소~최대)");
            L();
            L("| | " + string.Join(" | ", targets.Select(t => $"{t.Minute}분")) + " |");
            L("|---|" + string.Concat(targets.Select(_ => "---:|")));
            L("| 목표(기획서 11장) | " + string.Join(" | ", targets.Select(t => $"Lv{t.Level}")) + " |");
            L($"| 허용 띠 ±{Tolerance * 100:0}% | " + string.Join(" | ", targets.Select(t => $"{Lo(t.Level)}~{Hi(t.Level)}")) + " |");
            foreach (var p in Policies.All)
                L($"| 지금 데이터 · {Policies.Create(p).NameKo}({p}) | " + string.Join(" | ", targets.Select(t => LevelCell(byPolicy[p], t.Minute))) + " |");
            foreach (var p in Policies.All)
                L($"| 추천안 · {Policies.Create(p).NameKo}({p}) | " + string.Join(" | ", targets.Select(t => LevelCell(recByPolicy[p], t.Minute))) + " |");
            L();
            L("레벨 속도는 정책에 따라 별로 갈리지 않는다(공급이 정한다). 정책에 따라 갈리는 건 아래 절기 수와 화력이다.");
            L();
            L("### 경지 돌파와 절기 각성 시각(분, 중앙값과 최소~최대)");
            L();
            L("| | 목표 | " + string.Join(" | ", Policies.All.Select(p => Policies.Create(p).NameKo)) + " | 추천안 · 평균 |");
            L("|---|---:|" + string.Concat(Policies.All.Select(_ => "---|")) + "---|");
            foreach (var t in RealmTargets)
                L($"| {t.NameKo} | {TargetText(t.Minute)} | " + string.Join(" | ", Policies.All.Select(p => MinuteText(RealmMinutes(byPolicy[p], t.Id))))
                  + $" | {MinuteText(RealmMinutes(recRuns, t.Id))} |");
            string[] ord = { "첫", "둘째", "셋째" };
            for (int i = 0; i < 3; i++)
                L($"| {ord[i]} 절기 | {TargetText(AwakenTargets[i])} | " + string.Join(" | ", Policies.All.Select(p => MinuteText(AwakenMinutes(byPolicy[p], i))))
                  + $" | {MinuteText(AwakenMinutes(recRuns, i))} |");
            L();
            L("목표는 기획서 11장(경지)과 8장(첫 절기는 '절정 무렵(약 11분 반)이나 15분 보갑', 둘째는 '초절정 돌파(약 16분 반)'). 일부 판만 닿았으면 'n/전체판'을 붙였다.");
            L();
            L("### 화력(보스 한 마리에게 들어가는 초당 피해, 중앙값)");
            L();
            L("| | " + string.Join(" | ", DpsTargets.Select(t => $"{t.Minute}분")) + " |");
            L("|---|" + string.Concat(DpsTargets.Select(_ => "---:|")));
            L("| 목표(기획서 11장, 1장) | " + string.Join(" | ", DpsTargets.Select(t => t.Dps.ToString("N0", Inv))) + " |");
            foreach (var p in Policies.All)
                L($"| 지금 데이터 · {Policies.Create(p).NameKo} | " + string.Join(" | ", DpsTargets.Select(t => DpsCell(byPolicy[p], t.Minute, t.Dps))) + " |");
            L("| 추천안 · 평균 | " + string.Join(" | ", DpsTargets.Select(t => DpsCell(recRuns, t.Minute, t.Dps))) + " |");
            L();
            var recDpsPct = DpsTargets.Select(t => Median(recRuns.Select(r => r.At(t.Minute).SingleDps)) / t.Dps * 100).ToList();
            L(string.Format(Inv, "화력은 그 시각 빌드로 계산한 값이라 레벨이 높으면 같이 높다. 레벨이 목표대로 오르는 추천안에서 화력은 목표의 {0:0}~{1:0}%다. " +
                "무공 수치 자체는 기획서 화력 표와 크게 어긋나지 않는다는 뜻이라, 무공 수치보다 진기 쪽을 먼저 손본다.", recDpsPct.Min(), recDpsPct.Max()));
            L();

            // ── 분당 표
            L("## 분당 표");
            L();
            L("각 칸은 판들의 중앙값이다. 채움은 살아 있는 적 ÷ 그 시각 생성 목표. 화면 적은 살아 있는 적 전부(걸어오는 중인 적 포함), " +
              "무리 안은 그중 플레이어 곁에 닿아 맞을 수 있는 적이다. 화력(단일)은 보스 한 마리 기준, 무리 피해/초는 지난 1분 동안 실제로 깎은 기혈(넘친 피해 제외).");
            L();
            foreach (var p in Policies.All.Concat(new[] { "rec" }))
            {
                var runs = p == "rec" ? recRuns : byPolicy[p];
                L(p == "rec" ? $"### 추천안({rec.NameKo}) · 평균" : $"### 지금 데이터 · {Policies.Create(p).NameKo}({p})");
                L();
                L("| 분 | Lv | 경지 | 누적 처치 | 채움 | 화면 적 | 무리 안 | 화력(단일) | 무리 피해/초 | 절기 |");
                L("|---:|---:|---|---:|---:|---:|---:|---:|---:|---:|");
                int maxMinute = runs[0].Minutes.Max(m => m.Minute);
                for (int m = 1; m <= maxMinute; m++)
                {
                    int lv = (int)Math.Round(Median(runs.Select(r => (double)r.At(m).Level)), MidpointRounding.AwayFromZero);
                    double alive = Median(runs.Select(r => (double)r.At(m).Alive));
                    string fill = spawn.SpawningStopped(m) || m >= g.Balance.SpawnTarget.BossAtMinute ? "-" : (alive / spawn.TargetAt(m)).ToString("P0", Inv).Replace(" ", "");
                    L(string.Format(Inv, "| {0} | {1} | {2} | {3:N0} | {4} | {5:N0} | {6:N0} | {7:N0} | {8:N0} | {9:0.#} |",
                        m, lv, realms.For(lv).NameKo, Median(runs.Select(r => (double)r.At(m).Kills)), fill, alive,
                        Median(runs.Select(r => (double)r.At(m).InCrowd)), Median(runs.Select(r => r.At(m).SingleDps)),
                        Median(runs.Select(r => r.At(m).CrowdDamagePerSecond)), Median(runs.Select(r => (double)r.At(m).Evolved))));
                }
                L();
                var one = runs[0];
                L("시드 1의 빌드(무공 단계, `*`는 절기 / 심법 단계):");
                L();
                foreach (var t in targets) L($"- {t.Minute}분 Lv{one.At(t.Minute).Level}: {one.At(t.Minute).Build}");
                L(one.Awakenings.Count == 0 ? "- 절기: 없음"
                    : "- 절기: " + string.Join(", ", one.Awakenings.Select(a => string.Format(Inv, "{0} {1:0.0}분 Lv{2}({3})", a.NameKo, a.Minute, a.Level, a.Trigger))));
                L();
            }

            // ── 가정
            L("## 가정");
            L();
            L("데이터에 없는 값은 `Assumptions.cs` 에 모아 두었다. 결과를 크게 움직이는 것만 적는다.");
            L();
            string EnemyName(string id) => g.Enemies.FirstOrDefault(e => e.Id == id)?.NameKo ?? id;
            L("| 가정 | 값 | 근거 |");
            L("|---|---|---|");
            L($"| 처치 진기 | {string.Join(", ", Assumptions.KillXp.Select(kv => $"{EnemyName(kv.Key)} {kv.Value:0.#}"))}, 정예 {Assumptions.EliteXp:0}, 금두꺼비 {Assumptions.GoldenToadXp:0} " +
              "| `enemies.json` 에 아직 없다. 웹 프로토타입 값(`web/prototype/README.md` '처치 진기') |");
            L($"| 1장 적 구성 | {string.Join(" / ", Assumptions.EnemyMix.Select(r => $"{r.FromMinute:0}분~ " + string.Join("·", r.Mix.Select(x => $"{EnemyName(x.Id)} {x.Weight * 100:0}%"))))} " +
              "| 웹 프로토타입 `pickType`(기획서 7장 흐름을 옮긴 것). B-08 `chapters.json` 이 생기면 그쪽을 읽는다 |");
            L($"| 정예 기혈 | {string.Join(", ", Assumptions.EliteNameKo.Zip(Assumptions.EliteHealth, (n, h) => $"{n} {h:N0}"))}(5·10·15분, 시간 배율은 일반 적과 같이) | 웹 프로토타입 `ELITES`. 기획서에 숫자가 없다 |");
            L($"| 무리 반경 | {Assumptions.CrowdRadius:0.#}m | 무리 안 적이 이 원 안에 고르게 퍼져 있다고 본다. PC 화면 높이(18.6m)의 절반보다 조금 안쪽 |");
            L($"| 최대 밀도 | 1㎡에 {Assumptions.MaxDensity:0.#}마리 | 적 몸 반지름 0.4m |");
            L($"| 도착 시간 | {Assumptions.ArrivalSeconds:0}초 | 생성 고리(대각선 0.6~0.75배, 약 25m)에서 1장 적 평균 걸음 초속 3.3m |");
            L($"| 방향 무공 효율 | {Assumptions.FacingEfficiency:0.#} | 장풍처럼 바라보는 쪽으로만 나가는 무공이 무리를 향하는 비율 |");
            L($"| 구슬 줍기 | {Assumptions.PickupRate * 100:0}% | 400개가 넘으면 합쳐져 곁에 떨어진다. 낙관적인 쪽 |");
            L($"| 각성 파동 | 무리 안 잡몹 전부 처치, 정예 {Assumptions.WaveEliteRatio * 100:0}%, 생성 {Assumptions.WaveSpawnPause:0.#}초 멈춤, 그 절기 {Assumptions.GlowSeconds:0}초 {Assumptions.GlowMultiplier:0.#}배 | 기획서 8장 |");
            L($"| 보갑 | 강화 1개 {(1 - Assumptions.ChestThree - Assumptions.ChestTwo) * 100:0}%, 2개 {Assumptions.ChestTwo * 100:0}%, 3개 {Assumptions.ChestThree * 100:0}% | 웹 프로토타입 `openChest`(기획서는 1~5개) |");
            L();
            L("빠진 것: 플레이어 사망·피격(피격 오의 충전 포함), 깨달음(B-06 전), 연환·진전, 연무장, 다시 고르기·파기·정진, 넉백·둔화·기절, 범종·등롱·부적, 20분 보스전.");
            L();

            // ── 민감도
            L("## 가정 민감도");
            L();
            L($"재 볼 수 없는 무리 가정 셋을 크게 흔들어 본다. 칸은 {Minutes4} 레벨(평균 정책, 중앙값, {sensitivitySeeds}판).");
            L();
            L("| 가정 | 지금 데이터 | 목표 띠 안? | 추천안 | 목표 띠 안? |");
            L("|---|---|---|---|---|");
            foreach (var (name, b, r) in sens)
                L($"| {name} | {Lv4(b)} | {BandMark(b, targets)} | {Lv4(r)} | {BandMark(r, targets)} |");
            L();
            double maxCrowd = Enumerable.Range(1, 19).Max(m => Median(avg.Select(r => (double)r.At(m).InCrowd)));
            L(string.Format(Inv, "최대 밀도를 낮춰도 거의 그대로인 건 무리 안 적이 많아야 {0:0}마리(분당 중앙값)라 밀도가 1㎡에 {1:0.00}마리를 넘지 않기 때문이다.",
                maxCrowd, maxCrowd / (Math.PI * Assumptions.CrowdRadius * Assumptions.CrowdRadius)));
            L();
            L("가정을 이만큼 흔들어도 추천안 레벨은 " +
              string.Join(", ", targets.Select(t =>
              {
                  var lvs = sens.Select(s => MedianLevel(s.Rec, t.Minute)).ToList();
                  return $"{t.Minute}분 Lv{lvs.Min()}~{lvs.Max()}(목표 {t.Level})";
              })) +
              $" 안에서 움직인다. 같은 가정에서 시드에 따른 흔들림도 20분 기준 Lv{MinLevel(recRuns, 20)}~{MaxLevel(recRuns, 20)} 정도다. " +
              $"모형 자체가 이만한 오차를 안고 있어서 테스트의 허용 띠를 목표 ±{Tolerance * 100:0}%로 잡았다. 이보다 좁히면 재 볼 수 없는 가정 하나 때문에 테스트가 깨지고, " +
              "넓히면 지금 데이터처럼 10분부터 크게 앞서 나가는 곡선을 못 잡는다.");
            L();

            // ── 조정 제안
            L("## 조정 제안");
            L();
            L($"데이터 파일은 바꾸지 않았다. 아래는 읽어 온 값을 메모리에서만 바꿔 평균 정책으로 {scenarioSeeds}판씩 돌린 결과다(`Scenarios.cs`, `--scenario <id>`).");
            L();
            L("| 시험(id) | 바꾸는 값 | " + string.Join(" | ", targets.Select(t => $"{t.Minute}분")) + " | 절정 | 초절정 | 첫 절기 | 둘째 절기 | 19분 화면 적 | 판정 |");
            L("|---|---|" + string.Concat(targets.Select(_ => "---:|")) + "---:|---:|---:|---:|---:|---|");
            L("| 지금 데이터(`baseline`) | - | " + string.Join(" | ", targets.Select(t => $"Lv{MedianLevel(avg, t.Minute)}")) + $" | {ScenarioTail(avg, targets)} |");
            foreach (var s in Scenarios.All.Where(s => s.Id != Scenarios.Baseline.Id))
            {
                var runs = byScenario[s.Id];
                L($"| {s.NameKo}(`{s.Id}`) | {s.Change} | " + string.Join(" | ", targets.Select(t => $"Lv{MedianLevel(runs, t.Minute)}")) + $" | {ScenarioTail(runs, targets)} |");
            }
            L();
            L("### 추천(작은 변경부터)");
            L();
            var killXp = byScenario["kill-xp"];
            var curve = byScenario["curve"];
            var kx = Scenarios.Find("kill-xp");
            var cv = Scenarios.Find("curve");
            L($"1. **처치 진기를 데이터로 옮기면서 낮춘다(P-02 결정과 같이).** {kx.Change}. {kx.Why} " +
              $"이것만으로 레벨이 목표에 붙는다({Minutes4} {Lv4(killXp)}). 기존 숫자를 고치는 게 아니라 비어 있는 값을 채우는 일이라 다른 계산에 번지지 않는다. " +
              "Core `EnemyDef` 에 `Xp` 를 더하고 웹 프로토타입 `spawnEnemy` 의 진기 표를 그 값으로 바꾸면 된다.");
            L($"2. **짝 심법 기준을 Lv2로(1과 같이).** balance.json `evolution.pairPassiveMinLevel` 3 → 2. " +
              $"1만 하면 첫 절기가 {MedianText(AwakenMinutes(killXp, 0))}, 둘째는 {Count(AwakenMinutes(killXp, 1))}/{killXp.Count}판에서만 깨어난다. " +
              $"같이 하면 첫 절기 {MedianText(AwakenMinutes(recRuns, 0))}, 둘째 {MedianText(AwakenMinutes(recRuns, 1))}({Count(AwakenMinutes(recRuns, 1))}/{recRuns.Count}판). " +
              "기획서 8장의 '절정 무렵이나 15분 보갑 / 초절정 돌파'에 가깝다. 바꾸면 기획서 3장·8장의 'Lv3', balance.json·evolutions.json `_note`, " +
              "`LoadoutTests` 의 '짝 심법 Lv2는 아직 문턱 아님' 검사도 같이 고친다.");
            L($"3. **(1 대신) 프로토 진기를 그대로 둔다면 곡선 뒤쪽을 가파르게.** {cv.Change}. {cv.Why} " +
              $"레벨은 1보다 조금 높고({Minutes4} {Lv4(curve)}), 레벨 60까지 누적 진기가 바뀌어 기획서 11장의 '약 20,300'과 `BalanceTests` 도 고쳐야 한다. 그래서 1을 먼저 권한다.");
            L();
            L("### 고르지 않은 것");
            L();
            if (byScenario.TryGetValue("refill", out var refill))
                L($"- `refill`(채움 비율 0.1): 레벨은 내려가지만({Lv4(refill)}) 19분 화면 적이 {Median(refill.Select(r => (double)r.At(19).Alive)):N0}마리로 줄어 적이 화면을 메우는 맛이 사라진다.");
            if (hp != null)
                L($"- `health`(기혈 증가 0.2): 레벨이 거의 안 늦어진다({Lv4(hp)}). 닿는 족족 잡는 상태라 적이 단단해져도 공급이 그대로다.");
            if (byScenario.TryGetValue("kill-xp+health", out var kh))
            {
                double hpm = g.Balance.EnemyScaling.HealthPerMinute;
                L(string.Format(Inv, "- `kill-xp+health`(진기 + 기혈 증가 0.3): 레벨은 목표보다 조금 낮아지고({0}) 19분 화면 적은 {1:N0}마리. " +
                    "19분 적 기혈이 지금의 {2:0.0}배인데도 화면은 별로 안 찬다.",
                    Lv4(kh), Median(kh.Select(r => (double)r.At(19).Alive)), (1 + 0.3 * 19) / (1 + hpm * 19)));
            }
            if (byScenario.TryGetValue("refill+kill-xp", out var rk))
                L($"- `refill+kill-xp`: 둘을 같이 낮추면 띠 안이긴 해도 네 시점 모두 목표보다 낮다({Lv4(rk)}).");
            L();
            L("### 같이 볼 것");
            L();
            var boss = g.Bosses.FirstOrDefault(b => b.Chapter == "chapter1");
            if (boss != null)
            {
                double dps20 = Median(recRuns.Select(r => r.At(20).SingleDps));
                L(string.Format(Inv, "- {0}(기혈 {1:N0}): 추천안 20분 화력(단일 {2:N0})으로 잡으면 {3:N0}초 걸린다(목표 {4:N0}초). 60초에 맞추려면 기혈 약 {5:N0}. " +
                    "보스전에는 잡몹과 패턴 피하기가 끼므로 이 시간은 가장 짧게 잡은 값이다.",
                    boss.NameKo, boss.Health, dps20, boss.Health / dps20, boss.TargetKillSeconds, Math.Round(dps20 * boss.TargetKillSeconds / 1000) * 1000));
            }
            double kills20 = Median(recRuns.Select(r => (double)r.At(20).Kills));
            int at5000 = recRuns[0].Minutes.FirstOrDefault(m => m.Kills >= 5000)?.Minute ?? -1;
            L(string.Format(Inv, "- 추천안에서도 20분까지 {0:N0}마리를 잡는다{1}. 음공 해금 조건 '한 판에서 적 5,000 처치'는 평균 판이면 저절로 열린다.",
                kills20, at5000 > 0 ? $"(시드 1은 {at5000}분에 5,000)" : ""));
            L("- 화면 적이 생성 목표 400에 못 미치는 건 위 요약대로 생성 규칙 때문이다. 기획서 11장의 '화면 위 적 400마리'를 '생성 목표'로 읽을지, 실제로 보이는 수로 읽을지 정해야 한다. " +
              "보이는 수를 400에 맞추려면 생성 목표나 채움 비율을 올려야 하는데, 둘 다 진기 공급을 늘리므로 처치 진기를 이 시뮬레이터로 다시 맞춘다.");
            return sb.ToString();
        }

        // ───────── 셈 도우미 ─────────

        static double Median(IEnumerable<double> xs)
        {
            var l = xs.Where(x => !double.IsNaN(x)).OrderBy(x => x).ToList();
            if (l.Count == 0) return double.NaN;
            return l.Count % 2 == 1 ? l[l.Count / 2] : (l[l.Count / 2 - 1] + l[l.Count / 2]) / 2;
        }

        public static int MedianLevel(IReadOnlyList<SimResult> runs, int minute) =>
            (int)Math.Round(Median(runs.Select(r => (double)r.LevelAt(minute))), MidpointRounding.AwayFromZero);
        static int MinLevel(IReadOnlyList<SimResult> runs, int minute) => runs.Min(r => r.LevelAt(minute));
        static int MaxLevel(IReadOnlyList<SimResult> runs, int minute) => runs.Max(r => r.LevelAt(minute));

        public static int Lo(int target) => (int)Math.Ceiling(target * (1 - Tolerance));
        public static int Hi(int target) => (int)Math.Floor(target * (1 + Tolerance));
        public static bool InBand(int level, int target) => level >= Lo(target) && level <= Hi(target);

        static string Pct(int value, int target) => string.Format(Inv, "{0:+0;-0;0}%", (value - target) * 100.0 / target);

        static string LevelCell(IReadOnlyList<SimResult> runs, int minute) =>
            $"Lv{MedianLevel(runs, minute)} ({MinLevel(runs, minute)}~{MaxLevel(runs, minute)})";

        static string DpsCell(IReadOnlyList<SimResult> runs, int minute, double target)
        {
            double v = Median(runs.Select(r => r.At(minute).SingleDps));
            return string.Format(Inv, "{0:N0} ({1:+0;-0;0}%)", v, (v - target) * 100 / target);
        }

        static IEnumerable<double> RealmMinutes(IEnumerable<SimResult> runs, string id) =>
            runs.Select(r => r.RealmMinute.TryGetValue(id, out var m) ? m : double.NaN);

        static IEnumerable<double> AwakenMinutes(IEnumerable<SimResult> runs, int index) =>
            runs.Select(r => r.Awakenings.Count > index ? r.Awakenings[index].Minute : double.NaN);

        static int Count(IEnumerable<double> minutes) => minutes.Count(x => !double.IsNaN(x));

        static string TargetText(double minute) => double.IsNaN(minute) ? "-" : minute.ToString("0.#", Inv);

        /// <summary>"11.5분" 또는 "못 닿음". 닿은 판만의 중앙값.</summary>
        static string MedianText(IEnumerable<double> minutes)
        {
            var hit = minutes.Where(x => !double.IsNaN(x)).ToList();
            return hit.Count == 0 ? "못 닿음" : Median(hit).ToString("0.0", Inv) + "분";
        }

        /// <summary>"11.5 (9.8~13.0)" 또는 일부만 닿았으면 "15.3 (14.8~16.7) 8/12판".</summary>
        static string MinuteText(IEnumerable<double> minutes)
        {
            var all = minutes.ToList();
            var hit = all.Where(x => !double.IsNaN(x)).OrderBy(x => x).ToList();
            if (hit.Count == 0) return "못 닿음";
            string tail = hit.Count == all.Count ? "" : $" {hit.Count}/{all.Count}판";
            return string.Format(Inv, "{0:0.0} ({1:0.0}~{2:0.0}){3}", Median(hit), hit[0], hit[^1], tail);
        }

        static string BandMark(IReadOnlyList<SimResult> runs, IReadOnlyList<(int Minute, int Level)> targets)
        {
            var miss = targets.Where(t => !InBand(MedianLevel(runs, t.Minute), t.Level)).Select(t => $"{t.Minute}분").ToList();
            return miss.Count == 0 ? "예" : "아니오(" + string.Join("·", miss) + ")";
        }

        static string ScenarioTail(IReadOnlyList<SimResult> runs, IReadOnlyList<(int Minute, int Level)> targets) =>
            $"{MedianText(RealmMinutes(runs, "jeoljeong"))} | {MedianText(RealmMinutes(runs, "chojeoljeong"))} | " +
            $"{MinuteOrMiss(AwakenMinutes(runs, 0), runs.Count)} | {MinuteOrMiss(AwakenMinutes(runs, 1), runs.Count)} | " +
            $"{Median(runs.Select(r => (double)r.At(19).Alive)):N0} | {(BandMark(runs, targets) == "예" ? "띠 안" : "띠 밖 " + BandMark(runs, targets).Replace("아니오", ""))}";

        static string MinuteOrMiss(IEnumerable<double> minutes, int total)
        {
            var hit = minutes.Where(x => !double.IsNaN(x)).ToList();
            if (hit.Count == 0) return "못 닿음";
            return Median(hit).ToString("0.0", Inv) + "분" + (hit.Count == total ? "" : $" ({hit.Count}/{total}판)");
        }
    }
}
