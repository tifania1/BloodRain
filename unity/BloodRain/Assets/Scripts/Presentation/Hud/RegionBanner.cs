using BloodRain.Core.Data;
using BloodRain.Presentation.Fx;
using DG.Tweening;
using Febucci.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BloodRain.Presentation.Hud
{
    // 다른 땅에 들어설 때 뜨는 알림(2026-11-17 밤 사람: "각 지역을 이동할 때마다 맞는 이미지가 나와야").
    //   화면 위쪽에 그 땅의 바닥 무늬를 띠로 깔고, 한자 이름을 한 글자씩(Text Animator 타자기), 아래에 한글 이름과 혈운 농도.
    //   띠가 펼쳐지고 걷히는 움직임은 DOTween. 시계는 게임 시간과 따로(레벨업 화면이 떠도 끝까지 간다).
    //   생의 첫머리는 WorldMapHud 의 내려오는 연출이 땅 이름을 띄우니 여기서는 두 번째 땅부터.
    // 에셋 스토어의 DOTween Pro·Text Animator 를 쓴다(docs/design/store_assets.md — 저장소에는 없고 PC 에만 있다).
    public sealed class RegionBanner : MonoBehaviour
    {
        public LifeRunner runner;
        public TMP_FontAsset font;

        static readonly Color Ink = new Color(0.09f, 0.08f, 0.08f, 1f);

        string _shown;
        GameObject _canvas;
        CanvasGroup _group;
        RectTransform _band;
        RawImage _strip;
        TextMeshProUGUI _hanja, _name;
        TypewriterByCharacter _type;
        Sequence _seq;

        public bool Playing => _seq != null && _seq.IsActive() && _seq.IsPlaying();
        public string LastRegion { get; private set; } = "";

        void Update()
        {
            if (runner == null || runner.Life == null || runner.Life.Record != null) return;
            string id = runner.Life.RegionId;
            if (id == _shown || string.IsNullOrEmpty(id)) return;
            bool first = _shown == null;
            _shown = id;
            if (!first) Show(runner.Life.Regions.Region(id));
        }

        /// <summary>그 땅의 알림을 띄운다(시험에서도 부른다).</summary>
        public void Show(RegionDef region)
        {
            if (region == null) return;
            string big = Loc.Hanja(region.Hanja);
            if (string.IsNullOrEmpty(big)) big = Loc.Region(region); // 중국어 판은 한자 줄이 비어 이름을 크게
            Play(region, big, Loc.Region(region) + "   ·   " + Loc.F("ui.density", region.Density.ToString("0.0")), Ink);
        }

        static readonly Color Blood = new Color(0.62f, 0.09f, 0.08f, 1f);

        /// <summary>땅의 주인이 깨어났다는 알림(15분, EB-01a). 그 땅 바닥 무늬 위에 붉은 글씨.</summary>
        public void ShowLord(RegionDef region)
        {
            if (region == null) return;
            var bd = System.Linq.Enumerable.FirstOrDefault(DataBoot.Data.Bosses, b => b.Id == region.Boss);
            string boss = bd != null ? Loc.Boss(bd) : region.Boss;
            Play(region, Loc.T("ui.lord_woke.title"), Loc.F("ui.lord_woke.sub", Loc.Region(region), boss), Blood);
        }

        void Play(RegionDef region, string big, string sub, Color bigColor)
        {
            if (_canvas == null) Build();
            if (font == null)
            {
                var hud = FindFirstObjectByType<RegionHud>();
                if (hud != null && hud.regionText != null) font = hud.regionText.font;
            }
            if (font != null) { _hanja.font = font; _name.font = font; }
            LastRegion = region.Id;
            _strip.texture = Biomes.Tile(region.Id);
            _name.text = sub;
            _hanja.color = bigColor;
            _canvas.SetActive(true);
            _type.ShowText("{fade}{size}" + big + "{/size}{/fade}");

            if (_seq != null) _seq.Kill();
            _group.alpha = 0f;
            _band.localScale = new Vector3(0.55f, 1f, 1f);
            _band.anchoredPosition = new Vector2(0f, -132f);
            _name.alpha = 0f;
            _seq = DOTween.Sequence().SetUpdate(true)
                .Append(_group.DOFade(1f, 0.35f))
                .Join(_band.DOScaleX(1f, 0.5f).SetEase(Ease.OutCubic))
                .Insert(0.45f, DOTween.To(() => _name.alpha, a => _name.alpha = a, 1f, 0.5f))
                .AppendInterval(2.4f)
                .Append(_group.DOFade(0f, 0.7f))
                .Join(_band.DOAnchorPosY(-116f, 0.7f).SetEase(Ease.InQuad))
                .OnComplete(() => { if (_canvas != null) _canvas.SetActive(false); });
        }

        void OnDestroy() { if (_seq != null) _seq.Kill(); }

        void Build()
        {
            _canvas = new GameObject("지역 알림", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            _canvas.transform.SetParent(transform, false);
            var canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5; // 레벨업·깨달음 화면(40~50)이 위를 덮는다
            var scaler = _canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 1f;
            _group = _canvas.GetComponent<CanvasGroup>();
            _group.blocksRaycasts = false; _group.interactable = false;

            _band = new GameObject("띠", typeof(RectTransform)).GetComponent<RectTransform>();
            _band.SetParent(_canvas.transform, false);
            _band.anchorMin = _band.anchorMax = new Vector2(0.5f, 1f);
            _band.pivot = new Vector2(0.5f, 1f);
            _band.sizeDelta = new Vector2(620f, 112f);

            // 그 땅의 바닥 무늬(가로로 길게 되풀이)
            _strip = Child<RawImage>("바닥 무늬", _band, Vector2.zero, Vector2.one);
            _strip.uvRect = new Rect(0f, 0.2f, 2.2f, 0.4f);
            _strip.raycastTarget = false;
            // 위아래 먹 줄
            var top = Child<Image>("윗줄", _band, new Vector2(0f, 1f), new Vector2(1f, 1f)); top.rectTransform.sizeDelta = new Vector2(0f, 3f); top.color = Ink; top.raycastTarget = false;
            var bot = Child<Image>("아랫줄", _band, new Vector2(0f, 0f), new Vector2(1f, 0f)); bot.rectTransform.sizeDelta = new Vector2(0f, 3f); bot.color = Ink; bot.raycastTarget = false;

            _hanja = Child<TextMeshProUGUI>("한자", _band, new Vector2(0f, 0.34f), new Vector2(1f, 1f));
            _hanja.alignment = TextAlignmentOptions.Center; _hanja.fontSize = 54f; _hanja.color = Ink; _hanja.characterSpacing = 18f; _hanja.raycastTarget = false;
            _hanja.gameObject.AddComponent<TextAnimator_TMP>();
            _type = _hanja.gameObject.AddComponent<TypewriterByCharacter>();
            _type.waitForNormalChars = 0.14f;

            _name = Child<TextMeshProUGUI>("이름", _band, new Vector2(0f, 0f), new Vector2(1f, 0.36f));
            _name.alignment = TextAlignmentOptions.Center; _name.fontSize = 21f; _name.color = new Color(Ink.r, Ink.g, Ink.b, 0.92f); _name.raycastTarget = false;
            _canvas.SetActive(false);
        }

        static T Child<T>(string name, RectTransform parent, Vector2 min, Vector2 max) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return go.AddComponent<T>();
        }
    }
}
