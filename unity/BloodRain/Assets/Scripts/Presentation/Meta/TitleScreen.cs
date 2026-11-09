using System.Linq;
using BloodRain.Presentation.Hud;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BloodRain.Presentation.Meta
{
    // TT-01U 타이틀 화면(기획서 12장 화면 목록 '타이틀'). Boot 씬에 붙어 게임을 켜면 처음 보이는 화면.
    //   바탕 bg_title(없으면 선지 #EFE6D2) 위에 ScreenRain 붉은 비, 붓 글꼴 '血雨江湖' + 낙관, 아래 단추 넷.
    //   시작: 저장 칸 고르기(SV-01Ub, SlotSelectScreen) — 빈 칸은 서장(Run), 혼이 든 칸은 객잔(Inn).
    //   이어하기: 혼이 든 칸이 있을 때만 보임 → 마지막으로 쓴 칸(SoulSlots.ContinueSlot)의 객잔.
    //   크레딧(TT-02U)은 CreditsScreen 두루마리.
    //   설정(WP-57U)·인트로 다시 보기(DL-07b)는 그 화면이 아직 없어 잠근 단추로 자리만. 생기면 OpenSettings·OpenIntro 를 잇는다.
    //   메인 테마는 bgm_title 이 Unity 에 들어오면 붙인다(지금 없어 무음).
    public sealed class TitleScreen : MonoBehaviour
    {
        public Sprite background;
        public TMP_FontAsset font;       // 본문(NotoSerif)
        public TMP_FontAsset brushFont;  // 제목(Yuji Boku 동적)

        static readonly Color PaperBg = new Color32(0xEF, 0xE6, 0xD2, 0xFF);
        static readonly Color Paper = new Color(0.94f, 0.90f, 0.82f, 0.92f);
        static readonly Color Ink = new Color(0.12f, 0.10f, 0.09f);
        static readonly Color Seal = new Color32(0xB3, 0x26, 0x1E, 0xFF);

        /// <summary>새 혼이 아닌 혼 저장이 있는가(이어하기를 보일지).</summary>
        public bool CanContinue { get; private set; }
        bool _leaving;
        int? _continueSlot;
        ScreenRain _rain; // 설정을 닫으면 '혈우 연출' 세기를 다시 건다

        void Start()
        {
            var data = DataBoot.Data;
            SettingsScreen.Apply(SettingsScreen.Current); // WP-57U 저장된 음량·언어·조준을 처음에 건다
            _continueSlot = SlotSelectScreen.ContinueSlot(); // SV-01Ub 마지막으로 쓴 칸(없으면 가장 늦게 한 칸)
            CanContinue = _continueSlot != null;
            Build();
            Debug.Log($"[타이틀] 이어하기 칸 {(_continueSlot?.ToString() ?? "없음")}");
        }

        void Build()
        {
            if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var canvasGo = new GameObject("타이틀", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)canvasGo.transform;

            var bg = Child("바탕", root).gameObject.AddComponent<Image>();
            bg.sprite = background;
            bg.color = background != null ? Color.white : PaperBg;
            bg.preserveAspect = false;
            Stretch(bg.rectTransform);
            if (background != null) Cover(bg.rectTransform, background);

            var rain = Child("붉은 비", root).gameObject.AddComponent<ScreenRain>();
            rain.density = SettingsScreen.Current.RainScale; // WP-57U 설정 '혈우 연출'
            _rain = rain;
            Stretch(rain.rectTransform);

            var title = Text("제목", root, "血雨江湖", 150, FontStyles.Normal, Ink, brushFont != null ? brushFont : font);
            Place(title.rectTransform, new Vector2(0, 250), new Vector2(1200, 200));
            var sub = Text("부제", root, "혈우강호", 36, FontStyles.Normal, Ink, font);
            Place(sub.rectTransform, new Vector2(0, 120), new Vector2(800, 50));
            sub.characterSpacing = 30;

            // 낙관: 제목 오른쪽 아래 붉은 네모 도장
            var seal = Child("낙관", root).gameObject.AddComponent<Image>();
            seal.color = Seal;
            Place(seal.rectTransform, new Vector2(420, 170), new Vector2(64, 64));
            var sealText = Text("낙관 글", seal.transform, "血", 44, FontStyles.Normal, PaperBg, brushFont != null ? brushFont : font);
            Stretch(sealText.rectTransform);

            float y = -20; // 단추가 여섯(이어하기 포함)이라 화면 안에 들게
            Button("시작", root, Loc.T("ui.title.start"), ref y, true, StartGame);
            if (CanContinue) Button("이어하기", root, Loc.T("ui.title.continue"), ref y, true, ContinueGame);
            Button("설정", root, Loc.T("ui.settings.title"), ref y, true, OpenSettings); // WP-57U
            Button("크레딧", root, Loc.T("ui.title.credits"), ref y, true, OpenCredits);  // TT-02U
            Button("인트로", root, Loc.T("ui.title.intro"), ref y, false, null);         // DL-07b 가 생기면 잇는다
        }

        /// <summary>SV-01Ub 시작 → 저장 칸 고르기. 칸을 고르면 그 화면이 서장·객잔으로 보낸다.</summary>
        public void StartGame()
        {
            if (_leaving || SettingsScreen.AnyOpen || CreditsScreen.AnyOpen || SlotSelectScreen.AnyOpen) return;
            // 인트로 컷신(DL-07b)이 생기면 빈 칸을 골랐을 때 그것부터
            SlotSelectScreen.Show(font, () => Debug.Log("[타이틀] 칸 고르기 닫음"));
        }

        /// <summary>WP-57U 설정. 언어가 바뀌면 타이틀을 새 글로 다시 그린다.</summary>
        public void OpenSettings()
        {
            if (_leaving || SettingsScreen.AnyOpen || CreditsScreen.AnyOpen || SlotSelectScreen.AnyOpen) return;
            SettingsScreen.Show(font, langChanged =>
            {
                // QA-16: 언어가 그대로여도 '혈우 연출'을 바꿨으면 뒤의 비를 바로 맞춘다(전엔 다음에 타이틀을 열 때까지 그대로)
                if (_rain != null) _rain.density = SettingsScreen.Current.RainScale;
                if (!langChanged) return;
                for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
                Build();
            });
        }

        /// <summary>TT-02U 크레딧 두루마리. 끝나거나 Esc 면 타이틀로 돌아온다.</summary>
        public void OpenCredits()
        {
            if (_leaving || SettingsScreen.AnyOpen || CreditsScreen.AnyOpen || SlotSelectScreen.AnyOpen) return;
            CreditsScreen.Show(font, brushFont, () => Debug.Log("[타이틀] 크레딧 닫음"));
        }

        public void ContinueGame()
        {
            if (_leaving || !CanContinue || SlotSelectScreen.AnyOpen) return;
            _leaving = true;
            int n = _continueSlot.Value;
            Debug.Log($"[타이틀] 이어하기 → {n}번 칸");
            SlotSelectScreen.Enter(SlotSelectScreen.ReadSlots().First(x => x.Slot == n));
        }

        void Button(string name, RectTransform root, string label, ref float y, bool enabled, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(root, false);
            go.GetComponent<Image>().color = Paper;
            var b = go.GetComponent<Button>();
            var cb = b.colors;
            cb.highlightedColor = new Color(1.12f, 1.08f, 1.0f);
            cb.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.45f);
            b.colors = cb;
            b.interactable = enabled;
            if (onClick != null) b.onClick.AddListener(onClick);
            Place((RectTransform)go.transform, new Vector2(0, y), new Vector2(420, 76));
            var t = Text("글", go.transform, label, 32, FontStyles.Bold, Ink, font);
            if (!enabled) t.alpha = 0.55f;
            Stretch(t.rectTransform);
            y -= 86;
        }

        // 바탕 그림을 화면 비율에 맞춰 넘치게(cover) — 1920x1080 기준 캔버스 안에서 그림 비율을 지킨다
        static void Cover(RectTransform rt, Sprite s)
        {
            var fitter = rt.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = s.rect.width / Mathf.Max(1f, s.rect.height);
        }

        static RectTransform Child(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static TextMeshProUGUI Text(string name, Transform parent, string text, float size, FontStyles style, Color color, TMP_FontAsset f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (f != null) t.font = f;
            t.text = text; t.fontSize = size; t.fontStyle = style; t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }
    }
}
