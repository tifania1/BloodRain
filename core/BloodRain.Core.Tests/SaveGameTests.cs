using System;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using Newtonsoft.Json.Linq;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>SV-01: 메타 진행을 저장 칸마다 JSON 한 파일로(15장 '저장'), 옛 두 파일(LF-03)에서 옮기기.</summary>
    public class SaveGameTests
    {
        readonly GameData _d = Load();

        Soul SomeSoul()
        {
            var soul = Soul.FromJson(_d, new Soul(_d).ToJson().Replace("\"rebirthQi\": 0", "\"rebirthQi\": 1234"));
            soul.Discover(CodexKey.Link(_d.Synergies.Links[0].Id));
            return soul;
        }

        static DialogueProgress SomeDialogue() =>
            DialogueProgress.FromJson("{\"Version\":1,\"Seen\":[\"mok:first\"],\"Joined\":[\"hyegong\"],\"InnNext\":{\"hyegong\":2},\"Cycle\":2}");

        [Fact] public void 기획서는_칸마다_한_파일_저장()
        {
            // 11-09 혼 저장 세 칸(SV-01): 'JSON 한 파일' → 칸마다 한 파일. 칸 파일 이름은 SoulSlots(SoulSlotsTests)
            Assert.Contains("메타 진행은 저장 칸마다 JSON 한 파일", Gdd);
            Assert.Contains("대화 진행도 칸마다", Gdd);
        }

        [Fact] public void 묶었다_풀면_같다()
        {
            var s = new SaveGame(SomeSoul(), SomeDialogue());
            var json = s.ToJson();
            var back = SaveGame.FromJson(_d, json);
            Assert.Equal(s.Soul.ToJson(), back.Soul.ToJson());
            Assert.Equal(s.Dialogue.ToJson(), back.Dialogue.ToJson());
            Assert.Equal(json, back.ToJson());   // 같은 진행이면 같은 문자열
            Assert.Equal(1234, back.Soul.RebirthQi);
            Assert.True(back.Dialogue.HasJoined("hyegong"));
            Assert.Equal(2, back.Dialogue.InnLinesSaid("hyegong"));
            var o = JObject.Parse(json);
            Assert.Equal(SaveGame.SaveVersion, (int)o["version"]!);
            Assert.IsType<JObject>(o["soul"]);
            Assert.IsType<JObject>(o["dialogue"]);
        }

        [Fact] public void 새_저장은_빈_혼()
        {
            var s = SaveGame.FromJson(_d, SaveGame.New(_d).ToJson());
            Assert.Equal(new Soul(_d).ToJson(), s.Soul.ToJson());
            Assert.Equal(new DialogueProgress().ToJson(), s.Dialogue.ToJson());
        }

        [Fact] public void 대사가_빠지면_처음부터_혼은_살림()
        {
            var o = JObject.Parse(new SaveGame(SomeSoul(), SomeDialogue()).ToJson());
            o.Remove("dialogue");
            var s = SaveGame.FromJson(_d, o.ToString());
            Assert.Equal(1234, s.Soul.RebirthQi);
            Assert.False(s.Dialogue.HasJoined("hyegong"));
        }

        [Fact] public void 대사가_깨져도_혼은_살림()
        {
            var o = JObject.Parse(new SaveGame(SomeSoul(), SomeDialogue()).ToJson());
            o["dialogue"]!["Cycle"] = "셋";   // 숫자 칸에 글자
            var s = SaveGame.FromJson(_d, o.ToString());
            Assert.Equal(1234, s.Soul.RebirthQi);
            Assert.False(s.Dialogue.HasJoined("hyegong"));
        }

        [Fact] public void 혼이_없거나_새_형식이면_예외()
        {
            Assert.Throws<FormatException>(() => SaveGame.FromJson(_d, "{\"version\":1}"));
            Assert.Throws<FormatException>(() => SaveGame.FromJson(_d, "  "));
            var o = JObject.Parse(SaveGame.New(_d).ToJson());
            o["version"] = SaveGame.SaveVersion + 1;
            Assert.Throws<NotSupportedException>(() => SaveGame.FromJson(_d, o.ToString()));
        }

        [Fact] public void 옛_두_파일에서_옮긴다()
        {
            var soul = SomeSoul(); var dlg = SomeDialogue();
            var s = SaveGame.FromLegacy(_d, soul.ToJson(), dlg.ToJson());
            Assert.Equal(soul.ToJson(), s.Soul.ToJson());
            Assert.Equal(dlg.ToJson(), s.Dialogue.ToJson());
            // 대사 파일이 없거나 깨졌으면 대사만 처음부터
            Assert.Equal(new DialogueProgress().ToJson(), SaveGame.FromLegacy(_d, soul.ToJson(), null).Dialogue.ToJson());
            Assert.Equal(1234, SaveGame.FromLegacy(_d, soul.ToJson(), "{깨짐").Soul.RebirthQi);
            // 둘 다 없으면 새로
            Assert.Equal(SaveGame.New(_d).ToJson(), SaveGame.FromLegacy(_d, null, null).ToJson());
        }
    }
}
