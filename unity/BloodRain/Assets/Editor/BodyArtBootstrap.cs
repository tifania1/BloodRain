using System.Collections.Generic;
using System.IO;
using BloodRain.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BloodRain.Editor
{
    // BD-01U: Run 씬 플레이어에 PlayerLook 을 붙이고 Assets/Art/Characters/char_*.png 를 이름 → Sprite 목록으로 물린다.
    //   발밑·사람 높이는 png 알파(128 넘는 줄)로 잰다. 흰 번쩍임 재질 Art/Shaders/SpriteFlash.mat 도 없으면 만든다.
    //   다시 돌리면 같은 결과(그림을 더 뽑았으면 다시 돌린다).
    public static class BodyArtBootstrap
    {
        const string Folder = "Assets/Art/Characters";
        const string MatPath = "Assets/Art/Shaders/SpriteFlash.mat";
        const string ScenePath = "Assets/Scenes/Run.unity";

        [MenuItem("BloodRain/Bootstrap/몸 그림 목록 채우기")]
        public static void Fill()
        {
            if (Application.isPlaying) { Debug.LogWarning("[BodyArt] 플레이 중엔 안 됩니다"); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);
            var pc = Object.FindFirstObjectByType<PlayerController>();
            if (pc == null) { Debug.LogError("[BodyArt] Run 씬에 PlayerController 가 없습니다"); return; }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat == null)
            {
                var shader = Shader.Find("BloodRain/SpriteFlash");
                if (shader == null) { Debug.LogError("[BodyArt] BloodRain/SpriteFlash 셰이더가 없습니다"); return; }
                mat = new Material(shader) { name = "SpriteFlash" };
                AssetDatabase.CreateAsset(mat, MatPath);
            }

            var names = new List<string>();
            var sprites = new List<Sprite>();
            var foot = new List<float>();
            var body = new List<float>();
            var files = Directory.GetFiles(Folder, "char_*.png", SearchOption.TopDirectoryOnly);
            System.Array.Sort(files, System.StringComparer.Ordinal);
            foreach (var f in files)
            {
                string path = f.Replace('\\', '/');
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sp == null) { Debug.LogWarning($"[BodyArt] Sprite 아님: {path}"); continue; }
                var (lo, hi) = AlphaRows(path);
                names.Add(Path.GetFileNameWithoutExtension(path));
                sprites.Add(sp);
                foot.Add(lo);
                body.Add(Mathf.Max(0.05f, hi - lo));
            }

            var look = pc.GetComponent<PlayerLook>();
            if (look == null) look = Undo.AddComponent<PlayerLook>(pc.gameObject);
            Undo.RecordObject(look, "몸 그림 목록");
            look.names = names.ToArray();
            look.sprites = sprites.ToArray();
            look.footV = foot.ToArray();
            look.bodyV = body.ToArray();
            look.flashMaterial = mat;
            EditorUtility.SetDirty(look);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[BodyArt] 몸 그림 {names.Count}장: {string.Join(", ", names)}");
        }

        /// <summary>알파가 반 넘는 가장 아래·위 줄(uv, 0 = 아래).</summary>
        static (float lo, float hi) AlphaRows(string path)
        {
            var t = new Texture2D(2, 2);
            ImageConversion.LoadImage(t, File.ReadAllBytes(path));
            var px = t.GetPixels32();
            int w = t.width, h = t.height, lo = 0, hi = h - 1;
            bool Row(int y) { for (int x = 0; x < w; x++) if (px[y * w + x].a > 128) return true; return false; }
            while (lo < h - 1 && !Row(lo)) lo++;
            while (hi > lo && !Row(hi)) hi--;
            Object.DestroyImmediate(t);
            return (lo / (float)h, (hi + 1) / (float)h);
        }
    }
}
