#nullable enable
// 전투 HUD 왼쪽 위 칸(기획서 12장 '전투 HUD 배치'): 공격 무공 6칸 + 심법 6칸, 절기 문턱에 선 무공은 테두리가 금빛으로 뛰고
// (그릇이 없으면 흐림), 경지 아래에 절기 그릇 ◆◇. 일시정지 '절기 문턱·그릇 표시'도 같은 셈을 쓴다.
// 웹 프로토타입 renderSlots 와 같은 뜻: 문턱 = Loadout.AtThreshold, 그릇 = Loadout.Vessels(지금 레벨) 대 깨운 수.
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;

namespace BloodRain.Core.Build
{
    public enum SlotGlow
    {
        /// <summary>보통 칸.</summary>
        None,
        /// <summary>문턱에 섰고 그릇이 남음 — 다음 보갑에서 깨어난다(금빛으로 뜀).</summary>
        Ready,
        /// <summary>문턱에 섰지만 그릇이 없음(흐린 금빛) — 다음 그릇이 생기는 돌파를 기다린다.</summary>
        Waiting,
        /// <summary>이미 절기.</summary>
        Evolved,
    }

    public readonly struct HudSlot
    {
        /// <summary>칸 주인 id. 빈 칸이면 null.</summary>
        public readonly string? Id;
        public readonly int Level;
        public readonly SlotGlow Glow;
        public HudSlot(string? id, int level, SlotGlow glow) { Id = id; Level = level; Glow = glow; }
    }

    public sealed class HudSlotsView
    {
        public readonly List<HudSlot> Weapons = new List<HudSlot>();
        public readonly List<HudSlot> Passives = new List<HudSlot>();
        /// <summary>그릇 ◆ 수(깨운 절기 수, 지금 그릇 수를 넘지 않음).</summary>
        public int VesselsUsed;
        /// <summary>그릇 전체(◆ + ◇) = 지금 경지의 그릇 수.</summary>
        public int VesselsTotal;
    }

    public static class HudSlots
    {
        /// <summary>칸 상태. 무공·심법은 Loadout 차례대로, 칸 수만큼 채우고 남는 자리는 빈 칸.</summary>
        public static HudSlotsView Build(Loadout loadout, int playerLevel, string mode = "normal")
        {
            var view = new HudSlotsView();
            int vessels = loadout.Vessels(playerLevel, mode);
            int used = loadout.Evolved.Count;
            bool spare = used < vessels;
            var threshold = new HashSet<string>(loadout.AtThreshold().Select(e => e.Weapon));
            foreach (var kv in loadout.Weapons)
            {
                var glow = loadout.Evolved.Contains(kv.Key) ? SlotGlow.Evolved
                         : threshold.Contains(kv.Key) ? (spare ? SlotGlow.Ready : SlotGlow.Waiting)
                         : SlotGlow.None;
                view.Weapons.Add(new HudSlot(kv.Key, kv.Value, glow));
            }
            for (int i = view.Weapons.Count; i < loadout.WeaponSlots; i++) view.Weapons.Add(new HudSlot(null, 0, SlotGlow.None));
            foreach (var kv in loadout.Passives) view.Passives.Add(new HudSlot(kv.Key, kv.Value, SlotGlow.None));
            for (int i = view.Passives.Count; i < loadout.PassiveSlots; i++) view.Passives.Add(new HudSlot(null, 0, SlotGlow.None));
            view.VesselsTotal = vessels;
            view.VesselsUsed = System.Math.Min(used, vessels);
            return view;
        }
    }
}
