using System.Collections.Generic;
using System.IO;
using BloodRain.Presentation;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BloodRain.Editor
{
    // SFX-14U 효과음 재생기를 씬에 붙이고 소리 목록(Assets/Audio/SFX/sfx.json)을 채운다.
    // Run 씬은 LifeBootstrap.Attach, Inn 씬은 SoulBootstrap.BuildInnScene 이 부른다. 소리가 새로 구워지면 아래 메뉴만 다시.
    public static class SfxBootstrap
    {
        const string SfxDir = "Assets/Audio/SFX";
        const string RunScenePath = "Assets/Scenes/Run.unity";
        const string InnScenePath = "Assets/Scenes/Inn.unity";

        /// <summary>지금 열린 씬에 SfxPlayer 를 하나 두고 소리 목록을 채운다.</summary>
        public static SfxPlayer Attach()
        {
            var p = Object.FindFirstObjectByType<SfxPlayer>();
            if (p == null) p = new GameObject("효과음").AddComponent<SfxPlayer>();
            p.clips = LoadClips();
            if (Object.FindFirstObjectByType<AudioListener>() == null && Camera.main != null)
                Camera.main.gameObject.AddComponent<AudioListener>();
            EditorUtility.SetDirty(p);
            return p;
        }

        /// <summary>sfx.json 의 sounds·variants 를 AudioClip 으로. 변형에 note 가 있으면 id 는 '<id>_n<note>'(SfxGate.AbsorbSoundId 꼴).</summary>
        public static List<SfxPlayer.Clip> LoadClips()
        {
            var list = new List<SfxPlayer.Clip>();
            var manifest = Path.Combine(SfxDir, "sfx.json");
            if (!File.Exists(manifest)) return list;
            var json = JObject.Parse(File.ReadAllText(manifest));
            var catGain = json["categoryGainDb"] as JObject;
            foreach (var s in json["sounds"] ?? new JArray())
            {
                var id = (string)s["id"];
                if (string.IsNullOrEmpty(id)) continue;
                var cat = (string)s["category"];
                float cg = catGain != null && cat != null && catGain[cat] != null ? (float)catGain[cat] : 0f;
                foreach (var v in s["variants"] ?? new JArray())
                {
                    var file = (string)v["file"];
                    if (string.IsNullOrEmpty(file)) continue;
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{SfxDir}/{file}");
                    if (clip == null) { Debug.LogWarning($"[SfxBootstrap] 소리 파일이 없음: {SfxDir}/{file}"); continue; }
                    var key = v["note"] != null ? $"{id}_n{(int)v["note"]}" : id;
                    float g = (v["gainDb"] != null ? (float)v["gainDb"] : 0f) + cg;
                    list.Add(new SfxPlayer.Clip { id = key, clip = clip, gainDb = g });
                }
            }
            return list;
        }

        [MenuItem("BloodRain/Bootstrap/효과음 재생기 붙이기(Run·Inn)")]
        public static void AttachToScenes()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            int count = 0;
            foreach (var path in new[] { RunScenePath, InnScenePath })
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                count = Attach().clips.Count;
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log($"[SfxBootstrap] Run·Inn 씬에 효과음 재생기 붙임 · 소리 {count}개");
        }

        /// <summary>시험: 플레이 중 같은 소리를 한 프레임에 30번 부른다(4번만 울려야 함).</summary>
        [MenuItem("BloodRain/Test/효과음 한 프레임 30번(플레이 중)")]
        public static void Burst30()
        {
            var p = SfxPlayer.Instance;
            if (p == null) { Debug.LogWarning("[SfxBootstrap] 플레이 중이 아니거나 재생기가 없음"); return; }
            int before = p.Dropped;
            for (int i = 0; i < 30; i++) p.Play("sfx_levelup");
            Debug.Log($"[SfxBootstrap] 30번 부름 → 버린 수 +{p.Dropped - before}");
        }
    }
}
