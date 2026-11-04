using BloodRain.Presentation;
using UnityEditor;
using UnityEngine;

namespace BloodRain.Editor
{
    // 시험용 언어 바꾸기(L-01b2). PlayerPrefs `lang` 을 바꾼다. 화면을 다 새로 만들려면 플레이를 다시 켠다.
    public static class LangMenu
    {
        const string Root = "BloodRain/Test/언어 바꾸기/";

        [MenuItem(Root + "ko")] static void Ko() => Set("ko");
        [MenuItem(Root + "en")] static void En() => Set("en");
        [MenuItem(Root + "zh-Hans")] static void ZhHans() => Set("zh-Hans");
        [MenuItem(Root + "zh-Hant")] static void ZhHant() => Set("zh-Hant");

        static void Set(string lang)
        {
            Loc.Lang = lang;
            PlayerPrefs.Save();
            Debug.Log($"[LangMenu] 언어 = {lang}");
        }
    }
}
