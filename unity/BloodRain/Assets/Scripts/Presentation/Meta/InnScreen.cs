using System;
using System.Collections.Generic;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BloodRain.Presentation.Meta
{
    // 객잔 본채(거점, RM-03b · 기획서 10장 '객잔 시설'·12장 화면 목록 '객잔(거점)').
    // 2026-11-20 사람: "세이브를 고르고 게임 시작이나 지역 선택·업그레이드 등을 고를 수 있는 페이지가 없다. 죽으면 그 화면으로 돌아가야 한다."
    //   저장 칸을 고르거나 한 생이 끝나면 Inn 씬이 열리고, InnFlow 가 이 화면을 먼저 띄운다.
    //   - 새 생 시작: 침방(다음 몸·태어날 땅 고르기, 21장). 침방 왼쪽 위 '객잔으로'(Esc)로 돌아온다
    //   - 시설 문 일곱: Inn.IsOpen·CanEnter 로 켜고 끈다. 연무장은 TrainingScreen, 장경각은 수리 중이면 그 말만(M2),
    //     약방·대장간은 짓는 시설이라 조건을 갖추면 '짓기(혈정 N)', 아직 화면이 없는 시설은 '준비 중'
    //   - 설정, 타이틀로
    public sealed class InnScreen : MonoBehaviour
    {
        public TMP_FontAsset font;
        public Sprite background;
        [Tooltip("대장간 몸 색 미리 보기 그림(InnFlow 가 침방 문파 그림 하나를 넘긴다)")]
        public Sprite bodyPreview;

        public event Action StartLife;

        static readonly Color Paper = new Color(0.91f, 0.87f, 0.78f);
        static readonly Color PaperHot = new Color(0.97f, 0.93f, 0.83f);
        static readonly Color Ink = new Color(0.12f, 0.10f, 0.09f);
        static readonly Color Blood = new Color(0.55f, 0.10f, 0.08f);
        static readonly Color Gold = new Color(0.69f, 0.52f, 0.16f);
        static readonly Color Muted = new Color(0.12f, 0.10f, 0.09f, 0.45f);

        /// <summary>화면이 지금 바로 다루는 시설(시험·패드용). 데이터 순서.</summary>
        public static readonly string[] FacilityOrder = { "training", "library", "apothecary", "smithy", "cave", "haomun_board" };

        Soul _soul;
        GameObject _canvas;
        RectTransform _root, _grid;
        TextMeshProUGUI _sub, _toast;
        float _toastLeft;
        TrainingScreen _training;
        SmithyScreen _smithy;
        ApothecaryScreen _apothecary;
        SettingsScreen _settings;

        public bool IsOpen => _canvas != null && _canvas.activeSelf;
        public string ToastText => _toast != null ? _toast.text : "";

        public void Open(Soul soul)
        {
            _soul = soul;
            EnsureEventSystem();
            if (_canvas == null) Build();
            _canvas.SetActive(true);
            Refresh();
        }

        public void Hide() { if (_canvas != null) _canvas.SetActive(false); }

        // ---- 누르기 ----

        public void PressStart()
        {
            if (!IsOpen || _soul == null) return;
            Hide();
            StartLife?.Invoke();
        }

        public void PressFacility(string id)
        {
            if (!IsOpen || _soul == null) return;
            var f = _soul.Data.Cheonha.Inn.Facility(id);
            bool open = Inn.IsOpen(_soul, id);
            if (!open)
            {
                if (f.BuildBloodCrystal > 0 && Inn.MeetsRequirement(_soul, f))
                {
                    if (Inn.TryBuild(_soul, id)) { SoulStore.Save(_soul); Toast(Loc.F("ui.inn.built", Name(f))); Refresh(); }
                    else Toast(Loc.F("ui.inn.need_crystal", f.BuildBloodCrystal));
                }
                else Toast(Loc.F("ui.inn.locked_toast", Name(f), Requirement(f)));
                return;
            }
            if (!Inn.CanEnter(_soul, id)) { Toast(Loc.F("ui.inn.repair_toast", Name(f))); return; }
            switch (id)
            {
                case "training": OpenTraining(); break;
                case "smithy": OpenSmithy(); break;
                case "apothecary": OpenApothecary(); break;
                default: Toast(Loc.F("ui.inn.soon_toast", Name(f))); break;
            }
        }

        public void PressSettings()
        {
            if (!IsOpen || SettingsScreen.AnyOpen) return;
            _settings = SettingsScreen.Show(font, _ => { _settings = null; Refresh(); });
        }

        public void PressTitle()
        {
            if (!IsOpen) return;
            SoulStore.Save(_soul);
            SceneManager.LoadScene("Boot");
        }

        void OpenTraining()
        {
            if (_training == null)
            {
                _training = new GameObject("연무장 화면").AddComponent<TrainingScreen>();
                _training.font = font;
                _training.Closed += () => { SoulStore.Save(_soul); _canvas.SetActive(true); Refresh(); };
            }
            _canvas.SetActive(false);
            _training.Open(_soul);
        }

        void OpenApothecary()
        {
            if (_apothecary == null)
            {
                _apothecary = new GameObject("약방 화면").AddComponent<ApothecaryScreen>();
                _apothecary.font = font;
                _apothecary.Closed += () => { _canvas.SetActive(true); Refresh(); };
            }
            _canvas.SetActive(false);
            _apothecary.Open(_soul);
        }

        void OpenSmithy()
        {
            if (_smithy == null)
            {
                _smithy = new GameObject("대장간 화면").AddComponent<SmithyScreen>();
                _smithy.font = font;
                _smithy.Closed += () => { SoulStore.Save(_soul); _canvas.SetActive(true); Refresh(); };
            }
            _canvas.SetActive(false);
            _smithy.Open(_soul, bodyPreview);
        }

        void Update()
        {
            if (_toastLeft > 0f)
            {
                _toastLeft -= Time.unscaledDeltaTime;
                if (_toastLeft <= 0f && _toast != null) _toast.text = "";
            }
            if (!IsOpen || SettingsScreen.AnyOpen || Time.frameCount == SettingsScreen.ClosedFrame) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) PressStart();
            else if (kb.digit1Key.wasPressedThisFrame) PressFacility(FacilityOrder[0]);
            else if (kb.digit2Key.wasPressedThisFrame) PressFacility(FacilityOrder[1]);
            else if (kb.digit3Key.wasPressedThisFrame) PressFacility(FacilityOrder[2]);
            else if (kb.digit4Key.wasPressedThisFrame) PressFacility(FacilityOrder[3]);
            else if (kb.digit5Key.wasPressedThisFrame) PressFacility(FacilityOrder[4]);
            else if (kb.digit6Key.wasPressedThisFrame) PressFacility(FacilityOrder[5]);
        }

        // ---- 그리기 ----

        void Build()
        {
            _canvas = new GameObject("객잔 본채", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas.transform.SetParent(transform, false);
            var canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20; // 침방(0) 위
            var sc = _canvas.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)_canvas.transform;

            var bg = Img(_root, "배경", background != null ? Color.white : Paper);
            bg.sprite = background; bg.preserveAspect = false; bg.raycastTarget = true;
            Stretch(bg.rectTransform);
            if (background != null) { var dim = Img(_root, "어둡게", new Color(0.05f, 0.04f, 0.04f, 0.35f)); Stretch(dim.rectTransform); }

            // 가운데 종이 한 장
            var sheet = Img(_root, "종이", new Color(Paper.r, Paper.g, Paper.b, 0.95f));
            Place(sheet.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(1240, 940));

            var title = Text(_root, Loc.T("ui.inn.title"), 54, FontStyles.Bold, Ink);
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 400), new Vector2(1100, 70));
            _sub = Text(_root, "", 24, FontStyles.Normal, new Color(Ink.r, Ink.g, Ink.b, 0.7f));
            Place(_sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 345), new Vector2(1180, 36));

            // 새 생 시작(침방)
            var start = MakeButton(_root, "새 생 시작", Blood, out var sl, 40, PaperHot);
            Place((RectTransform)start.transform, new Vector2(0.5f, 0.5f), new Vector2(0, 245), new Vector2(720, 110));
            sl.text = $"{Loc.T("ui.inn.start")}\n<size=50%><alpha=#CC>{Loc.T("ui.inn.start_sub")}  ·  Enter</size>";
            start.onClick.AddListener(PressStart);

            var head = Text(_root, Loc.T("ui.inn.facilities"), 26, FontStyles.Bold, Ink);
            Place(head.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(1000, 36));

            _grid = new GameObject("시설", typeof(RectTransform)).GetComponent<RectTransform>();
            _grid.SetParent(_root, false);
            Place(_grid, new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(1160, 380));
            var g = _grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(370, 180); g.spacing = new Vector2(20, 20);
            g.childAlignment = TextAnchor.UpperCenter;
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = 3;

            var settings = MakeButton(_root, "설정", new Color(0.25f, 0.20f, 0.17f), out var stl, 24, Paper);
            Place((RectTransform)settings.transform, new Vector2(0.5f, 0.5f), new Vector2(-170, -380), new Vector2(300, 56));
            stl.text = Loc.T("ui.inn.settings");
            settings.onClick.AddListener(PressSettings);
            var toTitle = MakeButton(_root, "타이틀로", new Color(0.25f, 0.20f, 0.17f), out var ttl, 24, Paper);
            Place((RectTransform)toTitle.transform, new Vector2(0.5f, 0.5f), new Vector2(170, -380), new Vector2(300, 56));
            ttl.text = Loc.T("ui.inn.to_title");
            toTitle.onClick.AddListener(PressTitle);

            _toast = Text(_root, "", 24, FontStyles.Bold, Blood);
            Place(_toast.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -440), new Vector2(1200, 40));
        }

        public void Refresh()
        {
            if (_soul == null || _grid == null) return;
            var c = _soul.Data.Cheonha;
            _sub.text = Loc.F("ui.inn.sub", _soul.Lives + 1, _soul.RebirthQi.ToString("N0"), _soul.BloodCrystal, _soul.SealStoneCount, c.Bosses.FinalRequires.SealStones);
            for (int i = _grid.childCount - 1; i >= 0; i--) Destroy(_grid.GetChild(i).gameObject);
            for (int i = 0; i < FacilityOrder.Length; i++) Door(FacilityOrder[i], i + 1);
        }

        void Door(string id, int key)
        {
            var f = _soul.Data.Cheonha.Inn.Facility(id);
            bool open = Inn.IsOpen(_soul, id), enter = Inn.CanEnter(_soul, id);
            bool buildable = !open && f.BuildBloodCrystal > 0 && Inn.MeetsRequirement(_soul, f);
            bool ready = enter && id == "training"; // 지금 화면이 있는 시설
            var color = ready ? new Color(0.30f, 0.24f, 0.18f) : buildable ? new Color(0.45f, 0.33f, 0.12f) : open ? new Color(0.42f, 0.38f, 0.33f) : new Color(0.62f, 0.58f, 0.52f);
            var b = MakeButton(_grid, id, color, out var label, 30, open || buildable ? PaperHot : new Color(0.95f, 0.92f, 0.86f, 0.75f));
            string state = ready ? Loc.T("ui.inn.enter")
                : buildable ? Loc.F("ui.inn.build", f.BuildBloodCrystal)
                : open && !enter ? Loc.T("ui.inn.under_repair")
                : open ? Loc.T("ui.inn.soon")
                : Requirement(f);
            label.text = $"<size=60%><alpha=#99>{key}<alpha=#FF></size>  {Name(f)}\n<size=55%>{Loc.T($"ui.inn.desc.{id}")}</size>\n<size=58%><b>{state}</b></size>";
            label.textWrappingMode = TextWrappingModes.Normal;
            label.margin = new Vector4(14, 8, 14, 8);
            b.onClick.AddListener(() => PressFacility(id));
        }

        static string Name(FacilityDef f) => Loc.T($"cheonha.inn.facilities.{f.Id}.name", f.NameKo);

        string Requirement(FacilityDef f)
        {
            var r = f.Require;
            var parts = new List<string>();
            if (r.Prologue && !_soul.PrologueDone) parts.Add(Loc.T("ui.inn.req.prologue"));
            if (r.SealStones > 0) parts.Add(Loc.F("ui.inn.req.seal", r.SealStones));
            if (r.BossKilled != null) parts.Add(Loc.T("ui.inn.req.boss"));
            if (f.BuildBloodCrystal > 0) parts.Add(Loc.F("ui.inn.req.crystal", f.BuildBloodCrystal));
            return parts.Count == 0 ? Loc.T("ui.inn.locked") : string.Join(" · ", parts);
        }

        void Toast(string s) { if (_toast == null) return; _toast.text = s; _toastLeft = 3f; }

        // ---- UI 조각 ----

        Image Img(Transform parent, string name, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var i = go.GetComponent<Image>(); i.color = c; i.raycastTarget = false;
            return i;
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

        Button MakeButton(Transform parent, string name, Color color, out TextMeshProUGUI label, float size, Color textColor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            var b = go.GetComponent<Button>();
            var cb = b.colors;
            cb.highlightedColor = new Color(1.15f, 1.1f, 1.0f); cb.pressedColor = new Color(0.85f, 0.82f, 0.78f);
            b.colors = cb;
            label = Text(go.transform, "", size, FontStyles.Bold, textColor);
            Stretch(label.rectTransform);
            return b;
        }

        static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
    }

    /// <summary>객잔에서 연 화면 왼쪽 위 '객잔으로' 단추(연무장·침방이 같이 쓴다).</summary>
    public static class InnBack
    {
        public static Button Add(RectTransform root, TMP_FontAsset font, Action onBack)
        {
            var go = new GameObject("객잔으로", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(root, false);
            go.GetComponent<Image>().color = new Color(0.25f, 0.20f, 0.17f, 0.95f);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(28, -24); rt.sizeDelta = new Vector2(230, 54);
            var t = new GameObject("글", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            t.transform.SetParent(go.transform, false);
            if (font != null) t.font = font;
            t.text = $"{Loc.T("ui.inn.back")} <size=70%><alpha=#99>Esc</size>";
            t.fontSize = 24; t.fontStyle = FontStyles.Bold; t.color = new Color(0.95f, 0.91f, 0.82f);
            t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
            var trt = t.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            var b = go.GetComponent<Button>();
            b.onClick.AddListener(() => onBack());
            return b;
        }
    }
}
