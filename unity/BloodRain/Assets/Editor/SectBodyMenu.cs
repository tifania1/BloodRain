using BloodRain.Core.Build;
using BloodRain.Core.Meta;
using BloodRain.Presentation;
using BloodRain.Presentation.Meta;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BloodRain.Editor
{
    // R-04Ua 시험: 침방 없이 그 문파 몸(성질 없음)으로 Run 을 다시 연다. 플레이 중에만.
    //   혼은 지금 혼 그대로(없으면 저장된 혼). 문파 고유 능력(회피·회복 배율·태극 쾌속 …)을 바로 보려고 쓴다.
    public static class SectBodyMenu
    {
        const string Root = "BloodRain/Test/몸 바꾸기(문파)/";

        [MenuItem(Root + "소림")] static void Shaolin() => Switch("shaolin");
        [MenuItem(Root + "개방")] static void Gaebang() => Switch("gaebang");
        [MenuItem(Root + "남궁")] static void Namgung() => Switch("namgung");
        [MenuItem(Root + "당가")] static void Tang() => Switch("tang");
        [MenuItem(Root + "수로채")] static void Waterfort() => Switch("waterfort");
        [MenuItem(Root + "무당")] static void Wudang() => Switch("wudang");
        [MenuItem(Root + "화산")] static void Huashan() => Switch("huashan");
        [MenuItem(Root + "곤륜")] static void Kunlun() => Switch("kunlun");
        [MenuItem(Root + "마교")] static void Demonic() => Switch("demonic");

        [MenuItem(Root + "소림", true)] [MenuItem(Root + "개방", true)] [MenuItem(Root + "남궁", true)]
        [MenuItem(Root + "당가", true)] [MenuItem(Root + "수로채", true)] [MenuItem(Root + "무당", true)]
        [MenuItem(Root + "화산", true)] [MenuItem(Root + "곤륜", true)] [MenuItem(Root + "마교", true)]
        static bool Valid() => Application.isPlaying;

        public static void Switch(string sectId)
        {
            if (!Application.isPlaying) { Debug.LogWarning("[SectBodyMenu] 플레이 중에만 됩니다"); return; }
            var data = DataBoot.Data;
            var c = data.Cheonha;
            var sect = c.Sect(sectId);
            var cand = new RebirthCandidate(sect, c.Region(sect.Region), new System.Collections.Generic.List<BloodRain.Core.Data.BirthTraitDef>());
            if (RebirthHandoff.Soul == null) RebirthHandoff.Soul = SoulStore.Load(data);
            RebirthHandoff.ClearBody();
            RebirthHandoff.Body = Rebirth.Body(c, cand);
            Time.timeScale = 1f;
            Debug.Log($"[SectBodyMenu] {sect.NameKo} 몸으로 Run 다시 엶");
            SceneManager.LoadScene("Run");
        }
    }
}
