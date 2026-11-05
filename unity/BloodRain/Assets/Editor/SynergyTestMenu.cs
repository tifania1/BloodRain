using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Presentation;
using BloodRain.Presentation.Combat;
using UnityEditor;
using UnityEngine;

namespace BloodRain.Editor
{
    /// <summary>C-06b 시험 메뉴. 플레이 중 한 생의 행낭에 그 문파 무공·심법(synergies.json members)을 다 넣어 진전 단계를 끝까지 채운다.
    /// 무공 칸·심법 칸이 모자라면 한 칸씩 늘린다. 능력치·진전 효과는 LifeRunner 가 0.25초 안에 다시 매긴다.</summary>
    public static class SynergyTestMenu
    {
        const string Root = "BloodRain/Test/진전 채우기(플레이 중)/";

        [MenuItem(Root + "소림")] static void Shaolin() => Fill("shaolin");
        [MenuItem(Root + "무당")] static void Wudang() => Fill("wudang");
        [MenuItem(Root + "화산")] static void Huashan() => Fill("huashan");
        [MenuItem(Root + "개방")] static void Gaebang() => Fill("gaebang");
        [MenuItem(Root + "당가")] static void Tang() => Fill("tang");
        [MenuItem(Root + "남궁세가")] static void Namgung() => Fill("namgung");
        [MenuItem(Root + "장강수로채")] static void Waterfort() => Fill("waterfort");
        [MenuItem(Root + "곤륜")] static void Kunlun() => Fill("kunlun");
        [MenuItem(Root + "마도")] static void Demonic() => Fill("demonic");

        [MenuItem(Root + "소림", true)]
        static bool Valid() => Application.isPlaying;

        public static string Fill(string schoolId)
        {
            var lr = LifeRunner.Instance;
            if (lr == null || lr.Life == null) { Debug.LogWarning("[진전 채우기] Run 씬 플레이 중에만 됩니다"); return "no life"; }
            var data = DataBoot.Data;
            var school = data.Synergies.Schools.First(s => s.Id == schoolId);
            var lo = lr.Life.Loadout;
            var wr = Object.FindFirstObjectByType<WeaponRunner>();
            foreach (var id in school.Members)
            {
                bool weapon = data.Weapons.Any(w => w.Id == id);
                if (weapon)
                {
                    if (lo.Weapons.ContainsKey(id)) continue;
                    if (lo.Weapons.Count >= lo.WeaponSlots) lo.AddExtraSlot(true);
                    lo.Apply(new Offer(OfferKind.NewWeapon, id));
                    if (wr != null) wr.Learn(id);
                }
                else
                {
                    if (lo.Passives.ContainsKey(id)) continue;
                    if (lo.Passives.Count >= lo.PassiveSlots) lo.AddExtraSlot(false);
                    lo.Apply(new Offer(OfferKind.NewPassive, id));
                }
            }
            var st = Synergy.Compute(data, lo, null, BloodRain.Presentation.Meta.RebirthHandoff.Soul?.RestoredManuals).School(schoolId);
            var msg = $"[진전 채우기] {school.NameKo} {st.Count}개 · 켜진 단계 {st.TierCount} · 잠긴 단계 {st.LockedTiers.Count}";
            Debug.Log(msg);
            return msg;
        }
    }
}
