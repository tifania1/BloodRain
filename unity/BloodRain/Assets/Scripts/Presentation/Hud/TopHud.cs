using System.Collections.Generic;
using BloodRain.Core.Build;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BloodRain.Presentation.Hud
{
    // HUD-01U ①② 화면 맨 위 진기 바와 오른쪽 위 처치·은자(기획서 12장 '전투 HUD 배치').
    //   ① 맨 위 가로 진기 바: 먹선 위로 금빛이 RunXp.Progress 만큼 찬다. 오른쪽 끝에 'Lv N · 경지'(strings ui.hud.level).
    //   ② 그 아래 오른쪽: 처치 수(LifeRunner.Kills, 덩이 속 사람 수 — KC-01U)·은자(Life.Silver).
    //   ③ 왼쪽 위 무공 칸 + 심법 칸(Core HudSlots.Build — 칸 수는 Loadout 이 정함, 7칸째가 열리면 7). 그림이 없어 이름 첫 글자,
    //      문턱 무공(Ready)은 테두리 금빛이 뛰고, 그릇이 없으면(Waiting) 흐린 금빛, 절기(Evolved)는 굵은 금테. 그 아래 '경지 ◆◇'.
    //   ④ 캐릭터 머리 위 짧은 기혈 바(Balance.Contact.IsLowHealth 면 붉게, 웹 #B3261E)와 경공 충전 점(Dash.Charges/MaxCharges).
    public sealed class TopHud : MonoBehaviour
    {
        public TMP_FontAsset font;
        [Tooltip("진기 바 높이(기준 해상도 1920x1080 px)")]
        public float barHeight = 10f;
        [Tooltip("차오르는 빠르기(초당 비율)")]
        public float follow = 10f;
        [Tooltip("머리 위 기혈 바를 캐릭터 화면 자리에서 얼마나 위로(기준 px)")]
        public float headOffset = 46f;

        static readonly Color Gold = new Color32(214, 170, 72, 255);
        static readonly Color InkLine = new Color(0.10f, 0.08f, 0.07f, 0.75f);
        static readonly Color Paper = new Color(0.96f, 0.93f, 0.85f, 0.35f);
        static readonly Color TextInk = new Color(0.12f, 0.10f, 0.09f, 1f);

        static readonly Color SlotInk = new Color(0.12f, 0.10f, 0.09f, 0.85f);
        static readonly Color SlotPaper = new Color(0.96f, 0.93f, 0.85f, 0.82f);
        static readonly Color SlotEmpty = new Color(0.96f, 0.93f, 0.85f, 0.30f);
        static readonly Color HpOk = new Color(0.30f, 0.52f, 0.36f, 1f);
        static readonly Color HpLow = new Color32(179, 38, 30, 255);
        const float SlotSize = 44f, SlotGap = 6f;

        sealed class Cell { public Image Frame, Inner; public TextMeshProUGUI Glyph, Level; }
        readonly List<Cell> _wCells = new List<Cell>(), _pCells = new List<Cell>();
        RectTransform _root, _slotRoot, _head, _hpFill;
        Image _hpFillImg;
        readonly List<Image> _dots = new List<Image>();
        TextMeshProUGUI _vessels;
        HudSlotsView _view;
        float _slotTimer;
        PlayerController _player;
        PlayerVitals _vitals;

        RectTransform _fill;
        TextMeshProUGUI _level, _kills, _silver;
        float _shown;
        int _lastLevel = -1;
        long _lastKills = -1;
        int _lastSilver = -1;

        void Start() => Build();

        void Build()
        {
            var go = new GameObject("위 HUD", typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)go.transform;
            _root = root;

            // 바 바탕(옅은 종이) + 아래 먹선
            var bg = Img(root, "진기 바 바탕", Paper);
            Stretch(bg.rectTransform, barHeight);
            var line = Img(root, "먹선", InkLine);
            var lr = line.rectTransform;
            lr.anchorMin = new Vector2(0, 1); lr.anchorMax = new Vector2(1, 1); lr.pivot = new Vector2(0.5f, 1);
            lr.anchoredPosition = new Vector2(0, -barHeight); lr.sizeDelta = new Vector2(0, 2);
            // 금빛 채움: 왼쪽에서 오른쪽으로 anchorMax.x 가 진행
            var fill = Img(root, "진기", Gold);
            _fill = fill.rectTransform;
            _fill.anchorMin = new Vector2(0, 1); _fill.anchorMax = new Vector2(0, 1); _fill.pivot = new Vector2(0, 1);
            _fill.anchoredPosition = Vector2.zero; _fill.offsetMin = new Vector2(0, -barHeight); _fill.offsetMax = Vector2.zero;

            _level = Text(root, "레벨", 26, TextAlignmentOptions.Right, new Vector2(-24, -barHeight - 6));
            _kills = Text(root, "처치", 22, TextAlignmentOptions.Right, new Vector2(-24, -barHeight - 40));
            _silver = Text(root, "은자", 22, TextAlignmentOptions.Right, new Vector2(-24, -barHeight - 68));
            // ③ 왼쪽 위 칸 묶음
            _slotRoot = new GameObject("무공·심법 칸", typeof(RectTransform)).GetComponent<RectTransform>();
            _slotRoot.SetParent(root, false);
            _slotRoot.anchorMin = _slotRoot.anchorMax = new Vector2(0, 1); _slotRoot.pivot = new Vector2(0, 1);
            _slotRoot.anchoredPosition = new Vector2(16, -barHeight - 10); _slotRoot.sizeDelta = new Vector2(400, 150);
            _vessels = Text(_slotRoot, "그릇", 20, TextAlignmentOptions.Left, Vector2.zero);
            var vr = _vessels.rectTransform;
            vr.anchorMin = vr.anchorMax = new Vector2(0, 1); vr.pivot = new Vector2(0, 1);

            // ④ 머리 위 기혈 바·경공 점
            _head = new GameObject("머리 위", typeof(RectTransform)).GetComponent<RectTransform>();
            _head.SetParent(root, false);
            _head.anchorMin = _head.anchorMax = new Vector2(0.5f, 0.5f); _head.sizeDelta = new Vector2(60, 16);
            var hb = Img(_head, "기혈 바탕", new Color(0.10f, 0.08f, 0.07f, 0.65f));
            var hbr = hb.rectTransform; hbr.anchorMin = hbr.anchorMax = new Vector2(0.5f, 1); hbr.pivot = new Vector2(0.5f, 1);
            hbr.anchoredPosition = Vector2.zero; hbr.sizeDelta = new Vector2(60, 7);
            _hpFillImg = Img(hb.rectTransform, "기혈", HpOk);
            _hpFill = _hpFillImg.rectTransform;
            _hpFill.anchorMin = new Vector2(0, 0); _hpFill.anchorMax = new Vector2(1, 1); _hpFill.pivot = new Vector2(0, 0.5f);
            _hpFill.offsetMin = new Vector2(1, 1); _hpFill.offsetMax = new Vector2(-1, -1);
            _head.gameObject.SetActive(false);
        }

        Cell MakeCell(int row, int col)
        {
            var c = new Cell();
            c.Frame = Img(_slotRoot, $"칸 {row}-{col}", SlotInk);
            var fr = c.Frame.rectTransform;
            fr.anchorMin = fr.anchorMax = new Vector2(0, 1); fr.pivot = new Vector2(0, 1);
            fr.anchoredPosition = new Vector2(col * (SlotSize + SlotGap), -row * (SlotSize + SlotGap)); fr.sizeDelta = new Vector2(SlotSize, SlotSize);
            c.Inner = Img(fr, "속", SlotPaper);
            var ir = c.Inner.rectTransform; ir.anchorMin = Vector2.zero; ir.anchorMax = Vector2.one; ir.offsetMin = new Vector2(3, 3); ir.offsetMax = new Vector2(-3, -3);
            c.Glyph = Text(fr, "글자", 24, TextAlignmentOptions.Center, Vector2.zero);
            var gr = c.Glyph.rectTransform; gr.anchorMin = Vector2.zero; gr.anchorMax = Vector2.one; gr.pivot = new Vector2(0.5f, 0.5f);
            gr.offsetMin = Vector2.zero; gr.offsetMax = new Vector2(0, -2);
            c.Level = Text(fr, "Lv", 13, TextAlignmentOptions.BottomRight, Vector2.zero);
            var lr = c.Level.rectTransform; lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.pivot = new Vector2(0.5f, 0.5f);
            lr.offsetMin = new Vector2(0, 2); lr.offsetMax = new Vector2(-4, 0);
            return c;
        }

        void SyncCells(List<Cell> cells, List<HudSlot> slots, int row)
        {
            while (cells.Count < slots.Count) cells.Add(MakeCell(row, cells.Count));
            for (int i = 0; i < cells.Count; i++) cells[i].Frame.gameObject.SetActive(i < slots.Count);
        }

        static string NameOf(string id, bool weapon)
        {
            var data = DataBoot.Data;
            if (weapon) { foreach (var w in data.Weapons) if (w.Id == id) return Loc.Weapon(w); }
            else { foreach (var p in data.Passives) if (p.Id == id) return Loc.T($"passives.{p.Id}.name", p.NameKo); }
            return id;
        }

        void FillCells(List<Cell> cells, List<HudSlot> slots, bool weapon, float pulse)
        {
            for (int i = 0; i < slots.Count && i < cells.Count; i++)
            {
                var s = slots[i]; var c = cells[i];
                if (s.Id == null)
                {
                    c.Frame.color = new Color(SlotInk.r, SlotInk.g, SlotInk.b, 0.35f); c.Inner.color = SlotEmpty;
                    c.Glyph.text = ""; c.Level.text = "";
                    continue;
                }
                string name = NameOf(s.Id, weapon);
                c.Glyph.text = string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1); // 아이콘 그림이 오기 전엔 이름 첫 글자(웹 GLYPH 자리)
                c.Level.text = s.Level.ToString();
                c.Inner.color = SlotPaper;
                c.Glyph.color = TextInk;
                switch (s.Glow)
                {
                    case SlotGlow.Ready: c.Frame.color = Color.Lerp(SlotInk, Gold, 0.55f + 0.45f * pulse); break;          // 금빛으로 뜀
                    case SlotGlow.Waiting: c.Frame.color = new Color(Gold.r, Gold.g, Gold.b, 0.45f); break;               // 흐린 금빛
                    case SlotGlow.Evolved: c.Frame.color = Gold; c.Glyph.color = new Color(0.55f, 0.36f, 0.06f, 1f); break; // 금테
                    default: c.Frame.color = SlotInk; break;
                }
                var ir = c.Inner.rectTransform;
                float pad = s.Glow == SlotGlow.Evolved ? 5f : 3f;
                ir.offsetMin = new Vector2(pad, pad); ir.offsetMax = new Vector2(-pad, -pad);
            }
        }

        void UpdateSlots(BloodRain.Core.Run.Life life)
        {
            _slotTimer -= Time.unscaledDeltaTime;
            if (_view == null || _slotTimer <= 0f)
            {
                _slotTimer = 0.2f;
                _view = HudSlots.Build(life.Loadout, life.Level);
                SyncCells(_wCells, _view.Weapons, 0);
                SyncCells(_pCells, _view.Passives, 1);
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < _view.VesselsTotal; i++) sb.Append(i < _view.VesselsUsed ? '◆' : '◇');
                _vessels.text = Loc.Realm(life.Xp.Realm) + (sb.Length > 0 ? "  " + sb : "");
                _vessels.rectTransform.anchoredPosition = new Vector2(2, -2 * (SlotSize + SlotGap));
            }
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
            FillCells(_wCells, _view.Weapons, true, pulse);
            FillCells(_pCells, _view.Passives, false, pulse);
        }

        void UpdateHead()
        {
            if (_player == null) _player = FindFirstObjectByType<PlayerController>();
            if (_vitals == null) _vitals = FindFirstObjectByType<PlayerVitals>();
            var cam = Camera.main;
            if (_player == null || _vitals == null || cam == null) { _head.gameObject.SetActive(false); return; }
            Vector3 sp = cam.WorldToScreenPoint(_player.transform.position);
            if (sp.z < 0f) { _head.gameObject.SetActive(false); return; }
            _head.gameObject.SetActive(true);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, sp, null, out var local);
            _head.anchoredPosition = local + new Vector2(0, headOffset);
            float ratio = _vitals.MaxHealth > 0f ? Mathf.Clamp01(_vitals.Health / _vitals.MaxHealth) : 0f;
            _hpFill.anchorMax = new Vector2(ratio, 1);
            _hpFill.offsetMin = new Vector2(1, 1); _hpFill.offsetMax = new Vector2(-1, -1);
            _hpFillImg.color = DataBoot.Data.Balance.Contact.IsLowHealth(ratio) ? HpLow : HpOk; // LH-01U 문턱 아래면 붉게

            var dash = _player.Dash;
            int max = dash != null ? dash.MaxCharges : 0;
            while (_dots.Count < max)
            {
                var d = Img(_head, "경공 점", Gold);
                var dr = d.rectTransform; dr.anchorMin = dr.anchorMax = new Vector2(0.5f, 1); dr.pivot = new Vector2(0.5f, 1); dr.sizeDelta = new Vector2(6, 6);
                _dots.Add(d);
            }
            for (int i = 0; i < _dots.Count; i++)
            {
                bool on = i < max;
                _dots[i].gameObject.SetActive(on);
                if (!on) continue;
                _dots[i].rectTransform.anchoredPosition = new Vector2((i - (max - 1) * 0.5f) * 10f, -10f);
                // 찬 점은 짙게, 차는 중인 점은 RechargeProgress 만큼 서서히 짙어짐
                float a = i < dash.Charges ? 1f : i == dash.Charges ? 0.15f + 0.5f * dash.RechargeProgress : 0.15f;
                _dots[i].color = new Color(Gold.r, Gold.g, Gold.b, a);
            }
        }

        void Update()
        {
            var lr = LifeRunner.Instance;
            if (_fill == null || lr == null || lr.Life == null) return;
            var life = lr.Life;
            float target = Mathf.Clamp01((float)life.Xp.Progress);
            if (life.Level != _lastLevel) _shown = target < _shown ? 0f : _shown; // 레벨이 오르면 비우고 다시 참
            _shown = Mathf.Lerp(_shown, target, 1f - Mathf.Exp(-follow * Time.unscaledDeltaTime));
            _fill.anchorMax = new Vector2(_shown, 1);
            _fill.offsetMin = new Vector2(0, -barHeight); _fill.offsetMax = Vector2.zero;

            if (life.Level != _lastLevel)
            {
                _lastLevel = life.Level;
                _level.text = Loc.F("ui.hud.level", life.Level, Loc.Realm(life.Xp.Realm));
            }
            if (lr.Kills != _lastKills) { _lastKills = lr.Kills; _kills.text = Loc.F("ui.hud.kills", lr.Kills.ToString("N0")); }
            if (life.Silver != _lastSilver) { _lastSilver = life.Silver; _silver.text = Loc.F("ui.hud.silver", life.Silver.ToString("N0")); }
            UpdateSlots(life);
            UpdateHead();
        }

        static Image Img(Transform parent, string name, Color c)
        {
            var img = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(parent, false);
            img.color = c; img.raycastTarget = false;
            return img;
        }

        static void Stretch(RectTransform rt, float h)
        {
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(0, h);
        }

        TextMeshProUGUI Text(Transform parent, string name, float size, TextAlignmentOptions align, Vector2 pos)
        {
            var t = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            t.transform.SetParent(parent, false);
            if (font != null) t.font = font;
            t.fontSize = size; t.alignment = align; t.color = TextInk; t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(420, 32);
            return t;
        }
    }
}
