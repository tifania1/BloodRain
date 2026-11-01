using System.Collections.Generic;
using BloodRain.Core.Build;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Presentation.Combat;
using BloodRain.Presentation.Fx;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BloodRain.Presentation.Hud
{
    // 레벨업 화면(C-06 첫 판, 기획서 8장 레벨업 규칙). 레벨이 오르면 시간을 멈추고 비급 책장 카드 3~4장을 편다.
    //   카드 = Core Loadout.RollLevelUp(새 무공·무공 단계·새 심법·심법 단계, 다 새 무공이면 은자·음식 한 장 더).
    //   아래 버튼 셋 = Core LevelUpCharges: 다시 고르기(진소운 특성 +2), 파기(누른 뒤 카드를 고르면 이번 판에서 영영 뺌), 정진(진기 게이지 +20).
    //   고른 무공은 WeaponRunner.Learn 으로 바로 쏜다. 심법 능력치·시너지 진전·은자·음식은 아직 몸에 안 얹는다(C-06 남은 것, S-04).
    //   레벨은 지금 OrbLayer 의 시험용 RunXp 를 본다. Life 가 붙으면(C-10) 그쪽 레벨로 바꾼다.
    //   서장(진소운 제 몸)의 첫 레벨업은 Loadout.RollFirstLevelUp(chapters.json prologue firstLevelUpChoices) — 새 무공 한 장만,
    //   다시 고르기·파기는 숨긴다(C-06b, 기획서 5장 서장 표). 그 뒤는 보통 RollLevelUp.
    //   키보드: 1~4 카드, R 다시 고르기, B 파기, S 정진.
    public sealed class LevelUpScreen : MonoBehaviour
    {
        public TMP_FontAsset font;
        public OrbLayer orbs;
        public WeaponRunner weapons;

        static readonly Color Dim = new Color(0.05f, 0.04f, 0.04f, 0.72f);
        static readonly Color Paper = new Color(0.91f, 0.87f, 0.78f);
        static readonly Color PaperHot = new Color(0.97f, 0.93f, 0.83f);
        static readonly Color Ink = new Color(0.12f, 0.10f, 0.09f);
        static readonly Color Blood = new Color(0.55f, 0.10f, 0.08f);

        GameData _data;
        Loadout _loadout;
        LevelUpCharges _charges;
        UltimateCharge _ultimate;
        readonly HashSet<string> _unlocked = new HashSet<string>();
        readonly System.Random _rng = new System.Random();
        List<Offer> _offers = new List<Offer>();
        int _handledLevel = 1;
        int _pending;
        bool _open, _banishMode, _firstDone, _firstOpen;
        ChapterDef _prologue;
        float _savedTimeScale = 1f;

        RectTransform _root, _cardRow;
        TextMeshProUGUI _title;
        Button _reroll, _banish, _skip;
        TextMeshProUGUI _rerollLabel, _banishLabel, _skipLabel;

        public Loadout Loadout => _loadout;
        public LevelUpCharges Charges => _charges;
        public bool IsOpen => _open;

        void Start()
        {
            _data = DataBoot.Data;
            var body = Meta.RebirthHandoff.Body ?? Rebirth.Prologue(_data);
            var character = body.Character;
            _loadout = new Loadout(_data, character);
            if (body.IsPrologue) _prologue = _data.Chapter("prologue");
            else _firstDone = true;
            // 연무장·몸의 성질 덧셈은 Life 가 붙을 때(C-10) LevelUpCharges.Start 로. 지금은 특성 몫만.
            _charges = new LevelUpCharges(LevelUpCharges.TraitRerolls(_data.Balance.LevelUp, character), 0, 0);
            _ultimate = new UltimateCharge(_data.Balance.UltimateCharge, _data.Balance.Yinyang.Harmony);
            foreach (var w in _data.Weapons) if (w.Unlock == "start") _unlocked.Add(w.Id);
            if (orbs == null) orbs = FindFirstObjectByType<OrbLayer>();
            if (weapons == null) weapons = FindFirstObjectByType<WeaponRunner>();
            if (orbs != null && orbs.TestXp != null) _handledLevel = orbs.TestXp.Level;
            if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Build();
            _root.gameObject.SetActive(false);
        }

        void Update()
        {
            int level = orbs != null && orbs.TestXp != null ? orbs.TestXp.Level : _handledLevel;
            if (level > _handledLevel) { _pending += level - _handledLevel; _handledLevel = level; }
            if (!_open && _pending > 0) Open();
            if (!_open) return;

            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame) Pick(0);
            else if (kb.digit2Key.wasPressedThisFrame) Pick(1);
            else if (kb.digit3Key.wasPressedThisFrame) Pick(2);
            else if (kb.digit4Key.wasPressedThisFrame) Pick(3);
            else if (kb.rKey.wasPressedThisFrame) Reroll();
            else if (kb.bKey.wasPressedThisFrame) ToggleBanish();
            else if (kb.sKey.wasPressedThisFrame) Skip();
        }

        void Open()
        {
            _open = true;
            _pending--;
            _banishMode = false;
            _savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            _firstOpen = !_firstDone && _prologue != null;
            _firstDone = true;
            _offers = _firstOpen ? _loadout.RollFirstLevelUp(_rng, 0f, _unlocked, _prologue) : _loadout.RollLevelUp(_rng, 0f, _unlocked);
            _root.gameObject.SetActive(true);
            Refresh();
        }

        void Close()
        {
            _open = false;
            _root.gameObject.SetActive(false);
            Time.timeScale = _savedTimeScale;
        }

        void Pick(int i)
        {
            if (!_open || i < 0 || i >= _offers.Count) return;
            var o = _offers[i];
            if (_banishMode)
            {
                if (o.Kind == OfferKind.Fallback || !_charges.Can(LevelUpAction.Banish)) return;
                _charges.Banish(_loadout, o.Id);
                _offers.RemoveAt(i);
                if (_offers.Count == 0) _offers.Add(new Offer(OfferKind.Fallback, "silver_or_food"));
                _banishMode = false;
                Refresh();
                return;
            }
            _loadout.Apply(o);
            if ((o.Kind == OfferKind.NewWeapon || o.Kind == OfferKind.UpgradeWeapon) && weapons != null) weapons.Learn(o.Id);
            Close();
        }

        void Reroll()
        {
            if (!_open || _firstOpen || !_charges.Can(LevelUpAction.Reroll)) return;
            _charges.Reroll();
            _offers = _loadout.RollLevelUp(_rng, 0f, _unlocked);
            _banishMode = false;
            Refresh();
        }

        void ToggleBanish()
        {
            if (!_open || _firstOpen || !_charges.Can(LevelUpAction.Banish)) return;
            _banishMode = !_banishMode;
            Refresh();
        }

        void Skip()
        {
            if (!_open || !_charges.Can(LevelUpAction.Skip)) return;
            _charges.Skip(_ultimate);
            Close();
        }

        // ── 그리기 ─────────────────────────────────────────────

        void Build()
        {
            var go = new GameObject("레벨업", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)go.transform;

            var dim = MakeImage(_root, "어둠", Dim);
            Stretch(dim.rectTransform);

            _title = MakeText(_root, "제목", 54, Paper, TextAlignmentOptions.Center);
            Place(_title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 330), new Vector2(1200, 80));

            _cardRow = new GameObject("카드", typeof(RectTransform)).GetComponent<RectTransform>();
            _cardRow.SetParent(_root, false);
            Place(_cardRow, new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(1600, 480));
            var h = _cardRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 36; h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = h.childControlHeight = false;
            h.childForceExpandWidth = h.childForceExpandHeight = false;

            _reroll = MakeButton(_root, "다시 고르기", new Vector2(-360, -360), out _rerollLabel, Reroll);
            _banish = MakeButton(_root, "파기", new Vector2(0, -360), out _banishLabel, ToggleBanish);
            _skip = MakeButton(_root, "정진", new Vector2(360, -360), out _skipLabel, Skip);
        }

        void Refresh()
        {
            _title.text = _banishMode ? "뺄 비급을 고르라" : $"깨달음이 온다 · {_handledLevel - _pending} 단계";
            for (int i = _cardRow.childCount - 1; i >= 0; i--) Destroy(_cardRow.GetChild(i).gameObject);
            for (int i = 0; i < _offers.Count; i++) MakeCard(i, _offers[i]);

            _rerollLabel.text = $"다시 고르기 ({_charges.Rerolls})  R";
            _banishLabel.text = _banishMode ? $"파기 취소 ({_charges.Banishes})  B" : $"파기 ({_charges.Banishes})  B";
            _skipLabel.text = $"정진 ({_charges.Skips})  S";
            _reroll.interactable = _charges.Can(LevelUpAction.Reroll);
            _banish.interactable = _charges.Can(LevelUpAction.Banish);
            _skip.interactable = _charges.Can(LevelUpAction.Skip);
            // 서장 첫 레벨업은 카드 한 장(새 무공)뿐이라 다시 고르기·파기를 숨긴다. 정진은 남긴다.
            _reroll.gameObject.SetActive(!_firstOpen);
            _banish.gameObject.SetActive(!_firstOpen);
        }

        void MakeCard(int index, Offer o)
        {
            var (kind, name, level, note) = Describe(o);
            var card = MakeImage(_cardRow, $"카드 {index + 1}", Paper);
            card.rectTransform.sizeDelta = new Vector2(330, 460);
            card.raycastTarget = true;
            var btn = card.gameObject.AddComponent<Button>();
            var cols = btn.colors;
            cols.normalColor = Color.white; cols.highlightedColor = new Color(PaperHot.r / Paper.r, PaperHot.g / Paper.g, PaperHot.b / Paper.b); cols.pressedColor = new Color(0.85f, 0.8f, 0.7f);
            btn.colors = cols;
            int i = index;
            btn.onClick.AddListener(() => Pick(i));

            var spine = MakeImage(card.rectTransform, "책등", _banishMode && o.Kind != OfferKind.Fallback ? Blood : Ink);
            Place(spine.rectTransform, new Vector2(0f, 0.5f), new Vector2(9, 0), new Vector2(18, 460));

            var k = MakeText(card.rectTransform, "갈래", 26, Blood, TextAlignmentOptions.Center);
            Place(k.rectTransform, new Vector2(0.5f, 1f), new Vector2(8, -50), new Vector2(290, 40));
            k.text = kind;
            var n = MakeText(card.rectTransform, "이름", 44, Ink, TextAlignmentOptions.Center);
            Place(n.rectTransform, new Vector2(0.5f, 1f), new Vector2(8, -130), new Vector2(290, 110));
            n.textWrappingMode = TextWrappingModes.Normal;
            n.text = name;
            var l = MakeText(card.rectTransform, "단계", 32, Ink, TextAlignmentOptions.Center);
            Place(l.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(8, -20), new Vector2(290, 50));
            l.text = level;
            var d = MakeText(card.rectTransform, "설명", 24, new Color(0.3f, 0.26f, 0.22f), TextAlignmentOptions.Center);
            Place(d.rectTransform, new Vector2(0.5f, 0f), new Vector2(8, 110), new Vector2(290, 150));
            d.textWrappingMode = TextWrappingModes.Normal;
            d.text = note;
            var key = MakeText(card.rectTransform, "키", 26, new Color(0.45f, 0.4f, 0.35f), TextAlignmentOptions.Center);
            Place(key.rectTransform, new Vector2(0.5f, 0f), new Vector2(8, 26), new Vector2(290, 36));
            key.text = (index + 1).ToString();
        }

        (string kind, string name, string level, string note) Describe(Offer o)
        {
            switch (o.Kind)
            {
                case OfferKind.NewWeapon:
                {
                    var w = _data.Weapon(o.Id);
                    return ("새 무공", w.NameKo, "1 단계", $"{Aspect(w.Yinyang)} · 짝 심법 {PassiveName(w.PairPassive)}");
                }
                case OfferKind.UpgradeWeapon:
                {
                    var w = _data.Weapon(o.Id);
                    int lv = _loadout.Weapons[o.Id];
                    return ("무공 정진", w.NameKo, $"{lv} → {lv + 1} 단계", Milestone(w, lv + 1));
                }
                case OfferKind.NewPassive:
                {
                    var p = _data.Passive(o.Id);
                    return ("새 심법", p.NameKo, "1 단계", Aspect(p.Yinyang));
                }
                case OfferKind.UpgradePassive:
                {
                    var p = _data.Passive(o.Id);
                    int lv = _loadout.Passives[o.Id];
                    return ("심법 정진", p.NameKo, $"{lv} → {lv + 1} 단계", Aspect(p.Yinyang));
                }
                default:
                    return ("행낭", "은자 · 음식", "", "고를 비급이 없다");
            }
        }

        static string Milestone(WeaponDef w, int level)
        {
            if (w.Milestones != null && w.Milestones.TryGetValue(level.ToString(), out var m)) return $"새 동작: {m}";
            return "위력이 오른다";
        }

        string PassiveName(string id)
        {
            if (string.IsNullOrEmpty(id)) return "없음";
            foreach (var p in _data.Passives) if (p.Id == id) return p.NameKo;
            return id;
        }

        static string Aspect(string yy) => yy == "yang" ? "양(陽)" : yy == "yin" ? "음(陰)" : "중(中)";

        Image MakeImage(Transform parent, string name, Color c)
        {
            var img = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(parent, false);
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        TextMeshProUGUI MakeText(Transform parent, string name, float size, Color c, TextAlignmentOptions align)
        {
            var t = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            t.transform.SetParent(parent, false);
            if (font != null) t.font = font;
            t.fontSize = size;
            t.color = c;
            t.alignment = align;
            t.raycastTarget = false;
            return t;
        }

        Button MakeButton(Transform parent, string name, Vector2 pos, out TextMeshProUGUI label, UnityEngine.Events.UnityAction onClick)
        {
            var img = MakeImage(parent, name, Paper);
            img.raycastTarget = true;
            Place(img.rectTransform, new Vector2(0.5f, 0.5f), pos, new Vector2(320, 72));
            var b = img.gameObject.AddComponent<Button>();
            b.onClick.AddListener(onClick);
            label = MakeText(img.rectTransform, "글", 30, Ink, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            return b;
        }

        static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform r, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }
    }
}
