using System.Collections.Generic;
using BloodRain.Core.Build;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using BloodRain.Presentation.Combat;
using BloodRain.Presentation.Hud;
using BloodRain.Presentation.Meta;
using BloodRain.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BloodRain.Presentation.Fx
{
    // C-09d 보갑과 각성(기획서 8장 '절기 그릇과 각성', 보상 표). 웹 프로토타입 openChest 와 같은 셈.
    //   정예가 쓰러지면 그 자리에 보갑 하나. 흑백 쌍강시 대형은 한 쌍이 다 누웠을 때만(EnemyField.LastKillEndsPair).
    //   플레이어가 닿으면(Drops.InPickup) 연다 — 각성 연출 중(Awakening.DefersLevelUpAndChest)이나 멈춘 화면에서는 기다린다.
    //   Chest.Open(data, loadout, 레벨, rng, Chest.ExtraUpgrades(진전)) — 장강수로채 진전 4단계면 강화 하나 더(synergies.json chestUpgrades).
    //     강화 목록이면 Loadout 에 이미 들었으니 무공은 WeaponRunner.Learn 으로 따라가고, 짧은 목록 화면(세상 멈춤)을 띄운다.
    //     행낭(Fallback)은 레벨업 행낭 카드와 같은 은자·기혈.
    //     각성이면 Life.Awaken(Log·Loadout.Evolve·Core Awakening — Core 주석의 차례대로 연출 시작 전에) 뒤 AwakeningFx.Play.
    //     연출이 끝나면 절기 이름을 떠오르는 글로. 절기 모양(C-09e)·파동 피해 판정은 아직.
    //   비전 판정엔 RunRecord.OpenChest(기혈 비율).
    //   그림은 아직 없어(art_queue) 금테 두른 네모로만 그린다.
    public sealed class ChestLayer : MonoBehaviour
    {
        public static ChestLayer Instance { get; private set; }

        public LifeRunner runner;
        [Tooltip("시험용: 이 레벨 아래면 이 레벨로 셈(절기 그릇은 레벨로 정해진다). 0 이면 끔")]
        public int debugMinLevel;

        sealed class Box { public Vector2 Pos; public GameObject Go; public float Age; }
        readonly List<Box> _boxes = new List<Box>();
        readonly System.Random _rng = new System.Random();
        readonly List<(Vector2 at, string text, float born)> _popups = new List<(Vector2, string, float)>();
        Sprite _square;
        GUIStyle _style, _listStyle, _titleStyle, _hintStyle;
        string _hint; // CH-01U 그릇 안내(없으면 null)
        SecretWatch _secrets;

        // 열린 목록 화면
        List<string> _lines;
        float _savedTimeScale = 1f;
        float _openedAt;
        // 각성 연출이 끝나면 띄울 절기
        EvolutionDef _awakening;
        AwakeningFx _fx;

        /// <summary>시험용: 떨어진 보갑 수, 연 보갑 수, 각성한 수.</summary>
        public int Dropped { get; private set; }
        public int Opened { get; private set; }
        public int Awakened { get; private set; }
        public bool ListOpen => _lines != null;

        static float Scale => WorldScale.Current != null ? WorldScale.Current.Length(1f) : 1f;
        GameData Data => DataBoot.Data;

        void Awake()
        {
            Instance = this;
            if (runner == null) runner = GetComponent<LifeRunner>();
            _square = MakeSprite();
        }

        void Start()
        {
            if (runner != null && runner.enemies != null) runner.enemies.Killed += OnKilled;
            _secrets = FindFirstObjectByType<SecretWatch>();
            _fx = FindFirstObjectByType<AwakeningFx>();
        }

        void OnDestroy()
        {
            if (runner != null && runner.enemies != null) runner.enemies.Killed -= OnKilled;
            if (Instance == this) Instance = null;
            if (_lines != null) Time.timeScale = _savedTimeScale;
        }

        Vector2 PlayerPos => runner != null && runner.player != null ? (Vector2)runner.player.position : Vector2.zero;
        bool Alive => runner != null && runner.Life != null && runner.Life.Record == null;

        void OnKilled(Vector2 at, EnemyAgent a)
        {
            if (!Alive || runner.enemies == null) return;
            var def = runner.enemies.DefOf(a.Kind);
            if (def == null || def.Family != "elite") return;
            if (!runner.enemies.LastKillEndsPair) return; // 쌍강시 대형: 짝이 아직 서 있다
            Place(at);
        }

        /// <summary>보갑 하나를 놓는다(정예 처치·시험 메뉴).</summary>
        public void Place(Vector2 at)
        {
            var go = Make("보갑", at, new Color(0.45f, 0.20f, 0.12f), 0.8f, 6);
            var rim = Make("금테", at, new Color(0.85f, 0.69f, 0.23f), 0.95f, 5);
            rim.transform.SetParent(go.transform, true);
            _boxes.Add(new Box { Pos = at, Go = go });
            Dropped++;
        }

        /// <summary>시험 메뉴: 플레이어 바로 옆(경지 m)에 보갑 하나.</summary>
        public void PlaceNear(float realmMeters = 1.5f) => Place(PlayerPos + Vector2.right * realmMeters * Scale);

        void Update()
        {
            if (_lines != null)
            {
                // 목록 화면: 0.35초 뒤부터 Enter·Space·Esc·클릭으로 닫는다(레벨업 입력 막기와 같은 실제 시간)
                if (Time.unscaledTime - _openedAt < Data.Balance.LevelUp.InputGuardSeconds) return;
                var kb = Keyboard.current;
                var mouse = Mouse.current;
                bool close = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
                             || (mouse != null && mouse.leftButton.wasPressedThisFrame);
                if (close) CloseList();
                return;
            }
            if (_awakening != null && (_fx == null || !_fx.IsPlaying))
            {
                var evo = _awakening;
                _awakening = null;
                Pop(PlayerPos, Loc.F("ui.chest.awakened", Loc.T($"evolutions.{evo.Id}.name", evo.NameKo)));
            }
            if (!Alive || Time.timeScale <= 0f || _boxes.Count == 0) return;
            var life = runner.Life;
            if (life.Awakening.DefersLevelUpAndChest || (_fx != null && _fx.IsPlaying)) return;
            var me = PlayerPos;
            float sc = Scale;
            var spawn = Data.Balance.Drops.LanternSpawn;
            for (int i = _boxes.Count - 1; i >= 0; i--)
            {
                var b = _boxes[i];
                b.Age += Time.deltaTime;
                if (b.Age < 0.3f || !Drops.InPickup(spawn, Vector2.Distance(b.Pos, me) / sc)) continue;
                Destroy(b.Go);
                _boxes.RemoveAt(i);
                Open(b.Pos);
                return; // 한 번에 한 상자씩(목록 화면이 열리면 세상이 멈춘다)
            }
        }

        void Open(Vector2 at)
        {
            var life = runner.Life;
            var soul = RebirthHandoff.Soul;
            var syn = Synergy.Compute(Data, life.Loadout, life.Body.SchoolExtra(life.Encounters.SchoolExtra), soul != null ? soul.RestoredManuals : null);
            int extra = Chest.ExtraUpgrades(syn);
            int level = Mathf.Max(life.Level, debugMinLevel);
            var result = Chest.Open(Data, life.Loadout, level, _rng, extra);
            Opened++;
            var vitals = runner.vitals;
            float ratio = vitals != null && vitals.MaxHealth > 0f ? vitals.Health / vitals.MaxHealth : 1f;
            _secrets?.Record?.OpenChest(ratio);
            _secrets?.Poke();

            if (result.Awakened != null)
            {
                var evo = result.Awakened;
                var info = life.Awaken(evo);
                Awakened++;
                _awakening = evo;
                if (_fx == null) _fx = FindFirstObjectByType<AwakeningFx>();
                if (_fx != null && runner.player != null) _fx.Play(evo, runner.player, info.LongByDefault);
                Debug.Log($"[ChestLayer] 보갑 → 절기 각성 {evo.NameKo}({evo.Weapon}) · 길게 {info.LongByDefault} · 레벨 {level}{(_fx == null ? " · 씬에 AwakeningFx 없음" : "")}");
                return;
            }

            var weapons = runner.weapons != null ? runner.weapons : FindFirstObjectByType<WeaponRunner>();
            var lines = new List<string>();
            foreach (var o in result.Upgrades)
            {
                switch (o.Kind)
                {
                    case OfferKind.UpgradeWeapon:
                    {
                        if (weapons != null) weapons.Learn(o.Id);
                        var w = Data.Weapon(o.Id);
                        life.Loadout.Weapons.TryGetValue(o.Id, out var lv);
                        lines.Add(Loc.F("ui.chest.upgrade", w != null ? Loc.Weapon(w) : o.Id, lv));
                        break;
                    }
                    case OfferKind.UpgradePassive:
                    {
                        life.Loadout.Passives.TryGetValue(o.Id, out var lv);
                        lines.Add(Loc.F("ui.chest.upgrade", PassiveName(o.Id), lv));
                        break;
                    }
                    default:
                    {
                        // 행낭: 레벨업 행낭 카드와 같은 은자·기혈(은자 × (1 + 탐욕))
                        var fc = Data.Balance.LevelUp.FallbackCard;
                        var st = runner.Stats;
                        float greed = st != null ? st.Bonus[StatKind.Greed] : 0f;
                        int silver = life.GainSilver(Mathf.RoundToInt(fc.Silver * (1f + greed)));
                        if (vitals != null) vitals.Heal(fc.Heal);
                        lines.Add(Loc.F("ui.chest.fallback", silver, Mathf.RoundToInt(fc.Heal)));
                        break;
                    }
                }
            }
            // CH-01U 그릇 안내 한 줄(8장, 웹 chestHint): 강화가 Loadout 에 들어간 뒤에 센다
            var hint = Chest.Hint(Data, life.Loadout, level);
            _hint = hint.Kind == ChestHintKind.NoVessel ? Loc.F("ui.chest.hint_novessel", Loc.Realm(hint.Next), hint.Next.Level)
                  : hint.Kind == ChestHintKind.Full ? Loc.F("ui.chest.hint_full", Loc.Realm(hint.Next), hint.Next.Level)
                  : null;
            Debug.Log($"[ChestLayer] 보갑 → 강화 {result.Upgrades.Count}개(진전 덤 {extra}): {string.Join(", ", lines)}{(_hint != null ? " · " + _hint : "")}");
            _lines = lines;
            _savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            _openedAt = Time.unscaledTime;
        }

        void CloseList()
        {
            _lines = null;
            _hint = null;
            Time.timeScale = _savedTimeScale;
        }

        string PassiveName(string id)
        {
            foreach (var p in Data.Passives) if (p.Id == id) return Loc.T($"passives.{p.Id}.name", p.NameKo);
            return id;
        }

        void Pop(Vector2 at, string text) => _popups.Add((at, text, Time.unscaledTime));

        void OnGUI()
        {
            var cam = Camera.main;
            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            if (cam != null)
            {
                for (int i = _popups.Count - 1; i >= 0; i--)
                {
                    var (at, text, born) = _popups[i];
                    float t = (Time.unscaledTime - born) / 2f;
                    if (t >= 1f) { _popups.RemoveAt(i); continue; }
                    var sp = cam.WorldToScreenPoint(at);
                    GUI.color = new Color(1f, 0.82f, 0.38f, 1f - t * t);
                    GUI.Label(new Rect(sp.x - 200f, Screen.height - sp.y - 70f - t * 40f, 400f, 34f), text, _style);
                }
                GUI.color = Color.white;
            }
            if (_lines == null) return;
            // 짧은 목록 화면: 가운데 종이 한 장(그림 오기 전 임시)
            _titleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.55f, 0.10f, 0.08f) } };
            _listStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.12f, 0.10f, 0.09f) } };
            _hintStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 19, wordWrap = true, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.55f, 0.36f, 0.06f) } };
            float hintH = _hint != null ? 56f : 0f;
            float w = 460f, h = 130f + _lines.Count * 40f + hintH;
            var r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            GUI.color = new Color(0.05f, 0.04f, 0.04f, 0.6f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = new Color(0.91f, 0.87f, 0.78f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x, r.y + 14f, w, 40f), Loc.T("ui.chest.title"), _titleStyle);
            for (int i = 0; i < _lines.Count; i++)
                GUI.Label(new Rect(r.x, r.y + 64f + i * 40f, w, 36f), _lines[i], _listStyle);
            if (_hint != null) GUI.Label(new Rect(r.x + 20f, r.yMax - 46f - hintH, w - 40f, hintH), _hint, _hintStyle);
            GUI.Label(new Rect(r.x, r.yMax - 46f, w, 34f), Loc.T("ui.chest.close"), _listStyle);
        }

        GameObject Make(string name, Vector2 at, Color color, float realmMeters, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(at.x, at.y, 0f);
            go.transform.localScale = new Vector3(1.2f, 0.85f, 1f) * realmMeters * Scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _square;
            sr.color = color;
            sr.sortingOrder = order;
            return go;
        }

        static Sprite MakeSprite()
        {
            const int N = 16;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[N * N];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
        }
    }
}
