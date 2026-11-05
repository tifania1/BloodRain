using System.Collections.Generic;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using BloodRain.Core.Run;
using BloodRain.Presentation.Fx;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BloodRain.Presentation.Hud
{
    // RM-05U 보부상 화면(기획서 7장 '보부상', balance.json merchant). 생성 디렉터의 보부상 사건(12·24분)에서 LifeRunner 가 연다.
    // 열리면 세상 시간을 멈추고, 품목 셋(이름·값·남은 수)과 지금 은자를 보인다. 사기는 Core MerchantVisit.Buy 가 하고
    // 효과는 여기서 건다: 회복약은 PotionHeal 만큼 기혈, 취기부는 화면 안 진기 구슬 끌기. 다시 고르기 권은 Core 가 Charges 에 더한다.
    // 은자가 모자라거나 다 산 품목은 버튼을 흐리게. 닫으면 시간을 연 때 값으로 되돌린다.
    public sealed class MerchantScreen : MonoBehaviour
    {
        public TMP_FontAsset font;

        static readonly Color Paper = new Color(0.91f, 0.87f, 0.78f, 0.97f);
        static readonly Color Ink = new Color(0.12f, 0.10f, 0.09f);
        static readonly Color Gold = new Color32(176, 132, 40, 255);

        Life _life;
        MerchantVisit _visit;
        PlayerVitals _vitals;
        OrbLayer _orbs;
        float _prevScale = 1f;
        RectTransform _root;
        TextMeshProUGUI _silver;
        readonly List<(MerchantItemDef item, Button button, TextMeshProUGUI label)> _rows = new List<(MerchantItemDef, Button, TextMeshProUGUI)>();

        public bool IsOpen => _root != null;
        public MerchantVisit Visit => _visit;

        public void Open(Life life, GameData data, PlayerVitals vitals, OrbLayer orbs)
        {
            if (IsOpen) return;
            _life = life; _vitals = vitals; _orbs = orbs;
            _visit = new MerchantVisit(data.Balance.Merchant);
            _prevScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Build();
            Refresh();
        }

        public void Close()
        {
            if (_root != null) Destroy(_root.gameObject);
            _root = null;
            _rows.Clear();
            Time.timeScale = _prevScale;
        }

        void Build()
        {
            var go = new GameObject("보부상", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)go.transform;

            var dim = Panel(_root, new Color(0f, 0f, 0f, 0.45f));
            Stretch(dim.rectTransform);
            var card = Panel(_root, Paper);
            Place(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 520));

            var title = Text(card.transform, Loc.T("ui.merchant.title"), 42, FontStyles.Bold, Ink);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -52), new Vector2(700, 56));
            var sub = Text(card.transform, Loc.T("ui.merchant.sub"), 22, FontStyles.Italic, new Color(Ink.r, Ink.g, Ink.b, 0.6f));
            Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -96), new Vector2(700, 30));
            _silver = Text(card.transform, "", 28, FontStyles.Bold, Gold);
            Place(_silver.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -140), new Vector2(700, 40));

            float y = -210;
            foreach (var item in _visit.Items)
            {
                var b = new GameObject(item.Id, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button)).GetComponent<Button>();
                b.transform.SetParent(card.transform, false);
                b.GetComponent<Image>().color = new Color(0.30f, 0.22f, 0.16f);
                var cb = b.colors; cb.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.35f); b.colors = cb;
                Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(640, 70));
                var label = Text(b.transform, "", 26, FontStyles.Normal, Paper);
                Stretch(label.rectTransform);
                label.enableAutoSizing = true; label.fontSizeMin = 16; label.fontSizeMax = 26;
                var it = item;
                b.onClick.AddListener(() => Buy(it));
                _rows.Add((item, b, label));
                y -= 86;
            }

            var close = new GameObject("떠나기", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button)).GetComponent<Button>();
            close.transform.SetParent(card.transform, false);
            close.GetComponent<Image>().color = new Color(0.45f, 0.18f, 0.15f);
            Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0, 50), new Vector2(300, 54));
            var cl = Text(close.transform, Loc.T("ui.merchant.leave"), 24, FontStyles.Bold, Paper);
            Stretch(cl.rectTransform);
            close.onClick.AddListener(Close);
        }

        void Buy(MerchantItemDef item)
        {
            var r = _visit.Buy(item.Id, _life);
            Debug.Log($"[MerchantScreen] {item.Id} → {r} · 남은 은자 {_life.Silver}");
            if (r == MerchantBuy.Bought)
            {
                if (item.Id == MerchantVisit.Potion && _vitals != null)
                    // 회복 배율(마교 절반)은 PlayerVitals.Heal 이 healingMultiplier 로 곱한다. 여기선 1 을 넘긴다
                    _vitals.Heal(MerchantVisit.PotionHeal(item, _vitals.MaxHealth));
                else if (item.Id == MerchantVisit.ChwigiTalisman && _orbs != null && _orbs.Field != null)
                    _orbs.Field.PullAll(); // 등롱 취기부와 같은 길: 화면 안 구슬을 끌어온다
            }
            Refresh();
        }

        void Refresh()
        {
            if (_silver == null) return;
            _silver.text = Loc.F("ui.merchant.silver", _life.Silver.ToString("N0"));
            foreach (var (item, button, label) in _rows)
            {
                int left = _visit.Left(item.Id);
                string name = Loc.T($"balance.merchant.items.{item.Id}.name", item.NameKo);
                label.text = $"{name}   <color=#E8C468>{Loc.F("ui.merchant.price", item.Price.ToString("N0"))}</color>   <size=80%>{Loc.F("ui.merchant.left", left)}</size>";
                button.interactable = _visit.CanBuy(item.Id, _life.Silver);
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
