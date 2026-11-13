using System.IO;
using UnityEditor;
using UnityEngine;

namespace BloodRain.Editor
{
    // 무공 이펙트 통합 셰이더(FxInk)의 재질을 Resources/Fx 에 만든다(VF-01U).
    // SectFx 가 Resources.Load 로 읽으니 빌드에서도 셰이더가 빠지지 않는다. 다시 돌려도 같은 파일을 고친다.
    public static class FxInkBootstrap
    {
        const string Dir = "Assets/Resources/Fx";
        const string MatPath = Dir + "/FxInk.mat";

        [MenuItem("BloodRain/Bootstrap/이펙트 재질 짓기(FxInk)")]
        public static void Build()
        {
            var shader = Shader.Find("BloodRain/FxInk");
            if (shader == null) { Debug.LogError("BloodRain/FxInk 셰이더가 없다(Assets/Art/Shaders/FxInk.shader 컴파일 확인)."); return; }
            if (!Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); AssetDatabase.Refresh(); }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat == null) { mat = new Material(shader) { name = "FxInk" }; AssetDatabase.CreateAsset(mat, MatPath); }
            else mat.shader = shader;
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            Debug.Log("FxInk 재질: " + MatPath);
        }
    }
}
