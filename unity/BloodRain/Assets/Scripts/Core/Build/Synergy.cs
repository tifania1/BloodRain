#nullable enable
// 연환·진전 계산(기획서 20장). 로드아웃의 레벨만 보고 정하므로 같은 로드아웃이면 언제 불러도 같은 답이 나온다.
//   연환: 두 무공이 모두 minLevel 이상. 절기가 된 무공은 최고 단계로 친다.
//   진전: 문파 무공·심법을 가진 개수 + 바깥 보너스(고수의 유해, 연고지 …) + 기본의 극.
//   기본의 극: 삼재검법 Lv8이면 가장 많이 모은 문파에 하나 더(common.target 이 all 이면 모든 문파에).
//   돈오(GU-01): 로드아웃에서 온 레벨표면 조건을 갖춰도 돈오 카드를 집은 연환(Loadout.AwakenedLinks)만 켜지고, 나머지는 PendingLinks.
//     대성한 연환(Loadout.GrandLinks)은 LinkParam 이 doneoGrandMultiplier 배. 손으로 만든 레벨표(Awakened == null)는 조건만 본다.
//   비급 층(tier: secret): 복원한 것만 켜진다. 조건은 갖췄는데 잠긴 것은 LockedLinks·LockedTiers 로 따로 알린다('?' 표시, 찢긴 비급 등록).
using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;

namespace BloodRain.Core.Build
{
    /// <summary>판정용 레벨표. 무공은 절기가 되면 최고 단계로 넣는다.</summary>
    public sealed class BuildLevels
    {
        public IReadOnlyDictionary<string, int> Weapons { get; }
        public IReadOnlyDictionary<string, int> Passives { get; }
        /// <summary>돈오로 깨친 연환. null 이면 조건만 보고 켠다(시뮬레이터의 조합 셈·시험용).</summary>
        public IReadOnlyCollection<string>? Awakened { get; }
        /// <summary>대성한 연환. null 이면 없음.</summary>
        public IReadOnlyCollection<string>? Grand { get; }

        public BuildLevels(IReadOnlyDictionary<string, int> weapons, IReadOnlyDictionary<string, int> passives,
            IReadOnlyCollection<string>? awakened = null, IReadOnlyCollection<string>? grand = null)
        {
            Weapons = weapons;
            Passives = passives;
            Awakened = awakened;
            Grand = grand;
        }

        public static BuildLevels Of(GameData data, Loadout loadout)
        {
            int max = data.Balance.Levels.WeaponMax;
            var w = new Dictionary<string, int>();
            foreach (var kv in loadout.Weapons)
                w[kv.Key] = loadout.Evolved.Contains(kv.Key) ? Math.Max(kv.Value, max) : kv.Value;
            return new BuildLevels(w, loadout.Passives.ToDictionary(kv => kv.Key, kv => kv.Value),
                loadout.AwakenedLinks.ToList(), loadout.GrandLinks.ToList());
        }

        /// <summary>무공이든 심법이든 지금 레벨. 없으면 0.</summary>
        public int Level(string id) =>
            Weapons.TryGetValue(id, out var w) ? w : Passives.TryGetValue(id, out var p) ? p : 0;

        /// <summary>선택지 하나를 고른 뒤의 레벨표(원래 표는 그대로 둔다).</summary>
        public BuildLevels After(Offer offer)
        {
            var w = Weapons.ToDictionary(kv => kv.Key, kv => kv.Value);
            var p = Passives.ToDictionary(kv => kv.Key, kv => kv.Value);
            switch (offer.Kind)
            {
                case OfferKind.NewWeapon: w[offer.Id] = 1; break;
                case OfferKind.UpgradeWeapon: w[offer.Id] = (w.TryGetValue(offer.Id, out var wl) ? wl : 0) + offer.Levels; break;   // 대오 카드는 2레벨
                case OfferKind.NewPassive: p[offer.Id] = 1; break;
                case OfferKind.UpgradePassive: p[offer.Id] = (p.TryGetValue(offer.Id, out var pl) ? pl : 0) + offer.Levels; break;
            }
            return new BuildLevels(w, p, Awakened, Grand);
        }
    }

    /// <summary>한 문파의 진전 상태.</summary>
    public sealed class SchoolState
    {
        public SchoolDef School { get; }
        /// <summary>실제로 가진 그 문파 무공·심법 수.</summary>
        public int Members { get; }
        /// <summary>바깥 보너스와 기본의 극으로 더해진 수.</summary>
        public int Bonus { get; }
        public int Count => Members + Bonus;
        /// <summary>닿은 가장 높은 단계. 없으면 null.</summary>
        public SchoolTierDef? Tier { get; }
        /// <summary>다음 단계. 다 채웠으면 null.</summary>
        public SchoolTierDef? Next { get; }
        /// <summary>다음 단계까지 더 모아야 하는 수. 다음 단계가 없으면 0.</summary>
        public int ToNext => Next == null ? 0 : Next.Count - Count;
        /// <summary>닿은 단계의 개수(2·3·4·5). 없으면 0.</summary>
        public int TierCount => Tier?.Count ?? 0;
        /// <summary>개수가 닿아 켜진 단계(낮은 것부터). 잠긴 비급 단계는 빠진다.</summary>
        public IReadOnlyList<SchoolTierDef> ReachedTiers { get; }
        /// <summary>개수는 닿았는데 비급이 잠겨 안 켜진 단계.</summary>
        public IReadOnlyList<SchoolTierDef> LockedTiers { get; }

        /// <summary>open: 이 단계가 열려 있나(null 이면 모두 열림).</summary>
        public SchoolState(SchoolDef school, int members, int bonus, Func<SchoolTierDef, bool>? open = null)
        {
            School = school;
            Members = members;
            Bonus = bonus;
            int count = members + bonus;
            var reached = new List<SchoolTierDef>();
            var locked = new List<SchoolTierDef>();
            foreach (var t in school.Tiers.OrderBy(t => t.Count))
            {
                bool isOpen = open == null || open(t);
                if (t.Count <= count)
                {
                    if (isOpen) { Tier = t; reached.Add(t); }
                    else locked.Add(t);
                }
                else if (isOpen && Next == null) Next = t;
            }
            ReachedTiers = reached;
            LockedTiers = locked;
        }
    }

    public sealed class SynergyState
    {
        /// <summary>켜진 연환(데이터 순서).</summary>
        public IReadOnlyList<LinkDef> Links { get; }
        /// <summary>모든 문파(데이터 순서). 단계가 없는 문파도 들어 있다.</summary>
        public IReadOnlyList<SchoolState> Schools { get; }
        /// <summary>기본의 극이 하나를 채워 준 문파. all 모드면 null(모든 문파).</summary>
        public string? CommonSchool { get; }
        /// <summary>기본의 극이 켜져 있나(삼재검법 Lv8).</summary>
        public bool CommonActive { get; }
        /// <summary>두 무공 레벨은 갖췄는데 비급이 잠겨 안 켜진 연환.</summary>
        public IReadOnlyList<LinkDef> LockedLinks { get; }
        /// <summary>조건은 갖췄는데 돈오 카드를 아직 안 집어 꺼져 있는 연환(다음 레벨업에 돈오 카드로 뜬다).</summary>
        public IReadOnlyList<LinkDef> PendingLinks { get; }
        /// <summary>켜진 연환 중 대성한 것의 id.</summary>
        public IReadOnlyCollection<string> GrandLinks { get; }
        readonly float _grandMultiplier;

        public SynergyState(IReadOnlyList<LinkDef> links, IReadOnlyList<SchoolState> schools, string? commonSchool, bool commonActive,
            IReadOnlyList<LinkDef>? lockedLinks = null, IReadOnlyList<LinkDef>? pendingLinks = null,
            IReadOnlyCollection<string>? grandLinks = null, float grandMultiplier = 1f)
        {
            Links = links;
            Schools = schools;
            CommonSchool = commonSchool;
            CommonActive = commonActive;
            LockedLinks = lockedLinks ?? Array.Empty<LinkDef>();
            PendingLinks = pendingLinks ?? Array.Empty<LinkDef>();
            GrandLinks = grandLinks ?? Array.Empty<string>();
            _grandMultiplier = grandMultiplier;
        }

        public bool IsGrand(string id) => GrandLinks.Contains(id);

        /// <summary>조건은 갖췄는데 잠긴 비급의 복원 id(연환 먼저). 이 상태로 생을 마치면 찢긴 비급으로 오른다(Meta.LifeLog.NoteSynergy).</summary>
        public IEnumerable<string> LockedManualIds =>
            LockedLinks.Select(l => l.ManualId)
                .Concat(Schools.SelectMany(s => s.LockedTiers.Select(t => SynergyDefs.ManualIdOf(s.School, t))));

        public bool HasLink(string id) => Links.Any(l => l.Id == id);
        /// <summary>켜진 연환의 효과 숫자. 안 켜졌으면 null(무공 실행기가 '켜졌으면 이 값'으로 쓴다). LK-01.</summary>
        /// 대성한 연환은 doneoGrandMultiplier 배(16개 공통 배율, 기획서 20장 '돈오').
        public float? LinkParam(string id, string key)
        {
            foreach (var l in Links)
                if (l.Id == id)
                {
                    var v = l.Param(key);
                    return IsGrand(id) ? v * _grandMultiplier : v;
                }
            return null;
        }
        public SchoolState School(string id) => Schools.First(s => s.School.Id == id);
        public int TierOf(string schoolId) => School(schoolId).TierCount;
        /// <summary>단계가 하나라도 켜진 문파(HUD 인장).</summary>
        public IEnumerable<SchoolState> ActiveSchools => Schools.Where(s => s.Tier != null);

        /// <summary>닿은 진전 단계의 능력치 덧셈(synergies.json stats). Life.CurrentStats(extra) 의 extra 에 더한다.
        /// 장강수로채 2단계 은자 +30% 는 Greed 로 온다.</summary>
        public StatBonus StatBonus()
        {
            var b = new StatBonus();
            foreach (var s in Schools)
                foreach (var t in s.ReachedTiers)
                    foreach (var kv in t.Stats) b.Add(Stats.Parse(kv.Key), kv.Value);
            return b;
        }

        /// <summary>닿은 진전 단계가 넓혀 주는 태극 구간(synergies.json harmonyRange 중 가장 넓은 것). 없으면 null.
        /// Unity 쪽은 시너지가 바뀔 때 loadout.Gauge.SetSynergyHarmonyRange(이 값)을 부른다.</summary>
        public int? HarmonyRange
        {
            get
            {
                int? best = null;
                foreach (var s in Schools)
                    foreach (var t in s.ReachedTiers)
                        if (t.HarmonyRange is int r && (best == null || r > best)) best = r;
                return best;
            }
        }
    }

    /// <summary>레벨업 카드에 붙일 연환 안내: 이 무공이 들어간 연환 중 짝 무공을 이미 가진 것.</summary>
    public readonly struct LinkHint
    {
        public readonly LinkDef Link;
        public readonly string Partner;
        /// <summary>이 선택지로 연환이 켜진다.</summary>
        public readonly bool Completes;
        /// <summary>비급이 잠긴 연환이라 카드에는 '?'로만 보인다(켜지지 않는다).</summary>
        public readonly bool Locked;
        public LinkHint(LinkDef link, string partner, bool completes, bool locked = false)
        { Link = link; Partner = partner; Completes = completes; Locked = locked; }
    }

    public readonly struct SchoolTierChange
    {
        public readonly SchoolDef School;
        /// <summary>새로 닿은 단계(Reached) 또는 잃는 단계(Lost).</summary>
        public readonly SchoolTierDef Tier;
        public SchoolTierChange(SchoolDef school, SchoolTierDef tier) { School = school; Tier = tier; }
    }

    /// <summary>선택지 하나를 고르면 시너지가 어떻게 바뀌는지(웹 프로토타입 offerCard 의 연환·진전 줄).</summary>
    public sealed class OfferPreview
    {
        /// <summary>이 무공·심법의 문파. 삼재검법이나 은자·음식이면 null.</summary>
        public SchoolDef? School { get; }
        /// <summary>고른 뒤 그 문파의 진전 개수(문파가 없으면 0).</summary>
        public int SchoolCountAfter { get; }
        /// <summary>고르면 새로 닿는 진전 단계. 기본의 극이 옮겨 가며 생기는 것도 들어간다.</summary>
        public IReadOnlyList<SchoolTierChange> Reached { get; }
        /// <summary>기본의 극이 다른 문파로 옮겨 가서 내려가는 단계(드물다).</summary>
        public IReadOnlyList<SchoolTierChange> Lost { get; }
        /// <summary>이 무공이 들어간 연환 중 짝을 이미 가진 것(아직 안 켜진 것만).</summary>
        public IReadOnlyList<LinkHint> Links { get; }
        public IEnumerable<LinkDef> CompletedLinks => Links.Where(h => h.Completes).Select(h => h.Link);

        public OfferPreview(SchoolDef? school, int schoolCountAfter, IReadOnlyList<SchoolTierChange> reached,
            IReadOnlyList<SchoolTierChange> lost, IReadOnlyList<LinkHint> links)
        {
            School = school;
            SchoolCountAfter = schoolCountAfter;
            Reached = reached;
            Lost = lost;
            Links = links;
        }
    }

    public static class Synergy
    {
        static readonly IReadOnlyDictionary<string, int> NoExtra = new Dictionary<string, int>();

        /// <summary>지금 로드아웃의 연환·진전. extraCounts: 문파 id → 바깥에서 더할 진전 수(고수의 유해, 연고지, 혈마의 속삭임 …).</summary>
        /// <param name="restored">복원한 비급 id(Meta.Soul.RestoredManuals). null 이면 비급까지 모두 열린 것으로 친다(시뮬레이터·시험용). 게임은 늘 넘긴다.</param>
        public static SynergyState Compute(GameData data, Loadout loadout, IReadOnlyDictionary<string, int>? extraCounts = null,
            IReadOnlyCollection<string>? restored = null)
            => Compute(data, BuildLevels.Of(data, loadout), extraCounts, restored);

        public static SynergyState Compute(GameData data, BuildLevels levels, IReadOnlyDictionary<string, int>? extraCounts = null,
            IReadOnlyCollection<string>? restored = null)
        {
            var syn = data.Synergies;
            extraCounts ??= NoExtra;
            foreach (var key in extraCounts.Keys)
                if (syn.Schools.All(s => s.Id != key)) throw new ArgumentException($"없는 문파에 진전 보너스: {key}");

            var met = syn.Links
                .Where(l => levels.Weapons.TryGetValue(l.A, out var a) && a >= l.MinLevel
                         && levels.Weapons.TryGetValue(l.B, out var b) && b >= l.MinLevel)
                .ToList();
            bool LinkOpen(LinkDef l) => !l.IsSecret || restored == null || restored.Contains(l.ManualId);
            var open = met.Where(LinkOpen).ToList();
            var lockedLinks = met.Where(l => !LinkOpen(l)).ToList();
            var aw = levels.Awakened;
            var links = aw == null ? open : open.Where(l => aw.Contains(l.Id)).ToList();
            var pending = aw == null ? new List<LinkDef>() : open.Where(l => !aw.Contains(l.Id)).ToList();
            var gr = levels.Grand;
            var grand = gr == null ? new List<string>() : links.Where(l => gr.Contains(l.Id)).Select(l => l.Id).ToList();

            var members = new int[syn.Schools.Count];
            var bonus = new int[syn.Schools.Count];
            for (int i = 0; i < syn.Schools.Count; i++)
            {
                var sc = syn.Schools[i];
                members[i] = sc.Members.Count(m => levels.Weapons.ContainsKey(m) || levels.Passives.ContainsKey(m));
                bonus[i] = extraCounts.TryGetValue(sc.Id, out var x) ? x : 0;
            }

            // 장경각에서 아직 복원하지 않은 비급 층은 닫혀 있다(restored 가 null 이면 모두 열림).
            var opens = new Func<SchoolTierDef, bool>?[syn.Schools.Count];
            for (int i = 0; i < syn.Schools.Count; i++)
            {
                var sc = syn.Schools[i];
                opens[i] = restored == null ? null
                    : t => !t.IsSecret || restored.Contains(SynergyDefs.ManualIdOf(sc, t));
            }

            bool commonActive = syn.Common.Weapon != "" && levels.Weapons.TryGetValue(syn.Common.Weapon, out var cl) && cl >= syn.Common.AtLevel;
            string? commonSchool = null;
            if (commonActive)
            {
                if (syn.Common.Target == "all")
                {
                    for (int i = 0; i < bonus.Length; i++) bonus[i]++;
                }
                else
                {
                    int best = PickBest(syn.Schools, members, bonus, opens);
                    if (best >= 0) { bonus[best]++; commonSchool = syn.Schools[best].Id; }
                }
            }

            var schools = new List<SchoolState>(syn.Schools.Count);
            for (int i = 0; i < syn.Schools.Count; i++)
            {
                schools.Add(new SchoolState(syn.Schools[i], members[i], bonus[i], opens[i]));
            }
            return new SynergyState(links, schools, commonSchool, commonActive, lockedLinks, pending, grand,
                data.Balance.LevelUp.DoneoGrandMultiplier);
        }

        /// <summary>기본의 극이 채워 줄 문파. 가장 많이 모은 문파를 고르되, 이미 마지막 단계까지 찬 문파는 채울 것이 없으니 건너뛴다.
        /// 개수가 같으면 하나 더해서 새 단계에 닿는 쪽(닫힌 비급 층은 닿는 것으로 치지 않는다), 그래도 같으면 데이터 순서. 하나도 안 모았으면 -1.</summary>
        static int PickBest(IReadOnlyList<SchoolDef> schools, int[] members, int[] bonus, Func<SchoolTierDef, bool>?[] opens)
        {
            int best = -1, bestCount = 0;
            bool bestReaches = false;
            for (int i = 0; i < schools.Count; i++)
            {
                int count = members[i] + bonus[i];
                if (count < 1 || count >= schools[i].MaxTierCount) continue;
                var open = opens[i];
                bool reaches = schools[i].Tiers.Any(t => t.Count == count + 1 && (open == null || open(t)));
                if (best < 0 || count > bestCount || (count == bestCount && reaches && !bestReaches))
                {
                    best = i; bestCount = count; bestReaches = reaches;
                }
            }
            return best;
        }

        /// <summary>선택지를 고르면 무엇이 켜지고 무엇이 가까워지는지. 로드아웃은 건드리지 않는다.</summary>
        public static OfferPreview Preview(GameData data, Loadout loadout, Offer offer, IReadOnlyDictionary<string, int>? extraCounts = null,
            IReadOnlyCollection<string>? restored = null)
            => Preview(data, BuildLevels.Of(data, loadout), offer, extraCounts, restored);

        public static OfferPreview Preview(GameData data, BuildLevels levels, Offer offer, IReadOnlyDictionary<string, int>? extraCounts = null,
            IReadOnlyCollection<string>? restored = null)
        {
            var syn = data.Synergies;
            var after = levels.After(offer);
            var s0 = Compute(data, levels, extraCounts, restored);
            var s1 = Compute(data, after, extraCounts, restored);

            var reached = new List<SchoolTierChange>();
            var lost = new List<SchoolTierChange>();
            for (int i = 0; i < s0.Schools.Count; i++)
            {
                SchoolState a = s0.Schools[i], b = s1.Schools[i];
                // 기본의 극이 옮겨 가면 한 번에 두 단계가 켜지거나 꺼질 수 있어 단계마다 본다(낮은 것부터).
                foreach (var t in b.ReachedTiers)
                    if (!a.ReachedTiers.Contains(t)) reached.Add(new SchoolTierChange(b.School, t));
                foreach (var t in a.ReachedTiers)
                    if (!b.ReachedTiers.Contains(t)) lost.Add(new SchoolTierChange(a.School, t));
            }

            var hints = new List<LinkHint>();
            if (offer.Kind == OfferKind.NewWeapon || offer.Kind == OfferKind.UpgradeWeapon)
            {
                foreach (var l in syn.Links)
                {
                    var partner = l.Partner(offer.Id);
                    if (partner == null || s0.HasLink(l.Id) || s0.PendingLinks.Contains(l) || !levels.Weapons.ContainsKey(partner)) continue;
                    bool locked = l.IsSecret && restored != null && !restored.Contains(l.ManualId);
                    if (locked && s0.LockedLinks.Contains(l)) continue;
                    // 돈오: 조건이 갖춰지면 '켜짐'이 아니라 다음 레벨업에 돈오 카드가 뜬다. 카드 안내는 같이 '이 선택으로 깨칠 수 있음'.
                    bool completes = locked ? s1.LockedLinks.Contains(l) : s1.HasLink(l.Id) || s1.PendingLinks.Contains(l);
                    hints.Add(new LinkHint(l, partner, completes, locked));
                }
            }

            var school = offer.Kind == OfferKind.Fallback ? null : syn.SchoolOf(offer.Id);
            int countAfter = school == null ? 0 : s1.School(school.Id).Count;
            return new OfferPreview(school, countAfter, reached, lost, hints);
        }
    }
}
