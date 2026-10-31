using BloodRain.Sim;
using UnityEngine;

namespace BloodRain.Presentation.Fx
{
    // 지형 단계 2, 수묵 지도 섞기(기획서 15장, TU-08). 웹 시안 cheonha.html 의 바닥 그리기와 같은 문턱을 쓴다.
    //   - 수묵 지도: 세계 4,000 x 2,600m 를 덮는 판 한 장(지도 그림 비율 그대로 늘림). 불투명도 0.35 + 0.65 × 먼 값.
    //     먼 값 = 화면 대각선 140m→700m 사이 로그로 0→1(산이 떠오르는 문턱과 같다).
    //   - 마당 바닥: 8m 칸 타일을 화면 크기 판에 세계 좌표로 깐다. 불투명도 = 가까운 값, 화면 대각선 80m→260m 사이 로그로 1→0.
    //     타일이 불투명이라 삼류·이류에서는 지도가 안 보이고, 일류를 지나며 바닥이 걷히고 지도가 드러난다.
    //   - 소품 층이 화면에 비해 너무 작아지면 지도로 넘긴다: PropLayer 가 FadeFor(크기) 를 곱한다.
    // 그리는 순서: 지도(2970) → 바닥(2980) → 소품(2990) → 나머지(3000~).
    public sealed class MapLayer : MonoBehaviour
    {
        const float GroundTile = 8f; // 웹 시안 T = 8m

        [Tooltip("ClusterInk 재질. 복사해 쓴다")]
        public Material material;
        [Tooltip("수묵 지도(art_queue map_central_plains). 없으면 땅 모양만 그린 임시 텍스처")]
        public Texture2D map;

        Material _mapMat, _groundMat;
        Mesh _quad;
        MaterialPropertyBlock _mpb;
        BloodRain.Core.World.Regions _regions;
        static readonly int IdTint = Shader.PropertyToID("_Tint");
        static readonly int IdDissolve = Shader.PropertyToID("_Dissolve");

        /// <summary>지금 먼 값(0~1)과 가까운 값(0~1). 시험·HUD 용.</summary>
        public float Far { get; private set; }
        public float Near { get; private set; }
        public float MapAlpha => 0.35f + 0.65f * Far;
        /// <summary>그린 수묵 지도가 들어와 있나(임시 땅 모양이 아니라). 소품 층이 도형 산을 뺄지 본다.</summary>
        public static bool HasPaintedMap { get; private set; }
        void OnDestroy() => HasPaintedMap = false;

        /// <summary>화면 대각선(세계 m) → 먼 값. 140m 에서 0, 700m 에서 1(로그).</summary>
        public static float FarAt(float viewDiag) => Mathf.Clamp01((Mathf.Log(viewDiag) - Mathf.Log(140f)) / (Mathf.Log(700f) - Mathf.Log(140f)));
        /// <summary>화면 대각선 → 가까운 값. 80m 에서 1, 260m 에서 0(로그).</summary>
        public static float NearAt(float viewDiag) => 1f - Mathf.Clamp01((Mathf.Log(viewDiag) - Mathf.Log(80f)) / (Mathf.Log(260f) - Mathf.Log(80f)));

        /// <summary>소품을 지도로 넘기는 몫(1 = 소품 그대로, 0 = 지도에 맡김). 소품 크기가 화면 대각선의 1/150 이면 1, 1/300 이면 0(로그).
        /// 나무(4m)는 대각선 600~1,200m(초절정), 집(7m)은 1,050~2,100m(화경), 전각(22m)은 3,300~6,600m(현경)에서 넘어간다.</summary>
        public static float FadeFor(float sizeMeters, float viewDiag)
        {
            float k = (Mathf.Log(viewDiag / sizeMeters) - Mathf.Log(150f)) / (Mathf.Log(300f) - Mathf.Log(150f));
            return 1f - Mathf.Clamp01(k);
        }

        void Start()
        {
            _regions = new BloodRain.Core.World.Regions(DataBoot.Data);
            _quad = MakeQuad();
            _mpb = new MaterialPropertyBlock();
            HasPaintedMap = map != null;
            _mapMat = new Material(material) { name = "수묵 지도", renderQueue = 2970, mainTexture = map != null ? map : MakeLandTexture() };
            var ground = MakeGroundTile();
            _groundMat = new Material(material) { name = "마당 바닥", renderQueue = 2980, mainTexture = ground };
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null || !cam.orthographic || _mapMat == null) return;
            float h = cam.orthographicSize, w = h * cam.aspect;
            float diag = 2f * Mathf.Sqrt(w * w + h * h);
            Far = FarAt(diag);
            Near = NearAt(diag);
            Vector2 c = cam.transform.position;

            // 수묵 지도: 세계 (0,0)~(W,H) 를 Unity 로. 세계 y 가 아래로라 Unity 에선 위끝이 (0,0).
            float W = _regions.WidthMeters, H = _regions.HeightMeters;
            var topLeft = WorldCoords.ToUnity(new System.Numerics.Vector2(0, 0));
            var center = new Vector3(topLeft.x + W / 2f, topLeft.y - H / 2f, 0.7f);
            Draw(_mapMat, Matrix4x4.TRS(center, Quaternion.identity, new Vector3(W, H, 1f)), MapAlpha);

            // 마당 바닥: 화면을 덮는 판, 타일은 세계 좌표에 붙인다
            if (Near > 0.01f)
            {
                float qw = w * 2f + GroundTile * 2f, qh = h * 2f + GroundTile * 2f;
                _groundMat.mainTextureScale = new Vector2(qw / GroundTile, qh / GroundTile);
                _groundMat.mainTextureOffset = new Vector2((c.x - qw / 2f) / GroundTile, (c.y - qh / 2f) / GroundTile);
                Draw(_groundMat, Matrix4x4.TRS(new Vector3(c.x, c.y, 0.6f), Quaternion.identity, new Vector3(qw, qh, 1f)), Near);
            }
        }

        void Draw(Material m, Matrix4x4 mat, float alpha)
        {
            _mpb.Clear();
            _mpb.SetVector(IdTint, new Vector4(1, 1, 1, alpha));
            _mpb.SetFloat(IdDissolve, 0f);
            var rp = new RenderParams(m) { matProps = _mpb, layer = gameObject.layer, worldBounds = new Bounds(mat.GetColumn(3), new Vector3(1e6f, 1e6f, 1f)) };
            Graphics.RenderMesh(rp, _quad, 0, mat);
        }

        static Mesh MakeQuad()
        {
            var m = new Mesh { name = "지도 판" };
            m.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            m.RecalculateBounds();
            return m;
        }

        /// <summary>웹 시안 groundTile: 한지 빛 바닥, 64px 벽돌 줄눈, 풀 획, 붉은 점. 256px 한 칸.</summary>
        static Texture2D MakeGroundTile()
        {
            const int T = 256;
            var px = new Color[T * T];
            var bg = (Color)new Color32(0xE4, 0xD9, 0xBF, 255);
            for (int i = 0; i < px.Length; i++) px[i] = bg;
            var rng = new System.Random(11);
            float R() => (float)rng.NextDouble();
            void Blend(int x, int y, Color col)
            {
                x = ((x % T) + T) % T; y = ((y % T) + T) % T;
                int i = (T - 1 - y) * T + x;
                px[i] = Color.Lerp(px[i], new Color(col.r, col.g, col.b, 1f), col.a);
            }
            var line = new Color(90 / 255f, 80 / 255f, 60 / 255f, 0.18f);
            for (int y = 0; y < T; y += 64)
            {
                for (int x = 0; x < T; x++) { Blend(x, y, line); Blend(x, y + 1, line); }
                for (int x = (y / 64 % 2) * 32; x < T; x += 64)
                    for (int yy = y; yy < y + 64; yy++) { Blend(x, yy, line); Blend(x + 1, yy, line); }
            }
            for (int i = 0; i < 70; i++)
            {
                var g = new Color(62 / 255f, 90 / 255f, 58 / 255f, 0.2f + R() * 0.3f);
                int x = (int)(R() * T), y = (int)(R() * T);
                for (int s = 0; s < 7; s++) { Blend(x - s * 3 / 7, y - s, g); Blend(x + s * 3 / 6, y - s * 6 / 7, g); }
            }
            for (int i = 0; i < 26; i++)
            {
                var r = new Color(179 / 255f, 38 / 255f, 30 / 255f, 0.05f + R() * 0.1f);
                int cx = (int)(R() * T), cy = (int)(R() * T), rad = 1 + (int)(R() * 3);
                for (int y = -rad; y <= rad; y++) for (int x = -rad; x <= rad; x++) if (x * x + y * y <= rad * rad) Blend(cx + x, cy + y, r);
            }
            var tex = new Texture2D(T, T, TextureFormat.RGBA32, true) { name = "임시 마당 바닥", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear };
            tex.SetPixels(px);
            tex.Apply(true, true);
            return tex;
        }

        /// <summary>지도 그림이 없을 때: 바다 빛 바탕에 땅 다각형(PropField 와 같은 것)만.</summary>
        Texture2D MakeLandTexture()
        {
            const int Wd = 512, Hd = 333;
            var field = new PropField(_regions, 0);
            var px = new Color[Wd * Hd];
            var sea = (Color)new Color32(0xD9, 0xE1, 0xDA, 255);
            var land = (Color)new Color32(0xE8, 0xDE, 0xC6, 255);
            for (int y = 0; y < Hd; y++)
                for (int x = 0; x < Wd; x++)
                {
                    float wx = (x + 0.5f) / Wd * _regions.WidthMeters, wy = (y + 0.5f) / Hd * _regions.HeightMeters;
                    px[(Hd - 1 - y) * Wd + x] = field.InLand(wx, wy) ? land : sea;
                }
            var tex = new Texture2D(Wd, Hd, TextureFormat.RGBA32, true) { name = "임시 땅 모양", wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels(px);
            tex.Apply(true, true);
            return tex;
        }
    }
}
