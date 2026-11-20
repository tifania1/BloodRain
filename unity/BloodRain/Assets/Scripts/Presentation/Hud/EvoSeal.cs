using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BloodRain.Presentation.Hud
{
    // VF-04U 절기 큰 연출(시안 web/lookdev/sects.html sealFx): 절기가 쓰일 때 세 번에 한 번, 화면 위쪽에 절기 한자를 먹빛으로 크게 쓰고
    //   오른쪽에 밝은 주홍 낙관 '絶'을 찍는다(밝은 주홍은 여기만 — 색 규칙). 1.1초, 0.08초에 나타나 끝 40% 에 옅어지고, 낙관은 0.12초에 1.5배 → 1배.
    //   글자는 Unity 에서 얹는다(이미지에 굽지 않음). 절제(2026-11-17 사람): 여러 절기가 겹쳐도 6초에 한 번까지.
    public sealed class EvoSeal : MonoBehaviour
    {
        static EvoSeal _inst;
        static readonly Color Ink = new Color(0.11f, 0.10f, 0.09f, 1f);
        static readonly Color Vermilion = new Color32(200, 51, 31, 255);
        static readonly Color Paper = new Color(0.94f, 0.91f, 0.82f, 0.92f);
        const float Life = 1.1f;
        public const float MinGapSeconds = 6f;

        GameObject _canvas;
        RectTransform _sealRt;
        CanvasGroup _group;
        TextMeshProUGUI _hanja, _mark;
        float _t0;
        bool _on;
        static float _last = -100f;
        static int _count;

        /// <summary>시험용: 마지막에 쓴 한자.</summary>
        public static string LastText { get; private set; }

        /// <summary>절기가 한 번 쓰였다. 세 번째마다(그리고 마지막 낙관에서 6초가 지났으면) 찍는다. 찍었으면 참.</summary>
        public static bool Fired(string hanja, TMP_FontAsset font)
        {
            if (string.IsNullOrEmpty(hanja) || BloodRain.Presentation.Fx.FxSettings.Simplified) return false;
            if (_count++ % 3 != 0) return false;
            if (Time.unscaledTime - _last < MinGapSeconds) return false;
            _last = Time.unscaledTime;
            if (_inst == null) _inst = new GameObject("절기 낙관").AddComponent<EvoSeal>();
            _inst.Begin(hanja, font);
            return true;
        }

        void Begin(string hanja, TMP_FontAsset font)
        {
            if (_canvas == null) Build(font);
            _hanja.text = hanja;
            LastText = hanja;
            _hanja.ForceMeshUpdate();
            float tw = Mathf.Min(_hanja.preferredWidth, 1400f);
            float sz = _hanja.fontSize * 0.32f;
            _sealRt.sizeDelta = new Vector2(sz, sz);
            _sealRt.anchoredPosition = new Vector2(tw * 0.5f + sz * 0.8f, 216f + _hanja.fontSize * 0.12f);
            _t0 = Time.unscaledTime;
            _on = true;
            _canvas.SetActive(true);
            Step();
        }

        void Update() { if (_on) Step(); }

        void Step()
        {
            float t = Time.unscaledTime - _t0, k = t / Life;
            float al = Mathf.Min(1f, t / 0.08f) * (1f - Mathf.Max(0f, (k - 0.6f) / 0.4f));
            _group.alpha = Mathf.Clamp01(al);
            float pr = Mathf.Min(1f, t / 0.12f);
            _sealRt.localScale = Vector3.one * (1f + 0.5f * (1f - pr));
            if (t >= Life) { _on = false; _canvas.SetActive(false); }
        }

        void Build(TMP_FontAsset font)
        {
            _canvas = new GameObject("절기 낙관 캔버스", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            _canvas.transform.SetParent(transform, false);
            var c = _canvas.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 43;   // 경지 낙관(44) 아래, HUD 위
            var sc = _canvas.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;
            _group = _canvas.GetComponent<CanvasGroup>();
            _group.blocksRaycasts = false; _group.interactable = false;

            // 절기 한자: 화면 높이 13%, 먹빛 0.62(시안), 화면 위에서 30% 자리
            _hanja = Txt(_canvas.transform, 140f, new Color(Ink.r, Ink.g, Ink.b, 0.62f), font);
            var hr = _hanja.rectTransform; hr.anchorMin = hr.anchorMax = new Vector2(0.5f, 0.5f);
            hr.anchoredPosition = new Vector2(0f, 216f); hr.sizeDelta = new Vector2(1600f, 180f);

            // 주홍 낙관: 글자 오른쪽 위, 한자 높이의 32% 네모 안에 '絶'
            _sealRt = new GameObject("낙관", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
            _sealRt.SetParent(_canvas.transform, false);
            _sealRt.anchorMin = _sealRt.anchorMax = new Vector2(0.5f, 0.5f);
            var img = _sealRt.GetComponent<Image>(); img.color = Vermilion; img.raycastTarget = false;
            _mark = Txt(_sealRt, 28f, Paper, font);
            _mark.text = "絶";   // 絶
            _mark.fontStyle = FontStyles.Bold;
            var mr = _mark.rectTransform; mr.anchorMin = Vector2.zero; mr.anchorMax = Vector2.one; mr.offsetMin = mr.offsetMax = Vector2.zero;
            _mark.enableAutoSizing = true; _mark.fontSizeMin = 10f; _mark.fontSizeMax = 40f;
        }

        static TextMeshProUGUI Txt(Transform parent, float size, Color color, TMP_FontAsset font)
        {
            var go = new GameObject("글", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size; t.color = color; t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap; t.raycastTarget = false;
            return t;
        }
    }
}
