using System.Collections.Generic;
using BloodRain.Core.Data;
using BloodRain.Core.Run;
using BloodRain.Core.Spawning;
using BloodRain.Presentation.Hud;
using BloodRain.Sim;
using UnityEngine;

namespace BloodRain.Presentation.Fx
{
    // WP-39U 기연 사건(기획서 20장, synergies.json encounters). 웹 한 생 판(template.html 기연 갱신 줄)과 같은 셈.
    //   판 시작과 1분마다 EncounterRoller.RollAll(ctx) — ctx 의 Chapter·ChapterSchool 은 서 있는 땅(cheonha regions[].chapter·sect).
    //   나온 사건은 플레이어 둘레 고리(은거기인 11m, 영약·속삭임·검총 10m, 유해 9m — 웹 R)에 자리 표시로 놓는다(그림은 art_queue props_encounters 가 오면).
    //   은거기인: 1.7m 안에 머문 초(벗어나면 초당 0.5 씩 줄어듦)가 HermitReady 면 MeetHermit + 깨달음 고르기 한 번 더.
    //   유해: 1.3m 안에서 멈춰 선 초(움직이면 줄어듦)가 RelicReady 면 HonorRelic(그 땅 문파) — 진전 덧셈은 LifeRunner.ApplyStats 가 SchoolExtra 로.
    //   영약: 1.3m 안이면 TakeElixir(오의 가득·음양 0). 속삭임: 석상 3m 안에서 오의를 쓰면 AcceptWhisper, 다가갔다가 8m 밖으로 떠나면 거절.
    //   아직(다음 차례): 영물(달아나는 것)·검총 꽂기 화면·하오문 밀서(다음 보통 등롱). 나오면 로그만 남긴다.
    public sealed class EncounterLayer : MonoBehaviour
    {
        public static EncounterLayer Instance { get; private set; }

        public LifeRunner runner;
        [Tooltip("다시 굴리는 간격(게임 시간 초)")]
        public float rollSeconds = 60f;

        sealed class Spot { public EncounterDef Def; public Vector2 Pos; public GameObject Go; public SpriteRenderer Ring; public float Prog; public bool Near; public string School; }

        readonly List<Spot> _spots = new List<Spot>();
        readonly System.Random _rng = new System.Random();
        EncounterRoller _roller;
        float _acc;
        bool _first = true;
        Vector2 _lastPos;
        Sprite _circle, _square;
        SecretWatch _secrets;

        /// <summary>시험용: 받은(끝난) 사건 id.</summary>
        public readonly List<string> Done = new List<string>();
        /// <summary>시험용: 아직 손대지 않은 사건(영물·검총·밀서)이 나온 것.</summary>
        public readonly List<string> Pending = new List<string>();

        static float Scale => WorldScale.Current != null ? WorldScale.Current.Length(1f) : 1f;
        Life Life => runner != null ? runner.Life : null;
        bool Alive => Life != null && Life.Record == null && !Life.IsPrologue;
        Vector2 PlayerPos => runner != null && runner.player != null ? (Vector2)runner.player.position : Vector2.zero;

        void Awake()
        {
            Instance = this;
            if (runner == null) runner = GetComponent<LifeRunner>();
            _circle = MakeSprite(true);
            _square = MakeSprite(false);
        }

        void Start() => _secrets = FindFirstObjectByType<SecretWatch>();

        void OnDestroy() { if (Instance == this) Instance = null; }

        EncounterContext Context()
        {
            var life = Life;
            var region = life.Regions.Region(life.RegionId);
            return new EncounterContext
            {
                Chapter = region?.Chapter,
                ChapterSchool = region?.Sect,
                Minute = life.Minutes,
                Luck = runner.Stats != null ? runner.Stats.Luck : 0f,
            };
        }

        void Update()
        {
            if (!Alive || Time.timeScale <= 0f) return;
            float dt = Time.deltaTime;
            _roller ??= new EncounterRoller(DataBoot.Data, _rng);
            _acc += dt;
            if (_first || _acc >= rollSeconds)
            {
                _first = false;
                _acc = 0f;
                var ctx = Context();
                foreach (var e in _roller.RollAll(ctx)) Appear(e, ctx.ChapterSchool);
            }

            var me = PlayerPos;
            bool moving = (me - _lastPos).sqrMagnitude > 1e-6f;
            _lastPos = me;
            float sc = Scale;
            var syn = DataBoot.Data.Synergies;
            for (int i = _spots.Count - 1; i >= 0; i--)
            {
                var s = _spots[i];
                float d = Vector2.Distance(me, s.Pos) / sc; // 경지 m
                bool done = false;
                switch (s.Def.Id)
                {
                    case EncounterBoons.Hermit:
                        if (d < 1.7f) s.Prog += dt; else s.Prog = Mathf.Max(0f, s.Prog - dt * 0.5f);
                        SetRing(s, s.Prog / syn.Encounter(EncounterBoons.Hermit).Param("staySeconds"));
                        if (Life.Encounters.HermitReady(s.Prog))
                        {
                            Life.Encounters.MeetHermit();
                            runner.QueueExtraInsight();
                            done = true;
                        }
                        break;
                    case EncounterBoons.Relic:
                        if (d < 1.3f && !moving) s.Prog += dt; else s.Prog = Mathf.Max(0f, s.Prog - dt);
                        SetRing(s, s.Prog / syn.Encounter(EncounterBoons.Relic).Param("bowSeconds"));
                        if (Life.Encounters.RelicReady(s.Prog))
                        {
                            Life.Encounters.HonorRelic(s.School ?? "shaolin");
                            done = true;
                        }
                        break;
                    case EncounterBoons.Elixir:
                        if (d < 1.3f) { Life.TakeElixir(); done = true; }
                        break;
                    case EncounterBoons.Whisper:
                        if (d < 3f)
                        {
                            s.Near = true;
                            if (Life.Ultimate != null && Life.AcceptWhisper()) done = true;
                        }
                        else if (s.Near && d > 8f) { Debug.Log("[EncounterLayer] 혈마의 속삭임 — 석상을 지나쳤다"); Remove(i); continue; }
                        break;
                }
                if (done)
                {
                    Done.Add(s.Def.Id);
                    Debug.Log($"[EncounterLayer] 기연 받음: {s.Def.Id}{(s.School != null ? " (" + s.School + ")" : "")}");
                    if (_secrets != null) _secrets.ShowEncounter(s.Def);
                    Remove(i);
                }
            }
        }

        void Appear(EncounterDef e, string school)
        {
            if (e.Id == EncounterBoons.SpiritBeast || e.Id == EncounterBoons.SwordTomb || e.Id == EncounterBoons.Letter)
            {
                // 아직: 영물(달아나는 것)·검총 꽂기 화면·밀서(다음 보통 등롱)
                Pending.Add(e.Id);
                Debug.Log($"[EncounterLayer] 기연 {e.Id} 나옴 — 아직 붙이지 않은 사건");
                return;
            }
            float R = e.Id == EncounterBoons.Hermit ? 11f : e.Id == EncounterBoons.Relic ? 9f : 10f;
            float a = (float)(_rng.NextDouble() * Mathf.PI * 2f);
            Place(e, PlayerPos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * R * Scale, school);
        }

        /// <summary>시험 메뉴: 그 사건을 굴리지 않고 플레이어 앞 2m 에 바로 놓는다.</summary>
        public bool PlaceNow(string id)
        {
            if (!Alive) return false;
            var e = DataBoot.Data.Synergies.Encounter(id);
            _roller ??= new EncounterRoller(DataBoot.Data, _rng);
            _roller.Force(id);
            var ctx = Context();
            if (id == EncounterBoons.SpiritBeast || id == EncounterBoons.SwordTomb || id == EncounterBoons.Letter) { Appear(e, ctx.ChapterSchool); return false; }
            Place(e, PlayerPos + new Vector2(2f, 0f) * Scale, ctx.ChapterSchool ?? "shaolin");
            return true;
        }

        void Place(EncounterDef e, Vector2 pos, string school)
        {
            var go = new GameObject($"기연 {e.Id}");
            go.transform.position = pos;
            float sc = Scale;
            var body = go.AddComponent<SpriteRenderer>();
            body.sortingOrder = 4;
            switch (e.Id)
            {
                case EncounterBoons.Hermit: body.sprite = _square; body.color = new Color32(0xC9, 0xA4, 0x6A, 255); go.transform.localScale = new Vector3(0.9f, 0.6f, 1f) * sc; break;
                case EncounterBoons.Relic: body.sprite = _square; body.color = new Color32(0x8A, 0x84, 0x78, 255); go.transform.localScale = new Vector3(1.0f, 0.35f, 1f) * sc; break;
                case EncounterBoons.Elixir: body.sprite = _circle; body.color = new Color32(0xC9, 0xA2, 0x27, 200); go.transform.localScale = Vector3.one * 0.9f * sc; break;
                case EncounterBoons.Whisper: body.sprite = _square; body.color = new Color32(0x5A, 0x2A, 0x26, 255); go.transform.localScale = new Vector3(0.7f, 1.3f, 1f) * sc; break;
            }
            // 닿는 원(점선 대신 옅은 원)
            float ring = e.Id == EncounterBoons.Hermit ? 1.7f : e.Id == EncounterBoons.Whisper ? 3f : 1.3f;
            var rg = new GameObject("원");
            rg.transform.SetParent(go.transform, false);
            rg.transform.localScale = new Vector3(ring * 2f * sc / go.transform.localScale.x, ring * 2f * sc / go.transform.localScale.y, 1f);
            var rs = rg.AddComponent<SpriteRenderer>();
            rs.sprite = _circle;
            rs.color = new Color(0.1f, 0.1f, 0.1f, 0.12f);
            rs.sortingOrder = 3;
            _spots.Add(new Spot { Def = e, Pos = pos, Go = go, Ring = rs, School = school });
            Debug.Log($"[EncounterLayer] 기연의 기척: {e.Id} @ {pos}");
        }

        static void SetRing(Spot s, float t)
        {
            // 채워지는 만큼 원이 금빛으로
            t = Mathf.Clamp01(t);
            s.Ring.color = Color.Lerp(new Color(0.1f, 0.1f, 0.1f, 0.12f), new Color(0.79f, 0.64f, 0.15f, 0.45f), t);
        }

        void Remove(int i)
        {
            if (_spots[i].Go != null) Destroy(_spots[i].Go);
            _spots.RemoveAt(i);
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
