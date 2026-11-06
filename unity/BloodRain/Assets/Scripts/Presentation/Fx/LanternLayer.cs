using System.Collections.Generic;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using BloodRain.Presentation.Hud;
using BloodRain.Presentation.Meta;
using BloodRain.Sim;
using Unity.Mathematics;
using UnityEngine;

namespace BloodRain.Presentation.Fx
{
    // C-09b 등롱과 바닥에 떨어지는 것(기획서 8장 '드롭', balance.json drops). 웹 프로토타입 breakLantern·드롭 줍기와 같은 셈.
    //   등롱: 22초 × 1.5 마다(살아 있는 등롱 넷 미만일 때) 플레이어 둘레 5~9m(경지 m)에 하나. 무공이 친 자리(WeaponRunner 가 Strike 로 알림) 안이면 깨진다.
    //   깨지면 Drops.RollLantern 하나를 그 자리에 떨군다. 잡몹이 쓰러지면 Drops.EnemyDropsSilver 로 은자 주머니.
    //   줍기(플레이어 1m 안): 만두·오리구이 = Drops.FoodHeal(규칙, 종류, 1 + 음식 회복) — 마교·속삭임 배율은 PlayerVitals.Heal 이 곱한다.
    //     취기부 = 화면 안 진기 구슬 끌기(보부상 취기부와 같은 길), 정신부 = 화면 안 적을 JeongsinFreezeSeconds × 곤륜 진전 배율 동안 멈춤,
    //     은자 주머니 = RollSilverBag × (1 + 탐욕) → Life.GainSilver(혈월 배율은 Life 가). 음식은 LifeLog.EatFood·RunRecord.EatFood, 정신부는 UseTalisman 둘 다.
    //   은빛 등롱(QA-02U·R4-08U): 생성 디렉터의 silver_lantern 사건에 PlaceSilver. LifeLog.ManuscriptsHere(혈월 천하)일 때만 화면 안 가까이 놓고,
    //     깨면 LifeLog.BreakSilverLantern(cycle.bloodmoon.manuscriptChance)으로 의서 한 권 + 낙관(DiscoveryFx), 비전 판정엔 RunRecord.BreakSilverLantern. 보통 드롭도 하나 떨군다(웹과 같음).
    //   그림은 아직 없어(art_queue) 색 동그라미·네모로만 그린다.
    public sealed class LanternLayer : MonoBehaviour
    {
        public static LanternLayer Instance { get; private set; }

        public LifeRunner runner;
        [Tooltip("등롱이 새로 서는 간격(초). 웹 22 / timeScale × 1.5")]
        public float spawnSeconds = 33f;
        public int maxAlive = 4;
        [Tooltip("줍는 거리(경지 m)")]
        public float pickupMeters = 1f;

        sealed class Lantern { public Vector2 Pos; public GameObject Go; public bool Silver; }
        sealed class Drop { public Vector2 Pos; public string Kind; public GameObject Go; public float Age; }

        readonly List<Lantern> _lanterns = new List<Lantern>();
        readonly List<Drop> _drops = new List<Drop>();
        readonly List<int> _near = new List<int>();
        readonly System.Random _rng = new System.Random();
        readonly List<(Vector2 at, string text, float born)> _popups = new List<(Vector2, string, float)>();
        float _acc;
        Sprite _circle, _square;
        GUIStyle _style;
        SecretWatch _secrets;

        /// <summary>시험용: 깬 등롱 수, 종류별로 주운 수.</summary>
        public int Broken { get; private set; }
        public readonly Dictionary<string, int> Picked = new Dictionary<string, int>();

        static float Scale => WorldScale.Current != null ? WorldScale.Current.Length(1f) : 1f;
        DropsDef Rule => DataBoot.Data.Balance.Drops;

        void Awake()
        {
            Instance = this;
            if (runner == null) runner = GetComponent<LifeRunner>();
            _circle = MakeSprite(true);
            _square = MakeSprite(false);
        }

        void Start()
        {
            if (runner != null && runner.enemies != null) runner.enemies.Killed += OnKilled;
            _secrets = FindFirstObjectByType<SecretWatch>();
        }

        void OnDestroy()
        {
            if (runner != null && runner.enemies != null) runner.enemies.Killed -= OnKilled;
            if (Instance == this) Instance = null;
        }

        /// <summary>무공이 이 원 안을 쳤다(WeaponRunner). 안에 든 등롱은 깨진다. 웹 hitLanterns 처럼 등롱 반경 0.4m 를 더한다.</summary>
        public static void Strike(Vector2 center, float radius)
        {
            var l = Instance;
            if (l == null || l._lanterns.Count == 0) return;
            float r = radius + 0.4f * Scale;
            for (int i = l._lanterns.Count - 1; i >= 0; i--)
                if ((l._lanterns[i].Pos - center).sqrMagnitude <= r * r) l.Break(i);
        }

        /// <summary>시험 메뉴: 플레이어 둘레에 등롱 n 개를 고리로 놓는다.</summary>
        public void PlaceRing(int n, float realmMeters = 2f)
        {
            var p = PlayerPos;
            for (int k = 0; k < n; k++)
            {
                float a = k * Mathf.PI * 2f / n;
                Place(p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * realmMeters * Scale);
            }
        }

        Vector2 PlayerPos => runner != null && runner.player != null ? (Vector2)runner.player.position : Vector2.zero;
        bool Alive => runner != null && runner.Life != null && runner.Life.Record == null;

        void Update()
        {
            if (!Alive || Time.timeScale <= 0f) return;
            float dt = Time.deltaTime;
            _acc += dt;
            if (_acc >= spawnSeconds && _lanterns.Count < maxAlive)
            {
                _acc = 0f;
                float a = (float)(_rng.NextDouble() * Mathf.PI * 2f), R = Mathf.Lerp(5f, 9f, (float)_rng.NextDouble()) * Scale;
                Place(PlayerPos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * R);
            }
            var me = PlayerPos;
            float pick = pickupMeters * Scale;
            for (int i = _drops.Count - 1; i >= 0; i--)
            {
                var d = _drops[i];
                d.Age += dt;
                // 떨어진 뒤 0.3초는 그대로(깨진 자리에서 보이게), 그 뒤 닿으면 줍는다
                if (d.Age < 0.3f || (d.Pos - me).sqrMagnitude > pick * pick) continue;
                Destroy(d.Go);
                _drops.RemoveAt(i);
                PickUp(d.Kind, d.Pos);
            }
        }

        void OnKilled(Vector2 at, EnemyAgent a)
        {
            if (!Alive) return;
            var def = runner.enemies.DefOf(a.Kind);
            if (def == null || def.Family == "elite" || def.Family == "boss") return; // 잡몹만
            if (Drops.EnemyDropsSilver(Rule, _rng)) AddDrop(at, Drops.SilverBag);
        }

        void Place(Vector2 at, bool silver = false)
        {
            float size = silver ? 1.3f : 1f;
            var go = Make(silver ? "은빛 등롱" : "등롱", at, _circle, silver ? new Color(0.79f, 0.81f, 0.84f) : new Color(0.78f, 0.22f, 0.17f), 0.9f * size, 6);
            var rim = Make("테", at, _circle, silver ? new Color(0.95f, 0.96f, 1f) : new Color(0.85f, 0.69f, 0.23f), 1.05f * size, 5);
            rim.transform.SetParent(go.transform, true);
            _lanterns.Add(new Lantern { Pos = at, Go = go, Silver = silver });
        }

        /// <summary>은빛 등롱 사건(생성 디렉터). 의서가 나오는 생(혈월 천하)이 아니면 놓지 않고 false. 웹처럼 화면 대각선 0.28 거리.</summary>
        public bool PlaceSilver()
        {
            if (!Alive || !runner.Life.Log.ManuscriptsHere) return false;
            var cam = Camera.main;
            float diag = cam != null ? Mathf.Sqrt(Mathf.Pow(cam.orthographicSize * 2f * cam.aspect, 2f) + Mathf.Pow(cam.orthographicSize * 2f, 2f)) : 20f;
            float a = (float)(_rng.NextDouble() * Mathf.PI * 2f);
            var at = PlayerPos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * diag * 0.28f;
            Place(at, true);
            Pop(at, Loc.T("ui.drop.silver_lantern"));
            return true;
        }

        /// <summary>시험용: 은빛 등롱에서 나온 의서 수(이번 생).</summary>
        public int ManuscriptsFound { get; private set; }

        void Break(int i)
        {
            var l = _lanterns[i];
            _lanterns.RemoveAt(i);
            Destroy(l.Go);
            Broken++;
            if (l.Silver) BreakSilver(l.Pos);
            else if (EncounterLayer.Instance != null && EncounterLayer.Instance.LetterArmed) EncounterLayer.Instance.ReadLetter(l.Pos); // WP-39U 하오문 밀서
            AddDrop(l.Pos, Drops.RollLantern(Rule, _rng));
        }

        void BreakSilver(Vector2 at)
        {
            var life = runner.Life;
            _secrets?.Record?.BreakSilverLantern();
            _secrets?.Poke();
            if (!life.Log.BreakSilverLantern(_rng)) return;
            ManuscriptsFound++;
            int total = (RebirthHandoff.Soul != null ? RebirthHandoff.Soul.Manuscripts : 0) + life.Log.Manuscripts;
            var fx = FindFirstObjectByType<DiscoveryFx>();
            if (fx != null) fx.Show(DiscoveryFx.Kind.Encounter, Loc.T("ui.drop.manuscript"), "醫書");
            Pop(at, Loc.F("ui.drop.manuscript_count", total));
            Debug.Log($"[LanternLayer] 의서 한 권 · 모두 {total}권");
        }

        void AddDrop(Vector2 at, string kind)
        {
            bool talisman = kind == Drops.ChwigiTalisman || kind == Drops.JeongsinTalisman;
            Color c = kind switch
            {
                Drops.Bun => new Color(0.94f, 0.90f, 0.82f),
                Drops.Duck => new Color(0.62f, 0.36f, 0.16f),
                Drops.ChwigiTalisman => new Color(0.85f, 0.69f, 0.23f),
                Drops.JeongsinTalisman => new Color(0.95f, 0.80f, 0.30f),
                _ => new Color(0.79f, 0.81f, 0.84f),
            };
            var go = Make(kind, at, talisman ? _square : _circle, c, talisman ? 0.55f : 0.5f, 7);
            if (talisman) go.transform.localScale = new Vector3(0.45f, 0.8f, 1f) * Scale;
            _drops.Add(new Drop { Pos = at, Kind = kind, Go = go });
        }

        void PickUp(string kind, Vector2 at)
        {
            Picked[kind] = Picked.TryGetValue(kind, out var n) ? n + 1 : 1;
            var life = runner.Life;
            var st = runner.Stats;
            var vitals = runner.vitals;
            switch (kind)
            {
                case Drops.Bun:
                case Drops.Duck:
                {
                    float foodHeal = 1f + (st != null ? st.Bonus[StatKind.FoodHeal] : 0f);
                    float got = vitals != null ? vitals.Heal(Drops.FoodHeal(Rule, kind, foodHeal)) : 0f;
                    life.Log.EatFood();
                    _secrets?.Record?.EatFood();
                    Pop(at, Loc.F(kind == Drops.Bun ? "ui.drop.bun" : "ui.drop.duck", Mathf.RoundToInt(got)));
                    break;
                }
                case Drops.ChwigiTalisman:
                    if (runner.orbs != null && runner.orbs.Field != null) runner.orbs.Field.PullAll();
                    Pop(at, Loc.T("ui.drop.chwigi"));
                    break;
                case Drops.JeongsinTalisman:
                {
                    float mul = runner.weapons != null ? runner.weapons.effects.FreezeMultiplier : 1f;
                    float secs = Rule.JeongsinFreezeSeconds * mul;
                    int frozen = FreezeOnScreen(secs);
                    life.Log.UseTalisman();
                    _secrets?.Record?.UseTalisman();
                    Pop(at, Loc.F("ui.drop.jeongsin", secs.ToString("0.#")));
                    Debug.Log($"[LanternLayer] 정신부 {secs:0.#}초 · 멈춘 적 {frozen}");
                    break;
                }
                case Drops.SilverBag:
                {
                    float greed = st != null ? st.Bonus[StatKind.Greed] : 0f;
                    int got = life.GainSilver(Mathf.RoundToInt(Drops.RollSilverBag(Rule, _rng) * (1f + greed)));
                    Pop(at, Loc.F("ui.drop.silver", got));
                    break;
                }
            }
        }

        int FreezeOnScreen(float seconds)
        {
            var cam = Camera.main;
            var field = runner.enemies;
            if (cam == null || field == null) return 0;
            float h = cam.orthographicSize, w = h * cam.aspect;
            Vector2 c = cam.transform.position;
            field.Sim.QueryCircle(new float2(c.x, c.y), Mathf.Sqrt(w * w + h * h), _near);
            var agents = field.Sim.Agents;
            for (int i = _near.Count - 1; i >= 0; i--)
            {
                var p = agents[_near[i]].Pos;
                if (Mathf.Abs(p.x - c.x) > w || Mathf.Abs(p.y - c.y) > h) _near.RemoveAt(i);
            }
            return field.Freeze(_near, seconds);
        }

        void Pop(Vector2 at, string text) => _popups.Add((at, text, Time.unscaledTime));

        void OnGUI()
        {
            if (_popups.Count == 0) return;
            var cam = Camera.main;
            if (cam == null) return;
            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            for (int i = _popups.Count - 1; i >= 0; i--)
            {
                var (at, text, born) = _popups[i];
                float t = (Time.unscaledTime - born) / 1.4f;
                if (t >= 1f) { _popups.RemoveAt(i); continue; }
                var sp = cam.WorldToScreenPoint(at);
                GUI.color = new Color(0.94f, 0.88f, 0.70f, 1f - t * t);
                GUI.Label(new Rect(sp.x - 150f, Screen.height - sp.y - 40f - t * 40f, 300f, 30f), text, _style);
            }
            GUI.color = Color.white;
        }

        GameObject Make(string name, Vector2 at, Sprite sprite, Color color, float realmMeters, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(at.x, at.y, 0f);
            go.transform.localScale = Vector3.one * realmMeters * Scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return go;
        }

        static Sprite MakeSprite(bool circle)
        {
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float a = circle ? Mathf.Clamp01((1f - Mathf.Sqrt(dx * dx + dy * dy)) * N * 0.5f) : 1f;
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
        }
    }
}
