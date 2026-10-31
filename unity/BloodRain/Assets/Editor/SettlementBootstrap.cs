using System.IO;
using BloodRain.Presentation;
using BloodRain.Presentation.Meta;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BloodRain.Editor
{
    // TU-13 정산 시험 씬. 플레이하면 지어낸 한 생(당가, 둔재, 쓰러짐)으로 정산을 보여 준다. Enter 로 다 채우고, 다시 Enter 로 끝.
    public static class SettlementBootstrap
    {
        const string TestScenePath = "Assets/Scenes/Test/SettlementTest.unity";

        [MenuItem("BloodRain/Test/정산 시험 씬 만들기")]
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
            var screen = new GameObject("정산 화면").AddComponent<SettlementScreen>();
            screen.font = HudBootstrap.EnsureBodyFont();
            screen.demo = true;

            EditorSceneManager.SaveScene(scene, TestScenePath);
            Debug.Log($"[SettlementBootstrap] {TestScenePath} 만듦");
        }
    }
}
