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
            _nums.Add(at.x, at.y, radius, damage, crit, _rng.NextDouble());
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
            bool on = SettingsScreen.Current.NumbersOn && cam != null && cam.orthographic;
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
