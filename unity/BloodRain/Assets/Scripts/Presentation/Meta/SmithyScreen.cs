using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BloodRain.Presentation.Meta
{
    // 대장간 안(SM-01, 2026-11-20 추천대로 — 기획서 10장 '객잔 시설'). 꾸미기만, 능력치 없음.
    //   몸 색 셋(문파 빛깔·먹·담묵, cheonha.json inn.smithy.tints): 누르면 바로 바뀐다(공짜). 다음 생부터 몸 그림에 그 색을 곱한다(PlayerLook).
    //   무기 외형: 대장간에서 사지 않고 해금 보상(혼 해금 skin:<id>)으로 얻어 여기서 고른다. 아직 얻은 것이 없으면 그 말만.
    //   객잔 본채에서 연다. Esc·'객잔으로'로 닫히면 Closed.
    public sealed class SmithyScreen : MonoBehaviour
    {
        public TMP_FontAsset font;

        static readonly Color Paper = new Color(0.91f, 0.87f, 0.78f);
        static readonly Color Ink = new Color(0.12f, 0.10f, 0.09f);
        static readonly Color Gold = new Color32(176, 132, 40, 255);

        Soul _soul;
        RectTransform _root;
        readonly List<(BodyTintDef def, Image back, TextMeshProUGUI label)> _tints = new List<(BodyTintDef, Image, TextMeshProUGUI)>();
        Image _preview;

        public event System.Action Closed;
        public bool IsOpen => _root != null;

        public void Close()
        {
            if (_root == null) return;
            Destroy(_root.gameObject);
            _root = null;
            Closed?.Invoke();
        }

        void Update()
        {
            if (_root == null) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame) { Close(); return; }
            var tints = _soul.Data.Cheonha.Inn.Smithy.Tints;
            for (int i = 0; i < tints.Count && i < 9; i++)
                if (kb[UnityEngine.InputSystem.Key.Digit1 + i].wasPressedThisFrame) Pick(tints[i].Id);
        }

        public void Open(Soul soul, Sprite bodySprite)
        {
            _soul = soul;
            if (_root != null) Destroy(_root.gameObject);
            _tints.Clear();
            var go = new GameObject("대장간", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)go.transform;
            var bg = Img(_root, Paper);
            bg.rectTransform.anchorMin = Vector2.zero; bg.rectTransform.anchorMax = Vector2.one;
            bg.rectTransform.offsetMin = bg.rectTransform.offsetMax = Vector2.zero;

            Place(Text(_root, Loc.T("ui.smithy.title"), 44, FontStyles.Bold, Ink).rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -64), new Vector2(1000, 60));
            Place(Text(_root, Loc.T("ui.smithy.sub"), 22, FontStyles.Italic, new Color(Ink.r, Ink.g, Ink.b, 0.6f)).rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -108), new Vector2(1200, 32));

            // 왼쪽: 몸 그림 미리 보기(고른 색을 곱해 보여 줌)
            var frame = Img(_root, new Color(Ink.r, Ink.g, Ink.b, 0.10f));
            Place(frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-380, 0), new Vector2(420, 560));
            _preview = Img(frame.rectTransform, Color.white);
            _preview.sprite = bodySprite; _preview.preserveAspect = true;
            if (bodySprite == null) _preview.color = new Color(0, 0, 0, 0);
            var pr = _preview.rectTransform; pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one; pr.offsetMin = new Vector2(20, 20); pr.offsetMax = new Vector2(-20, -20);

            // 오른쪽 위: 몸 색
            Place(Text(_root, Loc.T("ui.smithy.tint"), 28, FontStyles.Bold, Ink).rectTransform, new Vector2(0.5f, 0.5f), new Vector2(260, 230), new Vector2(560, 40));
            var tints = soul.Data.Cheonha.Inn.Smithy.Tints;
            for (int i = 0; i < tints.Count; i++)
            {
                var t = tints[i];
                var b = Btn(_root, new Vector2(260, 160 - i * 80), new Vector2(520, 64), () => Pick(t.Id), out var back, out var label);
                // 색 견본
                var sw = Img(b.transform, new Color(t.Rgb[0], t.Rgb[1], t.Rgb[2]));
                Place(sw.rectTransform, new Vector2(0f, 0.5f), new Vector2(40, 0), new Vector2(40, 40));
                _tints.Add((t, back, label));
            }

            // 오른쪽 아래: 무기 외형
            float y = 160 - tints.Count * 80 - 40;
            Place(Text(_root, Loc.T("ui.smithy.skins"), 28, FontStyles.Bold, Ink).rectTransform, new Vector2(0.5f, 0.5f), new Vector2(260, y), new Vector2(560, 40));
            var skins = soul.WeaponSkins;
            string skinText = skins.Count == 0 ? Loc.T("ui.smithy.no_skins") : string.Join("  ·  ", skins);
            var st = Text(_root, skinText, 22, FontStyles.Normal, new Color(Ink.r, Ink.g, Ink.b, 0.7f));
            st.enableWordWrapping = true;
            Place(st.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(260, y - 70), new Vector2(560, 90));

            InnBack.Add(_root, font, Close);
            Refresh();
        }

        void Pick(string id)
        {
            if (_soul.TrySetBodyTint(id)) { SoulStore.Save(_soul); Refresh(); }
        }

        void Refresh()
        {
            string cur = _soul.BodyTint;
            foreach (var (def, back, label) in _tints)
            {
                bool on = def.Id == cur;
                back.color = on ? new Color(0.24f, 0.19f, 0.15f) : new Color(Ink.r, Ink.g, Ink.b, 0.12f);
                int n = _soul.Data.Cheonha.Inn.Smithy.Tints.IndexOf(def) + 1;
                label.text = $"<size=70%><alpha=#99>{n}<alpha=#FF></size>   {Loc.T($"cheonha.inn.smithy.tints.{def.Id}.name", def.NameKo)}" + (on ? $"   <size=70%>{Loc.T("ui.smithy.wearing")}</size>" : "");
                label.color = on ? Paper : Ink;
                if (on && _preview != null && _preview.sprite != null) _preview.color = new Color(def.Rgb[0], def.Rgb[1], def.Rgb[2]);
            }
        }

        Button Btn(Transform parent, Vector2 pos, Vector2 size, System.Action onClick, out Image back, out TextMeshProUGUI label)
        {
            var go = new GameObject("단추", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            back = go.GetComponent<Image>();
            Place((RectTransform)go.transform, new Vector2(0.5f, 0.5f), pos, size);
            label = Text(go.transform, "", 26, FontStyles.Bold, Ink);
            var lr = label.rectTransform; lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = new Vector2(80, 0); lr.offsetMax = Vector2.zero;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            var b = go.GetComponent<Button>();
            b.onClick.AddListener(() => onClick());
            return b;
        }

        Image Img(Transform parent, Color c)
        {
            var go = new GameObject("그림", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var i = go.GetComponent<Image>();
            i.color = c; i.raycastTarget = false;
            return i;
        }

        TextMeshProUGUI Text(Transform parent, string text, float size, FontStyles style, Color color)
        {
            var go = new GameObject("글", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text; t.fontSize = size; t.fontStyle = style; t.color = color;
            t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
            return t;
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
