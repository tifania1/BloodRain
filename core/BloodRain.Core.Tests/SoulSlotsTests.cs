using System;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>SV-01(11-09): 혼 저장 세 칸(12장 '저장 칸 고르기'·'타이틀과 저장 칸', 15장 '저장').</summary>
    public class SoulSlotsTests
    {
        readonly GameData _d = Load();

        Soul AfterPrologue()
        {
            var s = new Soul(_d);
            var life = s.BeginLife(Rebirth.Prologue(_d));
            life.Breakthrough("iryu");
            life.Breakthrough("ilryu");
            s.EndLife(life.Finish("fallen", 0));
            return s;
        }

        /// <summary>서장 + 무당 한 생(봉인석 둘, 땅 셋, 초절정까지).</summary>
        Soul TwoLives()
        {
            var s = AfterPrologue();
            var qi = new RebirthQiLedger(_d.Cheonha.RebirthQi).Settle(0);
            s.EndLife(new LifeRecord("wudang", "fallen", qi, new[] { "wudangsan", "songshan" },
                new[] { "wudangsan", "songshan", "huashan" }, "chojeoljeong", Array.Empty<string>()));
            return s;
        }

        [Fact] public void 칸은_셋이고_파일은_칸마다_하나()
        {
            Assert.Equal(new[] { 1, 2, 3 }, SoulSlots.All);
            Assert.Equal(new[] { "soul_1.json", "soul_2.json", "soul_3.json" }, SoulSlots.All.Select(SoulSlots.FileName));
            foreach (var n in SoulSlots.All) Assert.Equal(n, SoulSlots.SlotOf(SoulSlots.FileName(n)));
            Assert.Throws<ArgumentOutOfRangeException>(() => SoulSlots.FileName(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => SoulSlots.FileName(4));
            Assert.False(SoulSlots.IsValid(0));
            Assert.Null(SoulSlots.SlotOf("soul.json"));
            Assert.Null(SoulSlots.SlotOf("soul_4.json"));
            Assert.Null(SoulSlots.SlotOf("soul_1.json.tmp"));
            // 옛 한 칸 저장 파일 이름과 겹치지 않는다(옮기기 전 파일을 칸으로 잘못 읽지 않게)
            Assert.DoesNotContain(SaveGame.LegacySoulFile, SoulSlots.All.Select(SoulSlots.FileName));
        }

        [Fact] public void 기획서에_칸_화면과_칸마다_한_파일이_있다()
        {
            var screens = GddTable(GddSection("화면 목록")).Select(r => r[0]).ToList();
            Assert.Contains("저장 칸 고르기", screens);
            Assert.Contains("### 타이틀과 저장 칸", Gdd);
            var save = GddSection("저장");
            Assert.Contains("저장 칸마다 JSON 한 파일", save);
            Assert.Contains($"`{SoulSlots.FileName(SoulSlots.First)}`~`{SoulSlots.FileName(SoulSlots.First + SoulSlots.Count - 1)}`", save);
            Assert.Contains("Steam 클라우드에도 칸마다", save);
            Assert.Contains("1번 칸으로 옮기고", save);
            Assert.Contains(SaveGame.LegacySoulFile, save);
            Assert.Contains(SoulSlots.BackupSuffix, save);
            Assert.Equal(1, SoulSlots.LegacySlot);
        }

        [Fact] public void 옛_저장은_1번_칸이_없을_때만_옮긴다()
        {
            Assert.True(SoulSlots.ShouldMigrateLegacy(legacySoulExists: true, legacySlotExists: false));
            Assert.False(SoulSlots.ShouldMigrateLegacy(true, true));    // 이미 옮겼거나 1번 칸을 썼으면 덮어쓰지 않는다
            Assert.False(SoulSlots.ShouldMigrateLegacy(false, false));
            Assert.Equal(new[] { "soul.json", "dialogue.json" }, SoulSlots.LegacyFiles);
            Assert.Equal("soul.json.bak", SoulSlots.BackupName(SaveGame.LegacySoulFile));
            // 옮긴 내용은 칸 파일 형식(SaveGame) 그대로 — 대사 진행도 같은 칸에
            var soul = TwoLives();
            var dlg = DialogueProgress.FromJson("{\"Version\":1,\"Seen\":[\"mok:first\"],\"Joined\":[\"hyegong\"],\"InnNext\":{},\"Cycle\":1}");
            var moved = SaveGame.FromJson(_d, SaveGame.FromLegacy(_d, soul.ToJson(), dlg.ToJson()).ToJson());
            Assert.Equal(soul.ToJson(), moved.Soul.ToJson());
            Assert.True(moved.Dialogue.HasJoined("hyegong"));
        }

        [Fact] public void 새_혼은_빈_칸()
        {
            var fresh = SoulSlotSummary.From(new Soul(_d), 2, new DateTime(2026, 11, 9));
            Assert.True(fresh.IsEmpty);
            Assert.Equal(2, fresh.Slot);
            Assert.Null(fresh.HighestRealm);
            Assert.Equal(0, fresh.Lives);
            Assert.Equal(SlotDestination.Prologue, SoulSlots.DestinationOf(fresh));
            var none = SoulSlotSummary.Empty(3);
            Assert.True(none.IsEmpty);
            Assert.Null(none.LastPlayed);
            Assert.Throws<ArgumentOutOfRangeException>(() => SoulSlotSummary.Empty(4));
            Assert.Throws<ArgumentOutOfRangeException>(() => SoulSlotSummary.From(new Soul(_d), 0));
        }

        [Fact] public void 칸_카드는_혼에_있는_값만_띄운다()
        {
            var soul = TwoLives();
            soul.AddRebirthQi(100000);
            Assert.True(soul.TryBuyTraining("Might"));
            Assert.True(soul.TryBuyTraining("Might"));
            Assert.True(soul.TryBuyTraining("MaxHealth"));
            var when = new DateTime(2026, 11, 9, 21, 30, 0);
            var c = SoulSlotSummary.From(soul, 1, when);
            Assert.False(c.IsEmpty);
            Assert.Equal(1, c.Slot);
            Assert.Equal(2, c.Lives);
            Assert.Equal(3, c.LifeNumber);                    // 침방 'N번째 생'과 같은 셈(마친 생 + 1)
            Assert.Equal("chojeoljeong", c.HighestRealm);
            Assert.Equal(soul.SealStoneCount, c.SealStones);
            Assert.Equal(2, c.SealStones);
            Assert.Equal(_d.Cheonha.Bosses.FinalRequires.SealStones, c.SealStonesNeeded);
            Assert.Equal(soul.VisitedRegions.Count, c.RegionsVisited);
            Assert.Equal(3, c.TrainingLevels);
            Assert.Equal(when, c.LastPlayed);
            Assert.Equal(SlotDestination.Inn, SoulSlots.DestinationOf(c));
        }

        [Fact] public void 돌파가_없으면_첫_경지()
        {
            var s = new Soul(_d);
            var life = s.BeginLife(Rebirth.Prologue(_d));
            s.EndLife(life.Finish("fallen", 0));
            var first = _d.Balance.Realms.OrderBy(r => r.Level).First().Id;
            var c = SoulSlotSummary.From(s, 1);
            Assert.Equal(first, c.HighestRealm);
            Assert.Equal((1, 2), (c.Lives, c.LifeNumber));
        }

        [Fact] public void 저장했다_읽어도_카드가_같다()
        {
            var soul = TwoLives();
            var back = SaveGame.FromJson(_d, new SaveGame(soul, new DialogueProgress()).ToJson()).Soul;
            var a = SoulSlotSummary.From(soul, 2);
            var b = SoulSlotSummary.From(back, 2);
            Assert.Equal((a.Lives, a.HighestRealm, a.SealStones, a.RegionsVisited, a.TrainingLevels),
                (b.Lives, b.HighestRealm, b.SealStones, b.RegionsVisited, b.TrainingLevels));
        }

        [Fact] public void 이어하기는_마지막으로_쓴_칸()
        {
            var full = TwoLives();
            var cards = new[]
            {
                SoulSlotSummary.From(full, 1, new DateTime(2026, 11, 1)),
                SoulSlotSummary.Empty(2),
                SoulSlotSummary.From(full, 3, new DateTime(2026, 11, 8)),
            };
            Assert.Equal(1, SoulSlots.ContinueSlot(1, cards));
            Assert.Equal(3, SoulSlots.ContinueSlot(3, cards));
            // 기억한 칸이 비었거나(지움) 없는 번호거나 기억이 없으면 마지막으로 한 날이 가장 늦은 칸
            Assert.Equal(3, SoulSlots.ContinueSlot(2, cards));
            Assert.Equal(3, SoulSlots.ContinueSlot(7, cards));
            Assert.Equal(3, SoulSlots.ContinueSlot(null, cards));
            // 날을 모르면 앞 번호
            var noDates = new[] { SoulSlotSummary.Empty(1), SoulSlotSummary.From(full, 2), SoulSlotSummary.From(full, 3) };
            Assert.Equal(2, SoulSlots.ContinueSlot(null, noDates));
            // 혼이 든 칸이 없으면 이어하기 없음
            var empty = SoulSlots.All.Select(n => SoulSlotSummary.From(new Soul(_d), n)).ToList();
            Assert.Null(SoulSlots.ContinueSlot(1, empty));
        }

        [Fact] public void 칸_지우기는_두_번_묻는다()
        {
            var ask = new SlotDeleteAsk();
            Assert.Null(ask.Confirm());          // 묻지 않았는데 누르면 아무 일 없음
            ask.Ask(2);
            Assert.Equal((2, 1), (ask.Slot!.Value, ask.Step));
            Assert.Null(ask.Confirm());          // 첫 물음 → 둘째 물음
            Assert.Equal(2, ask.Step);
            Assert.Equal(2, ask.Confirm());      // 둘째 물음에서 지운다
            Assert.Null(ask.Slot);
            Assert.Equal(0, ask.Step);

            ask.Ask(3);
            ask.Confirm();
            ask.Cancel();                        // 둘째 물음에서 그만두면 지우지 않음
            Assert.Null(ask.Confirm());
            Assert.Throws<ArgumentOutOfRangeException>(() => ask.Ask(0));
        }

        [Fact] public void 칸_화면_글자가_네_언어에_있다()
        {
            var keys = new[]
            {
                "title", "sub", "name", "new", "new_sub", "life", "realm", "seal_stones", "regions", "training",
                "last_played", "date", "delete", "delete_ask", "delete_ask_again", "delete_yes", "delete_no", "back",
            }.Select(k => "ui.slot." + k).ToList();
            foreach (var lang in new[] { "ko", "en", "zh-Hans", "zh-Hant" })
            {
                var table = Newtonsoft.Json.Linq.JObject.Parse(ReadRepoFile("unity", "BloodRain", "Assets", "Resources", "Data", "strings", lang + ".json"));
                foreach (var k in keys) Assert.False(string.IsNullOrWhiteSpace((string?)table[k]), $"{lang}: {k} 가 없다");
            }
        }
    }
}
