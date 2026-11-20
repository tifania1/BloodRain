using System.Collections.Generic;
using UnityEngine;

namespace BloodRain.Presentation.Fx
{
    // HF-03U 절제된 타격감: 적이 쓰러지는 모습(2026-11-17 사람: "흔들림·번쩍임 대신 … 적이 죽을 때 좀 다르게 죽는다던가").
    // 쓰러진 적의 그림 한 장을 잠깐 남겨, 무공 성질마다 다르게 스러지게 한다(시안 sects.html 의 die·drawDeath, 단계 규칙은 VF-18).
    //   1·2단: 어느 무공이든 먹으로 번지며 밀려난다.
    //   3단부터 성질대로 — 검(먹): 둘로 갈라짐 · 권(금): 날아가며 금가루 · 봉(호박): 납작 눌림 · 번개(푸름): 그을려 굳었다 바스러짐
    //                      독: 녹아 가라앉음 · 물: 물살에 쓸려 감 · 꽃잎: 꽃잎으로 흩어짐 · 자주: 빨려 들며 오그라듦 · 불: 그을려 재로
    // 그리기는 덩이 렌더러와 같은 재질(ClusterInk: 사람마다 _Tint·_Dissolve). 규칙은 건드리지 않는다 — 적은 이미 무리에서 빠졌다.
    public sealed class DeathLayer : MonoBehaviour
    {
        public Material material;   // BloodRain/ClusterInk(덩이 렌더러 것). 복사해 쓴다

        const int Max = 96;
        enum Look { Fade, Split, Fly, Squash, Char, Melt, Sweep, Petal, Wither, Ash, Swept }
        struct Ghost { public Vector2 Pos, Dir; public float T, Life, Size, Spin, Reach; public Texture2D Art; public Look Look; public Color Tint; public bool Done, Unscaled; }

        readonly List<Ghost> _g = new List<Ghost>();
        readonly Matrix4x4[] _m = new Matrix4x4[1];
        readonly Vector4[] _t = new Vector4[1];
        readonly float[] _d = new float[1];
        Material _mat;
        MaterialPropertyBlock _mpb;
        Mesh _quad;
        static readonly int IdTint = Shader.PropertyToID("_Tint"), IdDissolve = Shader.PropertyToID("_Dissolve"), IdMainTex = Shader.PropertyToID("_MainTex");

        public int LiveCount => _g.Count;
        /// <summary>돌파 쓸림(EnemyField.Sweep)이 부른다. WeaponRunner 가 첫 처치 때 붙이니 그 전이면 null.</summary>
        public static DeathLayer Instance { get; private set; }

        static float Ez(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

        /// <summary>쓰러진 적 하나. dir 은 맞은 방향(몸에서 적 쪽), size 는 그림 한 변(월드), kind 는 무공 성질(SectFx.Hit 과 같은 말), stage 는 이펙트 단계.</summary>
        public void Add(Vector2 at, Vector2 dir, Texture2D art, float size, Color tint, string kind, int stage)
        {
            if (art == null || material == null || FxSettings.Simplified) return;
            if (_g.Count >= Max) _g.RemoveAt(0);
            var look = Look.Fade; float life = .3f;
            if (stage >= 3)
                switch (kind)
                {
                    case "gold": look = Look.Fly; life = .4f; break;
                    case "amber": look = Look.Squash; life = .5f; break;
                    case "blue": case "thunder": look = Look.Char; life = .55f; break;
                    case "poison": look = Look.Melt; life = .6f; break;
                    case "water": look = Look.Sweep; life = .45f; break;
                    case "petal": look = Look.Petal; life = .4f; break;
                    case "violet": look = Look.Wither; life = .42f; break;
                    case "fire": look = Look.Ash; life = .45f; break;
                    default: look = Look.Split; life = .4f; break;
                }
            if (dir.sqrMagnitude < 1e-6f) dir = Vector2.right;
            _g.Add(new Ghost { Pos = at, Dir = dir.normalized, Life = life, Size = size, Art = art, Look = look, Tint = tint, Spin = Random.value < .5f ? -1f : 1f });
            // HF-03U(2026-11-20 21시, 아침 사진에서 화산·마교 쓰러짐이 잘 안 읽힘): 꽃잎은 더 많이·크게(그림자는 SectFx 가 늘 깐다),
            // 혈조는 몸에서 핏방울이 공격한 쪽으로 빨려 나간다(빨리는 먹빛 붉은 방울)
            if (look == Look.Petal && SectFx.Instance != null) SectFx.Instance.Petals(at + Vector2.up * size * .4f, Mathf.Atan2(dir.y, dir.x), 9, 3.8f, 2.4f, .26f, FxRamp.Petal);
            if (look == Look.Wither && SectFx.Instance != null) SectFx.Instance.Drops(at + Vector2.up * size * .35f, Mathf.Atan2(-dir.y, -dir.x), 7, 7f, FxRamp.BloodMist, false, .12f, .45f);
        }

        /// <summary>BT-01U 돌파에 쓸린 적(시안 breakthrough.html): 0.25초(실제 시간 — 돌파 중 세상 시간은 0.05배) 동안 몸에서 바깥으로
        /// 거리의 35% 날아가며 먹빛으로 바뀌어 먹 점으로 흩어진다. 화면 밖이면 그리지 않는다(쓸림은 수백 덩이가 한꺼번에).</summary>
        public void AddSwept(Vector2 at, Vector2 center, Texture2D art, float size, Color tint)
        {
            if (art == null || material == null || FxSettings.Simplified) return;
            var cam = Camera.main;
            if (cam != null && cam.orthographic)
            {
                float h = cam.orthographicSize + size, w = cam.orthographicSize * cam.aspect + size;
                Vector2 c = cam.transform.position;
                if (Mathf.Abs(at.x - c.x) > w || Mathf.Abs(at.y - c.y) > h) return;
            }
            if (_g.Count >= Max) _g.RemoveAt(0);
            var d = at - center;
            float dist = d.magnitude;
            _g.Add(new Ghost { Pos = at, Dir = dist > 1e-5f ? d / dist : Vector2.right, Reach = dist * .35f, Life = .25f, Size = size, Art = art, Look = Look.Swept, Tint = tint, Unscaled = true, Spin = Random.value < .5f ? -1f : 1f });
        }

        void Awake()
        {
            Instance = this;
            _mpb = new MaterialPropertyBlock();
            _quad = new Mesh { name = "쓰러지는 사람" };
            _quad.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f) };
            _quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            _quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            _quad.RecalculateBounds();
        }

        void OnDestroy() { if (_mat != null) Destroy(_mat); if (Instance == this) Instance = null; }

        void LateUpdate()
        {
            if (_g.Count == 0 || material == null) return;
            if (_mat == null) _mat = new Material(material) { name = "쓰러지는 적", enableInstancing = true };
            float dt = Time.deltaTime, udt = Time.unscaledDeltaTime;
            var rp = new RenderParams(_mat) { matProps = _mpb, layer = gameObject.layer, worldBounds = new Bounds(Vector3.zero, Vector3.one * 1e7f) };
            for (int i = _g.Count - 1; i >= 0; i--)
            {
                var g = _g[i];
                g.T += g.Unscaled ? udt : dt;
                if (g.T >= g.Life) { _g.RemoveAt(i); continue; }
                float k = g.T / g.Life, s = g.Size;
                var n = new Vector2(-g.Dir.y, g.Dir.x);
                Color c = g.Tint;
                switch (g.Look)
                {
                    case Look.Split:   // 둘로 갈라져 양옆으로 미끄러진다
                        for (int h = -1; h <= 1; h += 2)
                            Draw(rp, g.Art, g.Pos + n * (h * s * .2f * Ez(k)) + g.Dir * (s * .12f * Ez(k)), h * 9f * k, s, s, Color.Lerp(c, new Color(.2f, .18f, .17f, 1f), k * .7f), .12f + k * .88f);
                        break;
                    case Look.Fly:     // 뒤로 날아가며 금빛으로 스러진다
                    {
                        float e = Ez(k);
                        Draw(rp, g.Art, g.Pos + g.Dir * (s * 2.2f * e) + Vector2.up * (Mathf.Sin(k * Mathf.PI) * s * .35f), g.Spin * 150f * e, s * (1f - .3f * k), s * (1f - .3f * k), Color.Lerp(c, new Color(1f, .82f, .38f, 1f), Mathf.Min(1f, k * 2f)), k * k);
                        if (!g.Done && k > .55f && SectFx.Instance != null) { g.Done = true; SectFx.Instance.Drops(g.Pos + g.Dir * (s * 2f), 0f, 7, 5f, FxRamp.Gold, true, .1f, 3.14f); }
                        break;
                    }
                    case Look.Squash:  // 납작 눌려 흙에 번진다
                    {
                        float e = Ez(Mathf.Min(1f, k * 3.5f)), sy = 1f - .78f * e, sx = 1f + .4f * e;
                        Draw(rp, g.Art, g.Pos - Vector2.up * (s * .39f * e), 0f, s * sx, s * sy, Color.Lerp(c, new Color(.5f, .38f, .24f, 1f), e * .8f), Mathf.Max(0f, (k - .4f) / .6f));
                        break;
                    }
                    case Look.Char:    // 그을려 굳었다가 바스러진다
                    {
                        float jit = k < .22f ? Mathf.Sin(g.T * 150f) * s * .035f : 0f;
                        Draw(rp, g.Art, g.Pos + Vector2.right * jit, 0f, s, s, Color.Lerp(c, new Color(.1f, .12f, .2f, 1f), Mathf.Min(1f, k * 6f)), Mathf.Max(0f, (k - .35f) / .65f));
                        break;
                    }
                    case Look.Melt:    // 녹색으로 물들며 녹아 가라앉는다
                    {
                        float sy = 1f - .5f * Ez(k);
                        Draw(rp, g.Art, g.Pos - Vector2.up * (s * .25f * Ez(k)), 0f, s * (1f + .2f * k), s * sy, Color.Lerp(c, new Color(.42f, .68f, .34f, 1f), Mathf.Min(1f, k * 3f)), .1f + k * .9f);
                        break;
                    }
                    case Look.Sweep:   // 물살에 쓸려 간다
                        Draw(rp, g.Art, g.Pos + g.Dir * (s * 1.5f * Ez(k)), -g.Dir.x * 28f * k, s, s * (1f - .25f * k), Color.Lerp(c, new Color(.58f, .84f, .88f, 1f), k), k);
                        break;
                    case Look.Petal:   // 짙은 분홍으로 물들며 위로 살짝 떠 흩어진다(HF-03U: 옅은 연분홍은 종이에 묻혔다)
                        Draw(rp, g.Art, g.Pos + Vector2.up * (s * .15f * Ez(k)), g.Spin * 8f * k, s * (1f - .15f * k), s * (1f - .15f * k), Color.Lerp(c, new Color(.82f, .38f, .52f, 1f), Mathf.Min(1f, k * 3f)), .1f + k * .9f);
                        break;
                    case Look.Wither:  // 피를 빨리며 검붉게 오그라든다(HF-03U: 자주가 흐렸다)
                        Draw(rp, g.Art, g.Pos - g.Dir * (s * .5f * Ez(k)), 0f, s * (1f - .6f * k), s * (1f - .35f * k), Color.Lerp(c, new Color(.36f, .06f, .12f, 1f), Mathf.Min(1f, k * 3.5f)), k * k);
                        break;
                    case Look.Swept:   // 돌파에 쓸려 바깥으로 날아가며 먹빛 점으로 흩어진다(시안: 거리 × 0.35, smoothstep, 먹 #1c1a17)
                    {
                        float e = k * k * (3f - 2f * k);
                        Draw(rp, g.Art, g.Pos + g.Dir * (g.Reach * e), g.Spin * 25f * e, s * (1f - .4f * e), s * (1f - .4f * e), Color.Lerp(c, new Color(.11f, .1f, .09f, 1f), Mathf.Min(1f, k * 4f)), .15f + .85f * k);
                        break;
                    }
                    case Look.Ash:     // 그을려 재가 되어 떠오른다
                        Draw(rp, g.Art, g.Pos + Vector2.up * (s * .3f * k), 0f, s, s, Color.Lerp(c, new Color(.2f, .15f, .1f, 1f), Mathf.Min(1f, k * 4f)), k);
                        break;
                    default:           // 먹으로 번지며 밀려난다
                        Draw(rp, g.Art, g.Pos + g.Dir * (s * .3f * Ez(k)), 0f, s, s, Color.Lerp(c, new Color(.25f, .22f, .2f, 1f), k), .1f + k * .9f);
                        break;
                }
                _g[i] = g;
            }
        }

        void Draw(RenderParams rp, Texture2D art, Vector2 pos, float rotDeg, float w, float h, Color tint, float dissolve)
        {
            _m[0] = Matrix4x4.TRS(new Vector3(pos.x, pos.y, -0.31f), Quaternion.Euler(0f, 0f, rotDeg), new Vector3(w, h, 1f));
            _t[0] = tint;
            _d[0] = Mathf.Clamp01(dissolve);
            _mpb.Clear();
            _mpb.SetTexture(IdMainTex, art);
            _mpb.SetVectorArray(IdTint, _t);
            _mpb.SetFloatArray(IdDissolve, _d);
            Graphics.RenderMeshInstanced(rp, _quad, 0, _m, 1);
        }
    }
}
