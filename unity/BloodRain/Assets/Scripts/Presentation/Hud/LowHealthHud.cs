using UnityEngine;
using UnityEngine.UI;

namespace BloodRain.Presentation.Hud
{
    // LH-01U 기혈 위험 표시(기획서 8장 '피격과 사망': 기혈 30% 이하에선 화면 가장자리에 먹물이 번지고 심장 소리).
    //   문턱은 Core Balance.Contact.IsLowHealth(balance.json contact.lowHealthRatio). 켜지면 가장자리 먹 판의 진하기가
    //   baseAlpha ± pulseAlpha 로 초당 pulseSpeed 라디안 출렁인다(웹: 가장자리 어둠 0.35 → 0.7 ± 0.15, 초당 6 라디안).
    //   먹 판은 코드로 굽는다: 가운데가 비고 가장자리로 갈수록 짙어지며, 경계가 노이즈로 번진 먹물처럼 들쭉날쭉하다.
    //   깜빡임 끄기 설정(WP-57U)이 Unity 에 붙으면 reduceFlashing 을 참으로 넣어 출렁임 없이 고정한다.
    //   심장 소리 sfx_low_health_heart 는 켜진 동안 SfxPlayer 로 되풀이(SFX-15U, 아래 Heart).
    public sealed class LowHealthHud : MonoBehaviour
    {
        [Tooltip("켜졌을 때 가장자리 먹의 가운데 진하기")]
        public float baseAlpha = 0.7f;
        [Tooltip("출렁이는 폭")]
        public float pulseAlpha = 0.15f;
        [Tooltip("출렁임 빠르기(초당 라디안)")]
        public float pulseSpeed = 6f;
        [Tooltip("켜지고 꺼지는 빠르기(초당 진하기)")]
        public float fade = 2.5f;
        [Tooltip("참이면 출렁임 없이 고정(깜빡임 끄기)")]
        public bool reduceFlashing;

        static readonly Color Ink = new Color(0.07f, 0.05f, 0.05f, 0f);

        Image _ink;
        PlayerVitals _vitals;

        /// <summary>지금 위험 표시가 켜졌나(시험·HUD 가 본다).</summary>
        public bool Active { get; private set; }

        void Start() => Build();

        void Build()
        {
            var go = new GameObject("기혈 위험 HUD", typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9; // 음양 HUD(10) 아래, 세상 위
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;

            var tex = BakeInkEdge(256);
            _ink = new GameObject("가장자리 먹", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            _ink.transform.SetParent(go.transform, false);
            _ink.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            _ink.color = Ink;
            _ink.raycastTarget = false;
            var r = _ink.rectTransform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        }

        void Update()
        {
            if (_ink == null) return;
            if (_vitals == null) _vitals = FindFirstObjectByType<PlayerVitals>();
            float ratio = _vitals != null && _vitals.MaxHealth > 0f ? _vitals.Health / _vitals.MaxHealth : 1f;
            Active = DataBoot.Data != null && DataBoot.Data.Balance.Contact.IsLowHealth(ratio);
            Heart();
            float target = 0f;
            if (Active)
                target = reduceFlashing ? baseAlpha : baseAlpha + pulseAlpha * Mathf.Sin(Time.unscaledTime * pulseSpeed);
            var c = _ink.color;
            // 켜질 때·꺼질 때만 천천히, 켜진 동안의 출렁임은 그대로 따라간다
            c.a = Active && c.a >= baseAlpha - pulseAlpha - 0.01f ? target : Mathf.MoveTowards(c.a, target, Time.unscaledDeltaTime * fade);
            _ink.color = c;
        }

        // SFX-15U 심장 소리: 위험에 들어서는 순간 한 번, 그 뒤 balance sfx.lowHealthHeartSeconds 마다(웹 G.heartT).
        // 세상 시간(deltaTime)으로 세니 레벨업 화면처럼 세상이 멈춘 동안은 쉰다. 위험을 벗어나면 바로 멈추고 다음에 다시 처음부터.
        float _heartT;
        bool _heartOn;

        void Heart()
        {
            float every = DataBoot.Data != null ? DataBoot.Data.Balance.Sfx.LowHealthHeartSeconds : 0f;
            if (!Active || every <= 0f) { _heartOn = false; return; }
            if (Time.timeScale <= 0f) return;
            if (!_heartOn) { _heartOn = true; _heartT = 0f; }
            _heartT -= Time.deltaTime;
            if (_heartT > 0f) return;
            _heartT += every;
            if (_heartT < 0f) _heartT = every;
            SfxPlayer.Instance?.Play("sfx_low_health_heart");
        }

        // 가장자리 먹 판. 거리(화면 비율로 늘려 씀)에 낮은 주파수 값 노이즈를 더해 경계가 번진 먹처럼 들쭉날쭉하게.
        static Texture2D BakeInkEdge(int n)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float fx = (x + 0.5f) / n * 2f - 1f, fy = (y + 0.5f) / n * 2f - 1f;
                    // 둥근 네모(가장자리를 따라 고르게): max 와 원의 중간
                    float d = Mathf.Lerp(Mathf.Max(Mathf.Abs(fx), Mathf.Abs(fy)), Mathf.Sqrt(fx * fx + fy * fy) / 1.4142f, 0.35f);
                    float noise = Mathf.PerlinNoise(x * 0.035f + 11.3f, y * 0.035f + 4.7f) * 0.6f
                                + Mathf.PerlinNoise(x * 0.11f + 2.1f, y * 0.11f + 9.9f) * 0.4f;
                    float edge = d + (noise - 0.5f) * 0.18f;
                    float v = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.48f, 0.98f, edge));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(v) * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
