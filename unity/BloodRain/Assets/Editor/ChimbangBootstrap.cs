using System.IO;
using System.Linq;
using BloodRain.Presentation;
using BloodRain.Presentation.Meta;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BloodRain.Editor
{
    // TU-11 침방 시험 씬. 다시 만들 수 있게 메뉴로 짓는다. 그림(art_queue)이 들어와 있으면 찾아 건다.
    //   bg_chimbang → Assets/Art/Backgrounds/bg_chimbang.(png|jpg), card_sect_<문파 id> → Assets/Art/Cards/card_sect_<id>.(png|jpg)
    public static class ChimbangBootstrap
    {
        const string TestScenePath = "Assets/Scenes/Test/ChimbangTest.unity";

        [MenuItem("BloodRain/Test/침방 시험 씬 만들기")]
        public static void CreateTestScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Assets/Scenes/Test");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.91f, 0.87f, 0.78f);
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.AddComponent<UniversalAdditionalCameraData>();

            new GameObject("DataBoot").AddComponent<DataBoot>();
            var screen = new GameObject("침방 화면").AddComponent<ChimbangScreen>();
            screen.font = HudBootstrap.EnsureBodyFont();
            screen.background = FindSprite("Assets/Art/Backgrounds/bg_chimbang");
            screen.allSects = true;   // 시험: 모든 문파에서 뽑아 카드를 두루 본다
            screen.extraRerolls = 2;
            foreach (var s in DataBoot.Load().Cheonha.Sects)
            {
                var art = FindSprite($"Assets/Art/Cards/card_sect_{s.Id}");
                if (art != null) screen.sectArt.Add(new ChimbangScreen.SectArt { sectId = s.Id, art = art });
            }

            EditorSceneManager.SaveScene(scene, TestScenePath);
            Debug.Log($"[ChimbangBootstrap] {TestScenePath} 만듦, 산문 그림 {screen.sectArt.Count}장, 배경 {(screen.background != null ? "있음" : "없음")}");
        }

        /// <summary>확장자 없는 경로로 png·jpg 를 찾아 Sprite 로 읽는다. 텍스처가 Sprite 로 안 들여와져 있으면 바꾼다.</summary>
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
