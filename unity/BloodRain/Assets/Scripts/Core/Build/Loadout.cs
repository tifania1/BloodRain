#nullable enable
// 한 판에서 가진 무공·심법과 레벨. 레벨업 선택지와 절기 진화 판정이 여기서 나온다.
using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Progression;

namespace BloodRain.Core.Build
{
    public enum OfferKind { NewWeapon, UpgradeWeapon, NewPassive, UpgradePassive, Fallback }

    public readonly struct Offer
    {
        public readonly OfferKind Kind;
        public readonly string Id;
        /// <summary>이 카드로 오르는 레벨 수. 0·1 은 보통 카드(1), 2 는 금빛 대오(大悟) 카드(기획서 8장, GU-02).</summary>
        readonly int _levels;
        public Offer(OfferKind kind, string id) { Kind = kind; Id = id; _levels = 1; }
        public Offer(OfferKind kind, string id, int levels) { Kind = kind; Id = id; _levels = levels < 1 ? 1 : levels; }
        public int Levels => _levels < 1 ? 1 : _levels;
        /// <summary>금빛 대오 카드(한 번에 2레벨 이상).</summary>
        public bool Great => Levels > 1;
        public Offer AsGreat(int levels) => new Offer(Kind, Id, levels);
        public override string ToString() => Great ? $"{Kind}:{Id}+{Levels}" : $"{Kind}:{Id}";
    }

    /// <summary>금빛 돈오(頓悟) 카드 한 장(기획서 20장 '돈오', GU-01). 레벨업 선택과 따로 뜨고 집어야 연환이 켜진다.
    /// Grand 면 이미 깨친 연환이 대성(大成)으로 한 번 더 깨인다(효과 값 doneoGrandMultiplier 배).</summary>
    public readonly struct DoneoCard
    {
        public readonly LinkDef Link;
        public readonly bool Grand;
        public DoneoCard(LinkDef link, bool grand) { Link = link; Grand = grand; }
        public override string ToString() => Grand ? $"Doneo:{Link.Id}+grand" : $"Doneo:{Link.Id}";
    }

    public sealed class Loadout
    {
        readonly GameData _data;
        readonly Dictionary<string, int> _weapons = new Dictionary<string, int>();
        readonly Dictionary<string, int> _passives = new Dictionary<string, int>();
        readonly HashSet<string> _evolved = new HashSet<string>();
        readonly HashSet<string> _banished = new HashSet<string>();
        readonly HashSet<string> _links = new HashSet<string>();
        readonly HashSet<string> _grandLinks = new HashSet<string>();

        public int WeaponSlots { get; private set; }
        public int PassiveSlots { get; private set; }
        public YinYangGauge Gauge { get; }

        public IReadOnlyDictionary<string, int> Weapons => _weapons;
        public IReadOnlyDictionary<string, int> Passives => _passives;
        public IReadOnlyCollection<string> Evolved => _evolved;
        /// <summary>돈오 카드를 집어 켠 연환 id. Synergy.Compute 는 이 안의 연환만 켠다.</summary>
        public IReadOnlyCollection<string> AwakenedLinks => _links;
        /// <summary>대성까지 깨친 연환 id.</summary>
        public IReadOnlyCollection<string> GrandLinks => _grandLinks;

        public Loadout(GameData data, CharacterDef character)
        {
            _data = data;
            WeaponSlots = data.Balance.Slots.Weapons;
            PassiveSlots = data.Balance.Slots.Passives;
            Gauge = new YinYangGauge(data.Balance.Yinyang, character.YinyangStart, character.HarmonyRange);
            _weapons[character.StartWeapon] = 1;
        }

        int WeaponMax => _data.Balance.Levels.WeaponMax;
        int PassiveMax => _data.Balance.Levels.PassiveMax;

        /// <summary>초절정 돌파 보상: 무공 또는 심법 7칸째.</summary>
        public void AddExtraSlot(bool weapon) { if (weapon) WeaponSlots++; else PassiveSlots++; }

        /// <summary>파기: 이번 판에서 영영 제외.</summary>
        public void Banish(string id) => _banished.Add(id);

        /// <summary>가진 무공 하나를 이번 판에서 버린다(검총). 칸에서 빼고 절기 표시도 지우고 다시 안 나오게 파기한다.
        /// 버린 무공의 레벨을 돌려준다. 가진 무공이 아니거나 마지막 하나면 예외.</summary>
        public int RemoveWeapon(string id)
        {
            if (!_weapons.TryGetValue(id, out var level)) throw new InvalidOperationException($"가진 무공이 아닙니다: {id}");
            if (_weapons.Count <= 1) throw new InvalidOperationException("마지막 무공은 버릴 수 없습니다");
            _weapons.Remove(id);
            _evolved.Remove(id);
            _banished.Add(id);
            return level;
        }

        public void Apply(Offer offer)
        {
            switch (offer.Kind)
            {
                case OfferKind.NewWeapon:
                    if (_weapons.ContainsKey(offer.Id)) throw new InvalidOperationException($"이미 가진 무공입니다: {offer.Id}");
                    if (_weapons.Count >= WeaponSlots) throw new InvalidOperationException("무공 칸이 가득 찼습니다");
                    _weapons[offer.Id] = 1;
                    Gauge.OnWeaponLevelUp(_data.Weapon(offer.Id));
                    break;
                case OfferKind.UpgradeWeapon:
                    if (_weapons[offer.Id] >= WeaponMax) throw new InvalidOperationException("이미 최고 단계입니다");
                    // 대오 카드는 레벨마다 한 번씩 올린다(음양도 레벨마다 움직임). 최고 단계에서 멈춘다.
                    for (int n = 0; n < offer.Levels && _weapons[offer.Id] < WeaponMax; n++)
                    {
                        _weapons[offer.Id]++;
                        Gauge.OnWeaponLevelUp(_data.Weapon(offer.Id));
                    }
                    break;
                case OfferKind.NewPassive:
                    if (_passives.ContainsKey(offer.Id)) throw new InvalidOperationException($"이미 가진 심법입니다: {offer.Id}");
                    if (_passives.Count >= PassiveSlots) throw new InvalidOperationException("심법 칸이 가득 찼습니다");
                    _passives[offer.Id] = 1;
                    Gauge.OnPassiveLevelUp(_data.Passive(offer.Id));
                    break;
                case OfferKind.UpgradePassive:
                    if (_passives[offer.Id] >= PassiveMax) throw new InvalidOperationException("이미 최고 단계입니다");
                    for (int n = 0; n < offer.Levels && _passives[offer.Id] < PassiveMax; n++)
                    {
                        _passives[offer.Id]++;
                        Gauge.OnPassiveLevelUp(_data.Passive(offer.Id));
                    }
                    break;
                case OfferKind.Fallback:
                    break;
            }
        }

        /// <summary>지금 고를 수 있는 모든 선택지(무작위 추첨 전 후보).</summary>
        public List<Offer> Candidates(ISet<string> unlocked)
        {
            var list = new List<Offer>();
            foreach (var kv in _weapons)
                if (kv.Value < WeaponMax && !_evolved.Contains(kv.Key)) list.Add(new Offer(OfferKind.UpgradeWeapon, kv.Key));
            foreach (var kv in _passives)
                if (kv.Value < PassiveMax) list.Add(new Offer(OfferKind.UpgradePassive, kv.Key));
            if (_weapons.Count < WeaponSlots)
                foreach (var w in _data.Weapons)
                    if (!_weapons.ContainsKey(w.Id) && !_banished.Contains(w.Id) && unlocked.Contains(w.Id))
                        list.Add(new Offer(OfferKind.NewWeapon, w.Id));
            if (_passives.Count < PassiveSlots)
                foreach (var p in _data.Passives)
                    if (!_passives.ContainsKey(p.Id) && !_banished.Contains(p.Id))
                        list.Add(new Offer(OfferKind.NewPassive, p.Id));
            return list;
        }

        /// <summary>레벨업 선택지 3개(기연이 높으면 4개). 고를 것이 없으면 은자·음식(Fallback).
        /// 뽑힌 것이 모두 새 무공이면 은자·음식을 한 장 더 붙인다(levelUp.fallbackWhenAllNewWeapons).</summary>
        /// <param name="knownLinks">장경각 도감에 오른 연환 id(지난 생들에서 깨친 것, Soul.Codex 의 link: 쪽). 그 연환의 짝 무공 카드는
        /// knownLinkWeight 배로 잘 뜬다(기획서 20장 '돈오'). null 이거나 가중치가 붙는 카드가 없으면 보통 추첨(난수 순서도 같다).</param>
        public List<Offer> RollLevelUp(Random rng, float luck, ISet<string> unlocked, IReadOnlyCollection<string>? knownLinks = null)
        {
            var rule = _data.Balance.LevelUp;
            int count = rule.BaseChoices;
            if (count < rule.MaxChoices && rng.NextDouble() < Math.Min(1.0, luck / rule.LuckForFourthChoice)) count++;

            var pool = Candidates(unlocked);
            var weights = pool.Select(o => KnownLinkWeight(o, knownLinks)).ToList();
            bool weighted = weights.Any(w => w != 1.0);
            var result = new List<Offer>();
            while (result.Count < count && pool.Count > 0)
            {
                int i = weighted ? WeightedIndex(rng, weights) : rng.Next(pool.Count);
                result.Add(pool[i]);
                pool.RemoveAt(i);
                weights.RemoveAt(i);
            }
            if (result.Count == 0 || (rule.FallbackWhenAllNewWeapons && result.All(o => o.Kind == OfferKind.NewWeapon)))
                result.Add(new Offer(OfferKind.Fallback, "silver_or_food"));
            MarkGreat(result, rng, luck);
            return result;
        }

        /// <summary>이 카드의 추첨 가중치: 무공 카드이고, 도감에 오른 연환 중 짝 무공을 가졌고 아직 안 깨친 것이 있으면 knownLinkWeight.</summary>
        public double KnownLinkWeight(Offer offer, IReadOnlyCollection<string>? knownLinks)
        {
            if (knownLinks == null || knownLinks.Count == 0) return 1.0;
            if (offer.Kind != OfferKind.NewWeapon && offer.Kind != OfferKind.UpgradeWeapon) return 1.0;
            double w = _data.Balance.LevelUp.KnownLinkWeight;
            if (w <= 0 || w == 1.0) return 1.0;
            foreach (var l in _data.Synergies.Links)
            {
                if (_links.Contains(l.Id) || !knownLinks.Contains(l.Id)) continue;
                var partner = l.Partner(offer.Id);
                if (partner != null && _weapons.ContainsKey(partner)) return w;
            }
            return 1.0;
        }

        static int WeightedIndex(Random rng, List<double> weights)
        {
            double total = weights.Sum(), r = rng.NextDouble() * total;
            for (int i = 0; i < weights.Count; i++)
            {
                r -= weights[i];
                if (r < 0) return i;
            }
            return weights.Count - 1;
        }

        /// <summary>대오 확률: levelUp.greatChance + 기연 × greatChancePerLuck(1 을 넘지 않음).</summary>
        public float GreatChance(float luck)
        {
            var rule = _data.Balance.LevelUp;
            return (float)Math.Max(0.0, Math.Min(1.0, rule.GreatChance + Math.Max(0f, luck) * rule.GreatChancePerLuck));
        }

        /// <summary>대오 카드가 될 수 있는 카드: 가진 것 강화이고 남은 단계가 greatLevels 이상. 새 무공·심법, 은자·음식은 안 된다.</summary>
        public bool CanBeGreat(Offer offer)
        {
            int need = _data.Balance.LevelUp.GreatLevels;
            if (need < 2) return false;
            switch (offer.Kind)
            {
                case OfferKind.UpgradeWeapon: return _weapons.TryGetValue(offer.Id, out var w) && WeaponMax - w >= need;
                case OfferKind.UpgradePassive: return _passives.TryGetValue(offer.Id, out var p) && PassiveMax - p >= need;
                default: return false;
            }
        }

        /// <summary>뽑힌 카드마다 대오 판정(기획서 8장 '대오 카드'). 확률이 0 이면 난수를 쓰지 않는다.</summary>
        void MarkGreat(List<Offer> offers, Random rng, float luck)
        {
            float chance = GreatChance(luck);
            if (chance <= 0f) return;
            int levels = _data.Balance.LevelUp.GreatLevels;
            for (int i = 0; i < offers.Count; i++)
                if (CanBeGreat(offers[i]) && rng.NextDouble() < chance) offers[i] = offers[i].AsGreat(levels);
        }

        /// <summary>흐름의 첫 레벨업(chapters.json firstLevelUpChoices, 서장 1). 새 무공 카드만 그 수만큼, 은자·음식은 붙이지 않는다.
        /// 0 이거나 새 무공이 없으면 보통 레벨업(RollLevelUp).</summary>
        public List<Offer> RollFirstLevelUp(Random rng, float luck, ISet<string> unlocked, ChapterDef chapter)
        {
            int n = chapter.FirstLevelUpChoices;
            if (n <= 0) return RollLevelUp(rng, luck, unlocked);
            var pool = Candidates(unlocked).Where(o => o.Kind == OfferKind.NewWeapon).ToList();
            if (pool.Count == 0) return RollLevelUp(rng, luck, unlocked);
            var result = new List<Offer>();
            while (result.Count < n && pool.Count > 0)
            {
                int i = rng.Next(pool.Count);
                result.Add(pool[i]);
                pool.RemoveAt(i);
            }
            return result;
        }

        /// <summary>절기 그릇: 지금 경지에서 몸에 담을 수 있는 절기 수(절정1·초절정2·화경3·현경4). 천하에선 모든 생에서 현경까지 가므로
        /// mode 는 결과를 바꾸지 않는다. balance.json realms 에 onlyIn 이 붙은 경지가 생기면 그 모드에서만 센다.</summary>
        public int Vessels(int playerLevel, string mode = "normal")
        {
            var vessels = _data.Balance.Evolution.Vessels;
            int n = 0;
            foreach (var r in _data.Balance.Realms)
            {
                if (r.Level > playerLevel) continue;
                if (r.OnlyIn != null && !r.OnlyIn.Contains(mode)) continue;
                if (vessels.TryGetValue(r.Id, out var v) && v > n) n = v;
            }
            return n;
        }

        /// <summary>문턱에 선 무공: 최고 단계 + 짝 심법이 기준 레벨 이상 + 아직 절기가 아님. 절기 데이터 순서.</summary>
        public List<EvolutionDef> AtThreshold()
        {
            int minPair = _data.Balance.Evolution.PairPassiveMinLevel;
            var list = new List<EvolutionDef>();
            foreach (var evo in _data.Evolutions)
            {
                if (_evolved.Contains(evo.Weapon)) continue;
                if (!_weapons.TryGetValue(evo.Weapon, out var lv) || lv < WeaponMax) continue;
                if (!_passives.TryGetValue(evo.Passive, out var plv) || plv < minPair) continue;
                list.Add(evo);
            }
            return list;
        }

        /// <summary>지금 깨어날 수 있는 절기들. 그릇이 남아 있을 때만 문턱 목록을 돌려준다.
        /// 돌파 순간에 여럿이면 플레이어가 하나를 고르고, 보갑은 첫 번째를 쓴다.</summary>
        public List<EvolutionDef> Awakenable(int playerLevel, string mode = "normal")
            => _evolved.Count < Vessels(playerLevel, mode) ? AtThreshold() : new List<EvolutionDef>();

        /// <summary>보갑을 열 때 깨어나는 절기(없으면 null).</summary>
        public EvolutionDef? AwakenOnChest(int playerLevel, string mode = "normal")
            => _data.Balance.Evolution.Triggers.Contains("chest") ? Awakenable(playerLevel, mode).FirstOrDefault() : null;

        /// <summary>fromLevel→toLevel 사이 돌파로 그릇이 늘었으면, 그 순간 깨어날 수 있는 절기 후보.</summary>
        public List<EvolutionDef> AwakenOnBreakthrough(int fromLevel, int toLevel, string mode = "normal")
        {
            if (!_data.Balance.Evolution.Triggers.Contains("breakthrough")) return new List<EvolutionDef>();
            if (Vessels(toLevel, mode) <= Vessels(fromLevel, mode)) return new List<EvolutionDef>();
            return Awakenable(toLevel, mode);
        }

        public void Evolve(EvolutionDef evo) => _evolved.Add(evo.Weapon);

        /// <summary>판정용 무공 레벨: 절기가 된 무공은 최고 단계로 친다(BuildLevels 와 같은 셈). 없으면 0.</summary>
        int LinkLevel(string id) =>
            !_weapons.TryGetValue(id, out var lv) ? 0 : _evolved.Contains(id) ? Math.Max(lv, WeaponMax) : lv;

        /// <summary>지금 뜰 돈오 카드(연환 데이터 순서). 두 무공이 minLevel 이상인데 아직 안 깨친 연환, 깨쳤고 둘 다 doneoGrandLevel 이상인데
        /// 아직 대성 안 한 연환. 장경각에서 복원 안 한 비급 연환은 뜨지 않는다(카드엔 '?'로 남음). restored 가 null 이면 비급도 모두 열림.
        /// 게임은 레벨업 화면이 열릴 때 이것을 레벨업 카드와 따로 보이고, 집은 것을 TakeDoneo 로 넘긴다.</summary>
        public List<DoneoCard> PendingDoneo(IReadOnlyCollection<string>? restored = null)
        {
            int grandLevel = _data.Balance.LevelUp.DoneoGrandLevel;
            var list = new List<DoneoCard>();
            foreach (var l in _data.Synergies.Links)
            {
                if (l.IsSecret && restored != null && !restored.Contains(l.ManualId)) continue;
                int a = LinkLevel(l.A), b = LinkLevel(l.B);
                if (!_links.Contains(l.Id))
                {
                    if (a >= l.MinLevel && b >= l.MinLevel) list.Add(new DoneoCard(l, false));
                }
                else if (grandLevel > 0 && !_grandLinks.Contains(l.Id) && a >= grandLevel && b >= grandLevel)
                    list.Add(new DoneoCard(l, true));
            }
            return list;
        }

        /// <summary>돈오 카드를 집는다: 연환을 켜거나 대성으로 올린다. 조건이 안 맞는 카드는 예외.</summary>
        public void TakeDoneo(DoneoCard card)
        {
            var l = card.Link;
            int a = LinkLevel(l.A), b = LinkLevel(l.B);
            if (!card.Grand)
            {
                if (a < l.MinLevel || b < l.MinLevel) throw new InvalidOperationException($"연환 조건이 안 됩니다: {l.Id}");
                _links.Add(l.Id);
            }
            else
            {
                int g = _data.Balance.LevelUp.DoneoGrandLevel;
                if (!_links.Contains(l.Id)) throw new InvalidOperationException($"아직 깨치지 않은 연환입니다: {l.Id}");
                if (g <= 0 || a < g || b < g) throw new InvalidOperationException($"대성 조건이 안 됩니다: {l.Id}");
                _grandLinks.Add(l.Id);
            }
        }

        /// <summary>뜬 돈오 카드를 모두 집는다(시뮬레이터·시험용, 플레이어는 늘 집는다고 본다). 집은 수.</summary>
        public int TakeAllDoneo(IReadOnlyCollection<string>? restored = null)
        {
            var cards = PendingDoneo(restored);
            foreach (var c in cards) TakeDoneo(c);
            return cards.Count;
        }
    }
}
