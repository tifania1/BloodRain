// 천하 연표 시제품 ②(backlog GU-11e). 보고서: docs/design/sim/chronicle.md
//
// CheonhaSim 의 한 생(평균 정책, 처음 혼, 성질 없음)을 시드마다 돌려 경지 도달 시각을 얻고,
// Core World.Chronicle 로 연표 사건마다 제때 닿는지 판정한다. 값은 고치지 않고 설계안 표와 견주기만 한다.
//
// 가정(이 파일에서 새로 정함):
//   - '닿음'은 그 시각에 장막이 사건 자리를 덮는다는 뜻이다. 실제로 걸어가는 시간은 넣지 않았다(연표가 Life 에 물리면 다시 본다).
//   - 판마다 판정해 제때 닿은 판의 몫을 내고, 가운데 도달 시각으로 한 줄 판정을 따로 낸다.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BloodRain.Core.World;

namespace BloodRain.Sim
{
    public static class ChronicleSim
    {
        /// <summary>설계안 '예시: 숭산에서 태어난 생' 표의 '첫 생' 칸(숭산 태생). 일류 6:40·절정 11:40·초절정 16:40 기준으로 쓴 값.</summary>
        public static readonly IReadOnlyDictionary<string, string> DesignFirstLife = new Dictionary<string, string>
        {
            ["songshan_refugees"] = "닿음",
            ["gaebang_scatter"] = "40초 늦음",
            ["wudang_barrier"] = "1분 40초 늦음",
            ["songshan_bell"] = "닿음",
            ["janggang_raid"] = "닿음",
            ["huashan_bloom"] = "장강과 둘 중 하나",
            ["danggata_poison"] = "40초 늦음",
            ["murim_muster"] = "닿음",
            ["imperial_south"] = "닿음",
            ["magyo_north"] = "닿음",
            ["bloodmoon"] = "",
        };

        public static readonly string[] Bodies = { "shaolin", "gaebang", "namgung" };

        static string M(double seconds)
        {
            int s = (int)Math.Round(seconds);
            return $"{s / 60}:{s % 60:00}";
        }

        static string Late(double seconds)
        {
            int s = (int)Math.Round(seconds);
            if (s < 60) return $"{s}초 늦음";
            return s % 60 == 0 ? $"{s / 60}분 늦음" : $"{s / 60}분 {s % 60}초 늦음";
        }

        static string Verdict(ChronicleJudgement j) => j.Reach switch
        {
            ChronicleReach.OnTime => "닿음",
            ChronicleReach.Late => Late(j.LateBy),
            _ => "못 닿음",
        };

        public static string Report(SimData data, int seeds = 30)
        {
            var g = data.Game;
            var chron = new Chronicle(g);
            var realms = g.Balance.Realms.OrderBy(r => r.Level).ToList();
            var sb = new StringBuilder();
            sb.AppendLine("# 천하 연표 시뮬레이션(GU-11e)");
            sb.AppendLine();
            sb.AppendLine($"`dotnet run --project core/BloodRain.Sim -c Release -- --chronicle docs/design/sim/chronicle.md` 로 다시 만든다. 코드는 `core/BloodRain.Sim/ChronicleSim.cs`, 판정은 Core `World.Chronicle`, 사건은 `chronicle.json`(설계안 `docs/design/cheonha_expansion_full.md` '예시: 숭산에서 태어난 생').");
            sb.AppendLine();
            sb.AppendLine($"몸마다 [cheonha.md](cheonha.md) 와 같은 한 생(정책 average, 처음 혼, 성질 없음, 30분)을 시드 1~{seeds} 돌려 경지 도달 시각을 얻었다. '닿음'은 사건 시각에 혈우 장막이 그 자리를 덮는다는 뜻이고 걸어가는 시간은 넣지 않았다. 값은 고치지 않았다.");
            sb.AppendLine();

            var reachBySect = new Dictionary<string, List<Dictionary<int, double>>>();
            foreach (var sectId in Bodies)
            {
                var runs = Enumerable.Range(1, seeds).Select(seed => CheonhaSim.Run(data, sectId, seed)).ToList();
                reachBySect[sectId] = runs.Select(r =>
                {
                    var d = new Dictionary<int, double>();
                    for (int i = 1; i < realms.Count; i++)
                        if (r.RealmMinute.TryGetValue(realms[i].Id, out var m)) d[i] = m * 60;
                    return d;
                }).ToList();
            }

            sb.AppendLine("## 읽을 것");
            sb.AppendLine();
            sb.AppendLine("- 지금 데이터로는 숭산 몸 첫 생이 설계안에서 '늦음'이던 개방 총타·무당산·당가타에도 대개 제때 닿는다. 경지 도달이 7장 목표보다 1~5분 이르기 때문이다(처치 진기, P-02). 이대로면 '회귀할수록 더 먼 사건에 닿는다'는 뼈대에 남는 자리가 없으니, 연표 시각은 P-02 가 정해진 뒤 도달 시각에 맞춰 다시 잡는다.");
            sb.AppendLine("- 개방 총타(6:00)는 일류 도달(가운데 5:52)과 8초 차이라 판마다 갈린다. 시각을 한 번 고칠 때 가장 먼저 볼 줄이다.");
            sb.AppendLine("- 세 몸의 도달 시각이 같은 것은 CheonhaSim 이 개방·남궁 출생도 숭산(chapter1) 적 값으로 돌리기 때문이다(cheonha.md 가정). 몸마다 다른 것은 거리뿐이다.");
            sb.AppendLine("- 남궁 몸은 숭산 아랫마을·개방 총타·무당산 세 사건에 첫 생부터 못 닿는다(멀어서). 몸마다 닿는 사건이 갈리는 것은 설계안 의도대로다.");
            sb.AppendLine();
            sb.AppendLine("## 경지 도달 시각(가운데값)");
            sb.AppendLine();
            sb.AppendLine("| 몸(출생지) | " + string.Join(" | ", realms.Skip(1).Select(r => r.NameKo)) + " |");
            sb.AppendLine("| --- | " + string.Join(" | ", realms.Skip(1).Select(_ => "---")) + " |");
            var median = new Dictionary<string, Dictionary<int, double?>>();
            foreach (var sectId in Bodies)
            {
                var sect = g.Cheonha.Sect(sectId);
                var runs = reachBySect[sectId];
                var med = new Dictionary<int, double?>();
                for (int i = 1; i < realms.Count; i++)
                {
                    var xs = runs.Select(r => r.TryGetValue(i, out var t) ? t : double.NaN).OrderBy(x => double.IsNaN(x) ? double.MaxValue : x).ToList();
                    var mid = xs[xs.Count / 2];
                    med[i] = double.IsNaN(mid) ? (double?)null : mid;
                }
                median[sectId] = med;
                sb.AppendLine($"| {sect.NameKo}({g.Cheonha.Region(sect.Region).NameKo}) | " +
                    string.Join(" | ", Enumerable.Range(1, realms.Count - 1).Select(i => med[i] == null ? "—" : M(med[i]!.Value))) + " |");
            }
            sb.AppendLine();
            sb.AppendLine("설계안이 기준으로 쓴 '지금 평균 빌드'는 일류 6:40, 절정 11:40, 초절정 16:40(7장 목표)이다. 시뮬레이터는 처치 진기가 높은 지금 데이터라 그보다 이르다([cheonha.md](cheonha.md) '읽을 것').");
            sb.AppendLine();

            var mismatches = new List<string>();
            foreach (var sectId in Bodies)
            {
                var sect = g.Cheonha.Sect(sectId);
                var birth = new Regions(g).Center(sect.Region);
                sb.AppendLine($"## {sect.NameKo} 몸({g.Cheonha.Region(sect.Region).NameKo})");
                sb.AppendLine();
                sb.AppendLine(sectId == "shaolin"
                    ? "| 시각 | 곳 | 사건 | 거리 | 필요한 경지 | 도달(가운데) | 판정(가운데) | 제때 닿은 판 | 설계안 '첫 생' |"
                    : "| 시각 | 곳 | 사건 | 거리 | 필요한 경지 | 도달(가운데) | 판정(가운데) | 제때 닿은 판 |");
                sb.AppendLine(sectId == "shaolin" ? "| --- | --- | --- | --- | --- | --- | --- | --- | --- |" : "| --- | --- | --- | --- | --- | --- | --- | --- |");
                var med = median[sectId];
                foreach (var e in chron.Events)
                {
                    var j = chron.Judge(e, birth, r => med.TryGetValue(r, out var t) && t != null ? (float?)t.Value : null);
                    int onTime = reachBySect[sectId].Count(run =>
                        chron.Judge(e, birth, r => run.TryGetValue(r, out var t) ? (float?)t : null).Reach == ChronicleReach.OnTime);
                    string dist = e.Everywhere ? "" : $"{chron.Distance(e, birth):0}m";
                    string reached = j.ReachedAt == null ? "—" : M(j.ReachedAt.Value);
                    string row = $"| {M(e.AtSeconds)} | {e.PlaceKo} | {e.NameKo} | {dist} | {realms[j.RequiredRealm].NameKo} | {reached} | {Verdict(j)} | {onTime * 100 / seeds}% |";
                    if (sectId == "shaolin")
                    {
                        var design = DesignFirstLife.TryGetValue(e.Id, out var d) ? d : "";
                        row += $" {design} |";
                        bool designOnTime = design == "닿음" || design == "장강과 둘 중 하나";
                        bool designLate = design.EndsWith("늦음", StringComparison.Ordinal);
                        if ((designOnTime && j.Reach != ChronicleReach.OnTime) || (designLate && Verdict(j) != design))
                            mismatches.Add($"- {M(e.AtSeconds)} {e.PlaceKo}: 설계안 '{design}', 시뮬레이터 '{Verdict(j)}'(필요한 {realms[j.RequiredRealm].NameKo} 도달 {reached})");
                    }
                    sb.AppendLine(row);
                }
                sb.AppendLine();
                var same = chron.Events.Where(e => chron.SameTime(e).Any()).GroupBy(e => e.AtSeconds).ToList();
                foreach (var grp in same)
                    sb.AppendLine($"같은 시각 {M(grp.Key)}: " + string.Join(" · ", grp.Select(e => e.PlaceKo)) + " — 한 생에 한쪽만 간다.");
                sb.AppendLine();
            }

            sb.AppendLine("## 설계안 '첫 생' 칸과 어긋난 곳(숭산 몸)");
            sb.AppendLine();
            if (mismatches.Count == 0) sb.AppendLine("없음.");
            else foreach (var m in mismatches) sb.AppendLine(m);
            sb.AppendLine();
            sb.AppendLine("어긋남은 사건 시각이 아니라 경지 도달 시각에서 온다. 처치 진기(P-02)는 플레이 뒤 정하기로 했으니(2026-10-30 사람) 사건 시각도 그때 같이 다시 잡는다. 회귀 가속(전생의 공력)이 붙으면 도달 시각이 더 당겨지니 그 곡선은 backlog GU-11f 가 따로 낸다.");
            return sb.ToString();
        }
    }
}
