using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BloodRain.Presentation.Meta
{
    // 침방 화면 2, 벽의 중원 지도(TU-12, 기획서 21장 '침방의 중원 지도'). 지명·깃발·원은 모두 여기서 얹는다(그림에 글자 없음).
    //   밟은 땅: 먹 글씨. 못 가 본 땅: 붉은 구름 아래 지명만 흐릿하게. 봉인석을 가져온 땅: 구름이 걷히고 문파 깃발.
    //   후보 카드에 커서가 가면: 그 문파 자리에 붉은 점, 절정·초절정 장막 원을 먹선으로, 원 안에 드는 땅의 주인을 주홍으로.
    // 지도 판은 세계(4,000 × 2,600m) 비율 그대로라 지도 비율(pos) × 판 크기가 곧 자리다. 장막 반경은 Core Scale.BoundaryRadius.
    public sealed class ChimbangMap : MonoBehaviour
    {
        static readonly Color Ink = new Color(0.12f, 0.10f, 0.09f);
        static readonly Color Crimson = new Color32(179, 38, 30, 255);
        const string CrimsonHex = "#B3261E";

        GameData _data;
        Regions _regions;
        Scale _scale;
        RectTransform _panel;
        TMP_FontAsset _font;
        Sprite _soft, _dot;
        readonly Dictionary<string, TextMeshProUGUI> _labels = new Dictionary<string, TextMeshProUGUI>();
        readonly List<GameObject> _hover = new List<GameObject>();
        Soul _soul;
        int _realmJeol, _realmChojeol;

        public RectTransform Panel => _panel;

        public void Build(RectTransform parent, TMP_FontAsset font, Texture map, GameData data, Vector2 size)
        {
            _data = data;
            _font = font;
            _regions = new Regions(data);
            _scale = new Scale(data);
            _realmJeol = RealmByName("절정");
            _realmChojeol = RealmByName("초절정");
            _soft = MakeDisc(64, soft: true);
            _dot = MakeDisc(32, soft: false);

            // 벽에 건 족자: 먹색 테두리 + 지도
            var frame = new GameObject("벽의 중원 지도", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
            frame.SetParent(parent, false);
            frame.GetComponent<Image>().color = new Color(0.25f, 0.20f, 0.17f);
            frame.GetComponent<Image>().raycastTarget = false;
            frame.sizeDelta = size + new Vector2(16, 16);

            _panel = new GameObject("지도", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RectTransform>();
            _panel.SetParent(frame, false);
            _panel.sizeDelta = size;
            var raw = _panel.GetComponent<RawImage>();
            raw.texture = map;
            raw.color = map != null ? Color.white : new Color(0.86f, 0.82f, 0.72f);
            raw.raycastTarget = false;
            _panel.gameObject.AddComponent<RectMask2D>(); // 초절정 원은 지도 밖까지 나간다
        }

        public RectTransform Frame => (RectTransform)_panel.parent;

        int RealmByName(string ko)
        {
            for (int i = 0; i < _scale.Realms.Count; i++) if (_scale.Realms[i].NameKo == ko) return i;
            return -1;
        }

        /// <summary>그 혼이 밟은 땅·봉인석으로 지명과 구름, 깃발을 다시 놓는다.</summary>
        public void SetSoul(Soul soul)
        {
            _soul = soul;
            for (int i = _panel.childCount - 1; i >= 0; i--) Destroy(_panel.GetChild(i).gameObject);
            _labels.Clear();
            _hover.Clear();
            var size = _panel.sizeDelta;
            foreach (var r in _regions.Visible(soul.SealStoneCount))
            {
                var p = MapPos(r, size);
                bool visited = soul.HasVisited(r.Id);
                bool sealed_ = r.SealStone != null && soul.SealStones.Contains(r.SealStone);
                if (!visited && !sealed_)
                {
                    // 붉은 구름: 짙은 땅일수록 진하게
                    var cloud = Img("붉은 구름 " + r.NameKo, _soft, new Color(Crimson.r, Crimson.g, Crimson.b, Mathf.Min(0.55f, 0.18f + 0.08f * r.Density)));
                    Put(cloud, p, Vector2.one * size.x * 0.11f);
                }
                if (sealed_ && r.Sect != null)
                {
                    var flag = Text("깃발 " + r.NameKo, "旗", 20, Crimson, FontStyles.Bold);
                    Put(flag.rectTransform, p + new Vector2(0, 26), new Vector2(30, 30));
                }
                // 지명 밑에 옅은 종이 번짐(바쁜 수묵 위에서 읽히게)
                var under = Img("지명 바탕 " + r.NameKo, _soft, new Color(0.93f, 0.89f, 0.80f, visited || sealed_ ? 0.75f : 0.35f));
                Put(under, p + new Vector2(0, -14), new Vector2(r.NameKo.Length * 18f + 40f, 30));
                var label = Text("지명 " + r.NameKo, r.NameKo, 16, visited || sealed_ ? Ink : new Color(Ink.r, Ink.g, Ink.b, 0.38f),
                    visited || sealed_ ? FontStyles.Bold : FontStyles.Normal);
                Put(label.rectTransform, p + new Vector2(0, -14), new Vector2(160, 24));
                _labels[r.Id] = label;
                var mark = Img("점 " + r.NameKo, _dot, label.color);
                Put(mark, p, Vector2.one * 6);
            }
        }

        /// <summary>후보 카드에 커서가 가면 그 문파 자리와 절정·초절정 장막 원. null 이면 지운다.</summary>
        public void Show(RebirthCandidate cand)
        {
            foreach (var g in _hover) Destroy(g);
            _hover.Clear();
            if (_soul == null) return;
            foreach (var kv in _labels)
            {
                var r = _regions.Region(kv.Key);
                bool known = _soul.HasVisited(r.Id) || (r.SealStone != null && _soul.SealStones.Contains(r.SealStone));
                kv.Value.text = r.NameKo;
                kv.Value.color = known ? Ink : new Color(Ink.r, Ink.g, Ink.b, 0.38f);
            }
            if (cand == null) return;

            var size = _panel.sizeDelta;
            var birth = _regions.Center(cand.Region);
            var p = MapPos(cand.Region, size);
            float pxPerMeter = size.x / _regions.WidthMeters;
            foreach (var realm in new[] { _realmJeol, _realmChojeol })
            {
                if (realm < 0) continue;
                var rad = _scale.BoundaryRadius(realm);
                if (!rad.HasValue) continue;
                var ring = new GameObject($"장막 원 {_scale.Realms[realm].NameKo}", typeof(RectTransform), typeof(CanvasRenderer), typeof(InkRing));
                ring.transform.SetParent(_panel, false);
                var ir = ring.GetComponent<InkRing>();
                ir.radius = rad.Value * pxPerMeter;
                ir.width = realm == _realmJeol ? 2.2f : 1.6f;
                ir.color = new Color(Ink.r, Ink.g, Ink.b, realm == _realmJeol ? 0.85f : 0.6f);
                ir.dashed = realm != _realmJeol;
                ((RectTransform)ring.transform).anchoredPosition = p;
                _hover.Add(ring);
                var tag = Text("원 이름", _scale.Realms[realm].NameKo, 15, ir.color, FontStyles.Italic);
                Put(tag.rectTransform, p + new Vector2(0, ir.radius + 10), new Vector2(80, 20));
                _hover.Add(tag.gameObject);
            }
            var dot = Img("태어나는 곳", _dot, Crimson);
            Put(dot, p, Vector2.one * 14);
            _hover.Add(dot.gameObject);

            // 초절정 원(없으면 절정) 안에 드는 땅의 주인
            var outer = _scale.BoundaryRadius(_realmChojeol) ?? _scale.BoundaryRadius(_realmJeol) ?? 0f;
            foreach (var kv in _labels)
            {
                var r = _regions.Region(kv.Key);
                if (r.Boss == null) continue;
                if (System.Numerics.Vector2.Distance(_regions.Center(r), birth) > outer) continue;
                string boss = _data.Bosses.FirstOrDefault(e => e.Id == r.Boss)?.NameKo ?? r.Boss;
                kv.Value.text = $"{r.NameKo} <color={CrimsonHex}><b>· {boss}</b></color>";
                kv.Value.color = Ink;
            }
        }

        Vector2 MapPos(RegionDef r, Vector2 size) => new Vector2((r.Pos[0] - 0.5f) * size.x, (0.5f - r.Pos[1]) * size.y);

        RectTransform Img(string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(_panel, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite; img.color = color; img.raycastTarget = false;
            return (RectTransform)go.transform;
        }

        TextMeshProUGUI Text(string name, string text, float size, Color color, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(_panel, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (_font != null) t.font = _font;
            t.text = text; t.fontSize = size; t.color = color; t.fontStyle = style;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }

        static void Put(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static Sprite MakeDisc(int n, bool soft)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            float c = (n - 1) / 2f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = soft ? Mathf.Pow(Mathf.Clamp01(1f - d), 1.6f) : Mathf.Clamp01((1f - d) * c);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }
    }
}
