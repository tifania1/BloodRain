#nullable enable
// 혼 저장 세 칸(기획서 12장 '저장 칸 고르기', 15장 '저장').
//   칸마다 파일 하나: soul_1.json ~ soul_3.json. 안은 SaveGame 형식({version, soul, dialogue}) 그대로라 대사 진행도 칸마다 따로다.
//   Steam 클라우드도 칸 파일을 하나씩 올린다.
//   옛 저장(soul.json·dialogue.json, LF-03)은 처음 열 때 1번 칸으로 옮기고 원본은 .bak 로 남긴다(지우지 않음).
//   마지막으로 쓴 칸은 기기마다(Unity PlayerPrefs)라 여기 두지 않는다. 여기는 이어하기가 어느 칸으로 갈지 셈만 한다.
//   칸 카드에 띄울 값은 SoulSlotSummary.From. 혼에 시각이 없어서 '마지막으로 한 날'은 Unity 가 파일 시각을 넘겨준다.
using System;
using System.Collections.Generic;
using System.Linq;

namespace BloodRain.Core.Meta
{
    public static class SoulSlots
    {
        /// <summary>저장 칸 수.</summary>
        public const int Count = 3;
        /// <summary>첫 칸 번호. 칸 번호는 1부터 센다(화면 글자 'N번 칸'과 파일 이름이 같은 수).</summary>
        public const int First = 1;
        /// <summary>옛 한 칸 저장을 옮겨 넣을 칸.</summary>
        public const int LegacySlot = 1;
        /// <summary>옮긴 옛 파일에 붙이는 꼬리(soul.json → soul.json.bak).</summary>
        public const string BackupSuffix = ".bak";

        /// <summary>칸 번호 전부(1, 2, 3).</summary>
        public static IReadOnlyList<int> All { get; } = Enumerable.Range(First, Count).ToArray();

        /// <summary>옛 저장 파일(LF-03). 옮긴 뒤 둘 다 .bak 로 남긴다. 대사 파일은 없을 수도 있다.</summary>
        public static IReadOnlyList<string> LegacyFiles { get; } = new[] { SaveGame.LegacySoulFile, SaveGame.LegacyDialogueFile };

        public static bool IsValid(int slot) => slot >= First && slot < First + Count;

        /// <summary>칸 파일 이름(persistentDataPath 아래, Steam 클라우드에도 이 이름). 없는 칸 번호면 ArgumentOutOfRangeException.</summary>
        public static string FileName(int slot)
        {
            if (!IsValid(slot)) throw new ArgumentOutOfRangeException(nameof(slot), slot, $"저장 칸은 {First}~{First + Count - 1} 입니다");
            return $"soul_{slot}.json";
        }

        /// <summary>파일 이름이 칸 파일이면 그 칸 번호, 아니면 null(soul.json·soul_4.json·soul_1.json.tmp 는 null).</summary>
        public static int? SlotOf(string fileName)
        {
            foreach (var n in All)
                if (string.Equals(fileName, FileName(n), StringComparison.Ordinal)) return n;
            return null;
        }

        public static string BackupName(string fileName) => fileName + BackupSuffix;

        /// <summary>옛 저장을 1번 칸으로 옮길 때인가. 옛 혼 파일(soul.json)이 있고 1번 칸 파일이 아직 없을 때만.
        /// 1번 칸이 이미 있으면(옮기다 꺼졌거나 이미 썼거나) 덮어쓰지 않는다. 옮기기는 SaveGame.FromLegacy 로 읽어 1번 칸에 쓰고 LegacyFiles 를 .bak 로.</summary>
        public static bool ShouldMigrateLegacy(bool legacySoulExists, bool legacySlotExists) => legacySoulExists && !legacySlotExists;

        /// <summary>이어하기로 갈 칸. 마지막으로 쓴 칸이 맞는 번호이고 비어 있지 않으면 그 칸,
        /// 아니면(기억이 없거나 그 칸을 지웠거나 다른 기기에서 받은 저장) 마지막으로 한 날이 가장 늦은 칸(날이 같거나 없으면 앞 번호).
        /// 혼이 든 칸이 없으면 null — 이어하기 단추를 보이지 않는다.</summary>
        public static int? ContinueSlot(int? lastUsed, IEnumerable<SoulSlotSummary> slots)
        {
            var filled = slots.Where(s => !s.IsEmpty && IsValid(s.Slot)).ToList();
            if (filled.Count == 0) return null;
            if (lastUsed is int last && filled.Any(s => s.Slot == last)) return last;
            return filled
                .OrderByDescending(s => s.LastPlayed ?? DateTime.MinValue)
                .ThenBy(s => s.Slot)
                .First().Slot;
        }

        /// <summary>칸을 골랐을 때 갈 곳: 빈 칸이면 서장, 혼이 든 칸이면 객잔(12장 타이틀 흐름).</summary>
        public static SlotDestination DestinationOf(SoulSlotSummary slot) => slot.IsEmpty ? SlotDestination.Prologue : SlotDestination.Inn;
    }

    /// <summary>칸을 고른 뒤 여는 곳.</summary>
    public enum SlotDestination
    {
        /// <summary>서장(Run 씬, 진소운 제 몸). 인트로 컷신이 생기면 그것부터.</summary>
        Prologue,
        /// <summary>객잔(Inn 씬).</summary>
        Inn,
    }

    /// <summary>저장 칸 카드 한 장에 띄울 값(12장 '저장 칸 고르기'). 모두 혼에 이미 있는 값이고, 마지막으로 한 날만 밖에서 받는다.</summary>
    public sealed class SoulSlotSummary
    {
        public int Slot { get; }
        /// <summary>새 혼(아직 한 생도 마치지 않음). 카드에 '새 혼'만 띄우고 고르면 서장. 타이틀 이어하기와 같은 셈.</summary>
        public bool IsEmpty { get; }
        /// <summary>마친 생 수(서장 포함, Soul.Lives).</summary>
        public int Lives { get; }
        /// <summary>이 칸으로 이어서 살 생이 몇 번째인지(마친 생 + 1, 침방 'N번째 생'과 같은 셈).</summary>
        public int LifeNumber => Lives + 1;
        /// <summary>가장 높이 오른 경지 id. 돌파한 적이 없으면 첫 경지. 빈 칸이면 null.</summary>
        public string? HighestRealm { get; }
        /// <summary>이번 회차에 모은 봉인석 수.</summary>
        public int SealStones { get; }
        /// <summary>종장에 필요한 봉인석 수(cheonha.json bosses.finalRequires.sealStones). '봉인석 3/7' 의 7.</summary>
        public int SealStonesNeeded { get; }
        /// <summary>한 번이라도 밟은 땅 수.</summary>
        public int RegionsVisited { get; }
        /// <summary>연무장에서 산 레벨의 합(항목마다 레벨을 더한 값).</summary>
        public int TrainingLevels { get; }
        /// <summary>마지막으로 한 날. 혼에는 시각이 없어 Unity 가 칸 파일의 마지막 쓴 시각을 넣는다. 모르면 null.</summary>
        public DateTime? LastPlayed { get; }

        SoulSlotSummary(int slot, bool isEmpty, int lives, string? highestRealm, int sealStones, int sealStonesNeeded,
            int regionsVisited, int trainingLevels, DateTime? lastPlayed)
        {
            Slot = slot; IsEmpty = isEmpty; Lives = lives; HighestRealm = highestRealm; SealStones = sealStones;
            SealStonesNeeded = sealStonesNeeded; RegionsVisited = regionsVisited; TrainingLevels = trainingLevels; LastPlayed = lastPlayed;
        }

        /// <summary>파일이 없는 칸.</summary>
        public static SoulSlotSummary Empty(int slot, DateTime? lastPlayed = null)
        {
            CheckSlot(slot);
            return new SoulSlotSummary(slot, true, 0, null, 0, 0, 0, 0, lastPlayed);
        }

        /// <summary>칸에 든 혼으로 카드 값을 만든다. 한 생도 안 마친 혼이면 빈 칸(새 혼)으로 친다.</summary>
        public static SoulSlotSummary From(Soul soul, int slot, DateTime? lastPlayed = null)
        {
            if (soul == null) throw new ArgumentNullException(nameof(soul));
            CheckSlot(slot);
            if (IsFresh(soul)) return Empty(slot, lastPlayed);
            var realms = soul.Data.Balance.Realms.OrderBy(r => r.Level).ToList();
            var top = realms.LastOrDefault(r => soul.HasReached(r.Id)) ?? realms[0];
            return new SoulSlotSummary(slot, false, soul.Lives, top.Id, soul.SealStoneCount,
                soul.Data.Cheonha.Bosses.FinalRequires.SealStones, soul.VisitedRegions.Count,
                soul.Training.Values.Sum(), lastPlayed);
        }

        /// <summary>새 혼인가: 서장도 안 마쳤고 마친 생도 없다(타이틀 TT-01U 이어하기와 같은 셈).</summary>
        public static bool IsFresh(Soul soul) => !soul.PrologueDone && soul.Lives == 0;

        static void CheckSlot(int slot)
        {
            if (!SoulSlots.IsValid(slot))
                throw new ArgumentOutOfRangeException(nameof(slot), slot, $"저장 칸은 {SoulSlots.First}~{SoulSlots.First + SoulSlots.Count - 1} 입니다");
        }
    }

    /// <summary>칸 지우기를 화면 안에서 두 번 묻는 차례(12장 '저장 칸 고르기', 따로 뜨는 확인 창 없음).
    /// 카드의 지우기 → Ask(칸) → 첫 물음 → Confirm → 둘째 물음 → Confirm 이면 그 칸 번호를 돌려준다(그때 파일을 지운다). 아무 때나 Cancel.</summary>
    public sealed class SlotDeleteAsk
    {
        /// <summary>묻고 있는 칸. 안 묻는 중이면 null.</summary>
        public int? Slot { get; private set; }
        /// <summary>0 안 물음, 1 첫 물음(ui.slot.delete_ask), 2 둘째 물음(ui.slot.delete_ask_again).</summary>
        public int Step { get; private set; }

        public void Ask(int slot)
        {
            if (!SoulSlots.IsValid(slot)) throw new ArgumentOutOfRangeException(nameof(slot));
            Slot = slot;
            Step = 1;
        }

        /// <summary>'지운다'를 눌렀다. 둘째 물음에서 누르면 지울 칸 번호, 그 전엔 null(다음 물음으로 넘어감).</summary>
        public int? Confirm()
        {
            if (Slot == null) return null;
            if (Step == 1) { Step = 2; return null; }
            var slot = Slot;
            Cancel();
            return slot;
        }

        public void Cancel()
        {
            Slot = null;
            Step = 0;
        }
    }
}
