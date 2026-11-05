using BloodRain.Core.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BloodRain.Presentation.Hud
{
    // C-08a 음양 태극 문양 HUD(기획서 3장 '음양'). 화면 왼쪽 아래 태극 문양 하나가 게이지 값만큼 기운다.
    //   기울기: 값 / 최대(balance.json yinyang.max) × maxTiltDegrees. 양(+)은 오른쪽으로, 음(-)은 왼쪽으로.
    //   테두리: 태극 구간(gauge.HarmonyRange) 안이면 금빛, 치우침이면 먹빛, 극단(ExtremeFrom 이상)이면 핏빛으로 맥동.
    //   문양 그림은 코드로 굽는다(256 칸, 이미지에 글자 없음). 아래에 지금 값과 띠 이름을 작게.
    // 한 생(LifeRunner)의 Loadout.Gauge 를 본다. 진전 인장은 아직 LevelUpScreen 안에 있다(옮기는 건 UI-01U 때).
    public sealed class YinYangHud : MonoBehaviour
    {
        public TMP_FontAsset font;
        [Tooltip("값이 최대일 때 기우는 각도")]
        public float maxTiltDegrees = 90f;
        [Tooltip("기울기가 따라가는 빠르기(초당 비율)")]
        public float follow = 8f;

        static readonly Color Gold = new Color32(205, 160, 60, 255);
        static readonly Color InkRing = new Color(0.12f, 0.10f, 0.09f, 0.85f);
        static readonly Color Blood = new Color32(170, 22, 20, 255);

        RectTransform _root, _symbol;
        Image _ring, _vignette, _needle;
        RectTransform _needlePivot;
        TextMeshProUGUI _label;
        float _angle;

        void Start() => Build();

        void Build()
        {
            var go = new GameObject("음양 HUD", typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)go.transform;

            // C-08b 역류 중 화면 가장자리 핏빛 맥동(가운데가 빈 둥근 그러데이션)
            var vig = Sprite.Create(BakeVignette(128), new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f));
            _vignette = Img(_root, "역류 맥동", vig, new Color(0.6f, 0.03f, 0.02f, 0f));
            var vr = _vignette.rectTransform; vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one; vr.offsetMin = vr.offsetMax = Vector2.zero;

            var circle = Sprite.Create(BakeDisc(128), new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f));
            _ring = Img(_root, "테두리", circle, Gold);
            Place(_ring.rectTransform, new Vector2(110, 160), 150);
            var taiji = Sprite.Create(BakeTaiji(256), new Rect(0, 0, 256, 256), new Vector2(0.5f, 0.5f));
            var sym = Img(_root, "태극", taiji, Color.white);
            _symbol = sym.rectTransform;
            Place(_symbol, new Vector2(110, 160), 136);
            // C-08b 다음 역류 예고 바늘: 극성에 머무는 동안 문양 둘레를 한 바퀴 돌면 역류
            var pivot = new GameObject("바늘 축", typeof(RectTransform)).GetComponent<RectTransform>();
            pivot.SetParent(_root, false);
            Place(pivot, new Vector2(110, 160), 0);
            _needlePivot = pivot;
            _needle = Img(pivot, "바늘", null, Blood);
            var nr = _needle.rectTransform; nr.anchorMin = nr.anchorMax = new Vector2(0.5f, 0.5f); nr.pivot = new Vector2(0.5f, 0f);
            nr.anchoredPosition = new Vector2(0, 64); nr.sizeDelta = new Vector2(6, 22);

            var t = new GameObject("값", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            t.transform.SetParent(_root, false);
            if (font != null) t.font = font;
            t.fontSize = 22; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            _label = t;
            var lr = t.rectTransform; lr.anchorMin = lr.anchorMax = Vector2.zero; lr.pivot = new Vector2(0.5f, 0.5f);
            lr.anchoredPosition = new Vector2(110, 62); lr.sizeDelta = new Vector2(220, 30);
        }

        void Update()
        {
            var lr = LifeRunner.Instance;
            if (_root == null || lr == null || lr.Life == null) return;
            var gauge = lr.Life.Loadout.Gauge;
            var def = DataBoot.Data.Balance.Yinyang;
            float max = Mathf.Max(1, Mathf.Max(Mathf.Abs(def.Min), def.Max));
            float target = -gauge.Value / max * maxTiltDegrees; // 양(+) = 시계 방향(오른쪽)
            _angle = Mathf.Lerp(_angle, target, 1f - Mathf.Exp(-follow * Time.unscaledDeltaTime));
            _symbol.localEulerAngles = new Vector3(0, 0, _angle);

            var band = gauge.Band;
            Color c = band == YinYangBand.Harmony ? Gold : band == YinYangBand.Extreme ? Blood : InkRing;
            if (band == YinYangBand.Extreme)
            {
                float p = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
                c = Color.Lerp(Blood, new Color(1f, 0.25f, 0.2f), p * 0.5f);
                _ring.rectTransform.localScale = Vector3.one * (1f + 0.04f * p);
            }
            else _ring.rectTransform.localScale = Vector3.one;
            _ring.color = c;

            string name = band == YinYangBand.Harmony ? Loc.T("ui.yinyang.harmony")
                : gauge.Value > 0 ? Loc.T("ui.yinyang.yang") : Loc.T("ui.yinyang.yin");
            if (band == YinYangBand.Extreme) name = Loc.F("ui.yinyang.extreme", name);
            _label.text = $"{name} <size=80%>{gauge.Value:+0;-0;0} · ±{gauge.HarmonyRange}</size>";
            _label.color = c;

            // C-08b 기혈역류: 바늘(극성일 때만, 다음 역류까지), 역류 중엔 화면 가장자리 맥동. 끊기 창 동안 더 빠르게
            var b = lr.Life.Backlash;
            var ext = def.Extreme;
            bool showNeedle = band == YinYangBand.Extreme && !b.Immune;
            _needle.enabled = showNeedle;
            if (showNeedle)
            {
                float t = ext.BacklashIntervalSeconds > 0f ? 1f - b.SecondsUntilNext / ext.BacklashIntervalSeconds : 0f;
                _needlePivot.localEulerAngles = new Vector3(0, 0, -t * 360f);
            }
            float a = 0f;
            if (b.IsActive)
            {
                float speed = b.CanCancel ? 18f : 7f;
                a = 0.35f + 0.3f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * speed));
            }
            var vc = _vignette.color; vc.a = Mathf.MoveTowards(vc.a, a, Time.unscaledDeltaTime * 3f); _vignette.color = vc;
        }

        static Image Img(Transform parent, string name, Sprite s, Color c)
        {
            var img = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(parent, false);
            img.sprite = s; img.color = c; img.raycastTarget = false;
            return img;
        }

        static void Place(RectTransform rt, Vector2 pos, float size)
        {
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(size, size);
        }

        // 가운데가 비고 가장자리로 갈수록 짙어지는 판(화면 비율로 늘려 쓴다)
        static Texture2D BakeVignette(int n)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float fx = (x + 0.5f) / n * 2f - 1f, fy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(fx * fx + fy * fy) / 1.4142f;
                    float v = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, d));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(v * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        // 꽉 찬 원(테두리 판). 가장자리 1.5칸을 부드럽게
        static Texture2D BakeDisc(int n)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            float r = n / 2f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                    byte a = (byte)(Mathf.Clamp01(r - d) * 255);
                    px[y * n + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        // 태극 문양: 위쪽 반원 = 양(종이빛), 아래 = 음(먹빛), 작은 원 둘이 서로를 품는다
        static Texture2D BakeTaiji(int n)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            var yang = new Color32(236, 226, 204, 255);
            var yin = new Color32(26, 22, 20, 255);
            float R = n / 2f, h = R / 2f, dot = R / 7f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float fx = x + 0.5f - R, fy = y + 0.5f - R;
                    float d = Mathf.Sqrt(fx * fx + fy * fy);
                    float alpha = Mathf.Clamp01(R - d);
                    // 기본: 오른쪽 반 = 양, 왼쪽 반 = 음. 세로 물방울 둘로 S 자
                    bool isYang = fx > 0f;
                    float dTop = Mathf.Sqrt(fx * fx + (fy - h) * (fy - h));
                    float dBot = Mathf.Sqrt(fx * fx + (fy + h) * (fy + h));
                    if (dTop < h) isYang = true;
                    if (dBot < h) isYang = false;
                    if (dTop < dot) isYang = false;
                    if (dBot < dot) isYang = true;
                    var c = isYang ? yang : yin;
                    c.a = (byte)(alpha * 255);
                    px[y * n + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
