using BloodRain.Presentation.Combat;
using BloodRain.Presentation.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BloodRain.Presentation.Hud
{
    // UI-01U 땅의 주인 체력바(기획서 12장 '위 가운데': 경과 시간 → 지역 이름·혈운 농도 → 그 아래 땅의 주인 이름과 체력바).
    // PatternRunner 가 싸우는 동안만 보이고, 설정 '땅의 주인 체력바'(GameSettings.BossBarOn)를 끄면 숨는다.
    // 여럿이면 작게 쌓는 것은 땅의 주인이 둘 이상 서는 싸움(분열·분신)이 붙을 때.
    public sealed class BossHealthBar : MonoBehaviour
    {
        public TMP_FontAsset font;
        [Tooltip("화면 위에서 얼마나 아래(기준 해상도 1920x1080 px)")]
        public float top = 116f;
        public float width = 560f;
        public float height = 12f;

        static readonly Color InkLine = new Color(0.10f, 0.08f, 0.07f, 0.8f);
        static readonly Color Back = new Color(0.96f, 0.93f, 0.85f, 0.45f);
        static readonly Color Blood = new Color32(150, 22, 18, 255);
        static readonly Color Lag = new Color(0.85f, 0.65f, 0.55f, 0.9f);
        static readonly Color TextInk = new Color(0.12f, 0.10f, 0.09f, 1f);

        GameObject _root;
        RectTransform _fill, _lag;
        TextMeshProUGUI _name;
        float _shown = 1f, _lagShown = 1f;
        int _bossId = -2;

        void Start() => Build();

        void Build()
        {
            var canvasGo = new GameObject("땅의 주인 체력바", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            _root = new GameObject("체력바", typeof(RectTransform));
            _root.transform.SetParent(canvasGo.transform, false);
            var rt = (RectTransform)_root.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -top);
            rt.sizeDelta = new Vector2(width, height + 34f);

            var nameGo = new GameObject("이름", typeof(RectTransform), typeof(TextMeshProUGUI));
            nameGo.transform.SetParent(rt, false);
            _name = nameGo.GetComponent<TextMeshProUGUI>();
            if (font != null) _name.font = font;
            _name.fontSize = 24f;
            _name.color = TextInk;
            _name.alignment = TextAlignmentOptions.Center;
            var nr = (RectTransform)nameGo.transform;
            nr.anchorMin = new Vector2(0f, 1f); nr.anchorMax = new Vector2(1f, 1f);
            nr.pivot = new Vector2(0.5f, 1f);
            nr.anchoredPosition = Vector2.zero;
            nr.sizeDelta = new Vector2(0f, 30f);

            var frame = Bar("테", InkLine, rt, new Vector2(width + 4f, height + 4f));
            var back = Bar("바탕", Back, frame, new Vector2(width, height));
            _lag = Bar("늦은 칸", Lag, back, new Vector2(width, height), true);
            _fill = Bar("기혈", Blood, back, new Vector2(width, height), true);
            _root.SetActive(false);
        }

        static RectTransform Bar(string name, Color c, RectTransform parent, Vector2 size, bool fromLeft = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = c;
            var r = (RectTransform)go.transform;
            if (fromLeft)
            {
                r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
                r.pivot = new Vector2(0f, 0.5f);
                r.anchoredPosition = Vector2.zero;
            }
            else
            {
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
                r.pivot = new Vector2(0.5f, 0f);
                r.anchoredPosition = Vector2.zero;
            }
            r.sizeDelta = size;
            return r;
        }

        /// <summary>시험용: 지금 보이는가.</summary>
        public bool Shown => _root != null && _root.activeSelf;
        public float ShownRatio => _shown;

        void Update()
        {
            if (_root == null) return;
            var pr = PatternRunner.Instance;
            bool show = pr != null && pr.InFight && SettingsScreen.Current.BossBarOn;
            if (_root.activeSelf != show) _root.SetActive(show);
            if (!show) return;
            if (pr.BossId != _bossId)
            {
                _bossId = pr.BossId;
                _shown = _lagShown = 1f;
                _name.text = pr.BossName;
            }
            float ratio = Mathf.Clamp01(pr.BossHealthRatio);
            float dt = Time.unscaledDeltaTime;
            _shown = Mathf.MoveTowards(_shown, ratio, dt * 2f);
            _lagShown = _lagShown < _shown ? _shown : Mathf.MoveTowards(_lagShown, _shown, dt * 0.35f);
            _fill.sizeDelta = new Vector2(width * _shown, height);
            _lag.sizeDelta = new Vector2(width * _lagShown, height);
        }
    }
}
