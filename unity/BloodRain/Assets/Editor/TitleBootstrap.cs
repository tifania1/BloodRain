using System.IO;
using System.Linq;
using BloodRain.Presentation.Meta;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BloodRain.Editor
{
    // TT-01U 타이틀 화면을 Boot 씬(빌드 0번)에 붙인다. 다시 돌리면 같은 것을 다시 채운다.
    public static class TitleBootstrap
    {
        const string BootPath = "Assets/Scenes/Boot.unity";
        const string BrushFontPath = "Assets/Art/Fonts/YujiBoku Dynamic SDF.asset";

        [MenuItem("BloodRain/Bootstrap/타이틀 화면 짓기(Boot 씬)")]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(BootPath, OpenSceneMode.Single);
            SceneBootstrap.AddDataBootToBoot();
            scene = EditorSceneManager.GetActiveScene();

            var screen = Object.FindFirstObjectByType<TitleScreen>();
            if (screen == null) screen = new GameObject("TitleScreen").AddComponent<TitleScreen>();
            screen.font = HudBootstrap.EnsureBodyFont();
            screen.brushFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BrushFontPath);
            screen.background = FindSprite("Assets/Art/Backgrounds/bg_title");

            if (Object.FindFirstObjectByType<Camera>() == null)
            {
                var cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color32(0xEF, 0xE6, 0xD2, 0xFF);
            }

            EditorUtility.SetDirty(screen);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Bootstrap] 타이틀 화면 붙임(바탕 {(screen.background != null ? "bg_title" : "없음 — 선지")}, 붓 글꼴 {(screen.brushFont != null ? "있음" : "없음")})");
        }

        static Sprite FindSprite(string pathNoExt)
        {
            var path = new[] { ".png", ".jpg" }.Select(e => pathNoExt + e).FirstOrDefault(File.Exists);
            if (path == null) return null;
            if (AssetImporter.GetAtPath(path) is TextureImporter ti && ti.textureType != TextureImporterType.Sprite)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
