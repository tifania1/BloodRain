using System.IO;
using BloodRain.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace BloodRain.Editor
{
    // TU-01 축척 시험 씬. 다시 만들 수 있게 메뉴로 짓는다.
    public static class ScaleBootstrap
    {
        const string TestScenePath = "Assets/Scenes/Test/ScaleTest.unity";

        [MenuItem("BloodRain/Test/축척 시험 씬 만들기")]
        public static void CreateScaleTestScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Assets/Scenes/Test");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.91f, 0.87f, 0.78f); // 한지 빛
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 100f;
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.AddComponent<UniversalAdditionalCameraData>();
            camGo.AddComponent<ScaleCamera>();

            var boot = new GameObject("DataBoot");
            boot.AddComponent<DataBoot>();
            new GameObject("ScaleTestDriver").AddComponent<ScaleTestDriver>();

            EditorSceneManager.SaveScene(scene, TestScenePath);
            Debug.Log($"[ScaleBootstrap] {TestScenePath} 만듦");
        }
    }
}
