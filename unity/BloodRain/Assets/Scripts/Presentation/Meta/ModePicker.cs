using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BloodRain.Presentation.Meta
{
    // 모드 고르기(R4-05U, 기획서 7장 '모드'). 객잔에서 생을 시작할 때 열린 모드(Core Modes.OpenModes)가 둘 이상이면 침방 앞에 띄운다.
    //   모드 이름은 Loc.Mode(strings cheonha.modes.<id>.name, 없으면 nameKo). 데이터 순서대로 줄을 세우고 1~4 키로도 고른다.
    //   일일 도전·보스 연전은 아직 규칙이 없어 열렸어도 버튼만 두고 잠가 둔다(Playable).
    //   혈월 천하는 soul.IsBloodMoon 이 이미 적 기혈·보상을 셈하니 고르기만 한다. 고른 id 는 RebirthHandoff.Mode 에 남는다.
    public sealed class ModePicker : MonoBehaviour
    {
        public TMP_FontAsset font;
        public event Action<string> Picked;

        /// <summary>규칙이 붙어 지금 고를 수 있는 모드. 나머지는 열려도 잠긴 버튼.</summary>
        public static readonly HashSet<string> Playable = new HashSet<string> { "cheonha", "bloodmoon" };

        static readonly Color Paper = new Color(0.91f, 0.87f, 0.78f);
        static readonly Color Ink = new Color(0.12f, 0.10f, 0.09f);
        static readonly Color Dim = new Color(0.05f, 0.04f, 0.04f, 0.82f);

        readonly List<string> _ids = new List<string>();
        bool _done;

        /// <summary>열린 모드가 둘 이상이면 고르기 화면을 띄우고 true. 하나뿐이면 띄우지 않는다.</summary>
        public bool Open(Soul soul)
        {
            var open = Modes.OpenModes(soul);
            if (open.Count < 2) return false;
            Build(soul, open);
            return true;
        }

        void Build(Soul soul, IReadOnlyList<string> open)
        {
            var canvasGo = new GameObject("모드 고르기", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50; // 침방 위
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)canvasGo.transform;

            var bg = Child("어둠", root).gameObject.AddComponent<Image>();
            bg.color = Dim;
            Stretch(bg.rectTransform);

            var title = Text("제목", root, Loc.T("ui.mode.title"), 44, FontStyles.Bold, Paper);
            Place(title.rectTransform, new Vector2(0, 300), new Vector2(1200, 70));

            var defs = DataBoot.Data.Cheonha.Modes.Where(m => open.Contains(m.Id)).ToList();
            float y = 150;
            for (int i = 0; i < defs.Count; i++)
            {
                var m = defs[i];
                _ids.Add(m.Id);
                bool playable = Playable.Contains(m.Id);
                var go = new GameObject(m.Id, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                go.transform.SetParent(root, false);
                go.GetComponent<Image>().color = m.Id == "bloodmoon" ? new Color(0.45f, 0.10f, 0.08f) : Paper;
                var b = go.GetComponent<Button>();
                var cb = b.colors;
                cb.highlightedColor = new Color(1.12f, 1.08f, 1.0f);
                cb.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.55f);
                b.colors = cb;
                b.interactable = playable;
                Place((RectTransform)go.transform, new Vector2(0, y), new Vector2(620, 92));
                string name = Loc.Mode(m);
                string label = playable
                    ? $"{name}  <size=60%><alpha=#88>{i + 1}<alpha=#FF></size>"
                    : $"{name}  <size=60%>{Loc.T("ui.mode.soon")}</size>";
                var t = Text("글", go.transform, label, 34, FontStyles.Bold, m.Id == "bloodmoon" ? Paper : Ink);
                Stretch(t.rectTransform);
                string id = m.Id;
                b.onClick.AddListener(() => Pick(id));
                y -= 112;
            }
        }

        public void Pick(string id)
        {
            if (_done || !Playable.Contains(id) || !_ids.Contains(id)) return;
            _done = true;
            RebirthHandoff.Mode = id;
            Debug.Log($"[모드] {id} 를 고름");
            Picked?.Invoke(id);
            Destroy(gameObject);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || _done) return;
            Key[] keys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4 };
            for (int i = 0; i < keys.Length && i < _ids.Count; i++)
                if (kb[keys[i]].wasPressedThisFrame) Pick(_ids[i]);
        }

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

        static void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        TextMeshProUGUI Text(string name, Transform parent, string text, float size, FontStyles style, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text; t.fontSize = size; t.fontStyle = style; t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }
    }
}
