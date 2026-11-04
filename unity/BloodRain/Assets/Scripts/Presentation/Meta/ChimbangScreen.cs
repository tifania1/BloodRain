using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    // 침방 화면 1, 후보 카드(TU-11, 기획서 21장 '환혼: 다음 몸 고르기'). 웹 시안 openRebirth() 와 같은 내용을 카드에 쓴다.
    //   카드: 문파 산문 그림(없으면 단색), "OO 제자", 태어나는 땅과 혈운 농도(2 이상 주홍 굵게), 시작 무공·기혈·걸음, 대표 인물, 타고난 성질.
    //   다시 고르기 버튼(남은 횟수). 고르면 Core Rebirth.Body 로 몸을 만들어 RebirthHandoff 에 두고 Chosen 을 부른다.
    // 후보 추첨·다시 고르기·몸 만들기는 모두 Core RebirthDraw 가 한다. 화면은 그리기와 입력만.
    // 카드 UI 는 후보 수(3·4)에 맞춰 실행 때 짓는다. 1~4 키로 고르고 R 로 다시 고르기.
    public sealed class ChimbangScreen : MonoBehaviour
    {
        [Serializable]
        public struct SectArt { public string sectId; public Sprite art; }

        public TMP_FontAsset font;
        [Tooltip("art_queue bg_chimbang. 없으면 한지 빛 단색")]
        public Sprite background;
        [Tooltip("art_queue card_sect_<문파 id>. 없는 문파는 단색 카드")]
        public List<SectArt> sectArt = new List<SectArt>();
        [Tooltip("벽에 거는 중원 지도(art_queue map_central_plains). 없으면 빈 종이")]
        public Texture wallMap;
        [Tooltip("시험용: 해금 문파 대신 모든 문파에서 뽑는다")]
        public bool allSects;
        [Tooltip("시험용: 후보 수에 더한다(넷째 몸을 산 것처럼 1)")]
        public int extraCandidates;
        [Tooltip("시험용: 다시 고르기 횟수에 더한다")]
        public int extraRerolls;
        [Tooltip("시험용: 첫 환혼(서장 직후 세 문파 고정)으로 연다")]
        public bool firstRebirth;
        public int seed = 7;
        [Tooltip("시작하면 새 혼으로 바로 연다(시험 씬). 객잔 씬은 끄고 InnFlow 가 저장된 혼으로 연다")]
        public bool openOnStart = true;
        [Tooltip("고르면 Run 씬을 연다(한 생 시작). 시험 씬에선 끔")]
        public bool loadRunScene;

        public event Action<RebirthBody> Chosen;
        public RebirthDraw Draw { get; private set; }

        static readonly Color Paper = new Color(0.91f, 0.87f, 0.78f);
        static readonly Color Ink = new Color(0.12f, 0.10f, 0.09f);
        const string CrimsonHex = "#B3261E";
        const string MutedHex = "#8A7F70";

        static readonly HashSet<string> Flat = new HashSet<string> { "Recovery", "Luck", "DashCharges", "Armor" };

        GameData _data;
        RectTransform _cards;
        TextMeshProUGUI _sub, _rerollLabel;
        Button _reroll;
        ChimbangMap _map;
        bool _done;

        void Start()
        {
            _data = DataBoot.Data;
            EnsureEventSystem();
            BuildFrame();
            if (openOnStart) Open(new Soul(_data), new System.Random(seed));
        }

        /// <summary>그 혼으로 침방을 연다. 첫 환혼이면 세 문파 고정, 아니면 해금 문파에서 후보 수만큼.</summary>
        public void Open(Soul soul, System.Random rng)
        {
            var c = _data.Cheonha;
            var sects = allSects ? c.Sects.Select(s => s.Id).ToList() : soul.UnlockedSects.ToList();
            bool first = firstRebirth || soul.IsFirstRebirth;
            Draw = new RebirthDraw(c, sects, soul.CandidateCount + extraCandidates, soul.RerollsPerRebirth + extraRerolls, first, rng);
            _sub.text = first
                ? Loc.F("ui.chimbang.first", string.Join("·", c.Rebirth.FirstRebirth.Select(id => Loc.Sect(c.Sect(id)))))
                : Loc.F("ui.chimbang.nth", soul.Lives + 1, CountWord(Draw.Count));
            _done = false;
            _soul = soul;
            _map.SetSoul(soul);
            BuildUpgrades(soul);
            Refresh();
        }

        Soul _soul;
        readonly List<MetaShopRow> _upgradeRows = new List<MetaShopRow>();
        RectTransform _upgrades;

        /// <summary>지도 아래 환혼 강화 두 줄(TU-14, 연무장과 같은 칸). 산 효과는 다음 환혼부터.</summary>
        void BuildUpgrades(Soul soul)
        {
            if (_upgrades != null) Destroy(_upgrades.gameObject);
            _upgradeRows.Clear();
            _upgrades = new GameObject("환혼 강화", typeof(RectTransform)).GetComponent<RectTransform>();
            _upgrades.SetParent(_map.Frame.parent, false);
            _upgrades.anchorMin = _upgrades.anchorMax = new Vector2(1f, 0f);
            _upgrades.anchoredPosition = new Vector2(-360, 215);
            _upgrades.sizeDelta = new Vector2(640, 120);
            var head = Text("환혼 강화 제목", _upgrades, UpgradeHead(soul), 22, FontStyles.Bold);
            Place(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, 0), new Vector2(640, 32));
            float y = -40;
            foreach (var up in _data.Cheonha.Rebirth.Upgrades)
            {
                var row = MetaShopRow.Upgrade(_upgrades, font, soul, up, () => { foreach (var r in _upgradeRows) r.Refresh(); head.text = UpgradeHead(soul); }, 640);
                row.Rect.anchorMin = row.Rect.anchorMax = new Vector2(0.5f, 1f);
                row.Rect.anchoredPosition = new Vector2(0, y);
                _upgradeRows.Add(row);
                y -= 44;
            }
        }

        static string UpgradeHead(Soul soul) =>
            $"{Loc.T("ui.chimbang.upgrades")} <size=75%><alpha=#88>{Loc.F("ui.qi.balance", soul.RebirthQi.ToString("N0"))}<alpha=#FF></size>";

        /// <summary>셋·넷(웹 시안 "제자 셋을"). 한국어는 받침 있는 말만 와서 조사는 '을'로 둔다. 표에 없는 수는 숫자로.</summary>
        static string CountWord(int n) => n >= 3 && n <= 5 ? Loc.T($"ui.count.{n}") : n.ToString();

        public void Reroll()
        {
            if (Draw == null || _done || Draw.RerollsLeft <= 0) return;
            Draw.Reroll();
            Refresh();
        }

        public void Choose(int index)
        {
            if (Draw == null || _done || index < 0 || index >= Draw.Candidates.Count) return;
            _done = true;
            var body = Draw.Choose(index);
            RebirthHandoff.Body = body;
            RebirthHandoff.Soul = _soul;
            Debug.Log($"[침방] {body.Sect?.NameKo} 제자로 환생 · {body.Region.NameKo}(혈운 {body.Region.Density:0.0}) · 성질 {string.Join(",", body.Traits.Select(t => t.NameKo))}");
            Chosen?.Invoke(body);
            if (loadRunScene) SceneManager.LoadScene("Run");
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || Draw == null) return;
            Key[] keys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4 };
            for (int i = 0; i < keys.Length; i++)
                if (kb[keys[i]].wasPressedThisFrame) Choose(i);
            if (kb.rKey.wasPressedThisFrame) Reroll();
        }

        // ---- 그리기 ----

        void Refresh()
        {
            for (int i = _cards.childCount - 1; i >= 0; i--) Destroy(_cards.GetChild(i).gameObject);
            for (int i = 0; i < Draw.Candidates.Count; i++) BuildCard(Draw.Candidates[i], i);
            _reroll.gameObject.SetActive(!Draw.IsFirst);
            _reroll.interactable = Draw.RerollsLeft > 0;
            _rerollLabel.text = $"{Loc.T("ui.chimbang.reroll")} <size=80%>{Loc.F("ui.chimbang.reroll_left", Draw.RerollsLeft)}</size>  <size=70%><alpha=#88>R<alpha=#FF></size>";
        }

        void BuildFrame()
        {
            var canvasGo = new GameObject("침방", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)canvasGo.transform;

            var bg = Rect("배경", root, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
            bg.sprite = background;
            bg.color = background != null ? Color.white : Paper;
            bg.preserveAspect = false;
            bg.raycastTarget = false;

            var title = Text("제목", root, Loc.T("ui.chimbang.title"), 46, FontStyles.Bold);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -70), new Vector2(1200, 70));
            _sub = Text("부제", root, "", 26, FontStyles.Normal);
            Place(_sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -125), new Vector2(1400, 40));
            // 목 노인 자리(그림이 오면 배경에 들어 있다). 지금은 이름만
            var elder = Text("목 노인", root, $"<alpha=#88>{Loc.T("ui.chimbang.elder")}<alpha=#FF>", 22, FontStyles.Italic);
            Place(elder.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -165), new Vector2(600, 32));

            _cards = Rect("후보 카드", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            // 왼쪽은 후보 카드, 오른쪽 벽에는 중원 지도(TU-12)
            _cards.sizeDelta = new Vector2(1200, 640);
            _cards.anchoredPosition = new Vector2(-330, -30);
            _map = gameObject.AddComponent<ChimbangMap>();
            _map.Build(root, font, wallMap, _data, new Vector2(640, 416));
            var frame = _map.Frame;
            frame.anchorMin = frame.anchorMax = new Vector2(1f, 0.5f);
            frame.anchoredPosition = new Vector2(-360, -30);
            var h = _cards.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 24; h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = h.childControlHeight = false;
            h.childForceExpandWidth = h.childForceExpandHeight = false;

            _reroll = MakeButton("다시 고르기", root, new Color(0.25f, 0.20f, 0.17f), out _rerollLabel, 26, Paper);
            Place((RectTransform)_reroll.transform, new Vector2(0.5f, 0f), new Vector2(0, 80), new Vector2(420, 64));
            _reroll.onClick.AddListener(Reroll);
        }

        void BuildCard(RebirthCandidate cand, int index)
        {
            var sect = cand.Sect;
            var btn = MakeButton($"{index + 1} {sect.NameKo}", _cards, SectColor(sect.Id), out var label, 1, Ink);
            Destroy(label.gameObject);
            var rt = (RectTransform)btn.transform;
            rt.sizeDelta = new Vector2(280, 600);
            int i = index;
            btn.onClick.AddListener(() => Choose(i));
            // 커서가 가면 벽 지도에 그 문파 자리와 장막 원(TU-12)
            var trig = btn.gameObject.AddComponent<EventTrigger>();
            AddTrigger(trig, EventTriggerType.PointerEnter, () => _map.Show(cand));
            AddTrigger(trig, EventTriggerType.PointerExit, () => _map.Show(null));

            // 산문 그림(위 절반). 없으면 단색 카드 그대로
            var art = sectArt.FirstOrDefault(a => a.sectId == sect.Id).art;
            if (art != null)
            {
                var img = Rect("산문", rt, new Vector2(0, 0.5f), Vector2.one).gameObject.AddComponent<Image>();
                img.sprite = art; img.preserveAspect = false; img.raycastTarget = false;
            }
            var paper = Rect("글 바탕", rt, Vector2.zero, new Vector2(1, art != null ? 0.5f : 0.62f)).gameObject.AddComponent<Image>();
            paper.color = new Color(Paper.r, Paper.g, Paper.b, 0.94f);
            paper.raycastTarget = false;

            var name = Text("이름", rt, $"{Loc.Disciple(sect)}{(cand.Female ? $" <size=70%>{Loc.T("ui.chimbang.female")}</size>" : "")}", 40, FontStyles.Bold);
            // 번호(오른쪽 위 40px)와 겹치지 않게 이름 칸은 번호 아래로, 양옆 20px 여백(L-01b3 영어 'Mount Hua Disciple')
            Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -78), new Vector2(240, 56));
            name.enableAutoSizing = true; name.fontSizeMin = 22; name.fontSizeMax = 38;
            var key = Text("번호", rt, $"<alpha=#88>{index + 1}<alpha=#FF>", 24, FontStyles.Normal);
            Place(key.rectTransform, new Vector2(1f, 1f), new Vector2(-26, -26), new Vector2(40, 40));

            var body = Text("내용", rt, CardBody(cand), 19, FontStyles.Normal);
            body.alignment = TextAlignmentOptions.TopLeft;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.lineSpacing = 6;
            var brt = body.rectTransform;
            brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(1, art != null ? 0.5f : 0.62f);
            brt.offsetMin = new Vector2(18, 16); brt.offsetMax = new Vector2(-18, -14);
        }

        string CardBody(RebirthCandidate cand)
        {
            var s = cand.Sect;
            var r = cand.Region;
            var sb = new StringBuilder();
            string dens = Loc.F("ui.density", r.Density.ToString("0.0"));
            if (r.Density >= 2f) dens = $"<color={CrimsonHex}><b>{dens}</b></color>";
            sb.Append(Loc.F("ui.chimbang.born_in", Loc.Region(r))).Append(" · ").Append(dens).Append('\n');
            var wd = _data.Weapons.FirstOrDefault(w => w.Id == s.StartWeapon);
            string weapon = wd != null ? Loc.Weapon(wd) : s.StartWeapon;
            sb.Append(Loc.F("ui.chimbang.body", weapon, s.MaxHealth.ToString("0"), Mathf.RoundToInt(s.MoveSpeed * 100))).Append('\n');
            var lc = s.Leader != null ? _data.Characters.FirstOrDefault(c => c.Id == s.Leader) : null;
            string leader = lc != null ? Loc.Character(lc) : s.Leader;
            sb.Append(leader != null ? Loc.F("ui.chimbang.leader", leader) + "\n" : $"<color={MutedHex}>{Loc.T("ui.chimbang.no_leader")}</color>\n");
            sb.Append('\n');
            if (cand.Traits.Count == 0) sb.Append($"<color={MutedHex}>{Loc.T("ui.chimbang.no_trait")}</color>");
            foreach (var t in cand.Traits) sb.Append(TraitLine(t)).Append('\n');
            return sb.ToString().TrimEnd('\n');
        }

        /// <summary>"천생신력 天生神力  공력 +15% · 쾌속 −5%". 웹 시안 traitLine 과 같은 표기.</summary>
        public static string TraitLine(BirthTraitDef t)
        {
            var parts = t.Stats.Select(kv => FormatStat(kv.Key, kv.Value)).ToList();
            if (t.YinyangStart != 0) parts.Add(Loc.F("ui.trait.yinyang", $"{(t.YinyangStart > 0 ? "+" : "−")}{Math.Abs(t.YinyangStart)}"));
            if (t.RebirthQi != 0) parts.Add(Loc.F("ui.trait.rebirth_qi", Mathf.RoundToInt(t.RebirthQi * 100)));
            return $"<b>{Loc.Trait(t)}</b> <size=75%><alpha=#99>{Loc.Hanja(t.Hanja)}<alpha=#FF></size>\n<size=90%>{string.Join(" · ", parts)}</size>";
        }

        public static string FormatStat(string key, float v)
        {
            string sign = v > 0 ? "+" : v < 0 ? "−" : "";
            float a = Math.Abs(v);
            string ko = Loc.Stat(key);
            if (key == "Recovery") return $"{ko} {Loc.F("ui.per_second", $"{sign}{a:0.##}")}";
            if (Flat.Contains(key)) return $"{ko} {sign}{a:0.##}";
            return $"{ko} {sign}{Mathf.RoundToInt(a * 100)}%";
        }

        /// <summary>문파마다 다른 차분한 단색(산문 그림이 오기 전 카드 바탕).</summary>
        static Color SectColor(string id)
        {
            uint h = 2166136261;
            foreach (char ch in id) h = (h ^ ch) * 16777619;
            float hue = (h % 360) / 360f;
            return Color.HSVToRGB(hue, 0.28f, 0.52f);
        }

        // ---- UI 조각 ----

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

        TextMeshProUGUI Text(string name, Transform parent, string text, float size, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text; t.fontSize = size; t.fontStyle = style; t.color = Ink;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }

        Button MakeButton(string name, Transform parent, Color color, out TextMeshProUGUI label, float size, Color textColor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            var b = go.GetComponent<Button>();
            var cb = b.colors;
            cb.highlightedColor = new Color(1.12f, 1.08f, 1.0f); cb.pressedColor = new Color(0.85f, 0.82f, 0.78f);
            cb.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
            b.colors = cb;
            label = Text("글", go.transform, "", size, FontStyles.Bold);
            label.color = textColor;
            var lrt = label.rectTransform; lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            return b;
        }

        static void AddTrigger(EventTrigger t, EventTriggerType type, System.Action a)
        {
            var e = new EventTrigger.Entry { eventID = type };
            e.callback.AddListener(_ => a());
            t.triggers.Add(e);
        }

        /// <summary>그 후보에 커서를 둔 것처럼 지도를 보인다(시험·패드용). -1 이면 지운다.</summary>
        public void Hover(int index) => _map.Show(Draw != null && index >= 0 && index < Draw.Candidates.Count ? Draw.Candidates[index] : null);

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
    }

    /// <summary>침방에서 고른 몸을 Run 씬으로 넘기는 자리. Run 씬이 읽고 비운다.</summary>
    public static class RebirthHandoff
    {
        public static RebirthBody Body;
        /// <summary>그 몸을 고른 혼. 레벨업 카드가 복원한 비급(Soul.RestoredManuals)을 읽는다. 없으면(Run 씬을 바로 켬) 새 혼으로 친다.</summary>
        public static Soul Soul;
        /// <summary>서장에서 쓰러져 처음부터(Life.RestartPrologue) 다시 만든 생. Run 씬을 다시 열면 LifeRunner 가 이것을 쓴다.</summary>
        public static BloodRain.Core.Run.Life PendingLife;

        /// <summary>생이 끝났다. 다음 Run 은 침방에서 고른 몸으로(남은 몸·생을 쓰지 않게).</summary>
        public static void ClearBody()
        {
            Body = null;
            PendingLife = null;
        }
    }
}
