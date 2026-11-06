using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BloodRain.Presentation.Hud
{
    // WP-39U 검총(기획서 20장, synergies.json encounters sword_tomb). 웹 showSwordTomb 과 같은 화면:
    //   가진 무공 카드마다 '꽂으면 남은 무공 공력 +레벨×mightPerLevel', 맨 끝에 '지나친다'. 열린 동안 세상 시간을 멈춘다. 1~9 키, Esc·Enter 는 지나친다.
    //   고르는 것만 여기서 하고 셈은 EncounterLayer 가 Life.Encounters.PlantSword 로 한다. pick 이 null 이면 지나친 것.
    public sealed class SwordTombScreen : MonoBehaviour
    {
        public TMP_FontAsset font;

        static readonly Color Paper = new Color(0.93f, 0.89f, 0.80f, 0.98f);
        static readonly Color Ink = new Color(0.12f, 0.10f, 0.09f);
        static readonly Color Steel = new Color32(120, 124, 130, 255);

        RectTransform _root;
        List<string> _ids;
        Action<string> _onPick;
        float _prevScale = 1f;

        public bool IsOpen => _root != null;

        public void Open(GameData data, Loadout loadout, float mightPerLevel, Action<string> onPick)
        {
            if (IsOpen) return;
            _ids = new List<string>(loadout.Weapons.Keys);
            _onPick = onPick;
            _prevScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Build(data, loadout, mightPerLevel);
        }

        void Update()
        {
            if (!IsOpen) return;
            if (Time.timeScale != 0f) Time.timeScale = 0f;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) { Pick(-1); return; }
            for (int i = 0; i < _ids.Count && i < 9; i++)
                if (kb[Key.Digit1 + i].wasPressedThisFrame) { Pick(i); return; }
        }

        /// <summary>i 번째 무공을 꽂는다. 범위 밖(-1)이면 지나친다.</summary>
        public void Pick(int i)
        {
            if (!IsOpen) return;
            string id = i >= 0 && i < _ids.Count ? _ids[i] : null;
            Destroy(_root.gameObject);
            _root = null;
            Time.timeScale = _prevScale;
            _onPick?.Invoke(id);
        }

        void Build(GameData data, Loadout loadout, float perLevel)
        {
            var go = new GameObject("검총", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 45;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)go.transform;

            var dim = Panel(_root, new Color(0.02f, 0.02f, 0.03f, 0.65f));
            Stretch(dim.rectTransform);
            var title = Text(_root, Loc.T("synergies.encounters.sword_tomb.name", "검총"), 46, FontStyles.Bold, Paper);
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 330), new Vector2(1400, 64));
            var sub = Text(_root, Loc.F("ui.encounter.tomb.sub", Mathf.RoundToInt(perLevel * 100f)), 22, FontStyles.Italic, new Color(Paper.r, Paper.g, Paper.b, 0.75f));
            Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 280), new Vector2(1600, 32));
            sub.enableAutoSizing = true; sub.fontSizeMin = 14; sub.fontSizeMax = 22;

            int n = _ids.Count + 1;
            float w = 250, gap = 24;
            float x0 = -(n - 1) * (w + gap) / 2f;
            for (int i = 0; i < n; i++)
            {
                bool pass = i == _ids.Count;
                var frame = Panel(_root, pass ? new Color(Paper.r, Paper.g, Paper.b, 0.4f) : Steel);
                Place(frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x0 + i * (w + gap), -20), new Vector2(w + 8, 368));
                var b = new GameObject(pass ? "지나친다" : _ids[i], typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button)).GetComponent<Button>();
                b.transform.SetParent(frame.transform, false);
                b.GetComponent<Image>().color = Paper;
                Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, 360));
                int idx = pass ? -1 : i;
                b.onClick.AddListener(() => Pick(idx));

                string glyph, name, text, key;
                if (pass)
                {
                    glyph = "行"; name = Loc.T("ui.encounter.tomb.pass"); text = ""; key = "Esc";
                }
                else
                {
                    var def = data.Weapon(_ids[i]);
                    int lv = loadout.Weapons[_ids[i]];
                    bool evolved = loadout.Evolved.Contains(_ids[i]);
                    glyph = def.NameKo.Substring(0, 1);
                    name = $"{Loc.T($"weapons.{def.Id}.name", def.NameKo)} Lv{lv}{(evolved ? " ★" : "")}";
                    text = Loc.F("ui.encounter.tomb.card", Mathf.RoundToInt(lv * perLevel * 100f));
                    key = (i + 1).ToString();
                }
                var g = Text(b.transform, glyph, 96, FontStyles.Normal, Ink);
                Place(g.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -90), new Vector2(w, 120));
                var nm = Text(b.transform, name, 28, FontStyles.Bold, Ink);
                Place(nm.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -190), new Vector2(w - 20, 44));
                nm.enableAutoSizing = true; nm.fontSizeMin = 16; nm.fontSizeMax = 28;
                var tx = Text(b.transform, text, 22, FontStyles.Normal, Ink);
                Place(tx.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -265), new Vector2(w - 30, 90));
                tx.textWrappingMode = TextWrappingModes.Normal;
                tx.enableAutoSizing = true; tx.fontSizeMin = 14; tx.fontSizeMax = 22;
                var k = Text(b.transform, key, 20, FontStyles.Bold, new Color(Ink.r, Ink.g, Ink.b, 0.45f));
                Place(k.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 22), new Vector2(80, 28));
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
