using BloodRain.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BloodRain.Editor
{
    // 혈마의 그림자(C-12)를 Run 씬에 붙인다. RunBootstrap 이 씬을 새로 지을 때도 부른다.
    public static class ShadowBootstrap
    {
        const string RunScenePath = "Assets/Scenes/Run.unity";

        [MenuItem("BloodRain/Bootstrap/Run 씬에 혈마의 그림자 붙이기")]
        public static void AddToRunScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != RunScenePath) scene = EditorSceneManager.OpenScene(RunScenePath);
            Attach();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ShadowBootstrap] Run 씬에 혈마의 그림자 붙임");
        }

        public static BloodShadowRunner Attach()
        {
            var r = Object.FindFirstObjectByType<BloodShadowRunner>();
            if (r == null) r = new GameObject("혈마의 그림자").AddComponent<BloodShadowRunner>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            r.player = player != null ? player.transform : null;
            r.enemies = Object.FindFirstObjectByType<EnemyField>();
            r.step = 0;
            EditorUtility.SetDirty(r);
            return r;
        }

        [MenuItem("BloodRain/Test/혈마의 그림자 단계 올리기(플레이 중)")]
        static void StepUp()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[ShadowBootstrap] 플레이 중에만"); return; }
            var r = Object.FindFirstObjectByType<BloodShadowRunner>();
            if (r == null) { Debug.LogWarning("[ShadowBootstrap] 씬에 그림자가 없다"); return; }
            r.step++;
            Debug.Log($"[ShadowBootstrap] 그림자 {r.step}단계");
        }
    }
}
