using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Meta;
using BloodRain.Core.World;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BloodRain.Presentation.Meta
{
    // 장경각 화면(S-04, 기획서 10·20장 '찢긴 비급'). 객잔에서 연다.
    //   위: 윤회의 기·봉인석 잔고, 다음 복원 값(몇 번째 복원인지로 오른다, synergies.json secretManuals).
    //   아래: 비급 층 시너지 전부를 도감처럼 두 줄로. 찢긴 것은 이름·설명과 복원 버튼, 복원한 것은 금빛 '복원함',
    //   아직 못 본 것은 '?' 로 가린다(조건을 갖춘 채 생을 마치면 오른다).
    // 복원 조건·값은 Core Soul.CanRestore·TryRestore·NextRestoreCost 그대로다. 봉인석은 쓰지 않고 지닌 수만 본다.
    public sealed class LibraryScreen : MonoBehaviour
    {
        public TMP_FontAsset font;
        [Tooltip("시험용: 찢긴 비급 셋, 윤회의 기 9,000, 봉인석 셋을 가진 혼으로 연다")]
        public bool demo;

        static readonly Color Paper = new Color(0.91f, 0.87f, 0.78f);
        static readonly Color Ink = new Color(0.12f, 0.10f, 0.09f);
        static readonly Color Crimson = new Color32(179, 38, 30, 255);
        static readonly Color Gold = new Color32(176, 132, 40, 255);

        Soul _soul;
        RectTransform _root;
        TextMeshProUGUI _balance, _cost;
        readonly List<Row> _rows = new List<Row>();

        sealed class Row
        {
            public string Id;
            public TextMeshProUGUI Name, Desc, State;
            public Button Restore;
            public TextMeshProUGUI RestoreLabel;
            public Image Back;
        }

        public Soul Soul => _soul;

        void Start()
        {
            if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            if (demo) Open(DemoSoul());
        }

        public void Open(Soul soul)
        {
            _soul = soul;
            if (_root != null) Destroy(_root.gameObject);
            _rows.Clear();
            var go = new GameObject("장경각", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)go.transform;
            var bg = Panel(_root, Paper);
            bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one; bg.offsetMin = bg.offsetMax = Vector2.zero;

            var title = Text(_root, Loc.T("ui.library.title"), 44, FontStyles.Bold, Ink);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -64), new Vector2(1000, 60));
            var sub = Text(_root, Loc.T("ui.library.sub"), 22, FontStyles.Italic, new Color(Ink.r, Ink.g, Ink.b, 0.55f));
            Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -108), new Vector2(1200, 32));
            _balance = Text(_root, "", 28, FontStyles.Bold, Gold);
            Place(_balance.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(1200, 40));
            _cost = Text(_root, "", 22, FontStyles.Normal, new Color(Ink.r, Ink.g, Ink.b, 0.75f));
            Place(_cost.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -188), new Vector2(1200, 32));

            var ids = soul.Data.Synergies.SecretManualIds.ToList();
            int half = (ids.Count + 1) / 2;
            for (int i = 0; i < ids.Count; i++)
            {
                var pos = new Vector2(i < half ? -440 : 440, -260 - (i % half) * 92);
                _rows.Add(MakeRow(ids[i], pos));
            }
            Refresh();
        }

        Row MakeRow(string id, Vector2 pos)
        {
            var r = new Row { Id = id };
            var back = Panel(_root, Color.clear);
            Place(back, new Vector2(0.5f, 1f), pos, new Vector2(840, 82));
            r.Back = back.GetComponent<Image>();
            r.Name = Text(back, "", 26, FontStyles.Bold, Ink);
            r.Name.alignment = TextAlignmentOptions.Left;
            Place(r.Name.rectTransform, new Vector2(0f, 1f), new Vector2(16 + 290, -22), new Vector2(580, 34));
            r.Desc = Text(back, "", 17, FontStyles.Normal, new Color(Ink.r, Ink.g, Ink.b, 0.7f));
            r.Desc.alignment = TextAlignmentOptions.TopLeft;
            r.Desc.textWrappingMode = TextWrappingModes.Normal;
            r.Desc.overflowMode = TextOverflowModes.Ellipsis;
            Place(r.Desc.rectTransform, new Vector2(0f, 1f), new Vector2(16 + 300, -60), new Vector2(600, 40));
            r.State = Text(back, "", 22, FontStyles.Bold, Gold);
            Place(r.State.rectTransform, new Vector2(1f, 0.5f), new Vector2(-100, 0), new Vector2(180, 40));

            r.Restore = new GameObject("복원", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button)).GetComponent<Button>();
            r.Restore.transform.SetParent(back, false);
            r.Restore.GetComponent<Image>().color = new Color(0.45f, 0.18f, 0.15f);
            Place((RectTransform)r.Restore.transform, new Vector2(1f, 0.5f), new Vector2(-100, 0), new Vector2(180, 52));
            r.RestoreLabel = Text(r.Restore.transform, "", 21, FontStyles.Bold, Paper);
            var lr = r.RestoreLabel.rectTransform; lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = lr.offsetMax = Vector2.zero;
            r.Restore.onClick.AddListener(() => TryRestore(id));
            return r;
        }

        /// <summary>버튼과 같은 일. 시험에서도 부른다.</summary>
        public bool TryRestore(string manualId)
        {
            if (_soul == null || !_soul.TryRestore(manualId)) return false;
            Refresh();
            return true;
        }

        public void Refresh()
        {
            var (stones, qi) = _soul.NextRestoreCost;
            _balance.text = Loc.F("ui.library.balance", _soul.RebirthQi.ToString("N0"), _soul.SealStoneCount);
            int torn = _soul.TornManuals.Count, restored = _soul.RestoredManuals.Count;
            _cost.text = Loc.F("ui.library.cost", torn, _rows.Count, restored, stones, qi.ToString("N0"));
            foreach (var r in _rows)
            {
                bool isTorn = _soul.TornManuals.Contains(r.Id);
                bool isRestored = _soul.IsRestored(r.Id);
                var (name, hanja, desc) = ManualNames.Of(_soul.Data, r.Id);
                if (!isTorn)
                {
                    r.Name.text = $"?  <size=70%><alpha=#88>{Loc.T("ui.library.unseen")}<alpha=#FF></size>";
                    r.Name.color = new Color(Ink.r, Ink.g, Ink.b, 0.45f);
                    r.Desc.text = Loc.T("ui.library.unseen_desc");
                    r.Back.color = new Color(Ink.r, Ink.g, Ink.b, 0.04f);
                    r.State.text = "";
                    r.Restore.gameObject.SetActive(false);
                    continue;
                }
                r.Name.text = string.IsNullOrEmpty(hanja) ? name : $"{name}  <size=70%><alpha=#99>{hanja}<alpha=#FF></size>";
                r.Name.color = isRestored ? Ink : Crimson;
                r.Desc.text = desc;
                r.Back.color = isRestored ? new Color(Gold.r, Gold.g, Gold.b, 0.14f) : new Color(Crimson.r, Crimson.g, Crimson.b, 0.07f);
                r.State.text = isRestored ? Loc.T("ui.library.restored") : "";
                r.Restore.gameObject.SetActive(!isRestored);
                if (!isRestored)
                {
                    bool can = _soul.CanRestore(r.Id);
                    r.Restore.interactable = can;
                    string why = _soul.SealStoneCount < stones ? Loc.F("ui.library.need_stones", stones) : $"{qi:N0}";
                    r.RestoreLabel.text = $"{Loc.T("ui.library.restore")}  <size=80%>{why}</size>";
                    r.RestoreLabel.alpha = can ? 1f : 0.55f;
                }
            }
        }

        Soul DemoSoul()
        {
            var data = DataBoot.Data;
            static string List(IEnumerable<string> ids) => "[" + string.Join(", ", ids.Select(id => $"\"{id}\"")) + "]";
            var stones = new Regions(data).All.Where(r => !string.IsNullOrEmpty(r.SealStone)).Take(3).Select(r => r.Id);
            var torn = data.Synergies.SecretManualIds.Where((_, i) => i % 3 == 0).Take(4);
            var json = new Soul(data).ToJson()
                .Replace("\"rebirthQi\": 0", "\"rebirthQi\": 9000")
                .Replace("\"prologueDone\": false", "\"prologueDone\": true")
                .Replace("\"sealStones\": []", "\"sealStones\": " + List(stones))
                .Replace("\"tornManuals\": []", "\"tornManuals\": " + List(torn));
            return Soul.FromJson(data, json);
        }

        // ---- UI 조각 ----

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static RectTransform Panel(Transform parent, Color color)
        {
            var go = new GameObject("면", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color; img.raycastTarget = false;
            return (RectTransform)go.transform;
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
    }
}
