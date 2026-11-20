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

        /// <summary>
        /// 테두리 두른 IMGUI 글(2026-11-21 — 줍는 글 '혈정 +1'·'은자 +3' 이 테두리 없이 돌바닥·종이 바닥에 묻혔다).
        /// 테두리색으로 여덟 방향에 한 번씩 그린 뒤 글자색. 알파는 둘 다 fg.a 를 따른다.
        /// </summary>
        public static void OutlinedLabel(Rect r, string text, GUIStyle style, Color fg, Color edge, float width = 1.6f)
        {
            var old = GUI.color;
            GUI.color = new Color(edge.r, edge.g, edge.b, edge.a * fg.a);
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (dx != 0 || dy != 0) GUI.Label(new Rect(r.x + dx * width, r.y + dy * width, r.width, r.height), text, style);
            GUI.color = fg;
            GUI.Label(r, text, style);
            GUI.color = old;
        }

        /// <summary>1080 줄 화면 기준 글 크기를 지금 화면에 맞춘다.</summary>
        public static int Px(float px1080) => Mathf.Max(10, Mathf.RoundToInt(px1080 * Screen.height / 1080f));

        /// <summary>OnGUI 맨 앞에서 부른다. 한글 글꼴을 찾았으면 지금 skin 의 글꼴로 건다.</summary>
        public static void Use()
        {
            var f = Font;
            if (f != null && GUI.skin.font != f) GUI.skin.font = f;
        }
    }
}
