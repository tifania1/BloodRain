using TMPro;
using UnityEngine;

namespace BloodRain.Presentation
{
    // 언어별 대체 글꼴 순서(L-08b). 간체(SC)와 번체(TC)는 같은 코드포인트가 많아서,
    // 본문 KR 글꼴에 없는 글자를 어느 쪽에서 먼저 찾느냐로 자형이 갈린다.
    // zh-Hant 면 TC 를, 그 밖이면 SC 를 둘 중 앞자리에 둔다(다른 대체 글꼴 자리는 그대로). KR 에 있는 한자는 그대로 KR 자형.
    // Loc 가 언어를 읽거나 바꿀 때 부른다. 메모리에 올라온 글꼴 에셋 중 SC·TC 를 둘 다 대체로 가진 것만 고친다.
    public static class FontFallback
    {
        const string ScName = "NotoSerifSC SDF";
        const string TcName = "NotoSerifTC SDF";

        public static void Apply(string lang)
        {
            bool hant = lang == "zh-Hant";
            foreach (var fa in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            {
                var list = fa.fallbackFontAssetTable;
                if (list == null) continue;
                var sc = list.Find(f => f != null && f.name == ScName);
                var tc = list.Find(f => f != null && f.name == TcName);
                if (sc == null || tc == null) continue;
                int a = list.IndexOf(sc), b = list.IndexOf(tc);
                int front = Mathf.Min(a, b), back = Mathf.Max(a, b);
                var first = hant ? tc : sc;
                if (list[front] == first) continue;
                list[front] = first;
                list[back] = hant ? sc : tc;
                TMPro_EventManager.ON_FONT_PROPERTY_CHANGED(true, fa);
            }
        }
    }
}
