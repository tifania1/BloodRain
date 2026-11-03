// 혈영 분신이 플레이어 무공을 흉내 낼 때 얼마나 아픈가(밸런스 시뮬레이션). 보고서: docs/design/sim/mimic.md
//
// 값은 Core Mimicry.Weapons 그대로(무공 기본 피해 × 배율, 쿨다운 × 배율, 플레이어 심법·문파 보너스 없음).
// 한 번 발동에 몸에 몇 대 들어가나는 WeaponModel.Strikes 의 SingleHits(보스 한 마리를 칠 때와 같은 셈)를 빌린다.
// 곧 '분신이 플레이어 쪽을 보고 서 있고, 플레이어가 피하지 않으면'의 위쪽 값이다. 대마다 Core Damage.Taken(호신)을 뺀다.
// 혈영회주 자신의 패턴(BossPatterns.ScaleDamage 로 분·농도에 따라 커짐)과 나란히 놓아 분신이 패턴 하나 몫인지 본다.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BloodRain.Core.Build;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using BloodRain.Core.Progression;

namespace BloodRain.Sim
{
    public static class MimicSim
    {
        public const string BossId = "hyeokryeon_hwi";
        public const string RegionId = "sipmandaesan";
        public const string ClonePattern = "mirror_clone";
        public const string MobId = "blood_shadow_clone";
        /// <summary>혈영회주와 싸우는 분 두 경우. [가정] 천하 30분 안 늦은 쪽(cheonha.md 의 마지막 땅의 주인 시각 근처).</summary>
        public static readonly float[] FightMinutes = { 20, 30 };

        /// <summary>분신 무공 하나가 초당 몸에 넣는 피해(호신 뺀 뒤). 상시 무공(쿨다운 0)은 닿는 간격(tickSeconds)에 쿨 배율을 곱한다. [가정]</summary>
        public static double Dps(SimData data, WeaponDef def, int level, EnemyMimic mimic, float armor)
        {
            var g = data.Game;
            int max = g.Balance.Levels.WeaponMax;
            var b = NeutralBuild(g);
            var ex = data.Extras.TryGetValue(def.Id, out var x) ? x : null;
            var strikes = WeaponModel.Strikes(def, ex, level, false, false, b, max);
            // Strikes 는 플레이어 공력을 곱한 값이라 분신 값(기본 피해 × 배율)으로 되돌린다.
            double hit = WeaponModel.HitDamage(def, level, false, false, b, max);
            double mimicHit = WeaponMath.DamageAt(def, level, WeaponGrowth.MaxLevel) * mimic.DamageScale;
            double r = hit > 0 ? mimicHit / hit : 0;
            double sum = 0;
            float min = g.Balance.Contact.MinDamage;
            foreach (var st in strikes)
            {
                if (st.SingleHits <= 0 && st.SingleDot <= 0) continue;
                sum += Damage.Taken((float)(st.Damage * r), armor, min) * st.SingleHits + st.SingleDot * r;
            }
            double cd = WeaponGrowth.Cooldown(def, level);
            if (cd <= 0) cd = WeaponModel.Interval(def, ex, level, false, 0, max);
            cd *= mimic.CooldownScale;
            return sum / cd;
        }

        /// <summary>공력 1·치명 0·태극(음양 보너스 없음). Strikes 의 범위·발 수만 쓰려고 만든다.</summary>
        static BuildStats NeutralBuild(GameData g)
        {
            var sheet = Stats.Compute(g, g.Character("jin_sowoon"), new Dictionary<string, int>(), new Dictionary<string, int>());
            return new BuildStats { Sheet = sheet, Cdr = 0, Band = YinYangBand.Harmony, Leaning = YinYang.Neutral, Crit = 0 };
        }

        /// <summary>시작 무공이 될 수 있는 무공 id(대표 인물 + 천하 문파), 겹침 없이 데이터 순서대로.</summary>
        public static IReadOnlyList<string> StartWeapons(GameData g) =>
            g.Characters.Select(c => c.StartWeapon).Concat(g.Cheonha.Sects.Select(s => s.StartWeapon))
                .Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();

        public static EnemyMimic BossMimic(GameData g) =>
            g.BossPatternSet(BossId).Patterns.First(p => p.Id == ClonePattern).Mimic!;

        public static EnemyMimic MobMimic(GameData g) => g.Enemies.First(e => e.Id == MobId).Mimic!;

        public static double LeaveSeconds(GameData g) =>
            g.BossPatternSet(BossId).Patterns.First(p => p.Id == ClonePattern).LeaveSeconds;

        /// <summary>혈영회주 1막 패턴 한 바퀴(분신 빼고)를 다 맞을 때의 피해와 그 바퀴 길이(초). 호신 뺀 뒤.</summary>
        public static (double Damage, double Seconds) BossCycle(GameData g, float minute, float armor)
        {
            var set = g.BossPatternSet(BossId);
            float density = g.Cheonha.Region(RegionId).Density;
            double dmg = 0;
            foreach (var p in set.Patterns.Where(p => p.BaseDamage > 0))
            {
                float scaled = BossPatterns.ScaleDamage(p.BaseDamage, minute, density, g.Balance.EnemyScaling.DamagePerMinute);
                dmg += Damage.Taken(scaled, armor, g.Balance.Contact.MinDamage);
            }
            return (dmg, set.IntervalSeconds * set.Patterns.Count);
        }

        public static string Report(SimData data)
        {
            var g = data.Game;
            int max = g.Balance.Levels.WeaponMax;
            int slots = g.Balance.Slots.Weapons;
            var boss = BossMimic(g);
            var mob = MobMimic(g);
            var armors = ContactSim.ArmorCases(g);
            float aMax = armors[armors.Count - 1].Armor;
            double leave = LeaveSeconds(g);
            string F1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
            string F0(double v) => v.ToString("0", CultureInfo.InvariantCulture);

            var sb = new StringBuilder();
            sb.AppendLine("# 혈영 분신이 흉내 낸 무공의 피해");
            sb.AppendLine();
            sb.AppendLine("`dotnet run --project core/BloodRain.Sim -c Release -- --mimic docs/design/sim/mimic.md` 로 다시 만든다(`MimicSim.cs`).");
            sb.AppendLine();
            sb.AppendLine("혈영 분신은 플레이어 무공을 같은 레벨로 흉내 낸다(Core `Mimicry`, 기획서 6·10장). 피해는 무공 기본 피해 × 배율이고 플레이어 심법·문파 보너스는 안 받는다. "
                + $"잡몹 분신(enemies.json)은 시작 무공 하나를 피해 {mob.DamageScale}배·쿨 {mob.CooldownScale}배로, 혈영회주가 부르는 분신(bosspatterns.json `mirror_clone`)은 가진 무공 모두를 피해 {boss.DamageScale}배·쿨 {boss.CooldownScale}배로 {F0(leave)}초 동안 쓴다.");
            sb.AppendLine();
            sb.AppendLine("## 가정");
            sb.AppendLine();
            sb.AppendLine("- 한 번 발동에 몸에 들어가는 대 수는 시뮬레이터 `WeaponModel` 이 보스 한 마리를 칠 때 쓰는 값(SingleHits)을 그대로 빌렸다. 분신이 플레이어를 보고 서 있고 플레이어가 하나도 안 피하는 경우라 **위쪽 값**이다.");
            sb.AppendLine("- 대마다 호신을 뺀다(`Damage.Taken`, 최소 피해 " + F0(g.Balance.Contact.MinDamage) + "). 지속 피해(화상·중독)는 호신을 안 뺐다.");
            sb.AppendLine("- 독무처럼 쿨다운 0(상시)인 무공은 닿는 간격(tickSeconds)에 쿨 배율을 곱했다. Core `Mimicry` 도 같은 값을 `Interval`(상시 무공이면 `Aura`)로 준다.");
            sb.AppendLine("- 절기로 각성한 무공도 각성 전 바탕 무공으로 셌다(Mimicry 규칙). 분신 피해는 분·혈운 농도로 커지지 않는다(Mimicry 에 그런 배율이 없다). 보스 패턴은 `BossPatterns.ScaleDamage` 로 커진다.");
            sb.AppendLine($"- 호신 세 경우는 contact.md 와 같다: {string.Join(", ", armors.Select(a => $"{a.Ko} {F0(a.Armor)}"))}.");
            sb.AppendLine();

            // 1. 무공별
            sb.AppendLine($"## 무공 하나, Lv{max}: 분신이 초당 넣는 피해");
            sb.AppendLine();
            sb.AppendLine($"칸은 호신 {string.Join(" / ", armors.Select(a => a.Ko))}. 잡몹 분신은 그 무공이 시작 무공일 때만 나온다.");
            sb.AppendLine();
            sb.AppendLine("| 무공 | 동작 | 보스 분신 | 잡몹 분신 |");
            sb.AppendLine("| --- | --- | --- | --- |");
            var rows = new List<(WeaponDef Def, double BossDps)>();
            foreach (var def in g.Weapons)
            {
                var bc = armors.Select(a => Dps(data, def, max, boss, a.Armor)).ToList();
                var mc = armors.Select(a => Dps(data, def, max, mob, a.Armor)).ToList();
                rows.Add((def, bc[0]));
                sb.AppendLine($"| {def.NameKo} | {def.Behavior} | {string.Join(" / ", bc.Select(F1))} | {string.Join(" / ", mc.Select(F1))} |");
            }
            sb.AppendLine();

            // 2. 보스 분신이 머무는 동안
            var sorted = rows.OrderByDescending(r => r.BossDps).ToList();
            var strong = sorted.Take(slots).Select(r => r.Def).ToList();
            var weak = sorted.Skip(Math.Max(0, sorted.Count - slots)).Select(r => r.Def).ToList();
            double Sum(IEnumerable<WeaponDef> ws, float armor) => ws.Sum(d => Dps(data, d, max, boss, armor));
            sb.AppendLine($"## 혈영회주의 분신이 {F0(leave)}초 머무는 동안(무공 {slots}칸 모두 Lv{max})");
            sb.AppendLine();
            sb.AppendLine("가장 센 조합은 위 표 보스 분신 맨몸 값이 큰 순서로 칸을 채운 것, 가장 약한 조합은 작은 순서. 실제 빌드는 둘 사이다. 칸은 '초당 · 머무는 동안 합'.");
            sb.AppendLine();
            sb.AppendLine("| 조합 | " + string.Join(" | ", armors.Select(a => $"호신 {a.Ko}")) + " |");
            sb.AppendLine("| --- | " + string.Join(" | ", armors.Select(_ => "---")) + " |");
            foreach (var (name, ws) in new[] { ("가장 셈", strong), ("가장 약함", weak) })
            {
                var cells = armors.Select(a => { double d = Sum(ws, a.Armor); return $"{F1(d)} · {F0(d * leave)}"; });
                sb.AppendLine($"| {name}({string.Join("·", ws.Select(w => w.NameKo))}) | " + string.Join(" | ", cells) + " |");
            }
            sb.AppendLine();

            // 3. 보스 자신과 견주기
            sb.AppendLine("## 혈영회주 자신의 패턴과 견주기");
            sb.AppendLine();
            var set = g.BossPatternSet(BossId);
            float density = g.Cheonha.Region(RegionId).Density;
            sb.AppendLine($"1막 패턴 {set.Patterns.Count}개가 {F1(set.IntervalSeconds)}초 간격으로 돈다. 분신을 뺀 패턴(피해 있는 것)을 한 바퀴 다 맞을 때의 피해를 바퀴 길이로 나눈 초당 값과, 분신 {F0(leave)}초 합을 나란히 놓았다. 십만대산 혈운 농도 {F1(density)}.");
            sb.AppendLine();
            sb.AppendLine("| 싸움 분 | 호신 | 패턴 한 바퀴(초당) | 분신 가장 셈(초당 · 합) | 분신 가장 약함(초당 · 합) |");
            sb.AppendLine("| --- | --- | --- | --- | --- |");
            foreach (var m in FightMinutes)
                foreach (var a in new[] { armors[0], armors[armors.Count - 1] })
                {
                    var (cyc, secs) = BossCycle(g, m, a.Armor);
                    double s = Sum(strong, a.Armor), w = Sum(weak, a.Armor);
                    sb.AppendLine($"| {m}분 | {a.Ko} | {F0(cyc)} ({F1(cyc / secs)}) | {F1(s)} · {F0(s * leave)} | {F1(w)} · {F0(w * leave)} |");
                }
            sb.AppendLine();

            // 4. 몸이 버티는 초와 맞추려면
            double hpLow = 100 + ContactSim.IryuHealthFlat(g);
            double hpHigh = 120 * (1 + ContactSim.MaxHealthBuild(g)) + ContactSim.IryuHealthFlat(g);
            double sMax = Sum(strong, 0), wMax = Sum(weak, 0), sArm = Sum(strong, aMax);
            var (c30, sec30) = BossCycle(g, 30, 0);
            double bossDps = c30 / sec30;
            sb.AppendLine("## 몸이 버티는 초");
            sb.AppendLine();
            sb.AppendLine($"기혈 {F0(hpLow)}(몸 100 + 이류 덧셈)과 {F0(hpHigh)}(몸 120 혜공 + 기혈 빌드 + 이류)으로, 분신 공격을 다 맞을 때 쓰러지기까지.");
            sb.AppendLine();
            sb.AppendLine("| 조합 | 맨몸 · 기혈 " + F0(hpLow) + " | 최대 호신 · 기혈 " + F0(hpHigh) + " |");
            sb.AppendLine("| --- | --- | --- |");
            sb.AppendLine($"| 가장 셈 | {F1(hpLow / sMax)}초 | {F1(hpHigh / sArm)}초 |");
            sb.AppendLine($"| 가장 약함 | {F1(hpLow / wMax)}초 | {F1(hpHigh / Sum(weak, aMax))}초 |");
            sb.AppendLine();

            // 배율을 어디까지 내리면 분신 가장 센 조합(맨몸)이 30분 보스 패턴 초당의 k배가 되나.
            sb.AppendLine("## 피해 배율을 바꾸면");
            sb.AppendLine();
            sb.AppendLine($"보스 분신 `damageScale` 을 바꿨을 때 가장 센 조합(맨몸)의 초당 피해와 30분 보스 패턴 초당({F1(bossDps)})의 몇 배인지. 지금 값은 {boss.DamageScale}.");
            sb.AppendLine();
            sb.AppendLine("| damageScale | 가장 셈 초당 | 패턴의 몇 배 | 기혈 " + F0(hpLow) + " 버티는 초 |");
            sb.AppendLine("| --- | --- | --- | --- |");
            foreach (var k in new[] { boss.DamageScale, 0.2f, 0.1f, 0.05f, 0.03f })
            {
                var m = new EnemyMimic { DamageScale = k, CooldownScale = boss.CooldownScale, AllWeapons = true };
                double d = strong.Sum(w => Dps(data, w, max, m, 0));
                sb.AppendLine($"| {k.ToString("0.##", CultureInfo.InvariantCulture)} | {F1(d)} | {F1(d / bossDps)} | {F1(hpLow / d)} |");
            }
            sb.AppendLine();

            // 잡몹 분신: 시작 무공 하나를 흉내 낸다. 분신 몸이 닿는 접촉 피해(같은 지역·분)와 견준다.
            var starts = StartWeapons(g).Select(id => g.Weapon(id)).ToList();
            var topStart = starts.OrderByDescending(d => Dps(data, d, max, mob, 0)).First();
            var clone = g.Enemies.First(e => e.Id == MobId);
            float mobMinute = FightMinutes[0];
            double touchDps = ContactSim.PerHit(g, clone.ContactDamage, mobMinute, density, 0, 0) / g.Balance.Contact.SameEnemyIntervalSeconds;
            sb.AppendLine("## 잡몹 분신 배율을 바꾸면");
            sb.AppendLine();
            sb.AppendLine($"잡몹 분신은 시작 무공 하나만 쓴다. 시작 무공이 될 수 있는 {starts.Count}개(characters.json·cheonha.json 문파) 가운데 맨몸 초당 피해가 가장 큰 {topStart.NameKo}(Lv{max})를 놓고, "
                + $"분신 몸 하나가 붙어서 치는 접촉 피해(기준 {F0(clone.ContactDamage)}, {mobMinute}분 십만대산, {g.Balance.Contact.SameEnemyIntervalSeconds}초에 한 번, 맨몸)와 견줬다. 지금 값은 {mob.DamageScale}·쿨 {mob.CooldownScale}배.");
            sb.AppendLine();
            sb.AppendLine("| damageScale | 무공 초당 | 접촉 초당 | 접촉의 몇 배 | 기혈 " + F0(hpLow) + " 버티는 초(무공만) |");
            sb.AppendLine("| --- | --- | --- | --- | --- |");
            foreach (var k in new[] { mob.DamageScale, 0.2f, 0.1f, 0.05f })
            {
                var m = new EnemyMimic { DamageScale = k, CooldownScale = mob.CooldownScale };
                double d = Dps(data, topStart, max, m, 0);
                sb.AppendLine($"| {k.ToString("0.##", CultureInfo.InvariantCulture)} | {F1(d)} | {F1(touchDps)} | {(d / touchDps).ToString("0.00", CultureInfo.InvariantCulture)} | {F1(hpLow / d)} |");
            }
            sb.AppendLine();

            sb.AppendLine("## 읽는 법");
            sb.AppendLine();
            sb.AppendLine($"- 지금 값이면 분신이 너무 아프다. 맨몸 가장 센 조합이 초당 {F1(sMax)}, 가장 약한 조합도 {F1(wMax)}로 30분 보스 패턴 한 바퀴 초당 {F1(bossDps)}의 {F1(sMax / bossDps)}배 · {F1(wMax / bossDps)}배다. 다 맞는다는 위쪽 값이지만 열에 하나만 맞아도 패턴보다 세다.");
            sb.AppendLine("- 까닭: 플레이어 무공 피해는 무리와 보스 체력(수백~수천)을 깎도록 맞춰져 있고 몸 기혈은 100 안팎이다. 같은 숫자를 절반만 줄여 몸에 넣으면 단위가 맞지 않는다.");
            sb.AppendLine($"- 호신 최대({F0(aMax)})에서 작은 타를 여러 번 넣는 무공(나한권·태극검·섬광지 등)은 대마다 최소 피해까지 깎여 크게 약해진다. 한 방이 큰 무공은 덜 깎인다. '빌드의 약점을 그대로 갖는다'는 취지와는 맞는 방향이다.");
            {
                var cur = Dps(data, topStart, max, mob, 0);
                sb.AppendLine($"- 잡몹 분신은 사정이 다르다. 지금 값({mob.DamageScale}·쿨 {mob.CooldownScale}배)이면 가장 센 시작 무공({topStart.NameKo})으로도 초당 {F1(cur)}에 그쳐 분신 몸이 붙어서 치는 접촉 초당 {F1(touchDps)}의 {(cur / touchDps).ToString("0.00", CultureInfo.InvariantCulture)}배다. 접촉보다 훨씬 약하니 무공 쪽 배율을 더 내릴 까닭은 적고, 멀리서도 맞는다는 점만 남는다.");
            }
            sb.AppendLine("- 분신은 분·농도로 커지지 않아 늦게 싸울수록 보스 패턴보다 상대적으로 약해진다. 대신 플레이어 무공 레벨을 따라가니 빌드가 클수록 세다.");
            sb.AppendLine();
            return sb.ToString();
        }
    }
}
