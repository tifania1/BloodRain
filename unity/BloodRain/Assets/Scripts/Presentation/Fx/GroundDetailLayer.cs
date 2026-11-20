using System.Collections.Generic;
using UnityEngine;

namespace BloodRain.Presentation.Fx
{
    // 바닥 잔무늬(2026-11-21, FL-01U 남은 것 '드문 먹 얼룩·풀·돌'). 발밑 바닥이 한 장 타일이라 넓은 데가 밋밋했다.
    //   세계를 Cell m 칸으로 나눠 칸마다 해시로 0~1개: 풀포기·잔돌·옅은 먹 번짐·꽃잎·마른 잎. 지역(MapLayer.GroundRegion) 결에 따라
    //   무엇이 얼마나 나올지 다르다(설원엔 풀이 없고, 화산엔 매화 꽃잎, 당가타엔 대나무 잎).
    //   크기가 세계 m 로 고정이라 경지가 오를수록 작아지고, 화면에서 7픽셀보다 작으면 안 그린다(이류 끝쯤 저절로 사라짐).
    //   그리는 차례: 바닥(2980~2983) 뒤, 소품(2990) 앞 — 2986. 그림은 코드로 그린 128px 여덟 칸(흰 바탕 알파, 색은 _Tint 가 곱함).
    //   PropLayer 가 Start 에서 붙인다(씬을 안 고친다).
    public sealed class GroundDetailLayer : MonoBehaviour
    {
        public Material material;

        const float Cell = 2.4f;     // m
        const int Kinds = 8;         // 0·1 풀포기, 2·3 잔돌, 4·5 먹 번짐, 6 꽃잎, 7 마른 잎
        const int Batch = 1023;
        const float MinPx = 7f;      // 이보다 작게 보이면 안 그린다
        static readonly int IdTint = Shader.PropertyToID("_Tint");
        static readonly int IdDissolve = Shader.PropertyToID("_Dissolve");

        // 지역 → 칸에 무엇이 나올 몫(풀, 돌, 먹, 꽃잎, 잎). 합이 그 칸에 무엇이든 나올 몫
        sealed class Mix { public float Grass, Stone, Ink, Petal, Leaf; public Color GrassC = new Color(0.36f, 0.42f, 0.33f); }
        static readonly Dictionary<string, Mix> Mixes = new Dictionary<string, Mix>
        {
            ["cheongun"] = new Mix { Grass = 0.20f, Stone = 0.20f, Ink = 0.06f, Leaf = 0.05f },
            ["songshan"] = new Mix { Grass = 0.12f, Stone = 0.10f, Ink = 0.05f, Leaf = 0.10f, GrassC = new Color(0.34f, 0.44f, 0.34f) },
            ["gaebang_hq"] = new Mix { Grass = 0.10f, Stone = 0.24f, Ink = 0.10f, Leaf = 0.08f },
            ["namgung_manor"] = new Mix { Grass = 0.08f, Stone = 0.10f, Ink = 0.04f, Leaf = 0.06f },
            ["danggata"] = new Mix { Grass = 0.16f, Stone = 0.06f, Ink = 0.05f, Leaf = 0.24f, GrassC = new Color(0.30f, 0.44f, 0.30f) },
            ["janggang"] = new Mix { Grass = 0.30f, Stone = 0.18f, Ink = 0.04f, GrassC = new Color(0.38f, 0.46f, 0.38f) },
            ["wudangsan"] = new Mix { Grass = 0.16f, Stone = 0.14f, Ink = 0.04f, Leaf = 0.06f },
            ["huashan"] = new Mix { Grass = 0.06f, Stone = 0.18f, Ink = 0.04f, Petal = 0.20f },
            ["kunlun"] = new Mix { Stone = 0.12f, Ink = 0.03f },
            ["sipmandaesan"] = new Mix { Grass = 0.22f, Stone = 0.12f, Ink = 0.12f, Leaf = 0.06f, GrassC = new Color(0.24f, 0.30f, 0.24f) },
            ["bongmagok"] = new Mix { Grass = 0.06f, Stone = 0.16f, Ink = 0.16f },
        };
        static readonly Mix Default = new Mix { Grass = 0.16f, Stone = 0.16f, Ink = 0.06f, Leaf = 0.04f };

        static readonly Color StoneC = new Color(0.62f, 0.60f, 0.57f);
        static readonly Color InkC = new Color(0.12f, 0.10f, 0.09f, 0.22f);
        static readonly Color PetalC = new Color(0.94f, 0.66f, 0.72f);
        static readonly Color LeafC = new Color(0.58f, 0.48f, 0.30f);

        Material[] _mats;
        Mesh _quad;
        MaterialPropertyBlock _mpb;
        MapLayer _map;
        readonly List<Matrix4x4>[] _m = new List<Matrix4x4>[Kinds];
        readonly List<Vector4>[] _t = new List<Vector4>[Kinds];
        readonly Matrix4x4[] _mBuf = new Matrix4x4[Batch];
        readonly Vector4[] _tBuf = new Vector4[Batch];
        readonly float[] _dBuf = new float[Batch];
        public int LastDrawn { get; private set; }

        void Start()
        {
            if (material == null) { enabled = false; return; }
            _map = FindFirstObjectByType<MapLayer>();
            _quad = MakeQuad();
            _mpb = new MaterialPropertyBlock();
            var atlas = Atlas;
            _mats = new Material[Kinds];
            for (int k = 0; k < Kinds; k++)
            {
                _mats[k] = new Material(material) { name = "바닥 잔무늬 " + k, renderQueue = 2986, enableInstancing = true, mainTexture = atlas };
                _mats[k].mainTextureScale = new Vector2(0.25f, 0.5f);
                _mats[k].mainTextureOffset = new Vector2((k % 4) * 0.25f, (k / 4) * 0.5f);
                _m[k] = new List<Matrix4x4>(); _t[k] = new List<Vector4>();
            }
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (_mats == null || cam == null || !cam.orthographic || FxSettings.Simplified) { LastDrawn = 0; return; }
            float h = cam.orthographicSize, w = h * cam.aspect;
            float pxPerM = cam.pixelHeight / (2f * h);
            if (0.9f * pxPerM < MinPx) { LastDrawn = 0; return; } // 가장 큰 것도 너무 작다(일류 이상)
            var mix = _map != null && _map.GroundRegion != null && Mixes.TryGetValue(_map.GroundRegion, out var mm) ? mm : Default;
            float total = mix.Grass + mix.Stone + mix.Ink + mix.Petal + mix.Leaf;

            for (int k = 0; k < Kinds; k++) { _m[k].Clear(); _t[k].Clear(); }
            Vector2 c = cam.transform.position;
            int x0 = Mathf.FloorToInt((c.x - w - 1f) / Cell), x1 = Mathf.FloorToInt((c.x + w + 1f) / Cell);
            int y0 = Mathf.FloorToInt((c.y - h - 1f) / Cell), y1 = Mathf.FloorToInt((c.y + h + 1f) / Cell);
            if ((x1 - x0 + 1) * (y1 - y0 + 1) > 6000) { LastDrawn = 0; return; }
            int drawn = 0;
            for (int iy = y0; iy <= y1; iy++)
                for (int ix = x0; ix <= x1; ix++)
                {
                    uint hs = Hash(ix, iy);
                    float r0 = U(hs, 0);
                    if (r0 >= total) continue;
                    int kind; Color col; float size;
                    float pick = r0;
                    if ((pick -= mix.Grass) < 0f) { kind = (hs & 1) == 0 ? 0 : 1; col = mix.GrassC; size = 0.55f + 0.35f * U(hs, 1); }
                    else if ((pick -= mix.Stone) < 0f) { kind = (hs & 2) == 0 ? 2 : 3; col = StoneC; size = 0.45f + 0.35f * U(hs, 1); }
                    else if ((pick -= mix.Ink) < 0f) { kind = (hs & 4) == 0 ? 4 : 5; col = InkC; size = 0.9f + 0.9f * U(hs, 1); }
                    else if ((pick -= mix.Petal) < 0f) { kind = 6; col = PetalC; size = 0.5f + 0.3f * U(hs, 1); }
                    else { kind = 7; col = LeafC; size = 0.5f + 0.3f * U(hs, 1); }
                    if (size * pxPerM < MinPx) continue;
                    var p = new Vector2((ix + 0.15f + 0.7f * U(hs, 2)) * Cell, (iy + 0.15f + 0.7f * U(hs, 3)) * Cell);
                    if (p.x + size < c.x - w || p.x - size > c.x + w || p.y + size < c.y - h || p.y - size > c.y + h) continue;
                    // 작게 보일수록 옅게(7 → 14픽셀에서 들어옴), 결마다 밝기 조금씩
                    float a = Mathf.Clamp01((size * pxPerM - MinPx) / MinPx) * col.a;
                    float shade = 0.88f + 0.2f * U(hs, 4);
                    bool flip = (hs & 8) != 0;
                    // 풀·돌은 위가 위(돌리지 않음), 먹·꽃잎·잎은 아무 쪽으로
                    float rot = kind <= 3 ? 0f : U(hs, 5) * 360f;
                    _m[kind].Add(Matrix4x4.TRS(new Vector3(p.x, p.y, 0.55f), Quaternion.Euler(0f, 0f, rot), new Vector3(flip ? -size : size, size, 1f)));
                    _t[kind].Add(new Vector4(col.r * shade, col.g * shade, col.b * shade, a));
                    drawn++;
                }
            LastDrawn = drawn;
            for (int k = 0; k < Kinds; k++)
            {
                var ms = _m[k]; if (ms.Count == 0) continue;
                var ts = _t[k];
                var rp = new RenderParams(_mats[k]) { matProps = _mpb, layer = gameObject.layer, worldBounds = new Bounds(new Vector3(c.x, c.y, 0.55f), new Vector3(1e5f, 1e5f, 1f)) };
                for (int start = 0; start < ms.Count; start += Batch)
                {
                    int n = Mathf.Min(Batch, ms.Count - start);
                    ms.CopyTo(start, _mBuf, 0, n);
                    ts.CopyTo(start, _tBuf, 0, n);
                    _mpb.Clear();
                    _mpb.SetVectorArray(IdTint, _tBuf);
                    _mpb.SetFloatArray(IdDissolve, _dBuf);
                    Graphics.RenderMeshInstanced(rp, _quad, 0, _mBuf, n);
                }
            }
        }

        static uint Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + 0x9E3779B9u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }

        // 해시에서 0~1 값 여럿(같은 칸이면 늘 같은 값)
        static float U(uint h, int i)
        {
            unchecked
            {
                uint v = h * (2654435761u + (uint)i * 40503u) + (uint)i * 2246822519u;
                v ^= v >> 15; v *= 2246822519u; v ^= v >> 13;
                return (v & 0xFFFFFF) / 16777216f;
            }
        }

        static Mesh MakeQuad()
        {
            var m = new Mesh { name = "바닥 잔무늬" };
            m.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            m.RecalculateBounds();
            return m;
        }

        // ---- 그림(512x256, 128px 여덟 칸) ----
        static Texture2D _atlas;
        public static Texture2D Atlas
        {
            get
            {
                if (_atlas != null) return _atlas;
                const int W = 512, H = 256, S = 128;
                var v = new float[W * H]; var a = new float[W * H];
                var rng = new System.Random(11);
                float R() => (float)rng.NextDouble();

                void Put(int cx, int cy, float x, float y, float val, float alpha)
                {
                    int px = cx * S + (int)x, py = cy * S + (int)y;
                    if (x < 1 || y < 1 || x >= S - 1 || y >= S - 1) return;
                    int i = py * W + px;
                    if (alpha > a[i]) { v[i] = Mathf.Lerp(v[i], val, Mathf.Clamp01((alpha - a[i]) / Mathf.Max(0.001f, alpha)) ); a[i] = alpha; }
                }
                void Dot(int cx, int cy, float x, float y, float rad, float val, float alpha)
                {
                    int r = Mathf.CeilToInt(rad + 1);
                    for (int dy = -r; dy <= r; dy++)
                        for (int dx = -r; dx <= r; dx++)
                        {
                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            float k = Mathf.Clamp01(rad + 0.5f - d);
                            if (k > 0f) Put(cx, cy, x + dx, y + dy, val, alpha * k);
                        }
                }

                // 0·1 풀포기: 아래 가운데에서 부채꼴로 뻗는 가는 잎 여러 가닥(뿌리 쪽 짙고 끝이 옅음)
                for (int cell = 0; cell < 2; cell++)
                {
                    int blades = cell == 0 ? 9 : 13;
                    for (int b = 0; b < blades; b++)
                    {
                        float bx = 64 + (R() - 0.5f) * 26f, by = 18 + R() * 6f;
                        float lean = (R() - 0.5f) * 1.6f + (b - blades / 2f) * 0.09f;
                        float len = 46 + R() * 50f * (cell == 0 ? 0.8f : 1f);
                        float bend = (R() - 0.5f) * 30f;
                        for (int s = 0; s <= 50; s++)
                        {
                            float t = s / 50f;
                            float x = bx + Mathf.Sin(lean) * len * t + bend * t * t;
                            float y = by + Mathf.Cos(lean) * len * t * 0.95f;
                            float rad = Mathf.Lerp(2.6f, 0.5f, t);
                            Dot(cell, 0, x, y, rad, Mathf.Lerp(0.45f, 1f, t), Mathf.Lerp(0.95f, 0.55f, t));
                        }
                    }
                }
                // 2·3 잔돌: 둥근 돌 셋~넷, 위가 밝고 아래 테가 먹
                for (int cell = 2; cell < 4; cell++)
                {
                    int cx = cell % 4;
                    int n = cell == 2 ? 3 : 5;
                    for (int s = 0; s < n; s++)
                    {
                        float ox = 30 + R() * 68f, oy = 30 + R() * 60f;
                        float rx = (s == 0 ? 22f : 9f + R() * 10f), ry = rx * (0.6f + R() * 0.25f);
                        float rot = R() * Mathf.PI;
                        float cs = Mathf.Cos(rot), sn = Mathf.Sin(rot);
                        int r = Mathf.CeilToInt(rx + 2);
                        for (int dy = -r; dy <= r; dy++)
                            for (int dx = -r; dx <= r; dx++)
                            {
                                float lx = (dx * cs + dy * sn) / rx, ly = (-dx * sn + dy * cs) / ry;
                                float d = Mathf.Sqrt(lx * lx + ly * ly);
                                if (d > 1.08f) continue;
                                float edge = Mathf.Clamp01((d - 0.78f) / 0.2f);
                                float light = 0.95f - 0.25f * Mathf.Clamp01(0.5f - dy / (2f * ry)); // 위(+y)가 밝다
                                float val = Mathf.Lerp(light, 0.22f, edge);
                                float al = Mathf.Clamp01((1.08f - d) / 0.08f);
                                Put(cx, 0, ox + dx, oy + dy, val, al);
                            }
                    }
                }
                // 4·5 먹 번짐: 들쭉날쭉한 덩이, 가장자리에 먹이 고임
                for (int cell = 4; cell < 6; cell++)
                {
                    int cx = cell % 4, cy = 1;
                    float ph1 = R() * 6.28f, ph2 = R() * 6.28f;
                    for (int y = 4; y < S - 4; y++)
                        for (int x = 4; x < S - 4; x++)
                        {
                            float dx = (x - 64f) / 54f, dy = (y - 64f) / 54f;
                            float ang = Mathf.Atan2(dy, dx), d = Mathf.Sqrt(dx * dx + dy * dy);
                            float rr = 0.78f + 0.12f * Mathf.Sin(ang * 3f + ph1) + 0.08f * Mathf.Sin(ang * 7f + ph2) + 0.05f * Mathf.Sin(ang * 13f);
                            if (d > rr) continue;
                            float k = d / rr;
                            float al = Mathf.Lerp(0.45f, 1f, k * k) * Mathf.Clamp01((rr - d) / 0.05f);
                            Put(cx, cy, x, y, 1f, al);
                        }
                }
                // 6 꽃잎: 끝이 살짝 파인 물방울 너덧 장
                for (int s = 0; s < 5; s++)
                {
                    float ox = 26 + R() * 76f, oy = 26 + R() * 76f, ang = R() * 6.28f, L = 12 + R() * 6f;
                    float cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                    for (int dy = -20; dy <= 20; dy++)
                        for (int dx = -20; dx <= 20; dx++)
                        {
                            float lx = (dx * cs + dy * sn) / L, ly = (-dx * sn + dy * cs) / (L * 0.62f);
                            float w = 1f - lx * lx; if (w <= 0f) continue;
                            float half = Mathf.Sqrt(w) * (0.55f + 0.45f * (lx + 1f) * 0.5f);
                            if (Mathf.Abs(ly) > half) continue;
                            if (lx > 0.8f && Mathf.Abs(ly) < 0.12f) continue; // 끝 파임
                            float val = 1f - 0.18f * Mathf.Clamp01(1f - Mathf.Abs(ly) / 0.15f); // 가운데 결
                            Put(2, 1, ox + dx, oy + dy, val, 0.92f);
                        }
                }
                // 7 마른 잎: 길쭉한 잎 둘~셋과 잎맥
                for (int s = 0; s < 3; s++)
                {
                    float ox = 34 + R() * 60f, oy = 34 + R() * 60f, ang = R() * 6.28f, L = 22 + R() * 10f;
                    float cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                    for (int dy = -34; dy <= 34; dy++)
                        for (int dx = -34; dx <= 34; dx++)
                        {
                            float lx = (dx * cs + dy * sn) / L, ly = (-dx * sn + dy * cs) / (L * 0.32f);
                            float w = 1f - lx * lx; if (w <= 0f) continue;
                            if (Mathf.Abs(ly) > Mathf.Sqrt(w)) continue;
                            float val = Mathf.Abs(ly) < 0.12f ? 0.55f : 0.9f + 0.1f * lx;
                            Put(3, 1, ox + dx, oy + dy, val, 0.9f);
                        }
                }

                var px = new Color[W * H];
                for (int i = 0; i < px.Length; i++) px[i] = new Color(v[i], v[i], v[i], a[i]);
                _atlas = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = "바닥 잔무늬", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                _atlas.SetPixels(px);
                _atlas.Apply(true, true);
                return _atlas;
            }
        }
    }
}
