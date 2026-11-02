using System.Collections.Generic;
using BloodRain.Sim;
using UnityEngine;

namespace BloodRain.Presentation.Fx
{
    // 적이 쓰러질 때 먹 튐 + 바닥 먹 자국(V-04, vfx.md '튀는 먹, 바닥 먹 자국': 파티클 버스트 + 바닥 데칼 풀링).
    //   먹 튐: Common/ink_splat_*(+x 로 튐)을 플레이어 반대쪽으로 돌려 0.1초에 펴지고 0.45초에 흩어진다.
    //   바닥 먹 자국: Common/ground_ink_* 를 그 자리에 깔고 dryingSeconds(8초)에 걸쳐 마른다(옅어지며 흩어짐).
    // 덩이 렌더러와 같은 재질(_Tint·_Dissolve 인스턴스 값)을 텍스처마다 하나씩 복사해 RenderMeshInstanced 로 그린다.
    // 그리는 순서는 renderQueue: 지도 2970 < 소품 2990 < 먹 자국 2995 < 적(3000) < 먹 튐 3001.
    // 한 프레임에 수백이 쓰러져도 무겁지 않게 프레임당 새로 만드는 수와 살아 있는 수에 상한을 두고, 이미 자국이 있는 자리엔 겹쳐 깔지 않는다.
    public sealed class InkMarks : MonoBehaviour
    {
        const int Batch = 1023;
        static readonly int IdTint = Shader.PropertyToID("_Tint");
        static readonly int IdDissolve = Shader.PropertyToID("_Dissolve");

        public Material material;
        public EnemyField enemies;
        public Transform player;
        public Texture2D[] splats;
        public Texture2D[] groundInks;
        [Tooltip("바닥 먹 자국이 마르는 시간(초)")]
        public float dryingSeconds = 8f;
        [Tooltip("먹 튐이 사라지는 시간(초)")]
        public float splatSeconds = 0.45f;
        [Tooltip("적 반지름에 곱하는 크기")]
        public float splatScale = 3.2f, groundScale = 3.0f;
        public int maxGround = 240, maxSplats = 300, newPerFrame = 12;

        struct Mark { public Vector2 Pos; public float Angle, Size, Age; public int Tex; }

        readonly List<Mark> _ground = new List<Mark>();
        readonly List<Mark> _splat = new List<Mark>();
        Material[] _groundMats, _splatMats;
        Mesh _quad;
        MaterialPropertyBlock _mpb;
        readonly Matrix4x4[] _mats = new Matrix4x4[Batch];
        readonly Vector4[] _tints = new Vector4[Batch];
        readonly float[] _dissolves = new float[Batch];
        int _madeThisFrame, _frame;

        public int GroundCount => _ground.Count;
        public int SplatCount => _splat.Count;

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _quad = MakeQuad();
            _groundMats = Make(groundInks, 2995, "바닥 먹 자국");
            _splatMats = Make(splats, 3001, "먹 튐");
        }

        Material[] Make(Texture2D[] texs, int queue, string name)
        {
            if (material == null || texs == null) return new Material[0];
            var list = new List<Material>();
            foreach (var t in texs)
                if (t != null) list.Add(new Material(material) { name = name, renderQueue = queue, enableInstancing = true, mainTexture = t });
            return list.ToArray();
        }

        void OnEnable() { if (enemies != null) enemies.Killed += OnKilled; }
        void OnDisable() { if (enemies != null) enemies.Killed -= OnKilled; }

        void OnKilled(Vector2 pos, EnemyAgent a)
        {
            if (Time.frameCount != _frame) { _frame = Time.frameCount; _madeThisFrame = 0; }
            if (_madeThisFrame >= newPerFrame) return;
            _madeThisFrame++;
            float r = Mathf.Max(a.Radius, 0.05f);
            Vector2 away = player != null ? pos - (Vector2)player.position : Random.insideUnitCircle;
            float ang = (away.sqrMagnitude > 1e-6f ? Mathf.Atan2(away.y, away.x) * Mathf.Rad2Deg : Random.value * 360f) + Random.Range(-25f, 25f);
            Splat(pos, r * splatScale, ang);
            Ground(pos, r * groundScale);
        }

        /// <summary>먹 튐 하나(시험에서도 부른다).</summary>
        public void Splat(Vector2 pos, float size, float angleDeg)
        {
            if (_splatMats.Length == 0) return;
            if (_splat.Count >= maxSplats) _splat.RemoveAt(0);
            _splat.Add(new Mark { Pos = pos, Angle = angleDeg, Size = size, Tex = Random.Range(0, _splatMats.Length) });
        }

        /// <summary>바닥 먹 자국 하나. 아직 덜 마른 자국이 가까이 있으면 깔지 않는다.</summary>
        public bool Ground(Vector2 pos, float size)
        {
            if (_groundMats.Length == 0) return false;
            float near = size * 0.35f;
            for (int i = _ground.Count - 1; i >= 0; i--)
            {
                var g = _ground[i];
                if (g.Age < dryingSeconds * 0.5f && (g.Pos - pos).sqrMagnitude < near * near) return false;
            }
            if (_ground.Count >= maxGround) _ground.RemoveAt(0);
            _ground.Add(new Mark { Pos = pos, Angle = Random.value * 360f, Size = size * Random.Range(0.85f, 1.15f), Tex = Random.Range(0, _groundMats.Length) });
            return true;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Age(_ground, dt, dryingSeconds);
            Age(_splat, dt, splatSeconds);
        }

        static void Age(List<Mark> list, float dt, float life)
        {
            int w = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                m.Age += dt;
                if (m.Age < life) list[w++] = m;
            }
            list.RemoveRange(w, list.Count - w);
        }

        void LateUpdate()
        {
            for (int t = 0; t < _groundMats.Length; t++) Draw(_ground, _groundMats[t], t, true);
            for (int t = 0; t < _splatMats.Length; t++) Draw(_splat, _splatMats[t], t, false);
        }

        void Draw(List<Mark> list, Material mat, int tex, bool ground)
        {
            var rp = new RenderParams(mat) { matProps = _mpb, layer = gameObject.layer, worldBounds = new Bounds(Vector3.zero, Vector3.one * 1e7f) };
            int c = 0;
            foreach (var m in list)
            {
                if (m.Tex != tex) continue;
                float s, alpha, dissolve;
                if (ground)
                {
                    float k = m.Age / dryingSeconds;            // 0 → 1
                    s = m.Size * (1f + 0.08f * Mathf.Clamp01(m.Age / 0.3f)); // 깔린 직후 살짝 번짐
                    alpha = 0.55f * (1f - k * k);               // 처음엔 진하다가 끝에 빨리 마름
                    dissolve = Mathf.Clamp01((k - 0.55f) / 0.45f);
                }
                else
                {
                    float k = m.Age / splatSeconds;
                    s = m.Size * Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(m.Age / 0.1f));
                    alpha = 0.9f;
                    dissolve = Mathf.Clamp01((k - 0.3f) / 0.7f);
                }
                _mats[c] = Matrix4x4.TRS(new Vector3(m.Pos.x, m.Pos.y, ground ? 0.3f : -0.1f), Quaternion.Euler(0, 0, m.Angle), new Vector3(s, s, 1f));
                _tints[c] = new Vector4(0.08f, 0.07f, 0.07f, alpha);
                _dissolves[c] = dissolve;
                if (++c == Batch) { Flush(rp, c); c = 0; }
            }
            if (c > 0) Flush(rp, c);
        }

        void Flush(RenderParams rp, int n)
        {
            _mpb.Clear();
            _mpb.SetVectorArray(IdTint, _tints);
            _mpb.SetFloatArray(IdDissolve, _dissolves);
            Graphics.RenderMeshInstanced(rp, _quad, 0, _mats, n);
        }

        static Mesh MakeQuad()
        {
            var m = new Mesh { name = "먹 자국" };
            m.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            m.RecalculateBounds();
            return m;
        }
    }
}
