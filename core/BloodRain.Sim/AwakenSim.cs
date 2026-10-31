// 캐릭터마다 절기가 언제 깨어나는지(밸런스 시뮬레이션). 보고서: docs/design/sim/awakenings.md
//
// level-curve.md 의 절기 시각은 진소운 한 사람 것이라, 캐릭터 여덟 명을 같은 판(RunSimulator, 숭산 무리 모형, 평균 정책 30분)으로 돌려 나란히 놓는다.
//   절기 문턱·그릇·각성은 Core Loadout 그대로(무공 Lv8 + 짝 심법 Lv3, 그릇이 늘어나는 돌파나 보갑에서 깨어남).
//   무공은 처음부터 열린 것만 나온다(RunSimulator 와 같음). 그래서 시작 무공이 처음엔 안 열린 무공(하연 쇄겸술, 운화 태극검, 백야 혈조)인 캐릭터도
//   자기 시작 무공은 들고 시작하고, 그 밖의 안 열린 무공은 레벨업에 안 나온다.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace BloodRain.Sim
{
    public sealed class AwakenRow
    {
        public string Character = "", NameKo = "", StartWeapon = "";
        public int Runs;
        /// <summary>n번째 절기(0부터) → 깨어난 분 가운데값(닿은 판만)과 닿은 판 수.</summary>
        public double[] Minute = new double[4];
        public int[] Reached = new int[4];
        /// <summary>30분에 가진 절기 수 가운데값.</summary>
        public double Count30;
        /// <summary>첫 절기가 시작 무공인 판 수.</summary>
        public int FirstIsStart;
        /// <summary>깨어난 절기 가운데 보갑으로 깨어난 몫.</summary>
        public double ChestShare;
    }

    public static class AwakenSim
    {
        public const double Minutes = 30;

        public static AwakenRow Measure(SimData data, string character, int seeds, double minutes = Minutes)
        {
            var ch = data.Game.Character(character);
            var runs = Enumerable.Range(1, seeds)
                .Select(s => RunSimulator.Run(data, new SimOptions { Character = character, Seed = s, Minutes = minutes }))
                .ToList();
            var row = new AwakenRow { Character = character, NameKo = ch.NameKo, StartWeapon = ch.StartWeapon, Runs = seeds };
            for (int i = 0; i < 4; i++)
            {
                var xs = runs.Where(r => r.Awakenings.Count > i).Select(r => r.Awakenings[i].Minute).ToList();
                row.Reached[i] = xs.Count;
                row.Minute[i] = Median(xs);
            }
            row.Count30 = Median(runs.Select(r => (double)r.Awakenings.Count));
            row.FirstIsStart = runs.Count(r => r.Awakenings.Count > 0 && r.Awakenings[0].WeaponId == ch.StartWeapon);
            int all = runs.Sum(r => r.Awakenings.Count);
            row.ChestShare = all == 0 ? double.NaN : (double)runs.Sum(r => r.Awakenings.Count(a => a.Trigger == "보갑")) / all;
            return row;
        }

        static double Median(IEnumerable<double> xs)
        {
            var a = xs.OrderBy(x => x).ToList();
            if (a.Count == 0) return double.NaN;
            return a.Count % 2 == 1 ? a[a.Count / 2] : (a[a.Count / 2 - 1] + a[a.Count / 2]) / 2;
        }

        static string M(double x) => double.IsNaN(x) ? "-" : x.ToString("0.0", CultureInfo.InvariantCulture);

        static string Cell(AwakenRow r, int i) =>
            r.Reached[i] == 0 ? "-" : r.Reached[i] == r.Runs ? M(r.Minute[i]) : $"{M(r.Minute[i])} ({r.Reached[i]}/{r.Runs})";

        public static string Report(SimData data, int seeds = 10)
        {
            var g = data.Game;
            var rows = g.Characters.Select(c => Measure(data, c.Id, seeds)).ToList();
            var sb = new StringBuilder();
            sb.AppendLine("# 캐릭터별 절기 각성 시각");
            sb.AppendLine();
            sb.AppendLine("`dotnet run --project core/BloodRain.Sim -c Release -- --awakenings docs/design/sim/awakenings.md` 로 다시 쓴다. 코드 `core/BloodRain.Sim/AwakenSim.cs`.");
            sb.AppendLine($"RunSimulator(숭산 무리 모형, 평균 정책) 30분 × 캐릭터 {g.Characters.Count}명 × 시드 {seeds}. 절기 문턱·그릇·각성은 Core `Loadout` 그대로. level-curve.md 는 진소운 한 사람이라 여덟 명을 나란히 놓았다.");
            sb.AppendLine("레벨업에는 처음부터 열린 무공만 나온다(시작 무공은 열렸든 아니든 들고 시작한다).");
            sb.AppendLine();
            sb.AppendLine("## 각성 시각(분, 가운데값)");
            sb.AppendLine();
            sb.AppendLine("목표는 기획서 8장: 첫 절기는 절정 무렵(약 11.7분)이나 15분 보갑, 둘째는 초절정 돌파(약 16.7분). 일부 판만 닿았으면 (닿은 판/전체).");
            sb.AppendLine();
            sb.AppendLine("| 캐릭터 | 시작 무공 | 첫 절기 | 둘째 | 셋째 | 넷째 | 30분 절기 수 | 첫 절기가 시작 무공 | 보갑으로 깬 몫 |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
            foreach (var r in rows)
                sb.AppendLine($"| {r.NameKo} | {g.Weapon(r.StartWeapon).NameKo} | {Cell(r, 0)} | {Cell(r, 1)} | {Cell(r, 2)} | {Cell(r, 3)} | {M(r.Count30)} | {r.FirstIsStart}/{r.Runs} | {(double.IsNaN(r.ChestShare) ? "-" : (r.ChestShare * 100).ToString("0", CultureInfo.InvariantCulture) + "%")} |");
            sb.AppendLine();

            var first = rows.Where(r => r.Reached[0] > 0).OrderBy(r => r.Minute[0]).ToList();
            sb.AppendLine("## 읽는 법");
            sb.AppendLine();
            if (first.Count > 0)
                sb.AppendLine($"- 첫 절기는 {Subject(first[0].NameKo)} 가장 이르고({M(first[0].Minute[0])}분) {Subject(first[first.Count - 1].NameKo)} 가장 늦다({M(first[first.Count - 1].Minute[0])}분). 목표(11.7분)보다 3분 넘게 늦은 캐릭터: {Join(rows.Where(r => r.Reached[0] < r.Runs || r.Minute[0] > 11.7 + 3))}.");
            sb.AppendLine($"- 30분에 절기를 넷 다 채운 캐릭터: {Join(rows.Where(r => r.Count30 >= 4))}. 넷째 그릇은 현경(레벨 90)에서 열리니, 숭산 무리 모형에서 30분 안에 현경에 닿는지가 먼저다.");
            sb.AppendLine($"- 첫 절기가 시작 무공인 판은 {rows.Sum(r => r.FirstIsStart)}/{rows.Sum(r => r.Runs)}뿐이다. 평균 정책은 시작 무공을 따로 아끼지 않고 무공 넷을 먼저 채운 뒤 보이는 강화를 고르기 때문이다(한 판 표를 찍어 보면 10분에 시작 무공이 Lv2~3 인 판이 흔하다). 사람은 시작 무공을 먼저 키울 때가 많아서 실제로는 이보다 자주 시작 무공이 먼저 깨어날 것이다. 정책 하나로 본 것이니 결론으로 쓰지 않는다.");
            sb.AppendLine("- 빠진 것: 처음엔 안 열린 무공(태극검·쇄겸술·혈조·음공·이화창·뇌부·낙화검우)은 레벨업에 안 나온다. 도감을 채운 뒤에는 고를 무공이 늘어 짝 맞추기가 조금 늦어질 수 있다.");
            return sb.ToString();
        }

        /// <summary>이름 끝 글자에 받침이 있으면 '이', 없으면 '가'.</summary>
        public static string Subject(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            char c = name[name.Length - 1];
            bool batchim = c >= 0xAC00 && c <= 0xD7A3 && (c - 0xAC00) % 28 != 0;
            return name + (batchim ? "이" : "가");
        }

        static string Join(IEnumerable<AwakenRow> rows)
        {
            var names = rows.Select(r => r.NameKo).ToList();
            return names.Count == 0 ? "없음" : string.Join("·", names);
        }
    }
}
