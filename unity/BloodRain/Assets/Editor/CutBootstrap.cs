using System.Linq;
using BloodRain.Presentation;
using BloodRain.Presentation.Meta;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BloodRain.Editor
{
    // C-10b 서장 끝 컷 화면을 Run 씬에 붙인다. 그림은 cheonha.json prologueBody.endCuts 이름 그대로 Assets/Art/Cutscenes 에서.
    public static class CutBootstrap
    {
        const string RunScenePath = "Assets/Scenes/Run.unity";
        const string CutFolder = "Assets/Art/Cutscenes";

        [MenuItem("BloodRain/Run 씬에 서장 끝 컷 화면 붙이기")]
        public static void Attach()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(RunScenePath, OpenSceneMode.Single);
            var screen = Object.FindFirstObjectByType<CutScreen>();
            if (screen == null) screen = new GameObject("서장 끝 컷").AddComponent<CutScreen>();
            screen.font = HudBootstrap.EnsureBodyFont();

            var data = DataBoot.Load();
            var ids = data.Cheonha.PrologueBody.EndCuts;
            screen.art = ids.Select(id => AssetDatabase.LoadAssetAtPath<Texture2D>($"{CutFolder}/{id}.jpg"))
                .Where(t => t != null).ToList();

            var runner = Object.FindFirstObjectByType<LifeRunner>();
            if (runner != null) { runner.cuts = screen; EditorUtility.SetDirty(runner); }
            EditorUtility.SetDirty(screen);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[CutBootstrap] 서장 끝 컷 {screen.art.Count}/{ids.Count}장, LifeRunner 이음 {runner != null}");
        }
    }
}
