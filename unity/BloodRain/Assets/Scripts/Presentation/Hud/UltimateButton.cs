using BloodRain.Core.Combat;
using BloodRain.Presentation.Combat;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BloodRain.Presentation.Hud
{
    // C-09a 오의 버튼(기획서 8장 '오의'). 오른쪽 아래 둥근 버튼이 한 생의 오의 게이지(life.Charge)만큼 아래에서 차오르고,
    // 가득 차면 금빛으로 켜진다. Space 또는 버튼을 누르면 life.UseUltimate() — 참이면 그 몸의 오의가 흐르기 시작하고,
    // 매 프레임 life.UltimatePulses 박자마다 life.UltimateCasts 번 화면 안 적 전부를 한 방 피해로 때린다.
    // 한 방 = Ultimates.HitDamage(몸·시작 무공·공력, life.UltimatePowerMultiplier). 절기·음양 배율은 아직 1(무공 실행기 쪽 배율, 다음 항목).
    // 그림은 진소운 일검 임시: 화면 가운데를 가르는 큰 붓 획(삼재검법 획을 크게) — WeaponRunner 의 붓 획 재료가 있으면 그것, 없으면 흰 선.
    // 몸마다 모양이 다른 오의(천뢰낙·사자후 …)는 R-15U, 천설부·천마해체는 R4-10U.
    public sealed class UltimateButton : MonoBehaviour
    {
        public TMP_FontAsset font;

        static readonly Color Gold = new Color32(205, 160, 60, 255);
        static readonly Color Dim = new Color(0.12f, 0.10f, 0.09f, 0.55f);
        static readonly Color Fill = new Color32(176, 40, 30, 220);

        RectTransform _root;
        Image _fill, _ring;
        TextMeshProUGUI _label;
        Image _slash;
        float _slashT = -1f;
        readonly System.Collections.Generic.List<int> _near = new System.Collections.Generic.List<int>();

        /// <summary>마지막 박자에 쓰러뜨리거나 때린 적 수(시험용).</summary>
        public int LastHits { get; private set; }
        public float LastDamage { get; private set; }

        void Start()
        {
            if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Build();
        }

        void Build()
        {
            var go = new GameObject("오의", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 11;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)go.transform;

            // 일검 붓 획(화면 가운데를 비스듬히 가른다)
            _slash = new GameObject("일검", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            _slash.transform.SetParent(_root, false);
            _slash.raycastTarget = false;
            var sr = _slash.rectTransform; sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 0.5f);
            sr.sizeDelta = new Vector2(2600, 60); sr.localEulerAngles = new Vector3(0, 0, -18f);
            _slash.color = new Color(0.10f, 0.07f, 0.06f, 0f); // 먹빛(종이 바탕 위)

            var disc = Sprite.Create(Disc(128), new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f));
            var btn = new GameObject("오의 버튼", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button)).GetComponent<Button>();
            btn.transform.SetParent(_root, false);
            _ring = btn.GetComponent<Image>();
            _ring.sprite = disc; _ring.color = Dim;
            var br = (RectTransform)btn.transform; br.anchorMin = br.anchorMax = new Vector2(1f, 0f); br.pivot = new Vector2(0.5f, 0.5f);
            br.anchoredPosition = new Vector2(-130, 150); br.sizeDelta = new Vector2(150, 150);
            btn.onClick.AddListener(Use);

            _fill = new GameObject("차오름", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            _fill.transform.SetParent(btn.transform, false);
            _fill.sprite = disc; _fill.color = Fill; _fill.raycastTarget = false;
            _fill.type = Image.Type.Filled; _fill.fillMethod = Image.FillMethod.Vertical; _fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            var fr = _fill.rectTransform; fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = new Vector2(8, 8); fr.offsetMax = new Vector2(-8, -8);

            _label = new GameObject("글", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            _label.transform.SetParent(btn.transform, false);
            if (font != null) _label.font = font;
            _label.alignment = TextAlignmentOptions.Center; _label.raycastTarget = false; _label.fontSize = 30;
            _label.textWrappingMode = TextWrappingModes.NoWrap;
            var lr = _label.rectTransform; lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = lr.offsetMax = Vector2.zero;
        }

        void Update()
        {
            var runner = LifeRunner.Instance;
            if (_root == null || runner == null || runner.Life == null || runner.Life.Record != null) return;
            var life = runner.Life;
            var c = life.Charge;
            _fill.fillAmount = c.Fraction;
            _ring.color = c.IsFull ? Color.Lerp(Gold, Color.white, 0.25f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f)) : Dim;
            _label.text = c.IsFull ? $"{Loc.T("ui.ultimate.ready")}\n<size=55%>Space</size>" : $"<size=70%>{c.Value:0}</size>";
            _label.color = c.IsFull ? new Color(0.12f, 0.10f, 0.09f) : new Color(1f, 0.95f, 0.85f);

            // 세상 시간이 멈춘 화면(레벨업·깨달음·보부상)에선 누르지 않는다
            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame && Time.timeScale > 0f) Use();

            for (int p = 0; p < life.UltimatePulses; p++)
                for (int k = 0; k < life.UltimateCasts; k++) Strike(runner);

            if (_slashT >= 0f)
            {
                _slashT += Time.unscaledDeltaTime;
                float t = _slashT / 0.6f;
                var col = _slash.color; col.a = t < 0.15f ? t / 0.15f : Mathf.Clamp01(1f - (t - 0.15f) / 0.85f); _slash.color = col;
                _slash.rectTransform.sizeDelta = new Vector2(2600, Mathf.Lerp(90f, 12f, t));
                if (t >= 1f) _slashT = -1f;
            }
        }

        public void Use()
        {
            var runner = LifeRunner.Instance;
            if (runner == null || runner.Life == null || Time.timeScale <= 0f) return;
            if (runner.Life.UseUltimate()) Debug.Log($"[UltimateButton] 오의 {runner.Life.Body.Character.Ultimate} × {runner.Life.UltimateCasts}");
        }

        void Strike(LifeRunner runner)
        {
            var life = runner.Life;
            var data = DataBoot.Data;
            float might = runner.Stats != null ? runner.Stats.Might : 1f;
            float dmg = Ultimates.HitDamage(data, life.Body.Character, life.Loadout, might, life.UltimatePowerMultiplier);
            LastDamage = dmg;
            _slashT = 0f;
            if (dmg <= 0f || runner.enemies == null || runner.enemies.Sim == null) { LastHits = 0; return; }
            var cam = Camera.main;
            if (cam == null) return;
            // 화면 안(카메라 사각형을 덮는 원) 적 전부
            var center = (Vector2)cam.transform.position;
            float r = cam.orthographicSize * Mathf.Sqrt(1f + cam.aspect * cam.aspect);
            runner.enemies.Sim.QueryCircle(new float2(center.x, center.y), r, _near);
            LastHits = runner.enemies.Damage(_near, dmg, center, 0f);
        }

        static Texture2D Disc(int n)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            float r = n / 2f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d) * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
