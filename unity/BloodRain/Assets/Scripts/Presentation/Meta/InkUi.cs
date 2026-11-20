using UnityEngine;
using UnityEngine.UI;

namespace BloodRain.Presentation.Meta
{
    // 메뉴 화면(객잔·정산 등)이 같이 쓰는 종이·붓 조각(2026-11-21). 납작한 단색 판에 선지 결·먹 테두리·붓 획을 얹는다.
    //   종이 결과 가장자리 어둡게는 판 바닥(MapLayer)과 같은 텍스처를 나눠 쓴다.
    public static class InkUi
    {
        static readonly Color Ink = new Color(0.12f, 0.10f, 0.09f);

        /// <summary>선지 결 한 장(RawImage). size 는 부모 기준 크기, 결 크기는 512px 한 장이 화면 512px 이 되게.</summary>
        public static RawImage PaperGrain(RectTransform parent, Vector2 pos, Vector2 size, Color tint)
        {
            var r = Raw(parent, "PaperGrain", BloodRain.Presentation.Fx.MapLayer.PaperTile, tint);
            Place(r.rectTransform, pos, size);
            r.uvRect = new Rect(0f, 0f, size.x / 512f, size.y / 512f);
            return r;
        }

        /// <summary>부모를 꽉 채우는 선지 결(버튼 칸 같은 데 옅게 얹을 때).</summary>
        public static RawImage PaperGrainFill(RectTransform parent, Vector2 size, float alpha)
        {
            var r = Raw(parent, "PaperGrain", BloodRain.Presentation.Fx.MapLayer.PaperTile, new Color(1f, 1f, 1f, alpha));
            var rt = r.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            r.uvRect = new Rect(0f, 0f, size.x / 512f, size.y / 512f);
            return r;
        }

        /// <summary>안쪽으로 inset 들어간 가는 먹 테두리(네 변).</summary>
        public static void Frame(RectTransform parent, Vector2 pos, Vector2 size, float inset, float width, Color c)
        {
            float w = size.x - inset * 2f, h = size.y - inset * 2f;
            Bar(parent, pos + new Vector2(0f, h * 0.5f), new Vector2(w, width), c);
            Bar(parent, pos - new Vector2(0f, h * 0.5f), new Vector2(w, width), c);
            Bar(parent, pos + new Vector2(w * 0.5f, 0f), new Vector2(width, h), c);
            Bar(parent, pos - new Vector2(w * 0.5f, 0f), new Vector2(width, h), c);
        }

        /// <summary>가로 붓 획 한 줄(양끝이 가늘고 가장자리가 거칢, 끝에 비백).</summary>
        public static RawImage Stroke(RectTransform parent, Vector2 pos, Vector2 size, Color c)
        {
            var r = Raw(parent, "BrushStroke", BrushStroke, c);
            Place(r.rectTransform, pos, size);
            return r;
        }

        static void Bar(RectTransform parent, Vector2 pos, Vector2 size, Color c)
        {
            var go = new GameObject("InkLine", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>(); img.color = c; img.raycastTarget = false;
            Place((RectTransform)go.transform, pos, size);
        }

        static RawImage Raw(RectTransform parent, string name, Texture tex, Color c)
        {
            var r = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
            r.transform.SetParent(parent, false);
            r.texture = tex; r.color = c; r.raycastTarget = false;
            return r;
        }

        static void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static Texture2D _brush;
        /// <summary>가로 붓 획 텍스처(흰색 알파 — 색은 RawImage 가 입힌다).</summary>
        public static Texture2D BrushStroke
        {
            get
            {
                if (_brush != null) return _brush;
                const int W = 512, H = 32;
                var px = new Color[W * H];
                var rng = new System.Random(5);
                float[] edgeT = new float[W], edgeB = new float[W];
                float et = 0f, eb = 0f;
                for (int x = 0; x < W; x++) { et = Mathf.Clamp(et + ((float)rng.NextDouble() - 0.5f) * 0.12f, -0.25f, 0.25f); eb = Mathf.Clamp(eb + ((float)rng.NextDouble() - 0.5f) * 0.12f, -0.25f, 0.25f); edgeT[x] = et; edgeB[x] = eb; }
                float[] gaps = new float[H];
                for (int y = 0; y < H; y++) gaps[y] = (float)rng.NextDouble();
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)(W - 1);
                    // 들어갈 때 눌렀다가 끝으로 빠지며 가늘어짐
                    float thick = Mathf.Sin(Mathf.PI * Mathf.Pow(u, 0.7f)) * (u < 0.08f ? 1.15f : 1f);
                    for (int y = 0; y < H; y++)
                    {
                        float v = (y + 0.5f) / H * 2f - 1f;
                        float top = thick * (0.8f + edgeT[x]), bot = -thick * (0.8f + edgeB[x]);
                        float a = Mathf.Clamp01((top - v) * 10f) * Mathf.Clamp01((v - bot) * 10f);
                        if (u > 0.55f && gaps[y] > 1.1f - u * 0.6f) a *= 0.25f; // 비백
                        px[y * W + x] = new Color(1f, 1f, 1f, a);
                    }
                }
                _brush = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = "BrushStroke", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                _brush.SetPixels(px);
                _brush.Apply(false, true);
                return _brush;
            }
        }
    }
}
