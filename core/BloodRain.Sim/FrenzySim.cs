// 마교 몸 '마인의 피' 곡선 견주기(밸런스 시뮬레이션, R-04c 결정 돕기). 보고서: docs/design/sim/frenzy.md
//
// 기획서 5장: "기혈이 낮을수록 공격속도 증가(30% 이하 최대 +50%)". 끝값만 있고 그 사이와 '공격속도'를 무엇으로 셀지가 없다.
// Core 의 쾌속(CooldownReduction)은 쿨다운에 (1 - 쾌속)을 곱하고 상한(balance.json stats.cooldownReductionCap)에서 자른다.
// 그래서 같은 '+50%'라도 쾌속 0.5 로 넣으면 공격 빈도는 두 배, 빈도 배율 1.5 로 넣으면 1.5배다. 세 안을 나란히 둔다.
// 쾌속이 붙는 다른 곳(청명심법, 연무장, 혈광폭주 깨달음, 태극)은 데이터에서 읽는다. 무공 쿨다운 자체는 안 본다(배수만 본다).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BloodRain.Core.Data;

namespace BloodRain.Sim
{
    public static class FrenzySim
    {
        /// <summary>기획서 5장 끝값: 30% 이하에서 최대, 그 값 +50%.</summary>
        public const double FullAt = 0.3, MaxBonus = 0.5;
        public static readonly double[] HealthPoints = { 1.0, 0.7, 0.5, 0.4, 0.3, 0.1 };

        public enum Plan { CdrHalf, RateMultiplier, CdrThird }

        public static string PlanName(Plan p) => p switch
        {
            Plan.CdrHalf => "가. 쾌속 +0.5(추천안)",
            Plan.RateMultiplier => "나. 빈도 배율 x1.5(상한 밖)",
            _ => "다. 쾌속 +1/3(빈도 x1.5 를 쾌속으로)",
        };

        static string N(double v, string f = "0.00") => v.ToString(f, CultureInfo.InvariantCulture);

        /// <summary>기혈 100% 에서 0, FullAt 이하에서 1, 그 사이 선형.</summary>
        public static double Ramp(double health) =>
            health >= 1 ? 0 : health <= FullAt ? 1 : (1 - health) / (1 - FullAt);

        public sealed class Setup
        {
            public string Name = "";
            /// <summary>기혈 비율 → 마교 몫을 뺀 쾌속 합(상한 전).</summary>
            public Func<double, double> Cdr = _ => 0;
        }

        public static List<Setup> Setups(GameData g)
        {
            var b = g.Balance;
            var cheong = g.Passives.Single(p => p.Id == "cheongmyeong");
            double passive = cheong.Effects.Single(e => e.Stat == "CooldownReduction").PerLevel * b.Levels.PassiveMax;
            var tr = b.Training.Items.Single(i => i.Stat == "CooldownReduction");
            double training = tr.PerLevel * tr.MaxLevel;
            var frenzy = g.Insights.Concat(g.DemonicInsights).Single(i => i.Effect == "low_health_frenzy");
            double fThreshold = frenzy.Params["healthThreshold"], fCdr = frenzy.Params["cooldownReduction"];
            double harmony = b.Yinyang.Harmony.CooldownReduction;
            double pt = passive + training;
            return new List<Setup>
            {
                new Setup { Name = "맨몸", Cdr = _ => 0 },
                new Setup { Name = $"{cheong.NameKo} Lv{b.Levels.PassiveMax}({N(passive, "0.##")})", Cdr = _ => passive },
                new Setup { Name = $"+ 연무장 쾌속 끝({N(training, "0.###")})", Cdr = _ => pt },
                new Setup { Name = $"+ {frenzy.NameKo}(기혈 {N(fThreshold * 100, "0")}% 아래 {N(fCdr, "0.##")})", Cdr = h => pt + (h < fThreshold ? fCdr : 0) },
                new Setup { Name = $"+ 태극({N(harmony, "0.##")})", Cdr = h => pt + harmony + (h < fThreshold ? fCdr : 0) },
            };
        }

        /// <summary>공격 빈도 배수(쿨다운 1 / 실제 쿨다운). 마교 몫을 넣은 것.</summary>
        public static double Rate(Plan plan, double baseCdr, double health, double cap)
        {
            double r = Ramp(health);
            switch (plan)
            {
                case Plan.CdrHalf: return 1 / (1 - Math.Min(cap, baseCdr + MaxBonus * r));
                case Plan.CdrThird: return 1 / (1 - Math.Min(cap, baseCdr + (1 - 1 / (1 + MaxBonus)) * r));
                default: return (1 + MaxBonus * r) / (1 - Math.Min(cap, baseCdr));
            }
        }

        public static double RateWithout(double baseCdr, double cap) => 1 / (1 - Math.Min(cap, baseCdr));

        public static string Report(SimData data)
        {
            var g = data.Game;
            double cap = g.Balance.Stats.CooldownReductionCap;
            var setups = Setups(g);
            var sb = new StringBuilder();
            sb.AppendLine("# 마교 '마인의 피' 곡선 견주기 (R-04c)");
            sb.AppendLine();
            sb.AppendLine("`dotnet run --project core/BloodRain.Sim -- --frenzy docs/design/sim/frenzy.md` 로 다시 쓴다(`FrenzySim.cs`). 테스트가 이 파일이 지금 데이터와 같은지 본다.");
            sb.AppendLine();
            sb.AppendLine("기획서 5장은 마교 몸에 '기혈이 낮을수록 공격속도 증가(30% 이하 최대 +50%)'만 적었다. 세 안 모두 기혈 100% 에서 0, 30% 이하에서 끝값, 그 사이 선형이다. 갈리는 것은 '+50%'를 무엇으로 세느냐다.");
            sb.AppendLine();
            sb.AppendLine($"- 가. 쾌속 +0.5: 쾌속 합에 더하고 상한 {N(cap, "0.##")} 에서 자른다. 쿨다운이 절반이 되니 맨몸이면 공격 빈도가 두 배다.");
            sb.AppendLine("- 나. 빈도 배율 x1.5: 쾌속과 따로 쿨다운을 1.5 로 나눈다. 상한에 안 걸린다.");
            sb.AppendLine($"- 다. 쾌속 +1/3: 맨몸 빈도가 정확히 1.5배가 되는 쾌속 값. 상한 {N(cap, "0.##")} 안.");
            sb.AppendLine();
            sb.AppendLine("칸의 숫자는 공격 빈도 배수(쿨다운이 1 이면 1 / 실제 쿨다운)다. 괄호는 마교 몫 없이 같은 몸.");
            sb.AppendLine();
            foreach (Plan plan in Enum.GetValues(typeof(Plan)))
            {
                sb.AppendLine($"## {PlanName(plan)}");
                sb.AppendLine();
                sb.AppendLine("| 쾌속 출처(위에서 쌓임) | " + string.Join(" | ", HealthPoints.Select(h => $"기혈 {N(h * 100, "0")}%")) + " |");
                sb.AppendLine("| --- |" + string.Concat(HealthPoints.Select(_ => " --- |")));
                foreach (var s in setups)
                    sb.AppendLine($"| {s.Name} | " + string.Join(" | ", HealthPoints.Select(h =>
                        $"{N(Rate(plan, s.Cdr(h), h, cap))} ({N(RateWithout(s.Cdr(h), cap))})")) + " |");
                sb.AppendLine();
            }

            sb.AppendLine("## 30% 이하에서 마교 몫이 실제로 더하는 배수");
            sb.AppendLine();
            sb.AppendLine("| 쾌속 출처 | " + string.Join(" | ", Enum.GetValues(typeof(Plan)).Cast<Plan>().Select(PlanName)) + " |");
            sb.AppendLine("| --- | --- | --- | --- |");
            foreach (var s in setups)
            {
                double c = s.Cdr(FullAt);
                sb.AppendLine($"| {s.Name} | " + string.Join(" | ", Enum.GetValues(typeof(Plan)).Cast<Plan>().Select(p =>
                    "x" + N(Rate(p, c, FullAt, cap) / RateWithout(c, cap)))) + " |");
            }
            sb.AppendLine();
            sb.AppendLine("## 읽는 법");
            sb.AppendLine();
            sb.AppendLine($"- 가(추천안)는 맨몸 30% 에서 빈도가 두 배다. 5장 글 '+50%'를 빈도로 읽으면 그보다 세다. 대신 쾌속을 조금만 모아도 상한 {N(cap, "0.##")}(빈도 x{N(RateWithout(cap, cap))})에 닿는다. 청명심법을 끝까지 올린 몸은 기혈 50% 에서 이미 상한이라 그 아래로는 더 오르지 않고, 혈광폭주까지 든 몸은 혈광폭주만으로 상한이라 30% 아래에서 마인의 피 몫이 x1.00 이다.");
            sb.AppendLine("- 나는 쾌속을 많이 모은 몸일수록 마인의 피가 그대로 1.5배를 더한다. 혈광폭주·태극까지 겹치면 상한 빈도에 1.5 를 더 곱해 가장 세다. 상한을 둔 뜻(9장 '최대 -60%')을 넘는다.");
            sb.AppendLine("- 다는 맨몸에서 글자 그대로 1.5배이고, 쾌속을 모으면 가처럼 상한에 막힌다. 가보다 약하고 상한에 늦게 닿는다.");
            sb.AppendLine("- 어느 안이든 마교 몸은 시작 음양이 -15 이고 혈공이 음으로 민다(5장). 태극 줄은 드물게 나오는 경우다.");
            sb.AppendLine();
            sb.AppendLine("고르는 기준: 마인의 피를 '쾌속 안 모아도 되는 몸'으로 보면 가 또는 다, '끝까지 세지는 몸'으로 보면 나. 결정이 나면 cheonha.json demonic `traitEffect` 와 5장 문장을 같이 고친다.");
            return sb.ToString();
        }
    }
}
