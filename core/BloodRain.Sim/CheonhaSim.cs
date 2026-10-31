// 천하 한 생 시뮬레이션(T-17). 보고서: docs/design/sim/cheonha.md
//
// 화력·진기는 기존 RunSimulator(무리 모형)가 30분 동안 낸 분당 값을 그대로 쓰고,
// 경지·지역·땅의 주인·윤회의 기는 Core 의 Life 를 0.1초씩 실제로 돌려서 잰다.
//
// 가정(이 파일에서 새로 정함):
//   - 적 기혈 배율은 출생지 장(章) 값으로 30분 내내 고정. 문파 마당만 있는 개방·남궁은 숭산(chapter1) 값.
//   - 진기는 RunSimulator 의 누적 진기를 분 사이에서 곧게 이어 0.1초마다 먹는다. 보스전 중에도 같다.
//   - 움직임: 깨어난 땅의 주인 가운데 가장 가까운 곳으로 곧장 걷는다(깨어난 곳이 없으면 그 자리).
//     걸음 = 초속 WalkMetersPerSecond × (지금 화면 대각선 ÷ 삼류 화면 대각선). 경지가 오를수록 걸음이 넓어진다.
//   - 보스전 시간 = 싸움 시작 때 보스 기혈 ÷ 그 분의 단일 대상 초당 피해(RunSimulator SingleDps). 피하느라 못 치는 몫은 BossUptime.
//   - 30분에 쓰러진 것으로 정산(남은 은자 0). 종장은 넣지 않는다.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Run;

namespace BloodRain.Sim
{
    public sealed class CheonhaBossLine
    {
        public string RegionId = "", BossKo = "";
        public double WokeAt, EngagedAt, KilledAt = double.NaN, FightSeconds;
        public float MaxHealth, TargetKillSeconds;
    }

    public sealed class CheonhaLifeResult
    {
        public string Sect = "", Birth = "";
        public int Seed;
        public Dictionary<string, double> RealmMinute = new Dictionary<string, double>();
        public List<CheonhaBossLine> Bosses = new List<CheonhaBossLine>();
        public List<string> Visited = new List<string>();
        public int FinalLevel;
        /// <summary>5분마다(5·10·…·30) 레벨.</summary>
        public Dictionary<int, int> LevelByMinute = new Dictionary<int, int>();
        /// <summary>분마다 서 있던 지역 id. [0] 은 태어난 순간, [k] 는 k분째(기연 시뮬레이션이 쓴다).</summary>
        public List<string> RegionByMinute = new List<string>();
        public RebirthQiResult Qi = null!;
    }

    public static class CheonhaSim
    {
        /// <summary>플레이어 기본 이동속도 100의 초속(m, 삼류 화면 기준). Assumptions 의 적 걸음 환산(4.6m/100)과 같다.</summary>
        public const double WalkMetersPerSecond = 4.6;
        /// <summary>보스전에서 실제로 치는 시간 비율(패턴 피하기). [가정]</summary>
        public const double BossUptime = 0.7;
        public const double Minutes = 30;
        const float Dt = 0.1f;

        public static CheonhaLifeResult Run(SimData data, string sectId, int seed, string policy = "average")
        {
            var g = data.Game;
            var c = g.Cheonha;
            var sect = c.Sect(sectId);
            var soul = new Soul(g);
            soul.EndLife(soul.BeginLife(Rebirth.Prologue(g)).Finish(LifeEndWay.Fallen, 0));
            var cand = new RebirthCandidate(sect, c.Region(sect.Region), Array.Empty<BirthTraitDef>());
            return Run(data, soul, cand, seed, o => o.With(x => x.Policy = policy));
        }

        /// <summary>혼 하나의 다음 생. minutes 까지 돌리고 쓰러진 것으로 정산한다(혼에 옮기는 일은 부른 쪽이 EndLife 로).</summary>
        public static (CheonhaLifeResult result, LifeRecord record) RunLife(SimData data, Soul soul, RebirthCandidate cand, int seed,
            Func<SimOptions, SimOptions>? apply = null, double minutes = Minutes)
        {
            var r = RunCore(data, soul, cand, seed, apply, minutes);
            return (r.Item1, r.Item2);
        }

        static CheonhaLifeResult Run(SimData data, Soul soul, RebirthCandidate cand, int seed, Func<SimOptions, SimOptions>? apply) =>
            RunCore(data, soul, cand, seed, apply, Minutes).Item1;

        static (CheonhaLifeResult, LifeRecord) RunCore(SimData data, Soul soul, RebirthCandidate cand, int seed,
            Func<SimOptions, SimOptions>? apply, double minutes)
        {
            var g = data.Game;
            var c = g.Cheonha;
            var sect = cand.Sect;
            var birth = cand.Region;
            string chapter = string.IsNullOrEmpty(birth.Chapter) || birth.Chapter == "prologue" ? "chapter1" : birth.Chapter!;

            var opt = new SimOptions { Seed = seed, Minutes = Math.Max(1, Math.Ceiling(minutes)), Chapter = chapter, Training = soul.Training };
            if (apply != null) opt = apply(opt);
            var run = RunSimulator.Run(data, opt);
            var snaps = run.Minutes.OrderBy(m => m.Minute).ToList();
            double XpAt(double minute)
            {
                if (minute <= 0) return 0;
                var prev = snaps.LastOrDefault(s => s.Minute <= minute);
                var next = snaps.FirstOrDefault(s => s.Minute > minute);
                double x0 = prev?.TotalXp ?? 0, m0 = prev?.Minute ?? 0;
                if (next == null) return x0;
                return x0 + (next.TotalXp - x0) * (minute - m0) / (next.Minute - m0);
            }
            double DpsAt(double minute)
            {
                var s = snaps.LastOrDefault(x => x.Minute <= Math.Max(1, minute)) ?? snaps.First();
                return Math.Max(1, s.SingleDps);
            }

            var life = new Life(soul, Rebirth.Body(c, cand), new Random(seed));

            var res = new CheonhaLifeResult { Sect = sect.NameKo, Birth = birth.NameKo, Seed = seed };
            var lines = new Dictionary<string, CheonhaBossLine>();
            double fed = 0, fightLeft = 0;
            CheonhaBossLine? fighting = null;
            float diag0 = life.Scale.ViewDiagonalAtRealm(0);
            Vector2 pos = life.Position;

            res.RegionByMinute.Add(life.RegionId);
            int guard = 0;
            while (life.Minutes < minutes && guard++ < 1_000_000)
            {
                // 걷기: 깨어난 곳 중 가장 가까운 곳
                if (fighting == null)
                {
                    var target = life.Bosses.Awake
                        .OrderBy(r => Vector2.Distance(pos, life.Regions.Center(r)))
                        .FirstOrDefault();
                    if (target != null)
                    {
                        var to = life.Regions.Center(target);
                        float step = (float)(WalkMetersPerSecond * life.Scale.ViewDiagonal(life.Level) / diag0) * Dt * life.WorldTimeScale;
                        var d = to - pos;
                        pos = d.Length() <= step ? to : pos + Vector2.Normalize(d) * step;
                    }
                }

                float before = life.Minutes;
                life.Tick(Dt, pos, 100);
                if (life.Record != null) break;
                int mark = (int)Math.Floor(life.Minutes / 5) * 5;
                if (mark > 0 && before < mark) res.LevelByMinute[mark] = life.Level;
                while (res.RegionByMinute.Count <= Math.Floor(life.Minutes)) res.RegionByMinute.Add(life.RegionId);

                double need = XpAt(life.Minutes) - fed;
                if (need > 0)
                {
                    var a = life.Absorb(need);
                    fed += need;
                    foreach (var bt in a.Breakthroughs)
                        if (!res.RealmMinute.ContainsKey(bt.Realm)) res.RealmMinute[bt.Realm] = life.Minutes;
                    foreach (var r in a.WokenBosses)
                        lines[r.Id] = new CheonhaBossLine
                        {
                            RegionId = r.Id,
                            BossKo = g.Bosses.First(b => b.Id == r.Boss).NameKo,
                            TargetKillSeconds = g.Bosses.First(b => b.Id == r.Boss).TargetKillSeconds,
                            WokeAt = life.Minutes, EngagedAt = double.NaN,
                        };
                }

                var f = life.Bosses.Fight;
                if (f != null && fighting == null && lines.TryGetValue(f.Region.Id, out var line))
                {
                    fighting = line;
                    line.EngagedAt = life.Minutes;
                    line.MaxHealth = f.MaxHealth;
                    line.FightSeconds = f.MaxHealth / (DpsAt(life.Minutes) * BossUptime);
                    fightLeft = line.FightSeconds;
                }
                if (fighting != null)
                {
                    fightLeft -= Dt * life.WorldTimeScale;
                    if (fightLeft <= 0)
                    {
                        life.DefeatBoss();
                        fighting.KilledAt = life.Minutes;
                        fighting = null;
                    }
                }
            }

            var record = life.Record ?? life.End(LifeEndWay.Fallen, 0);
            res.FinalLevel = life.Level;
            int last = (int)Math.Round(life.Minutes);
            if (last % 5 == 0 && last > 0) res.LevelByMinute[last] = life.Level;
            res.Qi = record.RebirthQi;
            res.Visited = record.VisitedRegions.ToList();
            res.Bosses = lines.Values.OrderBy(l => l.WokeAt).ToList();
            return (res, record);
        }

        /// <summary>T-19: 덩이 하나 = 적 하나 값(진기·기혈)인 천하에서 조정 시험별 레벨 속도. 숭산 소림 출생, 시드 1~seeds 가운데값.</summary>
        public static string ClusterXpReport(string? dataDir, int seeds = 5)
        {
            var ids = new[] { "baseline", "kill-xp", "kill-xp+health", "refill+kill-xp", "curve" };
            var marks = new[] { 5, 10, 15, 20, 25, 30 };
            var sb = new StringBuilder();
            sb.AppendLine("# 덩이와 처치 진기 점검(T-19)");
            sb.AppendLine();
            sb.AppendLine("`dotnet run --project core/BloodRain.Sim -- --cluster-xp docs/design/sim/cluster-xp.md` 로 다시 만든다.");
            sb.AppendLine();
            sb.AppendLine("천하에서는 경지가 오를수록 적 하나가 덩이(4·9·16·28·44·64명)로 나오지만 덩이 하나의 기혈과 진기는 적 하나 값이다(cheonha.json cluster). 그래서 화면 위 개체 수·기혈·진기는 1장 무리 모형과 같고, 다른 점은 지역 혈운 농도(적 기혈·마교 혈공)와 30분 길이다. 아래는 숭산 소림으로 태어나 30분을 돌린 Core `Life` 의 레벨이다(CheonhaSim, 땅의 주인 싸움 포함).");
            sb.AppendLine();
            sb.AppendLine("목표(11장): 5분 Lv15, 10분 Lv30, 15분 Lv45, 20분 Lv60, 30분 Lv90(현경). 조정 시험의 뜻은 `Scenarios.cs` 와 level-curve.md.");
            sb.AppendLine();
            sb.AppendLine("| 조정 시험 | " + string.Join(" | ", marks.Select(m => $"{m}분")) + " | 현경 도달 |");
            sb.AppendLine("| --- | " + string.Join(" | ", marks.Select(_ => "---")) + " | --- |");
            foreach (var id in ids)
            {
                var sc = Scenarios.Find(id);
                var data = sc.LoadData(dataDir);
                var g = data.Game;
                var sect = g.Cheonha.Sect("shaolin");
                var runs = Enumerable.Range(1, seeds).Select(seed =>
                {
                    var soul = new Soul(g);
                    soul.EndLife(soul.BeginLife(Rebirth.Prologue(g)).Finish(LifeEndWay.Fallen, 0));
                    var cand = new RebirthCandidate(sect, g.Cheonha.Region(sect.Region), Array.Empty<BirthTraitDef>());
                    return RunLife(data, soul, cand, seed, sc.ApplyOptions).result;
                }).ToList();
                string Lv(int m) { var a = runs.Select(r => r.LevelByMinute.TryGetValue(m, out var v) ? v : 0).OrderBy(x => x).ToList(); return a[a.Count / 2].ToString(); }
                var top = g.Balance.Realms.OrderBy(r => r.Level).Last().Id;
                var hy = runs.Select(r => r.RealmMinute.TryGetValue(top, out var v) ? v : double.NaN).OrderBy(x => double.IsNaN(x) ? 1e9 : x).ToList();
                sb.AppendLine($"| {sc.NameKo} | " + string.Join(" | ", marks.Select(Lv)) + $" | {M(hy[hy.Count / 2])} |");
            }
            sb.AppendLine();
            sb.AppendLine("'—'는 30분 안에 못 닿음. 처치 진기를 줄이는 안은 P-02 결정 대기라 데이터에는 넣지 않았다. 땅의 주인 처치 시간(T-40)도 이 레벨 곡선을 따라 같이 바뀐다.");
            return sb.ToString();
        }

        static string M(double minute)
        {
            if (double.IsNaN(minute)) return "—";
            int sec = (int)Math.Round(minute * 60);
            return $"{sec / 60}:{sec % 60:00}";
        }

        public static string Report(SimData data, int seeds = 5)
        {
            var g = data.Game;
            var sects = g.Cheonha.Sects.Where(s => !string.IsNullOrEmpty(s.Region)).ToList();
            var realms = g.Balance.Realms.Skip(1).ToList();
            var sb = new StringBuilder();
            sb.AppendLine("# 천하 한 생 시뮬레이션(T-17)");
            sb.AppendLine();
            sb.AppendLine("`dotnet run --project core/BloodRain.Sim -- --cheonha docs/design/sim/cheonha.md` 로 다시 만든다. 가정은 `core/BloodRain.Sim/CheonhaSim.cs` 맨 위.");
            sb.AppendLine();
            sb.AppendLine($"문파마다 정책 average, 시드 1~{seeds}의 가운데값. 진소운 시작 무공, 출생지 성질 없음, 처음 혼(연무장 0). 화력·진기는 level-curve 와 같은 무리 모형이라 **처치 진기가 높은 지금 데이터(B-07)를 그대로 따른다**. 보스전은 실제 치는 시간 {BossUptime:0%}로 잡았다.");
            sb.AppendLine();
            int headerEnd = sb.Length;
            var ratios = new List<double>();
            sb.AppendLine("## 경지 도달 시각");
            sb.AppendLine();
            sb.AppendLine("| 문파(출생지) | " + string.Join(" | ", realms.Select(r => r.NameKo)) + " | 30분 레벨 |");
            sb.AppendLine("| --- | " + string.Join(" | ", realms.Select(_ => "---")) + " | --- |");
            var all = new List<CheonhaLifeResult>();
            foreach (var s in sects)
            {
                var runs = Enumerable.Range(1, seeds).Select(seed => Run(data, s.Id, seed)).ToList();
                all.AddRange(runs);
                string Med(IEnumerable<double> xs) { var a = xs.Where(x => !double.IsNaN(x)).OrderBy(x => x).ToList(); return a.Count == 0 ? "—" : M(a[a.Count / 2]); }
                sb.AppendLine($"| {s.NameKo}({runs[0].Birth}) | " +
                    string.Join(" | ", realms.Select(r => Med(runs.Select(x => x.RealmMinute.TryGetValue(r.Id, out var m) ? m : double.NaN)))) +
                    $" | {runs.Select(x => x.FinalLevel).OrderBy(x => x).ElementAt(runs.Count / 2)} |");
            }
            sb.AppendLine();
            sb.AppendLine("목표(7장 경지와 축척 표): 이류 3:20, 일류 6:40, 절정 11:40, 초절정 16:40, 화경 23:20, 현경 30:00.");
            sb.AppendLine();
            sb.AppendLine("## 땅의 주인(출생지별, 시드 1)");
            sb.AppendLine();
            sb.AppendLine("깨어난 곳 중 가장 가까운 곳으로 걸어가 싸운다. '걸린 초'는 싸움 시작부터 처치까지.");
            sb.AppendLine();
            sb.AppendLine("| 문파 | 땅의 주인 | 깨어남 | 싸움 시작 | 기혈 | 걸린 초 | 목표 초 | 처치 |");
            sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- |");
            foreach (var r in all.Where(x => x.Seed == 1))
                foreach (var b in r.Bosses.Where(b => !double.IsNaN(b.EngagedAt)))
                    sb.AppendLine($"| {r.Sect} | {b.BossKo} | {M(b.WokeAt)} | {M(b.EngagedAt)} | {b.MaxHealth.ToString("N0", CultureInfo.InvariantCulture)} | {b.FightSeconds:0} | {b.TargetKillSeconds:0} | {M(b.KilledAt)} |");
            sb.AppendLine();
            sb.AppendLine("싸움을 못 걸어 본 땅의 주인은 표에서 뺐다(깨어나긴 했지만 30분 안에 차례가 안 옴).");
            sb.AppendLine();
            sb.AppendLine("### 출생지 땅의 주인 첫 싸움(시드 전부)");
            sb.AppendLine();
            sb.AppendLine("| 문파 | 땅의 주인 | 싸움 시작(가운데) | 걸린 초(가운데) | 목표 초 | 배 |");
            sb.AppendLine("| --- | --- | --- | --- | --- | --- |");
            foreach (var grp in all.GroupBy(x => x.Sect))
            {
                var firsts = grp.Select(x => x.Bosses.Where(b => !double.IsNaN(b.EngagedAt)).OrderBy(b => b.EngagedAt).FirstOrDefault()).Where(b => b != null).Select(b => b!).ToList();
                if (firsts.Count == 0) { sb.AppendLine($"| {grp.Key} | — | — | — | — | — |"); continue; }
                var secs = firsts.Select(b => b.FightSeconds).OrderBy(x => x).ToList();
                var starts = firsts.Select(b => b.EngagedAt).OrderBy(x => x).ToList();
                double med = secs[secs.Count / 2];
                if (grp.Key != "마교") ratios.Add(med / firsts[0].TargetKillSeconds);
                var who = firsts.GroupBy(b => b.BossKo).OrderByDescending(x => x.Count()).First().First();
                sb.AppendLine($"| {grp.Key} | {who.BossKo} | {M(starts[starts.Count / 2])} | {med:0} | {who.TargetKillSeconds:0} | {med / who.TargetKillSeconds:0.0} |");
            }
            sb.AppendLine();
            sb.AppendLine("## 윤회의 기(30분 쓰러짐 정산, 시드 1)");
            sb.AppendLine();
            sb.AppendLine("| 문파 | 구슬 | 보스 | 처음 밟은 땅 | 처음 오른 경지 | 합계 | 밟은 땅 수 |");
            sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");
            foreach (var r in all.Where(x => x.Seed == 1))
            {
                var q = r.Qi;
                int boss = q.BonusOf(RebirthQiBonusKind.BossFirstKill) + q.BonusOf(RebirthQiBonusKind.BossRepeatKill);
                sb.AppendLine($"| {r.Sect} | {q.FromOrbs} | {boss} | {q.BonusOf(RebirthQiBonusKind.FirstVisitRegion)} | {q.BonusOf(RebirthQiBonusKind.FirstReachRealm)} | {q.Total} | {r.Visited.Count} |");
            }
            var sum = new StringBuilder();
            sum.AppendLine("## 읽을 것");
            sum.AppendLine();
            var rm = all.Where(x => x.Sect != "마교").Select(x => x.RealmMinute.TryGetValue(realms.Last().Id, out var v) ? v : double.NaN).Where(v => !double.IsNaN(v)).OrderBy(v => v).ToList();
            if (rm.Count > 0)
                sum.AppendLine($"- 현경 도달은 마교 빼고 가운데 {M(rm[rm.Count / 2])}(목표 30:00). 처치 진기가 많은 지금 데이터 탓이고 B-07·P-02 와 같은 이야기다. 마교는 혈공(진기 × 혈운 농도)이 겹쳐 더 빠르다. 다만 이 표는 숭산 진기 흐름에 농도를 곱하기만 해서 적이 단단해지는 쪽이 빠졌다. 그쪽까지 넣은 값은 [blood-art.md](blood-art.md).");
            if (ratios.Count > 0)
            {
                ratios.Sort();
                sum.AppendLine($"- 출생지 땅의 주인 첫 싸움이 목표 처치 시간의 {ratios.First():0.0}~{ratios.Last():0.0}배(가운데 {ratios[ratios.Count / 2]:0.0}배) 걸린다. 땅의 주인은 절정에 깨어나는데 보스 기혈 식(7장, 20분 화력 기준 ÷ 2.6)이 그 무렵 화력보다 천천히 줄어서다. 깨어나는 경지를 늦추거나, 기혈 식을 싸움 시작 분의 화력에 맞추거나, 처치 시간 목표를 늘리는 쪽 중 하나를 골라야 한다(사람 결정).");
            }
            var fought = all.Where(x => x.Seed == 1).Select(x => $"{x.Sect} {x.Bosses.Count(b => !double.IsNaN(b.EngagedAt))}").ToList();
            sum.AppendLine($"- 땅의 주인은 절정·초절정에 한꺼번에 깨어나지만 30분 안에 실제로 싸움을 거는 수는 문파마다 다르다(시드 1: {string.Join(", ", fought)}). 첫 싸움이 길면 다른 땅을 거의 못 밟는다.");
            sum.AppendLine();
            sb.Insert(headerEnd, sum.ToString());
            return sb.ToString();
        }
    }
}
