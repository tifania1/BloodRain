using System.Collections.Generic;
using BloodRain.Core.World;
using BloodRain.Presentation.Meta;
using TMPro;
using UnityEngine;

namespace BloodRain.Presentation.Fx
{
    // DN-01U 피해 숫자(기획서 11장 '동시 피해 숫자 표시 80', 12장 설정 '피해 숫자 표시', 15장 풀링).
    //   어느 맞음에 띄울지·상한·수명은 Core DamageNumbers(balance.json damageNumber·limits.damageNumbers). 여기는 그리기만.
    //   웹 template.html '피해 숫자'와 같게: 보통은 먹색 굵은 작은 글, 치명타는 주홍 더 굵고 크게, 둘 다 종이색 테두리,
    //   수명 동안 위로 떠오르며 옅어진다. 설정 NumbersOn 을 끄면 그리기만 끈다(목록은 그대로 돈다).
    public sealed class DamageNumberLayer : MonoBehaviour
    {
        public EnemyField enemies;
        public TMP_FontAsset font;
        /// <summary>보통 숫자 글 높이(1080 높이 화면 기준 px). 웹 12px.</summary>
        public float normalPx = 22f;
        /// <summary>치명타 숫자 글 높이(px). 웹 17px.</summary>
        public float critPx = 31f;
        /// <summary>수명 동안 떠오르는 높이(1080 높이 기준 px). 웹 약 26px.</summary>
        public float risePx = 26f;
        public int sortingOrder = 400;
        /// <summary>Damage Numbers Pro 프리팹으로 띄운다(2026-11-18 DN-01U). 끄거나 프리팹이 없으면 예전 TextMeshPro 풀.</summary>
        public bool usePro = false; // 2026-11-18 PC: 켜고 숫자를 띄우자 D3D12 텍스처 올리기로 메모리가 34GB 까지 차 에디터가 두 번 죽음. 원인 찾기 전까지 끔(unity_tasks DN-01U)
        /// <summary>잇달아 들어간 숫자를 합치는 거리(1080 높이 화면 기준 px). 화면 거리라 경지 축척과 상관없다.</summary>
        public float combinePx = 48f;
        /// <summary>Resources/Fx 의 프리팹(메뉴 `BloodRain/Bootstrap/피해 숫자 프리팹 짓기(DNP)`).</summary>
        public DamageNumbersPro.DamageNumber proNormal, proCrit;
        DamageNumbersPro.DamageNumber _tplNormal, _tplCrit;
        bool _proReady;

        const float PopSeconds = 0.12f;
        static readonly Color Ink = new Color32(0x1A, 0x1A, 0x1A, 0xFF);
        static readonly Color Vermilion = new Color32(0xB3, 0x26, 0x1E, 0xFF); // 13장 주홍(혈우)
        static readonly Color Paper = new Color32(0xEF, 0xE6, 0xD2, 0xFF);

        DamageNumbers _nums;
        readonly System.Random _rng = new System.Random();
        readonly List<TextMeshPro> _pool = new List<TextMeshPro>();
        readonly List<int> _shownValue = new List<int>();
        readonly List<bool> _shownCrit = new List<bool>();
        Material _mat;
        bool _hooked;

        /// <summary>시험용: 떠 있는 숫자 수.</summary>
        public int Count => _nums != null ? _nums.Items.Count : 0;
        /// <summary>시험용: 화면에 켜진 글자 수.</summary>
        public int Visible { get; private set; }

        void OnEnable()
        {
            if (DataBoot.Data != null && _nums == null) _nums = new DamageNumbers(DataBoot.Data.Balance);
            Hook();
        }

        void OnDisable()
        {
            if (_hooked && enemies != null) enemies.Struck -= OnStruck;
            _hooked = false;
        }

        void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
        }

        void Hook()
        {
            if (_hooked) return;
            if (enemies == null) enemies = FindFirstObjectByType<EnemyField>();
            if (enemies == null) return;
            enemies.Struck += OnStruck;
            _hooked = true;
        }

        void OnStruck(Vector2 at, float radius, float damage, bool crit, bool eliteOrBoss)
        {
            if (_nums == null) return;
            if (!_nums.ShouldShow(damage, crit, eliteOrBoss, _rng.NextDouble())) return;
            var it = _nums.Add(at.x, at.y, radius, damage, crit, _rng.NextDouble());
            if (ProReady()) SpawnPro(it);
        }

        bool ProReady()
        {
            if (!usePro) return false;
            if (_proReady) return true;
            if (proNormal == null) proNormal = Resources.Load<DamageNumbersPro.DamageNumber>("Fx/DamageNumberInk");
            if (proCrit == null) proCrit = Resources.Load<DamageNumbersPro.DamageNumber>("Fx/DamageNumberCrit");
            if (proNormal == null || proCrit == null) { usePro = false; return false; }
            _tplNormal = MakeTemplate(proNormal, Ink);
            _tplCrit = MakeTemplate(proCrit, Vermilion);
            _proReady = true;
            return true;
        }

        // 프리팹을 꺼진 채로 한 벌 복제해 글꼴·종이색 테두리·색을 얹고, 그것을 Spawn 원본으로 쓴다(풀도 이 원본 기준).
        DamageNumbersPro.DamageNumber MakeTemplate(DamageNumbersPro.DamageNumber prefab, Color color)
        {
            var go = Instantiate(prefab.gameObject, transform);
            go.SetActive(false);
            go.name = prefab.name + " 원본";
            var dn = go.GetComponent<DamageNumbersPro.DamageNumber>();
            foreach (var t in go.GetComponentsInChildren<TMP_Text>(true))
            {
                if (font != null) t.font = font;
                t.fontSharedMaterial = OutlineMat(t);
                t.color = color;
            }
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.sortingOrder = sortingOrder;
            if (DataBoot.Data != null) dn.maxActiveInstances = DataBoot.Data.Balance.Limits.DamageNumbers;
            return dn;
        }

        Material OutlineMat(TMP_Text t)
        {
            if (_mat == null)
            {
                // 종이색 테두리 한 장을 모두가 나눠 쓴다(정점 알파가 테두리까지 옅게 한다)
                _mat = new Material(t.fontSharedMaterial);
                _mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
                _mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.25f);
                _mat.SetColor(ShaderUtilities.ID_OutlineColor, Paper);
                _mat.SetFloat(ShaderUtilities.ID_FaceDilate, 0.1f);
            }
            return _mat;
        }

        void SpawnPro(in DamageNumber it)
        {
            var cam = Camera.main;
            if (!SettingsScreen.Current.NumbersOn || cam == null || !cam.orthographic) return;
            float worldPerPx = 2f * cam.orthographicSize / 1080f;
            var tpl = it.Crit ? _tplCrit : _tplNormal;
            var dn = tpl.Spawn(new Vector3(it.X, it.Y + it.Lift, 0f), it.Value);
            dn.SetScale((it.Crit ? critPx : normalPx) * worldPerPx);
            var c = dn.combinationSettings;
            c.maxDistance = combinePx * worldPerPx;
            dn.combinationSettings = c;
        }

        void LateUpdate()
        {
            if (_nums == null)
            {
                if (DataBoot.Data == null) return;
                _nums = new DamageNumbers(DataBoot.Data.Balance);
            }
            Hook();
            _nums.Tick(Time.deltaTime);

            var cam = Camera.main;
            var items = _nums.Items;
            // Pro 가 그리면 Core 목록은 상한·떠 있는 수 세기에만 쓰고 예전 풀은 끈다
            bool on = !_proReady && SettingsScreen.Current.NumbersOn && cam != null && cam.orthographic;
            int n = on ? items.Count : 0;
            if (on)
            {
                float worldPerPx = 2f * cam.orthographicSize / 1080f;
                float life = Mathf.Max(0.01f, _nums.LifeSeconds);
                for (int i = 0; i < n; i++)
                {
                    var it = items[i];
                    var t = Slot(i);
                    if (_shownValue[i] != it.Value || _shownCrit[i] != it.Crit)
                    {
                        t.SetText("{0}", it.Value);
                        t.fontStyle = FontStyles.Bold;
                        // TextMeshPro 글자 크기 10 이 세계 1m 줄 높이 안팎
                        t.fontSize = (it.Crit ? critPx : normalPx) * worldPerPx * 10f;
                        _shownValue[i] = it.Value;
                        _shownCrit[i] = it.Crit;
                    }
                    var c = it.Crit ? Vermilion : Ink;
                    c.a = _nums.Alpha(it);
                    t.color = c;
                    float y = it.Y + it.Lift + it.Age / life * risePx * worldPerPx;
                    t.transform.position = new Vector3(it.X, y, 0f);
                    // 처음 0.12초 톡 튀어 나왔다 제 크기로(치명은 더 크게, 2026-11-21) — 숫자가 붙어 나와 밋밋했다
                    float pop = it.Age < PopSeconds ? 1f + (it.Crit ? 0.45f : 0.2f) * (1f - it.Age / PopSeconds) : 1f;
                    t.transform.localScale = new Vector3(pop, pop, 1f);
                    if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
                }
            }
            for (int i = n; i < _pool.Count; i++)
                if (_pool[i].gameObject.activeSelf) _pool[i].gameObject.SetActive(false);
            Visible = n;
        }

        TextMeshPro Slot(int i)
        {
            while (_pool.Count <= i)
            {
                var go = new GameObject("피해 숫자");
                go.transform.SetParent(transform, false);
                var t = go.AddComponent<TextMeshPro>();
                if (font != null) t.font = font;
                if (_mat == null)
                {
                    // 종이색 테두리 한 장을 모두가 나눠 쓴다(정점 알파가 테두리까지 옅게 한다)
                    _mat = new Material(t.fontSharedMaterial);
                    _mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
                    _mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.25f);
                    _mat.SetColor(ShaderUtilities.ID_OutlineColor, Paper);
                    _mat.SetFloat(ShaderUtilities.ID_FaceDilate, 0.1f);
                }
                t.fontSharedMaterial = _mat;
                t.alignment = TextAlignmentOptions.Center;
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.rectTransform.sizeDelta = new Vector2(4f, 1f);
                t.sortingOrder = sortingOrder;
                go.SetActive(false);
                _pool.Add(t);
                _shownValue.Add(int.MinValue);
                _shownCrit.Add(false);
            }
            return _pool[i];
        }
    }
}
