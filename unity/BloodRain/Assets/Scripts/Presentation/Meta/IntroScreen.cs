using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BloodRain.Presentation.Meta
{
    // DL-07b 인트로 컷신 「혈우」(기획서 19장 A, docs/story/intro.md, dialogue.json intro — 12컷 약 71초).
    //   새 혼(빈 칸)으로 서장에 들어갈 때 한 번, 타이틀 '인트로 다시 보기'로 언제든.
    //   컷마다 그림(Assets/Art/Cutscenes/<art>.jpg — 타이틀 씬 TitleScreen.introArt 가 물고 있음)을 화면에 꽉 채워
    //   천천히 당기며(1.0 → 1.06) 0.7초에 겹쳐 바꾸고, 줄은 아래 가운데에(속삭임은 옅게, 말하는 사람 이름은 달지 않음).
    //   마지막 컷(빈 종이 자리)에는 게임 제목 붓글씨를 Unity 글자로 얹는다(그림에 글자를 굽지 않는다).
    //   Space·Enter·누름 = 다음 컷, Esc·오른쪽 위 '건너뛰기' = 끝. 세상 시간과 따로 실제 시간으로 센다.
    public sealed class IntroScreen : MonoBehaviour
    {
        const float Fade = 0.7f, Guard = 0.5f, EndFade = 0.6f, Zoom = 0.06f;
        static readonly Color Paper = new Color(0.95f, 0.92f, 0.85f);
        static readonly Color Ink = new Color(0.12f, 0.10f, 0.09f);
        static readonly Color Seal = new Color32(0xB3, 0x26, 0x1E, 0xFF);

        public static bool AnyOpen { get; private set; }

        TMP_FontAsset _font, _brush;
        List<Texture2D> _art;
        Action _done;
        List<CutDef> _cuts;
        int _index = -1;
        float _cutStart, _endStart = -1f;
        RectTransform _root;
        RawImage[] _layers = new RawImage[2];
        AspectRatioFitter[] _fits = new AspectRatioFitter[2];
        int _front;
        TextMeshProUGUI _line, _title, _titleSub;
        Image _seal, _black;
        CanvasGroup _titleGroup;

        /// <summary>인트로를 띄운다. art 는 컷 그림(이름이 dialogue.json cuts[].art 와 같은 텍스처), done 은 끝나거나 건너뛴 뒤.</summary>
        public static IntroScreen Show(TMP_FontAsset font, TMP_FontAsset brush, List<Texture2D> art, Action done)
        {
            var s = new GameObject("인트로 「혈우」").AddComponent<IntroScreen>();
            s._font = font; s._brush = brush != null ? brush : font;
            s._art = art ?? new List<Texture2D>();
            s._done = done;
            s._cuts = DataBoot.Data.Dialogue.Intro.Cuts;
            AnyOpen = true;
            s.Build();
            s.Next();
            return s;
        }

        void Build()
        {
            var go = new GameObject("화면", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)go.transform;

            var bg = Child<Image>("바탕", _root); bg.color = Color.black; bg.raycastTarget = false; Stretch(bg.rectTransform);
            for (int i = 0; i < 2; i++)
            {
                var holder = Child<RectTransform>("그림 칸 " + i, _root); Stretch(holder);
                var img = Child<RawImage>("그림", holder);
                var fit = img.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;   // 화면을 꽉 채우고 넘치는 쪽은 자른다
                img.color = new Color(1f, 1f, 1f, 0f); img.raycastTarget = false;
                _layers[i] = img; _fits[i] = fit;
            }

            // 아래 어둡게(줄이 그림 위에서 읽히게)
            var shade = Child<RawImage>("아래 어둡게", _root);
            shade.texture = Gradient();
            shade.color = new Color(0f, 0f, 0f, 0.78f); shade.raycastTarget = false;
            var sr = shade.rectTransform; sr.anchorMin = new Vector2(0f, 0f); sr.anchorMax = new Vector2(1f, 0f); sr.pivot = new Vector2(0.5f, 0f);
            sr.sizeDelta = new Vector2(0f, 340f); sr.anchoredPosition = Vector2.zero;

            _line = Text("줄", _root, 40, Paper, _font);
            var lr = _line.rectTransform; lr.anchorMin = new Vector2(0.5f, 0f); lr.anchorMax = new Vector2(0.5f, 0f); lr.pivot = new Vector2(0.5f, 0.5f);
            lr.anchoredPosition = new Vector2(0f, 130f); lr.sizeDelta = new Vector2(1500f, 140f);
            _line.textWrappingMode = TextWrappingModes.Normal;
            DarkHalo(_line);

            // 마지막 컷 제목(빈 종이 자리)
            var tg = Child<RectTransform>("제목", _root);
            tg.anchorMin = tg.anchorMax = new Vector2(0.5f, 0.5f); tg.sizeDelta = new Vector2(1400f, 400f); tg.anchoredPosition = new Vector2(-40f, 70f);
            _titleGroup = tg.gameObject.AddComponent<CanvasGroup>(); _titleGroup.alpha = 0f;
            _title = Text("글", tg, 180, Ink, _brush);
            _title.text = "血雨江湖";
            var ttr = _title.rectTransform; ttr.anchorMin = ttr.anchorMax = new Vector2(0.5f, 0.5f); ttr.sizeDelta = new Vector2(1400f, 240f); ttr.anchoredPosition = new Vector2(0f, 40f);
            _titleSub = Text("부제", tg, 40, Ink, _font);
            _titleSub.text = "혈우강호"; _titleSub.characterSpacing = 30f;
            var tsr = _titleSub.rectTransform; tsr.anchorMin = tsr.anchorMax = new Vector2(0.5f, 0.5f); tsr.sizeDelta = new Vector2(800f, 60f); tsr.anchoredPosition = new Vector2(0f, -110f);
            _seal = Child<Image>("낙관", tg); _seal.color = Seal; _seal.raycastTarget = false;
            var slr = _seal.rectTransform; slr.anchorMin = slr.anchorMax = new Vector2(0.5f, 0.5f); slr.sizeDelta = new Vector2(70f, 70f); slr.anchoredPosition = new Vector2(430f, -20f);
            slr.localRotation = Quaternion.Euler(0f, 0f, -4f);
            var sealText = Text("낙관 글", slr, 48, Paper, _brush); sealText.text = "血"; Stretch(sealText.rectTransform);

            // 건너뛰기(오른쪽 위)
            var skip = Child<Image>("건너뛰기", _root);
            skip.color = new Color(0.05f, 0.04f, 0.04f, 0.55f);
            var kr = skip.rectTransform; kr.anchorMin = kr.anchorMax = new Vector2(1f, 1f); kr.pivot = new Vector2(1f, 1f);
            kr.anchoredPosition = new Vector2(-36f, -32f); kr.sizeDelta = new Vector2(220f, 56f);
            var b = skip.gameObject.AddComponent<Button>(); b.onClick.AddListener(Finish);
            var kt = Text("글", kr, 26, new Color(Paper.r, Paper.g, Paper.b, 0.85f), _font);
            kt.text = $"{Loc.T("ui.intro.skip")}  <size=70%>Esc</size>"; Stretch(kt.rectTransform);

            _black = Child<Image>("끝 어둡게", _root); _black.color = new Color(0f, 0f, 0f, 0f); _black.raycastTarget = false; Stretch(_black.rectTransform);
        }

        void Next()
        {
            if (_endStart >= 0f) return;
            _index++;
            if (_cuts == null || _index >= _cuts.Count) { Finish(); return; }
            var cut = _cuts[_index];
            _cutStart = Time.unscaledTime;
            _front = 1 - _front;
            var img = _layers[_front];
            var tex = _art.FirstOrDefault(t => t != null && t.name == cut.Art);
            img.texture = tex;
            img.color = new Color(1f, 1f, 1f, 0f);
            _fits[_front].aspectRatio = tex != null ? (float)tex.width / tex.height : 16f / 9f;
            img.transform.SetAsLastSibling();
            // 그림 두 겹 위로 어둡게·줄·제목·단추가 다시 오게
            var holder = img.transform.parent;
            holder.SetSiblingIndex(2);
            _line.text = Line(cut);
            _line.alpha = 0f;
            _titleGroup.alpha = 0f;
        }

        static string Line(CutDef cut)
        {
            var parts = new List<string>();
            for (int i = 0; i < cut.Lines.Count; i++)
            {
                var l = cut.Lines[i];
                string text = Loc.T($"dialogue.intro.cuts.{cut.Id}.lines.{i}.text", l.TextKo);
                parts.Add(l.Whisper ? $"<i><alpha=#A0>{text}<alpha=#FF></i>" : text);
            }
            return string.Join("\n", parts);
        }

        void Update()
        {
            float now = Time.unscaledTime;
            if (_endStart >= 0f)
            {
                float e = Mathf.Clamp01((now - _endStart) / EndFade);
                _black.color = new Color(0f, 0f, 0f, e);
                if (e >= 1f)
                {
                    var d = _done; _done = null;
                    AnyOpen = false;
                    d?.Invoke();
                    Destroy(gameObject);
                }
                return;
            }
            if (_index < 0 || _cuts == null || _index >= _cuts.Count) return;
            var cut = _cuts[_index];
            float t = now - _cutStart, dur = Mathf.Max(1f, cut.Seconds);

            // 그림: 0.7초에 겹쳐 나타나고, 컷 길이 동안 천천히 당긴다(홀짝 컷이 반대쪽으로 조금 흐름)
            var img = _layers[_front];
            img.color = new Color(1f, 1f, 1f, Mathf.Clamp01(t / Fade));
            float z = 1f + Zoom * Mathf.Clamp01(t / (dur + Fade));
            img.rectTransform.localScale = new Vector3(z, z, 1f);
            img.rectTransform.anchoredPosition = new Vector2((_index % 2 == 0 ? -1f : 1f) * 18f * Mathf.Clamp01(t / dur), 0f);
            var back = _layers[1 - _front];
            if (t >= Fade && back.color.a > 0f) back.color = new Color(1f, 1f, 1f, 0f);

            // 줄: 0.5초 늦게 0.4초에 나타나고, 컷 끝 0.4초 전에 옅어진다
            float la = Mathf.Clamp01((t - 0.5f) / 0.4f) * (1f - Mathf.Clamp01((t - (dur - 0.4f)) / 0.4f));
            _line.alpha = la;

            // 마지막 컷: 제목 붓글씨가 0.8초 뒤 1.2초에 걸쳐 스며 나온다
            bool last = _index == _cuts.Count - 1;
            if (last) _titleGroup.alpha = Mathf.Clamp01((t - 0.8f) / 1.2f);

            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) { Finish(); return; }
            bool pressed = (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
                || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
                || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && !OverSkip());
            if (t >= dur || (pressed && t >= Guard)) Next();
        }

        bool OverSkip()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }

        void Finish()
        {
            if (_endStart >= 0f) return;
            _endStart = Time.unscaledTime;
        }

        void OnDestroy() { if (_endStart < 0f) AnyOpen = false; }

        // ── 도구 ─────────────────────────────────────────────
        static Texture2D _gradient;
        static Texture2D Gradient()
        {
            if (_gradient != null) return _gradient;
            const int h = 64;
            _gradient = new Texture2D(1, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "IntroShade" };
            for (int y = 0; y < h; y++)
            {
                float v = y / (h - 1f);
                _gradient.SetPixel(0, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(1f, 0f, v)));
            }
            _gradient.Apply();
            return _gradient;
        }

        static void DarkHalo(TMP_Text t)
        {
            var m = t.fontMaterial;
            m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.85f));
            m.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.5f);
            m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.6f);
            t.UpdateMeshPadding();
        }

        static T Child<T>(string name, Transform parent) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            if (typeof(T) == typeof(RectTransform)) return (T)(Component)go.transform;
            return go.AddComponent<T>();
        }

        TextMeshProUGUI Text(string name, Transform parent, float size, Color c, TMP_FontAsset font)
        {
            var t = Child<TextMeshProUGUI>(name, parent);
            if (font != null) t.font = font;
            t.fontSize = size; t.color = c; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false; t.richText = true;
            return t;
        }

        static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }
    }
}
