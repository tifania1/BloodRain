// 혈마의 그림자를 얼마나 떼어 내고 버티는가(밸런스 시뮬레이션). 보고서: docs/design/sim/shadow.md
//
// 기획서 7장: 현경인데 봉인석이 모자라면 30분부터 그림자가 쫓아오고 5분마다 강해진다. 버틴 시간이 리더보드에 오른다.
// 여기서는 그림자 하나만 놓고(다른 적·지형 없음) 곧게 달아나는 몸을 1차원으로 쫓게 한다.
//   - 몸 속도 = 문파 이동 배율 × (1 + 신법 보너스), 경공은 Core Dash 그대로(충전 차례로 하나씩, 그림자가 붙기 직전에만 쓴다)
//   - 그림자 세기는 Core BloodShadow.Strength 그대로, 단계는 LifeDirector.ShadowStep 과 같은 식(30분부터 5분마다)
//   - 붙어 있으면 hitIntervalSeconds 마다 한 대, 경공 무적 중엔 안 맞는다. 받는 피해는 Core Damage.Taken(호신) 뒤 화경 -10%
// 를 보고, 처음 붙잡히는 분과 그림자만으로 쓰러지는 분을 잰다.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using BloodRain.Core.World;

namespace BloodRain.Sim
{
    public sealed class ShadowRun
    {
        /// <summary>처음 몸에 닿은 분. 끝까지 안 닿으면 null.</summary>
        public double? CaughtMinute;
        /// <summary>그림자만으로 기혈 0이 된 분. 끝까지 버티면 null.</summary>
        public double? DeadMinute;
        public int Hits;
    }

    public static class ShadowSim
    {
        /// <summary>[가정] 그림자가 나타날 때 몸과의 거리(m). 현경 첫 화면 가장자리쯤.</summary>
        public const double StartGap = 10;
        /// <summary>[가정] 몸 반지름 + 그림자 반지름(m). 둘 다 0.4(contact.md 와 같은 몸).</summary>
        public const double TouchGap = 0.8;
        /// <summary>[가정] 경공을 쓰는 거리(m). 이보다 가까워지면 충전이 있을 때 쓴다.</summary>
        public const double DashAtGap = 1.5;
        /// <summary>재는 끝(분).</summary>
        public const double EndMinute = 120;
        public const double Dt = 0.02;

        /// <summary>신법 네 경우: 맨몸, 연무장 신법 다 삼, 운룡보 Lv5, 둘 다. 운룡보 Lv5 는 경공 충전 +1 이 붙는다.</summary>
        public static IReadOnlyList<(string Ko, float Bonus, int ExtraCharges)> SpeedCases(GameData g)
        {
            var t = g.Balance.Training.Items.First(i => i.Stat == "MoveSpeed");
            float training = t.PerLevel * t.MaxLevel;
            var unryong = g.Passives.First(p => p.Id == "unryong");
            float passive = unryong.Effects.Where(e => e.Stat == "MoveSpeed").Sum(e => e.PerLevel) * g.Balance.Levels.PassiveMax;
            return new[] { ("맨몸", 0f, 0), ("연무장 신법", training, 0), ("운룡보 Lv5", passive, 1), ("둘 다", training + passive, 1) };
        }

        /// <summary>현경까지 오른 몸의 기본 경공 충전: 데이터 기본 + 일류 보상(dash_charge_N).</summary>
        public static int BaseCharges(GameData g)
        {
            int n = g.Balance.Dash.Charges;
            foreach (var r in g.Balance.Realms)
                foreach (var tok in (r.Reward ?? "").Split(','))
                    if (tok.StartsWith("dash_charge_", StringComparison.Ordinal) &&
                        int.TryParse(tok.Substring("dash_charge_".Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
                        n += v;
            return n;
        }

        /// <summary>그 분의 그림자 단계(LifeDirector.ShadowStep 에서 봉인석·경지 조건을 뺀 식).</summary>
        public static int StepAt(LifeEndDef end, double minute)
        {
            if (minute < end.ShadowFromMinute) return 0;
            if (end.ShadowStepMinutes <= 0) return 1;
            return 1 + (int)Math.Floor((minute - end.ShadowFromMinute) / end.ShadowStepMinutes + 1e-4);
        }

        /// <summary>걸음과 경공을 섞어 오래 달아날 때의 평균 속도(이동속도 단위, 100 = 기본).</summary>
        public static double SustainedSpeed(DashDef d, double walkSpeed)
        {
            double walk = walkSpeed / 100.0 * CheonhaSimWalk;
            double gain = Math.Max(0, d.Distance - walk * d.Duration) / d.RechargeSeconds;
            return (walk + gain) / CheonhaSimWalk * 100.0;
        }

        const double CheonhaSimWalk = CheonhaSim.WalkMetersPerSecond;

        public static ShadowRun Run(GameData g, double speed, int charges, float maxHealth, float armor, float recovery, float takenCut)
            => Run(g, g.Cheonha.LifeEnd.Shadow, 0, speed, charges, maxHealth, armor, recovery, takenCut);

        /// <summary>그림자 세기를 바꿔 끼워 돌린다(고칠 방향 비교용). teleportGap 이 0보다 크면 단계가 오를 때마다 그림자가 몸 뒤 그 거리로 순간이동한다(더 가까이 있으면 그대로).</summary>
        public static ShadowRun Run(GameData g, BloodShadowDef shadow, double teleportGap, double speed, int charges, float maxHealth, float armor, float recovery, float takenCut)
        {
            var end = g.Cheonha.LifeEnd;
            int lastStep = 0;
            var dash = new Dash(g.Balance.Dash);
            dash.SetMaxCharges(charges);
            double gap = StartGap, hp = maxHealth, hitCd = 0;
            var run = new ShadowRun();
            double walkMps = speed / 100.0 * CheonhaSimWalk;
            long steps = (long)Math.Round((EndMinute - end.ShadowFromMinute) * 60 / Dt);
            for (long i = 0; i < steps; i++)
            {
                double minute = end.ShadowFromMinute + i * Dt / 60.0;
                int step = StepAt(end, minute);
                var s = BloodShadow.Strength(shadow, step);
                if (step != lastStep)
                {
                    if (teleportGap > 0 && lastStep > 0 && gap > teleportGap) gap = teleportGap;
                    lastStep = step;
                }
                if (gap < DashAtGap && !dash.IsDashing) dash.TryStart(1, 0);
                bool dashing = dash.IsDashing;
                var (dx, _) = dash.Tick((float)Dt);
                double moved = dashing ? dx : walkMps * Dt;
                gap += moved - s.MoveSpeed / 100.0 * CheonhaSimWalk * Dt;
                if (gap < TouchGap) gap = TouchGap; // 그림자는 몸을 지나가지 않는다

                hp = Math.Min(maxHealth, hp + recovery * Dt);
                hitCd = Math.Max(0, hitCd - Dt);
                if (gap <= TouchGap + 1e-9)
                {
                    run.CaughtMinute ??= minute;
                    if (hitCd <= 0 && !dash.Invulnerable)
                    {
                        hp -= Damage.Taken(s.ContactDamage, armor, g.Balance.Contact.MinDamage) * (1 - takenCut);
                        run.Hits++;
                        hitCd = s.HitIntervalSeconds;
                        if (hp <= 0) { run.DeadMinute = minute; break; }
                    }
                }
            }
            return run;
        }

        /// <summary>문파 몸 하나, 신법 경우 하나, 방어 경우(맨몸·최대) 하나로 돌린다.</summary>
        public static ShadowRun RunSect(GameData g, SectDef sect, (string Ko, float Bonus, int ExtraCharges) speedCase, bool maxDefense)
            => RunSect(g, g.Cheonha.LifeEnd.Shadow, 0, sect, speedCase, maxDefense);

        public static ShadowRun RunSect(GameData g, BloodShadowDef shadow, double teleportGap, SectDef sect, (string Ko, float Bonus, int ExtraCharges) speedCase, bool maxDefense)
        {
            float iryu = ContactSim.RewardRatio(g, "iryu", "max_health_");
            float cut = ContactSim.RewardRatio(g, "hwagyeong", "damage_taken_minus");
            float traitArmor = sect.Trait == "flat_damage_reduction2" ? 2f : 0f;
            float hpBonus = iryu + (maxDefense ? ContactSim.MaxHealthBuild(g) : 0f);
            float armor = maxDefense ? ContactSim.ArmorCases(g).Last().Armor : traitArmor;
            float recovery = maxDefense ? ContactSim.RecoveryMax(g) : 0f;
            double speed = sect.MoveSpeed * 100.0 * (1 + speedCase.Bonus);
            return Run(g, shadow, teleportGap, speed, BaseCharges(g) + speedCase.ExtraCharges, sect.MaxHealth * (1 + hpBonus), armor, recovery, cut);
        }

        /// <summary>[가정] '순간이동' 안에서 그림자가 내려앉는 거리(m). 붙기 바로 앞.</summary>
        public const double TeleportGap = 1.0;

        /// <summary>T-42 고칠 방향 비교: 이름, 바꾼 그림자 세기, 단계마다 순간이동 거리(0이면 안 함).</summary>
        public static IReadOnlyList<(string Ko, BloodShadowDef Shadow, double Teleport)> Options(GameData g)
        {
            var now = g.Cheonha.LifeEnd.Shadow;
            BloodShadowDef With(float max) => new BloodShadowDef
            {
                ContactDamage = now.ContactDamage, DamageGrowth = now.DamageGrowth, MoveSpeed = now.MoveSpeed,
                SpeedPerStep = now.SpeedPerStep, MaxSpeed = max, HitIntervalSeconds = now.HitIntervalSeconds,
                EnemyDamagePerStep = now.EnemyDamagePerStep,
            };
            return new[]
            {
                ($"지금(상한 {F0(now.MaxSpeed)})", now, 0.0),
                ("(가) 상한 150", With(150), 0.0),
                ("(가) 상한 175", With(175), 0.0),
                ("(가) 상한 없음", With(float.MaxValue), 0.0),
                ($"(나) 지금 + 단계마다 순간이동", now, TeleportGap),
                ("(가)+(나) 상한 150 + 순간이동", With(150), TeleportGap),
            };
        }

        public static string Report(SimData data)
        {
            var g = data.Game;
            var end = g.Cheonha.LifeEnd;
            var d = g.Balance.Dash;
            var cases = SpeedCases(g);
            var sb = new StringBuilder();
            sb.AppendLine("# 혈마의 그림자 떼어 내기 (밸런스 시뮬레이션)");
            sb.AppendLine();
            sb.AppendLine("`dotnet run --project core/BloodRain.Sim -c Release -- --shadow docs/design/sim/shadow.md` 로 다시 쓴다. 코드는 `core/BloodRain.Sim/ShadowSim.cs`.");
            sb.AppendLine();
            sb.AppendLine($"기획서 7장 '생의 끝': 현경인데 봉인석이 모자라면 {F0(end.ShadowFromMinute)}분부터 혈마의 그림자가 쫓아오고 {F0(end.ShadowStepMinutes)}분마다 강해진다. 버틴 시간이 리더보드에 오른다. 여기서는 그림자 하나만 놓고(다른 적·지형 없음) 곧게 달아나는 몸을 쫓게 해, 처음 붙잡히는 분과 그림자만으로 쓰러지는 분을 잰다. {F0(EndMinute)}분까지만 잰다.");
            sb.AppendLine();

            sb.AppendLine("## 그림자 단계");
            sb.AppendLine();
            sb.AppendLine("| 단계 | 분 | 접촉 피해 | 이동속도 | 모든 적 피해 |");
            sb.AppendLine("| --- | --- | --- | --- | --- |");
            int capStep = 0;
            for (int step = 1; step <= 8; step++)
            {
                var s = BloodShadow.Strength(end.Shadow, step);
                double minute = end.ShadowFromMinute + end.ShadowStepMinutes * (step - 1);
                sb.AppendLine($"| {step} | {F0(minute)} | {F1(s.ContactDamage)} | {F0(s.MoveSpeed)} | {F1(s.EnemyDamageMultiplier)}배 |");
                if (capStep == 0 && s.MoveSpeed >= end.Shadow.MaxSpeed) capStep = step;
            }
            sb.AppendLine();
            sb.AppendLine($"이동속도는 {capStep}단계({F0(end.ShadowFromMinute + end.ShadowStepMinutes * (capStep - 1))}분)에 상한 {F0(end.Shadow.MaxSpeed)}에 닿고 그 뒤로는 피해만 오른다.");
            sb.AppendLine();

            sb.AppendLine("## 몸의 속도");
            sb.AppendLine();
            sb.AppendLine($"걸음은 이동속도 100 = 초속 {F1(CheonhaSimWalk)}m. 경공은 {F2(d.Duration)}초에 {F1(d.Distance)}m, 충전은 {F0(d.RechargeSeconds)}초에 하나씩이라 오래 달아나면 {F0(d.RechargeSeconds)}초마다 한 번 쓰는 셈이다. '지속'은 걸음과 경공을 섞은 평균 속도. 현경 몸의 경공 충전은 {BaseCharges(g)}회(기본 {d.Charges} + 일류 보상), 운룡보 Lv5 면 하나 더.");
            sb.AppendLine();
            sb.Append("| 몸 | 배율 |");
            foreach (var c in cases) sb.Append($" {c.Ko} 걸음 / 지속 |");
            sb.AppendLine();
            sb.Append("| --- | --- |");
            foreach (var _ in cases) sb.Append(" --- |");
            sb.AppendLine();
            foreach (var sect in g.Cheonha.Sects)
            {
                sb.Append($"| {sect.NameKo} | {F2(sect.MoveSpeed)} |");
                foreach (var c in cases)
                {
                    double w = sect.MoveSpeed * 100 * (1 + c.Bonus);
                    sb.Append($" {F0(w)} / {F0(SustainedSpeed(d, w))} |");
                }
                sb.AppendLine();
            }
            sb.AppendLine();

            sb.AppendLine("## 붙잡히는 분과 쓰러지는 분");
            sb.AppendLine();
            sb.AppendLine("칸마다 '처음 붙잡힌 분 / 그림자만으로 쓰러진 분'. `-` 는 재는 끝까지 그런 일이 없었다는 뜻이다. 방어 '보통'은 기혈 = 몸 × 이류 +10%, 호신 0(소림 성질 2), 회복 0. '최대'는 contact.md 의 최대(연무장 기혈·호신·회복 다 사고 혈해심법·금강체·매화심법 Lv5)를 다 얹은 것. 둘 다 화경 받는 피해 -10%.");
            sb.AppendLine();
            sb.Append("| 몸 |");
            foreach (var c in cases) sb.Append($" {c.Ko}·보통 | {c.Ko}·최대 |");
            sb.AppendLine();
            sb.Append("| --- |");
            foreach (var _ in cases) sb.Append(" --- | --- |");
            sb.AppendLine();
            var results = new List<(SectDef Sect, int Case, bool Max, ShadowRun Run)>();
            foreach (var sect in g.Cheonha.Sects)
            {
                sb.Append($"| {sect.NameKo} |");
                for (int ci = 0; ci < cases.Count; ci++)
                    foreach (bool max in new[] { false, true })
                    {
                        var r = RunSect(g, sect, cases[ci], max);
                        results.Add((sect, ci, max, r));
                        sb.Append($" {M(r.CaughtMinute)} / {M(r.DeadMinute)} |");
                    }
                sb.AppendLine();
            }
            sb.AppendLine();

            sb.AppendLine("## 고칠 방향별 (T-42)");
            sb.AppendLine();
            sb.AppendLine($"backlog T-42 의 (가)·(나)를 같은 셈으로 돌렸다. 방어는 '보통'. 칸은 '아홉 몸이 그림자만으로 쓰러진 분(가장 이른~가장 늦은)', 괄호는 {F0(EndMinute)}분까지 안 쓰러진 몸 수. (나)는 단계가 오를 때마다(5분마다) 그림자가 몸 뒤 {F1(TeleportGap)}m 로 내려앉는다고 두었다. (다)는 지금 줄과 같다.");
            sb.AppendLine();
            sb.Append("| 방향 |");
            foreach (var c in cases) sb.Append($" {c.Ko} |");
            sb.AppendLine();
            sb.Append("| --- |");
            foreach (var _ in cases) sb.Append(" --- |");
            sb.AppendLine();
            foreach (var opt in Options(g))
            {
                sb.Append($"| {opt.Ko} |");
                foreach (var c in cases)
                {
                    var dead = g.Cheonha.Sects.Select(sect => RunSect(g, opt.Shadow, opt.Teleport, sect, c, false).DeadMinute).ToList();
                    var hit = dead.Where(x => x.HasValue).Select(x => x!.Value).ToList();
                    int alive = dead.Count - hit.Count;
                    string range = hit.Count == 0 ? "-" : hit.Count == 1 ? F1(hit[0]) : $"{F1(hit.Min())}~{F1(hit.Max())}";
                    sb.Append($" {range}{(alive > 0 ? $" ({alive})" : "")} |");
                }
                sb.AppendLine();
            }
            sb.AppendLine();
            sb.AppendLine("- 상한을 올리면 단계마다 +" + F0(end.Shadow.SpeedPerStep) + "씩 계속 빨라져 맨몸·연무장 신법은 모두 잡힌다. 운룡보를 든 몸은 상한을 없애도 " + F0(EndMinute) + "분 안에 다 잡히지는 않는데, 그림자보다 빨랐던 30~50분 동안 벌려 둔 거리를 그림자가 다 좁혀야 해서다. 신법을 갖춘 만큼 늦게 잡히는 순서는 그대로 남는다.");
            sb.AppendLine("- 순간이동만 넣으면 그림자가 몸보다 느린 동안은 한두 대 맞고 경공으로 다시 떼어 낸다. 운룡보 몸은 여전히 안 쓰러진다. 대신 맨몸의 끝을 당겨 몸 사이 차이를 줄인다(가장 늦은 몸이 지금보다 이르다).");
            sb.AppendLine("- 둘을 섞으면 벌려 둔 거리가 5분마다 없어지니 운룡보 몸도 그림자가 몸보다 빨라지는 단계에서 곧 잡히기 시작한다. 그래도 경공이 넷인 '둘 다'는 대부분 버틴다. 버틴 시간의 폭을 정하는 건 상한과 '몸보다 빨라지는 단계'다.");
            sb.AppendLine("- 이 셈은 그림자가 몸에서 얼마나 멀리 떨어지든 그대로 쫓는다고 둔다. 실제로 그림자가 화면 밖 일정 거리 너머로 못 떨어지게(목줄) 하면 (나)와 같은 쪽으로 움직인다.");
            sb.AppendLine();

            sb.AppendLine("## 읽는 법");
            sb.AppendLine();
            var bare = results.Where(r => r.Case == 0 && !r.Max).ToList();
            var bareDead = bare.Where(r => r.Run.DeadMinute.HasValue).Select(r => r.Run.DeadMinute!.Value).ToList();
            if (bareDead.Count > 0)
                sb.AppendLine($"- 신법 없이 맨몸이면 {F1(bareDead.Min())}~{F1(bareDead.Max())}분에 그림자만으로 쓰러진다(쓰러진 몸 {bareDead.Count}/{bare.Count}). 버틴 시간은 30분 뒤로 {F1(bareDead.Min() - end.ShadowFromMinute)}~{F1(bareDead.Max() - end.ShadowFromMinute)}분.");
            var never = results.Where(r => !r.Run.CaughtMinute.HasValue).ToList();
            double minSafe = g.Cheonha.Sects.Min(s => s.MoveSpeed) * 100;
            var neverCases = never.GroupBy(r => cases[r.Case].Ko).Select(gp => $"{gp.Key} {gp.Select(x => x.Sect.Id).Distinct().Count()}몸").ToList();
            sb.AppendLine($"- 그림자 속도는 {F0(end.Shadow.MaxSpeed)}에서 멈추니 지속 속도가 그보다 빠르면 끝내 안 잡힌다. {F0(EndMinute)}분까지 한 번도 안 잡힌 경우: {(neverCases.Count == 0 ? "없음" : string.Join(", ", neverCases))}. 운룡보 Lv5 하나면 가장 느린 몸({F0(minSafe)})도 걸음만으로 상한을 넘는다({F0(minSafe * (1 + cases[2].Bonus))}). 리더보드 '버틴 시간'이 그림자로는 끝나지 않고, 다른 적(피해 배율이 5분마다 +{F1(end.Shadow.EnemyDamagePerStep)})이나 좌화로만 끝난다.");
            var caught = results.Where(r => r.Run.CaughtMinute.HasValue && r.Run.DeadMinute.HasValue && r.Max).ToList();
            if (caught.Count > 0)
            {
                double lag = caught.Average(r => r.Run.DeadMinute!.Value - r.Run.CaughtMinute!.Value) * 60;
                double lagMax = caught.Max(r => r.Run.DeadMinute!.Value - r.Run.CaughtMinute!.Value) * 60;
                sb.AppendLine($"- 방어를 다 얹어도 붙잡힌 뒤 평균 {F0(lag)}초(길어야 {F0(lagMax)}초) 만에 쓰러진다('최대' {caught.Count}칸). 그림자가 몸보다 빨라진 뒤엔 경공으로 떼어 내도 곧 다시 붙으니, 버틴 시간은 사실상 '지속 속도가 그림자에 따라잡히는 분'으로 정해진다. 기혈·호신·회복은 거의 보태지 않는다.");
            }
            sb.AppendLine("- 실제 판에서는 화면 위 적 무리가 길을 막고 그림자 곁에서 다른 적에게도 맞으니, 여기 분은 '그림자만으로'의 위쪽 끝이다. 반대로 지형(물·얼음·장막 잔재)과 기믹은 넣지 않았다.");
            sb.AppendLine($"- 시뮬레이터 가정: 그림자는 {F0(StartGap)}m 뒤에서 나타난다, 몸과 그림자가 {F1(TouchGap)}m 안이면 붙은 것, 그림자가 {F1(DashAtGap)}m 안으로 오면 충전이 있을 때 경공, 곧은 길로 끝없이 달아날 수 있다(현경엔 장막이 걷힌다), 간격 {F2(Dt)}초.");
            return sb.ToString();
        }

        static string M(double? v) => v.HasValue ? F1(v.Value) : "-";
        static string F0(double v) => v.ToString("0", CultureInfo.InvariantCulture);
        static string F1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
        static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
