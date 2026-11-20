using System.Collections.Generic;
using UnityEngine;

namespace BloodRain.Presentation.Fx
{
    // 밟힌 적이 먹물로 터지는 자국(TU-03). 둥근 먹점 몇 개가 번지며 옅어진다. 시계는 언스케일
    // (돌파 중 세상 시간 0.05배여도 쓸림 자국은 제 속도로 보이게).
    // 2026-11-20 21시 BT-01U: 둥근 흰 원 스프라이트(ScaleTestDriver.MakeCircle)를 크게 키워 찍던 것이 초절정 돌파 쓸림에서
    // 화면을 새까만 원판 백여 개로 덮었다. 가장자리가 들쭉날쭉한 먹 방울 그림(MakeBlot)으로 바꾸고, 크기·짙기·수를 줄였다.
    // 쓸린 적 몸은 DeathLayer.AddSwept 가 먹빛으로 날려 보내니 여기 먹점은 곁들이는 몫이다.
    public sealed class InkBurst : MonoBehaviour
    {
        public static readonly Color Ink = new Color(0.07f, 0.06f, 0.06f, 0.85f);
        public static readonly Color Ember = new Color(0.85f, 0.45f, 0.15f, 0.9f);

        [SerializeField] int blotsPerBurst = 6;
        [SerializeField] float life = 0.45f;

        struct Blot { public SpriteRenderer R; public Vector2 From, Vel; public float Size, Age; public Color C; }

        readonly List<Blot> _live = new List<Blot>();
        readonly Stack<SpriteRenderer> _pool = new Stack<SpriteRenderer>();
        Sprite _sprite;
        Material _mat;
        readonly System.Random _rng = new System.Random(3);

        public int LiveCount => _live.Count;

        public void Init(Sprite sprite, Material mat) { _sprite = sprite; _mat = mat; }

        /// <summary>at 에서 터진다. awayFrom 이 있으면 그 반대쪽으로 쓸려 나가듯 흐른다.</summary>
        public void Burst(Vector2 at, float size, Color color, Vector2? awayFrom)
        {
            Vector2 push = Vector2.zero;
            if (awayFrom.HasValue)
            {
                var d = at - awayFrom.Value;
                push = d.sqrMagnitude > 1e-6f ? d.normalized * size * 2f : Vector2.zero;
            }
            // 돌파 쓸림처럼 한꺼번에 수백이 밟히면 먹점이 천 개 넘게 떴다(2026-11-20 재 봄: 오브젝트 1,008개). 많이 떠 있으면 덜 찍는다
            int n = _live.Count > 240 ? 0 : _live.Count > 80 ? 1 : awayFrom.HasValue ? 3 : blotsPerBurst;
            for (int i = 0; i < n; i++)
            {
                float a = (float)(_rng.NextDouble() * Mathf.PI * 2);
                float r = (float)_rng.NextDouble() * size * 0.5f;
                var off = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var sr = _pool.Count > 0 ? _pool.Pop() : MakeRenderer();
                sr.gameObject.SetActive(true);
                _live.Add(new Blot
                {
                    R = sr, From = at + off * r, Vel = off * size * 0.8f + push,
                    Size = size * Mathf.Lerp(0.16f, 0.36f, (float)_rng.NextDouble()), Age = 0f, C = color,
                });
                sr.transform.rotation = Quaternion.Euler(0f, 0f, (float)_rng.NextDouble() * 360f);
            }
        }

        static Sprite _blot;

        /// <summary>먹 방울 한 점(64px): 가장자리가 잡음으로 들쭉날쭉하고 안쪽이 조금 더 짙으며, 곁에 작은 방울 몇 개가 튄다.</summary>
        public static Sprite MakeBlot()
        {
            if (_blot != null) return _blot;
            const int n = 64;
            var rng = new System.Random(11);
            float[] lobe = new float[7];
            for (int i = 0; i < lobe.Length; i++) lobe[i] = (float)rng.NextDouble() * 6.283f;
            var sat = new Vector3[4];
            for (int i = 0; i < sat.Length; i++)
            {
                float a = (float)rng.NextDouble() * 6.283f, r = 0.36f + (float)rng.NextDouble() * 0.1f;
                sat[i] = new Vector3(0.5f + Mathf.Cos(a) * r, 0.5f + Mathf.Sin(a) * r, 0.025f + (float)rng.NextDouble() * 0.03f);
            }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "먹 방울" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n - 0.5f, v = (y + 0.5f) / n - 0.5f;
                float d = Mathf.Sqrt(u * u + v * v), th = Mathf.Atan2(v, u);
                float edge = 0.27f + 0.035f * Mathf.Sin(th * 3f + lobe[0]) + 0.025f * Mathf.Sin(th * 5f + lobe[1]) + 0.015f * Mathf.Sin(th * 9f + lobe[2]);
                float a = Mathf.Clamp01((edge - d) * n * 0.5f);
                foreach (var s in sat)
                {
                    float sd = Mathf.Sqrt((u + 0.5f - s.x) * (u + 0.5f - s.x) + (v + 0.5f - s.y) * (v + 0.5f - s.y));
                    a = Mathf.Max(a, Mathf.Clamp01((s.z - sd) * n * 0.5f));
                }
                // 안쪽이 짙고 가장자리로 가며 살짝 옅다(먹이 번진 자리)
                float core = Mathf.Lerp(1f, 0.72f, Mathf.Clamp01(d / 0.3f));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * core * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            _blot = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n * 0.6f);
            return _blot;
        }

        SpriteRenderer MakeRenderer()
        {
            var go = new GameObject("먹물");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _sprite;
            sr.sharedMaterial = _mat;
            sr.sortingOrder = 5;
            return sr;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var b = _live[i];
                b.Age += dt;
                float k = Mathf.Clamp01(b.Age / life);
                if (k >= 1f)
                {
                    b.R.gameObject.SetActive(false);
                    _pool.Push(b.R);
                    _live.RemoveAt(i);
                    continue;
                }
                float ease = 1f - (1f - k) * (1f - k);
                var p = b.From + b.Vel * ease;
                b.R.transform.position = new Vector3(p.x, p.y, -0.6f);
                b.R.transform.localScale = Vector3.one * b.Size * Mathf.Lerp(0.7f, 1.25f, ease);
                var c = b.C; c.a *= 0.8f * (1f - k * k);
                b.R.color = c;
                _live[i] = b;
            }
        }
    }
}
