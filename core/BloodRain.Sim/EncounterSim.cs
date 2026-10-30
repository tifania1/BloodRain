// 천하 한 생에서 기연 사건이 얼마나 나오는지(밸런스 시뮬레이션). 보고서: docs/design/sim/encounters.md
//
// 지역 흐름은 CheonhaSim 이 Core Life 를 30분 돌려 남긴 '분마다 서 있던 지역'을 그대로 쓴다.
// 그 흐름 위에서 Core EncounterRoller 를 매분(0분 포함) RollAll 한다. Encounters.cs 주석의 '판 시작에 한 번, 그 뒤 1분마다' 그대로.
//   장(chapter)·문파는 그 분에 서 있던 지역의 cheonha.json regions[].chapter·sect.
//   기연 능력치는 생 내내 같다고 본다(심법으로 생 도중에 오르는 몫은 넣지 않는다).
//   진전에서 오는 확률 덧셈(마도 진전 2 → 혈마의 속삭임)과 비전으로 반드시 나오게 하는 것은 넣지 않는다.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BloodRain.Core.Data;
using BloodRain.Core.Spawning;

namespace BloodRain.Sim
{
    public static class EncounterSim
    {
        public static readonly float[] Lucks = { 0, 10, 30, 50 };

        /// <summary>지역 흐름 하나 위에서 한 생을 굴려 나온 사건 id(나온 순서).</summary>
        public static List<string> RollLife(GameData g, IReadOnlyList<string> regionByMinute, float luck, Random rng)
        {
            var roller = new EncounterRoller(g, rng);
            var ctx = new EncounterContext { Luck = luck };
            for (int m = 0; m < regionByMinute.Count; m++)
            {
                var r = g.Cheonha.Region(regionByMinute[m]);
                ctx.Chapter = r.Chapter;
                ctx.ChapterSchool = r.Sect;
                ctx.Minute = m;
                roller.RollAll(ctx);
            }
            return roller.Appeared.ToList();
        }

        /// <summary>도감 기연 쪽을 다 채우기까지 걸린 생 수. maxLives 안에 못 채우면 -1. last 는 마지막으로 채운 사건.</summary>
        public static int LivesToComplete(GameData g, IReadOnlyList<IReadOnlyList<string>> traces, float luck, Random rng, int maxLives, out string? last)
        {
            var need = new HashSet<string>(g.Synergies.Encounters.Select(e => e.Id));
            last = null;
            for (int life = 1; life <= maxLives; life++)
            {
                var trace = traces[rng.Next(traces.Count)];
                foreach (var id in RollLife(g, trace, luck, rng))
                    if (need.Remove(id)) last = id;
                if (need.Count == 0) return life;
            }
            return -1;
        }

        public static string Report(SimData data, int seeds = 3, int rollsPerTrace = 400, int souls = 2000, int maxLives = 200)
        {
            var g = data.Game;
            var encs = g.Synergies.Encounters;
            var sects = g.Cheonha.Sects;
            var traces = new List<(string sect, string birth, int seed, List<string> trace)>();
            foreach (var s in sects)
                for (int seed = 1; seed <= seeds; seed++)
                {
                    var r = CheonhaSim.Run(data, s.Id, seed);
                    traces.Add((s.NameKo, r.Birth, seed, r.RegionByMinute));
                }

            string Pct(double x) => (x * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
            string F1(double x) => x.ToString("0.0", CultureInfo.InvariantCulture);
            string RegionKo(string id) => g.Cheonha.Region(id).NameKo;

            var sb = new StringBuilder();
            sb.AppendLine("# 천하 한 생의 기연 사건");
            sb.AppendLine();
            sb.AppendLine("`dotnet run --project core/BloodRain.Sim -- --encounters docs/design/sim/encounters.md` 로 다시 만든다(`EncounterSim.cs`).");
            sb.AppendLine();
            sb.AppendLine($"문파 아홉 × 시드 {seeds}의 천하 30분(CheonhaSim, Core `Life`)이 지나간 지역 흐름 위에서 Core `EncounterRoller` 를 매분 굴렸다. 흐름 하나당 {rollsPerTrace}번. "
                + "기연 능력치는 생 내내 같다고 보고, 진전으로 오는 확률 덧셈과 비전의 '반드시 나옴'은 뺐다. 확률·조건은 synergies.json `encounters`(기획서 20장).");
            sb.AppendLine();

            // 1. 지역 흐름
            sb.AppendLine("## 지역 흐름(시드 1)");
            sb.AppendLine();
            sb.AppendLine("| 문파 | 태어난 곳 | 30분 동안 지나간 땅(처음 밟은 분) |");
            sb.AppendLine("| --- | --- | --- |");
            foreach (var t in traces.Where(t => t.seed == 1))
            {
                var firsts = new List<string>();
                var seen = new HashSet<string>();
                for (int m = 0; m < t.trace.Count; m++)
                    if (seen.Add(t.trace[m])) firsts.Add($"{RegionKo(t.trace[m])}({m})");
                sb.AppendLine($"| {t.sect} | {t.birth} | {string.Join(" → ", firsts)} |");
            }
            sb.AppendLine();

            // 2. 사건별 한 생 출현율
            var rate = new Dictionary<(string id, float luck), double>();
            var perLife = new Dictionary<float, double>();
            var perSect = new Dictionary<(string sect, float luck), double>();
            var rng = new Random(20261031);
            foreach (var luck in Lucks)
            {
                double total = 0;
                var count = encs.ToDictionary(e => e.Id, _ => 0);
                foreach (var t in traces)
                {
                    double sectTotal = 0;
                    for (int i = 0; i < rollsPerTrace; i++)
                    {
                        var got = RollLife(g, t.trace, luck, rng);
                        foreach (var id in got) count[id]++;
                        sectTotal += got.Count;
                    }
                    total += sectTotal;
                    var key = (t.sect, luck);
                    perSect[key] = (perSect.TryGetValue(key, out var v) ? v : 0) + sectTotal / rollsPerTrace / seeds;
                }
                int n = traces.Count * rollsPerTrace;
                foreach (var e in encs) rate[(e.Id, luck)] = (double)count[e.Id] / n;
                perLife[luck] = total / n;
            }

            sb.AppendLine("## 사건별로 한 생에 나오는 비율");
            sb.AppendLine();
            sb.AppendLine("문파 아홉을 똑같이 섞은 평균. '기획' 열은 기본 확률(기연 0, 조건이 맞는 땅에 있을 때).");
            sb.AppendLine();
            sb.AppendLine("| 사건 | 기획 | " + string.Join(" | ", Lucks.Select(l => $"기연 {l}")) + " |");
            sb.AppendLine("| --- | --- | " + string.Join(" | ", Lucks.Select(_ => "---")) + " |");
            foreach (var e in encs)
                sb.AppendLine($"| {e.NameKo} | {Pct(e.Chance)} | " + string.Join(" | ", Lucks.Select(l => Pct(rate[(e.Id, l)]))) + " |");
            sb.AppendLine("| **한 생 평균 사건 수** | | " + string.Join(" | ", Lucks.Select(l => F1(perLife[l]))) + " |");
            sb.AppendLine();

            // 3. 문파별
            sb.AppendLine("## 문파별 한 생 평균 사건 수");
            sb.AppendLine();
            sb.AppendLine("| 문파 | " + string.Join(" | ", Lucks.Select(l => $"기연 {l}")) + " | 기연 0에서 한 번도 안 나온 사건 |");
            sb.AppendLine("| --- | " + string.Join(" | ", Lucks.Select(_ => "---")) + " | --- |");
            foreach (var s in sects)
            {
                var never = new List<string>();
                var mine = traces.Where(t => t.sect == s.NameKo).ToList();
                var r2 = new Random(7);
                var seen = new HashSet<string>();
                foreach (var t in mine)
                    for (int i = 0; i < rollsPerTrace; i++)
                        foreach (var id in RollLife(g, t.trace, 0, r2)) seen.Add(id);
                foreach (var e in encs) if (!seen.Contains(e.Id)) never.Add(e.NameKo);
                sb.AppendLine($"| {s.NameKo} | " + string.Join(" | ", Lucks.Select(l => F1(perSect[(s.NameKo, l)]))) + $" | {(never.Count == 0 ? "없음" : string.Join(", ", never))} |");
            }
            sb.AppendLine();

            // 4. 도감 채우기
            sb.AppendLine("## 도감 기연 8쪽을 다 채우기까지");
            sb.AppendLine();
            sb.AppendLine($"혼 {souls}개. 생마다 문파 아홉 가운데 하나로 고르게 태어나고(환생 후보 셋 중 고르는 손은 넣지 않음), 위 흐름 가운데 하나를 탄다. {maxLives}생 안에 못 채우면 '못 채움'.");
            sb.AppendLine();
            sb.AppendLine("| 기연 | 가운데값(생) | 90%(생) | 못 채움 | 마지막으로 채운 쪽(많은 순) |");
            sb.AppendLine("| --- | --- | --- | --- | --- |");
            var flat = traces.Select(t => (IReadOnlyList<string>)t.trace).ToList();
            foreach (var luck in Lucks)
            {
                var r3 = new Random(31 + (int)luck);
                var lives = new List<int>();
                int fail = 0;
                var lastCount = new Dictionary<string, int>();
                for (int i = 0; i < souls; i++)
                {
                    int n = LivesToComplete(g, flat, luck, r3, maxLives, out var last);
                    if (n < 0) { fail++; continue; }
                    lives.Add(n);
                    if (last != null) lastCount[last] = (lastCount.TryGetValue(last, out var c) ? c : 0) + 1;
                }
                lives.Sort();
                string med = lives.Count > 0 ? lives[lives.Count / 2].ToString(CultureInfo.InvariantCulture) : "-";
                string p90 = lives.Count > 0 ? lives[Math.Min(lives.Count - 1, (int)(lives.Count * 0.9))].ToString(CultureInfo.InvariantCulture) : "-";
                var lastTop = lastCount.OrderByDescending(kv => kv.Value).Take(3)
                    .Select(kv => $"{g.Synergies.Encounter(kv.Key).NameKo} {Pct((double)kv.Value / Math.Max(1, lives.Count))}");
                sb.AppendLine($"| {luck} | {med} | {p90} | {Pct((double)fail / souls)} | {string.Join(", ", lastTop)} |");
            }
            sb.AppendLine();
            sb.AppendLine("## 읽는 법과 한계");
            sb.AppendLine();
            sb.AppendLine("- 땅을 가리는 사건(벼랑·영물·검총)은 그 땅을 밟아야 굴린다. 시뮬레이터는 깨어난 땅의 주인에게 곧장 걷기만 하므로, 사람이 일부러 찾아가면 이보다 자주 나온다.");
            sb.AppendLine("- 기연 50은 도문심법 Lv5(+50)에 맞먹는다. 실제로는 생 도중에 오르니 앞 분의 굴림은 이보다 낮은 기연으로 굴린다.");
            sb.AppendLine("- 사건이 맵에 '나오기로' 정해진 것까지만 센다. 플레이어가 그 자리에 가서 조건(6초 버티기, 벼랑에서 뛰기 등)을 채우는지는 넣지 않았다.");
            return sb.ToString();
        }
    }
}
