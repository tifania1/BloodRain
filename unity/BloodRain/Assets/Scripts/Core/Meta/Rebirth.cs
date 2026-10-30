#nullable enable
// 환혼(기획서 21장 '환혼: 다음 몸 고르기'·'타고난 성질'). 침방에서 다음 몸 후보를 뽑고, 고른 후보로 몸을 만든다.
//   후보: 해금한 문파에서 겹치지 않게 뽑는다. 해금한 문파가 후보 수보다 적으면(넷째 몸을 샀는데 셋만 열렸을 때)
//         같은 문파가 성질만 달리해 또 나온다.
//   타고난 성질: 후보마다 0·1·2개를 countWeights 비율로. 같은 성질은 두 번 안 붙고, excludes 쌍(음체·양체, 약골·강골)도 같이 안 붙는다.
//   첫 환혼(서장 직후)은 firstRebirth 셋 고정, firstRebirthTraits 가 거짓이면 성질도 없다. 다시 고르기도 없다.
//   몸: 문파가 기혈·걸음·음양 시작·시작 무공·오의·고유 능력을 정하고(5장), 성질이 능력치·음양 시작·윤회의 기 배율을 더한다.
//       태어난 문파의 진전은 homeSchoolTiers 만큼 더 센다. Synergy.Compute 의 바깥 보너스(고수의 유해와 같은 자리)로 넣는다.
// 난수는 밖에서 받는다(Insights.cs 와 같다). 같은 시드면 같은 후보가 나와야 일일 도전과 테스트가 된다.
using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Data;

namespace BloodRain.Core.Meta
{
    /// <summary>침방 후보 카드 한 장: 문파, 태어나는 곳, 그 땅의 혈운 농도, 타고난 성질, 대표 인물.</summary>
    public sealed class RebirthCandidate
    {
        public SectDef Sect { get; }
        /// <summary>태어나는 곳(지도의 점).</summary>
        public RegionDef Region { get; }
        /// <summary>타고난 성질(데이터 순서). 없으면 빈 목록.</summary>
        public IReadOnlyList<BirthTraitDef> Traits { get; }

        /// <summary>여자 몸인가. 능력치는 같고 대표 인물이 '사매'로 부르는 대사가 따로 있다(21장, docs/story/leaders.md).</summary>
        public bool Female { get; }

        public RebirthCandidate(SectDef sect, RegionDef region, IReadOnlyList<BirthTraitDef> traits, bool female = false)
        {
            Sect = sect;
            Region = region;
            Traits = traits;
            Female = female;
        }

        public float Density => Region.Density;
        /// <summary>그 문파 대표 인물(characters.json id). 화산·곤륜은 null.</summary>
        public string? Leader => Sect.Leader;

        /// <summary>성질 묶음이 같은가(순서는 보지 않는다).</summary>
        public bool SameTraits(RebirthCandidate other) =>
            Traits.Count == other.Traits.Count && Traits.All(t => other.Traits.Any(o => o.Id == t.Id));
    }

    /// <summary>고른 후보로 만든 몸. 한 판은 이 몸의 Character 로 시작한다.</summary>
    public sealed class RebirthBody
    {
        readonly StatBonus _traitStats;

        /// <summary>태어난 문파. 서장(진소운 제 몸)이면 null.</summary>
        public SectDef? Sect { get; }
        public RegionDef Region { get; }
        /// <summary>Loadout·Stats 가 받는 몸. 문파 몸이면 문파 값으로 만든 것이고, 서장이면 characters.json 의 진소운이다.</summary>
        public CharacterDef Character { get; }
        public IReadOnlyList<BirthTraitDef> Traits { get; }
        /// <summary>성질 능력치 합. 꺼낼 때마다 새 사본이라 더해도 몸은 바뀌지 않는다.</summary>
        public StatBonus TraitStats { get { var b = new StatBonus(); b.Add(_traitStats); return b; } }
        /// <summary>문파 음양 시작 + 성질 음양 시작(음체 -30, 양체 +30).</summary>
        public int YinyangStart => Character.YinyangStart;
        /// <summary>윤회의 기 성질 보너스 합(둔재 0.3, 단명 0.2). 정산 때 1 + 이 값을 곱한다.</summary>
        public float RebirthQiBonus { get; }
        /// <summary>태어난 문파 진전에 더할 수(문파 id → 수). 서장이면 비어 있다.</summary>
        public IReadOnlyDictionary<string, int> SchoolBonus { get; }

        public RebirthBody(SectDef? sect, RegionDef region, CharacterDef character, IReadOnlyList<BirthTraitDef> traits,
            StatBonus traitStats, float rebirthQiBonus, IReadOnlyDictionary<string, int> schoolBonus)
        {
            Sect = sect;
            Region = region;
            Character = character;
            Traits = traits;
            _traitStats = traitStats;
            RebirthQiBonus = rebirthQiBonus;
            SchoolBonus = schoolBonus;
        }

        public bool IsPrologue => Sect == null;
        /// <summary>여자 몸인가(후보에서 그대로). 서장 진소운은 남자.</summary>
        public bool Female { get; internal set; }

        /// <summary>이 몸으로 시작하는 로드아웃(시작 무공 Lv1, 음양 게이지는 이 몸의 시작값과 태극 구간).</summary>
        public Loadout NewLoadout(GameData data) => new Loadout(data, Character);

        /// <summary>이 몸의 능력치: 몸 + 심법 + 연무장 + 성질 (+ extra).</summary>
        public StatSheet ComputeStats(GameData data, IReadOnlyDictionary<string, int> passiveLevels,
            IReadOnlyDictionary<string, int> trainingLevels, StatBonus? extra = null)
        {
            var b = TraitStats;
            if (extra != null) b.Add(extra);
            return Stats.Compute(data, Character, passiveLevels, trainingLevels, b);
        }

        /// <summary>Synergy.Compute 에 넘길 바깥 보너스: 태어난 문파 + more(고수의 유해, 혈마의 속삭임 …)를 더한 것.</summary>
        public IReadOnlyDictionary<string, int> SchoolExtra(IReadOnlyDictionary<string, int>? more = null)
        {
            var d = new Dictionary<string, int>(SchoolBonus.Count + (more?.Count ?? 0));
            foreach (var kv in SchoolBonus) d[kv.Key] = kv.Value;
            if (more != null)
                foreach (var kv in more) d[kv.Key] = (d.TryGetValue(kv.Key, out var v) ? v : 0) + kv.Value;
            return d;
        }

        /// <summary>이 몸으로 본 지금 로드아웃의 연환·진전.</summary>
        public SynergyState ComputeSynergy(GameData data, Loadout loadout, IReadOnlyDictionary<string, int>? more = null) =>
            Synergy.Compute(data, loadout, SchoolExtra(more));
    }

    public static class Rebirth
    {
        /// <summary>같은 문파 후보끼리 성질이 겹치면 다시 굴리는 횟수 상한. 성질 조합은 수십 가지라 닿을 일이 없다.</summary>
        const int MaxTraitTries = 200;

        /// <summary>성질 수(0·1·2 …)를 비율대로 하나 뽑는다.</summary>
        public static int RollTraitCount(IReadOnlyList<float> weights, Random rng)
        {
            double total = 0;
            foreach (var w in weights) total += Math.Max(0, w);
            if (total <= 0) return 0;
            double r = rng.NextDouble() * total;
            for (int i = 0; i < weights.Count; i++)
            {
                r -= Math.Max(0, weights[i]);
                if (r < 0) return i;
            }
            return weights.Count - 1;
        }

        /// <summary>두 성질이 같이 붙을 수 없는 쌍인가.</summary>
        public static bool Excluded(BirthTraitsDef def, string a, string b) =>
            a != b && def.Excludes.Any(group => group.Contains(a) && group.Contains(b));

        /// <summary>후보 하나의 성질. 수는 countWeights, 이미 붙은 것과 안 맞는 성질은 빼고 고르게 뽑는다. 데이터 순서로 돌려준다.</summary>
        public static List<BirthTraitDef> RollTraits(BirthTraitsDef def, Random rng)
        {
            int n = RollTraitCount(def.CountWeights, rng);
            var chosen = new List<BirthTraitDef>(n);
            for (int k = 0; k < n; k++)
            {
                var options = def.List.Where(t => chosen.All(c => c.Id != t.Id && !Excluded(def, c.Id, t.Id))).ToList();
                if (options.Count == 0) break;
                chosen.Add(options[rng.Next(options.Count)]);
            }
            return def.List.Where(t => chosen.Contains(t)).ToList();
        }

        /// <summary>환생 후보 count 명. 해금한 문파에서 겹치지 않게 뽑고, 모자라면 같은 문파를 성질만 달리해 더 넣는다.</summary>
        public static List<RebirthCandidate> Roll(CheonhaDef c, IEnumerable<string> unlockedSects, int count, Random rng)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
            var unlocked = new HashSet<string>(unlockedSects);
            foreach (var id in unlocked)
                if (c.Sects.All(s => s.Id != id)) throw new ArgumentException($"없는 문파입니다: {id}");
            var pool = c.Sects.Where(s => unlocked.Contains(s.Id)).ToList();
            if (pool.Count == 0) throw new InvalidOperationException("해금한 문파가 없습니다");

            var picks = pool.ToList();
            int unique = Math.Min(count, picks.Count);
            for (int i = 0; i < unique; i++) Swap(picks, i, rng.Next(i, picks.Count));
            picks = picks.GetRange(0, unique);
            if (count > unique)
            {
                while (picks.Count < count) picks.Add(pool[rng.Next(pool.Count)]);
                // 겹친 문파가 늘 끝 카드에만 오지 않게 자리를 섞는다
                for (int i = 0; i < picks.Count - 1; i++) Swap(picks, i, rng.Next(i, picks.Count));
            }

            var result = new List<RebirthCandidate>(count);
            foreach (var sect in picks)
            {
                var region = c.Region(sect.Region);
                for (int tries = 0; ; tries++)
                {
                    if (tries >= MaxTraitTries) throw new InvalidOperationException($"{sect.Id} 후보끼리 성질을 다르게 붙이지 못했습니다");
                    var cand = new RebirthCandidate(sect, region, RollTraits(c.BirthTraits, rng));
                    if (result.Any(o => o.Sect.Id == sect.Id && o.SameTraits(cand))) continue;
                    result.Add(cand);
                    break;
                }
            }
            return WithGender(c, result, rng);
        }

        /// <summary>몸 성별을 굴린다. 성질을 다 굴린 뒤에 후보 차례대로(난수 순서를 지키려고 따로 둔다).</summary>
        public static bool RollFemale(RebirthDef def, SectDef sect, Random rng)
        {
            if (def.MaleOnlySects.Contains(sect.Id)) return false;
            return rng.NextDouble() < def.FemaleRatio;
        }

        static List<RebirthCandidate> WithGender(CheonhaDef c, List<RebirthCandidate> list, Random rng) =>
            list.Select(x => new RebirthCandidate(x.Sect, x.Region, x.Traits, RollFemale(c.Rebirth, x.Sect, rng))).ToList();

        /// <summary>첫 환혼(서장 직후): firstRebirth 문파를 데이터 순서대로. 성질은 firstRebirthTraits 가 참일 때만 굴린다.</summary>
        public static List<RebirthCandidate> First(CheonhaDef c, Random rng)
        {
            var result = new List<RebirthCandidate>(c.Rebirth.FirstRebirth.Count);
            foreach (var id in c.Rebirth.FirstRebirth)
            {
                var sect = c.Sect(id);
                var traits = c.Rebirth.FirstRebirthTraits ? RollTraits(c.BirthTraits, rng) : new List<BirthTraitDef>();
                result.Add(new RebirthCandidate(sect, c.Region(sect.Region), traits));
            }
            return WithGender(c, result, rng);
        }

        /// <summary>문파 바탕으로 만든 몸(Loadout·Stats 가 받는 CharacterDef 어댑터). Id 는 문파 id 다.</summary>
        public static CharacterDef CharacterOf(SectDef sect, int extraYinyang = 0) => new CharacterDef
        {
            Id = sect.Id,
            NameKo = sect.NameKo + " 제자",
            StartWeapon = sect.StartWeapon,
            MaxHealth = sect.MaxHealth,
            MoveSpeed = sect.MoveSpeed,
            YinyangStart = sect.YinyangStart + extraYinyang,
            HarmonyRange = sect.HarmonyRange,
            Ultimate = sect.Ultimate,
            Trait = sect.Trait,
            Unlock = sect.Unlock,
        };

        /// <summary>성질 묶음의 능력치 합. 이름은 StatKind 로 읽는다.</summary>
        public static StatBonus TraitStats(IEnumerable<BirthTraitDef> traits)
        {
            var b = new StatBonus();
            foreach (var t in traits)
                foreach (var kv in t.Stats) b.Add(Stats.Parse(kv.Key), kv.Value);
            return b;
        }

        /// <summary>고른 후보로 몸을 만든다.</summary>
        public static RebirthBody Body(CheonhaDef c, RebirthCandidate cand)
        {
            var traits = cand.Traits;
            int yinyang = traits.Sum(t => t.YinyangStart);
            float qi = traits.Sum(t => t.RebirthQi);
            var school = new Dictionary<string, int>();
            if (c.Rebirth.HomeSchoolTiers != 0) school[cand.Sect.Id] = c.Rebirth.HomeSchoolTiers;
            return new RebirthBody(cand.Sect, cand.Region, CharacterOf(cand.Sect, yinyang), traits, TraitStats(traits), qi, school) { Female = cand.Female };
        }

        /// <summary>서장(첫 생)의 몸: 진소운 제 몸(cheonha.json prologueBody). 성질·문파 진전 보너스 없음.</summary>
        public static RebirthBody Prologue(GameData data)
        {
            var c = data.Cheonha;
            return new RebirthBody(null, c.Region(c.PrologueBody.Region), data.Character(c.PrologueBody.Character),
                Array.Empty<BirthTraitDef>(), new StatBonus(), 0f, new Dictionary<string, int>());
        }

        static void Swap<T>(List<T> list, int i, int j)
        {
            var t = list[i]; list[i] = list[j]; list[j] = t;
        }
    }

    /// <summary>침방 한 번: 후보를 보여 주고, 다시 고르기를 남은 만큼 쓰고, 하나를 고른다.</summary>
    public sealed class RebirthDraw
    {
        readonly CheonhaDef _c;
        readonly List<string> _unlocked;
        readonly Random _rng;

        /// <summary>첫 환혼이면 참(후보 고정, 다시 고르기 없음).</summary>
        public bool IsFirst { get; }
        /// <summary>후보 수(넷째 몸이면 4).</summary>
        public int Count { get; }
        public int RerollsLeft { get; private set; }
        public IReadOnlyList<RebirthCandidate> Candidates { get; private set; }

        public RebirthDraw(CheonhaDef c, IEnumerable<string> unlockedSects, int count, int rerolls, bool first, Random rng)
        {
            _c = c;
            _unlocked = unlockedSects.ToList();
            _rng = rng;
            IsFirst = first;
            if (first)
            {
                Candidates = Rebirth.First(c, rng);
                Count = Candidates.Count;
                RerollsLeft = 0;
            }
            else
            {
                Count = count;
                RerollsLeft = Math.Max(0, rerolls);
                Candidates = Rebirth.Roll(c, _unlocked, count, rng);
            }
        }

        /// <summary>후보를 통째로 다시 뽑는다. 남은 횟수가 없으면 InvalidOperationException.</summary>
        public IReadOnlyList<RebirthCandidate> Reroll()
        {
            if (RerollsLeft <= 0) throw new InvalidOperationException("다시 고르기가 남지 않았습니다");
            RerollsLeft--;
            Candidates = Rebirth.Roll(_c, _unlocked, Count, _rng);
            return Candidates;
        }

        public RebirthBody Choose(int index)
        {
            if (index < 0 || index >= Candidates.Count) throw new ArgumentOutOfRangeException(nameof(index));
            return Rebirth.Body(_c, Candidates[index]);
        }
    }
}
