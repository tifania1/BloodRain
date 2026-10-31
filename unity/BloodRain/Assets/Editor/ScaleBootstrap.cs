using System.IO;
using BloodRain.Presentation;
using BloodRain.Presentation.Fx;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace BloodRain.Editor
{
    // TU-01 축척 시험 씬(TU-02 돌파, TU-03 밟힘도 여기서 본다). 다시 만들 수 있게 메뉴로 짓는다.
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
            var driver = new GameObject("ScaleTestDriver");
            driver.AddComponent<ScaleTestDriver>();
            driver.AddComponent<CrushTestField>(); // TU-03 밟힘과 쓸림 (E 적 무리, T 바닥 물건)

            // TU-05 혈우 장막. 돌파 연출(BreakthroughFx)은 드라이버가 실행 때 붙이니 드라이버 자식으로 둔다.
            var curtain = new GameObject("혈우 장막");
            curtain.transform.SetParent(driver.transform, false);
            curtain.AddComponent<MeshFilter>();
            curtain.AddComponent<MeshRenderer>().sharedMaterial = EnsureCurtainMaterial();
            curtain.AddComponent<BloodCurtain>();

            // TU-06 장막 너머 그림자(덩이 렌더러를 윗 경지 배율로)
            var shadows = new GameObject("장막 너머 그림자");
            shadows.transform.SetParent(driver.transform, false);
            shadows.AddComponent<ClusterRenderer>().material = ClusterBootstrap.EnsureMaterial();
            shadows.AddComponent<CurtainShadows>();

            // TU-07 소품 층. 청운진 한가운데를 출생지(Unity 0,0)로 둔다
            var props = new GameObject("소품 층");
            var pl = props.AddComponent<PropLayer>();
            pl.material = ClusterBootstrap.EnsureMaterial();
            pl.originRegion = "cheongun";

            EditorSceneManager.SaveScene(scene, TestScenePath);
            Debug.Log($"[ScaleBootstrap] {TestScenePath} 만듦");
        }

        const string CurtainMatPath = "Assets/Art/Materials/BloodCurtain.mat";

        public static Material EnsureCurtainMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(CurtainMatPath);
            if (mat != null) return mat;
            Directory.CreateDirectory("Assets/Art/Materials");
            mat = new Material(Shader.Find("BloodRain/BloodCurtain")) { name = "BloodCurtain" };
            AssetDatabase.CreateAsset(mat, CurtainMatPath);
            AssetDatabase.SaveAssets();
            return mat;
        }
    }
}
