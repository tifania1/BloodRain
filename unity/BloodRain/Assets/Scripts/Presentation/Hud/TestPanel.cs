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
        Vector2 _scroll;
        GUIStyle _btn, _lbl, _head;
        Texture2D _bg;
        WorldMapHud _mapHud;

        void Start()
        {
            _bg = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            _bg.SetPixel(0, 0, Color.white); _bg.Apply();
            _mapHud = GetComponent<WorldMapHud>();
            if (_mapHud != null) _mapHud.RegionClicked = r => { if (!string.IsNullOrEmpty(r.Sect)) SwitchBody(r.Sect); };
        }

        void OnDestroy() { if (_bg != null) Destroy(_bg); }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f2Key.wasPressedThisFrame) _open = !_open;
            if (_undying && runner != null && runner.vitals != null && runner.Life != null && runner.Life.Record == null
                && runner.vitals.Health < runner.vitals.MaxHealth) runner.vitals.Heal(runner.vitals.MaxHealth);
        }

        void OnGUI()
        {
            if (runner == null || runner.Life == null) return;
            ImguiFont.Use();
            float k = Screen.height / 720f;
            if (_btn == null)
            {
                _btn = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleCenter };
                _lbl = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft };
                _head = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold };
            }
            int fs = Mathf.RoundToInt(13f * k);
            _btn.fontSize = fs; _lbl.fontSize = fs; _head.fontSize = Mathf.RoundToInt(14f * k);
            _lbl.normal.textColor = new Color(0.93f, 0.9f, 0.82f); _head.normal.textColor = new Color(0.95f, 0.78f, 0.35f);

            float bw = 62f * k, bh = 28f * k;
            if (GUI.Button(new Rect(8f * k, Screen.height * 0.36f, bw, bh), _open ? "닫기 F2" : "시험 F2", _btn)) _open = !_open;
            if (!_open) return;

            float w = 318f * k, top = Screen.height * 0.36f + bh + 6f * k, h = Screen.height - top - 12f * k;
            var area = new Rect(8f * k, top, w, h);
            GUI.color = new Color(0.08f, 0.07f, 0.06f, 0.86f);
            GUI.DrawTexture(area, _bg);
            GUI.color = Color.white;
            GUILayout.BeginArea(new Rect(area.x + 8f * k, area.y + 6f * k, area.width - 16f * k, area.height - 12f * k));
            _scroll = GUILayout.BeginScrollView(_scroll);
            var life = runner.Life;
            var data = DataBoot.Data;
            var row = GUILayout.Height(26f * k);

            GUILayout.Label($"시험 메뉴 — 개발 판에서만 보인다", _head);
            GUILayout.Label($"{Loc.Realm(life.Scale.Realms[life.Realm])} · Lv {life.Level} · 적 {(runner.enemies != null ? runner.enemies.Count : 0)}", _lbl);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("경지 +1", _btn, row)) runner.GiveXpToNextRealm();
            if (GUILayout.Button("기혈 채움", _btn, row) && runner.vitals != null) runner.vitals.Heal(runner.vitals.MaxHealth);
            if (GUILayout.Button(_undying ? "안 죽기 켬" : "안 죽기 끔", _btn, row)) _undying = !_undying;
            GUILayout.EndHorizontal();
            if (runner.enemies != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"적 목표 {runner.enemies.targetCount}", _lbl, GUILayout.Width(110f * k));
                if (GUILayout.Button("×2", _btn, row)) runner.enemies.targetCount = Mathf.Min(runner.enemies.capacity, runner.enemies.targetCount * 2);
                if (GUILayout.Button("÷2", _btn, row)) runner.enemies.targetCount = Mathf.Max(1, runner.enemies.targetCount / 2);
                if (GUILayout.Button("지도 M", _btn, row) && _mapHud != null) { if (_mapHud.IsOpen) _mapHud.Close(); else _mapHud.Open(); }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6f * k);
            GUILayout.Label("몸 바꾸기 — 그 문파 땅에서 새 생", _head);
            var sects = data.Cheonha.Sects;
            for (int i = 0; i < sects.Count; i += 3)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < Mathf.Min(i + 3, sects.Count); j++)
                    if (GUILayout.Button(Loc.Sect(sects[j]), _btn, row)) SwitchBody(sects[j].Id);
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6f * k);
            GUILayout.Label("무공 — 단계는 이펙트 단계(1~5)", _head);
            int max = data.Balance.Levels.WeaponMax;
            foreach (var wdef in data.Weapons)
            {
                life.Loadout.Weapons.TryGetValue(wdef.Id, out int lv);
                GUILayout.BeginHorizontal();
                string state = lv > 0 ? $"Lv{lv} · {WeaponGrowth.VisualStage(wdef, lv)}단" : "—";
                GUILayout.Label($"{Loc.Weapon(wdef)}  {state}", _lbl, GUILayout.Width(170f * k));
                GUI.enabled = lv < max;
                if (GUILayout.Button(lv > 0 ? "+1" : "익힘", _btn, row)) Raise(wdef.Id, lv + 1);
                if (GUILayout.Button($"Lv{max}", _btn, row)) Raise(wdef.Id, max);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        /// <summary>그 무공을 level 까지. 칸이 모자라면 칸을 하나 늘린다(시험이니).</summary>
        public void Raise(string id, int level)
        {
            var weapons = runner.weapons != null ? runner.weapons : FindFirstObjectByType<WeaponRunner>();
            var lo = runner.Life.Loadout;
            if (weapons == null || lo == null) return;
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
            if (RebirthHandoff.Soul == null) RebirthHandoff.Soul = SoulStore.Load(data);
            RebirthHandoff.ClearBody();
            RebirthHandoff.Body = Rebirth.Body(c, cand);
            Time.timeScale = 1f;
            SceneManager.LoadScene("Run");
        }
    }
}
