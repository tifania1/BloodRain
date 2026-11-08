using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Presentation;
using BloodRain.Presentation.Combat;
using BloodRain.Presentation.Hud;
using UnityEditor;
using UnityEngine;

namespace BloodRain.Editor
{
    // LK-01Ua 연환 시험(플레이 중). '두 무공 Lv N' 은 연환 짝 두 무공을 그 레벨까지 올린다(Lv3 이면 꺼진 채, Lv4 면 켜짐).
    // '비급 다 복원(이 판만)' 은 레벨업 화면이 쓰는 복원 목록을 모든 비급으로 바꾼다(혼 저장은 그대로).
    // '치명 확률 +100%(이 판만)' 은 EnemyField.testCritBonus 를 1 로(암기연환 확인용).
    public static class LinkTestMenu
    {
        const string Root = "BloodRain/Test/연환 시험(플레이 중)/";

        static bool Playing() => Application.isPlaying;

        public static string Raise(string linkId, int level)
        {
            var runner = LifeRunner.Instance;
            var weapons = runner != null && runner.weapons != null ? runner.weapons : Object.FindFirstObjectByType<WeaponRunner>();
            var lo = runner != null ? runner.Life?.Loadout : null;
            if (weapons == null || lo == null) { Debug.LogWarning("[연환 시험] Run 씬 플레이 중에만 됩니다"); return "no life"; }
            var link = DataBoot.Data.Synergies.Links.First(l => l.Id == linkId);
            foreach (var id in new[] { link.A, link.B })
            {
                if (!lo.Weapons.ContainsKey(id) && lo.Weapons.Count >= lo.WeaponSlots) lo.AddExtraSlot(true);
                while (!lo.Weapons.TryGetValue(id, out var cur) || cur < level)
                {
                    lo.Apply(new Offer(lo.Weapons.ContainsKey(id) ? OfferKind.UpgradeWeapon : OfferKind.NewWeapon, id));
                    weapons.Learn(id);
                }
            }
            var ls = Object.FindFirstObjectByType<LevelUpScreen>();
            bool on = ls != null && ls.Synergy.LinkParam(linkId, link.Params.Keys.First()) != null;
            var msg = $"[연환 시험] {link.NameKo}: {link.A} Lv{lo.Weapons[link.A]} · {link.B} Lv{lo.Weapons[link.B]} · 켜짐 {on}";
            Debug.Log(msg);
            return msg;
        }

        public static string RestoreAll()
        {
            var ls = Object.FindFirstObjectByType<LevelUpScreen>();
            if (ls == null) return "no levelup";
            var all = DataBoot.Data.Synergies.SecretManualIds.ToList();
            typeof(LevelUpScreen).GetField("_restored", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(ls, all);
            var msg = $"[연환 시험] 비급 {all.Count}권 복원(이 판만)";
            Debug.Log(msg);
            return msg;
        }

        public static string CritOn()
        {
            var ef = Object.FindFirstObjectByType<EnemyField>();
            if (ef == null) return "no enemies";
            ef.testCritBonus = 1f;
            Debug.Log("[연환 시험] 치명 확률 +100%");
            return "crit on";
        }

        [MenuItem(Root + "비급 다 복원(이 판만)")] static void Restore() => RestoreAll();
        [MenuItem(Root + "비급 다 복원(이 판만)", true)] static bool RestoreV() => Playing();
        [MenuItem(Root + "치명 확률 +100%(이 판만)")] static void Crit() => CritOn();
        [MenuItem(Root + "치명 확률 +100%(이 판만)", true)] static bool CritV() => Playing();

        [MenuItem(Root + "쌍검합벽 Lv4")] static void Ssang() => Raise("ssanggeom", 4);
        [MenuItem(Root + "쌍검합벽 Lv4", true)] static bool SsangV() => Playing();
        [MenuItem(Root + "권장합일 Lv4")] static void Gwon() => Raise("gwonjang", 4);
        [MenuItem(Root + "권장합일 Lv4", true)] static bool GwonV() => Playing();
        [MenuItem(Root + "독비 Lv4")] static void Dok() => Raise("dokbi", 4);
        [MenuItem(Root + "독비 Lv4", true)] static bool DokV() => Playing();
        [MenuItem(Root + "암기연환 Lv4")] static void Amgi() => Raise("amgi", 4);
        [MenuItem(Root + "암기연환 Lv4", true)] static bool AmgiV() => Playing();
        [MenuItem(Root + "풍화 Lv4")] static void Pung() => Raise("punghwa", 4);
        [MenuItem(Root + "풍화 Lv4", true)] static bool PungV() => Playing();
        // LK-01Ub
        [MenuItem(Root + "검화 Lv4")] static void Geomhwa() => Raise("geomhwa", 4);
        [MenuItem(Root + "검화 Lv4", true)] static bool GeomhwaV() => Playing();
        [MenuItem(Root + "금강후 Lv4")] static void Geumgang() => Raise("geumganghu", 4);
        [MenuItem(Root + "금강후 Lv4", true)] static bool GeumgangV() => Playing();
        [MenuItem(Root + "점혈창 Lv4")] static void Jeomhyeol() => Raise("jeomhyeol", 4);
        [MenuItem(Root + "점혈창 Lv4", true)] static bool JeomhyeolV() => Playing();
        [MenuItem(Root + "뇌화 Lv4")] static void Noehwa() => Raise("noehwa", 4);
        [MenuItem(Root + "뇌화 Lv4", true)] static bool NoehwaV() => Playing();
        [MenuItem(Root + "혈독 Lv4")] static void Hyeoldok() => Raise("hyeoldok", 4);
        [MenuItem(Root + "혈독 Lv4", true)] static bool HyeoldokV() => Playing();
        [MenuItem(Root + "취중매화 Lv4")] static void Chwijung() => Raise("chwijung", 4);
        [MenuItem(Root + "취중매화 Lv4", true)] static bool ChwijungV() => Playing();
        [MenuItem(Root + "혈창 Lv4")] static void Hyeolchang() => Raise("hyeolchang", 4);
        [MenuItem(Root + "혈창 Lv4", true)] static bool HyeolchangV() => Playing();
    }
}
