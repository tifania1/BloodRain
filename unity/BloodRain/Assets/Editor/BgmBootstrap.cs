using System.Collections.Generic;
using System.IO;
using BloodRain.Presentation;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BloodRain.Editor
{
    // BGM-06U 배경음 재생기를 씬에 붙이고 곡 목록(Assets/Audio/BGM/bgm.json)을 채운다.
    // Run 씬은 LifeBootstrap.Attach, Inn 씬은 SoulBootstrap.BuildInnScene 이 부른다. 곡이 새로 들어오면 아래 메뉴만 다시.
    public static class BgmBootstrap
    {
        const string BgmDir = "Assets/Audio/BGM";
        const string RunScenePath = "Assets/Scenes/Run.unity";
        const string InnScenePath = "Assets/Scenes/Inn.unity";

        /// <summary>지금 열린 씬에 BgmPlayer 를 하나 두고 곡 목록을 채운다.</summary>
        public static BgmPlayer Attach()
        {
            var p = Object.FindFirstObjectByType<BgmPlayer>();
            if (p == null) p = new GameObject("배경음").AddComponent<BgmPlayer>();
            p.clips = LoadTracks();
            // 소리를 들을 귀. Inn 씬 카메라엔 AudioListener 가 없어 경고가 났다
            if (Object.FindFirstObjectByType<AudioListener>() == null && Camera.main != null)
                Camera.main.gameObject.AddComponent<AudioListener>();
            EditorUtility.SetDirty(p);
            return p;
        }

        /// <summary>bgm.json 의 tracks(id·file·loop)를 AudioClip 으로. 목록이나 파일이 없으면 빈 채로(재생기는 조용히 넘긴다).</summary>
        public static List<BgmPlayer.Track> LoadTracks()
        {
            var list = new List<BgmPlayer.Track>();
            var manifest = Path.Combine(BgmDir, "bgm.json");
            if (!File.Exists(manifest)) return list;
            var json = JObject.Parse(File.ReadAllText(manifest));
            foreach (var t in json["tracks"] ?? new JArray())
            {
                var id = (string)t["id"];
                var file = (string)t["file"];
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(file)) continue;
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{BgmDir}/{file}");
                if (clip == null) { Debug.LogWarning($"[BgmBootstrap] 곡 파일이 없음: {BgmDir}/{file}"); continue; }
                list.Add(new BgmPlayer.Track { id = id, clip = clip, loop = t["loop"] == null || (bool)t["loop"] });
            }
            return list;
        }

        [MenuItem("BloodRain/Bootstrap/배경음 재생기 붙이기(Run·Inn)")]
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
            Debug.Log($"[BgmBootstrap] Run·Inn 씬에 배경음 재생기 붙임 · 곡 {count}개");
        }
    }
}
