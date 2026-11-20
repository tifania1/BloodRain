using BloodRain.Core.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BloodRain.Presentation.Hud
{
    // BT-01U 경지 돌파를 '깨달음'답게(2026-11-15 클라우드 VF-11 시안, 기획서 7장 '돌파 연출'): 새 경지 한자를 어두운 자주 낙관으로 찍는다.
    //   0.02초부터 0.12초 동안 1.5배 → 1배로 내려앉고, 1.4초부터 0.3초에 옅어진다. 아래 한 줄 '이류 · 적 한 덩이 4'.
    //   다시 오른 경지(짧은 연출)는 시각을 모두 절반. 세상 시간이 0.05배로 느려지니 실시간으로 센다.
    //   흑백 줌·쓸림은 BreakthroughFx 그대로(이 화면은 그 위에 얹는 글자만).
    public sealed class RealmSeal : MonoBehaviour
    {
        static RealmSeal _inst;
        static readonly Color SealInk = new Color(0.30f, 0.09f, 0.20f, 0.95f);
        static readonly Color Paper = new Color(0.95f, 0.91f, 0.82f);

        GameObject _canvas;
        RectTransform _seal;
        CanvasGroup _group;
        TextMeshProUGUI _hanja, _sub;
        float _t0, _k = 1f;
        bool _on;

        /// <summary>시험용: 마지막에 찍은 한자와 아래 줄.</summary>
        public static string LastText { get; private set; }

        public static void Show(RealmDef realm, int clusterSize, bool revisit, TMP_FontAsset font)
        {
            if (_inst == null) _inst = new GameObject("경지 낙관").AddComponent<RealmSeal>();
            _inst.Begin(realm, clusterSize, revisit, font);
        }

        void Begin(RealmDef realm, int clusterSize, bool revisit, TMP_FontAsset font)
        {
            if (_canvas == null) Build(font);
            _hanja.text = Loc.T($"ui.breakthrough.seal.{realm.Id}");
            _sub.text = Loc.F("ui.breakthrough.sub", Loc.Realm(realm), clusterSize);
            LastText = _hanja.text + " / " + _sub.text;
            _k = revisit ? 0.5f : 1f;
            _t0 = Time.unscaledTime;
            _on = true;
            _canvas.SetActive(true);
            Step();
        }

        void Update() { if (_on) Step(); }

        void Step()
        {
            float t = (Time.unscaledTime - _t0) / _k;   // 다시 오른 경지는 시계가 두 배로 흐른다
            float inA = Mathf.Clamp01((t - 0.02f) / 0.10f);
            float outA = 1f - Mathf.Clamp01((t - 1.4f) / 0.3f);
            _group.alpha = inA * outA;
            float ease = 1f - (1f - inA) * (1f - inA);
            _seal.localScale = Vector3.one * Mathf.Lerp(1.5f, 1f, ease);
            if (t >= 1.7f) { _on = false; _canvas.SetActive(false); }
        }

        void Build(TMP_FontAsset font)
        {
            _canvas = new GameObject("낙관 캔버스", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            _canvas.transform.SetParent(transform, false);
            var c = _canvas.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 44;   // 레벨업(50)·깨달음 화면 아래, HUD 위
            var sc = _canvas.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;
            _group = _canvas.GetComponent<CanvasGroup>();
            _group.blocksRaycasts = false; _group.interactable = false;

            _seal = new GameObject("낙관", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
            _seal.SetParent(_canvas.transform, false);
            _seal.anchorMin = _seal.anchorMax = new Vector2(0.5f, 0.5f);
            _seal.anchoredPosition = new Vector2(0, 120);
            _seal.sizeDelta = new Vector2(330, 200);
            _seal.localRotation = Quaternion.Euler(0, 0, -4f);
            var img = _seal.GetComponent<Image>(); img.color = SealInk; img.raycastTarget = false;
            var rim = _seal.gameObject.AddComponent<Outline>(); rim.effectColor = new Color(0.55f, 0.18f, 0.32f, 0.9f); rim.effectDistance = new Vector2(4, -4);
            _hanja = Txt(_seal, 120, Paper, font);
            _hanja.enableAutoSizing = true; _hanja.fontSizeMin = 60; _hanja.fontSizeMax = 120;
            var hr = _hanja.rectTransform; hr.anchorMin = Vector2.zero; hr.anchorMax = Vector2.one; hr.offsetMin = new Vector2(16, 8); hr.offsetMax = new Vector2(-16, -8);

            _sub = Txt(_canvas.transform, 30, Paper, font);
            var sr = _sub.rectTransform; sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 0.5f);
            sr.anchoredPosition = new Vector2(0, -10); sr.sizeDelta = new Vector2(900, 44);
            _sub.outlineWidth = 0.25f; _sub.outlineColor = new Color(0.1f, 0.06f, 0.08f, 0.9f);
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
