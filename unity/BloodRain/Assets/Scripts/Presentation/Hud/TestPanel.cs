using System.Collections.Generic;
using BloodRain.Core.Build;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Presentation.Combat;
using BloodRain.Presentation.Meta;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace BloodRain.Presentation.Hud
{
    // 시험 메뉴(2026-11-17 사람: 웹에 올린 판으로는 이펙트 단계·중원 지도를 볼 길이 없어 "테스팅 의미가 없다").
    // 개발 판(에디터·개발 빌드·dev 주소·localhost)에서만 붙는다. 정식 빌드에는 안 나온다.
    //   왼쪽 가운데 '시험' 단추나 F2 로 연다. 세상은 멈추지 않는다(이펙트를 보면서 누르려고).
    //   무공: 익히기 / +1 / Lv8 — 에디터 메뉴 WeaponTrialMenu 와 같은 길(Loadout.Apply + WeaponRunner.Learn)
    //   경지 +1(LifeRunner.GiveXpToNextRealm) · 기혈 채움 · 안 죽기 · 적 수 ×2 ÷2
    //   몸 바꾸기: 그 문파 몸으로 Run 을 다시 연다(에디터 메뉴 SectBodyMenu 와 같은 길). 큰 지도(M)에서 땅을 눌러도 된다.
    public sealed class TestPanel : MonoBehaviour
    {
        public LifeRunner runner;

        /// <summary>개발 판인가. 정식 빌드(릴리스 + 정식 주소·스탠드얼론)에서는 거짓.</summary>
        public static bool Allowed
        {
            get
            {
                if (Application.isEditor || Debug.isDebugBuild) return true;
                string u = Application.absoluteURL;
                return !string.IsNullOrEmpty(u) && (u.Contains("//dev.") || u.Contains("//localhost") || u.Contains("//127.0.0.1"));
            }
        }

        bool _open, _undying;
        float _scroll;
        GUIStyle _btn, _lbl, _head;
        Texture2D _bg;
        WorldMapHud _mapHud;
        // 웹 빌드에서는 IMGUI 가 마우스 누름을 못 받는다(GUI.Button 이 안 눌림, 2026-11-17 dev 판에서 확인).
        // 그래서 그릴 때 단추 자리와 할 일을 적어 두고(_hits), 누름은 Input System 포인터로 직접 본다.
        readonly List<(Rect r, System.Action act)> _hits = new List<(Rect, System.Action)>();
        Rect _panel;

        void Start()
        {
            _bg = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            _bg.SetPixel(0, 0, Color.white); _bg.Apply();
            _mapHud = GetComponent<WorldMapHud>();
            if (_mapHud != null) _mapHud.RegionClicked = r => { if (!string.IsNullOrEmpty(r.Sect)) SwitchBody(r.Sect); };
        }

        void OnDestroy() { if (_bg != null) Destroy(_bg); if (_fast && Time.timeScale > 1f) Time.timeScale = 1f; LifeRunner.TestClockScale = 1f; }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f2Key.wasPressedThisFrame) _open = !_open;
            if (_undying && runner != null && runner.vitals != null && runner.Life != null && runner.Life.Record == null
                && runner.vitals.Health < runner.vitals.MaxHealth) runner.vitals.Heal(runner.vitals.MaxHealth);
            // 빨리 감기: 다른 화면이 시간을 세웠다(0) 풀면(1) 다시 4배로. 세운 동안은 건드리지 않는다
            if (TestAutopilot.Sticky) EnsureAuto(); // 서장 다시 하기·새 생으로 장면이 다시 열려도 자동 플레이를 이어 간다
            if (_fast && Time.timeScale == 1f) Time.timeScale = 4f;
            LifeRunner.TestClockScale = _fast ? 4f : 1f;
            var ptr = Pointer.current;
            if (ptr != null)
            {
                var pp = ptr.position.ReadValue();
                var gui = new Vector2(pp.x, Screen.height - pp.y);
                if (ptr.press.wasPressedThisFrame) Press(gui);
                var mouse = Mouse.current;
                if (_open && mouse != null && _panel.Contains(gui)) _scroll = Mathf.Max(0f, _scroll - mouse.scroll.ReadValue().y * 0.4f);
            }
        }

        /// <summary>GUI 좌표(왼쪽 위가 0,0)를 눌렀다. 맨 위에 그린 단추부터 본다. 누른 단추가 있으면 참.</summary>
        public bool Press(Vector2 gui)
        {
            for (int i = _hits.Count - 1; i >= 0; i--)
                if (_hits[i].r.Contains(gui)) { var act = _hits[i].act; act(); return true; }
            return false;
        }

        /// <summary>그 글자의 단추를 누른다(시험·자동 확인용). 없으면 거짓.</summary>
        public bool PressLabel(string label)
        {
            for (int i = 0; i < _labels.Count; i++)
                if (_labels[i] == label) { _hits[i].act(); return true; }
            return false;
        }
        readonly List<string> _labels = new List<string>();

        static bool _fast; // 장면이 다시 열려도 이어 간다(자동 플레이와 함께 쓰려고)
        TestAutopilot _auto;

        void EnsureAuto()
        {
            if (_auto != null || runner == null) return;
            _auto = GetComponent<TestAutopilot>();
            if (_auto == null) { _auto = gameObject.AddComponent<TestAutopilot>(); _auto.runner = runner; }
            if (TestAutopilot.Sticky) _auto.On = true;
        }

        bool Btn(Rect r, string label, System.Action act, bool enabled = true)
        {
            GUI.color = enabled ? new Color(0.24f, 0.21f, 0.18f, 0.96f) : new Color(0.16f, 0.15f, 0.14f, 0.7f);
            GUI.DrawTexture(r, _bg);
            GUI.color = new Color(0.62f, 0.52f, 0.36f, enabled ? 0.9f : 0.3f);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 1f, r.width, 1f), _bg);
            GUI.color = Color.white;
            _btn.normal.textColor = enabled ? new Color(0.95f, 0.92f, 0.84f) : new Color(0.6f, 0.58f, 0.54f);
            GUI.Label(r, label, _btn);
            if (enabled) { _hits.Add((r, act)); _labels.Add(label); }
            return false;
        }

        void OnGUI()
        {
            if (runner == null || runner.Life == null) return;
            if (Event.current.type != EventType.Repaint) return;
            ImguiFont.Use();
            float k = Screen.height / 720f;
            if (_btn == null)
            {
                _btn = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
                _lbl = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft };
                _head = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold };
            }
            int fs = Mathf.RoundToInt(13f * k);
            _btn.fontSize = fs; _lbl.fontSize = fs; _head.fontSize = Mathf.RoundToInt(14f * k);
            _lbl.normal.textColor = new Color(0.93f, 0.9f, 0.82f); _head.normal.textColor = new Color(0.95f, 0.78f, 0.35f);
            _hits.Clear(); _labels.Clear();

            // 단추는 왼쪽 가운데(무공 칸·문파 인장 밑, 음양 HUD 위), 판은 그 오른쪽에 길게(왼쪽 HUD 를 안 가리게)
            float bw = 70f * k, bh = 28f * k;
            Btn(new Rect(8f * k, Screen.height * 0.36f, bw, bh), _open ? "닫기 F2" : "시험 F2", () => _open = !_open);
            if (!_open) { _panel = default; return; }

            float w = 330f * k, top = 96f * k, h = Screen.height - top - 20f * k, pad = 8f * k, rh = 26f * k, gap = 4f * k;
            _panel = new Rect(136f * k, top, w, h);
            GUI.color = new Color(0.08f, 0.07f, 0.06f, 0.88f);
            GUI.DrawTexture(_panel, _bg);
            GUI.color = Color.white;
            var life = runner.Life;
            var data = DataBoot.Data;
            float x0 = _panel.x + pad, cw = w - pad * 2f, y = _panel.y + pad - _scroll;
            // 판 밖으로 나간 줄은 그리지도, 누르지도 않는다
            bool Vis(float yy) => yy >= _panel.y && yy + rh <= _panel.yMax;
            void Head(string t) { if (Vis(y)) GUI.Label(new Rect(x0, y, cw, rh), t, _head); y += rh; }
            void Row(params (string label, System.Action act, bool on)[] cells)
            {
                if (Vis(y))
                {
                    float cellW = (cw - gap * (cells.Length - 1)) / cells.Length;
                    for (int i = 0; i < cells.Length; i++) Btn(new Rect(x0 + i * (cellW + gap), y, cellW, rh), cells[i].label, cells[i].act, cells[i].on);
                }
                y += rh + gap;
            }

            Head("시험 메뉴 — 개발 판에서만 보인다");
            if (Vis(y)) GUI.Label(new Rect(x0, y, cw, rh), $"{Loc.Realm(life.Scale.Realms[life.Realm])} · Lv {life.Level} · 적 {(runner.enemies != null ? runner.enemies.Count : 0)}", _lbl);
            y += rh;
            Row(("경지 +1", () => runner.GiveXpToNextRealm(), true),
                ("기혈 채움", () => { if (runner.vitals != null) runner.vitals.Heal(runner.vitals.MaxHealth); }, true),
                (_undying ? "안 죽기 켬" : "안 죽기 끔", () => _undying = !_undying, true));
            if (runner.enemies != null)
                Row(($"적 {runner.enemies.targetCount} ×2", () => runner.enemies.targetCount = Mathf.Min(runner.enemies.capacity, runner.enemies.targetCount * 2), true),
                    ("÷2", () => runner.enemies.targetCount = Mathf.Max(1, runner.enemies.targetCount / 2), true),
                    ("지도 M", () => { if (_mapHud != null) { if (_mapHud.IsOpen) _mapHud.Close(); else _mapHud.Open(); } }, true));

            // 정예·땅의 주인·빨리 감기(2026-11-17 밤 사람: 정예와 보스가 나오는 걸 봐야 한다). 정예는 규칙으로 5분마다, 땅의 주인은 절정부터 그 땅 한가운데
            bool boss = runner.CanChallengeBoss();
            Row(("정예 부르기", () => runner.SummonElite(), true),
                (boss ? "땅의 주인과 싸우기" : "땅의 주인 없음", () => runner.ChallengeBoss(), boss),
                (_fast ? "빠르기 ×4 켬" : "빠르기 ×4 끔", () => { _fast = !_fast; if (!_fast && Time.timeScale > 1f) Time.timeScale = 1f; }, true));
            // 자동 플레이(2026-11-20): 적을 피해 걷고 레벨업은 자동으로 고른다. 빠르기 ×4 와 같이 쓰면 한 생을 금방 본다
            EnsureAuto();
            Row((_auto.On ? "자동 플레이 켬" : "자동 플레이 끔", () => { _auto.On = !_auto.On; TestAutopilot.Sticky = _auto.On; }, true));

            y += gap;
            Head("몸 바꾸기 — 그 문파 땅에서 새 생");
            var sects = data.Cheonha.Sects;
            for (int i = 0; i < sects.Count; i += 3)
            {
                var cells = new List<(string, System.Action, bool)>();
                for (int j = i; j < Mathf.Min(i + 3, sects.Count); j++) { string id = sects[j].Id; cells.Add((Loc.Sect(sects[j]), () => SwitchBody(id), true)); }
                Row(cells.ToArray());
            }

            y += gap;
            Head("무공 — 누를 때마다 다음 단계(1~5단), Lv8 이면 절기 깨우기");
            int max = data.Balance.Levels.WeaponMax;
            var ws = data.Weapons;
            for (int i = 0; i < ws.Count; i += 2)
            {
                var cells = new List<(string, System.Action, bool)>();
                for (int j = i; j < Mathf.Min(i + 2, ws.Count); j++)
                {
                    var wdef = ws[j];
                    life.Loadout.Weapons.TryGetValue(wdef.Id, out int lv);
                    string id = wdef.Id;
                    int next = NextStageLevel(wdef, lv, max);
                    string state = lv > 0 ? $"{WeaponGrowth.VisualStage(wdef, lv)}단 Lv{lv}" : "—";
                    // 2026-11-20 밤: 절기(VF-04U)를 웹 판에서 바로 보게 — Lv8 이면 짝 심법을 맞추고 깨운다(보갑·그릇 없이, 시험이니)
                    var evoDef = EvoFor(data, id);
                    bool evolved = System.Linq.Enumerable.Contains(life.Loadout.Evolved, id);
                    if (evolved) cells.Add(($"{Loc.Weapon(wdef)}  절기", () => { }, false));
                    else if (lv >= max && evoDef != null) cells.Add(($"{Loc.Weapon(wdef)}  → 절기", () => Awaken(evoDef), true));
                    else cells.Add(($"{Loc.Weapon(wdef)}  {state}", () => Raise(id, next), lv < max));
                }
                Row(cells.ToArray());
            }
            // 내용이 판보다 길면 휠로 굴린다
            float content = y + _scroll - _panel.y;
            _scroll = Mathf.Clamp(_scroll, 0f, Mathf.Max(0f, content - h + pad));
        }

        /// <summary>이펙트 단계가 하나 오르는 가장 낮은 레벨(없으면 1, 더 오를 단계가 없으면 최대 레벨).</summary>
        static int NextStageLevel(WeaponDef w, int level, int max)
        {
            if (level <= 0) return 1;
            int st = WeaponGrowth.VisualStage(w, level);
            for (int lv = level + 1; lv <= max; lv++) if (WeaponGrowth.VisualStage(w, lv) > st) return lv;
            return max;
        }

        static EvolutionDef EvoFor(GameData data, string weaponId)
        {
            foreach (var e in data.Evolutions) if (e.Weapon == weaponId) return e;
            return null;
        }

        /// <summary>시험: 그 절기를 깨운다. 짝 심법을 각성 기준 레벨까지 올리고 Loadout.Evolve(보갑·그릇 없이).</summary>
        public void Awaken(EvolutionDef evo)
        {
            var lo = runner.Life.Loadout;
            if (lo == null || evo == null) return;
            Raise(evo.Weapon, DataBoot.Data.Balance.Levels.WeaponMax);
            Raise(evo.Passive, DataBoot.Data.Balance.Evolution.PairPassiveMinLevel);
            lo.Evolve(evo);
        }

        /// <summary>그 무공을 level 까지. 칸이 모자라면 칸을 하나 늘린다(시험이니).</summary>
        public void Raise(string id, int level)
        {
            var weapons = runner.weapons != null ? runner.weapons : FindFirstObjectByType<WeaponRunner>();
            var lo = runner.Life.Loadout;
            if (weapons == null || lo == null) return;
            // 심법 id 를 넘기면 무공 칸에 잘못 들어가 레벨업 카드가 깨졌다(2026-11-20 시험 중). 심법은 심법 칸으로
            if (!System.Linq.Enumerable.Any(DataBoot.Data.Weapons, w => w.Id == id))
            {
                if (!System.Linq.Enumerable.Any(DataBoot.Data.Passives, p => p.Id == id)) return;
                level = Mathf.Min(level, DataBoot.Data.Balance.Levels.PassiveMax);
                if (!lo.Passives.ContainsKey(id) && lo.Passives.Count >= lo.PassiveSlots) lo.AddExtraSlot(false);
                for (int guard = 0; guard < 16; guard++)
                {
                    if (lo.Passives.TryGetValue(id, out int pl) && pl >= level) break;
                    lo.Apply(new Offer(lo.Passives.ContainsKey(id) ? OfferKind.UpgradePassive : OfferKind.NewPassive, id));
                }
                return;
            }
            level = Mathf.Min(level, DataBoot.Data.Balance.Levels.WeaponMax);
            if (!lo.Weapons.ContainsKey(id) && lo.Weapons.Count >= lo.WeaponSlots) lo.AddExtraSlot(true);
            for (int guard = 0; guard < 16; guard++)
            {
                if (lo.Weapons.TryGetValue(id, out int cur) && cur >= level) break;
                lo.Apply(new Offer(lo.Weapons.ContainsKey(id) ? OfferKind.UpgradeWeapon : OfferKind.NewWeapon, id));
                weapons.Learn(id);
            }
        }

        /// <summary>그 문파 몸(성질 없음)으로 Run 을 다시 연다. 혼은 지금 혼 그대로.</summary>
        public static void SwitchBody(string sectId)
        {
            var data = DataBoot.Data;
            var c = data.Cheonha;
            var sect = c.Sect(sectId);
            var cand = new RebirthCandidate(sect, c.Region(sect.Region), new List<BirthTraitDef>());
            var soul = RebirthHandoff.Soul ?? SoulStore.Load(data);
            if (!soul.PrologueDone)
            {
                // 새 혼은 서장부터라 문파 몸으로 바로 못 들어간다(Soul.BeginLife 가 "첫 생은 서장"이라며 막아 화면이 깨졌다 — 2026-11-18 웹에서 봄).
                // 시험에서는 서장을 마친 것으로 친 혼으로 바꿔 든다. 이 혼이 이 칸에 그대로 저장된다
                string json = System.Text.RegularExpressions.Regex.Replace(soul.ToJson(), "(\"prologueDone\"\\s*:\\s*)false", "$1true", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (!json.Contains("\"prologueDone\":true") && !json.Contains("\"prologueDone\": true")) json = "{\"prologueDone\":true," + json.TrimStart().Substring(1); // 칸이 빠져 있던 저장
                soul = Soul.FromJson(data, json);
            }
            RebirthHandoff.Soul = soul;
            RebirthHandoff.ClearBody();
            RebirthHandoff.Body = Rebirth.Body(c, cand);
            Time.timeScale = 1f;
            SceneManager.LoadScene("Run");
        }
    }
}
