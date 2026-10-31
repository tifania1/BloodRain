using System.Collections.Generic;
using BloodRain.Sim;
using UnityEngine;

namespace BloodRain.Presentation.Fx
{
    // 지형 단계 1, 소품 층 그리기(TU-07). 자리는 Sim.PropField, 그리는 규칙은 웹 시안 cheonha.html 과 같다.
    //   - 화면에서 2픽셀보다 작으면 안 그림, 2~6픽셀은 옅게 들어옴
    //   - 화면 짧은 변의 0.4배보다 커진 소품(마당의 전각 등)은 옅게(최소 0.25)
    //   - 산은 화면 대각선 140m→700m 사이에서 0→0.6 으로 떠오름
    // 소품 그림이 아직 없어 웹 시안의 도형 소품(나무·집·전각·산 세 벌씩)을 코드로 그린 임시 텍스처를 쓴다.
    // 종류·벌마다 재질 하나, Graphics.RenderMeshInstanced 로 그린다. 그리는 순서는 먼 것(지도 위쪽) 먼저.
    public sealed class PropLayer : MonoBehaviour
    {
        const int Batch = 1023;
        const int Kinds = 4, Variants = 3;
        static readonly int IdTint = Shader.PropertyToID("_Tint");
        static readonly int IdDissolve = Shader.PropertyToID("_Dissolve");

        [Tooltip("ClusterInk 재질(덩이 렌더러와 같은 셰이더). 종류마다 복사해 텍스처만 바꾼다")]
        public Material material;
        public int seed = 7;
        [Tooltip("이 지역 한가운데가 Unity (0,0). 비우면 WorldCoords.Origin 을 그대로 쓴다")]
        public string originRegion = "";

        PropField _field;
        Material[] _mats;
        Mesh _quad;
        MaterialPropertyBlock _mpb;
        readonly List<int> _query = new List<int>();
        readonly List<Matrix4x4>[] _m = new List<Matrix4x4>[Kinds * Variants];
        readonly List<Vector4>[] _t = new List<Vector4>[Kinds * Variants];
        readonly Matrix4x4[] _mBuf = new Matrix4x4[Batch];
        readonly Vector4[] _tBuf = new Vector4[Batch];
        readonly float[] _dBuf = new float[Batch];

        public PropField Field => _field;
        public int LastDrawn { get; private set; }
        public int LastQueried { get; private set; }

        void Start()
        {
            var regions = new BloodRain.Core.World.Regions(DataBoot.Data);
            if (!string.IsNullOrEmpty(originRegion)) WorldCoords.Origin = regions.Center(originRegion);
            _field = new PropField(regions, seed);
            _quad = MakeQuad();
            _mpb = new MaterialPropertyBlock();
            _mats = new Material[Kinds * Variants];
            for (int k = 0; k < Kinds; k++)
                for (int v = 0; v < Variants; v++)
                {
                    var m = new Material(material) { name = $"소품 {(PropKind)k} {v}", renderQueue = 2990 };
                    m.mainTexture = PropTextures.Make((PropKind)k, v);
                    _mats[k * Variants + v] = m;
                }
            for (int i = 0; i < _m.Length; i++) { _m[i] = new List<Matrix4x4>(); _t[i] = new List<Vector4>(); }
            for (int i = 0; i < _dBuf.Length; i++) _dBuf[i] = 0f;
            Debug.Log($"[소품 층] {_field.Props.Count}개 (산·전각 {_field.Landmarks.Count})");
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (_field == null || cam == null || !cam.orthographic) return;

            float h = cam.orthographicSize, w = h * cam.aspect;
            float pxPerM = cam.pixelHeight / (2f * h);
            float viewDiag = 2f * Mathf.Sqrt(w * w + h * h);
            float bigProp = Mathf.Min(cam.pixelWidth, cam.pixelHeight) * 0.4f;
            float aFar = Mathf.Clamp01((Mathf.Log(viewDiag) - Mathf.Log(140f)) / (Mathf.Log(700f) - Mathf.Log(140f)));

            Vector2 c = cam.transform.position;
            const float margin = 400f; // 산(최대 320m)이 화면 끝에 걸쳐도 잡히게
            var a = WorldCoords.FromUnity(new Vector2(c.x - w - margin, c.y + h + margin));
            var b = WorldCoords.FromUnity(new Vector2(c.x + w + margin, c.y - h - margin));
            _field.Query(a, b, _query);
            LastQueried = _query.Count;

            for (int i = 0; i < _m.Length; i++) { _m[i].Clear(); _t[i].Clear(); }
            int drawn = 0;
            var props = _field.Props;
            foreach (int i in _query)
            {
                var p = props[i];
                float size = p.S * pxPerM;
                if (size < 2f) continue;
                float alpha = 1f;
                if (p.Kind == PropKind.Mount) alpha = aFar * 0.6f;
                else if (size < 6f) alpha = (size - 2f) / 4f;
                else if (size > bigProp) alpha = Mathf.Clamp(1f - (size - bigProp) / bigProp, 0.25f, 1f);
                if (alpha <= 0.02f) continue;

                var u = WorldCoords.ToUnity(p.Pos);
                // 웹은 발밑이 그림 아래에서 0.15 위(그림 위끝이 자리 - 0.85×크기). Unity 는 y 가 위로라 가운데 = 자리 + 0.35×크기
                float s = p.S;
                if (u.x + s < c.x - w || u.x - s > c.x + w || u.y - s > c.y + h || u.y + s < c.y - h) continue;
                int slot = (int)p.Kind * Variants + p.Variant;
                _m[slot].Add(Matrix4x4.TRS(new Vector3(u.x, u.y + s * 0.35f, 0.5f), Quaternion.identity, new Vector3(s, s, 1f)));
                _t[slot].Add(new Vector4(1f, 1f, 1f, alpha));
                drawn++;
            }
            LastDrawn = drawn;

            // 산 → 전각 → 집 → 나무 순으로(큰 것이 뒤에)
            int[] order = { (int)PropKind.Mount, (int)PropKind.Temple, (int)PropKind.House, (int)PropKind.Tree };
            foreach (int k in order)
                for (int v = 0; v < Variants; v++)
                {
                    int slot = k * Variants + v;
                    var ms = _m[slot]; var ts = _t[slot];
                    var rp = new RenderParams(_mats[slot]) { matProps = _mpb, layer = gameObject.layer, worldBounds = new Bounds(new Vector3(c.x, c.y, 0.5f), new Vector3(1e6f, 1e6f, 1f)) };
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

        static Mesh MakeQuad()
        {
            var m = new Mesh { name = "소품" };
            m.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            m.RecalculateBounds();
            return m;
        }
    }

    /// <summary>웹 시안 propSprite 를 옮긴 임시 소품 그림. 좌표는 웹처럼 왼쪽 위 0,0(0~1)으로 적고 쓸 때 뒤집는다.</summary>
    static class PropTextures
    {
        static readonly Color Ink = new Color32(26, 26, 26, 255);

        public static Texture2D Make(PropKind kind, int v)
        {
            int P = kind == PropKind.Mount ? 256 : 96;
            var px = new Color[P * P];
            var rng = new System.Random(kind.ToString().Length * 13 + v * 5 + 3);
            float R() => (float)rng.NextDouble();

            void Blend(int x, int y, Color c)
            {
                if (x < 0 || y < 0 || x >= P || y >= P) return;
                int i = (P - 1 - y) * P + x;
                var d = px[i];
                float a = c.a + d.a * (1 - c.a);
                px[i] = a <= 0 ? Color.clear : new Color((c.r * c.a + d.r * d.a * (1 - c.a)) / a, (c.g * c.a + d.g * d.a * (1 - c.a)) / a, (c.b * c.a + d.b * d.a * (1 - c.a)) / a, a);
            }
            void Rect(float x, float y, float w, float h, Color c)
            {
                for (int yy = (int)(y * P); yy < (int)((y + h) * P); yy++)
                    for (int xx = (int)(x * P); xx < (int)((x + w) * P); xx++) Blend(xx, yy, c);
            }
            void Outline(float x, float y, float w, float h, float lw, Color c)
            {
                Rect(x, y, w, lw, c); Rect(x, y + h - lw, w, lw, c); Rect(x, y, lw, h, c); Rect(x + w - lw, y, lw, h, c);
            }
            void Ellipse(float cx, float cy, float rx, float ry, Color c)
            {
                for (int yy = (int)((cy - ry) * P); yy <= (int)((cy + ry) * P); yy++)
                    for (int xx = (int)((cx - rx) * P); xx <= (int)((cx + rx) * P); xx++)
                    {
                        float dx = ((xx + 0.5f) / P - cx) / rx, dy = ((yy + 0.5f) / P - cy) / ry;
                        if (dx * dx + dy * dy <= 1f) Blend(xx, yy, c);
                    }
            }
            // 위끝이 곡선(가운데가 높은)인 지붕: 아래 y0, 가운데 꼭대기 y0-hump, 좌우 끝 x0~x1
            void Roof(float x0, float x1, float yBase, float hump, Color c)
            {
                for (int xx = (int)(x0 * P); xx < (int)(x1 * P); xx++)
                {
                    float s = ((xx + 0.5f) / P - x0) / (x1 - x0);
                    float top = yBase - hump * 4f * s * (1 - s);
                    for (int yy = (int)(top * P); yy < (int)(yBase * P); yy++) Blend(xx, yy, c);
                }
            }

            switch (kind)
            {
                case PropKind.Tree:
                    Rect(0.47f, 0.55f, 0.06f, 0.35f, Ink);
                    for (int i = 0; i < 7; i++)
                    {
                        var col = i % 3 != 0 ? new Color32(0x3E, 0x5A, 0x3A, 255) : new Color32(0x2F, 0x46, 0x30, 255);
                        Color cc = col; cc.a = 0.55f + R() * 0.3f;
                        Ellipse(0.5f + (R() - 0.5f) * 0.4f, 0.38f + (R() - 0.5f) * 0.3f, 0.16f + R() * 0.1f, 0.12f + R() * 0.08f, cc);
                    }
                    break;
                case PropKind.House:
                    Rect(0.2f, 0.5f, 0.6f, 0.36f, new Color32(0xD9, 0xCD, 0xB0, 255));
                    Outline(0.2f, 0.5f, 0.6f, 0.36f, 0.03f, Ink);
                    Roof(0.08f, 0.92f, 0.54f, 0.2f + v * 0.03f, new Color32(0x3A, 0x3A, 0x3A, 255));
                    Rect(0.44f, 0.66f, 0.12f, 0.2f, Ink);
                    break;
                case PropKind.Temple:
                    for (int k = 0; k < 4; k++)
                    {
                        float y = 0.82f - k * 0.17f, w = 0.7f - k * 0.12f;
                        Color fill = k % 2 == 1 ? (Color)new Color32(0x8A, 0x3A, 0x2E, 255) : new Color32(0xD9, 0xCD, 0xB0, 255);
                        Rect(0.5f - w / 2, y - 0.1f, w, 0.1f, fill);
                        Outline(0.5f - w / 2, y - 0.1f, w, 0.1f, 0.025f, Ink);
                        Roof(0.5f - w / 2 - 0.08f, 0.5f + w / 2 + 0.08f, y - 0.1f, 0.1f, new Color32(0x2A, 0x2A, 0x2A, 255));
                    }
                    break;
                default: // 산: 마른 붓 세 겹
                    for (int k = 0; k < 3; k++)
                    {
                        float bse = 0.95f - k * 0.04f, peak = 0.12f + k * 0.14f + R() * 0.08f, cx = 0.5f + (R() - 0.5f) * 0.3f, hw = 0.46f - k * 0.08f;
                        var col = new Color((40 + k * 30) / 255f, (50 + k * 30) / 255f, (48 + k * 28) / 255f, 0.55f - k * 0.12f);
                        var jitter = new float[13];
                        for (int i = 0; i <= 12; i++) jitter[i] = 0.85f + R() * 0.3f;
                        for (int xx = (int)((cx - hw) * P); xx < (int)((cx + hw) * P); xx++)
                        {
                            float s = ((xx + 0.5f) / P - (cx - hw)) / (hw * 2);
                            if (s < 0 || s > 1) continue;
                            float fi = s * 12; int i0 = Mathf.Min(11, (int)fi);
                            float j = Mathf.Lerp(jitter[i0], jitter[i0 + 1], fi - i0);
                            float top = bse - (bse - peak) * Mathf.Pow(Mathf.Sin(s * Mathf.PI), 1.4f) * j;
                            for (int yy = (int)(top * P); yy < (int)(bse * P); yy++) Blend(xx, yy, col);
                        }
                    }
                    break;
            }

            var tex = new Texture2D(P, P, TextureFormat.RGBA32, true) { name = $"임시 소품 {kind} {v}", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            tex.SetPixels(px);
            tex.Apply(true, true);
            return tex;
        }
    }
}
