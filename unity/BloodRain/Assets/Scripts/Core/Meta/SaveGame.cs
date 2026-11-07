#nullable enable
// 메타 저장 한 파일(기획서 15장 '저장': 메타 진행은 JSON 한 파일로 저장하고 Steam 클라우드에 올린다).
//   혼(Soul.ToJson)과 객잔 대사 진행(DialogueProgress.ToJson)을 save.json 하나에 담는다:
//     { "version": 1, "soul": { …혼 저장… }, "dialogue": { …대사 진행… } }
//   안쪽 두 덩이는 제 형식(제 version)을 그대로 쓴다. 바깥 version 은 묶는 꼴이 바뀔 때만 올린다.
//   설정(GameSettings)·조작(Controls)은 기기마다 다른 값이라 여기 넣지 않는다(PlayerPrefs).
//   옛 저장(soul.json·dialogue.json 두 파일, LF-03)은 FromLegacy 로 한 번 읽어 옮긴다.
//   쓰는 쪽(Unity SoulStore)이 .tmp 에 먼저 쓰고 바꿔 끼우는 것은 그대로다.
using System;
using BloodRain.Core.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BloodRain.Core.Meta
{
    public sealed class SaveGame
    {
        public const int SaveVersion = 1;
        /// <summary>저장 파일 이름. Steam 클라우드(Steam Auto-Cloud)에 올릴 파일도 이것 하나.</summary>
        public const string FileName = "save.json";
        /// <summary>옮기기 전 옛 파일 이름(LF-03).</summary>
        public const string LegacySoulFile = "soul.json", LegacyDialogueFile = "dialogue.json";

        public Soul Soul { get; }
        public DialogueProgress Dialogue { get; }

        public SaveGame(Soul soul, DialogueProgress dialogue)
        {
            Soul = soul ?? throw new ArgumentNullException(nameof(soul));
            Dialogue = dialogue ?? throw new ArgumentNullException(nameof(dialogue));
        }

        /// <summary>처음 시작하는 저장(새 혼, 대사 처음부터).</summary>
        public static SaveGame New(GameData data) => new SaveGame(new Soul(data), new DialogueProgress());

        /// <summary>저장 문자열. 같은 혼·같은 대사 진행이면 같은 문자열(클라우드가 바뀐 것만 올리게).</summary>
        public string ToJson()
        {
            var o = new JObject
            {
                ["version"] = SaveVersion,
                ["soul"] = JObject.Parse(Soul.ToJson()),
                ["dialogue"] = JObject.Parse(Dialogue.ToJson()),
            };
            return o.ToString(Formatting.Indented);
        }

        /// <summary>저장 문자열을 읽는다. 혼이 없으면 FormatException, 더 새 형식이면 NotSupportedException.
        /// 대사 진행이 빠졌거나 깨졌으면 처음부터(혼은 살린다 — 대사 한 덩이 때문에 부른 쪽이 파일 전체를 .bad 로 옮기지 않게, FromLegacy 와 같음).</summary>
        public static SaveGame FromJson(GameData data, string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new FormatException("저장 문자열이 비었습니다");
            var o = JObject.Parse(json);
            var version = o.Value<int?>("version") ?? 1;
            if (version > SaveVersion) throw new NotSupportedException($"이 판보다 새 저장 형식입니다: {version}");
            if (!(o["soul"] is JObject soul)) throw new FormatException("저장에 혼이 없습니다");
            var soulJson = soul.ToString(Formatting.None);
            var parsedSoul = Soul.FromJson(data, soulJson);
            return new SaveGame(parsedSoul, ReadDialogue(o["dialogue"] is JObject d ? d.ToString(Formatting.None) : null));
        }

        /// <summary>옛 두 파일(soul.json·dialogue.json)의 내용으로 만든다. 없는 쪽은 null — 새로 시작.
        /// 대사 진행이 깨졌으면 처음부터(혼은 살린다). 혼이 깨졌으면 예외를 그대로 던진다(부른 쪽이 .bad 로 옮김).</summary>
        public static SaveGame FromLegacy(GameData data, string? soulJson, string? dialogueJson)
        {
            var soul = string.IsNullOrWhiteSpace(soulJson) ? new Soul(data) : Soul.FromJson(data, soulJson!);
            return new SaveGame(soul, ReadDialogue(dialogueJson));
        }

        /// <summary>대사 진행 문자열. 없거나 깨졌으면 처음부터. 더 새 형식(NotSupportedException)은 그대로 던진다 — 새 판에서 쓴 저장을 옛 판이 덮어쓰지 않게.</summary>
        static DialogueProgress ReadDialogue(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new DialogueProgress();
            try { return DialogueProgress.FromJson(json!); }
            catch (Exception e) when (e is JsonException || e is FormatException || e is InvalidCastException || e is ArgumentException) { return new DialogueProgress(); }
        }
    }
}
