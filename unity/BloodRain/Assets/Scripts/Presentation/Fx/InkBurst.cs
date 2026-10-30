using System.Collections.Generic;
using UnityEngine;

namespace BloodRain.Presentation.Fx
{
    // 밟힌 적이 먹물로 터지는 자국(TU-03). 둥근 먹점 몇 개가 번지며 옅어진다. 시계는 언스케일
    // (돌파 중 세상 시간 0.05배여도 쓸림 자국은 제 속도로 보이게).
    // 임시 도형이다. 먹 번짐 셰이더·적 아틀라스가 들어오면 바꾼다.
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
                push = d.sqrMagnitude > 1e-6f ? d.normalized * size * 3f : Vector2.zero;
            }
            for (int i = 0; i < blotsPerBurst; i++)
            {
                float a = (float)(_rng.NextDouble() * Mathf.PI * 2);
                float r = (float)_rng.NextDouble() * size * 0.5f;
                var off = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var sr = _pool.Count > 0 ? _pool.Pop() : MakeRenderer();
                sr.gameObject.SetActive(true);
                _live.Add(new Blot
                {
                    R = sr, From = at + off * r, Vel = off * size * 0.8f + push,
                    Size = size * Mathf.Lerp(0.35f, 0.7f, (float)_rng.NextDouble()), Age = 0f, C = color,
                });
            }
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
                b.R.transform.localScale = Vector3.one * b.Size * Mathf.Lerp(0.6f, 1.6f, ease);
                var c = b.C; c.a *= 1f - k * k;
                b.R.color = c;
                _live[i] = b;
            }
        }
    }
}
