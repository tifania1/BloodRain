using System.Collections.Generic;
using System.IO;
using BloodRain.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BloodRain.Editor
{
    // B-12Ua: Run 씬 EnemyField 에 Assets/Art/Enemies 의 enemy_<id>·elite_<id>.png 를 물린다.
    //   덩이 렌더러는 그림 한 장마다 한 번씩 그린다(아틀라스로 굽지 않음 — 그리기 호출은 화면 안 종류 수만큼).
    //   다시 돌리면 같은 결과(원화를 더 뽑았으면 다시 돌린다).
    public static class EnemyArtBootstrap
    {
        const string Folder = "Assets/Art/Enemies";
        const string ScenePath = "Assets/Scenes/Run.unity";

        [MenuItem("BloodRain/Bootstrap/적 원화를 덩이에")]
        public static void Fill()
        {
            if (Application.isPlaying) { Debug.LogWarning("[EnemyArt] 플레이 중엔 안 됩니다"); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);
            var field = Object.FindFirstObjectByType<EnemyField>();
            if (field == null) { Debug.LogError("[EnemyArt] Run 씬에 EnemyField 가 없습니다"); return; }

            var list = new List<Texture2D>();
            var files = new List<string>();
            files.AddRange(Directory.GetFiles(Folder, "enemy_*.png", SearchOption.TopDirectoryOnly));
            files.AddRange(Directory.GetFiles(Folder, "elite_*.png", SearchOption.TopDirectoryOnly));
            files.Sort(System.StringComparer.Ordinal);
            foreach (var f in files)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(f.Replace('\\', '/'));
                if (tex != null) list.Add(tex);
            }
            Undo.RecordObject(field, "적 원화");
            field.enemyArt = list.ToArray();
            EditorUtility.SetDirty(field);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[EnemyArt] 적 원화 {list.Count}장");
        }
    }
}
