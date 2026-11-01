using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BloodRain.Presentation.Fx
{
    // 비전·기연 발견 연출(S-04, 기획서 20장 '보이는 방식'): 화면이 탁본처럼 뒤집히며 낙관과 이름.
    //   1) 먹빛 탁본 한 장이 가운데서 좌우로 펼쳐지듯 뒤집혀 화면을 덮는다(0.35초).
    //   2) 이름(한글)과 한자가 종이빛으로 떠오르고, 붉은 낙관('秘傳' 또는 '奇緣')이 쾅 찍힌다.
    //   3) 잠시 머문 뒤 탁본이 다시 접히며 사라진다. 시계는 언스케일(레벨업 화면이 시간을 멈춰도 돈다).
    //   여러 개가 한꺼번에 열리면 차례로 보인다. 세상 시간은 건드리지 않는다.
    //   임시 도형이다. 탁본 질감(종이 결·먹 번짐)과 낙관 그림은 아트가 들어오면 바꾼다.
    public sealed class DiscoveryFx : MonoBehaviour
    {
        public enum Kind { Secret, Encounter }

        public TMP_FontAsset font;

        static readonly Color Rubbing = new Color(0.07f, 0.06f, 0.06f, 0.95f);
        static readonly Color Paper = new Color(0.93f, 0.89f, 0.80f);
        static readonly Color SealRed = new Color(0.66f, 0.12f, 0.09f);

        const float FlipIn = 0.35f, Stamp = 0.25f, Hold = 2.0f, FlipOut = 0.4f;

        struct Item { public Kind Kind; public string Name, Hanja; }

        readonly Queue<Item> _queue = new Queue<Item>();
        RectTransform _root, _sheet;
        Image _sheetImage, _seal;
        TextMeshProUGUI _name, _hanja, _sealText, _kindText;
        float _t = -1f;

        public bool Playing => _t >= 0f;
        public int Pending => _queue.Count;

        /// <summary>발견 하나를 띄운다. 이미 하나가 돌고 있으면 그 뒤에 선다.</summary>
        public void Show(Kind kind, string name, string hanja)
        {
            _queue.Enqueue(new Item { Kind = kind, Name = name, Hanja = hanja });
        }

        void Awake() => Build();

        void Update()
        {
            if (_t < 0f)
            {
                if (_queue.Count == 0) return;
                Begin(_queue.Dequeue());
            }
            _t += Time.unscaledDeltaTime;
            float total = FlipIn + Stamp + Hold + FlipOut;
            if (_t >= total) { _t = -1f; _root.gameObject.SetActive(false); return; }

            // 탁본 뒤집힘: 가로 폭이 0 → 1(앞) / 1 → 0(뒤). 접힐 때 모서리가 살짝 기울게 y도 조금.
            float open;
            if (_t < FlipIn) open = Ease(_t / FlipIn);
            else if (_t > total - FlipOut) open = 1f - Ease((_t - (total - FlipOut)) / FlipOut);
            else open = 1f;
            _sheet.localScale = new Vector3(open, 0.92f + 0.08f * open, 1f);
            _sheetImage.color = new Color(Rubbing.r, Rubbing.g, Rubbing.b, Rubbing.a * Mathf.Clamp01(open * 1.4f));

            // 글: 펼쳐진 뒤 떠오른다.
            float textA = Mathf.Clamp01((_t - FlipIn * 0.7f) / 0.3f) * open;
            _name.alpha = textA;
            _hanja.alpha = textA * 0.8f;
            _kindText.alpha = textA * 0.7f;

            // 낙관: FlipIn 끝에서 크게 시작해 쾅 내려앉는다.
            float s = (_t - FlipIn) / Stamp;
            if (s < 0f) { _seal.gameObject.SetActive(false); }
            else
            {
                _seal.gameObject.SetActive(true);
                float k = Mathf.Clamp01(s);
                float scale = Mathf.Lerp(1.9f, 1f, Ease(k));
                if (k >= 1f) scale = 1f + 0.06f * Mathf.Exp(-(s - 1f) * 6f) * Mathf.Sin((s - 1f) * 30f);
                _seal.rectTransform.localScale = Vector3.one * scale;
                var c = SealRed; c.a = Mathf.Clamp01(k * 2f) * open;
                _seal.color = c;
                _sealText.alpha = c.a;
            }
        }

        void Begin(Item it)
        {
            _t = 0f;
            _root.gameObject.SetActive(true);
            _name.text = it.Name;
            _hanja.text = it.Hanja;
            bool secret = it.Kind == Kind.Secret;
            _sealText.text = secret ? "秘\n傳" : "奇\n緣";
            _kindText.text = secret ? "비전을 찾았다" : "기연을 만났다";
        }

        static float Ease(float x) { x = Mathf.Clamp01(x); return 1f - (1f - x) * (1f - x) * (1f - x); }

        void Build()
        {
            var go = new GameObject("발견", typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60; // 레벨업 화면(50) 위
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)go.transform;

            _sheetImage = new GameObject("탁본", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            _sheet = _sheetImage.rectTransform;
            _sheet.SetParent(_root, false);
            _sheet.anchorMin = new Vector2(0f, 0.22f); _sheet.anchorMax = new Vector2(1f, 0.78f);
            _sheet.offsetMin = _sheet.offsetMax = Vector2.zero;
            _sheetImage.raycastTarget = false;

            _kindText = Text(_sheet, "갈래", 30, new Vector2(0, 150), new Vector2(900, 50));
            _name = Text(_sheet, "이름", 96, new Vector2(-40, 30), new Vector2(1400, 130));
            _hanja = Text(_sheet, "한자", 40, new Vector2(-40, -80), new Vector2(1400, 60));

            _seal = new GameObject("낙관", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var sr = _seal.rectTransform;
            sr.SetParent(_sheet, false);
            sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 0.5f);
            sr.anchoredPosition = new Vector2(560, -10);
            sr.sizeDelta = new Vector2(120, 150);
            sr.localRotation = Quaternion.Euler(0, 0, -4f);
            _seal.raycastTarget = false;
            _sealText = Text(sr, "글", 52, Vector2.zero, new Vector2(120, 150));
            _sealText.color = Paper;
            _sealText.lineSpacing = -20;

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
            t.color = Paper;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            return t;
        }
    }
}
