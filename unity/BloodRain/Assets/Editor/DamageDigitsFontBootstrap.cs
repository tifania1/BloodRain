using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace BloodRain.Editor
{
    // DN-01U 2026-11-22 22:50 PC: Pro 프리팹에 동적 글꼴(NotoSerifKR SDF + 뒤받침 셋)을 끼우자 숫자 하나에
    //   한 프레임 만에 아틀라스가 4 → 17 장으로 늘며 메모리가 14GB 를 넘었다. 숫자만 든 정적 SDF 를 따로 구워
    //   Pro 원본에 끼우면 실행 중에 글리프를 더하지 않으니 아틀라스가 늘 수 없다. 다시 돌려도 같은 파일을 고친다.
    public static class DamageDigitsFontBootstrap
    {
        const string SrcFont = "Assets/Art/Fonts/NotoSerifKR-Regular.otf";
        const string Out = "Assets/Resources/Fx/DamageDigits SDF.asset";
        public const string Chars = "0123456789,.-+%KMk ";

        [MenuItem("BloodRain/Bootstrap/피해 숫자 글꼴 굽기(정적)")]
        public static void Build()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(SrcFont);
            if (font == null)
            {
                foreach (var g in AssetDatabase.FindAssets("NotoSerifKR t:Font"))
                {
                    font = AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(g));
                    if (font != null) break;
                }
            }
            if (font == null) { Debug.LogError("NotoSerifKR 원본 글꼴을 못 찾음"); return; }

            var fa = TMP_FontAsset.CreateFontAsset(font, 64, 6, GlyphRenderMode.SDFAA, 256, 256, AtlasPopulationMode.Dynamic, false);
            fa.name = "DamageDigits SDF";
            fa.TryAddCharacters(Chars, out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.LogWarning("피해 숫자 글꼴에 없는 글자: '" + missing + "'");
            fa.atlasPopulationMode = AtlasPopulationMode.Static;
            fa.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();

            AssetDatabase.DeleteAsset(Out);
            AssetDatabase.CreateAsset(fa, Out);
            var tex = fa.atlasTexture;
            tex.name = fa.name + " Atlas";
            AssetDatabase.AddObjectToAsset(tex, fa);
            fa.material.name = fa.name + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(Out);
            Debug.Log("피해 숫자 글꼴: " + Out + " 글자 " + fa.characterTable.Count + " 아틀라스 " + fa.atlasTextureCount);
        }
    }
}
