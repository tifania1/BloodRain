using TMPro;
using UnityEngine;

namespace BloodRain.Presentation
{
    // OnGUI(IMGUI) 글자에 한글 글꼴을 건다(WG-01Ub). 에디터는 IMGUI 기본 글꼴이 OS 글꼴로 한글을 대신 그려 주지만
    // WebGL 빌드는 내장 글꼴만 써서 한글이 통째로 빠졌다(보갑 목록 창이 'Lv2 · (Lv 35) · Enter' 만 보이던 것).
    // 이미 빌드에 들어가는 본문 TMP 글꼴(NotoSerifKR SDF, 동적)의 원본 Font 를 GUI.skin.font 로 쓴다.
    // GUIStyle.font 가 비어 있으면 skin 글꼴을 쓰므로, 각 OnGUI 맨 앞에서 Use() 한 줄이면 된다.
    public static class ImguiFont
    {
        const string KrName = "NotoSerifKR SDF";
        static Font _font;
        static float _nextTry;

        public static Font Font
        {
            get
            {
                if (_font != null || Time.unscaledTime < _nextTry) return _font;
                _nextTry = Time.unscaledTime + 2f; // 못 찾으면 2초에 한 번만 다시 찾는다
                foreach (var fa in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                {
                    if (fa != null && fa.name == KrName && fa.sourceFontFile != null) { _font = fa.sourceFontFile; break; }
                }
                return _font;
            }
        }

        /// <summary>OnGUI 맨 앞에서 부른다. 한글 글꼴을 찾았으면 지금 skin 의 글꼴로 건다.</summary>
        public static void Use()
        {
            var f = Font;
            if (f != null && GUI.skin.font != f) GUI.skin.font = f;
        }
    }
}
