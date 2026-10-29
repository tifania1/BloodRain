// 조정 시험. 데이터 파일은 건드리지 않고, 읽어 온 값을 메모리에서만 바꿔 같은 시뮬레이션을 돌린다.
// 보고서 '조정 제안'의 숫자는 전부 여기 적힌 변경을 실제로 돌려 본 결과다.
//   dotnet run --project core/BloodRain.Sim -- --scenario <id> --seeds 30
using System;
using System.Collections.Generic;
using System.Linq;

namespace BloodRain.Sim
{
    public sealed class Scenario
    {
        public string Id = "";
        public string NameKo = "";
        /// <summary>어느 파일의 어느 값을 무엇으로 바꾸는지(사람이 그대로 옮겨 적을 수 있게).</summary>
        public string Change = "";
        /// <summary>왜 이 값인지.</summary>
        public string Why = "";
        public Action<SimData> ApplyData = _ => { };
        public Func<SimOptions, SimOptions> ApplyOptions = o => o;

        public SimData LoadData(string? dataDir)
        {
            var d = SimData.Load(dataDir);
            ApplyData(d);
            return d;
        }
    }

    public static class Scenarios
    {
        /// <summary>처치 진기를 enemies.json 에 새로 넣는다면(P-02). 동갑시·목인나한처럼 단단한 적만 2.</summary>
        public static readonly IReadOnlyDictionary<string, double> LeanKillXp = new Dictionary<string, double>
        {
            ["sigang"] = 1, ["hyeolin"] = 1, ["hyeol_gangsi"] = 1, ["hyeol_musa"] = 1, ["mogin_nahan"] = 2, ["donggap"] = 2,
        };

        public static readonly Scenario Baseline = new Scenario { Id = "baseline", NameKo = "지금 데이터" };

        public static readonly IReadOnlyList<Scenario> All = new List<Scenario>
        {
            Baseline,
            new Scenario
            {
                Id = "refill",
                NameKo = "생성 채움 비율 낮추기",
                Change = "balance.json `spawnTarget.spawnFractionPerSecond` 0.3 → 0.1",
                Why = "화면을 비우는 빌드에게 적이 바로 다시 채워지는 속도가 진기 공급의 상한이다.",
                ApplyData = d => d.Game.Balance.SpawnTarget.SpawnFractionPerSecond = 0.1f,
            },
            new Scenario
            {
                Id = "kill-xp",
                NameKo = "처치 진기 줄이기",
                Change = "enemies.json 에 `xp` 를 새로 두고 시강·혈인·혈강시·혈무사 1, 목인나한·동갑시 2(프로토는 1·1·2·3·3·4)",
                Why = "처치 진기는 아직 데이터에 없는 프로토 값이라(P-02) 지금 정하면 바꾸는 비용이 가장 적다.",
                ApplyOptions = o => o.With(x => x.KillXp = LeanKillXp),
            },
            new Scenario
            {
                Id = "health",
                NameKo = "적 기혈 증가 가파르게",
                Change = "balance.json `enemyScaling.healthPerMinute` 0.08 → 0.2",
                Why = "평균 빌드가 화면을 다 비우지 못해야 기획서의 '18분 400마리'가 나온다.",
                ApplyData = d => d.Game.Balance.EnemyScaling.HealthPerMinute = 0.2f,
            },
            new Scenario
            {
                Id = "curve",
                NameKo = "경험치 곡선 뒤쪽을 가파르게",
                Change = "balance.json `xpCurve.segments` 둘째 step 13 → 30, 셋째 base 455 → 795, step 16 → 45",
                Why = "레벨 21부터 필요한 진기를 늘려 공급이 많은 후반 레벨 속도만 늦춘다(초반 곡선은 그대로).",
                ApplyData = d =>
                {
                    var s = d.Game.Balance.XpCurve.Segments;
                    s[1].Step = 30;
                    s[2].Base = s[1].Base + s[1].Step * (s[1].ToLevel - s[1].Anchor); // 둘째 구간 끝과 이어지게
                    s[2].Step = 45;
                },
            },
            new Scenario
            {
                Id = "kill-xp+pair2",
                NameKo = "처치 진기 줄이기 + 짝 심법 Lv2",
                Change = "처치 진기 위와 같이 + balance.json `evolution.pairPassiveMinLevel` 3 → 2",
                Why = "레벨 곡선을 맞추면 절기 문턱이 늦어진다. 짝 심법 한 단계를 덜 요구해 초절정 돌파에 둘째가 깨어나게.",
                ApplyData = d => d.Game.Balance.Evolution.PairPassiveMinLevel = 2,
                ApplyOptions = o => o.With(x => x.KillXp = LeanKillXp),
            },
            new Scenario
            {
                Id = "kill-xp+health",
                NameKo = "처치 진기 줄이기 + 적 기혈 증가 0.3",
                Change = "처치 진기 위와 같이 + balance.json `enemyScaling.healthPerMinute` 0.08 → 0.3",
                Why = "화면 위 적 수가 목표(18분 400)에 못 미치는 걸 기혈로 메워 본다.",
                ApplyData = d => d.Game.Balance.EnemyScaling.HealthPerMinute = 0.3f,
                ApplyOptions = o => o.With(x => x.KillXp = LeanKillXp),
            },
            new Scenario
            {
                Id = "refill+kill-xp",
                NameKo = "채움 비율 0.1 + 처치 진기 줄이기",
                Change = "위 두 변경을 같이",
                Why = "공급 상한과 마리당 진기를 함께 낮춘다.",
                ApplyData = d => d.Game.Balance.SpawnTarget.SpawnFractionPerSecond = 0.1f,
                ApplyOptions = o => o.With(x => x.KillXp = LeanKillXp),
            },
        };

        public static Scenario Find(string id) =>
            All.FirstOrDefault(s => s.Id == id) ?? throw new ArgumentException(
                $"모르는 조정 시험입니다: {id} ({string.Join(", ", All.Select(s => s.Id))})");
    }
}
