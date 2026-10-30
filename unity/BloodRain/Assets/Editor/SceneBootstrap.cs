using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BloodRain.Editor
{
    // 기본 씬을 다시 만들 수 있게 코드로 둔다. 이미 있는 씬은 건드리지 않는다.
    public static class SceneBootstrap
    {
        public const string SceneDir = "Assets/Scenes";
        public static readonly string[] SceneNames = { "Boot", "Run", "Inn" };

        [MenuItem("BloodRain/Bootstrap/기본 씬 만들기")]
        public static void CreateBaseScenes()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(SceneDir);

            var build = new List<EditorBuildSettingsScene>();
            foreach (var name in SceneNames)
            {
                var path = $"{SceneDir}/{name}.unity";
                if (!File.Exists(path))
                {
                    var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                    var cam = camGo.AddComponent<Camera>();
                    cam.orthographic = true;
                    cam.orthographicSize = 5f;
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.93f, 0.90f, 0.84f); // 한지색 임시 배경
                    camGo.transform.position = new Vector3(0f, 0f, -10f);
                    EditorSceneManager.SaveScene(scene, path);
                    Debug.Log($"[Bootstrap] {path} 만듦");
                }
                build.Add(new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = build.ToArray(); // Boot 가 0번
            EditorSceneManager.OpenScene($"{SceneDir}/Boot.unity");
            AssetDatabase.SaveAssets();
        }
    }
}