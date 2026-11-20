using UnityEngine;
using UnityEngine.UI;

namespace BloodRain.Presentation.Hud
{
    // 화면 혈우(TU-09). 지금 선 자리의 혈운 농도만큼 빗줄기가 굵고 많아진다.
    // 웹 시안(web/lookdev/cheonha.html '혈우(화면)')의 식을 그대로 옮겼다. 단위는 캔버스 기준 픽셀.
    //   빗줄기 수 = round(30 + 26 × 농도), 길이 = 12 + 3 × 농도, 굵기 = 0.6 + 0.55 × 농도,
    //   불투명도 = min(0.42, 0.13 + 0.05 × 농도), 주홍 #B3261E 하나.
    // 농도는 RegionHud 가 넣는다(지역 사이에선 Core 가 섞은 값). 시계는 언스케일(돌파 중에도 내린다).
    // BT-01U 돌파 중엔 몸에서부터 빗줄기가 둥글게 갈라진다(시안 breakthrough.html: 반지름 = 화면 대각선 × 0.55 × 줌 진행,
    //   가장자리 0.12 대각선 안에서 옅어지며 바깥으로 밀림). 끝에 비가 한꺼번에 돌아오지 않게 줌 진행 0.8 부터 0.2 동안 다시 닫는다.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ScreenRain : MaskableGraphic
    {
        [Tooltip("혈운 농도. RegionHud 가 매 프레임 넣는다")]
        public float density = 1f;
        [Tooltip("굵기에 곱하는 값(웹은 CSS 픽셀이라 큰 화면에서 가늘어 보이면 올린다)")]
        public float widthScale = 1.5f;

        static readonly Color32 Crimson = new Color32(179, 38, 30, 255);

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        void Update() => SetVerticesDirty();

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            float w = r.width, h = r.height;
            if (w <= 0f || h <= 0f) return;

            float d = Mathf.Max(0f, density);
            float now = Application.isPlaying ? Time.unscaledTime : 0f;
            int n = Mathf.RoundToInt(30f + 26f * d);
            float len = 12f + 3f * d;
            float half = (0.6f + 0.55f * d) * widthScale * 0.5f;
            var col = (Color)Crimson;
            col.a = Mathf.Min(0.42f, 0.13f + 0.05f * d) * color.a;

            // 빗줄기 방향(웹: x 로 -0.28·len, 아래로 len). 굵기는 그 수직 방향으로 편다
            var dir = new Vector2(-0.28f * len, -len);
            var nrm = new Vector2(-dir.y, dir.x).normalized * half;
            float part = PartRadius(w, h, out var center), edge = Mathf.Sqrt(w * w + h * h) * 0.12f;
            for (int i = 0; i < n; i++)
            {
                float x = Frac(i * 97.13f + now * 30f) * w + r.xMin;
                float yDown = Frac(i * 0.618f + now * (0.8f + 0.1f * d)) * h;
                var a = new Vector2(x, r.yMax - yDown);
                var c = col;
                if (part > 0f)
                {
                    var off = a - center;
                    float dd = off.magnitude;
                    float al = dd < part ? 0f : dd < part + edge ? (dd - part) / edge : 1f;
                    if (al <= 0f) continue;
                    if (dd > 1e-3f) a += off / dd * ((1f - al) * edge * 0.6f);
                    c.a *= al;
                }
                var b = a + dir;
                int k = vh.currentVertCount;
                vh.AddVert(a - nrm, c, Vector2.zero);
                vh.AddVert(a + nrm, c, Vector2.zero);
                vh.AddVert(b + nrm, c, Vector2.zero);
                vh.AddVert(b - nrm, c, Vector2.zero);
                vh.AddTriangle(k, k + 1, k + 2);
                vh.AddTriangle(k, k + 2, k + 3);
            }
        }

        static float Frac(float v) => v - Mathf.Floor(v);

        /// <summary>돌파 중이면 빗줄기가 갈라진 반지름(캔버스 px)과 몸 자리(이 그림의 로컬 좌표). 아니면 0.</summary>
        float PartRadius(float w, float h, out Vector2 center)
        {
            center = Vector2.zero;
            var lr = BloodRain.Presentation.LifeRunner.Instance;
            var fx = lr != null ? lr.breakthrough : null;
            if (fx == null || !fx.IsPlaying || fx.Core == null || lr.player == null) return 0f;
            float zp = fx.Core.ZoomProgress;
            float k = zp < 0.8f ? zp : zp * Mathf.Clamp01((1f - zp) / 0.2f);
            if (k <= 0f) return 0f;
            var cam = Camera.main;
            if (cam == null) return 0f;
            var cv = canvas;
            var uiCam = cv != null && cv.renderMode != RenderMode.ScreenSpaceOverlay ? cv.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, cam.WorldToScreenPoint(lr.player.position), uiCam, out center)) return 0f;
            return Mathf.Sqrt(w * w + h * h) * 0.55f * k;
        }
    }
}
