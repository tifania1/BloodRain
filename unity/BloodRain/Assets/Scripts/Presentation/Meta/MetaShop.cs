using System;
using System.Collections.Generic;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BloodRain.Presentation.Meta
{
    // 윤회의 기로 사는 칸 한 줄(TU-14). 연무장 20항목과 침방 아래 환혼 강화 두 줄이 같은 칸을 쓴다.
    //   [이름] [레벨 점 ●●○] [효과] [다음 비용 버튼]. 최대면 버튼 대신 '다 익힘', 모자라면 버튼이 흐려진다.
    // 값(레벨·비용·사기)은 모두 Core Soul 이 정한다. 칸은 그리기와 누르기만.
    public sealed class MetaShopRow
    {
        // 능력치 이름은 strings/ko.json 의 ui.stat.<이름>(Loc.Stat).
        static readonly HashSet<string> Flat = new HashSet<string>
            { "Armor", "Recovery", "Amount", "Luck", "DashCharges", "Revival", "Rerolls", "Banishes", "Skips" };
        // 기획서 10장 표처럼 줄어드는 쪽으로 보이는 능력치
        static readonly HashSet<string> Negative = new HashSet<string> { "CooldownReduction" };

        static readonly Color Ink = new Color(0.12f, 0.10f, 0.09f);
        static readonly Color Paper = new Color(0.91f, 0.87f, 0.78f);
        static readonly Color Gold = new Color32(176, 132, 40, 255);

        readonly Func<int> _level;
        readonly int _max;
        readonly Func<int?> _cost;
        readonly Func<int> _balance;
        readonly Image[] _pips;
        readonly Button _buy;
        readonly TextMeshProUGUI _buyLabel;

        public RectTransform Rect { get; }

        MetaShopRow(Transform parent, TMP_FontAsset font, string name, string effect, int max, Func<int> level, Func<int?> cost,
            Func<int> balance, Func<bool> buy, Action changed, float width)
        {
            _level = level; _max = max; _cost = cost; _balance = balance;
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Rect = (RectTransform)go.transform;
            Rect.sizeDelta = new Vector2(width, 40);

            var n = Text(Rect, font, name, 22, FontStyles.Bold, Ink);
            Put(n.rectTransform, new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(150, 36), 0f);
            // 영어 'Projectile Speed'·'Lightfoot Charges' 가 점 칸을 덮지 않게(L-01b3) 한 줄로 줄여 맞춘다
            n.textWrappingMode = TextWrappingModes.NoWrap;
            n.enableAutoSizing = true; n.fontSizeMin = 13; n.fontSizeMax = 22;
            _pips = new Image[max];
            for (int i = 0; i < max; i++)
            {
                var p = new GameObject("점", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
                p.transform.SetParent(Rect, false);
                p.raycastTarget = false;
                Put(p.rectTransform, new Vector2(0, 0.5f), new Vector2(166 + i * 16, 0), new Vector2(11, 11), 0f);
                _pips[i] = p;
            }
            var e = Text(Rect, font, effect, 19, FontStyles.Normal, new Color(Ink.r, Ink.g, Ink.b, 0.75f));
            Put(e.rectTransform, new Vector2(0, 0.5f), new Vector2(256, 0), new Vector2(width - 256 - 130, 36), 0f);
            e.textWrappingMode = TextWrappingModes.NoWrap;
            e.enableAutoSizing = true; e.fontSizeMin = 12; e.fontSizeMax = 19;

            _buy = new GameObject("사기", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button)).GetComponent<Button>();
            _buy.transform.SetParent(Rect, false);
            _buy.GetComponent<Image>().color = new Color(0.25f, 0.20f, 0.17f);
            Put((RectTransform)_buy.transform, new Vector2(1, 0.5f), new Vector2(-4, 0), new Vector2(120, 34), 1f);
            _buyLabel = Text(_buy.transform, font, "", 19, FontStyles.Bold, Paper);
            _buyLabel.alignment = TextAlignmentOptions.Center;
            var lr = _buyLabel.rectTransform; lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = lr.offsetMax = Vector2.zero;
            _buy.onClick.AddListener(() => { if (buy()) changed?.Invoke(); });
            Refresh();
        }

        /// <summary>연무장 한 항목.</summary>
        public static MetaShopRow Training(Transform parent, TMP_FontAsset font, Soul soul, TrainingItemDef item, Action changed, float width = 600) =>
            new MetaShopRow(parent, font, Loc.Stat(item.Stat), Loc.F("ui.shop.per_level", FormatPerLevel(item.Stat, item.PerLevel)),
                soul.TrainingMax(item.Stat), () => soul.TrainingLevel(item.Stat), () => soul.TrainingCost(item.Stat), () => soul.RebirthQi,
                () => soul.TryBuyTraining(item.Stat), changed, width);

        /// <summary>환혼 강화 한 줄(넷째 몸, 다시 고르기, 바라는 문파).</summary>
        public static MetaShopRow Upgrade(Transform parent, TMP_FontAsset font, Soul soul, RebirthUpgradeDef u, Action changed, float width = 600)
        {
            var (key, amount) = Soul.ParseEffect(u.Effect);
            string effect = key == "candidates" ? Loc.F("ui.shop.candidates", amount) : key == "rerolls" ? Loc.F("ui.shop.rerolls", amount)
                : key == "pin" ? Loc.T("ui.shop.pin") : u.Effect;
            return new MetaShopRow(parent, font, Loc.Upgrade(u), effect, u.MaxLevel, () => soul.UpgradeLevel(u.Id), () => soul.UpgradeCost(u.Id),
                () => soul.RebirthQi, () => soul.TryBuyUpgrade(u.Id), changed, width);
        }

        public static string FormatPerLevel(string stat, float v)
        {
            string sign = Negative.Contains(stat) ? "−" : "+";
            if (stat == "Recovery") return Loc.F("ui.per_second", $"{sign}{v:0.##}");
            if (Flat.Contains(stat)) return $"{sign}{v:0.##}";
            return $"{sign}{v * 100:0.#}%";
        }

        public void Refresh()
        {
            int lv = _level();
            for (int i = 0; i < _pips.Length; i++) _pips[i].color = i < lv ? Gold : new Color(Ink.r, Ink.g, Ink.b, 0.18f);
            var c = _cost();
            _buy.interactable = c.HasValue && c.Value <= _balance();
            _buyLabel.text = c.HasValue ? $"{c.Value:N0}" : Loc.T("ui.shop.maxed");
            _buy.GetComponent<Image>().color = c.HasValue ? new Color(0.25f, 0.20f, 0.17f) : new Color(Gold.r, Gold.g, Gold.b, 0.5f);
        }

        static TextMeshProUGUI Text(Transform parent, TMP_FontAsset font, string text, float size, FontStyles style, Color color)
        {
            var go = new GameObject("글", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text; t.fontSize = size; t.fontStyle = style; t.color = color;
            t.alignment = TextAlignmentOptions.Left;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }

        static void Put(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, float pivotX)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(pivotX, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }
}
