#nullable enable
// 레벨업 옆 칸과 절기 조합표(기획서 12장 '화면 편의 규칙'). 웹 프로토타입 cardLights·evoTableHtml 과 같은 셈.
//   옆 칸: 고르려는 카드가 켜는 칸 — 레벨이 오르는 칸, 새로 들어갈 빈 칸, 절기 짝이 되는 가진 칸.
//   조합표: 한 번 깨어나 본 절기만(계정 기록, 장경각처럼) 이름과 재료를 보이고, 가진 재료는 굵게,
//           이번 생에 더는 못 맞추는 조합(재료가 없는데 그쪽 칸이 다 참, 또는 이미 깨어남)은 흐리게.
//   카드 글(12장 '레벨업 카드 규칙'): 첫째 줄 Lv a→b·'새', 둘째 줄 바뀌는 수치(무공 피해 a→b·이정표, 심법 효과),
//           배지 중 절기 짝·절기 문턱. 진전·연환 배지는 Synergy.Preview. 웹 offerCard 와 같은 셈.
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;

namespace BloodRain.Core.Build
{
    /// <summary>빛낼 칸 하나. Id 가 null 이면 그 줄의 첫 빈 칸(새로 들어갈 자리).</summary>
    public readonly struct SlotLight
    {
        public readonly bool Weapon;
        public readonly string? Id;
        public SlotLight(bool weapon, string? id) { Weapon = weapon; Id = id; }
        public override string ToString() => (Weapon ? "w:" : "p:") + (Id ?? "+");
    }

    public sealed class EvolutionRow
    {
        public EvolutionDef Evolution = null!;
        public int WeaponLevel;   // 0 = 없음
        public int PassiveLevel;  // 0 = 없음
        public bool Evolved;
        /// <summary>이번 생에 더는 못 맞춤(흐리게).</summary>
        public bool Dead;
        public bool HasAny => WeaponLevel > 0 || PassiveLevel > 0;
    }

    /// <summary>레벨업 카드 한 장의 글 재료(문장은 Presentation 이 strings 로 만든다).</summary>
    public sealed class CardText
    {
        public bool Weapon;
        /// <summary>처음 얻는 것('새 무공'·'새 심법').</summary>
        public bool IsNew;
        public int FromLevel;   // 새 것이면 0
        public int ToLevel;
        /// <summary>무공: 지금 피해(새 것이면 0)와 고른 뒤 피해(Lv1→Lv8 선형, 웹 lerp 와 같음).</summary>
        public float DamageFrom, DamageTo;
        /// <summary>무공: 고른 레벨의 이정표 글(weapons.json milestones), 없으면 null.</summary>
        public string? Milestone;
        /// <summary>심법: 레벨마다 오르는 효과(passives.json effects).</summary>
        public List<PassiveEffect> Effects = new List<PassiveEffect>();
        /// <summary>절기 짝 배지 — 무공이면 짝 심법, 심법이면 짝 무공(그 무공이 이번 생 풀에 있을 때만).</summary>
        public EvolutionDef? Pair;
        /// <summary>이걸 고르면 절기 문턱에 선다(무공이 최고 단계 + 짝 심법 Lv 문턱).</summary>
        public EvolutionDef? Threshold;
    }

    public static class LevelUpView
    {
        /// <summary>카드 글 재료. 은자·음식 카드(Fallback)는 null. weaponPool 은 이번 생 레벨업에 나오는 무공(null 이면 전부) — 심법의 짝 배지에만 쓴다.</summary>
        public static CardText? Card(GameData data, Loadout loadout, Offer offer, ICollection<string>? weaponPool = null)
        {
            int weaponMax = data.Balance.Levels.WeaponMax, pairMin = data.Balance.Evolution.PairPassiveMinLevel;
            switch (offer.Kind)
            {
                case OfferKind.NewWeapon:
                case OfferKind.UpgradeWeapon:
                {
                    var def = data.Weapons.First(w => w.Id == offer.Id);
                    loadout.Weapons.TryGetValue(offer.Id, out var lv);
                    var evo = data.Evolutions.FirstOrDefault(e => e.Weapon == offer.Id);
                    loadout.Passives.TryGetValue(evo?.Passive ?? "", out var pairLv);
                    string? ms = null;
                    def.Milestones?.TryGetValue((lv + 1).ToString(), out ms);
                    return new CardText
                    {
                        Weapon = true, IsNew = lv == 0, FromLevel = lv, ToLevel = lv + 1,
                        DamageFrom = lv == 0 ? 0 : Combat.WeaponMath.DamageAt(def, lv, weaponMax),
                        DamageTo = Combat.WeaponMath.DamageAt(def, lv + 1, weaponMax),
                        Milestone = ms,
                        Pair = evo,
                        Threshold = offer.Kind == OfferKind.UpgradeWeapon && evo != null && lv + 1 >= weaponMax && pairLv >= pairMin ? evo : null,
                    };
                }
                case OfferKind.NewPassive:
                case OfferKind.UpgradePassive:
                {
                    var def = data.Passives.First(p => p.Id == offer.Id);
                    loadout.Passives.TryGetValue(offer.Id, out var lv);
                    var evo = data.Evolutions.FirstOrDefault(e => e.Passive == offer.Id);
                    loadout.Weapons.TryGetValue(evo?.Weapon ?? "", out var wl);
                    bool inPool = evo != null && (weaponPool == null || weaponPool.Contains(evo.Weapon));
                    return new CardText
                    {
                        Weapon = false, IsNew = lv == 0, FromLevel = lv, ToLevel = lv + 1,
                        Effects = def.Effects,
                        Pair = inPool ? evo : null,
                        Threshold = evo != null && wl >= weaponMax && !loadout.Evolved.Contains(evo.Weapon) && lv + 1 == pairMin ? evo : null,
                    };
                }
                default: return null;
            }
        }

        /// <summary>심법 효과 한 줄의 값 글(능력치 이름 뒤에 붙임). 웹 STAT_TEXT 와 같은 셈 —
        /// 재사용 대기시간 '-n%', 방어 '-n', 개수 '+1', 흡혈 소수 한 자리 %, 행운·회복은 그냥 수, 나머지 '+n%'.</summary>
        public static string EffectValue(PassiveEffect e)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            string Pct(float v) => System.Math.Round(v * 100, System.MidpointRounding.AwayFromZero).ToString("0", inv) + "%";
            string Num(float v) => v.ToString("0.##", inv);
            switch (e.Stat)
            {
                case "CooldownReduction": return "-" + Pct(e.PerLevel);
                case "Armor": return "-" + Num(e.PerLevel);
                case "Amount": return "+1";
                case "Lifesteal": return "+" + (e.PerLevel * 100).ToString("0.0", inv) + "%";
                case "Luck":
                case "Recovery": return "+" + Num(e.PerLevel);
                default: return "+" + Pct(e.PerLevel);
            }
        }

        /// <summary>카드 한 장이 켜는 칸. 절기 짝은 지금 가진 것만(없는 칸은 빛낼 데가 없다).</summary>
        public static List<SlotLight> CardLights(GameData data, Loadout loadout, Offer offer)
        {
            var list = new List<SlotLight>();
            switch (offer.Kind)
            {
                case OfferKind.UpgradeWeapon: list.Add(new SlotLight(true, offer.Id)); break;
                case OfferKind.NewWeapon: list.Add(new SlotLight(true, null)); break;
                case OfferKind.UpgradePassive: list.Add(new SlotLight(false, offer.Id)); break;
                case OfferKind.NewPassive: list.Add(new SlotLight(false, null)); break;
                default: return list;
            }
            bool weapon = offer.Kind == OfferKind.UpgradeWeapon || offer.Kind == OfferKind.NewWeapon;
            var evo = weapon ? data.Evolutions.FirstOrDefault(e => e.Weapon == offer.Id) : data.Evolutions.FirstOrDefault(e => e.Passive == offer.Id);
            if (evo != null)
            {
                if (weapon && loadout.Passives.ContainsKey(evo.Passive)) list.Add(new SlotLight(false, evo.Passive));
                if (!weapon && loadout.Weapons.ContainsKey(evo.Weapon)) list.Add(new SlotLight(true, evo.Weapon));
            }
            return list;
        }

        /// <summary>조합표 줄(찾은 절기만, evolutions.json 차례). 재료 무공·심법이 데이터에 없는 절기는 뺀다. 두 번째 값은 전체 수.</summary>
        public static (List<EvolutionRow> rows, int total) EvolutionTable(GameData data, Loadout loadout, ICollection<string> seenEvolutions)
        {
            var weapons = new HashSet<string>(data.Weapons.Select(w => w.Id));
            var passives = new HashSet<string>(data.Passives.Select(p => p.Id));
            var all = data.Evolutions.Where(e => weapons.Contains(e.Weapon) && passives.Contains(e.Passive)).ToList();
            var rows = new List<EvolutionRow>();
            foreach (var e in all)
            {
                if (!seenEvolutions.Contains(e.Id)) continue;
                loadout.Weapons.TryGetValue(e.Weapon, out var wl);
                loadout.Passives.TryGetValue(e.Passive, out var pl);
                bool evolved = wl > 0 && loadout.Evolved.Contains(e.Weapon);
                bool dead = (wl == 0 && loadout.Weapons.Count >= loadout.WeaponSlots)
                         || (pl == 0 && loadout.Passives.Count >= loadout.PassiveSlots)
                         || evolved;
                rows.Add(new EvolutionRow { Evolution = e, WeaponLevel = wl, PassiveLevel = pl, Evolved = evolved, Dead = dead });
            }
            return (rows, all.Count);
        }
    }
}
