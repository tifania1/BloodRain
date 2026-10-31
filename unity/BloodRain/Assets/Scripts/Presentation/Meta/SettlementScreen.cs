using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.World;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BloodRain.Presentation.Meta
{
    /// <summary>Core 장부에 없는 한 생의 숫자(전투 쪽이 센다). 처치 수는 덩이 속 사람 수로 센 값.</summary>
    public sealed class SettlementStats
    {
        public float SurvivalSeconds;
        public long Kills;
        public int BloodCrystal;
        /// <summary>무공 id → 준 피해.</summary>
        public Dictionary<string, double> DamageByWeapon = new Dictionary<string, double>();
    }

    // 정산 화면(TU-13, 기획서 10장 '윤회의 기'·12장 화면 목록). 쓰러짐·좌화·봉인이 같은 화면이고 끝나면 객잔으로.
    //   왼쪽: 생존 시간, 오른 경지, 밟은 땅, 처치 수, 혈정, 무공별 피해 비중 막대.
    //   오른쪽: 윤회의 기 내역(Core RebirthQiResult.Lines 순서)을 한 줄씩 차오르게, 마지막에 합계와 혼의 잔고.
    // 숫자는 모두 Core 결과 그대로다. 화면은 줄을 늦게 보여 주고 세는 것만 한다(언스케일 시계).
    public sealed class SettlementScreen : MonoBehaviour
    {
        public TMP_FontAsset font;
        [Tooltip("시험용: 시작하면 지어낸 한 생으로 정산을 보여 준다")]
        public bool demo;
        [Tooltip("끝나면 Inn 씬을 연다. 시험 씬에선 끔")]
        public bool loadInnScene;
        [Tooltip("내역 한 줄이 차오르는 간격(초)")]
        public float lineInterval = 0.35f;

        public event Action Finished;

        static readonly Color Paper = new Color(0.91f, 0.87f, 0.78f);
        static readonly Color Ink = new Color(0.12f, 0.10f, 0.09f);
        static readonly Color Crimson = new Color32(179, 38, 30, 255);
        static readonly Color Gold = new Color32(176, 132, 40, 255);

        GameData _data;
        RectTransform _root;
        readonly List<(TextMeshProUGUI label, TextMeshProUGUI value, int amount)> _qiLines = new List<(TextMeshProUGUI, TextMeshProUGUI, int)>();
        TextMeshProUGUI _total, _balance;
        int _totalAmount, _balanceAfter;
        float _t;
        bool _showing;

        public bool Done => _showing && _t >= RevealEnd;
        float RevealEnd => (_qiLines.Count + 1) * lineInterval + 0.4f;

        void Start()
        {
            _data = DataBoot.Data;
            if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            if (demo) ShowDemo();
        }

        /// <summary>정산을 보여 준다. record 는 LifeLog.Finish, summary 는 Soul.EndLife 의 결과(잔고는 soul 에서).</summary>
        public void Show(LifeRecord record, LifeSummary summary, SettlementStats stats, int balanceAfter)
        {
            if (_data == null) _data = DataBoot.Data;
            if (_root != null) Destroy(_root.gameObject);
            _qiLines.Clear();
            BuildCanvas();
            var scale = new Scale(_data);
            var regions = new Regions(_data);

            string end = record.EndWay switch { "zuohwa" => "좌화 坐化", "sealed" => "봉인 封印", _ => "쓰러짐" };
            var title = Text(_root, $"{end} · 한 생이 끝났다", 44, FontStyles.Bold, Ink);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -70), new Vector2(1400, 64));
            string sect = record.Sect != null ? _data.Cheonha.Sect(record.Sect).NameKo + " 제자" : "진소운";
            var sub = Text(_root, sect, 26, FontStyles.Normal, new Color(Ink.r, Ink.g, Ink.b, 0.6f));
            Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -122), new Vector2(1000, 40));

            // ---- 왼쪽: 한 생 ----
            var left = Column(new Vector2(-430, 0));
            int sec = Mathf.FloorToInt(stats.SurvivalSeconds);
            string realm = scale.Realms.FirstOrDefault(r => r.Id == record.HighestRealm)?.NameKo ?? record.HighestRealm;
            var visited = record.VisitedRegions.Select(id => regions.All.FirstOrDefault(r => r.Id == id)?.NameKo ?? id).ToList();
            float y = 0;
            Row(left, ref y, "생존 시간", $"{sec / 60}:{sec % 60:00}");
            Row(left, ref y, "오른 경지", realm);
            Row(left, ref y, "밟은 땅", $"{visited.Count}곳", string.Join("·", visited));
            Row(left, ref y, "처치", $"{stats.Kills:N0}");
            Row(left, ref y, "혈정", $"{stats.BloodCrystal:N0}");
            y -= 24;
            var head = Text(left, "무공별 피해", 24, FontStyles.Bold, Ink);
            head.alignment = TextAlignmentOptions.Left;
            Place(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, y - 18), new Vector2(640, 34));
            y -= 46;
            double sum = stats.DamageByWeapon.Values.Sum();
            foreach (var kv in stats.DamageByWeapon.OrderByDescending(k => k.Value).Take(6))
            {
                float share = sum > 0 ? (float)(kv.Value / sum) : 0f;
                string name = _data.Weapons.FirstOrDefault(w => w.Id == kv.Key)?.NameKo ?? kv.Key;
                Bar(left, y, name, share);
                y -= 38;
            }

            // ---- 오른쪽: 윤회의 기 ----
            var right = Column(new Vector2(430, 0));
            var qhead = Text(right, "윤회의 기 輪迴氣", 30, FontStyles.Bold, Ink);
            Place(qhead.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(640, 44));
            var res = record.RebirthQi;
            float qy = -80;
            foreach (var line in res.Lines)
            {
                var (label, note) = LineText(line, res);
                var l = Text(right, $"{label}  <size=75%><alpha=#88>{note}<alpha=#FF></size>", 23, FontStyles.Normal, Ink);
                l.alignment = TextAlignmentOptions.Left;
                Place(l.rectTransform, new Vector2(0.5f, 1f), new Vector2(-60, qy), new Vector2(520, 36));
                var v = Text(right, "", 25, FontStyles.Bold, Ink);
                v.alignment = TextAlignmentOptions.Right;
                Place(v.rectTransform, new Vector2(0.5f, 1f), new Vector2(240, qy), new Vector2(160, 36));
                l.alpha = v.alpha = 0f;
                _qiLines.Add((l, v, line.Amount));
                qy -= 44;
            }
            var rule = Panel(right, new Color(Ink.r, Ink.g, Ink.b, 0.5f));
            Place(rule, new Vector2(0.5f, 1f), new Vector2(0, qy + 14), new Vector2(620, 2));
            _total = Text(right, "", 34, FontStyles.Bold, Gold);
            _total.alignment = TextAlignmentOptions.Right;
            Place(_total.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, qy - 16), new Vector2(620, 48));
            _balance = Text(right, "", 22, FontStyles.Normal, new Color(Ink.r, Ink.g, Ink.b, 0.7f));
            _balance.alignment = TextAlignmentOptions.Right;
            Place(_balance.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, qy - 60), new Vector2(620, 32));
            _totalAmount = res.Total;
            _balanceAfter = balanceAfter;
            if (summary.NewlyUnlockedSects.Count > 0 || summary.NewSealStones.Count > 0)
            {
                var extra = new List<string>();
                if (summary.NewSealStones.Count > 0) extra.Add("봉인석 " + string.Join("·", summary.NewSealStones.Select(s => regions.All.FirstOrDefault(r => r.Id == s)?.SealStone ?? s)));
                if (summary.NewlyUnlockedSects.Count > 0) extra.Add("새 문파 " + string.Join("·", summary.NewlyUnlockedSects.Select(id => _data.Cheonha.Sect(id).NameKo)));
                var ex = Text(right, string.Join("   ", extra), 22, FontStyles.Bold, Crimson);
                ex.alignment = TextAlignmentOptions.Right;
                Place(ex.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, qy - 96), new Vector2(620, 32));
            }

            var btn = MakeButton(_root, "객잔으로  <size=70%><alpha=#88>Enter<alpha=#FF></size>");
            Place((RectTransform)btn.transform, new Vector2(0.5f, 0f), new Vector2(0, 80), new Vector2(380, 64));
            btn.onClick.AddListener(Finish);

            _t = 0f;
            _showing = true;
            Tick(0f);
        }

        (string label, string note) LineText(RebirthQiLine line, RebirthQiResult res) => line.Kind switch
        {
            RebirthQiLineKind.Orbs => ("진기 × 혈운 농도", $"구슬 {line.Count:N0}개"),
            RebirthQiLineKind.BossFirstKill => ("땅의 주인", $"처음 {line.Count}"),
            RebirthQiLineKind.BossRepeatKill => ("땅의 주인", $"다시 {line.Count}"),
            RebirthQiLineKind.FirstVisitRegion => ("처음 밟은 땅", $"{line.Count}곳"),
            RebirthQiLineKind.FirstReachRealm => ("처음 오른 경지", $"{line.Count}"),
            RebirthQiLineKind.Silver => ("남은 은자 절반", $"은자 {line.Count:N0}"),
            RebirthQiLineKind.Traits => ("성질 배율", $"{(res.TraitBonus >= 0 ? "+" : "")}{Mathf.RoundToInt(res.TraitBonus * 100)}%"),
            RebirthQiLineKind.BloodMoon => ("혈월 보상", $"×{res.RewardMultiplier:0.##}"),
            _ => (line.Kind.ToString(), ""),
        };

        void Update()
        {
            if (!_showing) return;
            _t += Time.unscaledDeltaTime;
            Tick(_t);
            var kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
            {
                if (!Done) _t = RevealEnd; // 한 번 누르면 다 채우고, 다시 누르면 객잔으로
                else Finish();
            }
        }

        /// <summary>t 초 시점 모습. 줄 i 는 (i+1)×간격에 나타나 0.4초 동안 숫자가 오른다.</summary>
        public void Tick(float t)
        {
            for (int i = 0; i < _qiLines.Count; i++)
            {
                float s = Mathf.Clamp01((t - (i + 1) * lineInterval) / 0.4f);
                var (label, value, amount) = _qiLines[i];
                label.alpha = value.alpha = Mathf.Clamp01(s * 3f);
                value.text = s <= 0f ? "" : $"+{Mathf.RoundToInt(amount * EaseOut(s)):N0}";
            }
            float tt = Mathf.Clamp01((t - (_qiLines.Count + 1) * lineInterval) / 0.4f);
            _total.text = tt <= 0f ? "" : $"합계  {Mathf.RoundToInt(_totalAmount * EaseOut(tt)):N0}";
            _balance.text = tt < 1f ? "" : $"혼에 남은 윤회의 기 {_balanceAfter:N0}";
        }

        static float EaseOut(float x) => 1f - (1f - x) * (1f - x);

        public void Finish()
        {
            if (!_showing) return;
            _showing = false;
            Finished?.Invoke();
            if (loadInnScene) SceneManager.LoadScene("Inn");
        }

        // ---- 시험용 한 생 ----

        void ShowDemo()
        {
            // 서장을 마치고 두 생을 산 혼(첫 생은 서장 몸이어야 해서)
            var soul = Soul.FromJson(_data, new Soul(_data).ToJson()
                .Replace("\"prologueDone\": false", "\"prologueDone\": true")
                .Replace("\"lives\": 0", "\"lives\": 2")
                .Replace("\"livesBySect\": {}", "\"livesBySect\": {\"shaolin\": 1}")
                .Replace("\"visitedRegions\": []", "\"visitedRegions\": [\"cheongun\", \"songshan\"]"));
            var c = _data.Cheonha;
            var body = Rebirth.Body(c, new RebirthCandidate(c.Sect("tang"), c.Region(c.Sect("tang").Region),
                new List<BirthTraitDef> { c.BirthTraits.List.First(t => t.Id == "dunjae") }));
            var log = soul.BeginLife(body);
            var regions = new Regions(_data);
            foreach (var id in new[] { "danggata", "janggang", "wudangsan" }) log.Enter(id);
            for (int i = 0; i < 400; i++) log.AbsorbOrb(40, i < 250 ? 1.3f : 1.7f);
            var scale = new Scale(_data);
            for (int r = 1; r <= 3 && r < scale.Realms.Count; r++) log.Breakthrough(scale.Realms[r].Id);
            log.KillBoss("danggata");
            var record = log.Finish("fallen", 240);
            var summary = soul.EndLife(record);
            var stats = new SettlementStats
            {
                SurvivalSeconds = 1234, Kills = 18342, BloodCrystal = 12,
                DamageByWeapon = new Dictionary<string, double>(),
            };
            var ws = _data.Weapons.Take(5).ToList();
            for (int i = 0; i < ws.Count; i++) stats.DamageByWeapon[ws[i].Id] = 100000.0 / (i + 1.3);
            Show(record, summary, stats, soul.RebirthQi);
        }

        // ---- UI 조각 ----

        void BuildCanvas()
        {
            var go = new GameObject("정산", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)go.transform;
            var bg = Panel(_root, Paper);
            bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one; bg.offsetMin = bg.offsetMax = Vector2.zero;
        }

        RectTransform Column(Vector2 pos)
        {
            var rt = new GameObject("칸", typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(_root, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos + new Vector2(0, -200);
            rt.sizeDelta = new Vector2(680, 700);
            return rt;
        }

        void Row(RectTransform col, ref float y, string label, string value, string note = null)
        {
            var l = Text(col, label, 24, FontStyles.Normal, new Color(Ink.r, Ink.g, Ink.b, 0.65f));
            l.alignment = TextAlignmentOptions.Left;
            Place(l.rectTransform, new Vector2(0.5f, 1f), new Vector2(-200, y - 18), new Vector2(240, 36));
            var v = Text(col, note == null ? value : $"{value}  <size=70%><alpha=#99>{note}<alpha=#FF></size>", 28, FontStyles.Bold, Ink);
            v.alignment = TextAlignmentOptions.Left;
            Place(v.rectTransform, new Vector2(0.5f, 1f), new Vector2(120, y - 18), new Vector2(400, 36));
            y -= 48;
        }

        void Bar(RectTransform col, float y, string name, float share)
        {
            var n = Text(col, name, 21, FontStyles.Normal, Ink);
            n.alignment = TextAlignmentOptions.Left;
            Place(n.rectTransform, new Vector2(0.5f, 1f), new Vector2(-230, y - 16), new Vector2(180, 30));
            var back = Panel(col, new Color(Ink.r, Ink.g, Ink.b, 0.12f));
            Place(back, new Vector2(0.5f, 1f), new Vector2(60, y - 16), new Vector2(380, 14));
            var fill = Panel(back, Crimson);
            fill.anchorMin = new Vector2(0, 0); fill.anchorMax = new Vector2(share, 1); fill.offsetMin = fill.offsetMax = Vector2.zero;
            var p = Text(col, $"{Mathf.RoundToInt(share * 100)}%", 20, FontStyles.Normal, Ink);
            p.alignment = TextAlignmentOptions.Right;
            Place(p.rectTransform, new Vector2(0.5f, 1f), new Vector2(300, y - 16), new Vector2(80, 30));
        }

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

        Button MakeButton(Transform parent, string label)
        {
            var go = new GameObject("객잔으로", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.25f, 0.20f, 0.17f);
            var t = Text(go.transform, label, 26, FontStyles.Bold, Paper);
            var rt = t.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            return go.GetComponent<Button>();
        }
    }
}
