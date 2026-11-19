using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace BloodRain.Editor
{
    // 소품 낱장(Art/Props/Parts)을 스프라이트 아틀라스 하나로 묶는다(WEB-01U, 2026-11-19).
    // 까닭: WebGL 은 밉맵을 켠 텍스처가 2의 거듭제곱이 아니면 DXT 로 못 눌러 RGBA32(4배)로 싣는다.
    // 낱장은 740x640 같은 크기라 밉맵을 끄면 눌리지만 경지가 오르면 작게 보여 자글거린다(Captures 의 web01_props_realm1 비교).
    // 아틀라스는 2의 거듭제곱 판에 모아 밉맵 + DXT5 를 둘 다 쓴다. PropLayer 는 sprite.textureRect 로 UV 를 잡아 그대로 맞는다.
    // 다시 돌려도 된다(있으면 설정만 다시 맞춤).
    public static class PropAtlasBootstrap
    {
        const string AtlasPath = "Assets/Art/Props/PropParts.spriteatlasv2";
        const string Folder = "Assets/Art/Props/Parts";

        [MenuItem("BloodRain/Bootstrap/소품 아틀라스 짓기(WebGL 압축)")]
        public static void Build()
        {
            if (EditorSettings.spritePackerMode != SpritePackerMode.SpriteAtlasV2)
                EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;

            if (AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasPath) == null)
            {
                var asset = new SpriteAtlasAsset();
                asset.Add(new Object[] { AssetDatabase.LoadAssetAtPath<DefaultAsset>(Folder) });
                SpriteAtlasAsset.Save(asset, AtlasPath);
                AssetDatabase.Refresh();
            }

            var imp = (SpriteAtlasImporter)AssetImporter.GetAtPath(AtlasPath);
            imp.includeInBuild = true;
            imp.packingSettings = new SpriteAtlasPackingSettings
            {
                enableRotation = false,
                enableTightPacking = false,
                enableAlphaDilation = true,
                padding = 4,
            };
            imp.textureSettings = new SpriteAtlasTextureSettings
            {
                readable = false,
                generateMipMaps = true,
                sRGB = true,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 1,
            };
            var def = imp.GetPlatformSettings("DefaultTexturePlatform");
            def.maxTextureSize = 4096;
            def.textureCompression = TextureImporterCompression.Compressed;
            imp.SetPlatformSettings(def);
            imp.SaveAndReimport();
            SpriteAtlasUtility.PackAllAtlases(EditorUserBuildSettings.activeBuildTarget);
            AssetDatabase.SaveAssets();
            Debug.Log("[PropAtlas] " + AtlasPath + " 지음");
        }
    }
}
