using System;
using BloodRain.Core.Progression;
using BloodRain.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BloodRain.Presentation.Fx
{
    // 진기 구슬 떨어뜨리기·끌기·흡수·그리기(C-05). 규칙은 Sim/OrbField.
    //   적이 쓰러지면(EnemyField.Killed) 그 자리에 구슬. 구슬 값은 balance.json orbs.small(1). 적별 처치 진기는 아직 정하는 중(P-02)이라 모두 작은 구슬.
    //   자석 반경 = stats.magnetBase(경지 m), 흡수 0.5m, 끌리는 속도 9 + 4 × 나이(경지 m/s) — 웹 값. 모두 WorldScale 로 세계 단위.
    //   흡수한 진기는 Absorbed 로 내보낸다. Life 가 붙기 전(C-10)까지는 시험용 RunXp 에 직접 넣어 레벨만 보여 준다.
    public sealed class OrbLayer : MonoBehaviour
    {
        const int Batch = 1023;
        static readonly int IdTint = Shader.PropertyToID("_Tint");
        static readonly int IdDissolve = Shader.PropertyToID("_Dissolve");

        public Material material;
        public EnemyField enemies;
        public Transform player;
        [Tooltip("구슬 지름(경지 m)")]
        public float orbRealmMeters = 0.35f;
        public float absorbRealmMeters = 0.5f;
        public float pullBase = 9f, pullPerSecond = 4f;
        public float mergeRealmMeters = 5f;
        public bool ownTestXp = true;
        public bool showStats = true;

        /// <summary>흡수한 진기(배율 전 값)와 구슬 수.</summary>
        public event Action<double, int> Absorbed;

        public OrbField Field { get; private set; }
        public RunXp TestXp { get; private set; }

        float _magnet = 1.5f;
        double _small = 1, _medium = 5, _large = 25;
        Material _mat;
        Mesh _quad;
        MaterialPropertyBlock _mpb;
        readonly Matrix4x4[] _mats = new Matrix4x4[Batch];
        readonly Vector4[] _tints = new Vector4[Batch];
        readonly float[] _dissolves = new float[Batch];

        void Awake()
        {
            var data = DataBoot.Data;
            Field = new OrbField(data.Balance.Limits.Orbs, 1101);
            _magnet = data.Balance.Stats.MagnetBase;
            _small = data.Balance.Orbs.Small;
            _medium = data.Balance.Orbs.Medium;
            _large = data.Balance.Orbs.Large;
            if (ownTestXp) TestXp = new RunXp(new XpCurve(data.Balance.XpCurve), new Realms(data.Balance.Realms));
            if (material != null)
            {
                _mat = new Material(material) { name = "진기 구슬", enableInstancing = true, mainTexture = MakeOrbTexture() };
            }
            _mpb = new MaterialPropertyBlock();
            _quad = MakeQuad();
        }

        void OnEnable() { if (enemies != null) enemies.Killed += OnKilled; }
        void OnDisable() { if (enemies != null) enemies.Killed -= OnKilled; }

        void OnKilled(Vector2 pos, EnemyAgent a)
        {
            var ws = WorldScale.Current;
            if (ws == null || player == null) return;
            Field.Drop(pos, _small, player.position, ws.Length(mergeRealmMeters));
        }

        void Update()
        {
            var ws = WorldScale.Current;
            if (ws == null || player == null) return;
            var kb = Keyboard.current;
            if (kb != null && kb.oKey.wasPressedThisFrame) Spray(500); // 시험: 구슬 500개 흩뿌려 합치기 보기
            if (kb != null && kb.mKey.wasPressedThisFrame) Field.PullAll();

            var (value, count) = Field.Step(Time.deltaTime, player.position, ws.Length(_magnet), ws.Length(absorbRealmMeters),
                                            ws.Speed(pullBase), ws.Speed(pullPerSecond));
            if (count > 0)
            {
                Absorbed?.Invoke(value, count);
                TestXp?.Absorb(value);
            }
        }

        /// <summary>시험용: 플레이어 둘레 화면 안에 구슬 n 개.</summary>
        public void Spray(int n)
        {
            var ws = WorldScale.Current;
            var rng = new System.Random(n + Field.Count);
            float r = ws.ViewDiagonal * 0.45f;
            for (int i = 0; i < n; i++)
            {
                var off = new Vector2((float)(rng.NextDouble() * 2 - 1), (float)(rng.NextDouble() * 2 - 1)) * r;
                Field.Drop((Vector2)player.position + off, _small, player.position, ws.Length(mergeRealmMeters));
            }
        }

        void LateUpdate()
        {
            var ws = WorldScale.Current;
            if (ws == null || _mat == null || Field.Count == 0) return;
            var rp = new RenderParams(_mat) { matProps = _mpb, layer = gameObject.layer, worldBounds = new Bounds(Vector3.zero, Vector3.one * 1e7f) };
            float baseSize = ws.Length(orbRealmMeters);
            // 13장: 진기 구슬 청 / 녹 / 적 = 1 / 5 / 25. 합친 구슬은 값으로 색을 고르고 크게.
            var blue = new Vector4(0.36f, 0.62f, 0.86f, 1f);
            var green = new Vector4(0.40f, 0.72f, 0.42f, 1f);
            var red = new Vector4(0.80f, 0.20f, 0.16f, 1f);
            int c = 0;
            float t = Time.time;
            foreach (var o in Field.Orbs)
            {
                float s = baseSize * (o.Big ? 2.4f : o.Value >= _large ? 1.6f : o.Value >= _medium ? 1.25f : 1f) * (1f + 0.08f * Mathf.Sin(t * 6f + o.Pos.x));
                _mats[c] = Matrix4x4.TRS(new Vector3(o.Pos.x, o.Pos.y, -0.2f), Quaternion.identity, new Vector3(s, s, 1f));
                _tints[c] = o.Value >= _large ? red : o.Value >= _medium ? green : blue;
                _dissolves[c] = 0f;
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

        void OnGUI()
        {
            if (!showStats || Field == null) return;
            string xp = TestXp != null ? $"  레벨 {TestXp.Level} ({TestXp.Xp:0}/{TestXp.Required})  경지 {TestXp.Realm.NameKo}" : "";
            GUI.Label(new Rect(12, 52, 640, 22), $"구슬 {Field.Count}/{Field.Limit}  합침 {Field.Merges}{xp}   K 일격, O 구슬 500, M 모두 끌기");
        }

        static Mesh MakeQuad()
        {
            var m = new Mesh { name = "구슬" };
            m.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            m.RecalculateBounds();
            return m;
        }

        /// <summary>그림이 오기 전 임시: 가운데가 밝은 둥근 구슬.</summary>
        static Texture2D MakeOrbTexture()
        {
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            float c = (n - 1) / 2f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Clamp01((1f - d) * 3f);
                float glow = Mathf.Lerp(1f, 0.75f, d);
                px[y * n + x] = new Color(glow, glow, glow, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
