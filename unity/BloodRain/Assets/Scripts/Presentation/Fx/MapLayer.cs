using BloodRain.Core.Data;
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
    [DefaultExecutionOrder(500)] // 카메라(ScaleCamera)가 이 프레임 자리·크기를 정한 뒤에 바닥 판을 깐다. 돌파 줌 중 한 프레임 늦어 화면 끝에 바닥 없는 띠가 보였다(2026-11-20)
    public sealed class MapLayer : MonoBehaviour
    {
        const float GroundTile = 12f; // 선지 한 장이 덮는 길이(m). 결이 잘아서 되풀이로 안 읽힌다

        [Tooltip("ClusterInk 재질. 복사해 쓴다")]
        public Material material;
        [Tooltip("그린 지도(usePaintedMap 일 때만). 옛 map_central_plains 는 땅 자리와 맞지 않아 비워 둔다(2026-11-19)")]
        public Texture2D map;

        [Tooltip("바닥이 지도를 가리는 정도. 1 이면 지도가 안 비친다(2026-11-17 밤 사람: 지도를 확대해 놓은 것 같다 → 안 비치게)")]
        [Range(0.5f, 1f)] public float groundOpacity = 1f;
        [Tooltip("옛 수묵 지도 그림을 쓴다. 끄면 지역 자료로 그린 천하도(땅 자리와 늘 맞음)")]
        public bool usePaintedMap = false;
        [Tooltip("지역 경계에서 두 바닥이 섞이는 띠(m). 두 지역까지 거리 차가 이 안이면 섞인다")]
        public float blendBand = 90f;
        [Tooltip("PP-01U 가장자리 어둡게 세기(시안 finish.html 0.22)")]
        [Range(0f, 0.6f)] public float vignette = 0.22f;

        Material _mapMat, _vigMat;
        readonly Material[] _groundMats = new Material[4]; // 발밑(바탕·원화), 이웃(바탕·원화) — 그리는 차례대로
        /// <summary>지금 쓰는 천하도 그림(구석 지도·큰 지도가 같이 쓴다).</summary>
        public Texture2D MapTexture { get; private set; }
        /// <summary>지금 발밑 지역과, 섞이는 이웃 지역(없으면 null)·섞인 몫(0~0.5).</summary>
        public string GroundRegion { get; private set; }
        public string GroundRegion2 { get; private set; }
        public float GroundBlend { get; private set; }
        Mesh _quad;
        MaterialPropertyBlock _mpb;
        BloodRain.Core.World.Regions _regions;
        static readonly int IdTint = Shader.PropertyToID("_Tint");
        static readonly int IdDissolve = Shader.PropertyToID("_Dissolve");

        /// <summary>지금 먼 값(0~1)과 가까운 값(0~1). 시험·HUD 용.</summary>
        public float Far { get; private set; }
        public float Near { get; private set; }
        // 2026-11-17 사람: "중원 전체 맵 적용이 하나도 안 돼 있다" — 그린 지도가 있으면 낮은 경지에서도 선지 밑으로 비치게 진하게 둔다
        public float MapAlpha => 0.9f + 0.1f * Far;
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
            // 천하도: 옛 그림은 땅 자리와 안 맞아(사람 2026-11-17) 지역 자료로 그린 것을 쓴다. 산도 지도에 그려 넣으니 도형 산은 뺀다
            MapTexture = usePaintedMap && map != null ? map : Biomes.MakeMap(_regions, new PropField(_regions));
            HasPaintedMap = true;
            _mapMat = new Material(material) { name = "천하도", renderQueue = 2970, mainTexture = MapTexture };
            for (int i = 0; i < _groundMats.Length; i++) _groundMats[i] = new Material(material) { name = "지역 바닥 " + i, renderQueue = 2980 + i };
            _vigMat = new Material(material) { name = "가장자리 어둡게", renderQueue = 3990, mainTexture = MakeVignette() };
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

            // 지역 바닥: 화면을 덮는 판, 무늬는 세계 좌표에 붙인다. 발밑 지역의 타일을 깔고, 경계 띠에서는 이웃 지역 타일을 위에 겹쳐 섞는다
            if (Near > 0.01f)
            {
                PickGround(WorldCoords.FromUnity(c));
                float qw = w * 2f + GroundTile * 2f, qh = h * 2f + GroundTile * 2f;
                var scale = new Vector2(qw / GroundTile, qh / GroundTile);
                var offset = new Vector2((c.x - qw / 2f) / GroundTile, (c.y - qh / 2f) / GroundTile);
                var mat = Matrix4x4.TRS(new Vector3(c.x, c.y, 0.6f), Quaternion.identity, new Vector3(qw, qh, 1f));
                DrawGround(0, GroundRegion, mat, scale, offset, Near * groundOpacity);
                if (GroundBlend > 0.01f)
                {
                    mat.m23 = 0.58f;
                    DrawGround(1, GroundRegion2, mat, scale, offset, Near * groundOpacity * GroundBlend);
                }
            }
            // 가장자리 어둡게(FL-01U): 화면에 붙인 판. 가운데는 투명
            if (vignette > 0.001f)
                Draw(_vigMat, Matrix4x4.TRS(new Vector3(c.x, c.y, -0.9f), Quaternion.identity, new Vector3(w * 2f, h * 2f, 1f)), vignette);
        }

        /// <summary>한 지역의 바닥을 깐다. 원화 타일이 있으면 지역 바탕빛을 깔고 그 위에 원화를 옅게, 없으면 코드로 그린 타일.
        /// slot 0 은 발밑 지역, 1 은 경계에서 섞이는 이웃(재질·그리는 차례가 따로다).</summary>
        void DrawGround(int slot, string region, Matrix4x4 mat, Vector2 scale, Vector2 offset, float alpha)
        {
            var art = Biomes.Art(region, out float artOpacity);
            var m = _groundMats[slot * 2];
            if (art == null)
            {
                m.mainTexture = Biomes.Tile(region);
                m.mainTextureScale = scale; m.mainTextureOffset = offset;
                Draw(m, mat, alpha);
                return;
            }
            m.mainTexture = Texture2D.whiteTexture;
            var b = Biomes.Of(region).Base;
            Draw(m, mat, alpha, new Color(b.r, b.g, b.b, 1f));
            var top = _groundMats[slot * 2 + 1];
            top.mainTexture = art;
            top.mainTextureScale = scale; top.mainTextureOffset = offset;
            mat.m23 -= 0.005f;
            Draw(top, mat, alpha * artOpacity);
        }

        /// <summary>발밑 지역과 이웃 지역을 고른다. 가장 가까운 두 지역까지의 거리 차가 blendBand 안이면 이웃을 그만큼(경계에서 반반) 섞는다.
        /// 봉마곡은 봉인석이 다 모이기 전에는 지역으로 치지 않는 규칙(Regions.Nearest)이 있지만 바닥은 보이는 대로 깐다.</summary>
        void PickGround(System.Numerics.Vector2 world)
        {
            RegionDef a = null, b = null; float da = float.MaxValue, db = float.MaxValue;
            foreach (var r in _regions.All)
            {
                float d = System.Numerics.Vector2.Distance(world, _regions.Center(r));
                if (d < da) { b = a; db = da; a = r; da = d; }
                else if (d < db) { b = r; db = d; }
            }
            GroundRegion = a != null ? a.Id : null;
            GroundRegion2 = b != null ? b.Id : null;
            float k = b == null ? 0f : 1f - Mathf.Clamp01((db - da) / Mathf.Max(1f, blendBand));
            GroundBlend = 0.5f * k * k * (3f - 2f * k);
        }

        void Draw(Material m, Matrix4x4 mat, float alpha) => Draw(m, mat, alpha, Color.white);

        void Draw(Material m, Matrix4x4 mat, float alpha, Color tint)
        {
            _mpb.Clear();
            _mpb.SetVector(IdTint, new Vector4(tint.r, tint.g, tint.b, alpha));
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

        /// <summary>FL-01U 선지 한 장(512px, 이음매 없음): 종이 빛 바탕에 옅은 얼룩(되풀이 잡음 두 겹)·섬유 획·티.
        /// 벽돌 줄눈과 'v' 풀은 뺐다 — 같은 무늬가 화면 가득 되풀이돼 원화보다 먼저 눈에 들어왔다.</summary>
        static Texture2D MakePaperTile()
        {
            const int T = 512;
            var px = new Color[T * T];
            var rng = new System.Random(23);
            float R() => (float)rng.NextDouble();
            // 되풀이 잡음: 격자 값을 T 에서 감아 이음매를 없앤다
            float[] Lattice(int n) { var a = new float[n * n]; for (int i = 0; i < a.Length; i++) a[i] = R(); return a; }
            float Sample(float[] a, int n, float x, float y)
            {
                float fx = x * n / T, fy = y * n / T;
                int x0 = (int)fx, y0 = (int)fy;
                float tx = fx - x0, ty = fy - y0;
                tx = tx * tx * (3f - 2f * tx); ty = ty * ty * (3f - 2f * ty);
                int x1 = (x0 + 1) % n, y1 = (y0 + 1) % n; x0 %= n; y0 %= n;
                return Mathf.Lerp(Mathf.Lerp(a[y0 * n + x0], a[y0 * n + x1], tx), Mathf.Lerp(a[y1 * n + x0], a[y1 * n + x1], tx), ty);
            }
            var n1 = Lattice(6); var n2 = Lattice(19); var n3 = Lattice(64);
            var bg = (Color)new Color32(0xE7, 0xDD, 0xC5, 255);
            for (int y = 0; y < T; y++)
                for (int x = 0; x < T; x++)
                {
                    float v = (Sample(n1, 6, x, y) - 0.5f) * 0.05f + (Sample(n2, 19, x, y) - 0.5f) * 0.035f + (Sample(n3, 64, x, y) - 0.5f) * 0.03f;
                    px[y * T + x] = new Color(bg.r + v, bg.g + v * 0.98f, bg.b + v * 0.9f, 1f);
                }
            void Blend(int x, int y, Color col)
            {
                x = ((x % T) + T) % T; y = ((y % T) + T) % T;
                int i = y * T + x;
                px[i] = Color.Lerp(px[i], new Color(col.r, col.g, col.b, 1f), col.a);
            }
            // 섬유: 짧고 가는 획. 어두운 것 셋에 밝은 것 하나
            for (int i = 0; i < 900; i++)
            {
                bool light = i % 4 == 0;
                var col = light ? new Color(1f, 0.99f, 0.95f, 0.10f + R() * 0.10f) : new Color(0.42f, 0.36f, 0.26f, 0.04f + R() * 0.07f);
                float x = R() * T, y = R() * T, ang = R() * Mathf.PI, len = 5f + R() * R() * 34f, bend = (R() - 0.5f) * 0.05f;
                for (float t = 0; t < len; t += 0.8f)
                {
                    Blend(Mathf.RoundToInt(x), Mathf.RoundToInt(y), col);
                    x += Mathf.Cos(ang) * 0.8f; y += Mathf.Sin(ang) * 0.8f; ang += bend;
                }
            }
            // 티
            for (int i = 0; i < 260; i++)
                Blend((int)(R() * T), (int)(R() * T), new Color(0.35f, 0.3f, 0.22f, 0.06f + R() * 0.12f));
            var tex = new Texture2D(T, T, TextureFormat.RGBA32, true) { name = "선지 바닥", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 2 };
            tex.SetPixels(px);
            tex.Apply(true, true);
            return tex;
        }

        /// <summary>가장자리 어둡게: 먹빛(#1C1A17), 가운데 0 → 모서리 0.6. 판 한 장을 화면 크기로 깐다.</summary>
        static Texture2D MakeVignette()
        {
            const int T = 128;
            var px = new Color[T * T];
            var ink = (Color)new Color32(0x1C, 0x1A, 0x17, 255);
            for (int y = 0; y < T; y++)
                for (int x = 0; x < T; x++)
                {
                    float u = (x + 0.5f) / T * 2f - 1f, v = (y + 0.5f) / T * 2f - 1f;
                    float d = Mathf.Sqrt(u * u + v * v) / 1.4142f;          // 모서리에서 1
                    float a = Mathf.SmoothStep(0.45f, 1f, d);
                    px[y * T + x] = new Color(ink.r, ink.g, ink.b, a * a * 0.6f + a * 0.0f);
                }
            var tex = new Texture2D(T, T, TextureFormat.RGBA32, false) { name = "가장자리 어둡게", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.SetPixels(px);
            tex.Apply(false, true);
            return tex;
        }

        /// <summary>(옛 바닥) 웹 시안 groundTile: 한지 빛 바닥, 64px 벽돌 줄눈, 풀 획, 붉은 점. 256px 한 칸. FL-01U 뒤로는 안 쓴다.</summary>
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
