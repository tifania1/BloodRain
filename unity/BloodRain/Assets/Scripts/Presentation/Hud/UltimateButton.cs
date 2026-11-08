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
            _label.text = c.IsFull ? $"{Loc.T("ui.ultimate.ready")}\n<size=55%>{ControlInput.FirstLabel(BloodRain.Core.Meta.ControlAction.Ultimate) ?? ""}</size>" : $"<size=70%>{c.Value:0}</size>";
            _label.color = c.IsFull ? new Color(0.12f, 0.10f, 0.09f) : new Color(1f, 0.95f, 0.85f);

            // 세상 시간이 멈춘 화면(레벨업·깨달음·보부상)에선 누르지 않는다
            // WP-69U: 오의 키는 조작 재설정을 따른다(8장 기본 E·마우스 오른쪽·RT — 전엔 Space 를 경공과 같이 썼음)
            if (ControlInput.Pressed(BloodRain.Core.Meta.ControlAction.Ultimate) && Time.timeScale > 0f) Use();

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
            var ult = Ultimates.Of(data, life.Body.Character);
            float dmg = Ultimates.HitDamage(data, life.Body.Character, life.Loadout, might, life.UltimatePowerMultiplier);
            // R4-10U 천설부: 화면 안 적 전부를 FreezeSeconds 멈추고, 한 방은 강시 계열에만(UltimateEffects.HitDamageOn)
            float freeze = UltimateEffects.FreezeSeconds(ult);
            LastDamage = dmg;
            _slashT = 0f;
            if ((dmg <= 0f && freeze <= 0f) || runner.enemies == null || runner.enemies.Sim == null) { LastHits = 0; return; }
            var cam = Camera.main;
            if (cam == null) return;
            // 화면 안(카메라 사각형을 덮는 원) 적 전부
            var center = (Vector2)cam.transform.position;
            float r = cam.orthographicSize * Mathf.Sqrt(1f + cam.aspect * cam.aspect);
            runner.enemies.Sim.QueryCircle(new float2(center.x, center.y), r, _near);
            if (freeze > 0f) runner.enemies.Freeze(_near, freeze);
            var field = runner.enemies;
            if (StrikeShaped(runner, ult, dmg, center, cam)) return;
            LastHits = dmg <= 0f ? 0 : UltimateEffects.CheonseolBu == ult.Id
                ? field.Damage(_near, dmg, center, 0f, a => UltimateEffects.Damages(ult, field.DefOf(a.Kind)?.Family ?? ""))
                : field.Damage(_near, dmg, center, 0f);
        }

        // ── R-15U 문파 오의 모양(5장 오의 표, Core UltimateCast) ──
        // 천뢰낙: 화면 안 무작위 strikes 곳에 낙뢰, 한 곳 반경 StrikeRadius 안 적에 한 방 + 감전(ShockSeconds 멈춤).
        // 사자후: 몸 둘레 InRadius 안 적에 한 방 + 기절(StunSeconds). 만천화우: 박자마다 화면 전체에 한 방 + 중독(PoisonTickDamage / PoisonTickSeconds 초당).
        // 팔방풍우: 여덟 갈래 장풍 연타. 역류: 바라보는 쪽 띠 안 적을 끝 쪽으로 민다. 태극무량 탄 되돌림은 EnemyField reflectRadius. 감전·기절은 둘 다 EnemySim Stun 으로 멈춘다(천설부와 같은 틀).
        readonly System.Random _rng = new System.Random();
        readonly System.Collections.Generic.List<int> _picked = new System.Collections.Generic.List<int>();
        /// <summary>시험용: 천뢰낙 마지막 낙뢰 자리(세계 좌표).</summary>
        public Vector2[] LastStrikePoints { get; private set; }
        /// <summary>시험용: 역류 마지막 물살 길이(몸에서 화면 끝까지, 세계 m).</summary>
        public float LastSweepReach { get; private set; }

        bool StrikeShaped(LifeRunner runner, BloodRain.Core.Data.UltimateDef ult, float dmg, Vector2 center, Camera cam)
        {
            var cast = runner.Life.Ultimate;
            if (cast == null || cast.Id != ult.Id) return false;
            var field = runner.enemies;
            float sc = BloodRain.Sim.WorldScale.Current != null ? BloodRain.Sim.WorldScale.Current.Length(1f) : 1f;
            switch (ult.Id)
            {
                case "cheonroe_rak":
                {
                    float hh = cam.orthographicSize, hw = hh * cam.aspect;
                    var pts = cast.StrikePoints(new System.Numerics.Vector2(center.x - hw, center.y - hh), new System.Numerics.Vector2(center.x + hw, center.y + hh), _rng);
                    LastStrikePoints = new Vector2[pts.Length];
                    int hits = 0;
                    for (int k = 0; k < pts.Length; k++)
                    {
                        var at = new Vector2(pts[k].X, pts[k].Y);
                        LastStrikePoints[k] = at;
                        field.Sim.QueryCircle(new float2(at.x, at.y), cast.StrikeRadius(sc), _near);
                        if (cast.ShockSeconds > 0f) field.Freeze(_near, cast.ShockSeconds);
                        hits += field.Damage(_near, dmg, at, 0f);
                    }
                    LastHits = hits;
                    return true;
                }
                case "saja_hu":
                {
                    var me = runner.player != null ? (Vector2)runner.player.position : center;
                    field.Sim.QueryCircle(new float2(me.x, me.y), ult.Param("radius") * sc + 0.01f, _near);
                    _picked.Clear();
                    foreach (int i in _near)
                    {
                        if (i < 0 || i >= field.Sim.Count) continue;
                        var a = field.Sim.Get(i);
                        if (cast.InRadius(Vector2.Distance(me, new Vector2(a.Pos.x, a.Pos.y)), sc)) _picked.Add(i);
                    }
                    if (cast.StunSeconds > 0f) field.Freeze(_picked, cast.StunSeconds);
                    LastHits = field.Damage(_picked, dmg, me, 0f);
                    return true;
                }
                case "mancheon_hwau":
                {
                    if (cast.Poisons && cast.PoisonTickSeconds > 0f)
                    {
                        float might = runner.Stats != null ? runner.Stats.Might : 1f;
                        field.Poison(_near, cast.PoisonSeconds, cast.PoisonTickDamage(might) / cast.PoisonTickSeconds);
                    }
                    LastHits = field.Damage(_near, dmg, center, 0f);
                    return true;
                }
                case "yeokryu":
                {
                    // 역류(하연): 몸에서 바라보는 쪽 화면 끝까지 폭 WaveWidth 띠(InSweep) 안 적에 한 방, 끝 쪽으로 민다.
                    // WaveSpeed 는 플레이어 기본 이동 100 기준이라 m/s = MoveBase × WaveSpeed / 100 (× 경지 속도 축척). 넉백 속도로 준다.
                    var me = runner.player != null ? (Vector2)runner.player.position : center;
                    var wr = runner.weapons != null ? runner.weapons : Object.FindFirstObjectByType<WeaponRunner>();
                    Vector2 face = wr != null ? wr.Facing : Vector2.right;
                    if (face.sqrMagnitude < 1e-6f) face = Vector2.right;
                    face.Normalize();
                    float hh = cam.orthographicSize, hw = hh * cam.aspect;
                    // 화면 끝까지 거리: 바라보는 쪽 반직선이 카메라 사각형을 나가는 곳
                    float tx = Mathf.Abs(face.x) > 1e-5f ? ((face.x > 0 ? center.x + hw : center.x - hw) - me.x) / face.x : float.MaxValue;
                    float ty = Mathf.Abs(face.y) > 1e-5f ? ((face.y > 0 ? center.y + hh : center.y - hh) - me.y) / face.y : float.MaxValue;
                    float reach = Mathf.Max(0f, Mathf.Min(tx, ty));
                    var mid = me + face * (reach * 0.5f);
                    field.Sim.QueryCircle(new float2(mid.x, mid.y), reach * 0.5f + cast.WaveWidth(sc) + 0.01f, _near);
                    _picked.Clear();
                    var o = new System.Numerics.Vector2(me.x, me.y);
                    var fv = new System.Numerics.Vector2(face.x, face.y);
                    foreach (int i in _near)
                    {
                        if (i < 0 || i >= field.Sim.Count) continue;
                        var a = field.Sim.Get(i);
                        if (cast.InSweep(o, fv, new System.Numerics.Vector2(a.Pos.x, a.Pos.y), reach, sc)) _picked.Add(i);
                    }
                    var ws = BloodRain.Sim.WorldScale.Current;
                    float waveMps = DataBoot.Data.Balance.Stats.MoveBase * cast.WaveSpeed / 100f;
                    if (ws != null) waveMps = ws.Speed(waveMps);
                    // 넉백은 from 에서 바깥쪽이라, from 을 몸 한참 뒤에 두면 모두 바라보는 쪽으로 나란히 밀린다
                    LastSweepReach = reach;
                    LastHits = field.Damage(_picked, dmg, me - face * 10000f, waveMps);
                    return true;
                }
                case "palbang_pungu":
                {
                    var wr = runner.weapons != null ? runner.weapons : Object.FindFirstObjectByType<WeaponRunner>();
                    if (wr == null) return false;
                    StartCoroutine(Volleys(wr, cast, dmg, sc));
                    LastHits = 0;
                    return true;
                }
            }
            return false;
        }

        // 팔방풍우: 바라보는 쪽부터 Directions 여덟 갈래로 장풍 탄, VolleyGapSeconds(세상 시간)마다 Volleys 번.
        System.Collections.IEnumerator Volleys(WeaponRunner wr, UltimateCast cast, float dmg, float sc)
        {
            var shot = cast.Shot;
            var ws = BloodRain.Sim.WorldScale.Current;
            float speed = ws != null ? ws.Speed(shot.Speed) : shot.Speed, knock = ws != null ? ws.Speed(cast.Knockback) : cast.Knockback;
            for (int v = 0; v < cast.Volleys; v++)
            {
                if (v > 0) yield return new WaitForSeconds(cast.VolleyGapSeconds);
                var f = wr.Facing;
                foreach (var d in cast.Directions(new System.Numerics.Vector2(f.x, f.y)))
                    wr.ShootFree(Mathf.Atan2(d.Y, d.X), speed, shot.Radius * sc, dmg, shot.LifeSeconds, knock);
                VolleysFired++;
            }
        }
        /// <summary>시험용: 팔방풍우로 쏜 연타 수(누적).</summary>
        public int VolleysFired { get; private set; }

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
