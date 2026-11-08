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
    // EV-01U 그릇이 늘어나는 돌파에서 문턱 무공이 둘 이상이면 어느 절기를 깨울지 고른다(기획서 8장 '깨어나는 순간').
    // 웹 template.html '그릇이 늘어나는 돌파 순간'과 같은 차례: 돌파 줌이 끝난 자리에서 LifeRunner 가 열고, 고른 뒤 각성 연출 → 깨달음.
    // 카드마다 절기 이름·`무공 + 심법`·요약. 1~4 키로도 고른다. 열린 동안 세상 멈춤, 열린 직후 InputGuardSeconds 동안 입력 막기.
    public sealed class EvolutionPickScreen : MonoBehaviour
    {
        public TMP_FontAsset font;

        static readonly Color Paper = new Color(0.93f, 0.89f, 0.80f, 0.98f);
        static readonly Color Ink = new Color(0.12f, 0.10f, 0.09f);
        static readonly Color Gold = new Color32(190, 146, 52, 255);

        RectTransform _root;
        IReadOnlyList<EvolutionDef> _cards;
        Action<EvolutionDef> _onPick;
        float _prevScale = 1f, _openedAt;

        public bool IsOpen => _root != null;

        public void Open(IReadOnlyList<EvolutionDef> cards, string realmName, Action<EvolutionDef> onPick)
        {
            if (IsOpen || cards.Count == 0) return;
            _cards = cards; _onPick = onPick;
            _prevScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            _openedAt = Time.unscaledTime;
            if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Build(realmName);
        }

        bool Guarded => Time.unscaledTime - _openedAt < DataBoot.Data.Balance.LevelUp.InputGuardSeconds;

        void Update()
        {
            if (!IsOpen) return;
            if (Time.timeScale != 0f) Time.timeScale = 0f;
            var kb = Keyboard.current;
            if (kb == null || Guarded) return;
            if (kb.digit1Key.wasPressedThisFrame) Pick(0);
            else if (kb.digit2Key.wasPressedThisFrame) Pick(1);
            else if (kb.digit3Key.wasPressedThisFrame) Pick(2);
            else if (kb.digit4Key.wasPressedThisFrame) Pick(3);
        }

        public void Pick(int i)
        {
            if (!IsOpen || Guarded || i < 0 || i >= _cards.Count) return;
            var pick = _cards[i];
            Destroy(_root.gameObject);
            _root = null;
            Time.timeScale = _prevScale;
            _onPick?.Invoke(pick);
        }

        void Build(string realmName)
        {
            var go = new GameObject("절기 고르기", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 46;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)go.transform;

            var dim = Panel(_root, new Color(0.02f, 0.02f, 0.03f, 0.6f));
            Stretch(dim.rectTransform);
            var title = Text(_root, Loc.T("ui.evopick.title"), 46, FontStyles.Bold, Paper);
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 330), new Vector2(1200, 64));
            var sub = Text(_root, Loc.F("ui.evopick.sub", realmName), 22, FontStyles.Italic, new Color(Paper.r, Paper.g, Paper.b, 0.7f));
            Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 280), new Vector2(1200, 32));

            var data = DataBoot.Data;
            int n = _cards.Count;
            float w = 360, gap = 40;
            float x0 = -(n - 1) * (w + gap) / 2f;
            for (int i = 0; i < n; i++)
            {
                var d = _cards[i];
                var frame = Panel(_root, Gold);
                Place(frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x0 + i * (w + gap), -20), new Vector2(w + 8, 468));
                var b = new GameObject(d.Id, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button)).GetComponent<Button>();
                b.transform.SetParent(frame.transform, false);
                b.GetComponent<Image>().color = Paper;
                Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, 460));
                int idx = i;
                b.onClick.AddListener(() => Pick(idx));

                var hanja = Text(b.transform, d.Hanja, 64, FontStyles.Normal, Gold);
                Place(hanja.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -90), new Vector2(w - 24, 100));
                hanja.enableAutoSizing = true; hanja.fontSizeMin = 32; hanja.fontSizeMax = 64;
                var nm = Text(b.transform, Loc.T($"evolutions.{d.Id}.name", d.NameKo), 34, FontStyles.Bold, Ink);
                Place(nm.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -180), new Vector2(w - 24, 48));
                nm.enableAutoSizing = true; nm.fontSizeMin = 20; nm.fontSizeMax = 34;
                var wd = data.Weapon(d.Weapon);
                var pd = data.Passive(d.Passive);
                string pair = $"{Loc.T($"weapons.{wd.Id}.name", wd.NameKo)} + {Loc.T($"passives.{pd.Id}.name", pd.NameKo)}";
                var pr = Text(b.transform, pair, 22, FontStyles.Normal, new Color(Ink.r, Ink.g, Ink.b, 0.7f));
                Place(pr.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -226), new Vector2(w - 24, 32));
                pr.enableAutoSizing = true; pr.fontSizeMin = 14; pr.fontSizeMax = 22;
                var tx = Text(b.transform, Loc.T($"evolutions.{d.Id}.summary", d.Summary), 22, FontStyles.Normal, Ink);
                Place(tx.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -330), new Vector2(w - 40, 160));
                tx.textWrappingMode = TextWrappingModes.Normal;
                tx.enableAutoSizing = true; tx.fontSizeMin = 14; tx.fontSizeMax = 22;
                var key = Text(b.transform, (i + 1).ToString(), 20, FontStyles.Bold, new Color(Ink.r, Ink.g, Ink.b, 0.45f));
                Place(key.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 24), new Vector2(60, 28));
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
