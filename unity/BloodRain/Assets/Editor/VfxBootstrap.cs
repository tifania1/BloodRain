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
        const string AiDir = "Assets/Art/VFX/AI";
        const string MaterialPath = "Assets/Art/VFX/BrushStroke.mat";
        const string AddMaterialPath = "Assets/Art/VFX/BrushStrokeAdd.mat";
        const string RunScenePath = "Assets/Scenes/Run.unity";

        [MenuItem("BloodRain/Bootstrap/붓 획 이펙트(텍스처 설정·머티리얼·삼재검법 1단)")]
        public static void Run()
        {
            int n = ImportSettings(BrushDir) + ImportSettings(CommonDir) + ImportSettings(AiDir);
            var mat = EnsureMaterial();
            EnsureAddMaterial();

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
                // 뇌부 낙뢰(W-01): AI 빛 재료 fx_lightning_glow(먹 받침 + 금빛 + 흰빛, 위에서 아래로)
                wr.brushAddMaterial = EnsureAddMaterial();
                wr.lightningShade = Load($"{AiDir}/fx_lightning_glow_shade.png");
                wr.lightningGold = Load($"{AiDir}/fx_lightning_glow_gold.png");
                wr.lightningCore = Load($"{AiDir}/fx_lightning_glow_core.png");
                wr.lightningMeters = SizeOf(AiDir, "fx_lightning_glow", 4f);
                wr.aiFx = AiList();
                EditorUtility.SetDirty(wr);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log($"[VfxBootstrap] 텍스처 {n}장 설정, 머티리얼 {MaterialPath}, 무공 {(wr != null ? "연결" : "없음")}");
        }

        static Texture2D Load(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        /// <summary>ai.json 의 재료 전부를 WeaponRunner 가 이름으로 찾게 넘긴다(층 텍스처·sizeMeters·progress).</summary>
        static List<WeaponRunner.AiFx> AiList()
        {
            var list = new List<WeaponRunner.AiFx>();
            var path = $"{AiDir}/ai.json";
            if (!File.Exists(path)) return list;
            var root = JObject.Parse(File.ReadAllText(path));
            if (!(root["textures"] is JArray arr)) return list;
            foreach (var t in arr)
            {
                var name = (string)t["name"];
                if (string.IsNullOrEmpty(name)) continue;
                list.Add(new WeaponRunner.AiFx
                {
                    name = name,
                    meters = t["sizeMeters"] != null ? (float)t["sizeMeters"] : 2f,
                    kind = KindOf((string)t["progress"]),
                    stroke = Load($"{AiDir}/{name}_stroke.png"),
                    shade = Load($"{AiDir}/{name}_shade.png"),
                    gold = Load($"{AiDir}/{name}_gold.png"),
                    core = Load($"{AiDir}/{name}_core.png"),
                });
            }
            return list;
        }

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
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        /// <summary>AI 빛 재료(ai_glow 의 gold·core 층)용: 같은 셰이더에 더하기 섞기(Blend SrcAlpha One).</summary>
        public static Material EnsureAddMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(AddMaterialPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("BloodRain/BrushStroke")) { name = "BrushStrokeAdd" };
                AssetDatabase.CreateAsset(mat, AddMaterialPath);
            }
            mat.shader = Shader.Find("BloodRain/BrushStroke");
            mat.SetTexture("_NoiseTex", Load($"{CommonDir}/noise_1.png"));
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        /// <summary>ai.json progress → 셰이더 _Kind(radial 2·down 3·up 4·right 1·left 5).</summary>
        public static float KindOf(string progress)
        {
            switch (progress)
            {
                case "radial": return 2f;
                case "down": return 3f;
                case "up": return 4f;
                case "left": return 5f;
                default: return 1f; // right
            }
        }

        /// <summary>폴더 json 들의 textures[].name → sizeMeters.</summary>
        static Dictionary<string, float> Sizes(string dir)
        {
            var map = new Dictionary<string, float>();
            foreach (var json in Directory.GetFiles(dir, "*.json"))
            {
                var root = JObject.Parse(File.ReadAllText(json));
                if (root["textures"] is JArray arr)
                    foreach (var t in arr)
                    {
                        var name = (string)t["name"];
                        var size = t["sizeMeters"];
                        if (name != null && size != null) map[name] = (float)size;
                    }
                // Common/common.json 은 sizeMeters 가 {앞머리: m} 꼴이다(ink_splat_1 → ink_splat).
                if (root["sizeMeters"] is JObject prefixes)
                    foreach (var p in prefixes.Properties()) map[p.Name] = (float)p.Value;
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
                // 가장 긴 이름이 이긴다: fx_mist_ink_2_stroke 는 fx_mist_ink(3m)가 아니라 fx_mist_ink_2(3.5m).
                int best = -1;
                foreach (var kv in sizes)
                    if ((stem == kv.Key || stem.StartsWith(kv.Key + "_")) && kv.Key.Length > best) { size = kv.Value; best = kv.Key.Length; }
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
