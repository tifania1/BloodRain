using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BloodRain.Presentation.Hud
{
    // HUD-01U ①② 화면 맨 위 진기 바와 오른쪽 위 처치·은자(기획서 12장 '전투 HUD 배치').
    //   ① 맨 위 가로 진기 바: 먹선 위로 금빛이 RunXp.Progress 만큼 찬다. 오른쪽 끝에 'Lv N · 경지'(strings ui.hud.level).
    //   ② 그 아래 오른쪽: 처치 수(LifeRunner.Kills, 덩이 속 사람 수 — KC-01U)·은자(Life.Silver).
    //   ③ 무공·심법 칸, ④ 머리 위 기혈 바·경공 점은 아직(HUD-01U 남은 것).
    public sealed class TopHud : MonoBehaviour
    {
        public TMP_FontAsset font;
        [Tooltip("진기 바 높이(기준 해상도 1920x1080 px)")]
        public float barHeight = 10f;
        [Tooltip("차오르는 빠르기(초당 비율)")]
        public float follow = 10f;

        static readonly Color Gold = new Color32(214, 170, 72, 255);
        static readonly Color InkLine = new Color(0.10f, 0.08f, 0.07f, 0.75f);
        static readonly Color Paper = new Color(0.96f, 0.93f, 0.85f, 0.35f);
        static readonly Color TextInk = new Color(0.12f, 0.10f, 0.09f, 1f);

        RectTransform _fill;
        TextMeshProUGUI _level, _kills, _silver;
        float _shown;
        int _lastLevel = -1;
        long _lastKills = -1;
        int _lastSilver = -1;

        void Start() => Build();

        void Build()
        {
            var go = new GameObject("위 HUD", typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)go.transform;

            // 바 바탕(옅은 종이) + 아래 먹선
            var bg = Img(root, "진기 바 바탕", Paper);
            Stretch(bg.rectTransform, barHeight);
            var line = Img(root, "먹선", InkLine);
            var lr = line.rectTransform;
            lr.anchorMin = new Vector2(0, 1); lr.anchorMax = new Vector2(1, 1); lr.pivot = new Vector2(0.5f, 1);
            lr.anchoredPosition = new Vector2(0, -barHeight); lr.sizeDelta = new Vector2(0, 2);
            // 금빛 채움: 왼쪽에서 오른쪽으로 anchorMax.x 가 진행
            var fill = Img(root, "진기", Gold);
            _fill = fill.rectTransform;
            _fill.anchorMin = new Vector2(0, 1); _fill.anchorMax = new Vector2(0, 1); _fill.pivot = new Vector2(0, 1);
            _fill.anchoredPosition = Vector2.zero; _fill.offsetMin = new Vector2(0, -barHeight); _fill.offsetMax = Vector2.zero;

            _level = Text(root, "레벨", 26, TextAlignmentOptions.Right, new Vector2(-24, -barHeight - 6));
            _kills = Text(root, "처치", 22, TextAlignmentOptions.Right, new Vector2(-24, -barHeight - 40));
            _silver = Text(root, "은자", 22, TextAlignmentOptions.Right, new Vector2(-24, -barHeight - 68));
        }

        void Update()
        {
            var lr = LifeRunner.Instance;
            if (_fill == null || lr == null || lr.Life == null) return;
            var life = lr.Life;
            float target = Mathf.Clamp01((float)life.Xp.Progress);
            if (life.Level != _lastLevel) _shown = target < _shown ? 0f : _shown; // 레벨이 오르면 비우고 다시 참
            _shown = Mathf.Lerp(_shown, target, 1f - Mathf.Exp(-follow * Time.unscaledDeltaTime));
            _fill.anchorMax = new Vector2(_shown, 1);
            _fill.offsetMin = new Vector2(0, -barHeight); _fill.offsetMax = Vector2.zero;

            if (life.Level != _lastLevel)
            {
                _lastLevel = life.Level;
                _level.text = Loc.F("ui.hud.level", life.Level, Loc.Realm(life.Xp.Realm));
            }
            if (lr.Kills != _lastKills) { _lastKills = lr.Kills; _kills.text = Loc.F("ui.hud.kills", lr.Kills.ToString("N0")); }
            if (life.Silver != _lastSilver) { _lastSilver = life.Silver; _silver.text = Loc.F("ui.hud.silver", life.Silver.ToString("N0")); }
        }

        static Image Img(Transform parent, string name, Color c)
        {
            var img = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(parent, false);
            img.color = c; img.raycastTarget = false;
            return img;
        }

        static void Stretch(RectTransform rt, float h)
        {
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(0, h);
        }

        TextMeshProUGUI Text(Transform parent, string name, float size, TextAlignmentOptions align, Vector2 pos)
        {
            var t = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            t.transform.SetParent(parent, false);
            if (font != null) t.font = font;
            t.fontSize = size; t.alignment = align; t.color = TextInk; t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(420, 32);
            return t;
        }
    }
}
