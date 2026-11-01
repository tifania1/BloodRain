using System.Collections.Generic;
using System.IO;
using BloodRain.Presentation.Combat;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BloodRain.Editor
{
    // 붓 획 이펙트 틀(V-04). tools/bake_brush.py 가 구운 텍스처의 가져오기 설정과 붓 획 머티리얼을 만들고,
    // Run 씬 무공(WeaponRunner)에 삼재검법 1단 획을 물린다. 다시 돌려도 같은 결과.
    //   텍스처: Sprite, Pixels Per Unit = 512 / sizeMeters(같은 폴더 json), Clamp, 알파 = 투명.
    public static class VfxBootstrap
    {
        const string BrushDir = "Assets/Art/VFX/Brush";
        const string CommonDir = "Assets/Art/VFX/Common";
        const string MaterialPath = "Assets/Art/VFX/BrushStroke.mat";
        const string RunScenePath = "Assets/Scenes/Run.unity";

        [MenuItem("BloodRain/Bootstrap/붓 획 이펙트(텍스처 설정·머티리얼·삼재검법 1단)")]
        public static void Run()
        {
            int n = ImportSettings(BrushDir) + ImportSettings(CommonDir);
            var mat = EnsureMaterial();

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != RunScenePath) scene = EditorSceneManager.OpenScene(RunScenePath);
            var wr = Object.FindFirstObjectByType<WeaponRunner>();
            if (wr != null)
            {
                wr.brushMaterial = mat;
                wr.samjaeTier1 = new[]
                {
                    Load($"{BrushDir}/samjae_t1_1_stroke.png"), Load($"{BrushDir}/samjae_t1_2_stroke.png"), Load($"{BrushDir}/samjae_t1_3_stroke.png"),
                };
                wr.samjaeTier1Meters = SizeOf(BrushDir, "samjae_t1_1", 5.74f);
                wr.samjaeTier1Span = 2.3f;
                EditorUtility.SetDirty(wr);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log($"[VfxBootstrap] 텍스처 {n}장 설정, 머티리얼 {MaterialPath}, 무공 {(wr != null ? "연결" : "없음")}");
        }

        static Texture2D Load(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        static Material EnsureMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("BloodRain/BrushStroke")) { name = "BrushStroke" };
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }
            mat.shader = Shader.Find("BloodRain/BrushStroke");
            mat.SetTexture("_NoiseTex", Load($"{CommonDir}/noise_1.png"));
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        /// <summary>폴더 json 들의 textures[].name → sizeMeters.</summary>
        static Dictionary<string, float> Sizes(string dir)
        {
            var map = new Dictionary<string, float>();
            foreach (var json in Directory.GetFiles(dir, "*.json"))
            {
                var root = JObject.Parse(File.ReadAllText(json));
                if (root["textures"] is not JArray arr) continue;
                foreach (var t in arr)
                {
                    var name = (string)t["name"];
                    var size = t["sizeMeters"];
                    if (name != null && size != null) map[name] = (float)size;
                }
            }
            return map;
        }

        static float SizeOf(string dir, string name, float fallback) => Sizes(dir).TryGetValue(name, out var s) ? s : fallback;

        static int ImportSettings(string dir)
        {
            if (!Directory.Exists(dir)) return 0;
            var sizes = Sizes(dir);
            int n = 0;
            foreach (var png in Directory.GetFiles(dir, "*.png"))
            {
                var path = png.Replace('\\', '/');
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) continue;
                // 이름 = <텍스처 이름>_<층>.png (층: stroke·wash·gold). 공통 텍스처는 json 이름 그대로일 수도 있다.
                string stem = Path.GetFileNameWithoutExtension(path);
                float size = 0f;
                foreach (var kv in sizes)
                    if (stem == kv.Key || stem.StartsWith(kv.Key + "_")) { size = kv.Value; break; }
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                if (size > 0f) imp.spritePixelsPerUnit = 512f / size;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.SaveAndReimport();
                n++;
            }
            return n;
        }
    }
}
