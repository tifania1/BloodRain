using BloodRain.Presentation;
using BloodRain.Presentation.Fx;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BloodRain.Editor
{
    // C-01 Run 씬: 카메라(ScaleCamera 추적), 플레이어(이동·경공 대시), 혈우 장막, 소품·수묵 지도, 전투 HUD 위 가운데.
    // 출생지는 청운진(서장 몸). 적·무공은 C-02 부터 여기에 붙인다. 다시 돌리면 Run 씬을 새로 짓는다.
    public static class RunBootstrap
    {
        const string RunScenePath = "Assets/Scenes/Run.unity";

        [MenuItem("BloodRain/Bootstrap/Run 씬 짓기(플레이어·카메라)")]
        public static void BuildRunScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.91f, 0.87f, 0.78f);
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 100f;
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.AddComponent<UniversalAdditionalCameraData>();
            camGo.AddComponent<ScaleCamera>();

            new GameObject("DataBoot").AddComponent<DataBoot>();

            var player = new GameObject("플레이어");
            player.transform.position = new Vector3(0, 0, -1f);
            var sr = player.AddComponent<SpriteRenderer>();
            sr.color = new Color(0.70f, 0.15f, 0.12f);
            sr.sortingOrder = 20;
            player.AddComponent<PlayerController>();

            // 혈우 장막(출생지 = Unity 0,0)
            var curtain = new GameObject("혈우 장막");
            curtain.AddComponent<MeshFilter>();
            curtain.AddComponent<MeshRenderer>().sharedMaterial = ScaleBootstrap.EnsureCurtainMaterial();
            curtain.AddComponent<BloodCurtain>();

            // 소품 층과 수묵 지도. 청운진 한가운데가 Unity (0,0)
            var props = new GameObject("소품 층");
            var pl = props.AddComponent<PropLayer>();
            pl.material = ClusterBootstrap.EnsureMaterial();
            pl.originRegion = "cheongun";
            var ml = props.AddComponent<MapLayer>();
            ml.material = ClusterBootstrap.EnsureMaterial();
            ml.map = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Map/map_central_plains.jpg");

            var hud = HudBootstrap.CreateHud();
            hud.target = player.transform;

            EditorSceneManager.SaveScene(scene, RunScenePath);
            Debug.Log($"[RunBootstrap] {RunScenePath} 지음");
        }
    }
}
