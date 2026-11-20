using System.Collections.Generic;
using BloodRain.Sim;
using UnityEngine;

namespace BloodRain.Presentation.Fx
{
    /// <summary>화면에 그릴 적 한 덩이. 위치는 세계 m, 기혈은 0~1.</summary>
    public sealed class ClusterView
    {
        public Vector2 Position;
        public int BornRealm;
        public float Health = 1f;
        public int Seed;
        public Color Tint = Color.white;
        /// <summary>시뮬레이션이 주는 콩콩 박자(0~1, 앞 HopDuty 동안 공중). -1 이면 렌더러 시계로 계속 뛴다(TU-04 시험용), -2 면 안 뛴다.</summary>
        public float HopBeat = -1f;
        public float HopDuty = 0.72f;
        /// <summary>이 덩이 사람 그림(적 원화, B-12Ua). null 이면 렌더러의 atlas(임시 강시).</summary>
        public Texture2D Art;
        /// <summary>사람 하나 크기(그 덩이가 생긴 경지 m). 0 이면 렌더러의 memberRealmMeters.</summary>
        public float Size;

        /// <summary>몸놀림(2026-11-17 밤 사람: "너무 허접하게 움직이는 느낌" — 뛰지 않는 적은 그림이 미끄러지기만 했다).
        /// 걷는 박자는 실제로 움직인 빠르기를 따르고, 그림은 가는 쪽을 본다(원화는 오른쪽을 본다).</summary>
        public ClusterMotion Motion = ClusterMotion.Walk;

        /// <summary>HF-01U 맞은 때(Time.unscaledTime)와 맞은 방향의 가로 몫(-1~1, 친 쪽에서 적 쪽). EnemyField.Damage 가 넣는다.
        /// 맞으면 덩이 사람 모두가 0.055초 따뜻한 흰빛으로 섞이고(세기 0.28 — vfx.md '절제'), 0.16초 동안 가로로 퍼지고 세로로 눌리며 맞은 쪽 반대로 기운다(시안 sects.html hit).</summary>
        public float HitAt = -10f;
        public float HitDirX;

        internal int Drawn = -1;          // 지난 프레임에 그린 사람 수
        internal Vector2 LastPos; internal bool HasLast;
        internal float Face = 1f;         // 1 오른쪽, -1 왼쪽
        internal float FaceShown = 1f;    // 돌아서는 중(납작해졌다 펴짐)
        internal float Speed;             // 부드럽게 한 빠르기(세계 m/초)
        internal float Stride;            // 걸음 박자(걸은 거리로 쌓는다 — 서면 멈춤)
        internal int FadeFrom;            // 디졸브 중인 사람의 끝 순번(이 순번 아래, Drawn 이상이 사라지는 중)
        internal float FadeStart = -10f;
    }

    /// <summary>적 몸놀림 갈래. EnemyField 가 적 종류를 보고 고른다.</summary>
    public enum ClusterMotion : byte
    {
        Walk,   // 사람: 걸음마다 살짝 들썩이고 좌우로 기우뚱
        Hop,    // 강시: 콩콩(시뮬레이션 박자). 뜰 때 늘어나고 내려앉을 때 눌린다
        Leap,   // 두꺼비: 웅크렸다 뛰기(렌더러 박자)
        Float,  // 귀신·그림자: 떠서 느리게 흔들림
        Beast,  // 짐승·지네: 앞으로 숙이고 잰걸음
        Stiff,  // 목인·동인: 뻣뻣하게 좌우로 덜컥덜컥
        Heavy,  // 정예·땅의 주인: 느리고 무거운 걸음, 디딜 때 눌림
    }

    // 적 덩이 렌더러(기획서 7장 '적 한 덩이', TU-04). 적 그림 한 장을 덩이당 최대 64번 겹쳐 그린다.
    // 그리는 사람 수는 Core(Scale.DrawMembers = ceil(덩이 크기 × 남은 기혈 비율)).
    // 덩이 안 자리는 시드로 고정한다. 자리 순서도 시드로 섞어 두고 뒤 순번부터 빠지게 해서,
    // 사람 수가 줄어도 남은 사람은 제자리이고 빠지는 사람만 먹물 디졸브로 사라진다.
    // 강시 콩콩 트윈은 사람마다 박자를 조금씩 어긋나게(같은 덩이 안에서 0~0.3박).
    public sealed class ClusterRenderer : MonoBehaviour
    {
        const int Batch = 1023;
        static readonly int IdTint = Shader.PropertyToID("_Tint");
        static readonly int IdDissolve = Shader.PropertyToID("_Dissolve");
        static readonly int IdFlash = Shader.PropertyToID("_Flash");
        // HF-01U 맞는 쪽 반응(시안 sects.html hit, 절제): 번쩍 0.055초·세기 0.28, 찌그러짐 0.16초·가로 +28%·세로 −22%·기울기 0.35rad
        public const float FlashSeconds = 0.055f, FlashStrength = 0.28f, SquashSeconds = 0.16f;

        public Material material;
        public Texture2D atlas;
        [Tooltip("사람 하나 크기(그 덩이가 생긴 경지 m). 적 데이터가 붙으면 적마다 값.")]
        public float memberRealmMeters = 1.0f;
        [Tooltip("덩이 안 사람 사이 간격(사람 크기 배).")]
        public float spacing = 0.9f;
        public float hopPeriod = 0.55f;
        public float hopHeight = 0.22f;      // 사람 크기 배
        public float hopPhaseSpread = 0.3f;  // 박자 어긋남(1 = 한 박)
        public float dissolveSeconds = 0.35f;
        public bool cull = true;

        readonly List<ClusterView> _clusters = new List<ClusterView>();
        readonly Matrix4x4[] _mats = new Matrix4x4[Batch];
        readonly Vector4[] _tints = new Vector4[Batch];
        readonly float[] _dissolves = new float[Batch];
        readonly float[] _flashes = new float[Batch];
        readonly Dictionary<long, Vector3[]> _layouts = new Dictionary<long, Vector3[]>();
        MaterialPropertyBlock _mpb;
        Mesh _quad;
        int _count;

        public WorldScale World;
        public IReadOnlyList<ClusterView> Clusters => _clusters;
        /// <summary>지난 프레임에 그린 사람 수(디졸브 중 포함)와 그리기 호출 수.</summary>
        public int LastInstances { get; private set; }
        public int LastDrawCalls { get; private set; }

        public void Add(ClusterView c) => _clusters.Add(c);
        public void Clear() => _clusters.Clear();

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _quad = MakeQuad();
        }

        /// <summary>덩이 크기 n, 시드 s 의 자리(x, y: 덩이 가운데 기준 사람 크기 단위, z: 박자 어긋남 0~1). 해바라기 배치를 시드로 흔들고 순서를 섞는다.</summary>
        Vector3[] Layout(int n, int seed)
        {
            long key = ((long)n << 32) | (uint)seed;
            if (_layouts.TryGetValue(key, out var lay)) return lay;
            var rng = new System.Random(seed * 7919 + n);
            lay = new Vector3[n];
            const float golden = 2.39996323f;
            for (int i = 0; i < n; i++)
            {
                float r = n == 1 ? 0f : spacing * Mathf.Sqrt(i + 0.5f);
                float a = i * golden + (float)rng.NextDouble() * 0.5f;
                float j = n == 1 ? 0f : spacing * 0.25f;
                lay[i] = new Vector3(Mathf.Cos(a) * r + (float)(rng.NextDouble() - 0.5) * j,
                                     Mathf.Sin(a) * r * 0.8f + (float)(rng.NextDouble() - 0.5) * j,
                                     (float)rng.NextDouble());
            }
            for (int i = n - 1; i > 0; i--) { int k = rng.Next(i + 1); (lay[i], lay[k]) = (lay[k], lay[i]); }
            _layouts[key] = lay;
            return lay;
        }

        void LateUpdate()
        {
            var ws = World ?? WorldScale.Current;
            if (ws == null || material == null || _clusters.Count == 0) { LastInstances = LastDrawCalls = 0; return; }
            if (atlas != null) material.mainTexture = atlas;

            var cam = Camera.main;
            Rect view = default;
            if (cam != null && cam.orthographic)
            {
                float h = cam.orthographicSize, w = h * cam.aspect;
                Vector2 c = cam.transform.position;
                view = new Rect(c.x - w, c.y - h, w * 2, h * 2);
            }

            float now = Time.unscaledTime;
            float t = Time.time;
            int instances = 0, calls = 0;
            _count = 0;
            var rp = new RenderParams(material) { matProps = _mpb, layer = gameObject.layer, worldBounds = new Bounds(Vector3.zero, Vector3.one * 1e7f) };

            // 1) 덩이마다 그릴 사람 수·디졸브를 한 번만 셈하고, 화면 안 덩이만 그림별로 모은다(B-12Ua: 적 종류마다 제 원화)
            _visible.Clear();
            _textures.Clear();
            foreach (var c in _clusters)
            {
                int target = ws.Scale.DrawMembers(c.BornRealm, c.Health);
                if (c.Drawn < 0) c.Drawn = target;
                if (target < c.Drawn)
                {
                    bool fading = now - c.FadeStart < dissolveSeconds;
                    c.FadeFrom = fading ? Mathf.Max(c.FadeFrom, c.Drawn) : c.Drawn;
                    c.FadeStart = now;
                    c.Drawn = target;
                }
                else if (target > c.Drawn) c.Drawn = target;

                float fadeK = (now - c.FadeStart) / dissolveSeconds;
                int upto = fadeK < 1f ? Mathf.Max(c.Drawn, c.FadeFrom) : c.Drawn;
                if (upto <= 0) continue;

                int n = ws.Scale.ClusterSize(c.BornRealm);
                float size = ws.Length(c.Size > 0f ? c.Size : memberRealmMeters, c.BornRealm);
                if (cull && cam != null)
                {
                    float r = size * (spacing * Mathf.Sqrt(n) + 1f);
                    if (c.Position.x + r < view.xMin || c.Position.x - r > view.xMax ||
                        c.Position.y + r < view.yMin || c.Position.y - r > view.yMax) continue;
                }
                // 몸놀림 재료: 빠르기·보는 쪽·걸음 박자(덩이마다 한 번)
                float dt = Time.deltaTime;
                if (!c.HasLast) { c.LastPos = c.Position; c.HasLast = true; }
                else if (dt > 1e-5f)
                {
                    var d = c.Position - c.LastPos;
                    c.LastPos = c.Position;
                    float sp = d.magnitude / dt;
                    if (sp > size * 40f) sp = 0f; // 순간 이동(밀려남·자리 옮김)은 걸음으로 치지 않는다
                    c.Speed = Mathf.Lerp(c.Speed, sp, 1f - Mathf.Exp(-dt * 8f));
                    c.Stride += c.Speed * dt / Mathf.Max(0.01f, size);
                    if (Mathf.Abs(d.x) > size * 0.15f * dt) c.Face = d.x > 0f ? 1f : -1f;
                    c.FaceShown = Mathf.MoveTowards(c.FaceShown, c.Face, dt * 9f);
                }
                var tex = c.Art != null ? c.Art : atlas;
                if (!_textures.Contains(tex)) _textures.Add(tex);
                _visible.Add((c, n, size, upto, fadeK, tex));
            }

            // 2) 그림 한 장마다 그 그림을 쓰는 덩이만 모아 그린다(그리기 호출 = 화면 안 종류 수 × ceil(사람/1023))
            foreach (var tex in _textures)
            {
                _batchTex = tex;
                foreach (var (c, n, size, upto, fadeK, ctex) in _visible)
                {
                if (ctex != tex) continue;
                var lay = Layout(n, c.Seed);
                Vector4 tint = c.Tint;
                float hitAge = now - c.HitAt;
                float flash = hitAge >= 0f && hitAge < FlashSeconds ? FlashStrength * (1f - hitAge / FlashSeconds) : 0f;
                float squash = hitAge >= 0f && hitAge < SquashSeconds ? 1f - hitAge / SquashSeconds : 0f;
                for (int i = 0; i < upto; i++)
                {
                    var m = lay[i];
                    float hop;
                    if (c.HopBeat >= 0f)
                    {
                        // 시뮬레이션 박자: 공중에 있는 동안만 포물선, 사람마다 조금 늦게 뛴다
                        float f = Mathf.Repeat(c.HopBeat - m.z * hopPhaseSpread * 0.25f, 1f);
                        hop = f < c.HopDuty ? Mathf.Sin(f / c.HopDuty * Mathf.PI) * hopHeight : 0f;
                    }
                    else if (c.HopBeat < -1.5f) hop = 0f; // 뛰지 않는 적(-2)
                    else
                    {
                        float beat = t / hopPeriod + m.z * hopPhaseSpread;
                        hop = Mathf.Abs(Mathf.Sin(beat * Mathf.PI)) * hopHeight;
                    }
                    // 몸놀림: 들썩임(bob)·기울기(tilt, 라디안)·늘어남(sx, sy). 발은 땅에 붙여 둔다
                    float bob = 0f, tilt = 0f, sx = 1f, sy = 1f;
                    float own = m.z * 6.283f + c.Seed * 0.37f;          // 사람마다 어긋나는 몫
                    float moveK = Mathf.Clamp01(c.Speed / (size * 0.35f)); // 서 있으면 0
                    float breath = Mathf.Sin(t * 2.1f + own) * 0.012f;
                    switch (c.Motion)
                    {
                        case ClusterMotion.Hop:
                            if (c.HopBeat >= 0f)
                            {
                                float f = Mathf.Repeat(c.HopBeat - m.z * hopPhaseSpread * 0.25f, 1f);
                                if (f < c.HopDuty) { float a = Mathf.Sin(f / c.HopDuty * Mathf.PI); sy = 1f + 0.10f * a; sx = 1f - 0.06f * a; }
                                else { float g = Mathf.Sin((f - c.HopDuty) / (1f - c.HopDuty) * Mathf.PI); sy = 1f - 0.13f * g; sx = 1f + 0.09f * g; }
                            }
                            break;
                        case ClusterMotion.Leap:
                        {
                            float f = Mathf.Repeat(t / 0.9f + m.z * 0.6f + c.Seed * 0.13f, 1f);
                            if (f < 0.45f) { float a = Mathf.Sin(f / 0.45f * Mathf.PI); bob = a * 0.34f * moveK; sy = 1f + 0.14f * a * moveK; sx = 1f - 0.08f * a * moveK; }
                            else { float g = Mathf.Sin((f - 0.45f) / 0.55f * Mathf.PI); sy = 1f - 0.16f * g; sx = 1f + 0.12f * g; }
                            break;
                        }
                        case ClusterMotion.Float:
                            bob = 0.10f + Mathf.Sin(t * 1.7f + own) * 0.07f;
                            tilt = Mathf.Sin(t * 1.15f + own * 1.3f) * 0.07f - c.FaceShown * 0.05f * moveK;
                            sy = 1f + Mathf.Sin(t * 2.3f + own) * 0.035f; sx = 2f - sy;
                            break;
                        case ClusterMotion.Beast:
                        {
                            float ph = c.Stride * 2.6f + own;
                            bob = Mathf.Abs(Mathf.Sin(ph)) * 0.07f * moveK;
                            tilt = -c.FaceShown * (0.10f + 0.05f * Mathf.Sin(ph * 2f)) * moveK;
                            sx = 1f + 0.06f * Mathf.Sin(ph * 2f) * moveK; sy = 2f - sx + breath;
                            break;
                        }
                        case ClusterMotion.Stiff:
                        {
                            float ph = c.Stride * 1.5f + own;
                            float rock = Mathf.Sin(ph); rock = Mathf.Sign(rock) * Mathf.Pow(Mathf.Abs(rock), 0.35f); // 덜컥: 양 끝에 오래 머문다
                            tilt = rock * 0.13f * moveK;
                            bob = Mathf.Abs(Mathf.Cos(ph)) * 0.02f * moveK;
                            break;
                        }
                        case ClusterMotion.Heavy:
                        {
                            float ph = c.Stride * 1.1f + own;
                            float step = Mathf.Abs(Mathf.Sin(ph));
                            bob = step * 0.05f * moveK;
                            tilt = Mathf.Sin(ph) * 0.045f * moveK;
                            sy = 1f - (1f - step) * 0.05f * moveK + breath; sx = 1f + (1f - step) * 0.04f * moveK;
                            break;
                        }
                        default: // Walk
                        {
                            float ph = c.Stride * 1.9f + own;
                            bob = Mathf.Abs(Mathf.Sin(ph)) * 0.075f * moveK;
                            tilt = Mathf.Sin(ph) * 0.085f * moveK - c.FaceShown * 0.03f * moveK;
                            sy = 1f + Mathf.Sin(ph * 2f) * 0.03f * moveK + breath; sx = 2f - sy;
                            break;
                        }
                    }
                    if (squash > 0f) { sx *= 1f + 0.28f * squash; sy *= 1f - 0.22f * squash; tilt -= c.HitDirX * squash * 0.35f; } // 맞은 쪽 반대로 기울며 눌림
                    // 돌아설 때는 가로로 납작해졌다 펴진다(FaceShown -1~1). 0 을 지나며 그림이 뒤집힌다
                    float face = Mathf.Abs(c.FaceShown) < 0.12f ? (c.FaceShown < 0f ? -0.12f : 0.12f) : c.FaceShown;
                    float w2 = size * sx * face, h2 = size * sy;
                    float cs = Mathf.Cos(tilt), sn = Mathf.Sin(tilt);
                    float footX = c.Position.x + m.x * size, footY = c.Position.y + (m.y + hop + bob) * size - size * 0.5f;
                    var mat = Matrix4x4.identity;
                    mat.m00 = cs * w2; mat.m01 = -sn * h2;
                    mat.m10 = sn * w2; mat.m11 = cs * h2;
                    mat.m03 = footX - sn * h2 * 0.5f; mat.m13 = footY + cs * h2 * 0.5f;
                    mat.m23 = -0.3f - m.y * 0.0001f;
                    _mats[_count] = mat;
                    _tints[_count] = tint;
                    _dissolves[_count] = i < c.Drawn ? 0f : Mathf.Clamp01(fadeK);
                    _flashes[_count] = flash;
                    _count++;
                    instances++;
                    if (_count == Batch) { Flush(rp); calls++; }
                }
                }
                if (_count > 0) { Flush(rp); calls++; }
            }
            LastInstances = instances;
            LastDrawCalls = calls;
        }

        readonly List<(ClusterView c, int n, float size, int upto, float fadeK, Texture2D tex)> _visible = new List<(ClusterView, int, float, int, float, Texture2D)>();
        readonly List<Texture2D> _textures = new List<Texture2D>();
        Texture2D _batchTex;
        static readonly int IdMainTex = Shader.PropertyToID("_MainTex");

        void Flush(RenderParams rp)
        {
            _mpb.Clear();
            if (_batchTex != null) _mpb.SetTexture(IdMainTex, _batchTex);
            _mpb.SetVectorArray(IdTint, _tints);
            _mpb.SetFloatArray(IdDissolve, _dissolves);
            _mpb.SetFloatArray(IdFlash, _flashes);
            Graphics.RenderMeshInstanced(rp, _quad, 0, _mats, _count);
            _count = 0;
        }

        static Mesh MakeQuad()
        {
            var m = new Mesh { name = "덩이 사람" };
            m.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            m.RecalculateBounds();
            return m;
        }

        /// <summary>적 아틀라스가 오기 전 임시 그림: 먹빛 강시 실루엣(머리, 두 팔을 앞으로 뻗은 몸).</summary>
        public static Texture2D MakePlaceholderAtlas()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "임시 강시" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                float head = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.8f)) - 0.12f;
                float body = Mathf.Max(Mathf.Abs(u - 0.5f) - 0.16f, Mathf.Abs(v - 0.42f) - 0.3f);
                float arms = Mathf.Max(Mathf.Abs(u - 0.5f) - 0.3f, Mathf.Abs(v - 0.6f) - 0.05f);
                float d = Mathf.Min(head, Mathf.Min(body, arms));
                byte a = (byte)(Mathf.Clamp01(0.5f - d * n) * 255);
                bool hat = v > 0.86f && Mathf.Abs(u - 0.5f) < 0.16f;
                px[y * n + x] = hat ? new Color32(40, 30, 28, 255) : new Color32(70, 64, 72, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
