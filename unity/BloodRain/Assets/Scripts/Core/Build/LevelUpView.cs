#nullable enable
// 레벨업 옆 칸과 절기 조합표(기획서 12장 '화면 편의 규칙'). 웹 프로토타입 cardLights·evoTableHtml 과 같은 셈.
//   옆 칸: 고르려는 카드가 켜는 칸 — 레벨이 오르는 칸, 새로 들어갈 빈 칸, 절기 짝이 되는 가진 칸.
//   조합표: 한 번 깨어나 본 절기만(계정 기록, 장경각처럼) 이름과 재료를 보이고, 가진 재료는 굵게,
//           이번 생에 더는 못 맞추는 조합(재료가 없는데 그쪽 칸이 다 참, 또는 이미 깨어남)은 흐리게.
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

    public static class LevelUpView
    {
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
