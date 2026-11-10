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
    //   고른 무공은 WeaponRunner.Learn 으로 바로 쏜다. 행낭(은자·음식) 카드는 은자를 Silver 에 더하고 PlayerVitals.Heal.
    //   심법 능력치·시너지 진전 능력치는 아직 몸에 안 얹는다(C-06 남은 것, Life 가 붙을 때). 카드의 연환 띠·문파 인장과 HUD 진전 인장은 S-04.
    //   레벨·Loadout·Charges 는 Run 씬의 한 생(LifeRunner.Life)을 본다. 한 생이 없는 시험 씬이면 OrbLayer 의 시험용 RunXp.
    //   서장(진소운 제 몸)의 첫 레벨업은 Loadout.RollFirstLevelUp(chapters.json prologue firstLevelUpChoices) — 새 무공 한 장만,
    //   다시 고르기·파기는 숨긴다(C-06b, 기획서 5장 서장 표). 그 뒤는 보통 RollLevelUp.
    //   키보드: 1~4 카드, R 다시 고르기, B 파기, S 정진.
    public sealed class LevelUpScreen : MonoBehaviour
    {
        public TMP_FontAsset font;
        public OrbLayer orbs;
        public WeaponRunner weapons;
        public PlayerVitals vitals;
        /// <summary>행낭 카드로 받은 은자(한 생이 없는 시험 씬 자리). 한 생이 있으면 Life.GainSilver 로 간다(C-06a).</summary>
        public int Silver { get; private set; }

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
        // 복원한 비급(S-04, Soul.RestoredManuals). Synergy.Compute·Preview 에 늘 넘긴다 — null 을 넘기면 비급까지 다 켜진다.
        IReadOnlyCollection<string> _restored = new List<string>();
        RectTransform _sealRow;
        string _sealKey = "";

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
            // LF-01: Run 씬에 한 생이 있으면 그 생의 Loadout·Charges(연무장·몸의 성질 덧셈까지)를 쓴다.
            var life = LifeRunner.Instance != null ? LifeRunner.Instance.Life : null;
            if (life != null) body = life.Body;
            _loadout = life != null ? life.Loadout : new Loadout(_data, character);
            if (body.IsPrologue) _prologue = _data.Chapter("prologue");
            else _firstDone = true;
            // 연무장·몸의 성질 덧셈은 Life 가 붙을 때(C-10) LevelUpCharges.Start 로. 지금은 특성 몫만.
            _charges = life != null ? life.Charges : new LevelUpCharges(LevelUpCharges.TraitRerolls(_data.Balance.LevelUp, character), 0, 0);
            // C-09a 정진은 한 생의 오의 게이지를 채운다
            _ultimate = life != null ? life.Charge : new UltimateCharge(_data.Balance.UltimateCharge, _data.Balance.Yinyang.Harmony);
            foreach (var w in _data.Weapons) if (w.Unlock == "start") _unlocked.Add(w.Id);
            if (orbs == null) orbs = FindFirstObjectByType<OrbLayer>();
            if (weapons == null) weapons = FindFirstObjectByType<WeaponRunner>();
            _handledLevel = CurrentLevel();
            if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            if (Meta.RebirthHandoff.Soul != null) _restored = Meta.RebirthHandoff.Soul.RestoredManuals;
            Build();
            BuildSeals();
            _root.gameObject.SetActive(false);
            RefreshSeals();
        }

        // 한 생이 있으면 life.Level, 없으면(시험 씬) OrbLayer 의 시험 RunXp, 둘 다 없으면 그대로.
        int CurrentLevel()
        {
            if (LifeRunner.Instance != null) return LifeRunner.Instance.Life.Level;
            if (orbs != null && orbs.TestXp != null) return orbs.TestXp.Level;
            return _handledLevel;
        }

        /// <summary>지금 로드아웃의 연환·진전(복원한 비급만 켜짐).</summary>
        public SynergyState Synergy => BloodRain.Core.Build.Synergy.Compute(_data, _loadout, null, _restored);

        void Update()
        {
            int level = CurrentLevel();
            if (level > _handledLevel) { _pending += level - _handledLevel; _handledLevel = level; }
            // 돌파 줌이 도는 동안은 열지 않는다(줌이 끝나면 카드). 줌이 끝나며 세상 시간을 1로 되돌려도 열린 동안은 멈춰 둔다.
            bool zooming = LifeRunner.Instance != null && LifeRunner.Instance.breakthrough != null && LifeRunner.Instance.breakthrough.IsPlaying;
            // C-07b 깨달음 화면이 먼저(열려 있거나 열릴 차례면 기다린다)
            bool insight = LifeRunner.Instance != null && LifeRunner.Instance.InsightBusy;
            // 연 프레임엔 키를 읽지 않는다(깨달음 카드를 고른 같은 숫자 키가 레벨업 카드까지 고르지 않게)
            if (!_open && _pending > 0 && !zooming && !insight) { Open(); return; }
            if (!_open) return;
            if (Time.timeScale != 0f && !zooming) Time.timeScale = 0f;

            var kb = Keyboard.current;
            if (kb == null) return;
            if (Guarded) return;   // UI-01U 열린 직후 입력 막기
            if (kb.digit1Key.wasPressedThisFrame) Pick(0);
            else if (kb.digit2Key.wasPressedThisFrame) Pick(1);
            else if (kb.digit3Key.wasPressedThisFrame) Pick(2);
            else if (kb.digit4Key.wasPressedThisFrame) Pick(3);
            // 단축키는 조작 재설정(WP-69U, 기본은 웹과 같은 R·X·Z)
            else if (ControlInput.Pressed(BloodRain.Core.Meta.ControlAction.Reroll)) Reroll();
            else if (ControlInput.Pressed(BloodRain.Core.Meta.ControlAction.Banish)) ToggleBanish();
            else if (ControlInput.Pressed(BloodRain.Core.Meta.ControlAction.Skip)) Skip();
        }

        void Open()
        {
            _open = true;
            SfxPlayer.Instance?.Play("sfx_levelup"); // SFX-14U
            _pending--;
            _banishMode = false;
            _savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            _firstOpen = !_firstDone && _prologue != null;
            _firstDone = true;
            _offers = _firstOpen ? _loadout.RollFirstLevelUp(_rng, Luck(), _unlocked, _prologue) : _loadout.RollLevelUp(_rng, Luck(), _unlocked);
            _root.gameObject.SetActive(true);
            _openedAt = Time.unscaledTime;
            Refresh();
        }

        // ── UI-01U 열린 직후 입력 막기 · 옆 칸(기획서 12장 '레벨업 카드 규칙') ─────────────
        // 세상 시간이 멈춰 있으니 실제 시간으로 balance levelUp.inputGuardSeconds 동안 카드·단추를 안 받는다(달리며 누르던 키·클릭이 카드를 고르지 않게).
        float _openedAt = -999f;
        bool Guarded => Time.unscaledTime - _openedAt < _data.Balance.LevelUp.InputGuardSeconds;
        RectTransform _slotPanel;
        readonly List<(bool weapon, string id, Image img)> _slotCells = new List<(bool, string, Image)>();
        static readonly Color CellEmpty = new Color(0.20f, 0.17f, 0.15f, 0.85f);
        static readonly Color CellFull = new Color(0.32f, 0.27f, 0.22f, 0.95f);
        static readonly Color CellLit = new Color(0.79f, 0.64f, 0.16f, 1f);

        /// <summary>카드 줄 오른쪽에 무공 6칸·심법 6칸(HudSlots.Build, TopHud 와 같은 셈). 고르려는 카드가 켜는 칸은 LightSlots 가 금빛으로.</summary>
        void BuildSlotPanel()
        {
            if (_slotPanel != null) Destroy(_slotPanel.gameObject);
            _slotCells.Clear();
            _slotPanel = new GameObject("옆 칸", typeof(RectTransform)).GetComponent<RectTransform>();
            _slotPanel.SetParent(_root, false);
            // 카드 줄 바로 오른쪽(카드 넷이어도 16:10 1280x800 에서 안 잘리게 카드 수에 맞춰 붙인다)
            int n = Mathf.Max(1, _offers.Count);
            float half = (n * 330 + (n - 1) * 36) * 0.5f;
            Place(_slotPanel, new Vector2(0.5f, 0.5f), new Vector2(half + 95, 10), new Vector2(150, 420));
            int lvl = LifeRunner.Instance != null && LifeRunner.Instance.Life != null ? LifeRunner.Instance.Life.Level : _handledLevel;
            var view = HudSlots.Build(_loadout, lvl);
            Column(view.Weapons, true, -38, Loc.T("ui.levelup.slots_weapon"));
            Column(view.Passives, false, 38, Loc.T("ui.levelup.slots_passive"));
        }

        void Column(List<HudSlot> slots, bool weapon, float x, string head)
        {
            var h = MakeText(_slotPanel, "머리", 18, Paper, TextAlignmentOptions.Center);
            Place(h.rectTransform, new Vector2(0.5f, 1f), new Vector2(x, -12), new Vector2(74, 26));
            h.text = head;
            h.enableAutoSizing = true; h.fontSizeMin = 9; h.fontSizeMax = 18;
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                var cell = MakeImage(_slotPanel, $"{(weapon ? "무공" : "심법")} {i + 1}", s.Id != null ? CellFull : CellEmpty);
                Place(cell.rectTransform, new Vector2(0.5f, 1f), new Vector2(x, -58 - i * 62), new Vector2(66, 56));
                var t = MakeText(cell.rectTransform, "글", 15, Paper, TextAlignmentOptions.Center);
                Stretch(t.rectTransform);
                t.enableAutoSizing = true; t.fontSizeMin = 10; t.fontSizeMax = 16;
                t.margin = new Vector4(2, 2, 2, 2);
                if (s.Id != null)
                {
                    string name = weapon ? WeaponName(_data.Weapon(s.Id)) : SlotPassiveName(s.Id);
                    t.text = $"{Short(name)}\n<size=80%>Lv{s.Level}</size>";
                }
                _slotCells.Add((weapon, s.Id, cell));
            }
        }

        string SlotPassiveName(string id)
        {
            foreach (var p in _data.Passives) if (p.Id == id) return Loc.T($"passives.{p.Id}.name", p.NameKo);
            return id;
        }

        /// <summary>그 카드가 켜는 칸을 빛낸다(null 이면 끔). Core LevelUpView.CardLights — Id null 은 첫 빈 칸.</summary>
        void LightSlots(Offer? o)
        {
            var lights = o.HasValue && o.Value.Kind != OfferKind.Fallback ? LevelUpView.CardLights(_data, _loadout, o.Value) : new List<SlotLight>();
            var usedEmpty = new HashSet<bool>();
            foreach (var c in _slotCells) c.img.color = c.id != null ? CellFull : CellEmpty;
            foreach (var l in lights)
                for (int i = 0; i < _slotCells.Count; i++)
                {
                    var c = _slotCells[i];
                    if (c.weapon != l.Weapon) continue;
                    if (l.Id != null ? c.id == l.Id : (c.id == null && !usedEmpty.Contains(l.Weapon)))
                    {
                        if (l.Id == null) usedEmpty.Add(l.Weapon);
                        c.img.color = CellLit;
                        break;
                    }
                }
            LitCount = 0;
            foreach (var c in _slotCells) if (c.img.color == CellLit) LitCount++;
        }

        /// <summary>시험용: 지금 빛나는 칸 수.</summary>
        public int LitCount { get; private set; }
        /// <summary>시험용: 그 카드에 커서를 둔 것처럼.</summary>
        public void Hover(int index) => LightSlots(index >= 0 && index < _offers.Count ? _offers[index] : (Offer?)null);

        // LU-01U 기연(연무장·도문심법·타고난 성질)이 넷째 카드 확률을 정한다(balance levelUp.luckForFourthChoice). 시험 씬엔 0.
        static float Luck()
        {
            var st = LifeRunner.Instance != null ? LifeRunner.Instance.Stats : null;
            return st != null ? st.Luck : 0f;
        }

        void Close()
        {
            _open = false;
            _root.gameObject.SetActive(false);
            Time.timeScale = _savedTimeScale;
        }

        void Pick(int i)
        {
            if (!_open || i < 0 || i >= _offers.Count || Guarded) return;
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
            if (o.Kind == OfferKind.Fallback)
            {
                // 행낭 카드: 은자와 기혈을 함께(balance.json levelUp.fallbackCard, 회복에 음식 배율은 안 곱함).
                var fc = _data.Balance.LevelUp.FallbackCard;
                // 한 생이 있으면 그 지갑으로(C-06a, 혈월 배율은 GainSilver 가 곱함), 없으면(시험 씬) 여기 자리에
                var life = LifeRunner.Instance != null ? LifeRunner.Instance.Life : null;
                // R-04Ua 은자 × (1 + 탐욕). 개방 몸이면 +30%
                var st = LifeRunner.Instance != null ? LifeRunner.Instance.Stats : null;
                float greed = st != null ? st.Bonus[BloodRain.Core.Build.StatKind.Greed] : 0f;
                if (life != null) life.GainSilver(Mathf.RoundToInt(fc.Silver * (1f + greed)));
                else Silver += fc.Silver;
                if (vitals == null) vitals = FindFirstObjectByType<PlayerVitals>();
                if (vitals != null) vitals.Heal(fc.Heal);
            }
            if ((o.Kind == OfferKind.NewWeapon || o.Kind == OfferKind.UpgradeWeapon) && weapons != null) weapons.Learn(o.Id);
            Close();
            RefreshSeals();
        }

        // 다시 고르기·파기·정진 단추도 열린 직후엔 안 받는다(웹 tooSoon 이 셋 다 막음, 마우스 클릭은 키보드 막기를 안 거침)
        void Reroll()
        {
            if (!_open || Guarded || _firstOpen || !_charges.Can(LevelUpAction.Reroll)) return;
            _charges.Reroll();
            _offers = _loadout.RollLevelUp(_rng, Luck(), _unlocked);
            _banishMode = false;
            Refresh();
        }

        void ToggleBanish()
        {
            if (!_open || Guarded || _firstOpen || !_charges.Can(LevelUpAction.Banish)) return;
            _banishMode = !_banishMode;
            Refresh();
        }

        void Skip()
        {
            if (!_open || Guarded || !_charges.Can(LevelUpAction.Skip)) return;
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
            _title.text = _banishMode ? Loc.T("ui.levelup.title_banish") : Loc.F("ui.levelup.title", _handledLevel - _pending);
            for (int i = _cardRow.childCount - 1; i >= 0; i--) Destroy(_cardRow.GetChild(i).gameObject);
            for (int i = 0; i < _offers.Count; i++) MakeCard(i, _offers[i]);
            BuildSlotPanel();
            LightSlots(null);

            // {1} 은 조작 재설정한 단축키(WP-69U, 키를 잃었으면 빈 칸)
            _rerollLabel.text = Loc.F("ui.levelup.reroll", _charges.Rerolls, ControlInput.FirstLabel(BloodRain.Core.Meta.ControlAction.Reroll) ?? "");
            _banishLabel.text = Loc.F(_banishMode ? "ui.levelup.banish_cancel" : "ui.levelup.banish", _charges.Banishes, ControlInput.FirstLabel(BloodRain.Core.Meta.ControlAction.Banish) ?? "");
            _skipLabel.text = Loc.F("ui.levelup.skip", _charges.Skips, ControlInput.FirstLabel(BloodRain.Core.Meta.ControlAction.Skip) ?? "");
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
            // UI-01U 커서가 가면 이 카드가 켜는 옆 칸을 빛낸다
            var trig = card.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => LightSlots(o));
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => LightSlots(null));
            trig.triggers.Add(enter); trig.triggers.Add(exit);

            var spine = MakeImage(card.rectTransform, "책등", _banishMode && o.Kind != OfferKind.Fallback ? Blood : Ink);
            Place(spine.rectTransform, new Vector2(0f, 0.5f), new Vector2(9, 0), new Vector2(18, 460));

            var k = MakeText(card.rectTransform, "갈래", 26, Blood, TextAlignmentOptions.Center);
            Place(k.rectTransform, new Vector2(0.5f, 1f), new Vector2(8, -50), new Vector2(290, 40));
            k.text = kind;
            k.enableAutoSizing = true; k.fontSizeMin = 16; k.fontSizeMax = 26;
            var n = MakeText(card.rectTransform, "이름", 44, Ink, TextAlignmentOptions.Center);
            Place(n.rectTransform, new Vector2(0.5f, 1f), new Vector2(8, -130), new Vector2(290, 110));
            n.textWrappingMode = TextWrappingModes.Normal;
            n.text = name;
            n.enableAutoSizing = true; n.fontSizeMin = 26; n.fontSizeMax = 44;
            var l = MakeText(card.rectTransform, "단계", 32, Ink, TextAlignmentOptions.Center);
            Place(l.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(8, 10), new Vector2(290, 50));
            l.text = level;
            var d = MakeText(card.rectTransform, "설명", 24, new Color(0.3f, 0.26f, 0.22f), TextAlignmentOptions.Center);
            Place(d.rectTransform, new Vector2(0.5f, 0f), new Vector2(8, 98), new Vector2(290, 110));
            if (o.Kind != OfferKind.Fallback) DrawSynergy(card.rectTransform, o, k);
            d.textWrappingMode = TextWrappingModes.Normal;
            d.text = note;
            d.enableAutoSizing = true; d.fontSizeMin = 16; d.fontSizeMax = 24;
            var key = MakeText(card.rectTransform, "키", 26, new Color(0.45f, 0.4f, 0.35f), TextAlignmentOptions.Center);
            Place(key.rectTransform, new Vector2(0.5f, 0f), new Vector2(8, 26), new Vector2(290, 36));
            key.text = (index + 1).ToString();
        }

        // ── 시너지 표시(S-04 첫 판, 기획서 20장, 웹 프로토타입 offerCard) ─────────────
        //   카드 오른쪽 위 = 문파 인장: 문파 이름 두 자 + 고른 뒤 진전 수/다음 단계. 새 단계에 닿으면 핏빛 인장.
        //   카드 가운데 아래 = 연환 띠: 이 카드로 켜지면 핏빛 띠 "연환 완성", 짝은 있는데 레벨이 모자라면 먹 띠,
        //   비급이 잠긴 연환이면 '?' 띠(이름을 감춘다).

        static readonly Color Gold = new Color(0.79f, 0.64f, 0.16f);
        static readonly Color SealDim = new Color(0.55f, 0.10f, 0.08f, 0.35f);

        void DrawSynergy(RectTransform card, Offer o, TextMeshProUGUI kindText)
        {
            var pv = BloodRain.Core.Build.Synergy.Preview(_data, _loadout, o, null, _restored);
            if (pv.School != null)
            {
                bool reaches = false;
                foreach (var r in pv.Reached) if (r.School.Id == pv.School.Id) reaches = true;
                var st = BloodRain.Core.Build.Synergy.Compute(_data, _loadout, null, _restored).School(pv.School.Id);
                int next = pv.School.MaxTierCount;
                foreach (var t in pv.School.Tiers) if (t.Count > pv.SchoolCountAfter && t.Count < next) next = t.Count;
                var seal = MakeImage(card, "문파 인장", reaches ? Blood : SealDim);
                Place(seal.rectTransform, new Vector2(1f, 1f), new Vector2(-46, -46), new Vector2(72, 72));
                var sealText = MakeText(seal.rectTransform, "글", 20, reaches ? PaperHot : Blood, TextAlignmentOptions.Center);
                Stretch(sealText.rectTransform);
                sealText.lineSpacing = -20;
                // 영어는 이름 전체라 길다(L-01b2): 칸 안에서 줄이고, 머리글은 인장 왼쪽까지만 쓴다
                sealText.enableAutoSizing = true; sealText.fontSizeMin = 9; sealText.fontSizeMax = 20;
                sealText.margin = new Vector4(3, 3, 3, 3);
                Place(kindText.rectTransform, new Vector2(0.5f, 1f), new Vector2(-34, -50), new Vector2(214, 40));
                sealText.text = $"{SchoolSeal(pv.School)}\n{pv.SchoolCountAfter}/{next}";
                if (st.LockedTiers.Count > 0) sealText.text += "?";
            }

            if (pv.Links.Count == 0) return;
            LinkHint best = pv.Links[0];
            foreach (var h in pv.Links) if (h.Completes && !best.Completes) best = h;
            bool done = best.Completes && !best.Locked;
            var band = MakeImage(card, "연환 띠", best.Locked ? new Color(0.25f, 0.22f, 0.2f) : done ? Blood : Ink);
            Place(band.rectTransform, new Vector2(0.5f, 0f), new Vector2(8, 180), new Vector2(300, 44));
            var bt = MakeText(band.rectTransform, "글", 22, done ? Gold : Paper, TextAlignmentOptions.Center);
            Stretch(bt.rectTransform);
            bt.textWrappingMode = TextWrappingModes.NoWrap;
            bt.enableAutoSizing = true; bt.fontSizeMin = 14; bt.fontSizeMax = 22;
            bt.margin = new Vector4(8, 0, 8, 0);
            string partner = WeaponName(_data.Weapon(best.Partner));
            string linkName = Loc.T($"synergies.links.{best.Link.Id}.name", best.Link.NameKo);
            bt.text = best.Locked ? Loc.T(best.Completes ? "ui.levelup.link.torn_complete" : "ui.levelup.link.torn")
                : done ? Loc.F("ui.levelup.link.done", linkName)
                : Loc.F("ui.levelup.link.need", linkName, partner, best.Link.MinLevel);
            if (pv.Links.Count > 1) bt.text += Loc.F("ui.levelup.link.more", pv.Links.Count - 1);
        }

        static string Short(string name) => name.Length <= 2 ? name : name.Substring(0, 2);

        // 인장 칸(72px)에는 한국어·간체는 앞 두 글자, 영어는 이름 전체(글자 크기는 TMP 가 줄인다).
        static string SchoolSeal(SchoolDef school)
        {
            var name = Loc.T($"synergies.schools.{school.Id}.name", school.NameKo);
            return Loc.Current.Lang == "en" ? name : Short(name);
        }

        /// <summary>HUD 진전 인장: 화면 왼쪽 위에 단계가 켜진 문파 인장과 켜진 연환 수. 잠긴 단계·연환은 '?' 인장.</summary>
        void BuildSeals()
        {
            var go = new GameObject("진전 인장", typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;
            _sealRow = new GameObject("줄", typeof(RectTransform)).GetComponent<RectTransform>();
            _sealRow.SetParent(go.transform, false);
            _sealRow.anchorMin = _sealRow.anchorMax = _sealRow.pivot = new Vector2(0f, 1f);
            _sealRow.anchoredPosition = new Vector2(24, -160); // 위는 TopHud 무공·심법 칸과 경지·그릇 줄(HUD-01U ③)이 쓴다
            _sealRow.sizeDelta = new Vector2(900, 64);
            var h = _sealRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 10; h.childAlignment = TextAnchor.UpperLeft;
            h.childControlWidth = h.childControlHeight = false;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
        }

        void RefreshSeals()
        {
            if (_sealRow == null) return;
            var syn = Synergy;
            var key = new System.Text.StringBuilder();
            foreach (var s in syn.Schools) key.Append(s.School.Id).Append(s.TierCount).Append(s.LockedTiers.Count).Append(',');
            key.Append(syn.Links.Count).Append('/').Append(syn.LockedLinks.Count);
            if (key.ToString() == _sealKey) return;
            _sealKey = key.ToString();
            // 시너지가 바뀌면 생 기록에 남긴다(S-04a). 잠긴 비급 조건을 갖췄으면 생이 끝날 때 찢긴 비급으로 오른다.
            if (LifeRunner.Instance != null && LifeRunner.Instance.Life != null) LifeRunner.Instance.Life.Log.NoteSynergy(syn);

            for (int i = _sealRow.childCount - 1; i >= 0; i--) Destroy(_sealRow.GetChild(i).gameObject);
            foreach (var s in syn.Schools)
            {
                if (s.Tier == null && s.LockedTiers.Count == 0) continue;
                var seal = MakeImage(_sealRow, s.School.NameKo, s.Tier != null ? Blood : SealDim);
                seal.rectTransform.sizeDelta = new Vector2(60, 60);
                var t = MakeText(seal.rectTransform, "글", 18, PaperHot, TextAlignmentOptions.Center);
                Stretch(t.rectTransform);
                t.lineSpacing = -20;
                t.text = s.Tier != null ? $"{SchoolSeal(s.School)}\n{s.TierCount}" : $"{SchoolSeal(s.School)}\n?";
            }
            if (syn.Links.Count + syn.LockedLinks.Count > 0)
            {
                var band = MakeImage(_sealRow, "연환", Ink);
                band.rectTransform.sizeDelta = new Vector2(110, 60);
                var t = MakeText(band.rectTransform, "글", 18, Gold, TextAlignmentOptions.Center);
                Stretch(t.rectTransform);
                t.text = Loc.F("ui.levelup.seal.links", syn.Links.Count) + (syn.LockedLinks.Count > 0 ? $"\n<color=#E8DEC7>? {syn.LockedLinks.Count}</color>" : "");
            }
        }

        (string kind, string name, string level, string note) Describe(Offer o)
        {
            switch (o.Kind)
            {
                case OfferKind.NewWeapon:
                {
                    var w = _data.Weapon(o.Id);
                    return (Loc.T("ui.levelup.kind.new_weapon"), WeaponName(w), Loc.T("ui.levelup.level_one"), Loc.F("ui.levelup.pair_passive", Aspect(w.Yinyang), PassiveName(w.PairPassive)));
                }
                case OfferKind.UpgradeWeapon:
                {
                    var w = _data.Weapon(o.Id);
                    int lv = _loadout.Weapons[o.Id];
                    return (Loc.T("ui.levelup.kind.up_weapon"), WeaponName(w), Loc.F("ui.levelup.level_up", lv, lv + 1), UpgradeNote(w, o, lv + 1));
                }
                case OfferKind.NewPassive:
                {
                    var p = _data.Passive(o.Id);
                    return (Loc.T("ui.levelup.kind.new_passive"), Loc.T($"passives.{p.Id}.name", p.NameKo), Loc.T("ui.levelup.level_one"), Aspect(p.Yinyang));
                }
                case OfferKind.UpgradePassive:
                {
                    var p = _data.Passive(o.Id);
                    int lv = _loadout.Passives[o.Id];
                    return (Loc.T("ui.levelup.kind.up_passive"), Loc.T($"passives.{p.Id}.name", p.NameKo), Loc.F("ui.levelup.level_up", lv, lv + 1), Aspect(p.Yinyang));
                }
                default:
                {
                    var fc = _data.Balance.LevelUp.FallbackCard;
                    return (Loc.T("ui.levelup.kind.fallback"), Loc.T("ui.levelup.fallback.name"), Loc.F("ui.levelup.fallback.level", fc.Silver, fc.Heal.ToString("0")), Loc.T("ui.levelup.fallback.note"));
                }
            }
        }

        // UI-01U 카드 둘째 줄(12장 '레벨업 카드 규칙'): 바뀌는 수치 — 무공 피해 a → b, 이정표가 있으면 ' · 새 동작'. 웹 offerCard 와 같은 셈(LevelUpView.Card).
        string UpgradeNote(WeaponDef w, Offer o, int level)
        {
            var c = LevelUpView.Card(_data, _loadout, o);
            if (c == null) return Milestone(w, level);
            string dmg = Loc.F("ui.levelup.damage", c.DamageFrom.ToString("0"), c.DamageTo.ToString("0"));
            if (c.Milestone == null) return dmg;
            return dmg + " · " + Loc.T($"weapons.{w.Id}.milestones.{level}", c.Milestone);
        }

        static string Milestone(WeaponDef w, int level)
        {
            if (w.Milestones != null && w.Milestones.TryGetValue(level.ToString(), out var m))
                return Loc.F("ui.levelup.milestone", Loc.T($"weapons.{w.Id}.milestones.{level}", m));
            return Loc.T("ui.levelup.power_up");
        }

        string PassiveName(string id)
        {
            if (string.IsNullOrEmpty(id)) return Loc.T("ui.levelup.none");
            foreach (var p in _data.Passives) if (p.Id == id) return Loc.T($"passives.{p.Id}.name", p.NameKo);
            return id;
        }

        static string Aspect(string yy) => Loc.T(yy == "yang" ? "ui.aspect.yang" : yy == "yin" ? "ui.aspect.yin" : "ui.aspect.neutral");

        static string WeaponName(WeaponDef w) => Loc.T($"weapons.{w.Id}.name", w.NameKo);

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
