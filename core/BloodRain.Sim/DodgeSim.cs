// 땅의 주인 패턴을 피할 수 있는가(밸런스 시뮬레이션). 보고서: docs/design/sim/dodge.md
//
// contact.md 는 패턴에 맞으면 얼마나 아픈지만 셌다. 여기서는 예고가 뜬 순간 플레이어가 그 자리에 서 있었다고 보고,
//   - 판정 범위 밖으로 나가려면 몇 m 를 가야 하는지(몸 반지름 포함, Core BossPatterns.InCone·InCircle·ScatterOffsets 그대로)
//   - 예고 시간 동안 걸어서(기본 이동속도 100), 또는 경공 한 번을 섞어서 그만큼 갈 수 있는지
// 를 본다. 판정 순간에 경공 무적을 겹쳐 버티는 길은 따로 적는다(무적 시간 안에 판정이 끝나는가).
// 좌표는 예고를 시작할 때 플레이어 자리를 0,0 으로 둔 화면 기준 m, 축척 배율 1.
// 소환·시야·필드 나누기·끌어당기기처럼 '빠져나갈 거리'가 없는 패턴과, 데이터에 판정 크기가 없는 탄은 뺀다(보고서에 이유를 적는다).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;

namespace BloodRain.Sim
{
    public static class DodgeSim
    {
        /// <summary>플레이어 몸 반지름(m). contact.md 와 같은 시뮬레이터 가정.</summary>
        public const float BodyRadius = 0.4f;
        /// <summary>이동속도 100의 초속(m). CheonhaSim 과 같다.</summary>
        public const double WalkMps = CheonhaSim.WalkMetersPerSecond;
        /// <summary>흩뿌리는 패턴(범종·물기둥)을 굴려 보는 횟수.</summary>
        public const int Casts = 1000;

        /// <summary>판정 범위 밖으로 나가는 가장 짧은 거리(m). hit(x, y) 는 몸 가운데가 x, y 일 때 맞는가.</summary>
        public static double EscapeDistance(Func<float, float, bool> hit, double max = 25, double step = 0.05, int directions = 180)
        {
            for (double r = 0; r <= max + 1e-9; r += step)
            {
                if (r < 1e-9) { if (!hit(0, 0)) return 0; continue; }
                for (int k = 0; k < directions; k++)
                {
                    double a = 2 * Math.PI * k / directions;
                    if (!hit((float)(Math.Cos(a) * r), (float)(Math.Sin(a) * r))) return r;
                }
            }
            return double.PositiveInfinity;
        }

        /// <summary>예고 seconds 동안 걸어서 가는 거리(m).</summary>
        public static double WalkReach(double seconds, double speed = 100) => WalkMps * speed / 100.0 * seconds;

        /// <summary>예고 seconds 안에 경공 한 번을 섞어 가는 거리(m). 경공은 Duration 동안 Distance 를 가고, 남은 시간은 걷는다.</summary>
        public static double DashReach(DashDef d, double seconds, double speed = 100) =>
            seconds < d.Duration ? d.Distance * seconds / d.Duration : d.Distance + WalkReach(seconds - d.Duration, speed);

        /// <summary>부채꼴. 보스에서 bossDistance 떨어져 부채꼴 가운데 줄 위에 서 있을 때.</summary>
        public static double ConeEscape(BossPatternDef p, float bossDistance) =>
            EscapeDistance((x, y) => BossPatterns.InCone(p, x + bossDistance, y, 0f, BodyRadius));

        /// <summary>부채꼴에서 빠져나갈 거리가 가장 먼 자리(보스와의 거리, 거리).</summary>
        public static (float At, double Escape) ConeWorst(BossPatternDef p)
        {
            float best = 0; double worst = 0;
            for (float r = 0.5f; r <= p.Radius + 1e-3f; r += 0.25f)
            {
                double e = ConeEscape(p, r);
                if (e > worst) { worst = e; best = r; }
            }
            return (best, worst);
        }

        /// <summary>동그라미 여러 곳. 흩뿌림이 있으면 Core ScatterOffsets 로 casts 번 굴린 거리들(오름차순), 없으면 한 번.</summary>
        public static double[] CirclesEscape(BossPatternDef p, int casts = Casts, int seed = 1)
        {
            var rng = new Random(seed);
            int n = p.Scatter > 0 ? casts : 1;
            var list = new double[n];
            for (int i = 0; i < n; i++)
            {
                var offs = p.Scatter > 0 ? BossPatterns.ScatterOffsets(p, rng) : new (float x, float y)[Math.Max(1, p.Count)];
                list[i] = EscapeDistance((x, y) => offs.Any(o => BossPatterns.InCircle(p.Radius, x - o.x, y - o.y, BodyRadius)), step: 0.05, directions: 120);
            }
            Array.Sort(list);
            return list;
        }

        /// <summary>몸 너비만큼의 띠(일섬·사슬·얼음 가시 줄)를 옆으로 비켜나는 거리. 가운데 줄 위에 서 있을 때.</summary>
        public static double StripEscape(float halfWidth) => halfWidth + BodyRadius;

        /// <summary>판 전체를 쓸고 지나가는 띠(역류 파도)가 몸을 지나가는 시간(초). dashToward 면 띠 쪽으로 경공.</summary>
        public static double WaveOverlapSeconds(BossPatternDef p, DashDef d, bool dashToward)
        {
            double v = WalkMps * p.ProjectileSpeed / 100.0;
            double rel = dashToward ? v + d.Distance / d.Duration : v;
            return (p.Width + 2 * BodyRadius) / rel;
        }

        /// <summary>부채꼴로 퍼지는 탄 사이 틈(m). 보스에서 distance 떨어진 자리, 탄 크기는 0으로 본다.</summary>
        public static double FanGap(BossPatternDef p, float distance) =>
            p.Count <= 1 ? double.PositiveInfinity : distance * (p.AngleDegrees / (p.Count - 1)) * Math.PI / 180.0;

        /// <summary>사방으로 고르게 퍼지는 탄 사이 틈(m). 탄 크기 0.</summary>
        public static double RadialGap(BossPatternDef p, float distance) => distance * 2 * Math.PI / Math.Max(1, p.Count);

        /// <summary>플레이어 이동속도 playerSpeed 보다 빠른 적(순간이동형 speed 0 은 뺀다). enemyMultiplier 는 적 이동속도 배율(혈월 등).</summary>
        public static List<EnemyDef> Faster(GameData g, double playerSpeed, double enemyMultiplier = 1) =>
            g.Enemies.Where(e => e.Speed > 0 && e.Speed * enemyMultiplier > playerSpeed + 1e-6).OrderByDescending(e => e.Speed).ToList();

        public sealed class Row
        {
            public string Boss = "", Pattern = "", Note = "";
            public double Telegraph, EscapeMedian, Escape90, Walk, Dash;
            public double WalkOk, DashOk; // 비율 0~1
        }

        static Row Make(string boss, BossPatternDef p, double telegraph, double[] esc, DashDef d, string note)
        {
            double walk = WalkReach(telegraph), dash = DashReach(d, telegraph);
            return new Row
            {
                Boss = boss, Pattern = p.NameKo, Note = note, Telegraph = telegraph,
                EscapeMedian = Quantile(esc, 0.5), Escape90 = Quantile(esc, 0.9), Walk = walk, Dash = dash,
                WalkOk = esc.Count(e => e <= walk + 1e-9) / (double)esc.Length,
                DashOk = esc.Count(e => e <= dash + 1e-9) / (double)esc.Length,
            };
        }

        static double Quantile(double[] sorted, double q) => sorted[Math.Min(sorted.Length - 1, (int)Math.Floor(q * (sorted.Length - 1) + 0.5))];

        /// <summary>빠져나갈 거리를 셀 수 있는 패턴 전부.</summary>
        public static List<Row> Rows(GameData g)
        {
            var d = g.Balance.Dash;
            var rows = new List<Row>();
            foreach (var (bossId, set) in g.BossPatterns.Select(kv => (kv.Key, kv.Value)))
            {
                string boss = g.Bosses.FirstOrDefault(b => b.Id == bossId)?.NameKo ?? bossId;
                foreach (var p in set.Patterns.Concat(set.Phase2))
                {
                    switch (p.Shape)
                    {
                        case "cone":
                            var (at, worst) = ConeWorst(p);
                            rows.Add(Make(boss, p, p.TelegraphSeconds, new[] { worst }, d, $"보스에서 {F1(at)}m 떨어져 가운데 줄 위(가장 먼 자리)"));
                            break;
                        case "circles" when p.Arrangement == "":
                            rows.Add(Make(boss, p, p.TelegraphSeconds, CirclesEscape(p), d,
                                p.Scatter > 0 ? $"{p.Count}곳을 ±{F1(p.Scatter)}m 안에 흩뿌림, {Casts}번 굴림" : "플레이어 자리 한 곳"));
                            break;
                        case "circles":
                            rows.Add(Make(boss, p, p.TelegraphSeconds, new[] { (double)(p.Radius + BodyRadius) }, d, "별 하나가 플레이어 자리에 떨어진다고 봄(별자리 배치는 Core 에 없다)"));
                            break;
                        case "blink_slam":
                            rows.Add(Make(boss, p, p.TelegraphSeconds, new[] { (double)(p.Radius + BodyRadius) }, d, $"첫 내려찍기. 뒤 {p.Count - 1}번은 {F1(p.WaveIntervalSeconds)}초마다"));
                            rows.Add(Make(boss, p, p.WaveIntervalSeconds, new[] { (double)(p.Radius + BodyRadius) }, d, "둘째부터(앞 내려찍기 때 자리를 노린다고 봄)"));
                            break;
                        case "line":
                        case "pull_line":
                            rows.Add(Make(boss, p, p.TelegraphSeconds, new[] { StripEscape(p.Width * 0.5f) }, d, "가운데 줄 위에서 옆으로"));
                            break;
                        case "chain_circles":
                            rows.Add(Make(boss, p, p.TelegraphSeconds, new[] { StripEscape(p.Radius) }, d, "가시 줄 위에서 옆으로(첫 가시 기준)"));
                            break;
                    }
                }
            }
            return rows;
        }

        public static string Report(SimData data)
        {
            var g = data.Game;
            var d = g.Balance.Dash;
            var sb = new StringBuilder();
            sb.AppendLine("# 땅의 주인 패턴 피하기");
            sb.AppendLine();
            sb.AppendLine("`dotnet run --project core/BloodRain.Sim -- --dodge docs/design/sim/dodge.md` 로 다시 쓴다. 코드는 `core/BloodRain.Sim/DodgeSim.cs`.");
            sb.AppendLine();
            sb.AppendLine($"예고가 뜬 순간 플레이어가 노림 자리에 서 있다고 보고, 판정 밖으로 나가는 가장 짧은 거리(몸 반지름 {F1(BodyRadius)}m 포함)를 Core `BossPatterns` 판정 그대로 쟀다. "
                + $"걸음은 이동속도 100 = 초속 {F1(WalkMps)}m(축척 배율 1), 경공은 {F2(d.Duration)}초에 {F1(d.Distance)}m 하고 남은 예고 시간은 걷는다. "
                + $"경공 무적은 {F1(d.Iframes)}초, 충전 {d.Charges}회, 하나 차는 데 {F0(d.RechargeSeconds)}초.");
            sb.AppendLine();
            sb.AppendLine("## 걸어서, 경공 한 번으로");
            sb.AppendLine();
            sb.AppendLine("| 땅의 주인 | 패턴 | 예고(초) | 빠져나갈 거리 m(가운데값 / 90%) | 걸어서 닿는 m | 경공 섞어 닿는 m | 걸어서 피함 | 경공으로 피함 | 셈한 자리 |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
            var rows = Rows(g);
            foreach (var r in rows)
                sb.AppendLine($"| {r.Boss} | {r.Pattern} | {F1(r.Telegraph)} | {F1(r.EscapeMedian)} / {F1(r.Escape90)} | {F1(r.Walk)} | {F1(r.Dash)} | {Pct(r.WalkOk)} | {Pct(r.DashOk)} | {r.Note} |");
            sb.AppendLine();

            sb.AppendLine("## 무적으로 버티기와 탄 틈");
            sb.AppendLine();
            sb.AppendLine("| 땅의 주인 | 패턴 | 셈 | 값 |");
            sb.AppendLine("|---|---|---|---|");
            var extra = new List<string>();
            foreach (var (bossId, set) in g.BossPatterns.Select(kv => (kv.Key, kv.Value)))
            {
                string boss = g.Bosses.FirstOrDefault(b => b.Id == bossId)?.NameKo ?? bossId;
                foreach (var p in set.Patterns.Concat(set.Phase2))
                {
                    if (p.Shape == "sweep_wave")
                    {
                        sb.AppendLine($"| {boss} | {p.NameKo} | 서서 무적으로 버팀: 띠({F1(p.Width)}m)가 몸을 지나는 초 | {F2(WaveOverlapSeconds(p, d, false))}초 (무적 {F1(d.Iframes)}초) |");
                        sb.AppendLine($"| {boss} | {p.NameKo} | 띠 쪽으로 경공해 뚫음 | {F2(WaveOverlapSeconds(p, d, true))}초 (무적 {F1(d.Iframes)}초) |");
                    }
                    else if (p.Shape == "projectile_fan")
                        foreach (var dist in new[] { 3f, 6f, 10f })
                            sb.AppendLine($"| {boss} | {p.NameKo} | 보스에서 {F0(dist)}m, 탄 {p.Count}발 사이 틈(탄 크기 0) | {F2(FanGap(p, dist))}m (몸 너비 {F1(2 * BodyRadius)}m) |");
                    else if (p.Shape == "radial_burst")
                        foreach (var dist in new[] { 3f, 6f, 10f })
                            sb.AppendLine($"| {boss} | {p.NameKo} | 보스에서 {F0(dist)}m, 탄 {p.Count}발 사이 틈(탄 크기 0) | {F2(RadialGap(p, dist))}m (몸 너비 {F1(2 * BodyRadius)}m) |");
                    else if (!new[] { "cone", "circles", "blink_slam", "line", "pull_line", "chain_circles" }.Contains(p.Shape))
                        extra.Add($"{boss} {p.NameKo}({p.Shape})");
                }
            }
            sb.AppendLine();
            sb.AppendLine("셈하지 않은 패턴: " + string.Join(", ", extra) + ". 소환·시야·필드 나누기·강화는 빠져나갈 거리가 없고, 끌어당기기(태극권)는 당기는 힘이, 나선 탄(꽃잎 나선)은 탄 크기가 데이터에 없다.");
            sb.AppendLine();

            sb.AppendLine("## 걸어서 못 떼어 놓는 적");
            sb.AppendLine();
            float slow = g.Balance.Yinyang.Extreme.BacklashSlow;
            double moon = g.BossPatterns.Values.SelectMany(v => v.Phase2).Where(p => p.EnemySpeedMultiplier > 0).Select(p => (double)p.EnemySpeedMultiplier).DefaultIfEmpty(1).Max();
            sb.AppendLine($"enemies.json speed(플레이어 기본 100 기준)가 플레이어보다 큰 적. 순간이동형(speed 0)은 뺀다. 땅의 주인 이동속도는 {F0(g.BossPatterns.Values.Min(v => v.MoveSpeed))}~{F0(g.BossPatterns.Values.Max(v => v.MoveSpeed))}로 모두 플레이어보다 느리다.");
            sb.AppendLine();
            sb.AppendLine("| 경우 | 플레이어 속도 | 더 빠른 적 |");
            sb.AppendLine("|---|---|---|");
            string List(IEnumerable<EnemyDef> es) { var l = es.Select(e => $"{e.NameKo} {F0(e.Speed)}").ToList(); return l.Count == 0 ? "없음" : $"{l.Count}종: " + string.Join(", ", l); }
            sb.AppendLine($"| 평소 | 100 | {List(Faster(g, 100))} |");
            sb.AppendLine($"| 기혈역류 중(-{F0(slow * 100)}%) | {F0(100 * (1 - slow))} | {List(Faster(g, 100 * (1 - slow)))} |");
            if (moon > 1) sb.AppendLine($"| 혈월(적 ×{F1(moon)}) | 100 | {List(Faster(g, 100, moon))} |");
            sb.AppendLine();
            sb.AppendLine($"역류 중엔 적 {Faster(g, 100 * (1 - slow)).Count}종이 플레이어보다 빨라진다(전체 {g.Enemies.Count(e => e.Speed > 0)}종 가운데). 역류를 경공으로 끊는 이유가 곧 이것이다.");
            sb.AppendLine();

            sb.AppendLine("## 읽는 법");
            sb.AppendLine();
            var walkFail = rows.Where(r => r.WalkOk < 0.5).ToList();
            var dashFail = rows.Where(r => r.DashOk < 0.5).ToList();
            sb.AppendLine($"- 걸어서 절반도 못 피하는 것: {(walkFail.Count == 0 ? "없음" : string.Join(", ", walkFail.Select(r => $"{r.Boss} {r.Pattern}({F1(r.EscapeMedian)}m 대 {F1(r.Walk)}m)")))}. 이 패턴들은 노림 자리에 서 있으면 경공이 있어야 거리로 빠진다.");
            sb.AppendLine($"- 경공을 섞어도 절반을 못 피하는 것: {(dashFail.Count == 0 ? "없음" : string.Join(", ", dashFail.Select(r => $"{r.Boss} {r.Pattern}({F1(r.EscapeMedian)}m 대 {F1(r.Dash)}m)")))}.");
            double rate = 1.0 / d.RechargeSeconds;
            var needDash = rows.Where(r => r.WalkOk < 0.5).GroupBy(r => r.Boss).Select(gp => $"{gp.Key} {gp.Count()}").ToList();
            sb.AppendLine($"- 경공은 {F0(d.RechargeSeconds)}초에 하나씩 찬다(초당 {F2(rate)}번). 패턴 간격이 {F1(g.BossPatterns.Values.Min(v => v.IntervalSeconds))}~{F1(g.BossPatterns.Values.Max(v => v.IntervalSeconds))}초(2막 {F1(g.BossPatterns.Values.Min(v => v.Phase2IntervalSeconds))}~{F1(g.BossPatterns.Values.Max(v => v.Phase2IntervalSeconds))}초)라 세 패턴 가운데 하나만 경공이 필요하면 충전이 남고, 내려찍기처럼 한 시전에 여러 번 필요하면 충전 {d.Charges}회가 바로 바닥난다. 걸어서 못 피하는 줄 수: {(needDash.Count == 0 ? "없음" : string.Join(", ", needDash))}.");
            foreach (var set in g.BossPatterns)
                foreach (var p in set.Value.Patterns.Where(p => p.Shape == "blink_slam" && p.Count > d.Charges && WalkReach(p.WaveIntervalSeconds) < p.Radius + BodyRadius))
                {
                    string boss = g.Bosses.FirstOrDefault(b => b.Id == set.Key)?.NameKo ?? set.Key;
                    sb.AppendLine($"- {boss} {p.NameKo}은 한 시전에 {p.Count}번 내려찍는데 둘째부터는 {F1(p.WaveIntervalSeconds)}초 간격이라 걸어서는 {F1(WalkReach(p.WaveIntervalSeconds))}m 뿐이다(빠져나갈 거리 {F1(p.Radius + BodyRadius)}m). 경공 {d.Charges}회를 다 써도 {p.Count - d.Charges}번은 무적({F1(d.Iframes)}초)을 판정 순간에 맞추거나 맞아야 한다. 뒤 내려찍기가 어디를 노리는지(앞 내려찍기 자리, 지금 자리)는 데이터에 없어 Unity 구현 때 정해야 한다.");
                }
            foreach (var set in g.BossPatterns)
                foreach (var p in set.Value.Phase2.Concat(set.Value.Patterns).Where(p => p.Shape == "sweep_wave"))
                {
                    string boss = g.Bosses.FirstOrDefault(b => b.Id == set.Key)?.NameKo ?? set.Key;
                    sb.AppendLine($"- {boss} {p.NameKo}는 서서 무적으로 버티면 띠가 몸을 지나는 {F2(WaveOverlapSeconds(p, d, false))}초가 무적 {F1(d.Iframes)}초보다 길어 맞는다. 띠 쪽으로 경공해야({F2(WaveOverlapSeconds(p, d, true))}초) 뚫린다. 판 전체를 쓰는 띠라면 피하는 길은 이것뿐이니 튜토리얼 문구나 예고 그림이 '파도 쪽으로'를 알려 줘야 한다.");
                }
            var tight = new List<string>();
            foreach (var p in g.BossPatterns.Values.SelectMany(v => v.Patterns.Concat(v.Phase2)))
            {
                Func<float, double>? gap = p.Shape == "projectile_fan" ? x => FanGap(p, x) : p.Shape == "radial_burst" ? x => RadialGap(p, x) : (Func<float, double>?)null;
                if (gap == null) continue;
                // 틈이 몸 너비와 같아지는 거리
                float at = (float)(2 * BodyRadius / gap(1f));
                tight.Add($"{p.NameKo}는 보스에서 {F1(at)}m 안");
            }
            sb.AppendLine($"- 탄 틈은 탄 크기를 0으로 본 값이라 실제는 더 좁다. {string.Join(", ", tight)}이면 틈이 몸 너비보다 좁아 사이로 못 빠진다. 탄 판정 크기를 데이터에 넣어야 다시 잴 수 있다.");
            sb.AppendLine("- 위 거리는 '노림 자리에 가만히 서 있다가 예고를 보고 움직인다'일 때다. 늘 움직이며 싸우면 대개 이보다 짧다. 반대로 적 무리에 막혀 길이 없으면 더 멀다.");
            sb.AppendLine("- 시뮬레이터 가정: 몸 반지름 0.4m, 이동속도 100 = 초속 4.6m, 예고 시작과 동시에 움직인다(반응 시간 0), 경공 무적을 판정 순간에 맞추는 길은 '무적으로 버티기' 표에만 셌다.");
            return sb.ToString();
        }

        static string F0(double v) => v.ToString("0", CultureInfo.InvariantCulture);
        static string F1(double v) => double.IsInfinity(v) ? "∞" : v.ToString("0.0", CultureInfo.InvariantCulture);
        static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
        static string Pct(double v) => (v * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
    }
}
