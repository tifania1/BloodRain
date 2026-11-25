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
    // 연무장 화면(TU-14, 기획서 10장 '연무장'·'환혼 강화'·'경맥도'). 윤회의 기 잔고, 연무장 20항목, 사기와 전부 환불.
    // GU-04U(2026-11-20): 목록 대신 경맥도 — 왼쪽에 앉은 운기 자세 사람(2026-11-25 원화 ui_meridian_chart, 없으면 코드로 그린 옅은 먹 윤곽) 위 혈자리 20곳.
    //   혈마다 둘레 점(최대 레벨만큼, 뚫은 만큼 금빛), 다 뚫으면 혈이 금빛. 큰 혈 셋(관원·노궁·용천)은 두 배 크기, 뚫을 때 그 경맥 줄이 한 번 빛남.
    //   혈을 누르면(방향키로 옮기고 Enter 로 사기) 오른쪽에 혈 이름과 지금 목록과 같은 칸(MetaShopRow: 이름·점·레벨당 값·비용 단추).
    //   아래에 전부 환불과 침방 환혼 강화 줄(MetaShopRow)은 그대로. 비용 공식·환불·레벨 상한은 Core Soul(Stats.TrainingCost) 그대로다.
    public sealed class TrainingScreen : MonoBehaviour
    {
        public TMP_FontAsset font;
        /// <summary>연무장 그림(bg_training_yard, InnFlow → InnScreen 이 넣는다). 비면 종이만.</summary>
        public Sprite background;
        [Tooltip("시험용: 이만큼 윤회의 기를 가진 혼으로 연다(0 이면 안 연다)")]
        public int demoQi;

        static readonly Color Paper = new Color(0.91f, 0.87f, 0.78f);
        static readonly Color Ink = new Color(0.12f, 0.10f, 0.09f);

        Soul _soul;
        RectTransform _root;
        TextMeshProUGUI _balance, _refundLabel;
        Button _refund;
        readonly List<MetaShopRow> _rows = new List<MetaShopRow>();

        // ── 경맥도(기획서 10장 표 그대로: 항목 → 혈자리, 자리는 그림 상자 안 0~1, y 는 아래로) ──
        // 코드로 그린 윤곽용 자리(원화가 없을 때)
        static readonly (string stat, float x, float y)[] FigurePoints =
        {
            ("Growth", 0.50f, 0.045f), ("Luck", 0.50f, 0.125f), ("Rerolls", 0.585f, 0.175f),
            ("UltimateCharge", 0.50f, 0.30f), ("Magnet", 0.50f, 0.39f), ("MaxHealth", 0.50f, 0.48f), ("Revival", 0.50f, 0.555f),
            ("Armor", 0.43f, 0.215f), ("Skips", 0.585f, 0.47f), ("Greed", 0.385f, 0.43f),
            ("Area", 0.34f, 0.245f), ("ProjectileSpeed", 0.235f, 0.40f), ("CooldownReduction", 0.255f, 0.50f),
            ("Duration", 0.28f, 0.555f), ("Might", 0.335f, 0.595f), ("Amount", 0.385f, 0.635f),
            ("MoveSpeed", 0.67f, 0.60f), ("Recovery", 0.75f, 0.71f), ("Banishes", 0.64f, 0.80f), ("DashCharges", 0.53f, 0.845f),
        };
        // 원화 ui_meridian_chart(1003×1024) 위 자리 — 그림 전체 0~1, y 아래로(2026-11-25 그림을 보고 맞춤:
        //   백회는 정수리, 인당은 미간, 풍지는 귀 뒤, 임맥 넷은 그림의 가운데 줄, 대추·명문은 몸을 튼 오른쪽 목덜미·허리,
        //   팔 여섯은 왼팔 어깨에서 손바닥, 다리 넷은 오른 엉덩이에서 오른발 발바닥)
        static readonly (string stat, float x, float y)[] ArtPoints =
        {
            ("Growth", 0.489f, 0.125f), ("Luck", 0.436f, 0.188f), ("Rerolls", 0.546f, 0.227f),
            ("UltimateCharge", 0.496f, 0.386f), ("Magnet", 0.496f, 0.454f), ("MaxHealth", 0.496f, 0.547f), ("Revival", 0.496f, 0.607f),
            ("Armor", 0.560f, 0.295f), ("Skips", 0.610f, 0.564f), ("Greed", 0.437f, 0.503f),
            ("Area", 0.399f, 0.327f), ("ProjectileSpeed", 0.304f, 0.571f), ("CooldownReduction", 0.264f, 0.617f),
            ("Duration", 0.229f, 0.645f), ("Might", 0.152f, 0.652f), ("Amount", 0.199f, 0.693f),
            ("MoveSpeed", 0.678f, 0.684f), ("Recovery", 0.758f, 0.757f), ("Banishes", 0.688f, 0.793f), ("DashCharges", 0.618f, 0.828f),
        };
        /// <summary>경맥도 원화(Resources/Art/UI/ui_meridian_chart). 없으면 코드로 그린 윤곽과 그 자리.</summary>
        static Sprite ChartArt => _chartArtLoaded ? _chartArt : LoadChartArt();
        static Sprite _chartArt; static bool _chartArtLoaded;
        static Sprite LoadChartArt() { _chartArtLoaded = true; return _chartArt = Resources.Load<Sprite>("Art/UI/ui_meridian_chart"); }
        static (string stat, float x, float y)[] Points => ChartArt != null ? ArtPoints : FigurePoints;
        // 경맥 줄 색 — 원화가 있으면 그림의 남빛 경맥 줄과 겹쳐 지저분해 옅게(2026-11-25 PC GU-04Ua: 0.35 → 0.18)
        static Color LineColor => new Color(0.55f, 0.16f, 0.12f, ChartArt != null ? 0.18f : 0.35f);
        static readonly HashSet<string> Big = new HashSet<string> { "Revival", "Amount", "DashCharges" };
        // 경맥 줄(차례대로 잇는다). 큰 혈을 다 뚫으면 그 혈이 든 줄이 한 번 빛난다
        static readonly string[][] Channels =
        {
            new[] { "Growth", "Luck", "UltimateCharge", "Magnet", "MaxHealth", "Revival" },          // 머리에서 임맥
            new[] { "Growth", "Rerolls" },
            new[] { "Armor", "Skips" },                                                               // 독맥(등)
            new[] { "Magnet", "Greed" },
            new[] { "Area", "ProjectileSpeed", "CooldownReduction", "Duration", "Might", "Amount" },  // 팔
            new[] { "MoveSpeed", "Recovery", "Banishes", "DashCharges" },                             // 다리
        };
        static readonly Color Gold = new Color32(201, 162, 39, 255);
        RectTransform _chart, _detail;
        readonly Dictionary<string, (Image dot, Image[] pips, RectTransform rt)> _points = new Dictionary<string, (Image, Image[], RectTransform)>();
        readonly List<(Image line, string[] channel)> _lines = new List<(Image, string[])>();
        string _sel;
        MetaShopRow _selRow;
        static Sprite _circle;

        /// <summary>시험용: 지금 고른 혈의 항목.</summary>
        public string Selected => _sel;
        public int PointCount => _points.Count;

        public Soul Soul => _soul;

        /// <summary>'객잔으로'(Esc)로 닫혔다(객잔 본채가 다시 뜬다, 2026-11-20).</summary>
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
            // 방향키·WASD 로 그 쪽 가장 가까운 혈로, Enter·Space 로 뚫기
            Vector2 dir = Vector2.zero;
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) dir = Vector2.down;   // 그림 상자는 y 가 아래로
            else if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) dir = Vector2.up;
            else if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) dir = Vector2.left;
            else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) dir = Vector2.right;
            if (dir != Vector2.zero) Move(dir);
            else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) BuySelected();
        }

        void Move(Vector2 dir)
        {
            var from = PointPos(_sel);
            string best = null; float bestScore = float.MaxValue;
            foreach (var p in Points)
            {
                if (p.stat == _sel) continue;
                var d = new Vector2(p.x, p.y) - from;
                float along = Vector2.Dot(d, dir);
                if (along <= 0.005f) continue;
                float score = along + Mathf.Abs(Vector2.Dot(d, new Vector2(-dir.y, dir.x))) * 2.5f;
                if (score < bestScore) { bestScore = score; best = p.stat; }
            }
            if (best != null) Select(best);
        }

        static Vector2 PointPos(string stat)
        {
            foreach (var p in Points) if (p.stat == stat) return new Vector2(p.x, p.y);
            return new Vector2(0.5f, 0.5f);
        }

        /// <summary>고른 혈을 한 레벨 뚫는다(돈이 모자라거나 다 뚫었으면 그대로). 뚫었으면 true.</summary>
        public bool BuySelected()
        {
            if (_soul == null || _sel == null) return false;
            int before = _soul.TrainingLevel(_sel);
            if (!_soul.TryBuyTraining(_sel)) return false;
            if (Big.Contains(_sel) && _soul.TrainingLevel(_sel) >= _soul.TrainingMax(_sel) && before < _soul.TrainingMax(_sel)) FlashChannel(_sel);
            Refresh();
            return true;
        }

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
            go.GetComponent<Canvas>().sortingOrder = 30; // 객잔 본채(20)·침방 위
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
            // 2026-11-21 연무장 그림(bg_training_yard) 위에 옅게 비치는 종이 한 장 + 결·먹 테두리. 그림이 없으면 종이 결만
            if (background != null)
            {
                bg.sprite = background; bg.color = Color.white; bg.preserveAspect = false;
                var dim = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
                dim.transform.SetParent(_root, false); dim.color = new Color(0.05f, 0.04f, 0.04f, 0.3f); dim.raycastTarget = false;
                dim.rectTransform.anchorMin = Vector2.zero; dim.rectTransform.anchorMax = Vector2.one; dim.rectTransform.offsetMin = dim.rectTransform.offsetMax = Vector2.zero;
                var sheet = new GameObject("Sheet", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
                sheet.transform.SetParent(_root, false); sheet.color = new Color(Paper.r, Paper.g, Paper.b, 0.9f); sheet.raycastTarget = false;
                Place(sheet.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(40, 5), new Vector2(1680, 1050));
                InkUi.PaperGrain(_root, new Vector2(40, 5), new Vector2(1680, 1050), new Color(1f, 1f, 1f, 0.45f));
                InkUi.Frame(_root, new Vector2(40, 5), new Vector2(1680, 1050), 16f, 2f, new Color(Ink.r, Ink.g, Ink.b, 0.35f));
            }
            else InkUi.PaperGrain(_root, Vector2.zero, new Vector2(1920, 1080), new Color(1f, 1f, 1f, 0.5f));

            var title = Text(_root, Loc.T("ui.training.title"), 44, FontStyles.Bold, Ink);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -64), new Vector2(1000, 60));
            var sub = Text(_root, Loc.T("ui.training.sub"), 22, FontStyles.Italic, new Color(Ink.r, Ink.g, Ink.b, 0.55f));
            Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -108), new Vector2(1000, 32));
            InkUi.Stroke(_root, new Vector2(0, 540 - 180), new Vector2(460, 12), new Color(Ink.r, Ink.g, Ink.b, 0.7f));
            _balance = Text(_root, "", 30, FontStyles.Bold, new Color32(176, 132, 40, 255));
            Place(_balance.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(1000, 44));

            // 왼쪽: 경맥도(사람 윤곽 + 경맥 줄 + 혈자리 20곳)
            _points.Clear(); _lines.Clear(); _sel = null; _selRow = null;
            _chart = new GameObject("경맥도", typeof(RectTransform)).GetComponent<RectTransform>();
            _chart.SetParent(_root, false);
            var art = ChartArt;
            // 원화는 그 비율대로(가로 700 에 맞춤), 없으면 코드 윤곽용 700×880
            Place(_chart, new Vector2(0.5f, 0.5f), new Vector2(-380, -60),
                art != null ? new Vector2(700, 700f * art.rect.height / art.rect.width) : new Vector2(700, 880));
            if (art != null)
            {
                var sheet = MakeImage(_chart, "경맥도 그림", Color.white, false);
                sheet.sprite = art; sheet.raycastTarget = false;
                var srt = sheet.rectTransform; srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
                srt.offsetMin = srt.offsetMax = Vector2.zero;
            }
            else DrawFigure(_chart);
            foreach (var ch in Channels)
                for (int c = 0; c + 1 < ch.Length; c++) _lines.Add((Line(_chart, PointPos(ch[c]), PointPos(ch[c + 1])), ch));
            foreach (var p in Points) MakePoint(soul, p.stat, p.x, p.y);

            // 오른쪽: 고른 혈(이름·칸), 그 아래 전부 환불과 환혼 강화
            _detail = new GameObject("혈 설명", typeof(RectTransform)).GetComponent<RectTransform>();
            _detail.SetParent(_root, false);
            Place(_detail, new Vector2(0.5f, 1f), new Vector2(440, -320), new Vector2(760, 200));
            var hint = Text(_root, Loc.T("ui.meridian.hint"), 20, FontStyles.Italic, new Color(Ink.r, Ink.g, Ink.b, 0.5f));
            Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(440, -205), new Vector2(760, 30));

            float y = -470;
            _refund = new GameObject("전부 환불", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button)).GetComponent<Button>();
            _refund.transform.SetParent(_root, false);
            _refund.GetComponent<Image>().color = new Color(0.45f, 0.18f, 0.15f);
            Place((RectTransform)_refund.transform, new Vector2(0.5f, 1f), new Vector2(440, y), new Vector2(360, 50));
            _refundLabel = Text(_refund.transform, "", 22, FontStyles.Bold, Paper);
            var lr = _refundLabel.rectTransform; lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = lr.offsetMax = Vector2.zero;
            _refund.onClick.AddListener(() => { if (_soul.TrainingSpent > 0) { _soul.RefundTraining(); Refresh(); } });

            y -= 70;
            var uh = Text(_root, $"{Loc.T("ui.chimbang.upgrades")} <size=75%><alpha=#88>{Loc.T("ui.chimbang.name")}<alpha=#FF></size>", 26, FontStyles.Bold, Ink);
            Place(uh.rectTransform, new Vector2(0.5f, 1f), new Vector2(440, y), new Vector2(760, 40));
            y -= 50;
            int k = 0;
            foreach (var u in soul.Data.Cheonha.Rebirth.Upgrades)
            {
                var row = MetaShopRow.Upgrade(_root, font, soul, u, Refresh, 720);
                row.Rect.anchorMin = row.Rect.anchorMax = new Vector2(0.5f, 1f);
                row.Rect.anchoredPosition = new Vector2(440, y - k * 50);   // 경맥도 오른쪽에 한 줄씩
                _rows.Add(row);
                k++;
            }
            if (Closed != null) InnBack.Add(_root, font, Close);
            Select(Points[0].stat);
            Refresh();
        }

        // ── 경맥도 그리기 ──

        static Sprite Circle()
        {
            if (_circle != null) return _circle;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            for (int yy = 0; yy < n; yy++)
                for (int xx = 0; xx < n; xx++)
                {
                    float dx = (xx + 0.5f) / n * 2f - 1f, dy = (yy + 0.5f) / n * 2f - 1f, r = Mathf.Sqrt(dx * dx + dy * dy);
                    px[yy * n + xx] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01((1f - r) * n * 0.5f) * 255));
                }
            tex.SetPixels32(px); tex.Apply();
            _circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return _circle;
        }

        Image MakeImage(RectTransform parent, string name, Color c, bool round)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var im = go.GetComponent<Image>();
            if (round) im.sprite = Circle();
            im.color = c; im.raycastTarget = false;
            return im;
        }

        // 그림 상자 안 0~1(y 아래로) → 상자 기준 자리
        Vector2 At(float x, float y) => new Vector2((x - 0.5f) * _chart.sizeDelta.x, (0.5f - y) * _chart.sizeDelta.y);

        void Blob(RectTransform parent, float x, float y, float w, float h, float rot, bool round)
        {
            var b = MakeImage(parent, "몸", new Color(Ink.r, Ink.g, Ink.b, 0.09f), round);
            var rt = b.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = At(x, y); rt.sizeDelta = new Vector2(w * _chart.sizeDelta.x, h * _chart.sizeDelta.y);
            rt.localRotation = Quaternion.Euler(0, 0, rot);
        }

        // 몸을 살짝 튼 앉은 운기 자세(코드로 그린 옅은 먹 윤곽 — 원화 ui_meridian_chart 가 Resources 에 없을 때만)
        void DrawFigure(RectTransform chart)
        {
            Blob(chart, 0.5f, 0.10f, 0.20f, 0.13f, 0f, true);      // 머리
            Blob(chart, 0.5f, 0.19f, 0.08f, 0.05f, 0f, false);     // 목
            Blob(chart, 0.5f, 0.40f, 0.34f, 0.34f, 0f, true);      // 몸통
            Blob(chart, 0.30f, 0.33f, 0.09f, 0.22f, -18f, true);   // 왼 윗팔
            Blob(chart, 0.30f, 0.53f, 0.08f, 0.20f, 25f, true);    // 왼 아래팔
            Blob(chart, 0.70f, 0.33f, 0.09f, 0.22f, 18f, true);    // 오른 윗팔
            Blob(chart, 0.70f, 0.53f, 0.08f, 0.20f, -25f, true);   // 오른 아래팔
            Blob(chart, 0.5f, 0.72f, 0.66f, 0.22f, 0f, true);      // 엇갈려 앉은 다리
            Blob(chart, 0.5f, 0.84f, 0.30f, 0.07f, 0f, true);      // 발
        }

        Image Line(RectTransform chart, Vector2 a01, Vector2 b01)
        {
            var l = MakeImage(chart, "경맥", LineColor, false);
            var a = At(a01.x, a01.y); var b = At(b01.x, b01.y);
            var rt = l.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = (a + b) * 0.5f;
            rt.sizeDelta = new Vector2((b - a).magnitude, 3f);
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
            return l;
        }

        void MakePoint(Soul soul, string stat, float x, float y)
        {
            bool big = Big.Contains(stat);
            float size = big ? 44f : 24f;
            var go = new GameObject($"혈 {stat}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(_chart, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = At(x, y);
            rt.sizeDelta = new Vector2(size + 26, size + 26);   // 누르기 쉬운 칸(투명)
            go.GetComponent<Image>().color = new Color(1, 1, 1, 0.001f);
            go.GetComponent<Button>().onClick.AddListener(() => Select(stat));
            var dot = MakeImage(rt, "혈", Ink, true);
            dot.rectTransform.sizeDelta = new Vector2(size, size);
            int max = soul.TrainingMax(stat);
            var pips = new Image[max];
            float R = size * 0.5f + 8f;
            for (int i = 0; i < max; i++)
            {
                float a = Mathf.PI * 0.5f - i * Mathf.PI * 2f / Mathf.Max(max, 1);
                var p = MakeImage(rt, "점", Ink, true);
                p.rectTransform.sizeDelta = new Vector2(7, 7);
                p.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(a) * R, Mathf.Sin(a) * R);
                pips[i] = p;
            }
            _points[stat] = (dot, pips, rt);
        }

        void Select(string stat)
        {
            _sel = stat;
            foreach (Transform c in _detail) Destroy(c.gameObject);
            var item = _soul.Data.Balance.Training.Items.Find(i => i.Stat == stat);
            var name = Text(_detail, $"{Loc.T($"ui.meridian.{stat}")}", 38, FontStyles.Bold, Ink);
            Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -30), new Vector2(740, 52));
            var place = Text(_detail, Big.Contains(stat) ? Loc.T("ui.meridian.big") : "", 20, FontStyles.Italic, Gold);
            Place(place.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -72), new Vector2(740, 28));
            _selRow = MetaShopRow.Training(_detail, font, _soul, item, Refresh, 720);
            _selRow.Rect.anchorMin = _selRow.Rect.anchorMax = new Vector2(0.5f, 1f);
            _selRow.Rect.anchoredPosition = new Vector2(0, -130);
            Refresh();
        }

        float _flashUntil;
        string[] _flashChannel;
        void FlashChannel(string stat)
        {
            foreach (var ch in Channels) if (System.Array.IndexOf(ch, stat) >= 0) _flashChannel = ch;
            _flashUntil = Time.unscaledTime + 0.9f;
        }

        void LateUpdate()
        {
            if (_root == null) return;
            float k = _flashChannel != null && Time.unscaledTime < _flashUntil ? (_flashUntil - Time.unscaledTime) / 0.9f : 0f;
            foreach (var (line, ch) in _lines)
            {
                bool lit = ch == _flashChannel && k > 0f;
                line.color = lit ? Color.Lerp(LineColor, new Color(1f, 0.85f, 0.35f, 1f), Mathf.Sin(k * Mathf.PI)) : LineColor;
                line.rectTransform.sizeDelta = new Vector2(line.rectTransform.sizeDelta.x, lit ? 6f : 3f);
            }
            // 고른 혈은 숨 쉬듯 테두리가 커졌다 작아진다
            foreach (var kv in _points)
            {
                float s = kv.Key == _sel ? 1.15f + Mathf.Sin(Time.unscaledTime * 4f) * 0.08f : 1f;
                kv.Value.dot.rectTransform.localScale = Vector3.one * s;
            }
        }

        public void Refresh()
        {
            _balance.text = Loc.F("ui.qi.balance", _soul.RebirthQi.ToString("N0"));
            foreach (var r in _rows) r.Refresh();
            _selRow?.Refresh();
            foreach (var kv in _points)
            {
                int lv = _soul.TrainingLevel(kv.Key), max = _soul.TrainingMax(kv.Key);
                bool full = lv >= max && max > 0;
                kv.Value.dot.color = full ? Gold : lv > 0 ? new Color(0.55f, 0.16f, 0.12f, 0.9f) : new Color(Ink.r, Ink.g, Ink.b, kv.Key == _sel ? 0.75f : 0.35f);
                for (int i = 0; i < kv.Value.pips.Length; i++) kv.Value.pips[i].color = i < lv ? Gold : new Color(Ink.r, Ink.g, Ink.b, 0.22f);
            }
            _refund.interactable = _soul.TrainingSpent > 0;
            _refundLabel.text = $"{Loc.T("ui.training.refund")}  <size=80%>(+{_soul.TrainingSpent:N0})</size>";
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
