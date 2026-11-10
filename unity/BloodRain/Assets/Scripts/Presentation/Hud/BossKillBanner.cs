using BloodRain.Core.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BloodRain.Presentation.Hud
{
    // BR-02 땅의 주인을 쓰러뜨린 순간의 짧은 붓글씨 알림. 글은 Core BossKillNotice.Of(웹 lordDown toast 와 같은 글).
    //   화면 위쪽 1/3 에 먹 띠가 옆으로 번지듯 펼쳐지고, 제목(크게)과 보상 줄(작게)이 종이빛으로 떠오른 뒤 사라진다.
    //   시계는 언스케일. 종장(Of 가 null)은 엔딩으로 넘어가니 띄우지 않는다.
    //   임시 도형이다. 먹 띠 질감·붓글씨 글꼴은 아트가 들어오면 바꾼다.
    public sealed class BossKillBanner : MonoBehaviour
    {
        public TMP_FontAsset font;

        static readonly Color Ink = new Color(0.06f, 0.05f, 0.05f, 0.88f);
        static readonly Color Paper = new Color(0.93f, 0.89f, 0.80f);

        const float Open = 0.3f, Hold = 3.2f, Close = 0.5f;

        RectTransform _root, _band;
        Image _bandImage;
        TextMeshProUGUI _title, _body;
        float _t = -1f;

        public bool Playing => _t >= 0f;
        public string LastTitle { get; private set; } = "";
        public string LastBody { get; private set; } = "";

        /// <summary>처치 알림을 띄운다. notice 가 null 이면(종장) 아무것도 안 한다.</summary>
        public void Show(BossKillNotice notice)
        {
            if (notice == null) return;
            if (_root == null) Build(); // AddComponent 뒤에 font 를 넣으니 Awake 가 아니라 처음 띄울 때 만든다
            if (font != null) { _title.font = font; _body.font = font; }
            LastTitle = notice.Title;
            LastBody = notice.Body;
            _title.text = notice.Title;
            _body.text = notice.Body;
            _t = 0f;
            _root.gameObject.SetActive(true);
        }

        void Update()
        {
            if (_t < 0f) return;
            _t += Time.unscaledDeltaTime;
            float total = Open + Hold + Close;
            if (_t >= total) { _t = -1f; _root.gameObject.SetActive(false); return; }

            float k;
            if (_t < Open) k = Ease(_t / Open);
            else if (_t > total - Close) k = 1f - Ease((_t - (total - Close)) / Close);
            else k = 1f;
            _band.localScale = new Vector3(k, 1f, 1f);
            var c = Ink; c.a *= Mathf.Clamp01(k * 1.3f);
            _bandImage.color = c;
            float textA = Mathf.Clamp01((_t - Open * 0.6f) / 0.35f) * k;
            _title.alpha = textA;
            _body.alpha = textA * 0.9f;
        }

        static float Ease(float x) { x = Mathf.Clamp01(x); return 1f - (1f - x) * (1f - x) * (1f - x); }

        void Build()
        {
            var go = new GameObject("처치 알림", typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 45; // HUD 위, 레벨업 화면(50) 아래
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)go.transform;

            _bandImage = new GameObject("먹 띠", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            _band = _bandImage.rectTransform;
            _band.SetParent(_root, false);
            _band.anchorMin = new Vector2(0.12f, 0.62f); _band.anchorMax = new Vector2(0.88f, 0.80f);
            _band.offsetMin = _band.offsetMax = Vector2.zero;
            _bandImage.raycastTarget = false;

            _title = Text(_band, "제목", 64, new Vector2(0, 30), new Vector2(1400, 90));
            _title.color = Paper;
            _body = Text(_band, "보상", 30, new Vector2(0, -50), new Vector2(1400, 50));
            _body.color = Paper;

            _root.gameObject.SetActive(false);
        }

        TextMeshProUGUI Text(RectTransform parent, string name, float size, Vector2 pos, Vector2 box)
        {
            var t = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            var r = t.rectTransform;
            r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos;
            r.sizeDelta = box;
            if (font != null) t.font = font;
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }
    }
}
