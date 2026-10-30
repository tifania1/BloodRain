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

        internal int Drawn = -1;          // 지난 프레임에 그린 사람 수
        internal int FadeFrom;            // 디졸브 중인 사람의 끝 순번(이 순번 아래, Drawn 이상이 사라지는 중)
        internal float FadeStart = -10f;
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
                float size = ws.Length(memberRealmMeters, c.BornRealm);
                if (cull && cam != null)
                {
                    float r = size * (spacing * Mathf.Sqrt(n) + 1f);
                    if (c.Position.x + r < view.xMin || c.Position.x - r > view.xMax ||
                        c.Position.y + r < view.yMin || c.Position.y - r > view.yMax) continue;
                }

                var lay = Layout(n, c.Seed);
                Vector4 tint = c.Tint;
                for (int i = 0; i < upto; i++)
                {
                    var m = lay[i];
                    float beat = t / hopPeriod + m.z * hopPhaseSpread;
                    float hop = Mathf.Abs(Mathf.Sin(beat * Mathf.PI)) * hopHeight;
                    var pos = new Vector3(c.Position.x + m.x * size, c.Position.y + (m.y + hop) * size, -0.3f - m.y * 0.0001f);
                    _mats[_count] = Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(size, size, 1f));
                    _tints[_count] = tint;
                    _dissolves[_count] = i < c.Drawn ? 0f : Mathf.Clamp01(fadeK);
                    _count++;
                    instances++;
                    if (_count == Batch) { Flush(rp); calls++; }
                }
            }
            if (_count > 0) { Flush(rp); calls++; }
            LastInstances = instances;
            LastDrawCalls = calls;
        }

        void Flush(RenderParams rp)
        {
            _mpb.Clear();
            _mpb.SetVectorArray(IdTint, _tints);
            _mpb.SetFloatArray(IdDissolve, _dissolves);
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
