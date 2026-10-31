using System.Collections.Generic;
using BloodRain.Core.World;
using BloodRain.Sim;
using UnityEngine;

namespace BloodRain.Presentation.Fx
{
    // 화면 가장자리 표시(기획서 7·12장, TU-10).
    //   - 장막 붉은 번짐: Core PushStrength(0~1)만큼, 장막 쪽(출생지 반대쪽) 화면 가장자리가 주홍으로 번진다.
    //   - 땅의 주인 화살표: 깨어난 땅의 주인 지역이 화면 밖이면 그쪽 화면 끝에 붉은 화살표. 정예·금두꺼비는 AddMarker 로 같은 화살표(먹빛·금빛).
    //   - 혈운 기둥: 돌파로 새로 깨어난 지역 위로 붉은 기둥이 1.2초에 솟고 그 뒤엔 옅게 남는다(화면 대각선에 맞춰 크기).
    // 모두 화면 크기에 맞춰 그리는 판이라 축척과 상관없이 같은 크기로 보인다. ClusterInk 재질을 복사해 쓴다.
    public sealed class EdgeIndicators : MonoBehaviour
    {
        static readonly Color Crimson = new Color32(0xB3, 0x26, 0x1E, 255);
        static readonly int IdTint = Shader.PropertyToID("_Tint");
        static readonly int IdDissolve = Shader.PropertyToID("_Dissolve");

        public Material material;
        [Tooltip("출생지 지역(땅의 주인 깨어남 기준)")]
        public string birthRegion = "cheongun";
        [Tooltip("장막 번짐이 차지하는 가장자리 몫(화면 가운데→끝 거리 배)")]
        public float bloomBand = 0.35f;
        public float bloomMaxAlpha = 0.55f;
        [Tooltip("화살표 크기(화면 세로 배)")]
        public float arrowSize = 0.045f;
        public float pillarRise = 1.2f;

        public struct Marker { public System.Numerics.Vector2 World; public Color Color; }

        Material _bloomMat, _arrowMat, _pillarMat;
        Mesh _quad;
        MaterialPropertyBlock _mpb;
        Regions _regions;
        BossAwakening _boss;
        int _lastLevel = -1;
        readonly Dictionary<string, float> _pillarStart = new Dictionary<string, float>();
        readonly List<Marker> _markers = new List<Marker>();

        public BossAwakening Boss => _boss;
        public float Bloom { get; private set; }
        public int ArrowsShown { get; private set; }
        public int Pillars => _pillarStart.Count;

        /// <summary>정예·금두꺼비 같은 화면 밖 표시를 이번 프레임에 더한다(매 프레임 다시 넣는다).</summary>
        public void AddMarker(System.Numerics.Vector2 world, Color color) => _markers.Add(new Marker { World = world, Color = color });

        void Start()
        {
            _regions = new Regions(DataBoot.Data);
            _boss = new BossAwakening(DataBoot.Data, birthRegion, new BossRecord());
            _quad = MakeQuad();
            _mpb = new MaterialPropertyBlock();
            _bloomMat = new Material(material) { name = "장막 번짐", renderQueue = 3100, mainTexture = MakeEdgeGradient() };
            _arrowMat = new Material(material) { name = "화면 끝 화살표", renderQueue = 3110, mainTexture = MakeArrow() };
            _pillarMat = new Material(material) { name = "혈운 기둥", renderQueue = 3050, mainTexture = MakePillar() };
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            var ws = WorldScale.Current;
            var drv = ScaleTestDriver.Instance;
            if (cam == null || ws == null || _bloomMat == null) { _markers.Clear(); return; }

            // 돌파·경지 변화마다 깨어남을 다시 본다
            int level = (int)ws.Level;
            if (level != _lastLevel)
            {
                foreach (var r in _boss.UpdateRealm(level)) _pillarStart[r.Id] = Time.unscaledTime;
                _lastLevel = level;
            }

            float h = cam.orthographicSize, w = h * cam.aspect, diag = 2f * Mathf.Sqrt(w * w + h * h);
            Vector2 c = cam.transform.position;
            float z = cam.transform.position.z + 1f;

            // 혈운 기둥(세계에 서 있음)
            foreach (var kv in _pillarStart)
            {
                var u = WorldCoords.ToUnity(_regions.Center(kv.Key));
                float t = Mathf.Clamp01((Time.unscaledTime - kv.Value) / pillarRise);
                float rise = 1f - (1f - t) * (1f - t);
                float pw = diag * 0.025f, ph = diag * 0.35f * rise;
                float a = Mathf.Lerp(0.9f, 0.45f, Mathf.Clamp01((Time.unscaledTime - kv.Value - pillarRise) / 2f));
                Draw(_pillarMat, new Vector3(u.x, u.y + ph / 2f, 0.2f), 0f, new Vector3(pw, Mathf.Max(ph, 0.001f), 1f), new Color(1, 1, 1, a));
            }

            // 장막 붉은 번짐: 출생지 반대 방향(장막 쪽) 화면 끝
            Bloom = drv != null ? drv.PushStrength : 0f;
            if (Bloom > 0.001f && drv != null)
            {
                Vector2 dir = (Vector2)drv.Player.position - drv.Birthplace;
                if (dir.sqrMagnitude > 1e-6f)
                {
                    dir.Normalize();
                    float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    // 그 방향으로 화면 끝까지 거리
                    float e = Mathf.Min(Mathf.Abs(dir.x) > 1e-4f ? w / Mathf.Abs(dir.x) : 1e9f, Mathf.Abs(dir.y) > 1e-4f ? h / Mathf.Abs(dir.y) : 1e9f);
                    // 판 오른쪽 끝(u=1)이 화면 끝, 그라디언트가 u 1-bloomBand/2 ~ 1 에 있다
                    var col = Crimson; col.a = Bloom * bloomMaxAlpha;
                    Draw(_bloomMat, new Vector3(c.x, c.y, z), ang, new Vector3(e * 2f, diag * 2f, 1f), col);
                }
            }

            // 화면 밖 화살표: 깨어난 땅의 주인 + 넣어 준 표시
            int shown = 0;
            foreach (var r in _boss.Awake) shown += Arrow(cam, c, w, h, z, _regions.Center(r), Crimson) ? 1 : 0;
            foreach (var m in _markers) shown += Arrow(cam, c, w, h, z, m.World, m.Color) ? 1 : 0;
            ArrowsShown = shown;
            _markers.Clear();
        }

        bool Arrow(Camera cam, Vector2 c, float w, float h, float z, System.Numerics.Vector2 world, Color color)
        {
            var u = WorldCoords.ToUnity(world);
            Vector2 d = u - c;
            if (Mathf.Abs(d.x) <= w && Mathf.Abs(d.y) <= h) return false; // 화면 안이면 표시 안 함
            float size = h * 2f * arrowSize;
            float k = Mathf.Min((w - size) / Mathf.Max(1e-4f, Mathf.Abs(d.x)), (h - size) / Mathf.Max(1e-4f, Mathf.Abs(d.y)));
            var p = c + d * k;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f);
            var col = color; col.a *= pulse;
            Draw(_arrowMat, new Vector3(p.x, p.y, z), ang, new Vector3(size, size, 1f), col);
            return true;
        }

        void Draw(Material m, Vector3 pos, float angleDeg, Vector3 scale, Color tint)
        {
            _mpb.Clear();
            _mpb.SetVector(IdTint, tint);
            _mpb.SetFloat(IdDissolve, 0f);
            var rp = new RenderParams(m) { matProps = _mpb, layer = gameObject.layer, worldBounds = new Bounds(pos, Vector3.one * 1e6f) };
            Graphics.RenderMesh(rp, _quad, 0, Matrix4x4.TRS(pos, Quaternion.Euler(0, 0, angleDeg), scale));
        }

        static Mesh MakeQuad()
        {
            var m = new Mesh { name = "가장자리 판" };
            m.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            m.RecalculateBounds();
            return m;
        }

        /// <summary>가로 그라디언트: 판 가운데(u 0.5)에서 끝(u 1) 사이 바깥 35%(가장자리 몫)만 번지고, 위아래로 갈수록 옅게.</summary>
        Texture2D MakeEdgeGradient()
        {
            const int W = 256, H = 64;
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W, v = (y + 0.5f) / H;
                    float r = (u - 0.5f) * 2f; // 0 가운데, 1 화면 끝
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f - bloomBand, 1f, r));
                    a *= a;
                    // 옆으로 퍼지는 쪽은 화면 대각선 두 배 판이라 화면 안 부분(가운데 절반)이 진하게
                    float side = 1f - Mathf.Clamp01(Mathf.Abs(v - 0.5f) * 2.4f - 0.2f);
                    px[y * W + x] = new Color(1, 1, 1, a * side);
                }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = "장막 번짐", wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels(px); tex.Apply();
            return tex;
        }

        static Texture2D MakeArrow()
        {
            const int N = 64;
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (x + 0.5f) / N, v = (y + 0.5f) / N - 0.5f;
                    // 오른쪽을 가리키는 붓끝 삼각형
                    float half = 0.42f * (1f - u);
                    float a = Mathf.Clamp01((half - Mathf.Abs(v)) * N * 0.7f) * Mathf.Clamp01((u - 0.08f) * N * 0.5f);
                    px[y * N + x] = new Color(1, 1, 1, a);
                }
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { name = "화살표", wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels(px); tex.Apply();
            return tex;
        }

        static Texture2D MakePillar()
        {
            const int W = 32, H = 128;
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W - 0.5f, v = (y + 0.5f) / H;
                    float core = Mathf.Clamp01(1f - Mathf.Abs(u) * 2f);
                    float a = core * core * Mathf.Lerp(1f, 0f, v * v); // 아래 진하고 위로 흩어짐
                    var c = Color.Lerp(new Color32(0x6A, 0x12, 0x10, 255), new Color32(0xB3, 0x26, 0x1E, 255), v);
                    c.a = a;
                    px[y * W + x] = c;
                }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = "혈운 기둥", wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels(px); tex.Apply();
            return tex;
        }
    }
}
