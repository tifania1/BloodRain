using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BloodRain.Presentation.Meta
{
    // 수련동 도전 과제 목록(EL-07U, 2026-11-20 — 기획서 10장 '객잔 시설' 수련동, Core CaveList). 객잔 본채 '수련동'이 연다.
    //   CaveList.Rows 차례대로 한 줄씩: 조건 글(ui.cave.*) + 진행(있으면) → 보상 글. 이룬 줄은 ✓, 못 이룬 줄은 흐리게. 보기만 한다.
    //   줄이 많아 끌어 내리는 목록(마우스 휠). Esc·'객잔으로'로 닫힘.
    public sealed class CaveScreen : MonoBehaviour
    {
        public TMP_FontAsset font;

        static readonly Color Paper = new Color(0.91f, 0.87f, 0.78f);
        static readonly Color Ink = new Color(0.12f, 0.10f, 0.09f);

        RectTransform _root;
        public event System.Action Closed;
        public bool IsOpen => _root != null;
        public int RowCount { get; private set; }

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
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Close();
        }

        public void Open(Soul soul)
        {
            if (_root != null) Destroy(_root.gameObject);
            var go = new GameObject("수련동", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)go.transform;
            var bg = Img(_root, Paper, false);
            Stretch(bg.rectTransform);
            InkUi.PaperBackdrop(_root);   // 2026-11-21 선지 결·가장자리 어둡게

            var rows = CaveList.Rows(soul);
            int done = rows.Count(r => r.Done);
            Place(Text(_root, Loc.T("ui.cave.title"), 44, FontStyles.Bold, Ink).rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -64), new Vector2(1000, 60));
            Place(Text(_root, Loc.F("ui.cave.sub", done, rows.Count), 22, FontStyles.Italic, new Color(Ink.r, Ink.g, Ink.b, 0.6f)).rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -108), new Vector2(1200, 32));

            // 끌어 내리는 목록
            var view = new GameObject("목록", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask), typeof(ScrollRect));
            view.transform.SetParent(_root, false);
            var vr = (RectTransform)view.transform;
            vr.anchorMin = new Vector2(0.5f, 0f); vr.anchorMax = new Vector2(0.5f, 1f);
            vr.pivot = new Vector2(0.5f, 1f);
            vr.sizeDelta = new Vector2(1500, -200); vr.anchoredPosition = new Vector2(0, -150);
            view.GetComponent<Image>().color = new Color(1, 1, 1, 0.01f);
            view.GetComponent<Mask>().showMaskGraphic = false;
            var content = new GameObject("줄들", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(view.transform, false);
            var cr = (RectTransform)content.transform;
            cr.anchorMin = new Vector2(0f, 1f); cr.anchorMax = new Vector2(1f, 1f); cr.pivot = new Vector2(0.5f, 1f);
            cr.offsetMin = cr.offsetMax = Vector2.zero;
            var vl = content.GetComponent<VerticalLayoutGroup>();
            vl.spacing = 6; vl.childControlHeight = true; vl.childControlWidth = true; vl.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = view.GetComponent<ScrollRect>();
            scroll.content = cr; scroll.horizontal = false; scroll.scrollSensitivity = 40f; scroll.movementType = ScrollRect.MovementType.Clamped;

            var data = soul.Data;
            foreach (var row in rows)
            {
                var line = Img(content.transform, row.Done ? new Color(0.24f, 0.19f, 0.15f, 0.92f) : new Color(Ink.r, Ink.g, Ink.b, 0.07f), false);
                line.gameObject.AddComponent<LayoutElement>().preferredHeight = 58;
                string cond = Loc.F(row.ConditionKey, Arg(data, row));
                if (row.Progress is int p && row.Target is int t) cond += " " + Loc.F("ui.cave.progress", Mathf.Min(p, t), t);
                string reward = string.Join(" · ", row.Rewards.Select(r => Reward(data, r)));
                var txt = Text(line.transform, $"{(row.Done ? "● " : "○ ")}{cond}   <size=80%><alpha=#AA>→ {reward}</size>", 24, FontStyles.Normal,
                    row.Done ? Paper : new Color(Ink.r, Ink.g, Ink.b, 0.55f));
                txt.alignment = TextAlignmentOptions.MidlineLeft;
                var tr = txt.rectTransform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = new Vector2(24, 0); tr.offsetMax = new Vector2(-24, 0);
            }
            RowCount = rows.Count;
            InnBack.Add(_root, font, Close);
        }

        /// <summary>조건 글 자리 {0}: 땅의 주인 이름, 목표 수, 문파 수 등.</summary>
        static object Arg(GameData data, CaveRow row)
        {
            switch (row.Kind)
            {
                case CaveRowKind.BossFirstKill:
                case CaveRowKind.Costume:
                    var bd = data.Bosses.FirstOrDefault(b => b.Id == row.Subject);
                    return bd != null ? Loc.Boss(bd) : row.Subject;
                default:
                    return row.Target?.ToString() ?? "";
            }
        }

        static string Reward(GameData data, CaveReward r)
        {
            var C = data.Cheonha;
            switch (r.Kind)
            {
                case CaveRewardKind.TrainingHallAndRebirth: return Loc.T("ui.cave.reward_training_rebirth");
                case CaveRewardKind.Sect: { var s = C.Sects.FirstOrDefault(x => x.Id == r.Id); return Loc.F("ui.cave.reward_sect", s != null ? Loc.Sect(s) : r.Id); }
                case CaveRewardKind.Leader: { var c = data.Characters.FirstOrDefault(x => x.Id == r.Id); return Loc.F("ui.cave.reward_leader", c != null ? Loc.Character(c) : r.Id); }
                case CaveRewardKind.Weapon: { var w = data.Weapons.FirstOrDefault(x => x.Id == r.Id); return w != null ? Loc.Weapon(w) : r.Id; }
                case CaveRewardKind.SealStone: return Loc.T("ui.cave.reward_seal_stone");
                case CaveRewardKind.Title: return Loc.F("ui.cave.reward_title", Loc.T($"cheonha.titles.{r.Id}.name", C.Titles.FirstOrDefault(t => t.Id == r.Id)?.NameKo ?? r.Id));
                case CaveRewardKind.Insight: return Loc.F("ui.cave.reward_insight", Loc.T($"insights.{r.Id}.name", data.Insights.FirstOrDefault(i => i.Id == r.Id)?.NameKo ?? r.Id));
                case CaveRewardKind.Costume:
                {
                    var cos = C.Costumes.FirstOrDefault(x => x.Id == r.Id);
                    var ch = cos != null ? data.Characters.FirstOrDefault(x => x.Id == cos.Character) : null;
                    return Loc.F("ui.cave.reward_costume", ch != null ? Loc.Character(ch) : r.Id);
                }
                case CaveRewardKind.TrainingMax: return Loc.F("ui.cave.reward_training_max", Loc.Stat(r.Id), r.Amount);
                default: return Loc.T("ui.cave.reward_true_ending");
            }
        }

        Image Img(Transform parent, Color c, bool raycast)
        {
            var go = new GameObject("그림", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var i = go.GetComponent<Image>();
            i.color = c; i.raycastTarget = raycast;
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

        static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }
}
