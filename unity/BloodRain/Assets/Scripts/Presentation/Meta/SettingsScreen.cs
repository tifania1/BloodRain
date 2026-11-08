using System;
using BloodRain.Core.Combat;
using BloodRain.Core.Meta;
using BloodRain.Presentation.Combat;
using BloodRain.Presentation.Fx;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UI;

namespace BloodRain.Presentation.Meta
{
    // WP-57U 설정 화면(기획서 12장 화면 목록 '설정'·'화면 편의 규칙', 웹 프로토타입 SET_HTML·SET_TURN).
    //   타이틀 '설정'과 일시정지 '설정'이 연다. 단추 하나 = GameSettings 낱말 하나, 누르면 Turn → Save(PlayerPrefs) → Apply.
    //   값은 SettingsScreen.Current(처음 읽을 때 PlayerPrefs 에서 Load)에 있고, 쓰는 곳은 이걸 읽는다.
    //   지금 Apply 가 바로 거는 것: 음량·소리 끄기(AudioListener.volume), 언어(Loc.Lang), 조준 방식(WeaponRunner.SetAimMode),
    //   효과 간소화(FxSettings). 나머지(흔들림·각성 연출·깜빡임·체력바·혈우·내 이펙트·피해 숫자·방향 표시)는 값만 저장되고
    //   쓰는 곳이 하나씩 Current 를 읽게 잇는다(unity_tasks WP-57U 진행 줄).
    //   Esc·돌아가기로 닫는다. 언어가 바뀌면 onClose(true) — 부른 쪽이 제 화면을 다시 그린다.
    public sealed class SettingsScreen : MonoBehaviour
    {
        public TMP_FontAsset font;

        static readonly Color Paper = new Color(0.93f, 0.89f, 0.80f, 0.98f);
        static readonly Color Ink = new Color(0.12f, 0.10f, 0.09f);
        static readonly Color Gold = new Color32(190, 146, 52, 255);

        static GameSettings _current;

        /// <summary>지금 설정. 처음 읽을 때 PlayerPrefs 에서 읽고 Apply 한다.</summary>
        public static GameSettings Current
        {
            get
            {
                if (_current == null)
                {
                    _current = GameSettings.Load(k => PlayerPrefs.HasKey(k) ? PlayerPrefs.GetString(k) : null,
                        reducedMotion: false, systemLanguage: Application.systemLanguage.ToString());
                    Apply(_current, false);
                }
                return _current;
            }
        }

        /// <summary>열린 설정 화면이 있는가(일시정지가 Esc 를 넘겨받지 않게).</summary>
        public static bool AnyOpen => _openCount > 0;
        /// <summary>마지막으로 닫힌 프레임(같은 Esc 로 일시정지까지 닫히지 않게).</summary>
        public static int ClosedFrame { get; private set; } = -1;
        static int _openCount;

        RectTransform _root;
        RectTransform _list;
        Action<bool> _onClose;
        string _langAtOpen;

        public bool IsOpen => _root != null;

        /// <summary>새 설정 화면을 띄운다. 닫히면 onClose(언어가 바뀌었나).</summary>
        public static SettingsScreen Show(TMP_FontAsset font, Action<bool> onClose)
        {
            var s = new GameObject("설정 화면").AddComponent<SettingsScreen>();
            s.font = font;
            s.Open(onClose);
            return s;
        }

        public void Open(Action<bool> onClose)
        {
            if (IsOpen) return;
            _onClose = onClose;
            _langAtOpen = Current.Language;
            if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            _openCount++;
            Build();
        }

        public void Close()
        {
            if (!IsOpen) return;
            Destroy(_root.gameObject);
            _root = null;
            _openCount = Mathf.Max(0, _openCount - 1);
            ClosedFrame = Time.frameCount;
            bool langChanged = Current.Language != _langAtOpen;
            var cb = _onClose; _onClose = null;
            cb?.Invoke(langChanged);
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            _listenSub?.Dispose(); _listenSub = null;
            if (_root != null) _openCount = Mathf.Max(0, _openCount - 1);
        }

        void Update()
        {
            if (!IsOpen) return;
            if (_listen != null || _listenEndedFrame == Time.frameCount) return; // 키를 받는 중엔 Esc 도 키로 받는다
            var kb = Keyboard.current; var pad = Gamepad.current;
            if ((kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && (pad.buttonEast.wasPressedThisFrame || pad.startButton.wasPressedThisFrame)))
            {
                if (_controlsPage) { _controlsPage = false; Refill(); }
                else Close();
            }
        }

        // ---------------------------------------------------------------- 값 걸기

        /// <summary>바로 걸리는 값을 건다. 언어는 바뀐 때만(캐시를 비우므로).</summary>
        public static void Apply(GameSettings s, bool languageToo = true)
        {
            AudioListener.volume = s.MutedOn ? 0f : s.VolumeScale;
            WeaponRunner.SetAimMode(s.Aim);
            // Save 가 PlayerPrefs "lang" 을 먼저 써 두므로 Loc.Lang 이 아니라 지금 읽힌 표(Loc.Current.Lang)와 견준다
            if (languageToo && Loc.Current.Lang != s.Language) Loc.Lang = s.Language;
        }

        void Turn(string key)
        {
            var s = Current;
            if (key == FxSimpleKey) FxSettings.Simplified = !FxSettings.Simplified;
            else if (!s.Turn(key)) return;
            s.Save((k, v) => PlayerPrefs.SetString(k, v));
            PlayerPrefs.Save();
            Apply(s);
            Refill();
        }

        const string FxSimpleKey = "fx_simplified";

        static readonly string[] Order =
        {
            GameSettings.Shake, GameSettings.Awaken, GameSettings.Flash, GameSettings.BossBar, GameSettings.Rain,
            GameSettings.Volume, GameSettings.Muted, GameSettings.Effects, GameSettings.Numbers, GameSettings.AimModeKey,
            GameSettings.AimMarker, FxSimpleKey, GameSettings.Lang, GameSettings.Voice,
        };

        static string Pct(float v) => Mathf.RoundToInt(v * 100f).ToString();

        /// <summary>단추 글(큰 줄, 작은 줄). 웹 SET_HTML 과 같은 낱말.</summary>
        public static (string label, string sub) Label(GameSettings s, string key)
        {
            switch (key)
            {
                case GameSettings.Shake: return (Loc.F("ui.settings.shake", Pct(s.ShakeScale)), Loc.T(s.ShakeScale > 0f ? "ui.settings.shake_sub" : "ui.settings.shake_off_sub"));
                case GameSettings.Awaken:
                    string a = s.AwakenMode == AwakenScene.Long ? "long" : s.AwakenMode == AwakenScene.Short ? "short" : "auto";
                    return (Loc.T($"ui.settings.awaken_{a}"), Loc.T($"ui.settings.awaken_{a}_sub"));
                case GameSettings.Flash: return s.FlashOn ? (Loc.T("ui.settings.flash_on"), Loc.T("ui.settings.flash_on_sub")) : (Loc.T("ui.settings.flash_off"), Loc.T("ui.settings.flash_off_sub"));
                case GameSettings.BossBar: return (Loc.T(s.BossBarOn ? "ui.settings.bossbar_on" : "ui.settings.bossbar_off"), Loc.T("ui.settings.bossbar_sub"));
                case GameSettings.Rain: return (Loc.F("ui.settings.rain", Pct(s.RainScale)), Loc.T(s.RainScale > 0f ? "ui.settings.rain_sub" : "ui.settings.rain_off_sub"));
                case GameSettings.Volume: return (Loc.F("ui.settings.volume", Pct(s.VolumeScale)), Loc.T(s.MutedOn ? "ui.settings.volume_muted_sub" : "ui.settings.volume_sub"));
                case GameSettings.Muted: return (Loc.T(s.MutedOn ? "ui.settings.mute_on" : "ui.settings.mute_off"), "");
                case GameSettings.Effects: return (Loc.F("ui.settings.fx", Pct(s.EffectsAlpha)), Loc.T(s.EffectsAlpha < 1f ? "ui.settings.fx_low_sub" : "ui.settings.fx_sub"));
                case GameSettings.Numbers: return s.NumbersOn ? (Loc.T("ui.settings.nums_on"), Loc.T("ui.settings.nums_on_sub")) : (Loc.T("ui.settings.nums_off"), Loc.T("ui.settings.nums_off_sub"));
                case GameSettings.AimModeKey: return s.Aim == AimMode.Move ? (Loc.T("ui.settings.aim_move"), Loc.T("ui.settings.aim_move_sub")) : (Loc.T("ui.settings.aim_near"), Loc.T("ui.settings.aim_near_sub"));
                case GameSettings.AimMarker:
                    if (s.AimMarkerScale <= 0f) return (Loc.T("ui.settings.marker_off"), Loc.T("ui.settings.marker_off_sub"));
                    return (Loc.T(s.AimMarkerScale > 1f ? "ui.settings.marker_large" : "ui.settings.marker_normal"), Loc.T("ui.settings.marker_sub"));
                case FxSimpleKey: return (Loc.T(FxSettings.Simplified ? "ui.settings.fx_simple_on" : "ui.settings.fx_simple_off"), Loc.T("ui.settings.fx_simple_sub"));
                case GameSettings.Lang:
                    string ln = GameSettings.LanguageNames.TryGetValue(s.Language, out var n) ? n : s.Language;
                    return (Loc.F("ui.settings.lang", ln), Loc.T("ui.settings.lang_sub"));
                case GameSettings.Voice: return (Loc.F("ui.settings.voice", Loc.T($"ui.settings.voice_{s.VoiceLanguage}")), Loc.T("ui.settings.voice_sub"));
            }
            return (key, "");
        }

        // ---------------------------------------------------------------- 화면

        void Build()
        {
            var go = new GameObject("설정", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)go.transform;
            var dim = Img(_root, new Color(0.02f, 0.02f, 0.03f, 0.9f));
            Stretch(dim.rectTransform);
            _list = new GameObject("단추들", typeof(RectTransform)).GetComponent<RectTransform>();
            _list.SetParent(_root, false);
            Stretch(_list);
            Refill();
        }

        void Refill()
        {
            for (int i = _list.childCount - 1; i >= 0; i--) Destroy(_list.GetChild(i).gameObject);
            if (_controlsPage) { FillControls(); return; }
            var s = Current;
            var title = Text(_list, Loc.T("ui.settings.title"), 50, FontStyles.Bold, Paper);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -90), new Vector2(1000, 66));

            const float w = 560, h = 82, gapX = 40, gapY = 14;
            int rows = (Order.Length + 1) / 2;
            float top = 320;
            for (int i = 0; i < Order.Length; i++)
            {
                int col = i / rows, row = i % rows;
                var pos = new Vector2((col == 0 ? -1 : 1) * (w + gapX) / 2f, top - row * (h + gapY));
                string key = Order[i];
                var (label, sub) = Label(s, key);
                Item(pos, new Vector2(w, h), label, sub, () => Turn(key));
            }
            Item(new Vector2(-180, -400), new Vector2(320, 66), Loc.T("ui.settings.controls"), "", () => { _controlsPage = true; _refused = null; Refill(); });
            Item(new Vector2(180, -400), new Vector2(320, 66), Loc.T("ui.settings.back"), "", Close);
        }

        // ---------------------------------------------------------------- 조작 재설정(WP-69U)
        //   행동마다 키보드 두 칸·게임패드 한 칸·기본값. 칸을 누르면 다음에 누른 단추를 받아 Controls.Rebind(맞바꿈),
        //   못 고르는 키(1~4·Enter, 칸과 기기가 다름)면 '쓸 수 없는 키'. 바뀌면 ControlInput.Apply 가 얹고 저장한다.

        bool _controlsPage;
        string _listen;      // 키를 받는 행동(null 이면 안 받음)
        int _listenSlot;
        int _listenStartFrame, _listenEndedFrame = -1;
        IDisposable _listenSub;
        string _refused;     // 방금 거절된 키 글

        void FillControls()
        {
            var c = ControlInput.Current;
            var title = Text(_list, Loc.T("ui.controls.title"), 50, FontStyles.Bold, Paper);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -90), new Vector2(1000, 66));
            if (_refused != null)
            {
                var r = Text(_list, $"{Loc.T("ui.controls.refused")} · {_refused}", 26, FontStyles.Normal, new Color(0.95f, 0.45f, 0.4f));
                Place(r.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -140), new Vector2(1000, 36));
            }
            const float h = 58, gapY = 10, top = 330;
            var acts = ControlAction.All;
            for (int i = 0; i < acts.Count; i++)
            {
                string a = acts[i];
                float y = top - i * (h + gapY);
                var name = Text(_list, Loc.T($"ui.controls.{a}"), 30, FontStyles.Bold, Paper);
                name.alignment = TextAlignmentOptions.Right;
                Place(name.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-560, y), new Vector2(360, h));
                for (int slot = 0; slot <= Controls.Pad; slot++)
                {
                    int sl = slot;
                    string label = _listen == a && _listenSlot == slot ? Loc.T("ui.controls.listen")
                        : ControlInput.Label(a, slot) ?? Loc.T("ui.controls.none");
                    Item(new Vector2(-230 + slot * 290, y), new Vector2(270, h), label, "", () => Listen(a, sl));
                }
                Item(new Vector2(640, y), new Vector2(180, h), Loc.T("ui.controls.reset"), "", () =>
                {
                    StopListen();
                    var cc = ControlInput.Current; cc.Reset(a); ControlInput.Apply(cc);
                    _refused = null; Refill();
                });
            }
            Item(new Vector2(0, -400), new Vector2(320, 66), Loc.T("ui.settings.back"), "", () => { StopListen(); _controlsPage = false; Refill(); });
        }

        void Listen(string action, int slot)
        {
            StopListen();
            _listen = action; _listenSlot = slot; _listenStartFrame = Time.frameCount; _refused = null;
            // 다음에 누른 단추 하나. 칸을 누른 그 마우스 왼쪽은 넘기고, 마우스 왼쪽을 다시 누르면 그만둔다(UI 를 눌러야 하니)
            _listenSub = InputSystem.onAnyButtonPress.Call(ctrl =>
            {
                if (_listen == null || Time.frameCount == _listenStartFrame) return;
                string dev = ctrl.device is Keyboard ? "Keyboard" : ctrl.device is Mouse ? "Mouse" : ctrl.device is Gamepad ? "Gamepad" : null;
                string act = _listen; int sl = _listenSlot;
                StopListen();
                if (dev == null || (ctrl.device is Mouse && ctrl.name == "leftButton")) { Refill(); return; }
                // "/XInputControllerWindows/dpad/up" 꼴에서 기기 몫을 떼어 "<Gamepad>/dpad/up" 으로
                string p = $"<{dev}>{ctrl.path.Substring(ctrl.device.path.Length)}";
                var c = ControlInput.Current;
                if (c.Rebind(act, sl, p)) ControlInput.Apply(c);
                else _refused = InputControlPath.ToHumanReadableString(p, InputControlPath.HumanReadableStringOptions.OmitDevice);
                Refill();
            });
            Refill();
        }

        void StopListen()
        {
            if (_listen != null) _listenEndedFrame = Time.frameCount;
            _listen = null;
            _listenSub?.Dispose(); _listenSub = null;
        }

        void Item(Vector2 pos, Vector2 size, string label, string sub, Action onClick)
        {
            var frame = Img(_list, Gold);
            Place(frame.rectTransform, new Vector2(0.5f, 0.5f), pos, size + new Vector2(6, 6));
            var b = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button)).GetComponent<Button>();
            b.transform.SetParent(frame.transform, false);
            b.GetComponent<Image>().color = Paper;
            Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            b.onClick.AddListener(() => onClick());
            var t = Text(b.transform, string.IsNullOrEmpty(sub) ? label : $"{label}\n<size=62%><alpha=#99>{sub}", 30, FontStyles.Bold, Ink);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.enableAutoSizing = true; t.fontSizeMin = 16; t.fontSizeMax = 30;
            var rt = t.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(16, 6); rt.offsetMax = new Vector2(-16, -6);
        }

        Image Img(Transform parent, Color c)
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
