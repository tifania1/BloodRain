using System.Collections.Generic;
using System.IO;
using BloodRain.Presentation;
using BloodRain.Presentation.Fx;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BloodRain.Editor
{
    // PR-01U: Run 씬 LifeRunner 오브젝트에 PropArt 를 붙이고 Assets/Art/Props/Parts/*.png 를 이름 → Sprite 목록으로 물린다.
    //   LanternLayer·ChestLayer 는 플레이 때 LifeRunner 가 붙이니 그림 목록만 씬에 둔다. 다시 돌리면 같은 결과.
    public static class PropArtBootstrap
    {
        const string Folder = "Assets/Art/Props/Parts";
        const string ScenePath = "Assets/Scenes/Run.unity";

        [MenuItem("BloodRain/Bootstrap/소품 그림 목록 채우기")]
        public static void Fill()
        {
            if (Application.isPlaying) { Debug.LogWarning("[PropArt] 플레이 중엔 안 됩니다"); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);
            var runner = Object.FindFirstObjectByType<LifeRunner>();
            if (runner == null) { Debug.LogError("[PropArt] Run 씬에 LifeRunner 가 없습니다"); return; }

            var names = new List<string>();
            var sprites = new List<Sprite>();
            var files = Directory.GetFiles(Folder, "prop_*.png", SearchOption.TopDirectoryOnly);
            System.Array.Sort(files, System.StringComparer.Ordinal);
            foreach (var f in files)
            {
                string path = f.Replace('\\', '/');
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sp == null) { Debug.LogWarning($"[PropArt] Sprite 아님: {path}"); continue; }
                names.Add(Path.GetFileNameWithoutExtension(path));
                sprites.Add(sp);
            }

            var art = runner.GetComponent<PropArt>();
            if (art == null) art = Undo.AddComponent<PropArt>(runner.gameObject);
            Undo.RecordObject(art, "소품 그림 목록");
            art.names = names.ToArray();
            art.sprites = sprites.ToArray();
            EditorUtility.SetDirty(art);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[PropArt] 소품 그림 {names.Count}장: {string.Join(", ", names)}");
        }
    }
}
