#nullable enable
// 데이터를 읽은 뒤 한 번 훑어, 0 나누기·무한 반복·순서 뒤집힘으로 게임이 멈추거나 NaN 이 화면까지 가는 값을 찾는다(QA-03).
// 던지지 않고 문제 목록만 돌려준다. Unity DataBoot 는 에러 로그로, 테스트는 빈 목록인지 본다.
using System.Collections.Generic;

namespace BloodRain.Core.Data
{
    public static class DataCheck
    {
        /// <summary>경험치 곡선을 이 레벨까지 확인한다. 한 판에 오르는 레벨보다 넉넉하게.</summary>
        public const int XpCheckToLevel = 300;

        public static List<string> Problems(GameData d)
        {
            var p = new List<string>();
            var b = d.Balance;

            // 0 이면 나눗셈이 Infinity 가 된다.
            if (!(b.LevelUp.LuckForFourthChoice > 0)) p.Add($"balance.levelUp.luckForFourthChoice = {b.LevelUp.LuckForFourthChoice} (0보다 커야 함)");
            if (!(b.UltimateCharge.Max > 0)) p.Add($"balance.ultimateCharge.max = {b.UltimateCharge.Max} (0보다 커야 함)");
            foreach (var i in d.Insights) CheckKills(p, "insights", i);
            foreach (var i in d.DemonicInsights) CheckKills(p, "insights.demonicInsights", i);
            foreach (var w in d.Weapons)
                if (w.ShotsPerSecond != null)
                    for (int k = 0; k < w.ShotsPerSecond.Count; k++)
                        if (!(w.ShotsPerSecond[k] > 0)) p.Add($"weapons.{w.Id}.shotsPerSecond[{k}] = {w.ShotsPerSecond[k]} (0보다 커야 함)");

            // 경험치 곡선: 구간이 있어야 하고, 필요 진기가 0 이하면 LevelFor 가 끝나지 않는다.
            var segs = b.XpCurve.Segments;
            if (segs.Count == 0) p.Add("balance.xpCurve.segments 가 비었다");
            else
            {
                for (int k = 0; k < segs.Count; k++)
                    if (segs[k].FromLevel > segs[k].ToLevel) p.Add($"balance.xpCurve.segments[{k}] fromLevel {segs[k].FromLevel} > toLevel {segs[k].ToLevel}");
                var curve = new Progression.XpCurve(b.XpCurve);
                for (int lv = 1; lv <= XpCheckToLevel; lv++)
                    if (curve.Required(lv) <= 0) { p.Add($"balance.xpCurve 레벨 {lv} 필요 진기 {curve.Required(lv)} (0보다 커야 함)"); break; }
            }

            // [분, 값] 표: 점은 두 칸이고 분은 앞으로만 간다(사이를 곧게 잇는 식이 뒤집히거나 0 으로 나뉜다).
            CheckCurve(p, "balance.spawnTarget.points", b.SpawnTarget.Points, minPoints: 2);
            CheckCurve(p, "cheonha.bosses.healthCurve", d.Cheonha.Bosses.HealthCurve, minPoints: 1);

            // 경지 레벨은 앞으로만 간다(Scale.ProgressInRealm 이 두 경지 레벨 차로 나눈다).
            for (int k = 1; k < b.Realms.Count; k++)
                if (b.Realms[k].Level <= b.Realms[k - 1].Level)
                    p.Add($"balance.realms[{k}] {b.Realms[k].Id} level {b.Realms[k].Level} ≤ 앞 경지 {b.Realms[k - 1].Level}");

            // 깨지는 기믹이 은자를 떨구면 최소 ≤ 최대.
            CheckSilver(p, "gimmicks.wineJars", d.Gimmicks.WineJars);
            CheckSilver(p, "gimmicks.swordRacks", d.Gimmicks.SwordRacks);

            // 글로 적은 규칙(QA-05): 코드가 아는 값이 하나뿐이라 데이터만 바꾸면 조용히 무시되던 것.
            var c = d.Cheonha;
            CheckKnown(p, "cheonha.cluster.xpPerCluster", c.Cluster.XpPerCluster, "one");          // 덩이 하나에 적 하나 값
            CheckKnown(p, "cheonha.cluster.killCount", c.Cluster.KillCount, "members");             // Scale.KillCount
            CheckKnown(p, "cheonha.cluster.drawMembers", c.Cluster.DrawMembers, "ceil_by_health");  // Scale.DrawMembers
            CheckKnown(p, "cheonha.density.blend", c.Density.Blend, "nearest_two");                // Regions.Density
            foreach (var r in c.Cycle.Resets)
                if (r != "sealStones") p.Add($"cheonha.cycle.resets 에 회귀가 지울 줄 모르는 항목 {r} (Soul.Regress 는 sealStones 만)");
            foreach (var k in c.Cycle.Keeps)
                if (c.Cycle.Resets.Contains(k)) p.Add($"cheonha.cycle {k} 가 keeps 와 resets 에 함께 있다");
            foreach (var i in d.Insights)
                if (i.Effect == "periodic_block" && i.Params.TryGetValue("blocks", out var n) && n != 1f)
                    p.Add($"insights.{i.Id}.blocks = {n} (InsightEffects.TryBlock 은 간격마다 한 번만 막는다, 1 이어야 함)");
            return p;
        }

        static void CheckKills(List<string> p, string file, InsightDef i)
        {
            if (i.Params != null && i.Params.TryGetValue("killsPerStack", out var v) && !(v > 0))
                p.Add($"{file}.{i.Id}.killsPerStack = {v} (0보다 커야 함)");
        }

        static void CheckCurve(List<string> p, string name, List<float[]> pts, int minPoints)
        {
            if (pts.Count < minPoints) { p.Add($"{name} 점이 {pts.Count}개 (적어도 {minPoints}개)"); return; }
            for (int k = 0; k < pts.Count; k++)
            {
                if (pts[k] == null || pts[k].Length != 2) { p.Add($"{name}[{k}] 는 [분, 값] 두 칸이어야 함"); return; }
                if (k > 0 && !(pts[k][0] > pts[k - 1][0])) p.Add($"{name}[{k}] 분 {pts[k][0]} ≤ 앞 점 {pts[k - 1][0]} (분이 겹치거나 거꾸로)");
            }
        }

        static void CheckKnown(List<string> p, string name, string value, string known)
        {
            if (value != known) p.Add($"{name} = \"{value}\" (코드가 아는 값은 \"{known}\" 하나)");
        }

        static void CheckSilver(List<string> p, string name, BreakableGimmickDef g)
        {
            if (g.Drop == "silver" && g.SilverMax < g.SilverMin) p.Add($"{name} silverMax {g.SilverMax} < silverMin {g.SilverMin}");
        }
    }
}
