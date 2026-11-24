using System.Collections.Generic;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using BloodRain.Presentation.Hud;
using BloodRain.Sim;
using UnityEngine;

namespace BloodRain.Presentation.Fx
{
    // GU-10U ① 떨어진 문파 병기(기획서 8장 '문파 병기와 6단', gear.json, docs/design/sinbyeong.md).
    //   적이 쓰러지면 Life.RollGearDrop(그 문파 땅의 droppers·guaranteed, 기연은 StatSheet.Luck) — 떨어지면 그 자리에 병기 하나.
    //   땅의 주인 처치 결과 BossKill.GearDrop(유품)은 PatternRunner 가 DropAt 으로 놓는다.
    //   그림: 병기 원화가 아직 없어(art_queue) 문파 상징색 막대를 땅에 비스듬히 꽂고, 그 위로 문파 빛 기둥(위로 갈수록 옅게, 숨쉬듯).
    //   줍기: 몸이 등롱 줍기 반경(balance drops.lanternSpawn) 안에 들면 Life.PickGear. '금강 염주 · 범병' 한 줄, 같은 병기면 '양병으로'.
    //   유품이거나, 쥔 것과 다른 병기인데 쥔 것이 범병보다 높으면 저절로 줍지 않고 '바꿔 쥘까?'(예 Y / 아니오 N) — 아니오면 몸이 떨어질 때까지 다시 묻지 않는다.
    //   글은 IMGUI(LanternLayer 와 같은 금빛 글). Text Animator 로 바꾸는 것은 PC 에셋이라 나중에.
    public sealed class GearDropLayer : MonoBehaviour
    {
        public static GearDropLayer Instance { get; private set; }

        public LifeRunner runner;

        sealed class Dropped { public Vector2 Pos; public string GearId; public bool Relic; public GameObject Go; public SpriteRenderer Glow; public float Age; public bool Declined; }

        readonly List<Dropped> _drops = new List<Dropped>();
        readonly List<(string text, float born)> _notes = new List<(string, float)>();
        readonly System.Random _rng = new System.Random();
        Dropped _asking;
        Sprite _bar, _pillar, _shade;
        GUIStyle _style, _askStyle, _btnStyle;

        /// <summary>시험용: 떨어진 수, 주운 수.</summary>
        public int DroppedCount { get; private set; }
        public int PickedCount { get; private set; }
        public int OnGround => _drops.Count;
        public bool Asking => _asking != null;

        static float Scale => WorldScale.Current != null ? WorldScale.Current.Length(1f) : 1f;
        bool Alive => runner != null && runner.Life != null && runner.Life.Record == null;
        Vector2 PlayerPos => runner != null && runner.player != null ? (Vector2)runner.player.position : Vector2.zero;

        void Awake()
        {
            Instance = this;
            if (runner == null) runner = GetComponent<LifeRunner>();
            _bar = MakeBar();
            _pillar = MakePillar();
            _shade = MakeShade();
        }

        static Sprite MakeShade()
        {
            const int N = 32;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
        }

        void Start()
        {
            if (runner != null && runner.enemies != null) runner.enemies.Killed += OnKilled;
        }

        void OnDestroy()
        {
            if (runner != null && runner.enemies != null) runner.enemies.Killed -= OnKilled;
            if (Instance == this) Instance = null;
        }

        void OnKilled(Vector2 at, EnemyAgent a)
        {
            if (!Alive || runner.enemies == null) return;
            var def = runner.enemies.DefOf(a.Kind);
            if (def == null || def.Family == "boss") return; // 땅의 주인은 DefeatBoss 결과로(PatternRunner)
            float luck = runner.Stats != null ? runner.Stats.Luck : 0f;
            var id = runner.Life.RollGearDrop(def.Id, luck, _rng);
            if (id != null) DropAt(at, id);
        }

        /// <summary>그 자리에 병기를 놓는다. relic 이면 유품(닿으면 바꿔 쥘지 묻는다).</summary>
        public void DropAt(Vector2 at, string gearId, bool relic = false)
        {
            var def = Gear.Def(DataBoot.Data, gearId);
            Color tint = SchoolTint.OfSchoolId(def.Sect) ?? new Color(0.85f, 0.69f, 0.23f);
            float sc = Scale;
            var root = new GameObject("떨어진 병기 " + def.NameKo);
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(at.x, at.y, 0f);

            var glow = new GameObject("빛 기둥").AddComponent<SpriteRenderer>();
            glow.transform.SetParent(root.transform, false);
            glow.sprite = _pillar;
            glow.sortingOrder = 6;
            glow.transform.localPosition = new Vector3(0f, 1.5f * sc, 0f);
            glow.transform.localScale = new Vector3(1.5f, 0.9f, 1f) * sc; // 1.5m × 3.6m
            glow.color = new Color(tint.r, tint.g, tint.b, 0.7f);

            // 옅은 종이 바닥에서도 자리가 읽히게 발밑에 먹 그늘 한 점
            var shade = new GameObject("그늘").AddComponent<SpriteRenderer>();
            shade.transform.SetParent(root.transform, false);
            shade.sprite = _shade;
            shade.sortingOrder = 5;
            shade.transform.localScale = new Vector3(1.1f, 0.45f, 1f) * sc;
            shade.color = new Color(0.1f, 0.08f, 0.07f, 0.6f);

            var bar = new GameObject("병기").AddComponent<SpriteRenderer>();
            bar.transform.SetParent(root.transform, false);
            bar.sprite = _bar;
            bar.sortingOrder = 7;
            bar.transform.localPosition = new Vector3(0f, 0.5f * sc, 0f);
            bar.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);
            bar.transform.localScale = new Vector3(0.3f, 1.15f, 1f) * sc;
            bar.color = Color.Lerp(tint, new Color(0.1f, 0.08f, 0.07f), 0.35f);

            _drops.Add(new Dropped { Pos = at, GearId = gearId, Relic = relic, Go = root, Glow = glow });
            DroppedCount++;
            Debug.Log($"[GearDropLayer] 떨어짐 {def.NameKo}{(relic ? " (유품)" : "")}");
        }

        void Update()
        {
            if (!Alive) return;
            float dt = Time.deltaTime;
            var me = PlayerPos;
            float sc = Scale;
            var spawn = DataBoot.Data.Balance.Drops.LanternSpawn;
            for (int i = _drops.Count - 1; i >= 0; i--)
            {
                var d = _drops[i];
                d.Age += dt;
                float pulse = 0.68f + 0.17f * Mathf.Sin(Time.unscaledTime * 2.4f + i);
                var c = d.Glow.color; c.a = pulse; d.Glow.color = c;
                float dist = Vector2.Distance(d.Pos, me);
                bool inside = d.Age >= 0.4f && Drops.InPickup(spawn, dist / sc);
                if (!inside)
                {
                    d.Declined = false; // 몸이 떨어지면 다시 물을 수 있게
                    if (_asking == d) _asking = null;
                    continue;
                }
                if (_asking == d || d.Declined || _asking != null) continue;
                if (NeedsAsk(d)) { _asking = d; continue; }
                Take(d);
            }
        }

        bool NeedsAsk(Dropped d)
        {
            var slot = runner.Life.GearSlot;
            if (slot.Empty || slot.GearId == d.GearId) return false;
            return d.Relic || slot.Grade > 0;
        }

        void Take(Dropped d)
        {
            var data = DataBoot.Data;
            var slot = runner.Life.GearSlot;
            bool same = slot.GearId == d.GearId;
            int before = slot.Grade;
            int grade = runner.Life.PickGear(d.GearId);
            _drops.Remove(d);
            if (_asking == d) _asking = null;
            Destroy(d.Go);
            PickedCount++;
            var def = Gear.Def(data, d.GearId);
            string name = Loc.T($"gear.{def.Id}.name", def.NameKo);
            var g = data.Gear.Grades[grade];
            string gname = Loc.T($"gear.grades.{g.Id}.name", g.NameKo);
            Note(Loc.F(same && grade > before ? "ui.gear.graded" : "ui.gear.picked", name, gname));
            Debug.Log($"[GearDropLayer] 주움 {def.NameKo} → {g.NameKo}");
        }

        /// <summary>땅의 주인을 쓰러뜨려 쥔 문파 병기가 한 등급 올랐을 때(PatternRunner).</summary>
        public void NoteGradedUp()
        {
            var data = DataBoot.Data;
            var slot = runner != null && runner.Life != null ? runner.Life.GearSlot : null;
            if (slot == null || slot.Empty) return;
            var def = Gear.Def(data, slot.GearId);
            var g = data.Gear.Grades[slot.Grade];
            Note(Loc.F("ui.gear.graded", Loc.T($"gear.{def.Id}.name", def.NameKo), Loc.T($"gear.grades.{g.Id}.name", g.NameKo)));
        }

        void Note(string text) => _notes.Add((text, Time.unscaledTime));

        void Answer(bool yes)
        {
            var d = _asking;
            if (d == null) return;
            _asking = null;
            if (yes) Take(d); else d.Declined = true;
        }

        void OnGUI()
        {
            ImguiFont.Use();
            float k = Screen.height / 1080f;
            if (_notes.Count > 0)
            {
                _style ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                _style.fontSize = ImguiFont.Px(34f);
                for (int i = _notes.Count - 1; i >= 0; i--)
                {
                    var (text, born) = _notes[i];
                    float t = (Time.unscaledTime - born) / 2.2f;
                    if (t >= 1f) { _notes.RemoveAt(i); continue; }
                    float a = t < 0.1f ? t / 0.1f : 1f - Mathf.Pow(Mathf.Max(0f, t - 0.6f) / 0.4f, 2f);
                    float y = Screen.height * 0.30f - (t * 18f + i * 46f) * k;
                    ImguiFont.OutlinedLabel(new Rect(0f, y, Screen.width, 50f * k), text, _style,
                        new Color(0.96f, 0.86f, 0.52f, a), new Color(0.1f, 0.08f, 0.07f, 0.9f * a), 2f * Mathf.Max(1f, k));
                }
            }
            if (_asking == null) return;
            var e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Y || e.keyCode == KeyCode.Return) { Answer(true); e.Use(); return; }
                if (e.keyCode == KeyCode.N || e.keyCode == KeyCode.Escape) { Answer(false); e.Use(); return; }
            }
            var def = Gear.Def(DataBoot.Data, _asking.GearId);
            string q = Loc.F("ui.gear.swap", Loc.T($"gear.{def.Id}.name", def.NameKo));
            _askStyle ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _askStyle.fontSize = ImguiFont.Px(30f);
            _btnStyle ??= new GUIStyle(GUI.skin.button);
            _btnStyle.fontSize = ImguiFont.Px(24f);
            float w = 520f * k, h = 130f * k;
            var box = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.66f, w, h);
            GUI.color = new Color(0.1f, 0.08f, 0.07f, 0.78f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.white;
            ImguiFont.OutlinedLabel(new Rect(box.x, box.y + 10f * k, w, 50f * k), q, _askStyle,
                new Color(0.96f, 0.86f, 0.52f), new Color(0.1f, 0.08f, 0.07f, 0.9f), 1.6f * Mathf.Max(1f, k));
            float bw = 170f * k, bh = 46f * k, by = box.y + h - bh - 14f * k;
            if (GUI.Button(new Rect(box.center.x - bw - 10f * k, by, bw, bh), Loc.T("ui.gear.yes") + " (Y)", _btnStyle)) Answer(true);
            if (GUI.Button(new Rect(box.center.x + 10f * k, by, bw, bh), Loc.T("ui.gear.no") + " (N)", _btnStyle)) Answer(false);
        }

        // 병기 막대: 끝이 좁아지는 세로 막대(위가 날·끝, 아래 한 뼘은 자루처럼 짙게)
        static Sprite MakeBar()
        {
            const int W = 16, H = 64;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float v = (y + 0.5f) / H;
                    float half = v > 0.85f ? Mathf.Lerp(0.5f, 0.05f, (v - 0.85f) / 0.15f) : 0.5f;
                    float dx = Mathf.Abs((x + 0.5f) / W - 0.5f);
                    float a = Mathf.Clamp01((half - dx) * W);
                    byte l = (byte)(v < 0.22f ? 150 : 255);
                    px[y * W + x] = new Color32(l, l, l, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), H);
        }

        // 빛 기둥: 가로는 가운데가 짙은 종 모양, 세로는 밑에서 위로 옅어짐
        static Sprite MakePillar()
        {
            const int W = 32, H = 128;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W * 2f - 1f, v = (y + 0.5f) / H;
                    float across = Mathf.Exp(-u * u * 5f) - Mathf.Exp(-5f);
                    float up = Mathf.Pow(1f - v, 1.6f) * Mathf.Clamp01(v * 8f);
                    px[y * W + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(across * up) * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), W); // 가로 1 · 세로 4 단위
        }
    }
}
