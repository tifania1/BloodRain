// 한 생을 60분까지(backlog GU-11h3, 천하 확장 설계안 '혈월의 밤 2막'). 보고서: docs/design/sim/cheonha60.md
//
// 2막 규칙은 아직 없다. 지금 규칙(chapters.json life 마지막 표 연장, spawnTarget 마지막 값, bosses.healthCurve 기울기 연장,
// 현경 뒤 봉인석이 모자라면 혈마의 그림자 5분마다 한 단계)을 그대로 60분까지 늘리면 어떻게 되는지만 본다. 값은 고치지 않는다.
//
// 가정(이 파일에서 새로 정함):
//   - 한 생은 CheonhaSim 과 같다(숭산 소림, 처음 혼, 평균 정책). 그림자에 맞는 피해는 넣지 않는다(shadow.md 가 따로 본다).
//   - 봉인석은 0 개로 본다(처음 혼). 그래서 현경이면 30분부터 그림자가 나온다.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BloodRain.Core.Spawning;
using BloodRain.Core.World;

namespace BloodRain.Sim
{
    public static class LongLifeSim
    {
        public const double Minutes = 60;

        public static string Report(SimData data, int seeds = 10)
        {
            var g = data.Game;
            var c = g.Cheonha;
            var scale = new Scale(g);
            var curve = new SpawnCurve(g.Balance.SpawnTarget);
            var sh = c.LifeEnd.Shadow;
            int topLevel = scale.Realms[scale.TopRealm].Level;

            var runs = Enumerable.Range(1, seeds).Select(seed =>
            {
                var soul = new BloodRain.Core.Meta.Soul(g);
                soul.EndLife(soul.BeginLife(BloodRain.Core.Meta.Rebirth.Prologue(g)).Finish(BloodRain.Core.Run.LifeEndWay.Fallen, 0));
                var sect = c.Sect("shaolin");
                var cand = new BloodRain.Core.Meta.RebirthCandidate(sect, c.Region(sect.Region), Array.Empty<BloodRain.Core.Data.BirthTraitDef>());
                return CheonhaSim.RunLife(data, soul, cand, seed, null, Minutes).result;
            }).ToList();

            int Med(IEnumerable<int> xs) { var a = xs.OrderBy(x => x).ToList(); return a.Count == 0 ? 0 : a[a.Count / 2]; }

            var sb = new StringBuilder();
            sb.AppendLine("# 한 생 60분 시뮬레이션(GU-11h3)");
            sb.AppendLine();
            sb.AppendLine("`dotnet run --project core/BloodRain.Sim -c Release -- --cheonha60 docs/design/sim/cheonha60.md` 로 다시 만든다. 코드는 `core/BloodRain.Sim/LongLifeSim.cs`.");
            sb.AppendLine();
            sb.AppendLine($"천하 확장 설계안이 한 생 상한을 60분으로 늘리고 혈월(30분) 뒤에 '혈월의 밤 2막'을 두기로 했다(2026-11-18 사람). 2막 규칙은 아직 없으니 **지금 규칙을 그대로 60분까지 늘리면** 무엇이 어떻게 가는지만 본다. 숭산 소림, 처음 혼, 정책 average, 시드 1~{seeds} 가운데값. 봉인석 0 이라 현경이면 30분부터 그림자. 그림자에 맞는 피해는 넣지 않았다. 값은 고치지 않았다.");
            sb.AppendLine();
            sb.AppendLine("| 분 | 레벨 | 화면 위 목표 적 | 땅의 주인 기혈 배율 | 그림자 단계 | 그림자 접촉 피해 | 그림자 걸음 |");
            sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");
            for (int m = 5; m <= (int)Minutes; m += 5)
            {
                int lv = Med(runs.Select(r => r.LevelByMinute.TryGetValue(m, out var l) ? l : r.FinalLevel));
                int step = BloodRain.Core.Spawning.LifeDirector.ShadowStep(c, scale, m, Math.Max(lv, 0), 0);
                string dmg = step == 0 ? "" : $"{sh.ContactDamage * Math.Pow(sh.DamageGrowth, step - 1):0}";
                string spd = step == 0 ? "" : $"{sh.MoveSpeed + sh.SpeedPerStep * (step - 1):0}";
                sb.AppendLine($"| {m} | {lv} | {curve.TargetAt(m):0} | {BossAwakening.HealthMultiplier(c.Bosses, m):0.00} | {(step == 0 ? "" : step.ToString())} | {dmg} | {spd} |");
            }
            sb.AppendLine();
            int kills = Med(runs.Select(r => r.Bosses.Count(b => !double.IsNaN(b.KilledAt))));
            int killsAfter30 = Med(runs.Select(r => r.Bosses.Count(b => !double.IsNaN(b.KilledAt) && b.KilledAt >= 30)));
            sb.AppendLine($"쓰러뜨린 땅의 주인(가운데): 60분까지 {kills}, 그중 30분 뒤 {killsAfter30}. 60분 레벨(가운데) {Med(runs.Select(r => r.FinalLevel))}.");
            sb.AppendLine();
            sb.AppendLine("## 읽을 것");
            sb.AppendLine();
            sb.AppendLine("- 30분 뒤로는 적 수(400)와 적 종류(life 마지막 표)가 그대로라 2막에 새로 나오는 것이 없다. 땅의 주인 기혈 배율만 30분 점(1.31) 뒤로 기울기대로 계속 오른다.");
            sb.AppendLine("- 레벨은 30분 뒤에도 5분에 10~14씩 계속 오른다(진기 흐름이 그대로라). 2막에서 무엇으로 성장을 보여 줄지(새 깨달음·병기 등)는 아직 없다.");
            sb.AppendLine("- 봉인석이 모자란 현경은 그림자가 5분마다 세져 60분에 7단계(접촉 피해 곱으로 커짐, 걸음은 플레이어 기본 100보다 빠르고 상한 없음)가 된다. 언제 붙잡히는지는 [shadow.md](shadow.md). 2막에서 그림자를 어떻게 할지는 아래 줄(GU-11h4)에서 정했다.");
            // 2026-11-20 정한 2막(GU-11h4): 숭산에 머문 생이면 제 땅 밖 문파의 위기는 모두 무너진다 — 그 땅 주인이 30분부터 10분마다 하나씩 모여든다
            var gl = c.LifeEnd.GatheringLords;
            int lords = g.Chronicle.Events.Count(e => e.Kind == BloodRain.Core.Run.Life.SectCrisisKind && e.Region != null && e.Region != "songshan"
                && c.Regions.Any(r => r.Id == e.Region && r.Boss != null));
            if (gl.EveryMinutes > 0)
            {
                var at = Enumerable.Range(0, lords).Select(i => gl.FromMinute + i * gl.EveryMinutes).Where(m => c.LifeEnd.EndMinute <= 0 || m < c.LifeEnd.EndMinute).ToList();
                sb.AppendLine($"- **2026-11-20 정함(GU-11h4)**: Unity 한 생(Core `Life`)은 위 그림자 대신 '모여드는 땅의 주인'이다 — {gl.FromMinute:0}분에 하나, {gl.EveryMinutes:0}분마다 하나씩, 그 생에 무너진 문파 땅 주인 수까지, {c.LifeEnd.EndMinute:0}분에 혈월이 지며 좌화와 같은 정산. 숭산에만 머문 이 시뮬레이션의 생이면 무너지는 땅 주인이 {lords}곳이라 {string.Join("·", at.Select(m => m.ToString("0")))}분에 하나씩 온다. 위 그림자 칸은 옛 규칙이고 웹 판이 아직 따른다.");
            }
            return sb.ToString();
        }
    }
}
