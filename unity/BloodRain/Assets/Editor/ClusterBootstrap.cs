using System.IO;
using BloodRain.Presentation;
using BloodRain.Presentation.Fx;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BloodRain.Editor
{
    // TU-04 덩이 렌더러 시험 씬과 ClusterInk 머티리얼. 다시 만들 수 있게 메뉴로 짓는다.
    public static class ClusterBootstrap
    {
        const string ScenePath = "Assets/Scenes/Test/ClusterTest.unity";
        const string MatPath = "Assets/Art/Materials/ClusterInk.mat";

        public static Material EnsureMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat != null) return mat;
            Directory.CreateDirectory("Assets/Art/Materials");
            var shader = Shader.Find("BloodRain/ClusterInk");
            mat = new Material(shader) { name = "ClusterInk", enableInstancing = true };
            AssetDatabase.CreateAsset(mat, MatPath);
            AssetDatabase.SaveAssets();
            return mat;
        }

        [MenuItem("BloodRain/Test/덩이 시험 씬 만들기")]
        public static void CreateClusterTestScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var mat = EnsureMaterial();
            Directory.CreateDirectory("Assets/Scenes/Test");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.91f, 0.87f, 0.78f); // 한지 빛
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 100f;
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.AddComponent<UniversalAdditionalCameraData>();
            camGo.AddComponent<ScaleCamera>();

            new GameObject("DataBoot").AddComponent<DataBoot>();
            var go = new GameObject("ClusterTest");
            var r = go.AddComponent<ClusterRenderer>();
            r.material = mat;
            go.AddComponent<ClusterTestDriver>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[ClusterBootstrap] {ScenePath} 만듦");
        }
    }
}
