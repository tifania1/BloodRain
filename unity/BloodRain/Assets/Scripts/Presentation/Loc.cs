using System.Collections.Generic;
using BloodRain.Core.Data;
using UnityEngine;

namespace BloodRain.Presentation
{
    // 화면 문구를 지금 언어로 꺼낸다. 원문·키 규칙은 Core/Data/Strings.cs, 표는 Resources/Data/strings/<lang>.json.
    // 언어는 PlayerPrefs 에 남고, 정한 적 없으면 기기 언어(한국어·간체·번체·그 밖은 영어)로 시작한다. 설정 화면이 생기면 Lang 을 바꾼다.
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

        // ── 데이터 이름(키는 Core/Data/Strings.cs 규칙: 파일.경로.필드, 목록은 id). 화면 여럿이 같이 쓴다 ──
        public static string Sect(SectDef s) => T($"cheonha.sects.{s.Id}.name", s.NameKo);
        public static string Disciple(SectDef s) => F("ui.rebirth.disciple", Sect(s));
        public static string Region(RegionDef r) => T($"cheonha.regions.{r.Id}.name", r.NameKo);
        public static string SealStone(RegionDef r) => T($"cheonha.regions.{r.Id}.sealStone", r.SealStone);
        public static string Realm(RealmDef r) => T($"balance.realms.{r.Id}.name", r.NameKo);
        public static string Weapon(WeaponDef w) => T($"weapons.{w.Id}.name", w.NameKo);
        public static string Character(CharacterDef c) => T($"characters.{c.Id}.name", c.NameKo);
        public static string Boss(BossDef b) => T($"enemies.bosses.{b.Id}.name", b.NameKo);
        public static string Trait(BirthTraitDef t) => T($"cheonha.birthTraits.list.{t.Id}.name", t.NameKo);
        public static string Upgrade(RebirthUpgradeDef u) => T($"cheonha.rebirth.upgrades.{u.Id}.name", u.NameKo);
        /// <summary>능력치 이름(Might → 공력). 표에 없는 이름이면 그대로.</summary>
        // ui. 키는 한국어 원문이 strings/ko.json 에 있다. 두 인자 T(key, korean) 로 영어 키를 넘기면
        // 한국어 화면에 'Might' 가 그대로 나와서(2026-11-05 침방), 한 인자 T 로 원문까지 찾는다.
        public static string Stat(string stat) => T($"ui.stat.{stat}");
        /// <summary>이름 옆에 붙이는 한자. 중국어(간체·번체) 화면에선 이름이 이미 한자라 겹치니 뺀다.</summary>
        public static string Hanja(string hanja) => Current.Lang.StartsWith("zh") ? "" : hanja ?? "";

        static string FromSystem(SystemLanguage l) =>
            l == SystemLanguage.Korean ? "ko"
            : l == SystemLanguage.ChineseSimplified || l == SystemLanguage.Chinese ? "zh-Hans"
            : l == SystemLanguage.ChineseTraditional ? "zh-Hant"
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
            FontFallback.Apply(lang); // 간체·번체 대체 글꼴 순서(L-08b)
            return Strings.Load(lang, _catalog, asset != null ? asset.text : null);
        }

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() { _strings = null; _catalog = null; }
#endif
    }
}
