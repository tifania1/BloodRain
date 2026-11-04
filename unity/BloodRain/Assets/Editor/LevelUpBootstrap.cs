using BloodRain.Presentation;
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

            // 비전·기연 발견 연출과 판정(S-04)
            var fx = Object.FindFirstObjectByType<DiscoveryFx>();
            if (fx == null) fx = new GameObject("발견 연출").AddComponent<DiscoveryFx>();
            fx.font = screen.font;
            EditorUtility.SetDirty(fx);
            var watch = Object.FindFirstObjectByType<SecretWatch>();
            if (watch == null) watch = new GameObject("비전 판정").AddComponent<SecretWatch>();
            watch.levelUp = screen;
            watch.fx = fx;
            EditorUtility.SetDirty(watch);
            return screen;
        }

        /// <summary>플레이 중에 발견 연출을 바로 띄워 본다(비전 하나, 기연 하나).</summary>
        [MenuItem("BloodRain/Test/발견 연출 보기(플레이 중)")]
        public static void TestDiscovery()
        {
            var fx = Object.FindFirstObjectByType<DiscoveryFx>();
            if (!Application.isPlaying || fx == null) { Debug.LogWarning("[LevelUpBootstrap] 플레이 중 Run 씬에서 부른다"); return; }
            fx.Show(DiscoveryFx.Kind.Secret, Loc.T("synergies.secrets.mumyeong.name", "무명검"), "無名劍"); // 실제 SecretWatch 처럼 지금 언어로
            fx.Show(DiscoveryFx.Kind.Encounter, Loc.T("synergies.encounters.hermit.name", "은거기인"), "");
        }
    }
}
