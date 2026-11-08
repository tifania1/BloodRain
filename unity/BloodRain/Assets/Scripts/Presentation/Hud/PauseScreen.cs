using System.Collections.Generic;
using System.Linq;
using System.Text;
using BloodRain.Core.Build;
using BloodRain.Core.Run;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BloodRain.Presentation.Hud
{
    // PS-01U 일시정지 화면(기획서 12장 화면 목록 '일시정지', 웹 프로토타입 #pause).
    // Esc·패드 Start 로 연다. 레벨업·깨달음·보갑·각성 같은 다른 화면이 세상을 멈춰 두었으면(timeScale 0) 열지 않는다.
    // 본문: 왼쪽 지닌 무공·심법 칸과 경지·그릇(HudSlots.Build, TopHud 와 같은 셈), 가운데 능력치(이름 + 쉬운 말),
    //       오른쪽 무공별 지금까지 피해·초당 피해(DamageReport.Lines — 정산 막대와 같은 WeaponRunner.Dealt 합).
    // 단추: 계속하기 · 설정(WP-57U SettingsScreen) · 좌화(LifeRunner.EndLife(Zuohwa), 서장 몸이면 잠금).
    // 절기 조합표 탭은 UI-01U 몫이라 자리만. 무공 줄을 눌러 짝·진전·연환을 펼치는 것도 UI-01U 때.
    public sealed class PauseScreen : MonoBehaviour
    {
        public LifeRunner runner;
        public TMP_FontAsset font;

        static readonly Color Paper = new Color(0.93f, 0.89f, 0.80f, 0.98f);
        static readonly Color Ink = new Color(0.12f, 0.10f, 0.09f);
        static readonly Color Gold = new Color32(190, 146, 52, 255);
        static readonly Color Blood = new Color32(150, 24, 22, 255);

        RectTransform _root;
        float _prevScale = 1f;
        // 무공을 얻은 생의 시각(초). Loadout 에 처음 보인 때를 1초마다 적는다(초당 피해의 분모)
        readonly Dictionary<string, float> _acquiredAt = new Dictionary<string, float>();
        float _scanTimer;

        public bool IsOpen => _root != null;

        float LifeSeconds => runner != null && runner.Life != null ? (float)(runner.Life.Minutes * 60.0) : 0f;

        void Update()
        {
            if (runner == null || runner.Life == null) return;
            if (IsOpen)
            {
                if (Time.timeScale != 0f) Time.timeScale = 0f;
                // 설정 화면이 떠 있거나 같은 Esc 로 방금 닫혔으면 그 Esc 는 설정 몫
                if (BloodRain.Presentation.Meta.SettingsScreen.AnyOpen || BloodRain.Presentation.Meta.SettingsScreen.ClosedFrame == Time.frameCount) return;
                if (PressedPause()) Close();
                return;
            }
            _scanTimer -= Time.unscaledDeltaTime;
            if (_scanTimer <= 0f) { _scanTimer = 1f; ScanAcquired(); }
            // 다른 화면(보갑·검총)이 같은 Esc 로 닫히며 세상을 푼 그 프레임엔 열지 않는다
            bool frozenLastFrame = _wasFrozen;
            _wasFrozen = Time.timeScale <= 0f;
            if (!frozenLastFrame && PressedPause() && CanOpen()) Open();
        }

        bool _wasFrozen;

        static bool PressedPause()
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            return (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame);
        }

        bool CanOpen()
        {
            if (runner.Life.Record != null) return false;     // 생이 끝나 정산 중
            if (Time.timeScale <= 0f) return false;            // 레벨업·깨달음·보갑·보부상 등이 세상을 멈춤
            var lu = FindFirstObjectByType<LevelUpScreen>();
            if (lu != null && lu.IsOpen) return false;
            if (runner.InsightBusy) return false;              // 깨달음 줄·각성 연출
            return true;
        }

        void ScanAcquired()
        {
            var life = runner.Life;
            var view = HudSlots.Build(life.Loadout, life.Level);
            float now = LifeSeconds;
            foreach (var s in view.Weapons)
                if (s.Id != null && !_acquiredAt.ContainsKey(s.Id)) _acquiredAt[s.Id] = now;
        }

        public void Open()
        {
            if (IsOpen) return;
            ScanAcquired();
            _prevScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Build();
        }

        public void Close()
        {
            if (!IsOpen) return;
            Destroy(_root.gameObject);
            _root = null;
            Time.timeScale = _prevScale;
        }

        /// <summary>WP-57U 설정 화면. 언어가 바뀌었으면 닫힌 뒤 이 화면을 새 글로 다시 그린다.</summary>
        public void OpenSettings()
        {
            if (!IsOpen || BloodRain.Presentation.Meta.SettingsScreen.AnyOpen) return;
            BloodRain.Presentation.Meta.SettingsScreen.Show(font, langChanged =>
            {
                if (!langChanged || !IsOpen) return;
                Destroy(_root.gameObject);
                _root = null;
                Build();
            });
        }

        void Zuohwa()
        {
            if (!IsOpen) return;
            Destroy(_root.gameObject);
            _root = null;
            Time.timeScale = _prevScale;
            runner.EndLife(LifeEndWay.Zuohwa); // 쓰러짐과 같은 정산(정산이 다시 세상을 멈춘다)
        }

        // ---------------------------------------------------------------- 화면

        void Build()
        {
            var life = runner.Life;
            var go = new GameObject("일시정지", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)go.transform;

            var dim = Panel(_root, new Color(0.02f, 0.02f, 0.03f, 0.72f));
            Stretch(dim.rectTransform);

            var title = Text(_root, Loc.T("ui.pause.title"), 54, FontStyles.Bold, Paper);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -90), new Vector2(1200, 70));
            var sub = Text(_root, Loc.T("ui.pause.sub"), 22, FontStyles.Italic, new Color(Paper.r, Paper.g, Paper.b, 0.7f));
            Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -140), new Vector2(1200, 32));

            // 세 판: 칸 · 능력치 · 무공별 피해
            var left = Sheet(new Vector2(-560, 20), new Vector2(460, 640));
            var mid = Sheet(new Vector2(-40, 20), new Vector2(520, 640));
            var right = Sheet(new Vector2(520, 20), new Vector2(560, 640));
            FillSlots(left, life);
            FillStats(mid);
            FillDamage(right);

            // 단추 줄
            Button(Loc.T("ui.pause.resume"), new Vector2(-300, 70), Gold, true, Close);
            Button(Loc.T("ui.settings.title"), new Vector2(0, 70), Gold, true, OpenSettings); // WP-57U
            Button(Loc.T("ui.settle.end.zuohwa"), new Vector2(300, 70), Blood, !life.IsPrologue, Zuohwa);
        }

        RectTransform Sheet(Vector2 pos, Vector2 size)
        {
            var frame = Panel(_root, Gold);
            Place(frame.rectTransform, new Vector2(0.5f, 0.5f), pos, size + new Vector2(6, 6));
            var p = Panel(frame.transform, Paper);
            Place(p.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            return p.rectTransform;
        }

        void FillSlots(RectTransform sheet, Life life)
        {
            var view = HudSlots.Build(life.Loadout, life.Level);
            var sb = new StringBuilder();
            sb.Append("<b>").Append(Loc.Realm(life.Xp.Realm)).Append("</b>");
            if (view.VesselsTotal > 0)
            {
                sb.Append("  ");
                for (int i = 0; i < view.VesselsTotal; i++) sb.Append(i < view.VesselsUsed ? '◆' : '◇');
            }
            sb.Append("\n\n");
            foreach (var s in view.Weapons) sb.Append(SlotLine(s, true)).Append('\n');
            sb.Append('\n');
            foreach (var s in view.Passives) sb.Append(SlotLine(s, false)).Append('\n');
            var t = Body(sheet, sb.ToString(), 24);
        }

        static string SlotLine(HudSlot s, bool weapon)
        {
            if (s.Id == null) return "<alpha=#55>—";
            string name = NameOf(s.Id, weapon);
            string mark = s.Glow switch
            {
                SlotGlow.Evolved => " <color=#BE9234>◆</color>",
                SlotGlow.Ready => " <color=#BE9234>◇</color>",
                SlotGlow.Waiting => " <alpha=#88>◇",
                _ => "",
            };
            return $"{name}  <size=80%>Lv{s.Level}</size>{mark}";
        }

        void FillStats(RectTransform sheet)
        {
            var st = runner.Stats;
            if (st == null) { Body(sheet, "", 22); return; }
            var rows = new (string key, string value)[]
            {
                ("MaxHealth", st.MaxHealth.ToString("0")),
                ("Recovery", st.Recovery.ToString("0.#")),
                ("Armor", st.Armor.ToString("0.#")),
                ("Might", Ratio(st.Might)),
                ("CooldownReduction", Percent(st.CooldownReduction)),
                ("Area", Ratio(st.Area)),
                ("Amount", "+" + st.Amount),
                ("Duration", Ratio(st.Duration)),
                ("ProjectileSpeed", Ratio(st.ProjectileSpeed)),
                ("MoveSpeed", Ratio(st.MoveSpeed)),
                ("Magnet", st.Magnet.ToString("0.#")),
                ("Growth", Ratio(st.Growth)),
                ("Luck", "+" + st.Luck.ToString("0")),   // 기연은 그대로 더하는 수(도문심법·연무장 +10, 50이면 넷째 선택지) — % 가 아님
                ("Revival", st.Revival.ToString()),
            };
            var sb = new StringBuilder();
            foreach (var (key, value) in rows)
            {
                string easy = Loc.T($"ui.stat_easy.{key}"); // 없는 키는 키 그대로 나오니 아래에서 거른다
                sb.Append("<b>").Append(Loc.Stat(key)).Append("</b>");
                if (!string.IsNullOrEmpty(easy) && !easy.StartsWith("ui.")) sb.Append(" <size=82%><alpha=#99>") /* SD-01U 1280x800 에서 12px 넘게 */.Append(easy).Append("<alpha=#FF></size>");
                sb.Append("<pos=78%>").Append(value).Append('\n');
            }
            Body(sheet, sb.ToString(), 22);
        }

        void FillDamage(RectTransform sheet)
        {
            var weapons = runner.weapons;
            var dealt = weapons != null
                ? weapons.Dealt.Select(kv => new KeyValuePair<string, double>(kv.Key, kv.Value))
                : Enumerable.Empty<KeyValuePair<string, double>>();
            var lines = DamageReport.Lines(dealt, LifeSeconds, _acquiredAt).OrderByDescending(l => l.Dealt).ToList();
            var sb = new StringBuilder();
            double sum = lines.Sum(l => l.Dealt);
            foreach (var l in lines)
            {
                sb.Append("<b>").Append(NameOf(l.Id, true)).Append("</b>")
                  .Append("<pos=52%>").Append(l.Dealt.ToString("N0"))
                  .Append("<pos=76%><size=80%>").Append(l.PerSecond.ToString("N0")).Append("/s</size>")
                  .Append("\n<size=60%><color=#96181688>").Append(new string('■', Mathf.Clamp(Mathf.RoundToInt(l.Share * 30), 0, 30))).Append("</color></size>\n");
            }
            if (lines.Count > 0) sb.Append("\n<alpha=#88>Σ  ").Append(sum.ToString("N0"));
            Body(sheet, sb.ToString(), 22);
            // 절기 조합표 탭 자리(UI-01U)
        }

        static string Ratio(float v) => "×" + v.ToString("0.00");
        static string Percent(float v) => (v * 100f).ToString("0") + "%";

        static string NameOf(string id, bool weapon)
        {
            var data = DataBoot.Data;
            if (data == null) return id;
            foreach (var w in data.Weapons) if (w.Id == id) return Loc.Weapon(w);
            foreach (var e in data.Evolutions) if (e.Id == id) return Loc.T($"evolutions.{e.Id}.name", e.NameKo);
            foreach (var p in data.Passives) if (p.Id == id) return Loc.T($"passives.{p.Id}.name", p.NameKo);
            return id;
        }

        // ---------------------------------------------------------------- 조각

        TextMeshProUGUI Body(RectTransform sheet, string text, float size)
        {
            var t = Text(sheet, text, size, FontStyles.Normal, Ink);
            t.alignment = TextAlignmentOptions.TopLeft;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.enableAutoSizing = true; t.fontSizeMin = 14; t.fontSizeMax = size;
            var rt = t.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(28, 24); rt.offsetMax = new Vector2(-28, -24);
            return t;
        }

        void Button(string label, Vector2 pos, Color edge, bool enabled, System.Action onClick)
        {
            var frame = Panel(_root, enabled ? edge : new Color(edge.r, edge.g, edge.b, 0.35f));
            Place(frame.rectTransform, new Vector2(0.5f, 0f), pos, new Vector2(266, 66));
            var b = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Button)).GetComponent<UnityEngine.UI.Button>();
            b.transform.SetParent(frame.transform, false);
            b.GetComponent<Image>().color = enabled ? Paper : new Color(Paper.r, Paper.g, Paper.b, 0.5f);
            Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260, 60));
            b.interactable = enabled;
            if (enabled && onClick != null) b.onClick.AddListener(() => onClick());
            var t = Text(b.transform, label, 28, FontStyles.Bold, enabled ? Ink : new Color(Ink.r, Ink.g, Ink.b, 0.4f));
            Stretch(t.rectTransform);
            t.enableAutoSizing = true; t.fontSizeMin = 16; t.fontSizeMax = 28;
        }

        Image Panel(Transform parent, Color c)
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
            t.textWrappingMode = TextWrappingModes.NoWrap;
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
