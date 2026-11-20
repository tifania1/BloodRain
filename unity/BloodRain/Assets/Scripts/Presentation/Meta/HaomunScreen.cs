using System.Linq;
using BloodRain.Core.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BloodRain.Presentation.Meta
{
    // 하오문 게시판(EL-06, 2026-11-20 추천대로 — 기획서 10장 '객잔 시설'·20장 '힌트 구하기'). 객잔 본채 '하오문 게시판'이 연다.
    //   윤회의 기로 힌트를 산다(값 = 300 × (산 수 + 1), Core Soul.TryBuyHint). 사면 아직 못 찾은 비전 하나의 조건이 밀서처럼 드러난다.
    //   아래에는 지금까지 드러난 조건(밀서·힌트)을 모두 적는다. Enter 로 사고 Esc·'객잔으로'로 닫힘.
    public sealed class HaomunScreen : MonoBehaviour
    {
        public TMP_FontAsset font;

        static readonly Color Paper = new Color(0.91f, 0.87f, 0.78f);
        static readonly Color Ink = new Color(0.12f, 0.10f, 0.09f);

        Soul _soul;
        RectTransform _root;
        TextMeshProUGUI _head, _buyLabel, _list;
        Button _buy;

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
            if (kb.escapeKey.wasPressedThisFrame) Close();
            else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Buy();
        }

        public void Open(Soul soul)
        {
            _soul = soul;
            if (_root != null) Destroy(_root.gameObject);
            var go = new GameObject("하오문 게시판", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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

            Place(Text(_root, Loc.T("ui.haomun.title"), 44, FontStyles.Bold, Ink).rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -64), new Vector2(1000, 60));
            Place(Text(_root, Loc.T("ui.haomun.sub"), 22, FontStyles.Italic, new Color(Ink.r, Ink.g, Ink.b, 0.6f)).rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -108), new Vector2(1400, 32));
            _head = Text(_root, "", 28, FontStyles.Bold, new Color32(176, 132, 40, 255));
            Place(_head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -160), new Vector2(1400, 40));

            _buy = new GameObject("힌트 사기", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button)).GetComponent<Button>();
            _buy.transform.SetParent(_root, false);
            _buy.GetComponent<Image>().color = new Color(0.45f, 0.18f, 0.15f);
            Place((RectTransform)_buy.transform, new Vector2(0.5f, 1f), new Vector2(0, -230), new Vector2(520, 64));
            _buyLabel = Text(_buy.transform, "", 26, FontStyles.Bold, Paper);
            var lr = _buyLabel.rectTransform; lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = lr.offsetMax = Vector2.zero;
            _buy.onClick.AddListener(Buy);

            _list = Text(_root, "", 22, FontStyles.Normal, Ink);
            _list.alignment = TextAlignmentOptions.TopLeft;
            _list.enableWordWrapping = true;
            Place(_list.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -620), new Vector2(1500, 700));

            InnBack.Add(_root, font, Close);
            Refresh();
        }

        void Buy()
        {
            var id = _soul.TryBuyHint();
            if (id == null) return;
            SoulStore.Save(_soul);
            Refresh();
        }

        void Refresh()
        {
            bool any = _soul.NextHintSecret != null;
            bool afford = _soul.RebirthQi >= _soul.NextHintCost;
            _head.text = Loc.F("ui.qi.balance", _soul.RebirthQi.ToString("N0"));
            _buy.interactable = any && afford;
            _buyLabel.text = !any ? Loc.T("ui.haomun.none") : Loc.F("ui.haomun.buy", _soul.NextHintCost.ToString("N0"));
            var secrets = _soul.Data.Synergies.Secrets;
            var shown = secrets.Where(s => _soul.Letters.Contains(s.Id)).ToList();
            if (shown.Count == 0) { _list.text = $"<alpha=#88>{Loc.T("ui.haomun.empty")}"; return; }
            var sb = new System.Text.StringBuilder();
            foreach (var s in shown)
            {
                string name = Loc.T($"synergies.secrets.{s.Id}.name", s.NameKo);
                bool found = _soul.Codex.Contains(CodexKey.Secret(s.Id));
                sb.Append(found ? "● " : "○ ").Append("<b>").Append(name).Append("</b>  ")
                  .Append(Loc.T($"synergies.secrets.{s.Id}.condition", s.Condition)).Append('\n');
            }
            _list.text = sb.ToString().TrimEnd('\n');
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
