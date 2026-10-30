using System.IO;
using System.Linq;
using System.Text;
using BloodRain.Presentation;
using BloodRain.Presentation.Fx;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;

namespace BloodRain.Editor
{
    // 절기 각성 연출(J-01U)에 필요한 것들을 코드로 만든다. 다시 돌려도 있는 것은 그대로 두고 빠진 것만 채운다.
    public static class AwakeningBootstrap
    {
        const string SettingsDir = "Assets/Settings";
        const string RendererPath = SettingsDir + "/URP2D_Renderer.asset";
        const string PipelinePath = SettingsDir + "/URP2D.asset";
        const string OverlayRendererPath = SettingsDir + "/URP2D_Overlay_Renderer.asset"; // 흑백 효과 없는 겹 카메라용(렌더러 1번)
        const string HoleShaderPath = "Assets/Art/Shaders/AwakenHole.shader";
        const string HoleMaterialPath = "Assets/Art/Shaders/AwakenHole.mat";
        const string FontPath = "Assets/Art/Fonts/YujiBoku-Regular.ttf";
        const string FontAssetPath = "Assets/Art/Fonts/YujiBoku SDF.asset";
        const string TestScenePath = "Assets/Scenes/Test/AwakenTest.unity";

        /// <summary>URP 2D 파이프라인 에셋이 없으면 만들어 Graphics 설정에 건다. 각성 흑백 구멍 Full Screen Pass 도 붙인다.</summary>
        [MenuItem("BloodRain/Bootstrap/URP 2D 파이프라인과 각성 화면 효과")]
        public static void SetupPipeline()
        {
            Directory.CreateDirectory(SettingsDir);
            var data = AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<Renderer2DData>();
                AssetDatabase.CreateAsset(data, RendererPath);
                Debug.Log($"[Bootstrap] {RendererPath} 만듦");
            }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(data);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
                Debug.Log($"[Bootstrap] {PipelinePath} 만듦");
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = null; // 품질 단계마다 따로 걸지 않고 기본 하나를 쓴다

            var mat = AssetDatabase.LoadAssetAtPath<Material>(HoleMaterialPath);
            if (mat == null)
            {
                mat = new Material(AssetDatabase.LoadAssetAtPath<Shader>(HoleShaderPath));
                AssetDatabase.CreateAsset(mat, HoleMaterialPath);
            }

            if (!data.rendererFeatures.OfType<FullScreenPassRendererFeature>().Any(f => f.passMaterial == mat))
            {
                var feat = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feat.name = "AwakenHole";
                feat.passMaterial = mat;
                feat.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing; // 2D 렌더러에서 After 는 화면이 한 색으로 나왔다
                feat.fetchColorBuffer = true;
                AssetDatabase.AddObjectToAsset(feat, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feat, out _, out long localId);

                var so = new SerializedObject(data);
                var list = so.FindProperty("m_RendererFeatures");
                var map = so.FindProperty("m_RendererFeatureMap");
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feat;
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[Bootstrap] 각성 흑백 구멍 Full Screen Pass 붙임");
            }
            // 겹 카메라 렌더러(1번). 화면 효과를 붙이지 않는다.
            var overlay = AssetDatabase.LoadAssetAtPath<Renderer2DData>(OverlayRendererPath);
            if (overlay == null)
            {
                overlay = ScriptableObject.CreateInstance<Renderer2DData>();
                AssetDatabase.CreateAsset(overlay, OverlayRendererPath);
            }
            var pso = new SerializedObject(pipeline);
            var rl = pso.FindProperty("m_RendererDataList");
            bool has = false;
            for (int i = 0; i < rl.arraySize; i++) has |= rl.GetArrayElementAtIndex(i).objectReferenceValue == overlay;
            if (!has)
            {
                rl.arraySize++;
                rl.GetArrayElementAtIndex(rl.arraySize - 1).objectReferenceValue = overlay;
                pso.ApplyModifiedPropertiesWithoutUndo();
            }
            EnsureLayer(AwakeningFx.OverlayLayerName);

            EditorUtility.SetDirty(data);
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
        }

        static void EnsureLayer(string name)
        {
            if (LayerMask.NameToLayer(name) >= 0) return;
            var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tm.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                var l = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(l.stringValue)) continue;
                l.stringValue = name;
                tm.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"[Bootstrap] 레이어 {i} {name}");
                return;
            }
            Debug.LogError("[Bootstrap] 빈 레이어가 없다");
        }

        /// <summary>Yuji Boku 로 절기 이름 SDF 글꼴을 만든다. 절기 16개 한자와 絶·秘傳만 담은 고정 아틀라스(용량 때문에).</summary>
        [MenuItem("BloodRain/Bootstrap/절기 이름 글꼴(Yuji Boku SDF)")]
        public static TMP_FontAsset BuildNameFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (font == null) { Debug.LogError($"[Bootstrap] {FontPath} 이 없다"); return null; }

            var chars = new StringBuilder("絶秘傳");
            foreach (var e in DataBoot.Load().Evolutions) chars.Append(e.Hanja);
            var set = new string(chars.ToString().Distinct().ToArray());

            if (File.Exists(FontAssetPath)) AssetDatabase.DeleteAsset(FontAssetPath);
            var fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            fa.name = "YujiBoku SDF";
            AssetDatabase.CreateAsset(fa, FontAssetPath);
            fa.atlasTextures[0].name = "YujiBoku SDF Atlas";
            fa.material.name = "YujiBoku SDF Material";
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            AssetDatabase.AddObjectToAsset(fa.material, fa);

            fa.TryAddCharacters(set, out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.LogWarning($"[Bootstrap] Yuji Boku 에 없는 글자: {missing}");
            fa.atlasPopulationMode = AtlasPopulationMode.Static;
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Bootstrap] {FontAssetPath} 만듦, 글자 {set.Length}개");
            return fa;
        }

        /// <summary>각성 연출 시험 씬. 플레이하면 첫 절기를 길게 보여 주고 Space·S·N 으로 다시 본다.</summary>
        [MenuItem("BloodRain/Test/각성 연출 시험 씬 만들기")]
        public static void CreateTestScene()
        {
            SetupPipeline();
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath) == null) BuildNameFont();

            Directory.CreateDirectory(Path.GetDirectoryName(TestScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.2f, 0.22f, 0.18f);
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            var baseData = camGo.AddComponent<UniversalAdditionalCameraData>();
            int overlayLayer = LayerMask.NameToLayer(AwakeningFx.OverlayLayerName);
            cam.cullingMask = ~(1 << overlayLayer);

            var ovGo = new GameObject("Overlay Camera (각성 글자)");
            ovGo.transform.SetParent(camGo.transform, false);
            var ov = ovGo.AddComponent<Camera>();
            ov.orthographic = true;
            ov.orthographicSize = cam.orthographicSize;
            ov.cullingMask = 1 << overlayLayer;
            var ovData = ovGo.AddComponent<UniversalAdditionalCameraData>();
            ovData.renderType = CameraRenderType.Overlay;
            ovData.SetRenderer(1);
            baseData.cameraStack.Add(ov);

            var lightGo = new GameObject("Global Light 2D");
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;

            var fxGo = new GameObject("AwakeningFx");
            var fx = fxGo.AddComponent<AwakeningFx>();
            // 새 씬을 열면 안 쓰는 에셋이 내려가므로 글꼴은 씬을 만든 뒤에 읽는다.
            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            var fso = new SerializedObject(fx);
            fso.FindProperty("cam").objectReferenceValue = cam;
            fso.FindProperty("overlayCam").objectReferenceValue = ov;
            fso.FindProperty("nameFont").objectReferenceValue = fa;
            fso.ApplyModifiedPropertiesWithoutUndo();

            var stage = new GameObject("시험 무대");
            var driver = stage.AddComponent<AwakeningTestDriver>();
            var dso = new SerializedObject(driver);
            dso.FindProperty("fx").objectReferenceValue = fx;
            dso.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, TestScenePath);
            Debug.Log($"[Bootstrap] {TestScenePath} 만듦");
        }
    }
}
