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
    //   판 시작에 사건마다 나올 분(EncounterSpots.AppearMinute)을 뽑고, 그 분이 되면 EncounterRoller.Roll(id, ctx) — 형편이 안 맞으면 RetryMinutes 뒤 다시(EN-01U).
    //   ctx 의 Chapter·ChapterSchool 은 서 있는 땅(cheonha regions[].chapter·sect). 거리(고리·닿기·떠나기·줄어듦)는 synergies.json encounters[].spot.
    //   나온 사건은 플레이어 둘레 고리(EncounterSpots.PlaceOffset)에 자리 표시로 놓는다(그림은 art_queue props_encounters 가 오면).
    //   은거기인: 1.7m 안에 머문 초(벗어나면 초당 0.5 씩 줄어듦)가 HermitReady 면 MeetHermit + 깨달음 고르기 한 번 더.
    //   유해: 1.3m 안에서 멈춰 선 초(움직이면 줄어듦)가 RelicReady 면 HonorRelic(그 땅 문파) — 진전 덧셈은 LifeRunner.ApplyStats 가 SchoolExtra 로.
    //   영약: 1.3m 안이면 TakeElixir(오의 가득·음양 0). 속삭임: 석상 3m 안에서 오의를 쓰면 AcceptWhisper, 다가갔다가 8m 밖으로 떠나면 거절.
    //   영물: 금두꺼비(golden_toad)를 이벤트 고리에 불러 그 적 번호를 기억, 잡히면(LifeRunner.OnKilled → TryCatchBeast) CatchSpiritBeast(오의 가득·음양 0·혈정).
    //   검총: 1.5m 안에 서면 SwordTombScreen(가진 무공 + 지나친다) → PlantSword. 무공이 하나뿐이면 '꽂을 검이 이것 하나뿐'. 2.5m 밖으로 벗어나야 다시 뜬다.
    //   하오문 밀서: 다음에 깨는 보통 등롱(LanternLayer.Break)에서 ReadLetter(이번 생 SecretWatch.Found 를 찾은 비전으로) — 비전 이름은 떠오르는 글, 조건은 로그.
    //     장경각 도감 '밀서: 조건' 줄은 도감에 비전 줄이 생긴 뒤(soul.Letters 는 Core 가 이미 남김).
    public sealed class EncounterLayer : MonoBehaviour
    {
        public static EncounterLayer Instance { get; private set; }

        public LifeRunner runner;

        sealed class Spot { public EncounterDef Def; public Vector2 Pos; public GameObject Go; public SpriteRenderer Ring; public float Prog; public bool Near; public string School; }

        readonly List<Spot> _spots = new List<Spot>();
        readonly System.Random _rng = new System.Random();
        EncounterRoller _roller;
        // EN-01U 사건마다 나올 게임 분(판 시작에 EncounterSpots.AppearMinute 로 뽑음). 굴렸으면 지운다
        Dictionary<string, float> _due;
        readonly List<string> _dueKeys = new List<string>();
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
            RollDue();

            // 오의를 쓴 순간: 사자후처럼 한 박자로 끝나는 오의는 Ultimate 가 같은 프레임에 지워질 수 있어 게이지가 비는 것도 본다
            var ul = Life.Ultimate;
            float cv = Life.Charge.Value;
            bool ultNow = ul != null || cv < _lastCharge - Life.Charge.Max * 0.5f;
            _lastCharge = cv;

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
                        s.Prog = EncounterSpots.Progress(s.Def, s.Prog, dt, EncounterSpots.Reached(s.Def, d));
                        SetRing(s, s.Prog / syn.Encounter(EncounterBoons.Hermit).Param("staySeconds"));
                        if (Life.Encounters.HermitReady(s.Prog))
                        {
                            Life.Encounters.MeetHermit();
                            runner.QueueExtraInsight();
                            done = true;
                        }
                        break;
                    case EncounterBoons.Relic:
                        s.Prog = EncounterSpots.Progress(s.Def, s.Prog, dt, EncounterSpots.Reached(s.Def, d) && !moving);
                        SetRing(s, s.Prog / syn.Encounter(EncounterBoons.Relic).Param("bowSeconds"));
                        if (Life.Encounters.RelicReady(s.Prog))
                        {
                            Life.Encounters.HonorRelic(s.School ?? "shaolin");
                            done = true;
                        }
                        break;
                    case EncounterBoons.Elixir:
                        if (EncounterSpots.Reached(s.Def, d)) { Life.TakeElixir(); done = true; }
                        break;
                    case EncounterBoons.Whisper:
                        if (EncounterSpots.Reached(s.Def, d))
                        {
                            s.Near = true;
                            if (ultNow && Life.AcceptWhisper()) done = true;
                        }
                        else if (s.Near && EncounterSpots.Left(s.Def, d)) { Debug.Log("[EncounterLayer] 혈마의 속삭임 — 석상을 지나쳤다"); Remove(i); continue; }
                        break;
                    case EncounterBoons.SwordTomb:
                        // 무덤 1.5m 안에 서면 가진 무공 카드에서 골라 꽂는다. 지나쳤으면 2.5m 밖으로 벗어났다 와야 다시 뜬다(웹과 같음)
                        if (EncounterSpots.Left(s.Def, d)) s.Near = false;
                        else if (EncounterSpots.Reached(s.Def, d) && !s.Near && !TombOpen)
                        {
                            s.Near = true;
                            if (EncounterBoons.CanPlantSword(Life.Loadout, FirstWeapon())) OpenTomb(s);
                            else { runner.ShowPopup(Loc.T("ui.encounter.tomb.only")); Debug.Log("[EncounterLayer] 검총 — 꽂을 검이 이것 하나뿐"); }
                        }
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

        // ── 영물 · 검총 · 하오문 밀서 ──
        readonly HashSet<int> _beasts = new HashSet<int>();
        SwordTombScreen _tomb;
        float _lastCharge;

        /// <summary>하오문 밀서가 나와 다음 보통 등롱을 기다리는 중.</summary>
        public bool LetterArmed { get; private set; }
        bool TombOpen => _tomb != null && _tomb.IsOpen;

        string FirstWeapon()
        {
            foreach (var k in Life.Loadout.Weapons.Keys) return k;
            return "";
        }

        /// <summary>LifeRunner.OnKilled 가 부른다. 영물로 부른 적이면 잡은 셈을 하고 true(금두꺼비 수에 안 넣게).</summary>
        public bool TryCatchBeast(int agentId, Vector2 at)
        {
            if (!_beasts.Remove(agentId) || !Alive) return false;
            int crystal = Life.CatchSpiritBeast();
            var def = DataBoot.Data.Synergies.Encounter(EncounterBoons.SpiritBeast);
            Done.Add(def.Id);
            Debug.Log($"[EncounterLayer] 기연 받음: spiritbeast (혈정 +{crystal}, 오의 {Life.Charge.Value:0}/{Life.Charge.Max:0}, 음양 {Life.Loadout.Gauge.Value:0.##})");
            if (_secrets != null) _secrets.ShowEncounter(def);
            runner.ShowPopup(Loc.F("ui.encounter.beast", crystal), at);
            return true;
        }

        void OpenTomb(Spot s)
        {
            if (_tomb == null)
            {
                _tomb = new GameObject("검총 화면").AddComponent<SwordTombScreen>();
                var lu = FindFirstObjectByType<LevelUpScreen>();
                if (lu != null) _tomb.font = lu.font;
            }
            float per = s.Def.Param("mightPerLevel");
            _tomb.Open(DataBoot.Data, Life.Loadout, per, id =>
            {
                if (id == null || !Alive || !EncounterBoons.CanPlantSword(Life.Loadout, id)) { Debug.Log("[EncounterLayer] 검총 — 지나쳤다"); return; }
                float bonus = Life.Encounters.PlantSword(Life.Loadout, id);
                Done.Add(s.Def.Id);
                Debug.Log($"[EncounterLayer] 기연 받음: sword_tomb — {id} 꽂음, 공력 +{bonus * 100f:0}% (모두 +{Life.Encounters.WeaponMightBonus * 100f:0}%)");
                if (_secrets != null) _secrets.ShowEncounter(s.Def);
                int i = _spots.IndexOf(s);
                if (i >= 0) Remove(i);
            });
        }

        /// <summary>LanternLayer 가 보통 등롱을 깰 때 부른다. 밀서를 펴 아직 못 찾은 비전 하나의 조건을 보인다.</summary>
        public void ReadLetter(Vector2 at)
        {
            if (!LetterArmed || !Alive) return;
            LetterArmed = false;
            var found = new List<string>();
            if (_secrets != null) foreach (var x in _secrets.Found) found.Add(x.Id);
            var secret = Life.ReadLetter(found);
            var def = DataBoot.Data.Synergies.Encounter(EncounterBoons.Letter);
            Done.Add(def.Id);
            if (_secrets != null) _secrets.ShowEncounter(def);
            if (secret == null)
            {
                runner.ShowPopup(Loc.T("ui.encounter.letter_blank"), at);
                Debug.Log("[EncounterLayer] 하오문 밀서 — 빈 종이");
                return;
            }
            string cond = Loc.T($"synergies.secrets.{secret.Id}.condition", secret.Condition);
            runner.ShowPopup(Loc.F("ui.encounter.letter", Loc.T($"synergies.secrets.{secret.Id}.name", secret.NameKo)), at);
            Debug.Log($"[EncounterLayer] 하오문 밀서 — {secret.NameKo}({secret.Id}): {cond}");
        }

        void AddBlades(Transform parent)
        {
            // 흙무더기에 비스듬히 꽂힌 검 다섯(그림 전 자리 표시, 웹 sword_tomb 그리기와 같은 뜻)
            for (int i = 0; i < 5; i++)
            {
                var b = new GameObject("검");
                b.transform.SetParent(parent, false);
                b.transform.localPosition = new Vector3(-0.36f + i * 0.18f, 0.55f, 0f);
                b.transform.localRotation = Quaternion.Euler(0, 0, (i - 2) * 9f + (i % 2 == 0 ? 4f : -6f));
                b.transform.localScale = new Vector3(0.05f, 1.3f, 1f);
                var r = b.AddComponent<SpriteRenderer>();
                r.sprite = _square;
                r.color = new Color32(0xA8, 0xAD, 0xB2, 255);
                r.sortingOrder = 5;
            }
        }

        void Appear(EncounterDef e, string school)
        {
            if (e.Id == EncounterBoons.SpiritBeast)
            {
                // 영물: 금두꺼비처럼 달아나는 것(웹 spawnSpiritBeast). 잡으면 LifeRunner.OnKilled → TryCatchBeast
                int id = runner.SpawnSpiritBeast();
                if (id >= 0) _beasts.Add(id);
                Debug.Log($"[EncounterLayer] 영물이 달아난다 (적 #{id})");
                return;
            }
            if (e.Id == EncounterBoons.Letter)
            {
                // 하오문 밀서: 다음에 깨는 보통 등롱 속에 쪽지(웹 letterArmed)
                LetterArmed = true;
                Debug.Log("[EncounterLayer] 하오문 밀서 — 다음 보통 등롱 속에");
                return;
            }
            var (ox, oy) = EncounterSpots.PlaceOffset(e, _rng);
            Place(e, PlayerPos + new Vector2(ox, oy) * Scale, school);
        }

        /// <summary>EN-01U 나올 분이 된 사건을 굴린다. 형편이 안 맞으면 RetryMinutes 뒤 다시(웹 newEnc·ev.at).</summary>
        void RollDue()
        {
            var syn = DataBoot.Data.Synergies;
            if (_due == null)
            {
                _due = new Dictionary<string, float>();
                foreach (var e in syn.Encounters) _due[e.Id] = EncounterSpots.AppearMinute(e, _rng);
                var plan = new System.Text.StringBuilder();
                foreach (var kv in _due) plan.Append($" {kv.Key} {kv.Value:0.0}분");
                Debug.Log($"[EncounterLayer] 기연 나올 때:{plan}");
            }
            if (_due.Count == 0) return;
            float now = Life.Minutes;
            _dueKeys.Clear();
            foreach (var kv in _due) if (now >= kv.Value) _dueKeys.Add(kv.Key);
            if (_dueKeys.Count == 0) return;
            var ctx = Context();
            foreach (var id in _dueKeys)
            {
                var e = syn.Encounter(id);
                if (_roller.HasRolled(id)) { _due.Remove(id); continue; } // 시험 메뉴·비전이 이미 놓음
                if (!EncounterRoller.CanAppear(e, ctx)) { _due[id] = now + EncounterSpots.RetryMinutes; continue; }
                _due.Remove(id);
                bool hit = _roller.Roll(id, ctx);
                Debug.Log($"[EncounterLayer] {now:0.0}분 {id} 굴림 → {(hit ? "나옴" : "안 나옴")}");
                if (hit) Appear(e, ctx.ChapterSchool);
            }
        }

        /// <summary>시험 메뉴: 그 사건을 굴리지 않고 플레이어 앞 2m 에 바로 놓는다.</summary>
        public bool PlaceNow(string id)
        {
            if (!Alive) return false;
            var e = DataBoot.Data.Synergies.Encounter(id);
            _roller ??= new EncounterRoller(DataBoot.Data, _rng);
            _roller.Force(id);
            var ctx = Context();
            if (id == EncounterBoons.SpiritBeast || id == EncounterBoons.Letter) { Appear(e, ctx.ChapterSchool); return true; }
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
                case EncounterBoons.SwordTomb: body.sprite = _circle; body.color = new Color32(0x6B, 0x5A, 0x44, 255); go.transform.localScale = new Vector3(1.1f, 0.55f, 1f) * sc; AddBlades(go.transform); break;
            }
            // 닿는 원(점선 대신 옅은 원)
            float ring = e.Spot != null && e.Spot.ReachRadius > 0f ? e.Spot.ReachRadius : 1.3f;
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
