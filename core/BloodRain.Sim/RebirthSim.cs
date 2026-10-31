// 침방 환생 후보 추첨(밸런스 시뮬레이션). 보고서: docs/design/sim/rebirth.md
//
// Core Rebirth.Roll·First·RebirthDraw 를 그대로 굴린다. 판의 내용(경지, 윤회의 기)은 보지 않는다.
//   후보 한 장의 분포: 해금한 문파 수(3·6·9)마다 문파·성질 수·성질별·여자 몸 비율.
//   구파윤회(아홉 문파로 한 번씩 환생): 문파 아홉이 다 열린 뒤 시작해, 안 해 본 문파가 후보에 있으면 그중 맨 앞을 고르고
//   없으면 다시 고르기를 남은 만큼 쓴다. 다 써도 없으면 아무나 고른다(그 생은 헛걸음).
//   넷째 몸(후보 4), 다시 고르기 0~3 을 조합해 몇 번 환혼에 칭호가 나오는지 센다.
//   특정 문파 하나(예: 마교 몸)를 원할 때 한 번 환혼에 그 문파가 나올 확률도 같은 방식으로 센다.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;

namespace BloodRain.Sim
{
    public static class RebirthSim
    {
        /// <summary>후보를 n번 뽑아 센 것.</summary>
        public sealed class DrawStats
        {
            public int Draws;
            public int Cards;
            public Dictionary<string, int> Sect = new Dictionary<string, int>();
            public int[] TraitCount = new int[3];
            public Dictionary<string, int> Trait = new Dictionary<string, int>();
            public int Female;
            public int FemaleEligible;
            public int SameSectTwice;
            public int ExcludedPairs;
        }

        public static DrawStats Draws(CheonhaDef c, IReadOnlyList<string> unlocked, int count, int n, Random rng)
        {
            var s = new DrawStats { Draws = n };
            for (int i = 0; i < n; i++)
            {
                var cands = Rebirth.Roll(c, unlocked, count, rng);
                if (cands.Select(x => x.Sect.Id).Distinct().Count() < cands.Count) s.SameSectTwice++;
                foreach (var x in cands)
                {
                    s.Cards++;
                    Inc(s.Sect, x.Sect.Id);
                    s.TraitCount[Math.Min(2, x.Traits.Count)]++;
                    foreach (var t in x.Traits) Inc(s.Trait, t.Id);
                    for (int a = 0; a < x.Traits.Count; a++)
                        for (int b = a + 1; b < x.Traits.Count; b++)
                            if (Rebirth.Excluded(c.BirthTraits, x.Traits[a].Id, x.Traits[b].Id)) s.ExcludedPairs++;
                    if (!c.Rebirth.MaleOnlySects.Contains(x.Sect.Id))
                    {
                        s.FemaleEligible++;
                        if (x.Female) s.Female++;
                    }
                }
            }
            return s;
        }

        /// <summary>
        /// 아홉 문파가 다 열린 뒤 구파윤회까지 걸린 환혼 수. played 는 이미 해 본 문파(서장 몸은 문파가 아니라 넣지 않는다).
        /// maxLives 안에 못 채우면 -1.
        /// </summary>
        public static int LivesToAllSects(CheonhaDef c, int count, int rerolls, Random rng, ISet<string>? played = null, int maxLives = 500)
        {
            var all = c.Sects.Select(s => s.Id).ToList();
            var need = new HashSet<string>(all);
            if (played != null) need.ExceptWith(played);
            if (need.Count == 0) return 0;
            for (int life = 1; life <= maxLives; life++)
            {
                var draw = new RebirthDraw(c, all, count, rerolls, false, rng);
                int pick = IndexOfNeeded(draw.Candidates, need);
                while (pick < 0 && draw.RerollsLeft > 0)
                {
                    draw.Reroll();
                    pick = IndexOfNeeded(draw.Candidates, need);
                }
                var body = draw.Choose(Math.Max(0, pick));
                need.Remove(body.Sect!.Id);
                if (need.Count == 0) return life;
            }
            return -1;
        }

        /// <summary>한 번 환혼에서 원하는 문파 하나가 후보에 나올 확률(다시 고르기를 그 문파가 나올 때까지 쓴다).</summary>
        public static double ChanceOfSect(CheonhaDef c, IReadOnlyList<string> unlocked, string sect, int count, int rerolls, int n, Random rng)
        {
            int hit = 0;
            for (int i = 0; i < n; i++)
            {
                var draw = new RebirthDraw(c, unlocked, count, rerolls, false, rng);
                bool ok = draw.Candidates.Any(x => x.Sect.Id == sect);
                while (!ok && draw.RerollsLeft > 0)
                {
                    draw.Reroll();
                    ok = draw.Candidates.Any(x => x.Sect.Id == sect);
                }
                if (ok) hit++;
            }
            return (double)hit / n;
        }

        static int IndexOfNeeded(IReadOnlyList<RebirthCandidate> cands, HashSet<string> need)
        {
            for (int i = 0; i < cands.Count; i++)
                if (need.Contains(cands[i].Sect.Id)) return i;
            return -1;
        }

        static void Inc(Dictionary<string, int> d, string k) => d[k] = d.TryGetValue(k, out var v) ? v + 1 : 1;

        public static double Percentile(List<int> sorted, double p) =>
            sorted.Count == 0 ? 0 : sorted[Math.Min(sorted.Count - 1, (int)Math.Floor(p * (sorted.Count - 1) + 0.5))];

        public static string Report(SimData data, int draws = 20000, int souls = 5000)
        {
            var c = data.Game.Cheonha;
            var rng = new Random(1);
            var inv = CultureInfo.InvariantCulture;
            string Pct(double v) => (v * 100).ToString("0", inv) + "%";
            string Pct1(double v) => (v * 100).ToString("0.0", inv) + "%";
            var allIds = c.Sects.Select(s => s.Id).ToList();
            var startIds = c.Sects.Where(s => s.Unlock == "start").Select(s => s.Id).ToList();
            var sixIds = allIds.Take(6).ToList();
            int baseCount = c.Rebirth.Candidates;
            var reroll = c.Rebirth.Upgrades.First(u => u.Id == "reroll");

            var sb = new StringBuilder();
            sb.AppendLine("# 침방 환생 후보 추첨");
            sb.AppendLine();
            sb.AppendLine("`dotnet run --project core/BloodRain.Sim -c Release -- --rebirth docs/design/sim/rebirth.md` 로 다시 만든다(`RebirthSim.cs`).");
            sb.AppendLine();
            sb.AppendLine($"Core `Rebirth.Roll`·`RebirthDraw` 를 그대로 굴렸다(시드 1). 후보 분포는 {draws:N0}번 뽑기, 구파윤회는 혼 {souls:N0}개. 규칙은 cheonha.json `rebirth`·`birthTraits`(기획서 21장).");
            sb.AppendLine("판 내용(경지·윤회의 기)과 문파 해금 순서는 보지 않는다. 그건 [meta.md](meta.md) 몫이다.");
            sb.AppendLine();

            // 1. 후보 한 장
            sb.AppendLine("## 후보 한 장의 분포");
            sb.AppendLine();
            sb.AppendLine($"후보 {baseCount}장. '기획' 은 21장 문장에서 나오는 값.");
            sb.AppendLine();
            var sets = new (string name, List<string> ids)[] { ($"처음 {startIds.Count}문파", startIds), ("여섯 문파", sixIds), ("아홉 문파", allIds) };
            var stats = sets.Select(x => Draws(c, x.ids, baseCount, draws, rng)).ToList();
            var w = c.BirthTraits.CountWeights;
            double wt = w.Sum();
            sb.AppendLine("| | 기획 | " + string.Join(" | ", sets.Select(x => x.name)) + " |");
            sb.AppendLine("| --- | --- | " + string.Join(" | ", sets.Select(_ => "---")) + " |");
            for (int k = 0; k < 3; k++)
                sb.AppendLine($"| 성질 {k}개 | {Pct(w[k] / wt)} | " + string.Join(" | ", stats.Select(s => Pct1((double)s.TraitCount[k] / s.Cards))) + " |");
            sb.AppendLine($"| 여자 몸(소림 빼고) | {Pct(c.Rebirth.FemaleRatio)} | " + string.Join(" | ", stats.Select(s => Pct1((double)s.Female / Math.Max(1, s.FemaleEligible)))) + " |");
            sb.AppendLine($"| 여자 몸(모든 카드) | | " + string.Join(" | ", stats.Select(s => Pct1((double)s.Female / s.Cards))) + " |");
            sb.AppendLine($"| 같은 문파가 두 장 | 0% | " + string.Join(" | ", stats.Select(s => Pct1((double)s.SameSectTwice / s.Draws))) + " |");
            sb.AppendLine($"| 안 맞는 성질이 한 장에 | 0 | " + string.Join(" | ", stats.Select(s => s.ExcludedPairs.ToString(inv))) + " |");
            sb.AppendLine();

            var nine = stats[2];
            sb.AppendLine("### 아홉 문파일 때 문파별·성질별");
            sb.AppendLine();
            sb.AppendLine($"문파: 한 번 환혼에 그 문파가 후보에 나오는 비율. 기대값은 {baseCount}/{allIds.Count} = {Pct1((double)baseCount / allIds.Count)}.");
            sb.AppendLine();
            sb.AppendLine("| 문파 | 후보에 나옴 |");
            sb.AppendLine("| --- | --- |");
            foreach (var s in c.Sects)
                sb.AppendLine($"| {s.NameKo} | {Pct1((double)nine.Sect.GetValueOrDefault(s.Id) / nine.Draws)} |");
            sb.AppendLine();
            sb.AppendLine("성질: 카드 한 장에 그 성질이 붙는 비율. 음체·양체, 약골·강골은 서로 막혀 있어 둘째 성질로 덜 뽑힌다.");
            sb.AppendLine();
            sb.AppendLine("| 성질 | 카드에 붙음 |");
            sb.AppendLine("| --- | --- |");
            foreach (var t in c.BirthTraits.List)
                sb.AppendLine($"| {t.NameKo}({t.Hanja}) | {Pct1((double)nine.Trait.GetValueOrDefault(t.Id) / nine.Cards)} |");
            sb.AppendLine();

            // 2. 원하는 문파 하나
            sb.AppendLine("## 원하는 문파 하나가 나올 확률(아홉 문파)");
            sb.AppendLine();
            sb.AppendLine("한 번 환혼에, 원하는 문파(여기서는 마교)가 안 보이면 다시 고르기를 쓴다. 문파는 겹치지 않게 뽑으니 문파마다 같다.");
            sb.AppendLine();
            sb.AppendLine("| 다시 고르기 | 다시 고르기에 든 윤회의 기(누적) | 후보 3 | 후보 4 |");
            sb.AppendLine("| --- | --- | --- | --- |");
            for (int r = 0; r <= reroll.MaxLevel; r++)
            {
                int cost = 0;
                for (int lv = 1; lv <= r; lv++) cost += reroll.BaseCost * (reroll.CostPerLevel ? lv : 1);
                double p3 = ChanceOfSect(c, allIds, "demonic", baseCount, r, draws, rng);
                double p4 = ChanceOfSect(c, allIds, "demonic", baseCount + 1, r, draws, rng);
                sb.AppendLine($"| {r} | {cost:N0} | {Pct(p3)} | {Pct(p4)} |");
            }
            sb.AppendLine();

            // 3. 구파윤회
            sb.AppendLine("## 구파윤회(아홉 문파로 한 번씩 환생)");
            sb.AppendLine();
            sb.AppendLine("아홉 문파가 다 열린 뒤부터 센 환혼 수. 안 해 본 문파가 후보에 있으면 고르고, 없으면 다시 고르기를 쓴다. 가운데값 / 90번째 / 가장 많이.");
            sb.AppendLine("'처음 셋 해 봄' 은 소림·개방·남궁을 이미 해 본 채로 나머지 여섯이 열렸을 때(보통 순서).");
            sb.AppendLine();
            sb.AppendLine("| 다시 고르기 | 후보 3, 처음부터 | 후보 4, 처음부터 | 후보 3, 처음 셋 해 봄 | 후보 4, 처음 셋 해 봄 |");
            sb.AppendLine("| --- | --- | --- | --- | --- |");
            var startPlayed = new HashSet<string>(startIds);
            for (int r = 0; r <= reroll.MaxLevel; r++)
            {
                var cells = new List<string>();
                foreach (var (count, played) in new[] { (baseCount, (ISet<string>?)null), (baseCount + 1, null), (baseCount, startPlayed), (baseCount + 1, startPlayed) })
                {
                    var lives = new List<int>(souls);
                    for (int i = 0; i < souls; i++) lives.Add(LivesToAllSects(c, count, r, rng, played));
                    lives.Sort();
                    cells.Add($"{Percentile(lives, 0.5):0} / {Percentile(lives, 0.9):0} / {lives[lives.Count - 1]}");
                }
                sb.AppendLine($"| {r} | " + string.Join(" | ", cells) + " |");
            }
            sb.AppendLine();
            sb.AppendLine($"가장 적은 수는 처음부터면 {allIds.Count}생, 처음 셋 해 봄이면 {allIds.Count - startIds.Count}생이다. 다시 고르기가 없으면 마지막 한두 문파가 안 나와 헛걸음하는 생이 길게 늘어진다(쿠폰 모으기와 같은 꼴).");
            var fourth = c.Rebirth.Upgrades.First(u => u.Id == "fourth_body");
            sb.AppendLine($"윤회의 기로 견주면 넷째 몸은 {fourth.BaseCost:N0}, 다시 고르기 1단계는 {reroll.BaseCost:N0}이다. 위 두 표에서 다시 고르기 1단계(후보 3)와 넷째 몸(다시 고르기 0)을 견주어 보면 된다.");
            sb.AppendLine();
            return sb.ToString();
        }
    }
}
