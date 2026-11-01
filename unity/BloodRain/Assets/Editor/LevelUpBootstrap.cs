using BloodRain.Presentation.Combat;
using BloodRain.Presentation.Fx;
using BloodRain.Presentation.Hud;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BloodRain.Editor
{
    // 레벨업 화면(C-06)을 Run 씬에 붙인다. RunBootstrap 이 씬을 새로 지을 때도 부른다.
    public static class LevelUpBootstrap
    {
        const string RunScenePath = "Assets/Scenes/Run.unity";

        [MenuItem("BloodRain/Bootstrap/Run 씬에 레벨업 화면 붙이기")]
        public static void AddToRunScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != RunScenePath) scene = EditorSceneManager.OpenScene(RunScenePath);
            Attach();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[LevelUpBootstrap] Run 씬에 레벨업 화면 붙임");
        }

        /// <summary>지금 열린 씬에 레벨업 화면이 없으면 만든다.</summary>
        public static LevelUpScreen Attach()
        {
            var screen = Object.FindFirstObjectByType<LevelUpScreen>();
            if (screen == null) screen = new GameObject("레벨업 화면").AddComponent<LevelUpScreen>();
            screen.font = HudBootstrap.EnsureBodyFont();
            screen.orbs = Object.FindFirstObjectByType<OrbLayer>();
            screen.weapons = Object.FindFirstObjectByType<WeaponRunner>();
            EditorUtility.SetDirty(screen);
            return screen;
        }
    }
}
