using System.Linq;
using BloodRain.Presentation;
using BloodRain.Presentation.Fx;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BloodRain.Editor
{
    // 먹 튐·바닥 먹 자국(V-04)을 Run 씬에 붙인다. RunBootstrap 이 씬을 새로 지을 때도 부른다.
    public static class InkMarksBootstrap
    {
        const string RunScenePath = "Assets/Scenes/Run.unity";
        const string Common = "Assets/Art/VFX/Common/";

        [MenuItem("BloodRain/Bootstrap/Run 씬에 먹 자국 붙이기")]
        public static void AddToRunScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != RunScenePath) scene = EditorSceneManager.OpenScene(RunScenePath);
            Attach();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[InkMarksBootstrap] Run 씬에 먹 자국 붙임");
        }

        /// <summary>지금 열린 씬에 먹 자국 층이 없으면 만든다.</summary>
        public static InkMarks Attach()
        {
            var ink = Object.FindFirstObjectByType<InkMarks>();
            if (ink == null) ink = new GameObject("먹 자국").AddComponent<InkMarks>();
            ink.material = ClusterBootstrap.EnsureMaterial();
            ink.enemies = Object.FindFirstObjectByType<EnemyField>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            ink.player = player != null ? player.transform : null;
            ink.splats = Enumerable.Range(1, 6).Select(i => AssetDatabase.LoadAssetAtPath<Texture2D>($"{Common}ink_splat_{i}.png")).Where(t => t != null).ToArray();
            ink.groundInks = Enumerable.Range(1, 4).Select(i => AssetDatabase.LoadAssetAtPath<Texture2D>($"{Common}ground_ink_{i}.png")).Where(t => t != null).ToArray();
            EditorUtility.SetDirty(ink);
            return ink;
        }
    }
}
