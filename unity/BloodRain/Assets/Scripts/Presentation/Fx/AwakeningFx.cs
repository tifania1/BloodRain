using System.Collections;
using System.Collections.Generic;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using TMPro;
using UnityEngine;

namespace BloodRain.Presentation.Fx
{
    // 절기 각성 연출(기획서 8장 '절기 그릇과 각성', docs/design/vfx.md '절기 각성 연출').
    // 시각표와 판정은 Core Awakening 이 갖고, 여기는 장면 신호(Cue)를 받아 그리기만 한다.
    // 연출 시계는 실제 시간(unscaledDeltaTime), 세상 시간은 Core 의 WorldTimeScale 을 Time.timeScale 에 넣는다.
    // 흑백 구멍은 URP Full Screen Pass(BloodRain/AwakenHole)가 전역 변수로 받는다.
    public sealed class AwakeningFx : MonoBehaviour
    {
        [SerializeField] Camera cam;
        [Tooltip("흑백 효과를 안 받는 겹 카메라(금빛 균열·이름·낙관). 없으면 주 카메라에 그린다")]
        [SerializeField] Camera overlayCam;
        public const string OverlayLayerName = "AwakenOverlay";
        [SerializeField] TMP_FontAsset nameFont;
        [Tooltip("설정의 화면 흔들림 100·50·0%")]
        [Range(0f, 1f)] [SerializeField] float shakeSetting = 1f;
        [Tooltip("기기의 '움직임 줄이기'. 켜지면 흔들림 0, 탁본 뒤집기 건너뜀")]
        [SerializeField] bool reduceMotion;
        [SerializeField] int sortingOrder = 500;
        [Tooltip("검수용. 연출 시계를 이만큼 느리게 돌린다(1이 실제 속도)")]
        [SerializeField] float clockScale = 1f;

        float Dt => Time.unscaledDeltaTime * clockScale;
        public float ClockScale { get => clockScale; set => clockScale = value; }

        static readonly int IdCenter = Shader.PropertyToID("_AwakenCenter");
        static readonly int IdRadius = Shader.PropertyToID("_AwakenRadius");
        static readonly int IdAmount = Shader.PropertyToID("_AwakenAmount");
        static readonly int IdSoft = Shader.PropertyToID("_AwakenSoft");
        static readonly int IdVignette = Shader.PropertyToID("_AwakenVignette");
        static readonly int IdInvert = Shader.PropertyToID("_AwakenInvert");

        static readonly Color Ink = new Color(0.08f, 0.07f, 0.06f, 1f);
        static readonly Color Gold = new Color(1f, 0.82f, 0.38f, 1f);
        static readonly Color Vermilion = new Color32(0xB3, 0x26, 0x1E, 0xFF);
        static readonly Color Paper = new Color(0.96f, 0.94f, 0.88f, 1f);

        // 시각 상수(초, 실제 시간). 웹 프로토타입 #awaken 에 맞춘 연출 값이고 판정 숫자는 아니다.
        const float HoleRadius = 0.11f;
        const float ZoomIn = 0.86f;         // 16% 확대
        const float ZoomInSeconds = 0.35f;
        const float ShockSpreadSeconds = 0.45f;
        const float GlyphFlySeconds = 0.4f;
        const float CharStagger = 0.11f;    // 글자마다 0.11초 차
        const float CharStroke = 0.22f;
        const float StampSeconds = 0.12f;
        const float FlipSeconds = 0.08f;
        const int InkGrainCount = 70;

        Awakening _aw;
        Transform _target;
        Coroutine _run;
        float _baseOrtho;
        Vector3 _camRest;
        float _trauma;
        float _shockAt = -1f;
        float _flipUntil = -1f;
        readonly List<GameObject> _spawned = new List<GameObject>();
        static Sprite _dot, _square;

        public bool IsPlaying => _run != null;
        public Awakening Core => _aw;

        void Awake()
        {
            if (cam == null) cam = Camera.main;
            ResetGlobals();
        }

        void OnDisable()
        {
            ResetGlobals();
            if (_run != null) { Time.timeScale = 1f; _run = null; }
        }

        static void ResetGlobals()
        {
            Shader.SetGlobalFloat(IdAmount, 0f);
            Shader.SetGlobalFloat(IdInvert, 0f);
        }

        /// <summary>각성 연출을 시작한다. forceLong 은 시연·서장처럼 늘 길게 보여 줄 때.</summary>
        public void Play(EvolutionDef evolution, Transform target, bool firstOnAccount,
            AwakeningLength setting = AwakeningLength.Auto, bool? forceLong = null)
        {
            if (_run != null) return;
            _aw ??= new Awakening(DataBoot.Data.Balance.Awakening);
            _target = target;
            _aw.Start(evolution, firstOnAccount, setting, forceLong);
            _run = StartCoroutine(Run(evolution));
        }

        IEnumerator Run(EvolutionDef evo)
        {
            _baseOrtho = cam.orthographicSize;
            _camRest = cam.transform.localPosition;
            _trauma = 0f;
            _shockAt = -1f;
            _flipUntil = -1f;
            var tl = _aw.Timeline;

            Shader.SetGlobalFloat(IdSoft, 0.05f);
            Shader.SetGlobalFloat(IdVignette, 0.55f);
            StartCoroutine(InkGrains());

            NameCard name = null;
            Stamp stamp = null;
            var cracks = new List<LineRenderer>();
            float shockRadiusWorld = 0f;

            while (true)
            {
                float dt = Dt;
                var cues = _aw.Tick(dt);
                float t = _aw.Elapsed;
                Time.timeScale = _aw.WorldTimeScale;
                _aw.TickWorld(Time.deltaTime);

                if ((cues & AwakeningCue.Glyph) != 0) StartCoroutine(GlyphFly(evo));
                if ((cues & AwakeningCue.Crack) != 0) { cracks = MakeCracks(); cracks.ForEach(c => ToOverlay(c.gameObject)); AddTrauma(0.3f); }
                if ((cues & AwakeningCue.Name) != 0) { name = new NameCard(this, evo); ToOverlay(name.Root.gameObject); }
                if ((cues & AwakeningCue.Stamp) != 0 && name != null)
                {
                    stamp = new Stamp(this, name.Root);
                    ToOverlay(name.Root.gameObject);
                    AddTrauma(0.55f);
                    if (!reduceMotion) _flipUntil = t + FlipSeconds;
                }
                if ((cues & AwakeningCue.Shock) != 0)
                {
                    _shockAt = t;
                    AddTrauma(1f);
                    float h = _baseOrtho * 2f, w = h * cam.aspect;
                    shockRadiusWorld = _aw.ShockRadius(w, h);
                    StartCoroutine(Ring(shockRadiusWorld));
                }

                // 흑백 구멍: 처음 0.18초에 켜지고, 파동 때 구멍이 화면 끝까지 커지며 색이 돌아온다.
                float amount = Mathf.Clamp01(t / 0.18f);
                float radius = HoleRadius;
                if (_shockAt >= 0f)
                {
                    float k = Ease(Mathf.Clamp01((t - _shockAt) / ShockSpreadSeconds));
                    radius = Mathf.Lerp(HoleRadius, 1.6f, k);
                    amount *= 1f - Mathf.Clamp01((t - _shockAt - ShockSpreadSeconds) / 0.3f);
                }
                Vector3 vp = cam.WorldToViewportPoint(_target.position);
                Shader.SetGlobalVector(IdCenter, new Vector4(vp.x, vp.y, 0, 0));
                Shader.SetGlobalFloat(IdRadius, radius);
                Shader.SetGlobalFloat(IdAmount, amount);
                Shader.SetGlobalFloat(IdInvert, t < _flipUntil ? 1f : 0f);

                // 확대: 처음 0.35초에 0.86배, 파동 뒤 되돌림.
                float zoom = Mathf.Lerp(1f, ZoomIn, Ease(Mathf.Clamp01(t / ZoomInSeconds)));
                if (_shockAt >= 0f)
                    zoom = Mathf.Lerp(ZoomIn, 1f, Ease(Mathf.Clamp01((t - _shockAt) / (tl.DurationSeconds - _shockAt))));
                cam.orthographicSize = _baseOrtho * zoom;
                if (overlayCam != null) overlayCam.orthographicSize = cam.orthographicSize;

                ApplyShake(dt);

                // 균열은 붓질 무렵부터 옅어진다.
                FadeCracks(cracks, t, tl);

                name?.Update(t, tl, cam, sortingOrder);
                stamp?.Update(t - tl.StampSeconds);

                if ((cues & AwakeningCue.End) != 0) break;
                yield return null;
            }

            cam.orthographicSize = _baseOrtho;
            if (overlayCam != null) overlayCam.orthographicSize = _baseOrtho;
            cam.transform.localPosition = _camRest;
            cam.transform.localRotation = Quaternion.identity;
            Time.timeScale = 1f;
            ResetGlobals();
            foreach (var go in _spawned) if (go != null) Destroy(go);
            _spawned.Clear();
            _run = null;
        }

        // ---------- 흔들림: 트라우마² × 부드러운 잡음, 기울기 0.03rad ----------

        void AddTrauma(float v) => _trauma = Mathf.Clamp01(Mathf.Max(_trauma, v));

        void ApplyShake(float dt)
        {
            float scale = reduceMotion ? 0f : shakeSetting;
            float s = _trauma * _trauma * scale;
            float time = Time.unscaledTime * 22f;
            float amp = cam.orthographicSize * 0.06f;
            var off = new Vector3((Mathf.PerlinNoise(time, 0.1f) - 0.5f) * 2f, (Mathf.PerlinNoise(0.7f, time) - 0.5f) * 2f, 0f) * amp * s;
            float rot = (Mathf.PerlinNoise(time, 3.3f) - 0.5f) * 2f * 0.03f * s * Mathf.Rad2Deg;
            cam.transform.localPosition = _camRest + off;
            cam.transform.localRotation = Quaternion.Euler(0, 0, rot);
            _trauma = Mathf.Max(0f, _trauma - dt * 1.4f);
        }

        // ---------- 흡기: 화면 끝 먹 알갱이 70개가 몸으로 빨려 든다 ----------

        IEnumerator InkGrains()
        {
            var grains = new List<(Transform tr, Vector3 from, float delay, float dur)>();
            float h = cam.orthographicSize, w = h * cam.aspect;
            var rnd = new System.Random(7);
            for (int i = 0; i < InkGrainCount; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float r = 1.05f + (float)rnd.NextDouble() * 0.25f;
                var from = _target.position + new Vector3(Mathf.Cos(a) * w * r, Mathf.Sin(a) * h * r, 0f);
                var go = MakeSprite("먹 알갱이", Dot, Ink, sortingOrder - 5, true);
                float size = 0.05f + (float)rnd.NextDouble() * 0.09f;
                go.transform.localScale = Vector3.one * size * h / 5f * 4f;
                go.transform.position = from;
                grains.Add((go.transform, from, (float)rnd.NextDouble() * 0.25f, 0.45f + (float)rnd.NextDouble() * 0.3f));
            }
            float t = 0f;
            bool alive = true;
            while (alive)
            {
                t += Dt;
                alive = false;
                foreach (var g in grains)
                {
                    if (g.tr == null) continue;
                    float k = Mathf.Clamp01((t - g.delay) / g.dur);
                    if (k < 1f) alive = true;
                    // 끌어당김: 처음엔 느리게, 몸 가까이서 빨라짐
                    g.tr.position = Vector3.Lerp(g.from, _target.position, k * k * k);
                    g.tr.gameObject.SetActive(k < 1f);
                }
                yield return null;
            }
        }

        // ---------- 문턱: 무공 칸 글자가 날아와 몸에 스민다(포물선) ----------

        IEnumerator GlyphFly(EvolutionDef evo)
        {
            if (nameFont == null || string.IsNullOrEmpty(evo.Hanja)) yield break;
            var go = new GameObject("무공 칸 글자");
            _spawned.Add(go);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = nameFont;
            tmp.text = evo.Hanja.Substring(0, 1);
            tmp.fontSize = 6f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Gold;
            tmp.sortingOrder = sortingOrder + 5;
            ToOverlay(go);
            // HUD 무공 칸 자리(화면 왼쪽 아래)에서 출발
            Vector3 from = cam.ViewportToWorldPoint(new Vector3(0.12f, 0.1f, 10f));
            float t = 0f;
            while (t < GlyphFlySeconds && go != null)
            {
                t += Dt;
                float k = Mathf.Clamp01(t / GlyphFlySeconds);
                var to = _target.position;
                var p = Vector3.Lerp(from, to, k);
                p.y += Mathf.Sin(k * Mathf.PI) * cam.orthographicSize * 0.35f;
                p.z = 0f;
                go.transform.position = p;
                go.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.3f, k * k) * cam.orthographicSize / 5f;
                tmp.alpha = 1f - k * k;
                yield return null;
            }
            if (go != null) go.SetActive(false);
        }

        // ---------- 균열: 몸에서 위아래로 화면을 가르는 금빛 지그재그, 3겹 ----------

        static readonly float[] Zig = { 0f, 0.35f, -0.25f, 0.45f, -0.4f, 0.2f, -0.5f, 0.3f, -0.15f, 0.4f, 0f };

        List<LineRenderer> MakeCracks()
        {
            var list = new List<LineRenderer>();
            float h = _baseOrtho;
            var layers = new (float width, Color color)[]
            {
                (0.32f, new Color(Gold.r, Gold.g, Gold.b, 0.18f)),
                (0.12f, Gold),
                (0.04f, new Color(1f, 0.98f, 0.9f, 1f)),
            };
            for (int i = 0; i < layers.Length; i++)
            {
                var go = new GameObject("금빛 균열 " + i);
                _spawned.Add(go);
                var lr = go.AddComponent<LineRenderer>();
                lr.material = SpriteMaterial();
                lr.sortingOrder = sortingOrder - 10 + i; // 이름 띠 아래
                lr.widthMultiplier = layers[i].width * h / 5f;
                lr.startColor = lr.endColor = layers[i].color;
                lr.positionCount = Zig.Length * 2 - 1;
                lr.useWorldSpace = false;
                go.transform.position = _target.position;
                var c = Vector3.zero;
                int n = 0;
                for (int j = Zig.Length - 1; j >= 0; j--)
                    lr.SetPosition(n++, c + new Vector3(Zig[j] * 0.35f * h / 5f, j / (float)(Zig.Length - 1) * h * 1.3f, 0));
                for (int j = 1; j < Zig.Length; j++)
                    lr.SetPosition(n++, c + new Vector3(-Zig[j] * 0.35f * h / 5f, -j / (float)(Zig.Length - 1) * h * 1.3f, 0));
                list.Add(lr);
            }
            return list;
        }

        static void FadeCracks(List<LineRenderer> cracks, float t, AwakeningTimelineDef tl)
        {
            if (cracks.Count == 0 || tl.CrackSeconds == null) return;
            float start = tl.CrackSeconds.Value;
            float grow = Mathf.Clamp01((t - start) / 0.12f);
            float fade = 1f - Mathf.Clamp01((t - tl.ShockSeconds) / 0.3f);
            foreach (var lr in cracks)
            {
                var c = lr.startColor;
                float a = (lr.name.EndsWith("0") ? 0.18f : 1f) * fade;
                lr.startColor = lr.endColor = new Color(c.r, c.g, c.b, a);
                lr.transform.localScale = new Vector3(1f, grow, 1f);
            }
        }

        // ---------- 파동: 먹 고리가 화면 끝까지 퍼짐 ----------

        IEnumerator Ring(float radius)
        {
            var go = new GameObject("먹 고리");
            _spawned.Add(go);
            var lr = go.AddComponent<LineRenderer>();
            lr.material = SpriteMaterial();
            lr.loop = true;
            lr.sortingOrder = sortingOrder - 1;
            const int seg = 96;
            lr.positionCount = seg;
            float t = 0f;
            while (t < ShockSpreadSeconds + 0.2f && go != null)
            {
                t += Dt;
                float k = Ease(Mathf.Clamp01(t / ShockSpreadSeconds));
                float r = Mathf.Lerp(0.2f, radius, k);
                var c = _target.position;
                for (int i = 0; i < seg; i++)
                {
                    float a = i / (float)seg * Mathf.PI * 2f;
                    float wobble = 1f + 0.02f * Mathf.Sin(a * 7f + 1.3f);
                    lr.SetPosition(i, c + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * r * wobble);
                }
                lr.widthMultiplier = Mathf.Lerp(0.9f, 0.08f, k) * _baseOrtho / 5f;
                float alpha = 1f - Mathf.Clamp01((t - ShockSpreadSeconds * 0.6f) / 0.35f);
                lr.startColor = lr.endColor = new Color(Ink.r, Ink.g, Ink.b, alpha);
                yield return null;
            }
            if (go != null) go.SetActive(false);
        }

        // ---------- 이름: 흰 물감 띠 위에 한자 이름을 한 글자씩 위→아래 붓질 ----------

        sealed class NameCard
        {
            public readonly Transform Root;
            readonly TextMeshPro _tmp;
            readonly Transform _band;
            readonly SpriteRenderer[] _strokes;
            readonly bool _vertical;

            public NameCard(AwakeningFx fx, EvolutionDef evo)
            {
                var go = new GameObject("절기 이름");
                fx._spawned.Add(go);
                Root = go.transform;
                _vertical = fx.cam.aspect < 1f; // 세로 화면은 오른쪽에 세로로

                // 흰 물감 띠: 겹친 붓 획 7개
                var band = new GameObject("흰 물감 띠");
                band.transform.SetParent(Root, false);
                _band = band.transform;
                _strokes = new SpriteRenderer[7];
                var rnd = new System.Random(evo.Id.GetHashCode());
                for (int i = 0; i < _strokes.Length; i++)
                {
                    var s = MakeSprite("붓 획 " + i, Square, Paper, fx.sortingOrder);
                    s.transform.SetParent(_band, false);
                    float off = ((float)rnd.NextDouble() - 0.5f) * 0.5f;
                    float thick = 0.35f + (float)rnd.NextDouble() * 0.25f;
                    s.transform.localPosition = new Vector3(0, off, 0);
                    s.transform.localRotation = Quaternion.Euler(0, 0, ((float)rnd.NextDouble() - 0.5f) * 3f);
                    s.transform.localScale = new Vector3(1f, thick, 1f);
                    _strokes[i] = s.GetComponent<SpriteRenderer>();
                    _strokes[i].color = new Color(Paper.r, Paper.g, Paper.b, 0.55f + 0.08f * i);
                }
                if (_vertical) _band.localRotation = Quaternion.Euler(0, 0, 90);

                var txt = new GameObject("한자");
                txt.transform.SetParent(Root, false);
                _tmp = txt.AddComponent<TextMeshPro>();
                _tmp.font = fx.nameFont;
                _tmp.text = _vertical ? string.Join("\n", evo.Hanja.ToCharArray()) : evo.Hanja;
                _tmp.fontSize = _vertical ? 9f : 11f;
                _tmp.characterSpacing = _vertical ? 0f : 8f;
                _tmp.lineSpacing = -20f;
                _tmp.alignment = TextAlignmentOptions.Center;
                _tmp.textWrappingMode = TextWrappingModes.NoWrap;
                _tmp.color = Ink;
                _tmp.sortingOrder = fx.sortingOrder + 2;
                _tmp.ForceMeshUpdate();
            }

            public void Update(float t, AwakeningTimelineDef tl, Camera cam, int order)
            {
                float since = t - tl.NameSeconds;
                float h = cam.orthographicSize, w = h * cam.aspect;
                Vector3 c = cam.transform.position;
                c.z = 0f;
                Root.position = _vertical ? c + new Vector3(w * 0.7f, 0, 0) : c + new Vector3(0, h * 0.62f, 0);
                Root.localScale = Vector3.one * h / 5f;
                Root.rotation = Quaternion.identity;

                int count = _tmp.textInfo.characterCount;
                float bandWidth = _vertical ? 9f : 10.5f;
                // 띠가 먼저 펴진다
                float bandK = Ease(Mathf.Clamp01(since / 0.18f));
                _band.localScale = new Vector3(bandWidth * bandK, 2.1f, 1f);

                // 사라짐: 파동 뒤 여운 구간
                float fade = 1f - Mathf.Clamp01((t - tl.ShockSeconds) / Mathf.Max(0.1f, tl.DurationSeconds - tl.ShockSeconds));
                foreach (var s in _strokes)
                {
                    var col = s.color;
                    s.color = new Color(col.r, col.g, col.b, 0.6f * fade);
                }

                // 글자마다 0.11초 차, 위→아래로 드러남(정점 알파와 살짝 내려오는 오프셋)
                var info = _tmp.textInfo;
                for (int i = 0; i < count; i++)
                {
                    var ch = info.characterInfo[i];
                    if (!ch.isVisible) continue;
                    float k = Mathf.Clamp01((since - i * CharStagger) / CharStroke);
                    int mi = ch.materialReferenceIndex, vi = ch.vertexIndex;
                    var cols = info.meshInfo[mi].colors32;
                    // 위 두 정점(1,2)이 먼저, 아래 두 정점(0,3)이 뒤에 차오른다
                    byte top = (byte)(255 * Mathf.Clamp01(k * 2f) * fade);
                    byte bot = (byte)(255 * Mathf.Clamp01(k * 2f - 1f) * fade);
                    cols[vi + 1].a = top; cols[vi + 2].a = top;
                    cols[vi + 0].a = bot; cols[vi + 3].a = bot;
                }
                _tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
            }
        }

        // ---------- 낙관: 붉은 絶 이 1.8배에서 1배로 찍힘 ----------

        sealed class Stamp
        {
            readonly Transform _root;
            public Stamp(AwakeningFx fx, Transform nameRoot)
            {
                var go = new GameObject("絶 낙관");
                fx._spawned.Add(go);
                _root = go.transform;
                _root.SetParent(nameRoot, false);
                _root.localPosition = fx.cam.aspect < 1f ? new Vector3(0, -5.2f, 0) : new Vector3(5.6f, -0.9f, 0);
                var sq = MakeSprite("인주", Square, Vermilion, fx.sortingOrder + 3);
                sq.transform.SetParent(_root, false);
                sq.transform.localScale = new Vector3(1.4f, 1.4f, 1f);
                var txt = new GameObject("絶");
                txt.transform.SetParent(_root, false);
                var tmp = txt.AddComponent<TextMeshPro>();
                tmp.font = fx.nameFont;
                tmp.text = "絶";
                tmp.fontSize = 9f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Paper;
                tmp.sortingOrder = fx.sortingOrder + 4;
                _root.localRotation = Quaternion.Euler(0, 0, -6f);
                _root.localScale = Vector3.zero;
            }

            public void Update(float since)
            {
                if (since < 0f) return;
                float k = Mathf.Clamp01(since / StampSeconds);
                _root.localScale = Vector3.one * Mathf.Lerp(1.8f, 1f, k * k);
            }
        }

        // ---------- 도우미 ----------

        void ToOverlay(GameObject go)
        {
            if (overlayCam == null) return;
            int layer = LayerMask.NameToLayer(OverlayLayerName);
            if (layer < 0) return;
            foreach (var tr in go.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = layer;
        }

        static float Ease(float x) => x * x * (3f - 2f * x);

        static Material _spriteMat;
        static Material SpriteMaterial()
        {
            if (_spriteMat == null)
            {
                var sh = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                _spriteMat = new Material(sh);
            }
            return _spriteMat;
        }

        static GameObject MakeSprite(string name, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            sr.sharedMaterial = SpriteMaterial();
            return go;
        }

        GameObject MakeSprite(string name, Sprite sprite, Color color, int order, bool track)
        {
            var go = MakeSprite(name, sprite, color, order);
            if (track) _spawned.Add(go);
            return go;
        }

        static Sprite Dot => _dot ??= MakeDotSprite();
        static Sprite Square => _square ??= Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);

        static Sprite MakeDotSprite()
        {
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01((1f - d) * 3f)));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }
    }
}
