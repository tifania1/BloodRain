using BloodRain.Presentation.Fx;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BloodRain.Editor
{
    // PP-01U: 지금 연 씬(Run)에 '화면 마감'(PostFinish = Bloom·Vignette Volume) 하나를 붙이고,
    // 2D 렌더러에 PostProcessData 가 비어 있으면 URP 기본 것을 넣는다(비어 있으면 빌드에서 후처리가 안 돈다).
    // 다시 돌려도 하나만 남는다. Run 씬 짓기 메뉴도 이것을 부른다.
    public static class PostFinishBootstrap
    {
        const string RendererPath = "Assets/Settings/URP2D_Renderer.asset";
        const string DefaultPostData = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";

        [MenuItem("BloodRain/Bootstrap/화면 마감 붙이기(Bloom·Vignette)")]
        public static void Attach()
        {
            EnsurePostProcessData();
            var found = Object.FindObjectsByType<PostFinish>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 1; i < found.Length; i++) Object.DestroyImmediate(found[i].gameObject);
            if (found.Length == 0) new GameObject("화면 마감").AddComponent<PostFinish>();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[PP-01U] 화면 마감 붙임");
        }

        public static void EnsurePostProcessData()
        {
            var rd = AssetDatabase.LoadAssetAtPath<ScriptableObject>(RendererPath);
            if (rd == null) { Debug.LogWarning("[PP-01U] 2D 렌더러를 못 찾음: " + RendererPath); return; }
            var so = new SerializedObject(rd);
            var p = so.FindProperty("m_PostProcessData");
            if (p == null || p.objectReferenceValue != null) return;
            var data = AssetDatabase.LoadAssetAtPath<Object>(DefaultPostData);
            if (data == null) { Debug.LogWarning("[PP-01U] 기본 PostProcessData 를 못 찾음"); return; }
            p.objectReferenceValue = data;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rd);
            AssetDatabase.SaveAssets();
            Debug.Log("[PP-01U] 2D 렌더러에 PostProcessData 넣음");
        }
    }
}
