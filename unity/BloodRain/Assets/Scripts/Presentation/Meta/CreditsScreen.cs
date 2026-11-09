using System;
using System.Collections.Generic;
using BloodRain.Core.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BloodRain.Presentation.Meta
{
    // TT-02U 크레딧 화면(기획서 12장 화면 목록 '크레딧'). 타이틀의 '크레딧' 단추가 띄운다.
    //   선지 바탕에 Core CreditsDef.Roll(Loc.T) 이 주는 줄을 위에서부터 그대로 아래→위로 흘린다(두루마리).
    //   아무 키·클릭을 누르고 있으면 빨리 감기, Esc·패드 B 는 바로 타이틀로. 끝까지 흐르면 저절로 닫힌다.
    //   줄 모양은 CreditLineKind 마다 한 벌(Title 붓 글꼴 + 낙관, Heading 먹 굵게, Use 담묵 작게, Name, Notice 작게, LicenseText 가장 작게·폭 맞춰 줄바꿈, Thanks 크게 띄워).
    public sealed class CreditsScreen : MonoBehaviour
    {
        public TMP_FontAsset font;       // 본문(NotoSerif, SC·TC 대체 목록 그대로)
        public TMP_FontAsset brushFont;  // 제목(Yuji Boku 동적)

        /// <summary>보통 흐르는 빠르기(캔버스 1080 기준 px/초)와 빨리 감기 배수.</summary>
        public const float Speed = 70f, FastMultiplier = 8f;
        const float Width = 1100f;

        static readonly Color PaperBg = new Color32(0xEF, 0xE6, 0xD2, 0xFF);
        static readonly Color Ink = new Color(0.12f, 0.10f, 0.09f);
        static readonly Color InkLight = new Color(0.12f, 0.10f, 0.09f, 0.62f);
        static readonly Color Seal = new Color32(0xB3, 0x26, 0x1E, 0xFF);

        public static bool AnyOpen { get; private set; }

        RectTransform _root, _scroll;
        float _height;   // 흘릴 글 전체 높이
        float _y;        // _scroll 의 지금 위치
        Action _onClose;
        int _openedFrame;

        public bool Finished { get; private set; }

        public static CreditsScreen Show(TMP_FontAsset font, TMP_FontAsset brushFont, Action onClose)
        {
            var s = new GameObject("크레딧 화면").AddComponent<CreditsScreen>();
            s.font = font; s.brushFont = brushFont;
            s.Open(onClose);
            return s;
        }

        public void Open(Action onClose)
        {
            _onClose = onClose;
            _openedFrame = Time.frameCount;
            AnyOpen = true;
            if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Build(DataBoot.Data.Credits.Roll(Loc.T));
        }

        void Build(IReadOnlyList<CreditLine> lines)
        {
            var canvasGo = new GameObject("크레딧", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)canvasGo.transform;

            var bg = Child("바탕", _root).gameObject.AddComponent<Image>();
            bg.color = PaperBg; // 클릭을 받아 뒤 타이틀 단추가 눌리지 않게(raycastTarget 기본 켬)
            Stretch(bg.rectTransform);

            // 흘릴 글: 화면 가운데 위쪽을 기준으로 아래로 쌓고, 통째로 위로 올린다
            _scroll = Child("두루마리", _root);
            _scroll.anchorMin = _scroll.anchorMax = new Vector2(0.5f, 0f);
            _scroll.pivot = new Vector2(0.5f, 1f);
            _scroll.sizeDelta = new Vector2(Width, 10);

            float y = 0;
            CreditLineKind? prev = null;
            foreach (var line in lines)
            {
                y -= GapBefore(line.Kind, prev);
                if (line.Kind == CreditLineKind.LicenseText)
                {
                    // 문단은 빈 줄로 나뉨 — 문단마다 한 덩이, 폭 맞춰 줄바꿈
                    var paras = line.Text.Replace("\r\n", "\n").Split(new[] { "\n\n" }, StringSplitOptions.None);
                    for (int i = 0; i < paras.Length; i++)
                    {
                        var p = paras[i].Trim();
                        if (p.Length == 0) continue;
                        y -= AddLine(CreditLineKind.LicenseText, p.Replace("\n", " "), y) + 14;
                    }
                }
                else y -= AddLine(line.Kind, line.Text, y);
                prev = line.Kind;
            }
            _height = -y;
            _y = 0; // 맨 윗줄이 화면 아래 끝에서 시작
            _scroll.anchoredPosition = new Vector2(0, _y);
        }

        static float GapBefore(CreditLineKind k, CreditLineKind? prev)
        {
            if (prev == null) return 0;
            switch (k)
            {
                case CreditLineKind.Heading: return prev == CreditLineKind.Title ? 120 : 90;
                case CreditLineKind.Use: return prev == CreditLineKind.Heading ? 22 : 40;
                case CreditLineKind.Thanks: return 260;
                case CreditLineKind.LicenseText: return 18;
                default: return 6;
            }
        }

        /// <summary>한 줄을 y 에 놓고 높이를 돌려준다.</summary>
        float AddLine(CreditLineKind kind, string text, float y)
        {
            float size; FontStyles style = FontStyles.Normal; Color color = Ink; var f = font; bool wrap = false;
            switch (kind)
            {
                case CreditLineKind.Title: size = 120; f = brushFont != null ? brushFont : font; break;
                case CreditLineKind.Heading: size = 40; style = FontStyles.Bold; break;
                case CreditLineKind.Use: size = 24; color = InkLight; break;
                case CreditLineKind.Name: size = 34; wrap = true; break;
                case CreditLineKind.Notice: size = 22; color = InkLight; wrap = true; break;
                case CreditLineKind.LicenseText: size = 18; color = InkLight; wrap = true; break;
                default: size = 44; break; // Thanks
            }
            var t = Text(kind.ToString(), _scroll, text, size, style, color, f);
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            if (kind == CreditLineKind.LicenseText) t.alignment = TextAlignmentOptions.Justified;
            float h = Mathf.Ceil(t.GetPreferredValues(text, Width, 0).y) + 4;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(Width, h);
            rt.anchoredPosition = new Vector2(0, y);

            if (kind == CreditLineKind.Title)
            {
                // 붉은 낙관: 제목 오른쪽 아래(타이틀 화면과 같은 도장)
                var seal = Child("낙관", rt).gameObject.AddComponent<Image>();
                seal.color = Seal;
                seal.rectTransform.anchorMin = seal.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                seal.rectTransform.anchoredPosition = new Vector2(330, -40);
                seal.rectTransform.sizeDelta = new Vector2(56, 56);
                var st = Text("낙관 글", seal.transform, "血", 38, FontStyles.Normal, PaperBg, brushFont != null ? brushFont : font);
                Stretch(st.rectTransform);
            }
            return h;
        }

        void Update()
        {
            if (_root == null || Time.frameCount == _openedFrame) return; // 연 클릭이 곧바로 빨리 감기로 잡히지 않게
            var kb = Keyboard.current; var pad = Gamepad.current; var mouse = Mouse.current;
            if ((kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame)) { Close(); return; }

            bool fast = (kb != null && kb.anyKey.isPressed)
                        || (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed))
                        || (pad != null && (pad.buttonSouth.isPressed || pad.rightTrigger.isPressed || pad.startButton.isPressed));
            _y += Speed * (fast ? FastMultiplier : 1f) * Time.unscaledDeltaTime;
            _scroll.anchoredPosition = new Vector2(0, _y);
            // 끝 인사가 화면 가운데에 닿으면 잠깐 머물렀다 닫는다
            if (_y >= _height + 540f + 60f)
            {
                if (!Finished) { Finished = true; _endAt = Time.unscaledTime + 2.5f; }
                _y = _height + 600f;
                _scroll.anchoredPosition = new Vector2(0, _y);
                if (Time.unscaledTime >= _endAt || fast && Time.unscaledTime >= _endAt - 2f) Close();
            }
        }
        float _endAt;

        /// <summary>시험·검수용: 흐름을 정해진 곳(0=시작, 1=끝 인사)으로 옮긴다.</summary>
        public void Seek(float t) { _y = Mathf.Lerp(0, _height + 540f, Mathf.Clamp01(t)); if (_scroll != null) _scroll.anchoredPosition = new Vector2(0, _y); }

        public void Close()
        {
            if (_root == null) return;
            Destroy(_root.gameObject);
            _root = null;
            AnyOpen = false;
            var cb = _onClose; _onClose = null;
            cb?.Invoke();
            Destroy(gameObject);
        }

        void OnDestroy() { if (_root != null) AnyOpen = false; }

        static RectTransform Child(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static TextMeshProUGUI Text(string name, Transform parent, string text, float size, FontStyles style, Color color, TMP_FontAsset f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (f != null) t.font = f;
            t.text = text; t.fontSize = size; t.fontStyle = style; t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }
    }
}
