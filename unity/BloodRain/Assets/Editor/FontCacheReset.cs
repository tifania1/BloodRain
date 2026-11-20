using System.Linq;
using TMPro;
using UnityEditor;

namespace BloodRain.Editor
{
    // 글꼴 파일(tools/subset_fonts.py 가 다시 자른 otf·ttf)이 바뀌면 그 글꼴을 쓰는 동적 TMP 글꼴 에셋의 캐시를 비운다.
    // 동적 에셋은 처음 그린 글자를 '글자 → 글꼴 파일 속 글리프 번호'로 저장해 두는데, 글꼴을 다시 자르면 번호가 바뀌어
    // 캐시에 남은 글자가 빈칸으로 나왔다(2026-11-20 레벨업 카드 '단'·'류' 빠짐). 비워 두면 그릴 때 새 파일에서 다시 만든다.
    // 글꼴 엔진이 붙들고 있던 옛 얼굴도 내려놓는다. 빌드 때도 비우도록 에셋마다 Clear Dynamic Data On Build 를 켜 두었다.
    sealed class FontCacheReset : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            var fonts = imported.Where(p => p.StartsWith("Assets/Art/Fonts/") && (p.EndsWith(".otf") || p.EndsWith(".ttf"))).ToList();
            if (fonts.Count == 0) return;
            // 글꼴 엔진이 옛 파일 얼굴을 쥐고 있으면 새로 그린 글자가 0x0(빈칸)으로 나온다(2026-11-20 확인). 먼저 내려놓는다.
            UnityEngine.TextCore.LowLevel.FontEngine.UnloadAllFontFaces();
            foreach (var guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets/Art/Fonts" }))
            {
                var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (fa == null || fa.atlasPopulationMode != AtlasPopulationMode.Dynamic || fa.sourceFontFile == null) continue;
                if (!fonts.Contains(AssetDatabase.GetAssetPath(fa.sourceFontFile))) continue;
                fa.ClearFontAssetData(true);
                EditorUtility.SetDirty(fa);
                UnityEngine.Debug.Log($"[FontCacheReset] {fa.name}: 글꼴 파일이 바뀌어 동적 글자 캐시를 비움");
            }
            AssetDatabase.SaveAssets();
        }
    }
}
