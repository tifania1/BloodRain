using UnityEngine;
using UnityEngine.UI;

namespace BloodRain.Presentation.Meta
{
    /// <summary>먹선 원(UI). dashed 면 끊어 그린다.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class InkRing : MaskableGraphic
    {
        public float radius = 50f;
        public float width = 2f;
        public bool dashed;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            int n = Mathf.Clamp(Mathf.RoundToInt(radius * 0.8f), 48, 256);
            float r0 = radius - width * 0.5f, r1 = radius + width * 0.5f;
            for (int i = 0; i < n; i++)
            {
                if (dashed && (i / 3) % 2 == 1) continue;
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                var d0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0));
                var d1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));
                int k = vh.currentVertCount;
                vh.AddVert(d0 * r0, color, Vector2.zero);
                vh.AddVert(d0 * r1, color, Vector2.zero);
                vh.AddVert(d1 * r1, color, Vector2.zero);
                vh.AddVert(d1 * r0, color, Vector2.zero);
                vh.AddTriangle(k, k + 1, k + 2);
                vh.AddTriangle(k, k + 2, k + 3);
            }
        }
    }
}
