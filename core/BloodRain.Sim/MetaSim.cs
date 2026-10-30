// 메타 곡선 시뮬레이션(T-18). 보고서: docs/design/sim/meta.md
//
// 혼 하나로 생을 이어 돌린다. 생마다 CheonhaSim.RunLife(Core Life 를 0.1초씩)로 정산하고 Soul.EndLife 로 옮긴 뒤,
// 모인 윤회의 기를 연무장에 쓰고 다음 몸을 고른다.
//
// 가정(새로 정함, 화면이 없어 잴 수 없는 값):
//   - 생의 길이: 시뮬레이터에는 플레이어가 맞는 모형이 없다. 대신 '버티는 분' = BaseSurvive + PerTrainingLevel × 산 연무장 레벨 수, 30분까지.
//     첫 생이 몇 분에 쓰러지는지는 플레이 결과가 나와야 알 수 있어서, BaseSurvive 를 셋으로 흔들어 본다.
//   - 연무장: 생이 끝날 때마다 살 수 있는 것 중 가장 싼 것을 계속 산다(값이 같으면 balance.json 순서). 환혼 강화는 사지 않는다.
//   - 몸 고르기: 후보 가운데 자기 땅의 주인을 아직 못 잡은 출생지를 먼저, 없으면 첫 후보. 성질은 굴린 그대로.
//   - 시간: 서장 10분 + 생마다 버틴 분 + 객잔 1분.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Run;

namespace BloodRain.Sim
{
    public sealed class MetaLifeRow
    {
        public int Life;
        public string Sect = "";
        public double Minutes;
        public string Realm = "";
        public int Qi, TrainingLevels, SealStones;
        public double Hours;
        public List<string> Killed = new List<string>();
    }

    public static class MetaSim
    {
        public const double PrologueMinutes = 10;
        public const double InnMinutes = 1;
        public const double PerTrainingLevel = 0.5;
        public static readonly double[] BaseSurviveOptions = { 6, 8, 12 };

        public static List<MetaLifeRow> Run(SimData data, double baseSurvive, Func<SimOptions, SimOptions>? apply, int maxLives = 40, int seed = 1)
        {
            var g = data.Game;
            var c = g.Cheonha;
            var rng = new Random(seed);
            var soul = new Soul(g);
            soul.EndLife(soul.BeginLife(Rebirth.Prologue(g)).Finish(LifeEndWay.Fallen, 0));
            double hours = PrologueMinutes / 60;
            var rows = new List<MetaLifeRow>();
            var realmOrder = g.Balance.Realms.OrderBy(r => r.Level).Select(r => r.Id).ToList();

            for (int n = 1; n <= maxLives; n++)
            {
                var cands = n == 1 ? Rebirth.First(c, rng) : Rebirth.Roll(c, soul.UnlockedSects, soul.CandidateCount, rng);
                var pick = cands.FirstOrDefault(x => x.Region.Boss != null && !soul.BossesKilled.Contains(x.Region.Id)) ?? cands[0];
                int levels = soul.Training.Values.Sum();
                double minutes = Math.Min(CheonhaSim.Minutes, baseSurvive + PerTrainingLevel * levels);

                var (res, record) = CheonhaSim.RunLife(data, soul, pick, seed * 1000 + n, apply, minutes);
                var summary = soul.EndLife(record);
                BuyTraining(soul);
                hours += (minutes + InnMinutes) / 60;
                rows.Add(new MetaLifeRow
                {
                    Life = n, Sect = pick.Sect.NameKo, Minutes = minutes, Realm = g.Balance.Realms.Single(r => r.Id == record.HighestRealm).NameKo,
                    Qi = record.RebirthQi.Total, TrainingLevels = soul.Training.Values.Sum(), SealStones = soul.SealStoneCount, Hours = hours,
                    Killed = record.BossesKilled.Select(id => g.Bosses.First(b => b.Id == c.Region(id).Boss).NameKo).ToList(),
                });
                if (soul.HasAllSealStones && rows.Count >= 5) break;
            }
            return rows;
        }

        static void BuyTraining(Soul soul)
        {
            var items = soul.Data.Balance.Training.Items.Select(i => i.Stat).ToList();
            while (true)
            {
                var next = items.Select(s => (s, cost: soul.TrainingCost(s)))
                    .Where(x => x.cost.HasValue && x.cost.Value <= soul.RebirthQi)
                    .OrderBy(x => x.cost!.Value).FirstOrDefault();
                if (next.s == null) return;
                if (!soul.TryBuyTraining(next.s)) return;
            }
        }

        static string Life(IEnumerable<MetaLifeRow> rows, Func<MetaLifeRow, bool> hit)
        {
            var r = rows.FirstOrDefault(hit);
            return r == null ? "—" : $"{r.Life}생({r.Hours:0.0}시간)";
        }

        public static string Report(SimData data)
        {
            var g = data.Game;
            var sb = new StringBuilder();
            sb.AppendLine("# 메타 곡선 시뮬레이션(T-18)");
            sb.AppendLine();
            sb.AppendLine("`dotnet run --project core/BloodRain.Sim -- --meta docs/design/sim/meta.md` 로 다시 만든다. 가정은 `core/BloodRain.Sim/MetaSim.cs` 맨 위, 한 생은 `CheonhaSim`(cheonha.md)과 같다.");
            sb.AppendLine();
            sb.AppendLine("시뮬레이터에는 플레이어가 맞는 모형이 없어서 생의 길이를 '첫 생 버티는 분 + 산 연무장 레벨마다 0.5분'으로 가정했다. 첫 생이 몇 분 버티는지는 플레이해 봐야 아니까 6·8·12분 셋으로 흔든다. 종장은 넣지 않았다(봉인석 7개가 모인 생까지).");
            sb.AppendLine();
            var scenarios = new[] { Scenarios.Baseline, Scenarios.Find("kill-xp") };
            sb.AppendLine("## 진행 속도 목표와 견주기");
            sb.AppendLine();
            sb.AppendLine("목표(10장): 첫 절정 2~4생, 첫 봉인석 3~6생, 봉인 엔딩 15~20시간.");
            sb.AppendLine();
            sb.AppendLine("| 데이터 | 첫 생 버팀 | 첫 절정 | 첫 봉인석 | 봉인석 7 | 그때 연무장 레벨 |");
            sb.AppendLine("| --- | --- | --- | --- | --- | --- |");
            var detail = new List<(string, double, List<MetaLifeRow>)>();
            foreach (var sc in scenarios)
                foreach (var b in BaseSurviveOptions)
                {
                    var rows = Run(data, b, sc.ApplyOptions);
                    detail.Add((sc.NameKo, b, rows));
                    var seven = rows.FirstOrDefault(r => r.SealStones >= g.Cheonha.Bosses.FinalRequires.SealStones);
                    sb.AppendLine($"| {sc.NameKo} | {b:0}분 | {Life(rows, r => r.Realm == "절정" || r.Realm == "초절정" || r.Realm == "화경" || r.Realm == "현경")} | {Life(rows, r => r.SealStones >= 1)} | {Life(rows, r => r.SealStones >= 7)} | {(seven?.TrainingLevels.ToString() ?? "—")} |");
                }
            sb.AppendLine();
            sb.AppendLine("'처치 진기 줄이기'는 level-curve 의 kill-xp 조정 시험(P-02 후보)을 얹은 것이다.");
            sb.AppendLine();
            foreach (var (name, b, rows) in detail.Where(d => d.Item2 == 8))
            {
                sb.AppendLine($"## 생마다({name}, 첫 생 {b:0}분)");
                sb.AppendLine();
                sb.AppendLine("| 생 | 문파 | 버틴 분 | 오른 경지 | 잡은 땅의 주인 | 윤회의 기 | 연무장 레벨 | 봉인석 | 누적 시간 |");
                sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- |");
                foreach (var r in rows)
                    sb.AppendLine($"| {r.Life} | {r.Sect} | {r.Minutes:0.#} | {r.Realm} | {(r.Killed.Count == 0 ? "—" : string.Join(", ", r.Killed))} | {r.Qi:N0} | {r.TrainingLevels} | {r.SealStones} | {r.Hours:0.0} |");
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
