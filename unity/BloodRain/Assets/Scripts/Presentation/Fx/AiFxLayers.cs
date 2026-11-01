using UnityEngine;

namespace BloodRain.Presentation.Fx
{
    // AI 이펙트 재료 한 장을 층대로 겹쳐 그린다(V-04, art_pipeline.md '적용').
    //   ai_ink  : stroke 층 하나, 먹 #141414, 보통 섞기(BrushStroke.mat).
    //   ai_glow : shade(먹 받침, 보통 섞기) → gold(금빛 #C9A227, 더하기) → core(흰빛, 더하기) 순서로 깐다.
    //             받침이 없으면 밝은 종이 바닥에서 더하기 빛이 크림색으로 묻힌다(2026-11-01 PC 화면).
    //   그어짐(_Progress)·흩어짐(_Dissolve)·방향(_Kind, ai.json progress)은 층 모두 같은 값으로 묶는다.
    //   텍스처는 VfxBootstrap 이 Sprite(PPU 512/sizeMeters)로 가져온다. 무공에 물리는 것은 무공 쪽(C-04 이펙트)에서.
    public sealed class AiFxLayers : MonoBehaviour
    {
        public static readonly Color InkColor = new Color32(0x14, 0x14, 0x14, 0xFF);
        public static readonly Color GoldColor = new Color32(0xC9, 0xA2, 0x27, 0xFF);

        public Material normalMaterial;
        public Material addMaterial;
        public Texture2D stroke, shade, gold, core;
        [Tooltip("ai.json progress → VfxBootstrap.KindOf (right 1·radial 2·down 3·up 4·left 5)")]
        public float kind = 1f;
        [Range(0, 1)] public float progress = 1f;
        [Range(0, 1)] public float dissolve;
        public int sortingOrder = 20;

        SpriteRenderer[] _layers;
        MaterialPropertyBlock _mpb;

        void OnEnable() => Build();

        public void Build()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var c = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
            }
            var list = new System.Collections.Generic.List<SpriteRenderer>();
            Add(list, "stroke", stroke, normalMaterial, InkColor, 0);
            Add(list, "shade", shade, normalMaterial, InkColor, 0);
            Add(list, "gold", gold, addMaterial, GoldColor, 1);
            Add(list, "core", core, addMaterial, Color.white, 2);
            _layers = list.ToArray();
            _mpb ??= new MaterialPropertyBlock();
            Apply();
        }

        void Add(System.Collections.Generic.List<SpriteRenderer> list, string name, Texture2D tex, Material mat, Color color, int order)
        {
            if (tex == null || mat == null) return;
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteOf(tex);
            sr.sharedMaterial = mat;
            sr.color = color;
            sr.sortingOrder = sortingOrder + order;
            list.Add(sr);
        }

        static Sprite SpriteOf(Texture2D tex)
        {
#if UNITY_EDITOR
            var path = UnityEditor.AssetDatabase.GetAssetPath(tex);
            if (!string.IsNullOrEmpty(path))
                foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
                    if (o is Sprite s) return s;
#endif
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 512f / 4f);
        }

        /// <summary>그어짐·흩어짐을 층 모두에 같은 값으로 건다.</summary>
        public void Apply()
        {
            if (_layers == null) return;
            _mpb ??= new MaterialPropertyBlock();
            foreach (var sr in _layers)
            {
                sr.GetPropertyBlock(_mpb);
                _mpb.SetTexture("_MainTex", sr.sprite.texture);
                _mpb.SetColor("_Color", sr.color); // 셰이더는 꼭짓점 색이 아니라 _Color 를 쓴다
                _mpb.SetFloat("_Progress", progress);
                _mpb.SetFloat("_Dissolve", dissolve);
                _mpb.SetFloat("_Kind", kind);
                sr.SetPropertyBlock(_mpb);
            }
        }

        void OnValidate() { if (_layers != null) Apply(); }
    }
}
