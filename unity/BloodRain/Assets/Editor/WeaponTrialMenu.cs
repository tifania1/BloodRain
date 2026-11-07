using BloodRain.Core.Build;
using BloodRain.Presentation;
using BloodRain.Presentation.Combat;
using UnityEditor;
using UnityEngine;

namespace BloodRain.Editor
{
    // R-12U 무공 일곱 시험: 플레이 중 한 번 누르면 그 무공을 익히고(있으면 +1 레벨), 한 생 Loadout 에도 같이 넣는다.
    // Lv1·Lv5·Lv8 을 보려면 1번·5번·8번 누른다. 메뉴 이름은 weapons.json id.
    public static class WeaponTrialMenu
    {
        const string Root = "BloodRain/Test/무공 넣기(플레이 중)/";

        static void Add(string id)
        {
            var runner = LifeRunner.Instance;
            var weapons = runner != null && runner.weapons != null ? runner.weapons : Object.FindFirstObjectByType<WeaponRunner>();
            if (weapons == null) { Debug.LogWarning("[WeaponTrialMenu] Run 씬 플레이 중에만 됩니다"); return; }
            int max = DataBoot.Data.Balance.Levels.WeaponMax;
            var loadout = runner != null ? runner.Life?.Loadout : null;
            if (loadout != null && loadout.Weapons.TryGetValue(id, out var cur) && cur >= max) { Debug.Log($"[WeaponTrialMenu] {id} 은 이미 Lv{max}"); return; }
            loadout?.Apply(new Offer(loadout.Weapons.ContainsKey(id) ? OfferKind.UpgradeWeapon : OfferKind.NewWeapon, id));
            weapons.Learn(id);
            int lv = 0;
            foreach (var s in weapons.slots) if (s.id == id) lv = s.level;
            Debug.Log($"[WeaponTrialMenu] {id} Lv{lv}");
        }

        static bool Playing() => Application.isPlaying;

        [MenuItem(Root + "drunken_staff")] static void Staff() => Add("drunken_staff");
        [MenuItem(Root + "drunken_staff", true)] static bool StaffV() => Playing();
        [MenuItem(Root + "taiji_sword")] static void Taiji() => Add("taiji_sword");
        [MenuItem(Root + "taiji_sword", true)] static bool TaijiV() => Playing();
        [MenuItem(Root + "chain_sickle")] static void Chain() => Add("chain_sickle");
        [MenuItem(Root + "chain_sickle", true)] static bool ChainV() => Playing();
        [MenuItem(Root + "blood_claw")] static void Claw() => Add("blood_claw");
        [MenuItem(Root + "blood_claw", true)] static bool ClawV() => Playing();
        [MenuItem(Root + "sonic_qin")] static void Qin() => Add("sonic_qin");
        [MenuItem(Root + "sonic_qin", true)] static bool QinV() => Playing();
        [MenuItem(Root + "pear_spear")] static void Spear() => Add("pear_spear");
        [MenuItem(Root + "pear_spear", true)] static bool SpearV() => Playing();
        [MenuItem(Root + "falling_petals")] static void Petal() => Add("falling_petals");
        [MenuItem(Root + "falling_petals", true)] static bool PetalV() => Playing();
    }
}
