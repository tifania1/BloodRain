using TMPro;
using UnityEngine;

namespace BloodRain.Presentation
{
    // OnGUI(IMGUI) 글자에 한글 글꼴을 건다(WG-01Ub). 에디터는 IMGUI 기본 글꼴이 OS 글꼴로 한글을 대신 그려 주지만
    // WebGL 빌드는 내장 글꼴만 써서 한글이 통째로 빠졌다(보갑 목록 창이 'Lv2 · (Lv 35) · Enter' 만 보이던 것).
    // 본문 TMP 글꼴(NotoSerifKR SDF)과 같은 글꼴 파일을 IMGUI 몫으로 한 벌 더 들여(Resources/ImguiFonts) GUI.skin.font 로 쓴다.
    // GUIStyle.font 가 비어 있으면 skin 글꼴을 쓰므로, 각 OnGUI 맨 앞에서 Use() 한 줄이면 된다.
    public static class ImguiFont
    {
        const string KrName = "NotoSerifKR SDF";
        const string KrFile = "ImguiFonts/NotoSerifKR-Imgui";
        const string TcFile = "ImguiFonts/NotoSerifTC-Imgui";

        /// <summary>IMGUI 몫 글꼴 파일을 읽는다. 없으면(파일이 빠진 판) TMP 글꼴의 원본을 복제 없이 그대로 쓴다 — 글자가 가끔 엉키지만 빈칸보다 낫다.</summary>
        static Font LoadOwn(string file, string tmpName)
        {
            var f = Resources.Load<Font>(file);
            if (f != null) return f;
            foreach (var fa in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                if (fa != null && fa.name == tmpName && fa.sourceFontFile != null) return fa.sourceFontFile;
            return null;
        }
        static Font _font;
        static float _nextTry;

        public static Font Font
        {
            get
            {
                if (_font != null || Time.unscaledTime < _nextTry) return _font;
                _nextTry = Time.unscaledTime + 2f; // 못 찾으면 2초에 한 번만 다시 찾는다
                // 원본 Font 를 그대로 쓰면 TMP 동적 글꼴(같은 원본)과 글리프 자리가 엉켜 천하도 글자가 빠지거나 다른 글자로 그려졌다
                // (2026-11-21: '남궁세가'가 '가'만, '세상이 작아져'가 '생이 장아져', 嵩 빠짐). 그래서 IMGUI 는 따로 들인 같은 글꼴 파일
                // (Resources/ImguiFonts, 원본의 복사본)을 쓴다. 처음엔 Object.Instantiate 로 복제했는데 동적 글꼴은 복제가 안 돼
                // Play 마다 'Cloning a dynamic font is not supported' 오류가 났다(2026-11-21 20:37 PC Unity 세션).
                _font = LoadOwn(KrFile, KrName);
                return _font;
            }
        }

        const string HanName = "NotoSerifTC SDF";
        /// <summary>데이터에 쓰는데 Noto Serif KR 에 없는 한자(테스트 FontCoverageTests 가 데이터와 맞는지 본다).
        /// 에디터는 OS 글꼴로 대신 그려 Font.HasCharacter 가 참이라 목록으로도 본다.</summary>
        public const string MissingInKr = "丐罩";
        static Font _han;
        static float _nextHanTry;

        /// <summary>KR 글꼴에 없는 한자(丐·罩 — Noto Serif KR 에 아예 없다)를 그릴 IMGUI 한자 글꼴(TC 복사본). 없으면 null.
        /// TMP 는 SC·TC 를 대체 글꼴로 걸어 두어 괜찮지만 IMGUI 는 대체가 없어 천하도 '丐幇總舵'의 丐 가 빈칸이었다(2026-11-21 웹 판).</summary>
        public static Font Hanja
        {
            get
            {
                if (_han != null || Time.unscaledTime < _nextHanTry) return _han;
                _nextHanTry = Time.unscaledTime + 2f;
                _han = LoadOwn(TcFile, HanName);
                return _han;
            }
        }

        /// <summary>IMGUI 한글 글꼴에 없는 CJK 글자가 들어 있나(그러면 Hanja 글꼴로 그린다).</summary>
        public static bool Lacks(string s)
        {
            var f = Font;
            if (f == null || string.IsNullOrEmpty(s)) return false;
            foreach (char c in s) if (c >= 0x2E80 && (MissingInKr.IndexOf(c) >= 0 || !f.HasCharacter(c))) return true;
            return false;
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
