using System;
using System.Collections.Generic;
using BloodRain.Core.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BloodRain.Presentation.Hud
{
    // C-07b 돌파 깨달음 3택1(기획서 3장 '깨달음'). 돌파 줌이 끝난 자리에서 LifeRunner 가 연다. 레벨업 카드보다 먼저 뜬다.
    // 카드 석 장(올라 본 경지면 넉 장, 진소운 특성도 넉 장). 마공 카드(마교 몸)는 테두리를 붉게.
    // 고르는 것만 여기서 하고, 효과는 LifeRunner 가 life.TakeInsight 로 건다. 열린 동안 세상 시간을 멈춘다. 1~4 키로도 고른다.
    public sealed class InsightScreen : MonoBehaviour
    {
        public TMP_FontAsset font;

        static readonly Color Paper = new Color(0.93f, 0.89f, 0.80f, 0.98f);
        static readonly Color Ink = new Color(0.12f, 0.10f, 0.09f);
        static readonly Color Gold = new Color32(190, 146, 52, 255);
        static readonly Color Blood = new Color32(150, 24, 22, 255);

        RectTransform _root;
        IReadOnlyList<InsightDef> _cards;
        Func<InsightDef, bool> _isDemonic;
        Action<InsightDef> _onPick;
        float _prevScale = 1f;

        public bool IsOpen => _root != null;

        public void Open(IReadOnlyList<InsightDef> cards, Func<InsightDef, bool> isDemonic, Action<InsightDef> onPick)
        {
            if (IsOpen || cards.Count == 0) return;
            _cards = cards; _isDemonic = isDemonic; _onPick = onPick;
            _prevScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Build();
        }

        void Update()
        {
            if (!IsOpen) return;
            if (Time.timeScale != 0f) Time.timeScale = 0f;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame) Pick(0);
            else if (kb.digit2Key.wasPressedThisFrame) Pick(1);
            else if (kb.digit3Key.wasPressedThisFrame) Pick(2);
            else if (kb.digit4Key.wasPressedThisFrame) Pick(3);
        }

        public void Pick(int i)
        {
            if (!IsOpen || i < 0 || i >= _cards.Count) return;
            var pick = _cards[i];
            Destroy(_root.gameObject);
            _root = null;
            Time.timeScale = _prevScale;
            _onPick?.Invoke(pick);
        }

        void Build()
        {
            var go = new GameObject("깨달음", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 45;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)go.transform;

            var dim = Panel(_root, new Color(0.02f, 0.02f, 0.03f, 0.6f));
            Stretch(dim.rectTransform);
            var title = Text(_root, Loc.T("ui.insight.title"), 46, FontStyles.Bold, Paper);
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 330), new Vector2(1200, 64));
            var sub = Text(_root, Loc.T("ui.insight.sub"), 22, FontStyles.Italic, new Color(Paper.r, Paper.g, Paper.b, 0.7f));
            Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 280), new Vector2(1200, 32));

            int n = _cards.Count;
            float w = 360, gap = 40;
            float x0 = -(n - 1) * (w + gap) / 2f;
            for (int i = 0; i < n; i++)
            {
                var d = _cards[i];
                bool demonic = _isDemonic != null && _isDemonic(d);
                var frame = Panel(_root, demonic ? Blood : Gold);
                Place(frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x0 + i * (w + gap), -20), new Vector2(w + 8, 468));
                var b = new GameObject(d.Id, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button)).GetComponent<Button>();
                b.transform.SetParent(frame.transform, false);
                b.GetComponent<Image>().color = Paper;
                Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, 460));
                int idx = i;
                b.onClick.AddListener(() => Pick(idx));

                var glyph = Text(b.transform, d.Glyph, 120, FontStyles.Normal, demonic ? Blood : Ink);
                Place(glyph.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -110), new Vector2(w, 150));
                string name = demonic ? Loc.T($"insights.demonicInsights.{d.Id}.name", d.NameKo) : Loc.T($"insights.{d.Id}.name", d.NameKo);
                string text = demonic ? Loc.T($"insights.demonicInsights.{d.Id}.text", d.TextKo) : Loc.T($"insights.{d.Id}.text", d.TextKo);
                var nm = Text(b.transform, $"{name}<size=60%><alpha=#88> {Loc.Hanja(d.Hanja)}</size>", 34, FontStyles.Bold, Ink);
                Place(nm.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -230), new Vector2(w - 24, 48));
                nm.enableAutoSizing = true; nm.fontSizeMin = 20; nm.fontSizeMax = 34;
                var tx = Text(b.transform, text, 24, FontStyles.Normal, Ink);
                Place(tx.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -330), new Vector2(w - 40, 140));
                tx.textWrappingMode = TextWrappingModes.Normal;
                tx.enableAutoSizing = true; tx.fontSizeMin = 16; tx.fontSizeMax = 24;
                var key = Text(b.transform, (i + 1).ToString(), 20, FontStyles.Bold, new Color(Ink.r, Ink.g, Ink.b, 0.45f));
                Place(key.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 24), new Vector2(60, 28));
                if (demonic)
                {
                    var tag = Text(b.transform, Loc.T("ui.insight.demonic"), 20, FontStyles.Bold, Blood);
                    Place(tag.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -24), new Vector2(w, 28));
                }
            }
        }

        Image Panel(Transform parent, Color c)
        {
            var img = new GameObject("판", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(parent, false);
            img.color = c;
            return img;
        }

        TextMeshProUGUI Text(Transform parent, string text, float size, FontStyles style, Color color)
        {
            var go = new GameObject("글", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text; t.fontSize = size; t.fontStyle = style; t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }
}
