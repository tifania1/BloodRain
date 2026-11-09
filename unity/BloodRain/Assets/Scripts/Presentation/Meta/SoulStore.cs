using System;
using System.IO;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using UnityEngine;

namespace BloodRain.Presentation.Meta
{
    // LF-03 혼 저장 → SV-01Ua 칸 셋. persistentDataPath 에 칸마다 soul_1.json ~ soul_3.json(SoulSlots.FileName) 한 파일,
    //   안은 SaveGame({version, soul, dialogue}) — 혼과 객잔 대사 진행이 한 파일에 같이 든다.
    //   지금 칸은 Slot(기본 1). 칸 고르기 화면(SV-01Ub)이 바꾼다.
    //   Save(혼)·SaveDialogue(대사)는 따로 불려도 칸 파일의 다른 쪽을 디스크에서 다시 읽어 함께 쓴다(혼만 저장하다 대사를 옛 것으로 덮지 않게).
    //   한 번에 쓰려면 LoadGame/SaveAll.
    //   쓸 때는 .tmp 에 먼저 쓰고 바꿔 끼운다(쓰다 꺼져도 옛 저장이 남게).
    //   읽다 깨진 칸은 지우지 않고 .bad 로 옮겨 두고 새 혼으로 시작한다.
    //   옛 저장(soul.json·dialogue.json)은 처음 읽을 때 1번 칸으로 옮기고 원본은 .bak 로 남긴다(SoulSlots.ShouldMigrateLegacy).
    public static class SoulStore
    {
        public static string Dir => Application.persistentDataPath;

        static int _slot = SoulSlots.First;
        /// <summary>지금 쓰는 저장 칸(1~3).</summary>
        public static int Slot
        {
            get => _slot;
            set
            {
                if (!SoulSlots.IsValid(value)) throw new ArgumentOutOfRangeException(nameof(value), value, "없는 저장 칸");
                _slot = value;
            }
        }

        public static string SlotPath(int slot) => Path.Combine(Dir, SoulSlots.FileName(slot));
        /// <summary>지금 칸 파일.</summary>
        public static string SoulPath => SlotPath(Slot);
        static string LegacyPath(string file) => Path.Combine(Dir, file);

        public static bool HasSoul { get { MigrateLegacy(); return File.Exists(SoulPath); } }
        public static bool SlotExists(int slot) { MigrateLegacy(); return File.Exists(SlotPath(slot)); }

        /// <summary>지금 칸의 저장(혼 + 대사 진행). 없으면 새 저장.</summary>
        public static SaveGame LoadGame(GameData data) => LoadGame(data, Slot);

        public static SaveGame LoadGame(GameData data, int slot)
        {
            MigrateLegacy();
            var path = SlotPath(slot);
            if (!File.Exists(path)) return SaveGame.New(data);
            try
            {
                return SaveGame.FromJson(data, File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogError($"[SoulStore] {Path.GetFileName(path)} 을 못 읽어 새 혼으로 시작합니다(옛 파일은 .bad): {e.Message}");
                MoveAside(path);
                return SaveGame.New(data);
            }
        }

        public static void SaveAll(SaveGame game) => Write(SoulPath, game.ToJson());

        /// <summary>지금 칸의 혼. 없으면 새 혼.</summary>
        public static Soul Load(GameData data) => LoadGame(data).Soul;

        /// <summary>혼을 지금 칸에 쓴다. 대사 진행은 칸 파일에 있던 것 그대로.</summary>
        public static void Save(Soul soul) => SaveAll(new SaveGame(soul, PeekDialogue(DataBoot.Data)));

        /// <summary>지금 칸의 대사 진행. 없으면 처음부터.</summary>
        public static DialogueProgress LoadDialogue() => LoadGame(DataBoot.Data).Dialogue;

        /// <summary>대사 진행을 지금 칸에 쓴다. 혼은 칸 파일에 있던 것 그대로.</summary>
        public static void SaveDialogue(DialogueProgress progress) => SaveAll(new SaveGame(Load(DataBoot.Data), progress));

        /// <summary>칸 파일을 지운다(.bak 안 만듦 — 칸 지우기, SV-01Ub).</summary>
        public static void DeleteSlot(int slot)
        {
            var p = SlotPath(slot);
            if (File.Exists(p)) File.Delete(p);
        }

        /// <summary>시험 메뉴: 칸 셋·옛 두 파일·.bak 를 다 지운다.</summary>
        public static void Clear()
        {
            foreach (var n in SoulSlots.All) DeleteSlot(n);
            foreach (var f in SoulSlots.LegacyFiles)
            {
                foreach (var p in new[] { LegacyPath(f), LegacyPath(SoulSlots.BackupName(f)) })
                    if (File.Exists(p)) File.Delete(p);
            }
        }

        /// <summary>옛 soul.json(·dialogue.json)을 1번 칸으로 옮긴다. 옮길 것이 없거나 1번 칸이 이미 있으면 아무것도 안 함.</summary>
        public static void MigrateLegacy()
        {
            var soulPath = LegacyPath(SaveGame.LegacySoulFile);
            var slotPath = SlotPath(SoulSlots.LegacySlot);
            if (!SoulSlots.ShouldMigrateLegacy(File.Exists(soulPath), File.Exists(slotPath))) return;
            try
            {
                var dialoguePath = LegacyPath(SaveGame.LegacyDialogueFile);
                var game = SaveGame.FromLegacy(DataBoot.Data, File.ReadAllText(soulPath),
                    File.Exists(dialoguePath) ? File.ReadAllText(dialoguePath) : null);
                Write(slotPath, game.ToJson());
                foreach (var f in SoulSlots.LegacyFiles)
                {
                    var from = LegacyPath(f);
                    if (!File.Exists(from)) continue;
                    var to = LegacyPath(SoulSlots.BackupName(f));
                    if (File.Exists(to)) File.Delete(to);
                    File.Move(from, to);
                }
                Debug.Log($"[SoulStore] 옛 저장을 {SoulSlots.FileName(SoulSlots.LegacySlot)} 으로 옮김(원본은 .bak)");
            }
            catch (Exception e)
            {
                // 혼이 깨졌으면 옮기지 않고 그대로 둔다
                Debug.LogError($"[SoulStore] 옛 저장(soul.json)을 못 읽어 옮기지 않았습니다: {e.Message}");
            }
        }

        /// <summary>칸 파일의 대사 진행만 본다(깨졌어도 파일은 건드리지 않음).</summary>
        static DialogueProgress PeekDialogue(GameData data)
        {
            MigrateLegacy();
            var path = SoulPath;
            if (!File.Exists(path)) return new DialogueProgress();
            try { return SaveGame.FromJson(data, File.ReadAllText(path)).Dialogue; }
            catch (Exception) { return new DialogueProgress(); }
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
