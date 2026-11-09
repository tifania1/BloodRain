using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    // SV-01Ub 저장 칸 고르기(기획서 12장 화면 목록 '저장 칸 고르기'·'타이틀과 저장 칸'). 타이틀의 '시작'이 띄운다.
    //   선지 바탕 카드 셋. 혼이 든 칸은 SoulSlotSummary 값(몇 번째 생·가장 높은 경지·봉인석·밟은 땅·연무장 레벨 합·마지막으로 한 날),
    //   빈 칸은 '새 혼'. 카드를 고르면 SoulStore.Slot 을 그 칸으로, PlayerPrefs 에 마지막으로 쓴 칸을 적고 DestinationOf 대로 서장(Run)·객잔(Inn).
    //   칸 지우기: 혼이 든 카드 귀퉁이 '지우기' → 화면 아래 띠에서 두 번 묻기(SlotDeleteAsk). 따로 뜨는 확인 창은 없다.
    //   Esc·패드 B: 묻는 중이면 묻기를 그만두고, 아니면 타이틀로.
    public sealed class SlotSelectScreen : MonoBehaviour
    {
        public const string LastSlotKey = "lastSlot";

        public TMP_FontAsset font;

        static readonly Color PaperBg = new Color32(0xEF, 0xE6, 0xD2, 0xFF);
        static readonly Color Card = new Color(0.97f, 0.94f, 0.87f, 1f);
        static readonly Color Ink = new Color(0.12f, 0.10f, 0.09f);
        static readonly Color InkLight = new Color(0.12f, 0.10f, 0.09f, 0.6f);
        static readonly Color Seal = new Color32(0xB3, 0x26, 0x1E, 0xFF);

        public static bool AnyOpen { get; private set; }

        RectTransform _root;
        Action _onBack;
        readonly SlotDeleteAsk _ask = new SlotDeleteAsk();
        int _openedFrame;
        bool _leaving;

        public IReadOnlyList<SoulSlotSummary> Slots { get; private set; } = Array.Empty<SoulSlotSummary>();

        // ---------------------------------------------------------------- 칸 셈(타이틀 이어하기도 씀)

        /// <summary>칸 카드 값 셋. 파일이 없으면 빈 칸, 있으면 혼과 파일 시각으로.</summary>
        public static List<SoulSlotSummary> ReadSlots()
        {
            var data = DataBoot.Data;
            var list = new List<SoulSlotSummary>();
            foreach (var n in SoulSlots.All)
            {
                if (!SoulStore.SlotExists(n)) { list.Add(SoulSlotSummary.Empty(n)); continue; }
                var soul = SoulStore.LoadGame(data, n).Soul;
                var path = SoulStore.SlotPath(n);
                DateTime? when = File.Exists(path) ? File.GetLastWriteTime(path) : (DateTime?)null; // 깨져 .bad 로 갔으면 없음
                list.Add(SoulSlotSummary.From(soul, n, when));
            }
            return list;
        }

        public static int? LastUsedSlot => PlayerPrefs.HasKey(LastSlotKey) ? PlayerPrefs.GetInt(LastSlotKey) : (int?)null;

        /// <summary>이어하기로 갈 칸(없으면 null — 이어하기를 안 보임).</summary>
        public static int? ContinueSlot() => SoulSlots.ContinueSlot(LastUsedSlot, ReadSlots());

        /// <summary>칸을 지금 칸으로 두고 마지막으로 쓴 칸으로 적은 뒤 갈 곳으로.</summary>
        public static void Enter(SoulSlotSummary slot)
        {
            SoulStore.Slot = slot.Slot;
            PlayerPrefs.SetInt(LastSlotKey, slot.Slot);
            PlayerPrefs.Save();
            RebirthHandoff.Soul = null; // 다른 칸의 혼을 들고 넘어가지 않게
            var scene = SoulSlots.DestinationOf(slot) == SlotDestination.Inn ? "Inn" : "Run";
            Debug.Log($"[칸 고르기] {slot.Slot}번 칸 → {scene}");
            SceneManager.LoadScene(scene);
        }

        // ---------------------------------------------------------------- 화면

        public static SlotSelectScreen Show(TMP_FontAsset font, Action onBack)
        {
            var s = new GameObject("저장 칸 고르기").AddComponent<SlotSelectScreen>();
            s.font = font;
            s._onBack = onBack;
            s._openedFrame = Time.frameCount;
            AnyOpen = true;
            if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            s.Rebuild();
            return s;
        }

        void Rebuild()
        {
            if (_root != null) Destroy(_root.gameObject);
            Slots = ReadSlots();

            var canvasGo = new GameObject("칸 고르기", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)canvasGo.transform;

            var bg = Child("바탕", _root).gameObject.AddComponent<Image>();
            bg.color = PaperBg;
            Stretch(bg.rectTransform);

            Place(Text("머리", _root, Loc.T("ui.slot.title"), 56, FontStyles.Bold, Ink).rectTransform, new Vector2(0, 400), new Vector2(1600, 80));
            Place(Text("부제", _root, Loc.T("ui.slot.sub"), 28, FontStyles.Normal, InkLight).rectTransform, new Vector2(0, 335), new Vector2(1600, 40));

            for (int i = 0; i < Slots.Count; i++)
                BuildCard(Slots[i], new Vector2((i - 1) * 540, -10));

            if (_ask.Slot != null) BuildAskBar();
            else Button("돌아가기", _root, Loc.T("ui.slot.back"), new Vector2(0, -420), new Vector2(320, 70), 30, Back);
        }

        void BuildCard(SoulSlotSummary s, Vector2 pos)
        {
            var go = new GameObject($"칸 {s.Slot}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(_root, false);
            var rt = (RectTransform)go.transform;
            Place(rt, pos, new Vector2(480, 560));
            go.GetComponent<Image>().color = Card;
            var b = go.GetComponent<Button>();
            var cb = b.colors; cb.highlightedColor = new Color(1.06f, 1.03f, 0.96f); cb.disabledColor = new Color(0.8f, 0.8f, 0.8f, 0.7f); b.colors = cb;
            b.interactable = _ask.Slot == null;
            b.onClick.AddListener(() => { if (!_leaving && _ask.Slot == null) { _leaving = true; Enter(s); } });

            // 테두리 붉은 띠(위)
            var band = Child("띠", rt).gameObject.AddComponent<Image>();
            band.color = s.IsEmpty ? InkLight : Seal;
            band.rectTransform.anchorMin = new Vector2(0, 1); band.rectTransform.anchorMax = new Vector2(1, 1);
            band.rectTransform.pivot = new Vector2(0.5f, 1); band.rectTransform.sizeDelta = new Vector2(0, 8); band.rectTransform.anchoredPosition = Vector2.zero;

            CardLine(rt, Loc.F("ui.slot.name", s.Slot), 40, FontStyles.Bold, Ink, 220);
            if (s.IsEmpty)
            {
                CardLine(rt, Loc.T("ui.slot.new"), 44, FontStyles.Normal, Ink, 40);
                CardLine(rt, Loc.T("ui.slot.new_sub"), 26, FontStyles.Normal, InkLight, -20);
                return;
            }
            var realm = DataBoot.Data.Balance.Realms.FirstOrDefault(r => r.Id == s.HighestRealm);
            float y = 140;
            CardLine(rt, Loc.F("ui.slot.life", s.LifeNumber), 38, FontStyles.Normal, Ink, y); y -= 70;
            if (realm != null) { CardLine(rt, Loc.F("ui.slot.realm", Loc.Realm(realm)), 26, FontStyles.Normal, Ink, y); y -= 46; }
            CardLine(rt, Loc.F("ui.slot.seal_stones", s.SealStones, s.SealStonesNeeded), 26, FontStyles.Normal, Ink, y); y -= 46;
            CardLine(rt, Loc.F("ui.slot.regions", s.RegionsVisited), 26, FontStyles.Normal, Ink, y); y -= 46;
            CardLine(rt, Loc.F("ui.slot.training", s.TrainingLevels), 26, FontStyles.Normal, Ink, y); y -= 46;
            if (s.LastPlayed is DateTime d)
                CardLine(rt, Loc.F("ui.slot.last_played", Loc.F("ui.slot.date", d.Year, d.Month, d.Day)), 22, FontStyles.Normal, InkLight, y);

            // 귀퉁이 지우기(오른쪽 아래)
            if (_ask.Slot == null)
            {
                var del = Button("지우기", rt, Loc.T("ui.slot.delete"), Vector2.zero, new Vector2(120, 44), 22, () => { _ask.Ask(s.Slot); Rebuild(); });
                del.anchorMin = del.anchorMax = del.pivot = new Vector2(1, 0);
                del.anchoredPosition = new Vector2(-16, 16);
            }
        }

        void BuildAskBar()
        {
            var bar = Child("묻는 띠", _root).gameObject.AddComponent<Image>();
            bar.color = new Color(0.16f, 0.12f, 0.10f, 0.92f);
            Place(bar.rectTransform, new Vector2(0, -420), new Vector2(1500, 110));
            string q = _ask.Step == 1 ? Loc.F("ui.slot.delete_ask", _ask.Slot) : Loc.T("ui.slot.delete_ask_again");
            var t = Text("물음", bar.rectTransform, q, 30, FontStyles.Normal, PaperBg);
            Place(t.rectTransform, new Vector2(-260, 0), new Vector2(900, 90));
            Button("지운다", bar.rectTransform, Loc.T("ui.slot.delete_yes"), new Vector2(370, 0), new Vector2(200, 64), 28, ConfirmDelete, Seal, PaperBg);
            Button("그만둔다", bar.rectTransform, Loc.T("ui.slot.delete_no"), new Vector2(600, 0), new Vector2(200, 64), 28, () => { _ask.Cancel(); Rebuild(); });
        }

        public void ConfirmDelete()
        {
            var slot = _ask.Confirm();
            if (slot is int n)
            {
                SoulStore.DeleteSlot(n);
                Debug.Log($"[칸 고르기] {n}번 칸 지움");
            }
            Rebuild();
        }

        /// <summary>시험·검수용: 칸 지우기를 묻기 시작한다.</summary>
        public void AskDelete(int slot) { _ask.Ask(slot); Rebuild(); }
        public int AskStep => _ask.Step;

        void Update()
        {
            if (_root == null || _leaving || Time.frameCount == _openedFrame) return;
            var kb = Keyboard.current; var pad = Gamepad.current;
            if ((kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame))
            {
                if (_ask.Slot != null) { _ask.Cancel(); Rebuild(); }
                else Back();
            }
        }

        public void Back()
        {
            if (_root == null) return;
            Destroy(_root.gameObject);
            _root = null;
            AnyOpen = false;
            var cb = _onBack; _onBack = null;
            cb?.Invoke();
            Destroy(gameObject);
        }

        void OnDestroy() { if (_root != null) AnyOpen = false; }

        // ---------------------------------------------------------------- 그리기 도구

        void CardLine(RectTransform card, string text, float size, FontStyles style, Color color, float y)
        {
            var t = Text("줄", card, text, size, style, color);
            Place(t.rectTransform, new Vector2(0, y), new Vector2(440, size + 18));
        }

        RectTransform Button(string name, RectTransform parent, string label, Vector2 pos, Vector2 size, float fontSize, UnityEngine.Events.UnityAction onClick,
            Color? bgColor = null, Color? textColor = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = bgColor ?? new Color(0.90f, 0.85f, 0.75f, 1f);
            var b = go.GetComponent<Button>();
            var cb = b.colors; cb.highlightedColor = new Color(1.12f, 1.08f, 1.0f); b.colors = cb;
            b.onClick.AddListener(onClick);
            var rt = (RectTransform)go.transform;
            Place(rt, pos, size);
            var t = Text("글", rt, label, fontSize, FontStyles.Bold, textColor ?? Ink);
            Stretch(t.rectTransform);
            return rt;
        }

        TextMeshProUGUI Text(string name, Transform parent, string text, float size, FontStyles style, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text; t.fontSize = size; t.fontStyle = style; t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            return t;
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
    }
}
