// 비전 여덟 가지 가운데 무공 구성으로 여는 것들이 한 판에서 언제쯤 갖춰지는지(밸런스 시뮬레이션). 보고서: docs/design/sim/secrets.md
//
// 레벨업 추첨은 Core Loadout.RollLevelUp, 판정은 Core SecretChecker 그대로. 무공은 전부 열렸다고 본다(도감을 거의 채운 사람).
//   한 판을 '레벨업 차례'로 돌리고, 차례마다 몇 분인지는 RunSimulator(숭산 무리 모형, 평균 정책) 30분 레벨 곡선 가운데값으로 바꾼다.
//   고르는 사람: 노리는 비전에 든 무공·심법이 보이면 먼저 받고 목표 레벨까지 올린다. 남은 칸이 노리는 것보다 적어지면 다른 새 것은 안 받는다.
//   무명검(무공 하나만)은 새 무공을 피하다가 고를 것이 새 무공뿐이면 어쩔 수 없이 받는다(다시 고르기·파기 0).
//   무극(게이지 0에서 3분)은 게이지를 0 쪽으로 옮기는 카드를 반긴다. 게이지는 레벨업으로만 움직이니 차례 사이에는 그대로다.
//   사건·횟수 조건(정신부, 음식, 보갑, 의서, 은빛 등롱, 역류 끊기)과 문파 몸·깬 장은 이 시뮬레이터에 없어서 무공 구성 몫만 잰다.
//   빠진 것: 보갑 강화(1~3개), 초절정 7칸째, 다시 고르기·파기, 깨달음 역근(게이지 0으로). 모두 있으면 더 빨라지는 쪽이다.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BloodRain.Core.Build;
using BloodRain.Core.Data;

namespace BloodRain.Sim
{
    public sealed class SecretRow
    {
        public string SecretId = "", NameKo = "", Character = "", CharacterKo = "";
        public int Runs;
        /// <summary>무공 구성 몫(levels·onlyWeapon)을 30분 안에 갖춘 판 수와, 갖춘 분 가운데값.</summary>
        public int BuildRuns;
        public double BuildMinute = double.NaN;
        /// <summary>Core SecretChecker 가 실제로 연 판 수와 분 가운데값(사건·횟수 조건이 없는 비전만 의미가 있다).</summary>
        public int OpenRuns;
        public double OpenMinute = double.NaN;
        /// <summary>무명검: 새 무공을 억지로 받은 판 수.</summary>
        public int ForcedRuns;
        /// <summary>30분에 노리는 무공·심법을 못 가졌는데 그 칸이 이미 다른 것으로 찬 판 수.</summary>
        public int BlockedRuns;
        /// <summary>다시 고르기 횟수(연무장).</summary>
        public int Rerolls;
        /// <summary>판정에 이 시뮬레이터가 못 보는 조건이 있는지.</summary>
        public bool Partial;
    }

    public static class SecretSim
    {
        public const double Minutes = 30;

        /// <summary>RunSimulator 평균 정책으로 잰 분 → 레벨 가운데값(0분은 1).</summary>
        public static List<(double Minute, double Level)> LevelCurve(SimData data, string character, int seeds, double minutes = Minutes)
        {
            var runs = Enumerable.Range(1, seeds)
                .Select(s => RunSimulator.Run(data, new SimOptions { Character = character, Seed = s, Minutes = minutes }))
                .ToList();
            var curve = new List<(double, double)> { (0, 1) };
            for (int m = 1; m <= (int)minutes; m++)
                curve.Add((m, Median(runs.Select(r => (double)r.LevelAt(m)))));
            return curve;
        }

        /// <summary>레벨 level 에 닿는 분(곡선 사이는 곧게). 곡선 끝까지 못 닿으면 NaN.</summary>
        public static double MinuteOfLevel(IReadOnlyList<(double Minute, double Level)> curve, double level)
        {
            if (level <= curve[0].Level) return curve[0].Minute;
            for (int i = 1; i < curve.Count; i++)
            {
                var (m0, l0) = curve[i - 1];
                var (m1, l1) = curve[i];
                if (level <= l1)
                    return l1 <= l0 ? m1 : m0 + (m1 - m0) * (level - l0) / (l1 - l0);
            }
            return double.NaN;
        }

        /// <summary>비전 하나의 무공 구성 몫이 지금 갖춰졌는지(levels·onlyWeapon).</summary>
        public static bool BuildReady(SecretDef s, BuildLevels levels)
        {
            var req = s.Require;
            if (req.OnlyWeapon != null && (levels.Weapons.Count != 1 || !levels.Weapons.ContainsKey(req.OnlyWeapon))) return false;
            foreach (var kv in req.Levels)
                if (levels.Level(kv.Key) < Math.Max(1, kv.Value)) return false;
            return true;
        }

        /// <summary>시뮬레이터가 못 보는 조건(문파 몸, 깬 장, 사건, 횟수 가운데 경과 시간 아닌 것)이 있는지.</summary>
        public static bool HasUnseen(SecretDef s)
        {
            var r = s.Require;
            return r.Sect != null || r.Cleared.Count > 0 || r.Event != null || r.Counts.Keys.Any(k => k != "elapsedSeconds");
        }

        /// <summary>판 하나. 반환: (구성 갖춘 분, 연 분, 새 무공을 억지로 받았나, 칸이 막혔나). 못 했으면 NaN.
        /// rerolls: 다시 고르기. 받고 싶은 카드가 하나도 없을 때(가장 나은 점수가 0 아래) 쓴다.</summary>
        public static (double Build, double Open, bool Forced, bool Blocked) RunOnce(GameData g, SecretDef s, string character,
            IReadOnlyList<(double Minute, double Level)> curve, int seed, double minutes = Minutes, int rerolls = 0, bool escape = false)
        {
            var rng = new Random(seed);
            var ch = g.Character(character);
            var loadout = new Loadout(g, ch);
            var unlocked = new HashSet<string>(g.Weapons.Select(w => w.Id));
            var record = new RunRecord(character);
            var checker = new SecretChecker(g, record);
            var req = s.Require;
            bool hold = req.Hold != null;
            double build = double.NaN, open = double.NaN;
            bool forced = false;

            void Mark(double minute)
            {
                var lv = BuildLevels.Of(g, loadout);
                if (double.IsNaN(build) && BuildReady(s, lv)) build = minute;
                if (double.IsNaN(open) && checker.Check(lv).Any(x => x.Id == s.Id)) open = minute;
            }

            double now = 0;
            Mark(0);
            for (int level = 2; ; level++)
            {
                double next = MinuteOfLevel(curve, level);
                double until = double.IsNaN(next) || next > minutes ? minutes : next;
                // 차례 사이: 1초씩 흘리며 판정(무극의 3분, 무명검의 10분을 센다)
                while (now + 1.0 / 60 <= until + 1e-9)
                {
                    now += 1.0 / 60;
                    record.Tick(1f, loadout.Gauge.Value);
                    Mark(now);
                }
                if (double.IsNaN(next) || next > minutes) break;

                var offers = loadout.RollLevelUp(rng, 0f, unlocked);
                var pick = Choose(g, s, loadout, offers, rng, hold, out double best);
                while (best < 0 && rerolls > 0)
                {
                    rerolls--;
                    offers = loadout.RollLevelUp(rng, 0f, unlocked);
                    pick = Choose(g, s, loadout, offers, rng, hold, out best);
                }
                // escape: S-07 (가) 고를 것이 새 무공뿐이면 은자·음식 카드가 섞여 나온다(받아도 칸·레벨이 그대로)
                if (escape && req.OnlyWeapon != null && pick.Kind == OfferKind.NewWeapon && best < 0) { Mark(now); continue; }
                if (req.OnlyWeapon != null && pick.Kind == OfferKind.NewWeapon) forced = true;
                loadout.Apply(pick);
                Mark(now);
            }
            bool blocked = req.Levels.Keys.Any(id =>
                (g.Weapons.Any(w => w.Id == id) && !loadout.Weapons.ContainsKey(id) && loadout.Weapons.Count >= loadout.WeaponSlots) ||
                (g.Passives.Any(p => p.Id == id) && !loadout.Passives.ContainsKey(id) && loadout.Passives.Count >= loadout.PassiveSlots));
            return (build, open, forced, blocked);
        }

        /// <summary>노리는 비전 쪽으로 고른다. 점수가 같으면 섞는다.</summary>
        public static Offer Choose(GameData g, SecretDef s, Loadout l, IReadOnlyList<Offer> offers, Random rng, bool favorZero) =>
            Choose(g, s, l, offers, rng, favorZero, out _);

        /// <summary>best: 고른 카드의 점수(흔들림 빼고). 0 아래면 받고 싶지 않은 카드밖에 없었다는 뜻.</summary>
        public static Offer Choose(GameData g, SecretDef s, Loadout l, IReadOnlyList<Offer> offers, Random rng, bool favorZero, out double best)
        {
            var req = s.Require;
            int missingW = req.Levels.Keys.Count(id => g.Weapons.Any(w => w.Id == id) && !l.Weapons.ContainsKey(id));
            int missingP = req.Levels.Keys.Count(id => g.Passives.Any(p => p.Id == id) && !l.Passives.ContainsKey(id));
            bool ready = BuildReady(s, BuildLevels.Of(g, l));

            double Score(Offer o) => Base(o) + rng.NextDouble() * 0.5;
            double Base(Offer o)
            {
                bool target = req.Levels.TryGetValue(o.Id, out int want);
                double sc;
                switch (o.Kind)
                {
                    case OfferKind.NewWeapon:
                        if (target) sc = 10;
                        else if (req.OnlyWeapon != null) sc = -10;
                        else sc = l.WeaponSlots - l.Weapons.Count <= missingW ? -5 : 5;
                        break;
                    case OfferKind.NewPassive:
                        if (target) sc = 10;
                        else sc = l.PassiveSlots - l.Passives.Count <= missingP ? -5 : 4;
                        break;
                    case OfferKind.UpgradeWeapon:
                        sc = target && l.Weapons[o.Id] < Math.Max(1, want) ? 9 : (o.Id == req.OnlyWeapon ? 9 : 6);
                        break;
                    case OfferKind.UpgradePassive:
                        sc = target && l.Passives[o.Id] < Math.Max(1, want) ? 9 : 4;
                        break;
                    default: sc = 0; break;
                }
                if (favorZero)
                {
                    int after = Math.Abs(l.Gauge.Value + GaugeDelta(g, o));
                    // 구성을 갖춘 뒤엔 게이지를 0에 두는 것이 먼저다
                    sc += ready ? -after : -0.05 * after;
                }
                return sc;
            }

            var pick = offers.OrderByDescending(Score).First();
            best = Base(pick);
            return pick;
        }

        /// <summary>그 카드를 받으면 게이지가 얼마나 옮겨지나(Core YinYangGauge 와 같은 셈, 끝 자르기 전).</summary>
        public static int GaugeDelta(GameData g, Offer o)
        {
            var y = g.Balance.Yinyang;
            int Sign(YinYang a) => a == YinYang.Yang ? 1 : a == YinYang.Yin ? -1 : 0;
            switch (o.Kind)
            {
                case OfferKind.NewWeapon:
                case OfferKind.UpgradeWeapon:
                    return Sign(g.Weapon(o.Id).Aspect) * y.WeaponPerLevel;
                case OfferKind.NewPassive:
                case OfferKind.UpgradePassive:
                    var p = g.Passive(o.Id);
                    return Sign(p.Aspect) * (p.YinyangPerLevelOverride ?? y.PassivePerLevel);
                default: return 0;
            }
        }

        /// <summary>그 비전을 노리기 가장 좋은 캐릭터들: 시작 무공이 조건에 든 캐릭터, 없으면 진소운.</summary>
        public static List<string> CharactersFor(GameData g, SecretDef s)
        {
            var list = g.Characters.Where(c => s.Require.Levels.ContainsKey(c.StartWeapon) || c.StartWeapon == s.Require.OnlyWeapon)
                .Select(c => c.Id).ToList();
            if (s.Require.Character != null) list = new List<string> { s.Require.Character };
            if (list.Count == 0) list.Add("jin_sowoon");
            return list;
        }

        public static SecretRow Measure(GameData g, SecretDef s, string character, IReadOnlyList<(double Minute, double Level)> curve, int runs, int rerolls = 0, bool escape = false)
        {
            var res = Enumerable.Range(1, runs).Select(seed => RunOnce(g, s, character, curve, seed, Minutes, rerolls, escape)).ToList();
            return new SecretRow
            {
                SecretId = s.Id, NameKo = s.NameKo, Character = character, CharacterKo = g.Character(character).NameKo, Runs = runs,
                BuildRuns = res.Count(r => !double.IsNaN(r.Build)),
                BuildMinute = Median(res.Select(r => r.Build).Where(x => !double.IsNaN(x))),
                OpenRuns = res.Count(r => !double.IsNaN(r.Open)),
                OpenMinute = Median(res.Select(r => r.Open).Where(x => !double.IsNaN(x))),
                ForcedRuns = res.Count(r => r.Forced),
                BlockedRuns = res.Count(r => r.Blocked),
                Rerolls = rerolls,
                Partial = HasUnseen(s),
            };
        }

        /// <summary>비전 하나를 '버틸 시간(counts.elapsedSeconds)'만 바꿔 복사한다(S-07 (나) 비교용).</summary>
        public static SecretDef WithSeconds(SecretDef s, double seconds)
        {
            var r = s.Require;
            var counts = new Dictionary<string, double>(r.Counts) { ["elapsedSeconds"] = seconds };
            return new SecretDef
            {
                Id = s.Id, NameKo = s.NameKo, Hanja = s.Hanja, Proto = s.Proto, Condition = s.Condition, Reward = s.Reward, Hint = s.Hint,
                Require = new SecretRequireDef
                {
                    Character = r.Character, Sect = r.Sect, Cleared = r.Cleared, Levels = r.Levels, OnlyWeapon = r.OnlyWeapon,
                    Counts = counts, Event = r.Event, Hold = r.Hold,
                },
            };
        }

        /// <summary>S-07 무명검 고칠 방향: 이름, 버틸 초, 다시 고르기, 은자·음식 섞기.</summary>
        public static IReadOnlyList<(string Ko, double Seconds, int Rerolls, bool Escape)> MumyeongOptions(SecretDef s, int maxRerolls)
        {
            double now = s.Require.Counts.TryGetValue("elapsedSeconds", out var v) ? v : 600;
            return new[]
            {
                ($"지금({now / 60:0}분)", now, 0, false),
                ($"지금 + 다시 고르기 {maxRerolls}", now, maxRerolls, false),
                ("(가) 새 무공뿐이면 은자·음식", now, 0, true),
                ("(나) 7분", 420.0, 0, false),
                ("(나) 5분", 300.0, 0, false),
                ($"(나) 5분 + 다시 고르기 {maxRerolls}", 300.0, maxRerolls, false),
                ("(다) 파기·다시 고르기 15", now, 15, false),
                ("(다) 파기·다시 고르기 30", now, 30, false),
            };
        }

        /// <summary>연무장 다시 고르기 최대(balance.json training Rerolls perLevel × maxLevel).</summary>
        public static int MaxRerolls(GameData g)
        {
            var t = g.Balance.Training.Items.FirstOrDefault(i => i.Stat == "Rerolls");
            return t == null ? 0 : (int)Math.Round(t.PerLevel * t.MaxLevel);
        }

        static double Median(IEnumerable<double> xs)
        {
            var a = xs.OrderBy(x => x).ToList();
            if (a.Count == 0) return double.NaN;
            return a.Count % 2 == 1 ? a[a.Count / 2] : (a[a.Count / 2 - 1] + a[a.Count / 2]) / 2;
        }

        static string M(double x) => double.IsNaN(x) ? "-" : x.ToString("0.0", CultureInfo.InvariantCulture);

        public static string Report(SimData data, int seeds = 5, int runs = 200)
        {
            var g = data.Game;
            var curves = new Dictionary<string, List<(double, double)>>();
            List<(double, double)> Curve(string c) => curves.TryGetValue(c, out var v) ? v : curves[c] = LevelCurve(data, c, seeds);

            int maxRerolls = MaxRerolls(g);
            var rows = new List<SecretRow>();
            var rerolled = new List<SecretRow>();
            foreach (var s in g.Synergies.Secrets)
            {
                if (s.Require.Levels.Count == 0 && s.Require.OnlyWeapon == null) continue;
                foreach (var c in CharactersFor(g, s))
                {
                    rows.Add(Measure(g, s, c, Curve(c), runs));
                    rerolled.Add(Measure(g, s, c, Curve(c), runs, maxRerolls));
                }
            }
            var skipped = g.Synergies.Secrets.Where(s => s.Require.Levels.Count == 0 && s.Require.OnlyWeapon == null).ToList();

            var sb = new StringBuilder();
            sb.AppendLine("# 비전: 무공 구성은 언제 갖춰지나");
            sb.AppendLine();
            sb.AppendLine("`dotnet run --project core/BloodRain.Sim -c Release -- --secrets docs/design/sim/secrets.md` 로 다시 쓴다. 코드 `core/BloodRain.Sim/SecretSim.cs`.");
            sb.AppendLine($"레벨업 추첨은 Core `Loadout.RollLevelUp`, 판정은 Core `SecretChecker` 그대로. 무공은 전부 열렸다고 보고(도감을 거의 채운 사람), 레벨업 차례마다 몇 분인지는 RunSimulator(숭산, 평균 정책) 30분 레벨 곡선 가운데값(시드 {seeds})으로 바꿨다. 비전·캐릭터마다 {runs}판.");
            sb.AppendLine("고르는 사람은 노리는 비전에 든 무공·심법이 보이면 먼저 받고 목표 레벨까지 올린다. 파기 0, 기연 0(카드 3장). 다시 고르기는 0번과 연무장 최대를 나란히 놓았다.");
            sb.AppendLine();
            sb.AppendLine("## 1. 비전마다");
            sb.AppendLine();
            sb.AppendLine("캐릭터는 시작 무공이 조건에 든 몸(없으면 진소운). '구성'은 무공·심법 레벨(무명검은 무공 하나만)까지, '열림'은 Core 판정이 실제로 연 것이다. 사건·횟수·문파 몸·깬 장이 붙은 비전은 이 시뮬레이터가 그 몫을 못 채워서 열림이 늘 '-'다(표에 *).");
            sb.AppendLine();
            sb.AppendLine($"'칸 막힘'은 30분에 노리는 무공·심법을 못 가졌는데 그 칸이 이미 다른 것으로 찬 판이다(받고 싶은 카드가 하나도 없는 차례에 어쩔 수 없이 받은 것). 오른쪽 둘은 연무장 다시 고르기 최대({maxRerolls}번)를 그런 차례에 쓴 것.");
            sb.AppendLine();
            sb.AppendLine($"| 비전 | 캐릭터 | 구성 갖춘 판 | 구성 분(가운데값) | 열린 판 | 열린 분 | 칸 막힘 | 다시 고르기 {maxRerolls}: 구성 | 열린 판 |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                var x = rerolled[i];
                sb.AppendLine($"| {r.NameKo}{(r.Partial ? "*" : "")} | {r.CharacterKo} | {r.BuildRuns}/{r.Runs} | {M(r.BuildMinute)} | {(r.Partial ? "-" : $"{r.OpenRuns}/{r.Runs}")} | {(r.Partial ? "-" : M(r.OpenMinute))} | {r.BlockedRuns}/{r.Runs} | {x.BuildRuns}/{x.Runs}({M(x.BuildMinute)}분) | {(x.Partial ? "-" : $"{x.OpenRuns}/{x.Runs}")} |");
            }
            sb.AppendLine();
            if (skipped.Count > 0)
                sb.AppendLine($"무공 구성 조건이 없는 비전({string.Join("·", skipped.Select(s => s.NameKo))})은 뺐다. 역천의 역류 횟수는 yinyang.md 를 본다.");
            sb.AppendLine();

            SecretRow R(SecretRow r) => rerolled[rows.IndexOf(r)];
            var mumyeong = rows.Where(r => g.Synergies.Secrets.First(s => s.Id == r.SecretId).Require.OnlyWeapon != null).ToList();
            var mugeuk = rows.Where(r => g.Synergies.Secrets.First(s => s.Id == r.SecretId).Require.Hold != null).ToList();
            foreach (var r in mumyeong)
            {
                var def = g.Synergies.Secrets.First(x => x.Id == r.SecretId);
                sb.AppendLine($"## 2. {r.NameKo} 고칠 방향별 (S-07)");
                sb.AppendLine();
                sb.AppendLine($"backlog S-07 의 셋을 같은 시드 {runs}판으로 돌렸다({r.CharacterKo}). (가)는 카드 석 장이 모두 새 무공일 때 은자·음식 카드가 섞여 나와 그걸 받는다고 두었다(무공·심법은 그대로). (다)는 이 시뮬레이터에 파기가 없어서 '받고 싶은 카드가 없을 때 쓰는 다시 고르기'를 그만큼 준 것으로 갈음했다.");
                sb.AppendLine();
                var opts = new List<(string Ko, SecretRow Row)>();
                sb.AppendLine("| 방향 | 열린 판 | 열린 분(가운데값) | 새 무공을 억지로 받은 판 |");
                sb.AppendLine("|---|---|---|---|");
                foreach (var o in MumyeongOptions(def, maxRerolls))
                {
                    var x = Measure(g, WithSeconds(def, o.Seconds), r.Character, Curve(r.Character), runs, o.Rerolls, o.Escape);
                    sb.AppendLine($"| {o.Ko} | {x.OpenRuns}/{x.Runs} | {M(x.OpenMinute)} | {x.ForcedRuns}/{x.Runs} |");
                    opts.Add((o.Ko, x));
                }
                sb.AppendLine();
                var esc = opts.First(o => o.Ko.StartsWith("(가)")).Row;
                var five = opts.First(o => o.Ko == "(나) 5분").Row;
                var d30 = opts.Last(o => o.Ko.StartsWith("(다)")).Row;
                sb.AppendLine($"- (가)만 판마다 연다({esc.OpenRuns}/{esc.Runs}, 가운데 {M(esc.OpenMinute)}분). 10분보다 늦는 건 삼재검법 Lv8 이 그때쯤 차서다. (나)는 5분으로 줄여도 {five.OpenRuns}/{five.Runs}: 삼재검법이 Lv8 에 닿기 전에 석 장이 모두 새 무공인 차례가 먼저 온다. (다)는 받고 싶지 않은 차례를 {MumyeongOptions(def, maxRerolls).Last().Rerolls}번 넘겨야 {d30.OpenRuns}/{d30.Runs}이고, 연 뒤엔 결국 새 무공을 받는다(보상은 그 뒤에도 남는다).");
                sb.AppendLine();
            }

            sb.AppendLine("## 읽는 법");
            sb.AppendLine();
            foreach (var r in mumyeong)
                sb.AppendLine($"- {r.NameKo}({r.CharacterKo}): 새 무공을 피해도 {r.ForcedRuns}/{r.Runs}판은 고를 것이 새 무공뿐인 차례가 와서 억지로 받았다. 열린 판 {r.OpenRuns}/{r.Runs}, 다시 고르기 {maxRerolls}번이어도 {R(r).OpenRuns}/{r.Runs}. 무공 칸이 비어 있는 동안 카드 석 장이 모두 새 무공인 차례가 자주 오고, 운이 좋아도 받을 수 있는 강화가 삼재검법 {g.Balance.Levels.WeaponMax - 1}번 + 심법 {g.Balance.Slots.Passives}칸 × {g.Balance.Levels.PassiveMax}단계 = {g.Balance.Levels.WeaponMax - 1 + g.Balance.Slots.Passives * g.Balance.Levels.PassiveMax}번뿐이라 레벨 {g.Balance.Levels.WeaponMax + g.Balance.Slots.Passives * g.Balance.Levels.PassiveMax}(진소운 곡선으로 {M(MinuteOfLevel(Curve(r.Character), g.Balance.Levels.WeaponMax + g.Balance.Slots.Passives * g.Balance.Levels.PassiveMax))}분)부터는 새 무공 말고 고를 것이 없다. 10분을 넘기려면 그 뒤로 남은 레벨업마다 파기나 다시 고르기가 있어야 한다. 조건(10분)이나 '새 무공 카드 대신 은자·음식' 같은 빠져나갈 길을 정하지 않으면 사실상 못 여는 비전이다.");
            foreach (var r in mugeuk)
                sb.AppendLine($"- {r.NameKo}({r.CharacterKo}): 구성은 {M(r.BuildMinute)}분쯤 갖추지만 게이지가 0에 3분 머문 판은 {r.OpenRuns}/{r.Runs}. 게이지는 레벨업으로만 움직여서, 무공·심법이 다 차 레벨업이 멈춘 뒤 0에 서 있으면 열리고 아니면 안 열린다. 역근(게이지 0으로)을 넣으면 더 쉬워진다. 다시 고르기 {maxRerolls}번이면 {R(r).OpenRuns}/{r.Runs}.");
            sb.AppendLine("- 사건 조건이 붙은 비전(*)은 구성 분이 '여기서부터 사건을 기다릴 수 있다'는 뜻이다. 사건이 얼마나 자주 나는지(정신부·음식·보갑·의서)는 데이터에 개수가 없어 재지 않았다.");
            sb.AppendLine("- 다시 고르기는 낙매검선·어강술·매화삼롱처럼 정해진 무공·심법 셋을 모아야 하는 비전에서 '칸 막힘'을 거의 없앤다. 다시 고르기가 없는 첫 몇 생에는 이 비전들이 네 판에 한 판꼴로 칸이 막힌다.");
            sb.AppendLine("- 빠진 것: 보갑 강화, 초절정 7칸째, 파기, 기연 4장째. 모두 넣으면 구성 분은 더 이르다. 무공이 덜 열린 사람은 노리는 무공이 아예 안 나올 수 있다(이 표는 다 열린 경우).");
            return sb.ToString();
        }
    }
}
