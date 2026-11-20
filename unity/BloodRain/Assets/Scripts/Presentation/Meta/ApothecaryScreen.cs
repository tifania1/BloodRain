using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BloodRain.Presentation.Meta
{
    // 약방(EL-02U, 2026-11-20 — 기획서 10장 '영약(약방)', cheonha.json inn.elixirs). 객잔 본채 '약방'이 연다.
    //   영약 여섯 가운데 이번에 지니고 나갈 것을 고른다(한 생에 1개, 봉인석 다섯이면 2개 — InnDef.ElixirsAt).
    //   여기서는 고르기만 하고 혈정은 새 생이 시작할 때 쓴다(RebirthHandoff.Elixirs → LifeRunner 가 Apothecary.TryPrepare·Life.TakeElixirs).
    //   혈정이 모자라면 못 고른다. 1~6 키로 고르고 다시 누르면 뺀다. Esc·'객잔으로'로 닫힘.
    public sealed class ApothecaryScreen : MonoBehaviour
    {
        public TMP_FontAsset font;

        static readonly Color Paper = new Color(0.91f, 0.87f, 0.78f);
        static readonly Color Ink = new Color(0.12f, 0.10f, 0.09f);

        Soul _soul;
        RectTransform _root;
        TextMeshProUGUI _head;
        readonly List<(ElixirDef def, Image back, TextMeshProUGUI label)> _rows = new List<(ElixirDef, Image, TextMeshProUGUI)>();

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
            var list = _soul.Data.Cheonha.Inn.Elixirs;
            for (int i = 0; i < list.Count && i < 9; i++)
                if (kb[UnityEngine.InputSystem.Key.Digit1 + i].wasPressedThisFrame) Toggle(list[i].Id);
        }

        int Max => _soul.Data.Cheonha.Inn.ElixirsAt(_soul.SealStoneCount);
        int CostOf(IEnumerable<string> ids) => ids.Sum(id => _soul.Data.Cheonha.Inn.Elixirs.First(e => e.Id == id).Cost);

        public void Open(Soul soul)
        {
            _soul = soul;
            // 지난번 고른 것 가운데 지금 못 쓰는 것(혈정 부족·칸 줄어듦)은 뺀다
            while (RebirthHandoff.Elixirs.Count > Max) RebirthHandoff.Elixirs.RemoveAt(0);
            if (CostOf(RebirthHandoff.Elixirs) > soul.BloodCrystal) RebirthHandoff.Elixirs.Clear();
            if (_root != null) Destroy(_root.gameObject);
            _rows.Clear();
            var go = new GameObject("약방", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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
            InkUi.PaperBackdrop(_root);   // 2026-11-21 선지 결·가장자리 어둡게

            Place(Text(_root, Loc.T("ui.apothecary.title"), 44, FontStyles.Bold, Ink).rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -64), new Vector2(1000, 60));
            Place(Text(_root, Loc.T("ui.apothecary.sub"), 22, FontStyles.Italic, new Color(Ink.r, Ink.g, Ink.b, 0.6f)).rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -108), new Vector2(1400, 32));
            _head = Text(_root, "", 28, FontStyles.Bold, new Color32(176, 132, 40, 255));
            Place(_head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -156), new Vector2(1400, 40));

            var list = soul.Data.Cheonha.Inn.Elixirs;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                var b = new GameObject("영약", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                b.transform.SetParent(_root, false);
                var back = b.GetComponent<Image>();
                Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(i % 2 == 0 ? -400 : 400, -260 - (i / 2) * 130), new Vector2(760, 110));
                InkUi.PaperCard((RectTransform)b.transform, new Vector2(760, 110));
                var label = Text(b.transform, "", 24, FontStyles.Normal, Ink);
                var lr = label.rectTransform; lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = new Vector2(28, 8); lr.offsetMax = new Vector2(-28, -8);
                label.alignment = TextAlignmentOptions.MidlineLeft;
                string id = e.Id;
                b.GetComponent<Button>().onClick.AddListener(() => Toggle(id));
                _rows.Add((e, back, label));
            }
            InnBack.Add(_root, font, Close);
            Refresh();
        }

        void Toggle(string id)
        {
            var sel = RebirthHandoff.Elixirs;
            if (sel.Contains(id)) { sel.Remove(id); Refresh(); return; }
            var next = new List<string>(sel.Count >= Max ? sel.Skip(1) : sel) { id };
            if (CostOf(next) > _soul.BloodCrystal) return; // 혈정이 모자라면 못 고른다
            sel.Clear(); sel.AddRange(next);
            Refresh();
        }

        void Refresh()
        {
            var sel = RebirthHandoff.Elixirs;
            _head.text = Loc.F("ui.apothecary.head", sel.Count, Max, CostOf(sel), _soul.BloodCrystal);
            for (int i = 0; i < _rows.Count; i++)
            {
                var (def, back, label) = _rows[i];
                bool on = sel.Contains(def.Id);
                bool afford = on || CostOf(new List<string>(sel.Count >= Max ? sel.Skip(1) : sel) { def.Id }) <= _soul.BloodCrystal;
                back.color = on ? new Color(0.24f, 0.19f, 0.15f) : new Color(Ink.r, Ink.g, Ink.b, afford ? 0.12f : 0.05f);
                label.color = on ? Paper : new Color(Ink.r, Ink.g, Ink.b, afford ? 1f : 0.45f);
                string name = Loc.T($"cheonha.inn.elixirs.{def.Id}.name", def.NameKo), desc = Loc.T($"cheonha.inn.elixirs.{def.Id}.desc", def.DescKo);
                label.text = $"<size=70%><alpha=#99>{i + 1}<alpha=#FF></size>  <b>{name}</b> <size=80%>{Loc.Hanja(def.Hanja)}</size>   <size=80%>{Loc.F("ui.apothecary.cost", def.Cost)}</size>" + (on ? $"   <size=70%>{Loc.T("ui.apothecary.taking")}</size>" : "") + $"\n<size=85%>{desc}</size>";
            }
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
