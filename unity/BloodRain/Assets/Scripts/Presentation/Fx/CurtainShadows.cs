using BloodRain.Sim;
using UnityEngine;

namespace BloodRain.Presentation.Fx
{
    // 장막 너머 그림자(기획서 7장 '혈우 장막', TU-06). 장막 바깥 둘레에 윗 경지의 혈운 군세를
    // 검은 실루엣으로만 어른거리게 한다. 덩이 렌더러(TU-04)를 그대로 쓰되 덩이가 생긴 경지를
    // '지금 경지 + 1' 로 두어 한 단계 큰 배율(사람 크기 2.25배, 덩이 인원도 윗 경지 것)로 그린다.
    // 어른거림은 덩이마다 기혈 비율을 천천히 오르내리게 해서 사람이 먹물 디졸브로 사라졌다 다시 나타나게 한다.
    // 장막이 없는 경지(현경)에서는 그리지 않는다. 돌파 중에는 BreakthroughFx 가 셈한 장막 반경을 따른다.
    [RequireComponent(typeof(ClusterRenderer))]
    public sealed class CurtainShadows : MonoBehaviour
    {
        public BreakthroughFx breakthrough;
        public Vector2 birthplace;
        [Tooltip("장막 둘레에 둘 덩이 수. 둘레가 화면 높이의 30배쯤이라 화면에 몇 덩이는 걸리게 넉넉히")]
        public int count = 160;
        [Tooltip("장막 바깥으로 떨어진 거리(그 경지 첫 화면 대각선 배). x 가장 가까이, y 가장 멀리")]
        public Vector2 depth = new Vector2(0.06f, 0.5f);
        [Tooltip("둘레를 따라 떠도는 빠르기(라디안/초, 언스케일)")]
        public float drift = 0.012f;
        [Tooltip("사라졌다 나타나는 한 주기(초)")]
        public float flickerPeriod = 7f;
        public Color tint = new Color(0.06f, 0.03f, 0.03f, 0.62f);
        public int seed = 31;

        ClusterRenderer _renderer;
        ClusterView[] _views;
        float[] _angle, _dist, _phase, _speed;

        void Awake()
        {
            _renderer = GetComponent<ClusterRenderer>();
            if (_renderer.material != null) _renderer.material = new Material(_renderer.material) { name = "장막 너머 그림자" };
            if (_renderer.atlas == null) _renderer.atlas = ClusterRenderer.MakePlaceholderAtlas();

            var rng = new System.Random(seed);
            _views = new ClusterView[count];
            _angle = new float[count]; _dist = new float[count]; _phase = new float[count]; _speed = new float[count];
            for (int i = 0; i < count; i++)
            {
                _angle[i] = (i + (float)rng.NextDouble() * 0.8f) / count * Mathf.PI * 2f;
                _dist[i] = (float)rng.NextDouble();
                _phase[i] = (float)rng.NextDouble();
                _speed[i] = ((float)rng.NextDouble() * 0.6f + 0.7f) * (rng.Next(2) == 0 ? 1f : -1f);
                _views[i] = new ClusterView { Seed = rng.Next(), Tint = tint };
                _renderer.Add(_views[i]);
            }
        }

        /// <summary>지금 보이는 그림자 덩이 수(기혈이 0 보다 큰 것).</summary>
        public int Visible { get; private set; }

        void Update()
        {
            var ws = WorldScale.Current;
            if (ws == null) { _renderer.enabled = false; return; }

            int realm = ws.Realm;
            float? radius = ws.Scale.BoundaryRadius(realm);
            float alpha = 1f;
            if (breakthrough != null && breakthrough.IsPlaying)
            {
                alpha = breakthrough.CurtainAlpha;
                radius = alpha > 0f ? breakthrough.CurtainRadius : (float?)null;
            }
            if (radius == null || alpha <= 0f) { _renderer.enabled = false; Visible = 0; return; }
            _renderer.enabled = true;

            int born = Mathf.Min(realm + 1, ws.Scale.TopRealm);
            float view = ws.Scale.ViewDiagonalAtRealm(realm);
            // 덩이 반지름(덩이 렌더러 배치와 같은 식). 윗 경지 덩이는 화면에 비해 경지마다 커지니(삼류 11% → 초절정 37%)
            // 그만큼 밖으로 물려야 장막 안쪽으로 넘어오지 않는다. 배치가 세로로 0.8배 납작하고 가장자리 사람은 드무니
            // 반지름의 0.6배만 물린다(더 물리면 초절정에서 화면 위 끝 밖으로 나가 안 보인다).
            float spread = 0.6f * (_renderer.spacing * Mathf.Sqrt(ws.Scale.ClusterSize(born)) + 1f)
                         * ws.Length(_renderer.memberRealmMeters, born);
            float t = Time.unscaledTime;
            int visible = 0;
            for (int i = 0; i < _views.Length; i++)
            {
                var v = _views[i];
                float a = _angle[i] + t * drift * _speed[i];
                float d = radius.Value + spread + view * Mathf.Lerp(depth.x, depth.y, _dist[i])
                        + view * 0.02f * Mathf.Sin(t * 0.4f + _phase[i] * 6.283f);
                v.Position = birthplace + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                v.BornRealm = born;
                // 0~1 을 천천히 오르내리고 주기의 1/6쯤은 아예 빈다(어른거림)
                float wave = Mathf.Sin((t / flickerPeriod + _phase[i]) * Mathf.PI * 2f);
                v.Health = Mathf.Clamp01(wave * 0.6f + 0.55f);
                var c = tint; c.a *= alpha;
                v.Tint = c;
                if (v.Health > 0f) visible++;
            }
            Visible = visible;
        }
    }
}
