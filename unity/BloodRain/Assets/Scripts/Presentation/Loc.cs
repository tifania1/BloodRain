using System.Collections.Generic;
using BloodRain.Core.Data;
using UnityEngine;

namespace BloodRain.Presentation
{
    // 화면 문구를 지금 언어로 꺼낸다. 원문·키 규칙은 Core/Data/Strings.cs, 표는 Resources/Data/strings/<lang>.json.
    // 언어는 PlayerPrefs 에 남고, 정한 적 없으면 기기 언어(한국어·간체·그 밖은 영어)로 시작한다. 설정 화면이 생기면 Lang 을 바꾼다.
    //   Loc.T("ui.levelup.none")                         UI 문구
    //   Loc.F("ui.levelup.skip", charges)                {0} 자리 채우기
    //   Loc.T($"weapons.{w.Id}.name", w.NameKo)           데이터 문구(옮긴 값이 없으면 넘긴 한국어)
    public static class Loc
    {
        const string PrefKey = "lang";
        const string Dir = DataBoot.ResourceDir + "/strings/";

        static Strings _strings;
        static StringCatalog _catalog;

        public static Strings Current => _strings ??= Load(Lang);

        public static string Lang
        {
            get => PlayerPrefs.GetString(PrefKey, FromSystem(Application.systemLanguage));
            set
            {
                PlayerPrefs.SetString(PrefKey, value);
                _strings = null;
            }
        }

        public static string T(string key) => Current.Get(key);
        public static string T(string key, string korean) => Current.Get(key, korean);
        public static string F(string key, params object[] args) => Current.Format(key, args);

        static string FromSystem(SystemLanguage l) =>
            l == SystemLanguage.Korean ? "ko"
            : l == SystemLanguage.ChineseSimplified || l == SystemLanguage.Chinese ? "zh-Hans"
            : "en";

        static Strings Load(string lang)
        {
            if (System.Array.IndexOf(Strings.Languages, lang) < 0) lang = "en";
            if (_catalog == null)
            {
                var files = new Dictionary<string, string>();
                foreach (var name in GameData.FileNames)
                {
                    var data = Resources.Load<TextAsset>($"{DataBoot.ResourceDir}/{name}");
                    if (data != null) files[name] = data.text;
                }
                var ko = Resources.Load<TextAsset>(Dir + StringCatalog.KoreanFile);
                _catalog = StringCatalog.FromData(files, ko != null ? ko.text : "{}");
            }
            var asset = lang == "ko" ? null : Resources.Load<TextAsset>(Dir + lang);
            if (lang != "ko" && asset == null) Debug.LogError($"[Loc] Resources/{Dir}{lang}.json 이 없다");
            return Strings.Load(lang, _catalog, asset != null ? asset.text : null);
        }

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() { _strings = null; _catalog = null; }
#endif
    }
}
