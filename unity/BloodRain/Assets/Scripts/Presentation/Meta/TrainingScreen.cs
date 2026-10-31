using System.Collections.Generic;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BloodRain.Presentation.Meta
{
    // 연무장 화면(TU-14, 기획서 10장 '연무장'·'환혼 강화'). 윤회의 기 잔고, 연무장 20항목(레벨, 다음 비용, 효과), 사기와 전부 환불.
    // 아래에 침방 환혼 강화 두 줄(넷째 몸, 다시 고르기)을 같은 칸(MetaShopRow)으로 붙인다. 침방 화면도 같은 칸을 쓴다.
    // 비용 공식·환불·레벨 상한은 Core Soul(Stats.TrainingCost) 그대로다.
    public sealed class TrainingScreen : MonoBehaviour
    {
        public TMP_FontAsset font;
        [Tooltip("시험용: 이만큼 윤회의 기를 가진 혼으로 연다(0 이면 안 연다)")]
        public int demoQi;

        static readonly Color Paper = new Color(0.91f, 0.87f, 0.78f);
        static readonly Color Ink = new Color(0.12f, 0.10f, 0.09f);

        Soul _soul;
        RectTransform _root;
        TextMeshProUGUI _balance, _refundLabel;
        Button _refund;
        readonly List<MetaShopRow> _rows = new List<MetaShopRow>();

        public Soul Soul => _soul;

        void Start()
        {
            if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            if (demoQi > 0)
            {
                var data = DataBoot.Data;
                Open(Soul.FromJson(data, new Soul(data).ToJson().Replace("\"rebirthQi\": 0", $"\"rebirthQi\": {demoQi}")));
            }
        }

        public void Open(Soul soul)
        {
            _soul = soul;
            if (_root != null) Destroy(_root.gameObject);
            _rows.Clear();
            var go = new GameObject("연무장", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)go.transform;
            var bg = new GameObject("배경", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            bg.transform.SetParent(_root, false);
            bg.color = Paper; bg.raycastTarget = false;
            bg.rectTransform.anchorMin = Vector2.zero; bg.rectTransform.anchorMax = Vector2.one;
            bg.rectTransform.offsetMin = bg.rectTransform.offsetMax = Vector2.zero;

            var title = Text(_root, "연무장 演武場", 44, FontStyles.Bold, Ink);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -64), new Vector2(1000, 60));
            var sub = Text(_root, "혼에 새긴 수련이라 몸이 바뀌어도 남는다", 22, FontStyles.Italic, new Color(Ink.r, Ink.g, Ink.b, 0.55f));
            Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -108), new Vector2(1000, 32));
            _balance = Text(_root, "", 30, FontStyles.Bold, new Color32(176, 132, 40, 255));
            Place(_balance.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(1000, 44));

            var items = soul.Data.Balance.Training.Items;
            int half = (items.Count + 1) / 2;
            for (int i = 0; i < items.Count; i++)
            {
                var row = MetaShopRow.Training(_root, font, soul, items[i], Refresh, 760);
                var rt = row.Rect;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(i < half ? -420 : 420, -220 - (i % half) * 50);
                _rows.Add(row);
            }

            float y = -220 - half * 50 - 20;
            _refund = new GameObject("전부 환불", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button)).GetComponent<Button>();
            _refund.transform.SetParent(_root, false);
            _refund.GetComponent<Image>().color = new Color(0.45f, 0.18f, 0.15f);
            Place((RectTransform)_refund.transform, new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(360, 50));
            _refundLabel = Text(_refund.transform, "", 22, FontStyles.Bold, Paper);
            var lr = _refundLabel.rectTransform; lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = lr.offsetMax = Vector2.zero;
            _refund.onClick.AddListener(() => { if (_soul.TrainingSpent > 0) { _soul.RefundTraining(); Refresh(); } });

            y -= 70;
            var uh = Text(_root, "환혼 강화 <size=75%><alpha=#88>침방<alpha=#FF></size>", 26, FontStyles.Bold, Ink);
            Place(uh.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(800, 40));
            y -= 50;
            int k = 0;
            foreach (var u in soul.Data.Cheonha.Rebirth.Upgrades)
            {
                var row = MetaShopRow.Upgrade(_root, font, soul, u, Refresh, 760);
                row.Rect.anchorMin = row.Rect.anchorMax = new Vector2(0.5f, 1f);
                row.Rect.anchoredPosition = new Vector2(k % 2 == 0 ? -420 : 420, y);
                _rows.Add(row);
                k++;
            }
            Refresh();
        }

        public void Refresh()
        {
            _balance.text = $"윤회의 기 {_soul.RebirthQi:N0}";
            foreach (var r in _rows) r.Refresh();
            _refund.interactable = _soul.TrainingSpent > 0;
            _refundLabel.text = $"전부 환불  <size=80%>(+{_soul.TrainingSpent:N0})</size>";
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

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }
}
