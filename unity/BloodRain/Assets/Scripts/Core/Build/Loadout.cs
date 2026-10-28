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
        public Offer(OfferKind kind, string id) { Kind = kind; Id = id; }
        public override string ToString() => $"{Kind}:{Id}";
    }

    public sealed class Loadout
    {
        readonly GameData _data;
        readonly Dictionary<string, int> _weapons = new Dictionary<string, int>();
        readonly Dictionary<string, int> _passives = new Dictionary<string, int>();
        readonly HashSet<string> _evolved = new HashSet<string>();
        readonly HashSet<string> _banished = new HashSet<string>();

        public int WeaponSlots { get; private set; }
        public int PassiveSlots { get; private set; }
        public YinYangGauge Gauge { get; }

        public IReadOnlyDictionary<string, int> Weapons => _weapons;
        public IReadOnlyDictionary<string, int> Passives => _passives;
        public IReadOnlyCollection<string> Evolved => _evolved;

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

        public void Apply(Offer offer)
        {
            switch (offer.Kind)
            {
                case OfferKind.NewWeapon:
                    if (_weapons.Count >= WeaponSlots) throw new InvalidOperationException("무공 칸이 가득 찼습니다");
                    _weapons[offer.Id] = 1;
                    Gauge.OnWeaponLevelUp(_data.Weapon(offer.Id));
                    break;
                case OfferKind.UpgradeWeapon:
                    if (_weapons[offer.Id] >= WeaponMax) throw new InvalidOperationException("이미 최고 단계입니다");
                    _weapons[offer.Id]++;
                    Gauge.OnWeaponLevelUp(_data.Weapon(offer.Id));
                    break;
                case OfferKind.NewPassive:
                    if (_passives.Count >= PassiveSlots) throw new InvalidOperationException("심법 칸이 가득 찼습니다");
                    _passives[offer.Id] = 1;
                    Gauge.OnPassiveLevelUp(_data.Passive(offer.Id));
                    break;
                case OfferKind.UpgradePassive:
                    if (_passives[offer.Id] >= PassiveMax) throw new InvalidOperationException("이미 최고 단계입니다");
                    _passives[offer.Id]++;
                    Gauge.OnPassiveLevelUp(_data.Passive(offer.Id));
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

        /// <summary>레벨업 선택지 3개(기연이 높으면 4개). 고를 것이 없으면 은자·음식(Fallback).</summary>
        public List<Offer> RollLevelUp(Random rng, float luck, ISet<string> unlocked)
        {
            var rule = _data.Balance.LevelUp;
            int count = rule.BaseChoices;
            if (count < rule.MaxChoices && rng.NextDouble() < Math.Min(1.0, luck / rule.LuckForFourthChoice)) count++;

            var pool = Candidates(unlocked);
            var result = new List<Offer>();
            while (result.Count < count && pool.Count > 0)
            {
                int i = rng.Next(pool.Count);
                result.Add(pool[i]);
                pool.RemoveAt(i);
            }
            if (result.Count == 0) result.Add(new Offer(OfferKind.Fallback, "silver_or_food"));
            return result;
        }

        /// <summary>절기 그릇: 지금 경지에서 몸에 담을 수 있는 절기 수. 현경처럼 모드에 따라 막힌 경지는 세지 않는다.</summary>
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
    }
}
