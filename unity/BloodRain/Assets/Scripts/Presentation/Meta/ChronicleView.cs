using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BloodRain.Presentation.Meta
{
    // 천하 연표 화면(CX-01U, 기획서 12장 '천하 확장: 연표 화면'). 웹 합친 판 renderChron 과 같은 표.
    //   가로 = 사건 시각(오름차순), 세로 = 사건 자리(placeKo). 혼이 겪은 사건(+ 이번 생에 겪은 것)만 먹 칸에 이름,
    //   막은 위기는 주홍 '止'. 모르는 칸은 점선 빈 칸. 흐린 소문 칸·이번 생의 목표는 결정 #70 뒤.
    // 객잔 본채와 일시정지 화면이 Open 으로 띄운다. 자기 캔버스(맨 위)를 만들고 Esc·닫기로 스스로 지운다.
    public sealed class ChronicleView : MonoBehaviour
    {
        static ChronicleView _open;
        /// <summary>연표가 떠 있는가. 떠 있는 동안 아래 화면은 Esc 를 먹지 않는다.</summary>
        public static bool IsOpen => _open != null;

        static readonly Color Paper = new Color(0.91f, 0.87f, 0.78f);
        static readonly Color Ink = new Color(0.12f, 0.10f, 0.09f);
        static readonly Color Dash = new Color(0.73f, 0.68f, 0.58f, 0.55f);
        const string Crimson = "#E2685E";

        TMP_FontAsset _font;
        int _openedFrame;

        /// <summary>시험용: 먹으로 찬 칸 수와 전체 사건 수.</summary>
        public static (int known, int total) Count { get; private set; }

        /// <summary>연표를 띄운다. soul 은 혼(겪은 사건·막은 위기), 생 도중이면 그 생의 기록도 더한다.</summary>
        public static ChronicleView Open(Soul soul, TMP_FontAsset font)
        {
            Close();
            var go = new GameObject("연표");
            var v = go.AddComponent<ChronicleView>();
            v._font = font;
            v._openedFrame = Time.frameCount;
            v.Build(soul);
            _open = v;
            return v;
        }

        /// <summary>마지막으로 닫힌 프레임. 같은 Esc 로 아래 화면까지 닫히지 않게 본다.</summary>
        public static int ClosedFrame { get; private set; } = -1;

        public static void Close()
        {
            if (_open != null) { Destroy(_open.gameObject); ClosedFrame = Time.frameCount; }
            _open = null;
        }

        void OnDestroy() { if (_open == this) _open = null; }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && Time.frameCount > _openedFrame && kb.escapeKey.wasPressedThisFrame) Close();
        }

        void Build(Soul soul)
        {
            var data = DataBoot.Data;
            var events = data.Chronicle.Events;
            var known = new HashSet<string>(soul != null ? soul.Chronicle : Enumerable.Empty<string>());
            var blocked = new HashSet<string>(soul != null ? soul.CrisesBlocked : Enumerable.Empty<string>());
            var life = LifeRunner.Instance != null ? LifeRunner.Instance.Life : null;
            if (life != null && life.Log != null)
            {
                known.UnionWith(life.Log.Chronicle);
                blocked.UnionWith(life.Log.CrisesBlocked);
            }
            var times = events.Select(e => e.AtSeconds).Distinct().OrderBy(t => t).ToList();
            var places = new List<string>();
            foreach (var e in events.OrderBy(e => e.AtSeconds)) if (!places.Contains(e.PlaceKo)) places.Add(e.PlaceKo);
            Count = (events.Count(e => known.Contains(e.Id)), events.Count);

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            var root = (RectTransform)transform;

            var dim = Rect("어둠", root, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
            dim.color = new Color(0, 0, 0, 0.6f);

            float labelW = 170f;
            float cellW = Mathf.Clamp((1640f - labelW) / Mathf.Max(1, times.Count), 90f, 170f);
            float cellH = Mathf.Clamp(700f / Mathf.Max(1, places.Count), 44f, 74f);   // 자리 11줄이면 63 — 판이 1080 안에 든다
            float tableW = labelW + cellW * times.Count;
            float tableH = 34f + cellH * places.Count;

            var panel = Rect("판", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            panel.sizeDelta = new Vector2(Mathf.Max(900f, tableW + 80f), tableH + 220f);
            panel.gameObject.AddComponent<Image>().color = Paper;   // 0.98 이면 일시정지 위에서 땅 이름 큰 글이 비쳤다

            var title = Text(panel, Loc.T("ui.chronicle.title"), 40, FontStyles.Bold, Ink);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -46), new Vector2(1200, 56));
            var sum = Text(panel, $"<alpha=#99>{Loc.F("ui.chronicle.sum", Count.known, Count.total)}<alpha=#FF>", 22, FontStyles.Italic, Ink);
            Place(sum.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -92), new Vector2(1400, 32));

            var table = Rect("표", panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            table.pivot = new Vector2(0.5f, 1f);
            table.sizeDelta = new Vector2(tableW, tableH);
            table.anchoredPosition = new Vector2(0, -124);
            float x0 = -tableW / 2f;

            for (int c = 0; c < times.Count; c++)
            {
                int t = Mathf.RoundToInt(times[c]);
                var h = Text(table, $"{t / 60}:{t % 60:00}", 20, FontStyles.Normal, Ink);
                Cell(h.rectTransform, x0 + labelW + cellW * c, 0, cellW, 34);
            }
            for (int r = 0; r < places.Count; r++)
            {
                float y = 34f + cellH * r;
                var any = events.First(e => e.PlaceKo == places[r]);
                var pl = Text(table, Loc.T($"chronicle.events.{any.Id}.place", places[r]), 20, FontStyles.Normal, Ink);
                pl.alignment = TextAlignmentOptions.Right;
                Cell(pl.rectTransform, x0, y, labelW - 10, cellH);
                for (int c = 0; c < times.Count; c++)
                {
                    var box = Rect("칸", table, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
                    Cell(box, x0 + labelW + cellW * c + 2, y + 2, cellW - 4, cellH - 4);
                    var here = events.Where(e => e.PlaceKo == places[r] && Mathf.Approximately(e.AtSeconds, times[c])).ToList();
                    var seen = here.Where(e => known.Contains(e.Id)).ToList();
                    var img = box.gameObject.AddComponent<Image>();
                    img.raycastTarget = false;
                    if (seen.Count == 0) { img.color = Dash; continue; }
                    img.color = new Color(0.10f, 0.10f, 0.10f, 0.95f);
                    var txt = string.Join("\n", seen.Select(e =>
                        Loc.T($"chronicle.events.{e.Id}.name", e.NameKo) + (blocked.Contains(e.Id) ? $" <color={Crimson}><b>止</b></color>" : "")));
                    var lab = Text(box, txt, 15, FontStyles.Normal, new Color(0.94f, 0.90f, 0.82f));
                    var lrt = lab.rectTransform; lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
                    lrt.offsetMin = new Vector2(5, 3); lrt.offsetMax = new Vector2(-5, -3);
                    lab.textWrappingMode = TextWrappingModes.Normal;
                    lab.enableAutoSizing = true; lab.fontSizeMin = 10; lab.fontSizeMax = 15;
                }
            }

            var close = new GameObject("닫기", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            close.transform.SetParent(panel, false);
            close.GetComponent<Image>().color = new Color(0.25f, 0.20f, 0.17f);
            Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0, 50), new Vector2(260, 54));
            var cl = Text(close.transform, Loc.T("ui.chronicle.close"), 24, FontStyles.Bold, Paper);
            var crt = cl.rectTransform; crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one; crt.offsetMin = crt.offsetMax = Vector2.zero;
            close.GetComponent<Button>().onClick.AddListener(Close);
        }

        static void Cell(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        TextMeshProUGUI Text(Transform parent, string text, float size, FontStyles style, Color color)
        {
            var go = new GameObject("글", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (_font != null) t.font = _font;
            t.text = text; t.fontSize = size; t.fontStyle = style; t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }
    }
}
