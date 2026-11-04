using System;
using System.IO;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using UnityEngine;

namespace BloodRain.Presentation.Meta
{
    // LF-03 혼 저장. persistentDataPath 에 soul.json(Soul.ToJson)과 dialogue.json(DialogueProgress.ToJson).
    //   쓸 때는 .tmp 에 먼저 쓰고 바꿔 끼운다(쓰다 꺼져도 옛 저장이 남게).
    //   읽다 깨진 저장은 지우지 않고 .bad 로 옮겨 두고 새 혼으로 시작한다.
    public static class SoulStore
    {
        public static string Dir => Application.persistentDataPath;
        public static string SoulPath => Path.Combine(Dir, "soul.json");
        public static string DialoguePath => Path.Combine(Dir, "dialogue.json");

        public static bool HasSoul => File.Exists(SoulPath);

        /// <summary>저장된 혼. 없으면 새 혼.</summary>
        public static Soul Load(GameData data)
        {
            if (!File.Exists(SoulPath)) return new Soul(data);
            try
            {
                return Soul.FromJson(data, File.ReadAllText(SoulPath));
            }
            catch (Exception e)
            {
                Debug.LogError($"[SoulStore] 혼 저장을 못 읽어 새 혼으로 시작합니다(옛 파일은 soul.json.bad): {e.Message}");
                MoveAside(SoulPath);
                return new Soul(data);
            }
        }

        public static void Save(Soul soul) => Write(SoulPath, soul.ToJson());

        public static DialogueProgress LoadDialogue()
        {
            if (!File.Exists(DialoguePath)) return new DialogueProgress();
            try
            {
                return DialogueProgress.FromJson(File.ReadAllText(DialoguePath));
            }
            catch (Exception e)
            {
                Debug.LogError($"[SoulStore] 대사 진행 저장을 못 읽어 처음부터(옛 파일은 dialogue.json.bad): {e.Message}");
                MoveAside(DialoguePath);
                return new DialogueProgress();
            }
        }

        public static void SaveDialogue(DialogueProgress progress) => Write(DialoguePath, progress.ToJson());

        /// <summary>혼·대사 저장을 지운다(시험 메뉴).</summary>
        public static void Clear()
        {
            foreach (var p in new[] { SoulPath, DialoguePath })
                if (File.Exists(p)) File.Delete(p);
        }

        static void Write(string path, string json)
        {
            Directory.CreateDirectory(Dir);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }

        static void MoveAside(string path)
        {
            try
            {
                var bad = path + ".bad";
                if (File.Exists(bad)) File.Delete(bad);
                File.Move(path, bad);
            }
            catch (Exception) { }
        }
    }
}
