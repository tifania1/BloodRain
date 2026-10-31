// 맞는 쪽 셈(밸런스 시뮬레이션). 보고서: docs/design/sim/contact.md
//
// 다른 시뮬레이터는 모두 '플레이어는 죽지 않는다'로 돈다(Assumptions 맨 아래). 여기서는 반대로 맞는 쪽만 본다.
// 움직임은 모형에 넣지 않는다. 대신 '한 번 맞으면 얼마', '둘러싸이면 몇 초', '회복이 몇 마리를 따라잡나'를 표로 낸다.
//   한 번 피해: Core EnemyScaling.DamageAt(기준 피해, 분, 혈운 농도) → Damage.Taken(호신, 최소 피해) → 화경이면 받는 피해 -10%.
//   닿는 적: chapters.json 한 생(life) 흐름의 그 분 가중치표 + 지역 고유 적(localShare). 접촉 피해가 0인 적(궁수·술사·금두꺼비)은 뺀다.
//   둘러싸임: 플레이어 몸 둘레에 적 몸이 몇 개 붙는지(SurroundCount). 같은 적은 contact.sameEnemyIntervalSeconds 마다 한 번.
// 기획에 없는 것(덩이가 사람 수만큼 세게 치는지, 받는 피해 -10%를 호신 앞뒤 어디서 빼는지)은 [가정]으로 아래에 적었다.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using BloodRain.Core.Spawning;

namespace BloodRain.Sim
{
    public static class ContactSim
    {
        /// <summary>플레이어 몸 반지름(m). [가정] 적 몸(Assumptions.EnemyRadius 0.4)과 같게 둔다.</summary>
        public const double PlayerRadius = 0.4;
        /// <summary>받는 피해 -10%(화경 보상)는 호신을 뺀 뒤에 곱한다. [가정]</summary>
        public const bool TakenAfterArmor = true;

        public static readonly float[] Minutes = { 0, 5, 10, 20, 30 };

        /// <summary>호신 세 경우: 맨몸, 연무장 호신 3, 연무장 3 + 금강체 Lv5 + 소림 성질(받는 피해 -2)을 모두 얹은 최대.</summary>
        public static IReadOnlyList<(string Ko, float Armor)> ArmorCases(GameData g)
        {
            float training = TrainingMax(g, "Armor");
            var geumgang = g.Passives.First(p => p.Id == "geumgang");
            float passive = geumgang.Effects.Where(e => e.Stat == "Armor").Sum(e => e.PerLevel) * g.Balance.Levels.PassiveMax;
            return new[] { ("맨몸", 0f), ("연무장", training), ("최대", training + passive + 2f) };
        }

        /// <summary>최대 기혈 비율 두 경우: 몸 그대로, 연무장 기혈 다 사고 혈해심법 Lv5. 이류 보상 +10%는 이류 뒤 분에만 더한다.</summary>
        public static float MaxHealthBuild(GameData g)
        {
            var hyeolhae = g.Passives.First(p => p.Id == "hyeolhae");
            return TrainingMax(g, "MaxHealth") + hyeolhae.Effects.Where(e => e.Stat == "MaxHealth").Sum(e => e.PerLevel) * g.Balance.Levels.PassiveMax;
        }

        /// <summary>회복 최대(초당): 연무장 회복 다 사고 매화심법 Lv5.</summary>
        public static float RecoveryMax(GameData g)
        {
            var maehwa = g.Passives.First(p => p.Id == "maehwa");
            return TrainingMax(g, "Recovery") + maehwa.Effects.Where(e => e.Stat == "Recovery").Sum(e => e.PerLevel) * g.Balance.Levels.PassiveMax;
        }

        static float TrainingMax(GameData g, string stat)
        {
            var t = g.Balance.Training.Items.First(i => i.Stat == stat);
            return t.PerLevel * t.MaxLevel;
        }

        /// <summary>몸에 맞닿아 둘러설 수 있는 적 수. 두 원 중심 사이 원둘레를 적 지름으로 나눈 내림.</summary>
        public static int SurroundCount => (int)Math.Floor(Math.PI * (PlayerRadius + Assumptions.EnemyRadius) / Assumptions.EnemyRadius);

        /// <summary>경지 보상 문자열에서 '접두어 + 숫자'(max_health_10, damage_taken_minus10)를 찾아 비율로 돌려준다.</summary>
        public static float RewardRatio(GameData g, string realmId, string prefix)
        {
            var r = g.Balance.Realms.FirstOrDefault(x => x.Id == realmId);
            if (r?.Reward == null) return 0;
            foreach (var tok in r.Reward.Split(','))
                if (tok.StartsWith(prefix, StringComparison.Ordinal) &&
                    int.TryParse(tok.Substring(prefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
                    return v / 100f;
            return 0;
        }

        /// <summary>그 분, 그 지역에서 닿는 적의 기준 접촉 피해(가중 평균)와 가장 센 적. 접촉 피해 0인 적은 뺀다.</summary>
        public static (double Avg, double Max, string MaxId) BaseContact(GameData g, WaveTable wt, RegionSpawns rs, string regionId, float minute)
        {
            var flow = wt.Chapter(WaveTable.Life);
            var band = wt.BandAt(WaveTable.Life, minute);
            var local = rs.LocalEnemies(regionId);
            float share = local.Count > 0 ? flow.LocalShareAt(minute) : 0;
            var weights = new Dictionary<string, double>();
            void Add(string id, double w) { weights[id] = (weights.TryGetValue(id, out var x) ? x : 0) + w; }
            foreach (var s in band.Spawns) Add(s.Enemy, (1 - share) * s.Weight / band.TotalWeight);
            foreach (var id in local) Add(id, share / local.Count);

            double sum = 0, wsum = 0, max = 0; string maxId = "";
            foreach (var kv in weights)
            {
                var e = g.Enemies.First(x => x.Id == kv.Key);
                if (e.ContactDamage <= 0 || kv.Value <= 0) continue;
                sum += e.ContactDamage * kv.Value; wsum += kv.Value;
                if (e.ContactDamage > max) { max = e.ContactDamage; maxId = e.Id; }
            }
            return (wsum > 0 ? sum / wsum : 0, max, maxId);
        }

        /// <summary>한 번 맞을 때 피해.</summary>
        public static double PerHit(GameData g, double baseDamage, float minute, float density, float armor, float takenCut)
        {
            var sc = new EnemyScaling(g.Balance.EnemyScaling);
            float incoming = sc.DamageAt((float)baseDamage, minute, density);
            float min = g.Balance.Contact.MinDamage;
            return TakenAfterArmor ? Damage.Taken(incoming, armor, min) * (1 - takenCut) : Damage.Taken(incoming * (1 - takenCut), armor, min);
        }

        /// <summary>둘러싸였을 때 초당 받는 피해.</summary>
        public static double SurroundedDps(GameData g, double perHit) => perHit * SurroundCount / g.Balance.Contact.SameEnemyIntervalSeconds;

        /// <summary>회복이 따라잡는 '붙어 있는 적' 수.</summary>
        public static double RecoveryCovers(GameData g, double perHit, double recovery) =>
            recovery / (perHit / g.Balance.Contact.SameEnemyIntervalSeconds);

        public static string Report(SimData data)
        {
            var g = data.Game;
            var wt = WaveTable.From(g);
            var rs = new RegionSpawns(g);
            var armors = ArmorCases(g);
            float hpBuild = MaxHealthBuild(g), recMax = RecoveryMax(g);
            float iryuHp = RewardRatio(g, "iryu", "max_health_");
            float hwaCut = RewardRatio(g, "hwagyeong", "damage_taken_minus");
            var regions = g.Cheonha.Regions.Where(r => r.Id != "cheongun").ToList();
            float interval = g.Balance.Contact.SameEnemyIntervalSeconds;
            string F0(double x) => x.ToString("0", CultureInfo.InvariantCulture);
            string F1(double x) => x.ToString("0.0", CultureInfo.InvariantCulture);
            string F2(double x) => x.ToString("0.00", CultureInfo.InvariantCulture);
            string M(double m) { if (double.IsNaN(m)) return "—"; int sec = (int)Math.Round(m * 60); return $"{sec / 60}:{sec % 60:00}"; }

            var sb = new StringBuilder();
            sb.AppendLine("# 맞는 쪽 셈(접촉 피해)");
            sb.AppendLine();
            sb.AppendLine("`dotnet run --project core/BloodRain.Sim -- --contact docs/design/sim/contact.md` 로 다시 만든다(`ContactSim.cs`).");
            sb.AppendLine();
            sb.AppendLine("다른 시뮬레이터는 플레이어가 죽지 않는다고 보고 돈다. 여기서는 맞는 쪽만 본다. 움직임은 넣지 않고 '한 번 맞으면 얼마', '둘러싸이면 몇 초 버티나', '회복이 몇 마리를 따라잡나'만 낸다. "
                + "피해 식은 Core `EnemyScaling.DamageAt`(기준 × (1 + 0.03 × 분) × √혈운 농도)과 `Damage.Taken`(max(1, 피해 − 호신)) 그대로다. "
                + "닿는 적은 chapters.json 한 생 흐름의 그 분 가중치표에 지역 고유 적(localShare)을 섞은 가중 평균이고, 접촉 피해가 0인 적(궁수·술사·금두꺼비)은 뺐다.");
            sb.AppendLine();

            // 가정
            sb.AppendLine("## 가정");
            sb.AppendLine();
            sb.AppendLine($"- 둘러싸임: 몸 반지름 {F1(PlayerRadius)}m 둘레에 적(반지름 {F1(Assumptions.EnemyRadius)}m) **{SurroundCount}마리**가 붙는다. 같은 적은 {interval}초에 한 번 치니 초당 {F1(SurroundCount / interval)}대.");
            sb.AppendLine($"- 호신: {string.Join(", ", armors.Select(a => $"{a.Ko} {F0(a.Armor)}"))}. 최대는 연무장 호신 다 사고 금강체 Lv5에 소림 성질(받는 피해 −2)을 호신 2로 쳤다.");
            sb.AppendLine($"- 기혈: 몸 기혈 × (1 + 보너스). 이류 보상 +{F0(iryuHp * 100)}%는 격자 표에선 5분 칸부터, 동선 표에선 이류에 닿은 뒤부터 넣었다. '기혈 빌드'는 연무장 기혈 다 사고 혈해심법 Lv5(+{F0(hpBuild * 100)}%).");
            sb.AppendLine($"- 화경 보상 받는 피해 −{F0(hwaCut * 100)}%는 격자 표에선 30분 칸에만(목표 화경 23:20 뒤), 동선 표에선 그 생이 화경에 닿은 뒤부터 넣었다. 호신을 뺀 뒤에 곱했다(기획서에 순서가 없다).");
            sb.AppendLine("- 덩이(7장 '적 한 덩이')는 사람 수와 상관없이 적 하나 피해로 친다. 기획서에 덩이 접촉 피해가 따로 없어서다.");
            sb.AppendLine("- 아래 경지에서 생긴 적은 닿아도 피해가 없다(crush). 표는 모두 '지금 경지에서 생긴 적'에게 맞는 경우다.");
            sb.AppendLine();

            // 1. 한 번 피해
            sb.AppendLine("## 한 번 맞을 때 피해");
            sb.AppendLine();
            sb.AppendLine($"칸은 호신 {string.Join(" / ", armors.Select(a => a.Ko))}. 괄호는 그 분에 닿는 적 가운데 가장 센 적의 맨몸 피해.");
            sb.AppendLine();
            sb.AppendLine("| 지역(농도) | " + string.Join(" | ", Minutes.Select(m => $"{m}분")) + " |");
            sb.AppendLine("| --- | " + string.Join(" | ", Minutes.Select(_ => "---")) + " |");
            foreach (var r in regions)
            {
                var cells = Minutes.Select(m =>
                {
                    var (avg, max, _) = BaseContact(g, wt, rs, r.Id, m);
                    float cut = m >= 30 ? hwaCut : 0;
                    string a = string.Join(" / ", armors.Select(x => F1(PerHit(g, avg, m, r.Density, x.Armor, cut))));
                    return $"{a} ({F0(PerHit(g, max, m, r.Density, 0, cut))})";
                });
                sb.AppendLine($"| {r.NameKo}({F1(r.Density)}) | " + string.Join(" | ", cells) + " |");
            }
            sb.AppendLine();

            // 2. 둘러싸여 버티는 초
            sb.AppendLine($"## {SurroundCount}마리에게 둘러싸여 버티는 초");
            sb.AppendLine();
            sb.AppendLine($"몸 기혈 100(진소운·남궁·무당 몸) 기준. 칸은 '맨몸 호신 0 / 기혈 빌드 + 최대 호신'. 경공 무적은 {F1(g.Balance.Dash.Iframes)}초, 충전 {g.Balance.Dash.Charges}회, 다시 차는 데 {F0(g.Balance.Dash.RechargeSeconds)}초.");
            sb.AppendLine();
            sb.AppendLine("| 지역(농도) | " + string.Join(" | ", Minutes.Select(m => $"{m}분")) + " |");
            sb.AppendLine("| --- | " + string.Join(" | ", Minutes.Select(_ => "---")) + " |");
            var armorMax = armors[armors.Count - 1].Armor;
            double Seconds(double avg, float m, float density, float armor, float bonus)
            {
                float cut = m >= 30 ? hwaCut : 0;
                double hp = 100 * (1 + bonus + (m >= 5 ? iryuHp : 0));
                return hp / SurroundedDps(g, PerHit(g, avg, m, density, armor, cut));
            }
            foreach (var r in regions)
            {
                var cells = Minutes.Select(m =>
                {
                    var (avg, _, _) = BaseContact(g, wt, rs, r.Id, m);
                    return $"{F1(Seconds(avg, m, r.Density, 0, 0))} / {F1(Seconds(avg, m, r.Density, armorMax, hpBuild))}";
                });
                sb.AppendLine($"| {r.NameKo}({F1(r.Density)}) | " + string.Join(" | ", cells) + " |");
            }
            sb.AppendLine();

            // 3. 동선 위
            sb.AppendLine("## 실제 동선 위(천하 30분, 시드 1)");
            sb.AppendLine();
            sb.AppendLine("CheonhaSim(Core `Life`)이 남긴 분마다 서 있던 지역에서, 그 분 닿는 적 평균에게 맨몸으로 한 번 맞는 피해와 몸 기혈 대비 몇 대에 쓰러지는지. 칸은 '피해 · 대'.");
            sb.AppendLine();
            int[] marks = { 5, 10, 15, 20, 25, 30 };
            sb.AppendLine("| 문파(몸 기혈) | 현경 | " + string.Join(" | ", marks.Select(m => $"{m}분")) + " |");
            sb.AppendLine("| --- | --- | " + string.Join(" | ", marks.Select(_ => "---")) + " |");
            var hitsAt30 = new List<double>();
            var lives = new Dictionary<string, CheonhaLifeResult>();
            foreach (var s in g.Cheonha.Sects)
            {
                var life = CheonhaSim.Run(data, s.Id, 1);
                lives[s.Id] = life;
                bool Reached(string realm, int m) => life.RealmMinute.TryGetValue(realm, out var at) && at <= m;
                var cells = marks.Select(m =>
                {
                    var rid = life.RegionByMinute[Math.Min(m, life.RegionByMinute.Count - 1)];
                    var reg = g.Cheonha.Region(rid);
                    var (avg, _, _) = BaseContact(g, wt, rs, rid, m);
                    float cut = Reached("hwagyeong", m) ? hwaCut : 0;
                    double hit = PerHit(g, avg, m, reg.Density, 0, cut);
                    double hits = s.MaxHealth * (1 + (Reached("iryu", m) ? iryuHp : 0)) / hit;
                    if (m == 30) hitsAt30.Add(hits);
                    return $"{F1(hit)} · {F1(hits)}";
                });
                double hy = life.RealmMinute.TryGetValue("hyeongyeong", out var x) ? x : double.NaN;
                sb.AppendLine($"| {s.NameKo}({F0(s.MaxHealth)}) | {M(hy)} | " + string.Join(" | ", cells) + " |");
            }
            sb.AppendLine();

            // 4. 땅의 주인
            sb.AppendLine("## 땅의 주인 패턴(시드 1)");
            sb.AppendLine();
            sb.AppendLine("그 땅에서 태어난 문파가 처음 싸움을 건 분(없으면 아홉 문파 가운데 가장 이른 분)의 피해. bosspatterns.json baseDamage 를 Core `BossPatterns.ScaleDamage` 로 키웠다. "
                + "칸은 '맨몸 한 번 피해 · 그 문파 몸 기혈(이류 보상 포함)로 몇 대'. 0.35초·0.5초마다 치는 장판은 한 번 피해로 셌다.");
            sb.AppendLine();
            sb.AppendLine("| 땅의 주인(지역, 농도) | 싸움 건 분 | 몸 | 패턴 |");
            sb.AppendLine("| --- | --- | --- | --- |");
            var bossHits = new List<(string Boss, string Ko, double Hits)>();
            foreach (var reg in g.Cheonha.Regions.Where(r => r.Boss != null && g.BossPatterns.ContainsKey(r.Boss!)))
            {
                var set = g.BossPatterns[reg.Boss!];
                var home = g.Cheonha.Sects.FirstOrDefault(x => x.Region == reg.Id);
                double engaged = double.NaN; CheonhaBossLine? line = null;
                if (home != null) line = lives[home.Id].Bosses.FirstOrDefault(b => b.RegionId == reg.Id && !double.IsNaN(b.EngagedAt) && b.EngagedAt > 0);
                if (line == null)
                    line = lives.Values.SelectMany(l => l.Bosses).Where(b => b.RegionId == reg.Id && !double.IsNaN(b.EngagedAt) && b.EngagedAt > 0)
                        .OrderBy(b => b.EngagedAt).FirstOrDefault();
                if (line != null) engaged = line.EngagedAt;
                var body = home ?? g.Cheonha.Sects.First();
                string bossKo = line?.BossKo ?? reg.Boss!;
                double hp = body.MaxHealth * (1 + iryuHp);
                float fm = double.IsNaN(engaged) ? 30f : (float)engaged;
                string Hit(string ko, float baseDmg)
                {
                    double d = Damage.Taken(BossPatterns.ScaleDamage(baseDmg, fm, reg.Density, g.Balance.EnemyScaling.DamagePerMinute), 0, g.Balance.Contact.MinDamage);
                    if (ko != "몸") bossHits.Add((bossKo, ko, hp / d));
                    return $"{ko} {F0(d)} · {F1(hp / d)}";
                }
                var parts = new List<string> { Hit("몸", set.ContactDamage) };
                foreach (var pt in set.Patterns.Concat(set.Phase2).Where(x => x.BaseDamage > 0)) parts.Add(Hit(pt.NameKo, pt.BaseDamage));
                sb.AppendLine($"| {bossKo}({reg.NameKo}, {F1(reg.Density)}) | {(double.IsNaN(engaged) ? "30:00(안 싸움)" : M(engaged))} | {body.NameKo} {F0(body.MaxHealth)} | {string.Join(", ", parts)} |");
            }
            sb.AppendLine();

            // 5. 회복
            var song = g.Cheonha.Region("songshan");
            var bong = regions.OrderByDescending(r => r.Density).First();
            var (a0, _, _) = BaseContact(g, wt, rs, song.Id, 5);
            var (a30, _, _) = BaseContact(g, wt, rs, bong.Id, 30);
            double h0 = PerHit(g, a0, 5, song.Density, 0, 0), h30 = PerHit(g, a30, 30, bong.Density, 0, hwaCut);
            sb.AppendLine("## 회복");
            sb.AppendLine();
            sb.AppendLine($"회복 최대(연무장 회복 다 사고 매화심법 Lv5)는 초당 {F2(recMax)}. 붙어 있는 적 하나가 초당 주는 피해(한 번 피해 ÷ {interval}초)와 견주면 "
                + $"{song.NameKo} 5분에 {F2(RecoveryCovers(g, h0, recMax))}마리, {bong.NameKo} 30분에 {F2(RecoveryCovers(g, h30, recMax))}마리를 따라잡는다(맨몸).");
            sb.AppendLine();

            // 읽을 것
            var (s0, _, _) = BaseContact(g, wt, rs, song.Id, 0);
            double early = PerHit(g, s0, 0, song.Density, 0, 0);
            double earlyArmored = PerHit(g, s0, 0, song.Density, armorMax, 0);
            double lateArmored = PerHit(g, a30, 30, bong.Density, armorMax, hwaCut);
            double lateBare = PerHit(g, a30, 30, bong.Density, 0, hwaCut);
            sb.AppendLine("## 읽을 것");
            sb.AppendLine();
            sb.AppendLine($"- 둘러싸이면 어디서든 몇 초 안에 쓰러진다. {song.NameKo} 0분 맨몸도 {F1(100 / SurroundedDps(g, early))}초다. 기획서 8장 '포위당하면 빠르게 녹는다' 그대로이고, 경공(무적 {F1(g.Balance.Dash.Iframes)}초)으로 빠져나가는 게임이라는 뜻이다.");
            sb.AppendLine($"- 호신은 초반에만 크다. {song.NameKo} 0분 평균 적은 최대 호신이면 {F1(early)} → {F1(earlyArmored)}({F0((1 - earlyArmored / early) * 100)}% 줄어듦)이지만, {bong.NameKo} 30분에는 {F1(lateBare)} → {F1(lateArmored)}({F0((1 - lateArmored / lateBare) * 100)}% 줄어듦)이다. 뺄셈 호신이라 적 피해가 √농도와 분으로 커지면 몫이 작아진다.");
            sb.AppendLine($"- 회복은 30분 고농도 땅에선 거의 의미가 없다(붙은 적 {F2(RecoveryCovers(g, h30, recMax))}마리분). 회복을 살리려면 비율 회복(최대 기혈의 몇 %)이나 처치 흡혈 쪽이 필요하다. 지금은 손대지 않고 플레이 결과를 본다.");
            sb.AppendLine($"- 30분 자리에서 맨몸으로 몇 대 맞으면 쓰러지나: 문파 아홉 가운데값 {F1(Median(hitsAt30))}대. 몸 기혈은 경지로 거의 안 오르고(이류 +{F0(iryuHp * 100)}%뿐) 적 피해만 오르니, 경지가 오를수록 '덜 맞는 실력'이 더 중요해진다.");
            var twoHit = bossHits.Where(x => x.Hits < 2).OrderBy(x => x.Hits).ToList();
            sb.AppendLine($"- 땅의 주인 패턴은 처음 싸움에서 한 방이 그 땅 문파 몸 기혈의 {F0(100 / bossHits.Max(x => x.Hits))}~{F0(100 / bossHits.Min(x => x.Hits))}%다. "
                + (twoHit.Count > 0 ? $"두 대 안에 쓰러지는 패턴은 {string.Join(", ", twoHit.Select(x => $"{x.Boss} {x.Ko}({F1(x.Hits)}대)"))}. 예고가 있는 패턴이라 피하라는 뜻이지만, 몸 기혈이 낮은 문파가 첫 싸움에서 그 땅 주인과 맞붙으면 한 번 실수가 곧 죽음이다." : "두 대 안에 쓰러지는 패턴은 없다.")
                + " 패턴 피해는 싸움을 건 분으로 정해지니, 땅의 주인을 일찍 건드릴수록 덜 아프다.");
            sb.AppendLine("- 덩이 접촉 피해가 기획서에 없다. 화경 군세(44명)에 닿아도 한 사람 피해라면 고경지 잡몹이 너무 약해 보일 수 있고, 사람 수만큼이면 바로 쓰러진다. Unity 적 시뮬레이션(C-02) 전에 정할 일이다.");
            return sb.ToString();
        }

        static double Median(List<double> xs)
        {
            var s = xs.OrderBy(x => x).ToList();
            if (s.Count == 0) return double.NaN;
            return s.Count % 2 == 1 ? s[s.Count / 2] : (s[s.Count / 2 - 1] + s[s.Count / 2]) / 2;
        }
    }
}
