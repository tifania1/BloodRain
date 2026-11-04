using System.Collections.Generic;
using System.IO;
using System.Linq;
using BloodRain.Core.World;
using Newtonsoft.Json.Linq;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>Core Bgm.TrackId 가 고르는 곡과 audio_queue 의 곡이 서로 빠짐없이 맞는지, 우선순위가 맞는지.</summary>
    public class BgmTests
    {
        static HashSet<string> QueueIds() =>
            File.ReadAllLines(Path.Combine(RepoRoot, "audio_queue", "queue.jsonl")).Where(l => l.Trim().Length > 0)
                .Select(l => (string)JObject.Parse(l)["id"]!).ToHashSet();

        /// <summary>게임에서 나올 수 있는 상태를 데이터에서 모두 만들어 본다.</summary>
        static IEnumerable<BgmState> AllStates()
        {
            var d = Load();
            foreach (var screen in new[] { BgmScreen.Title, BgmScreen.Inn, BgmScreen.Chimbang, BgmScreen.Yeonmu, BgmScreen.Settle })
                yield return new BgmState(screen);
            foreach (var e in d.Cheonha.Cycle.EndingsFirst.Concat(d.Cheonha.Cycle.EndingsSecond).Distinct())
                yield return new BgmState(BgmScreen.Ending, endingId: e);
            var prologue = d.Cheonha.Regions.First(r => r.Id == d.Cheonha.PrologueBody.Region);
            yield return new BgmState(BgmScreen.Life, prologue: true, regionId: prologue.Id);
            yield return new BgmState(BgmScreen.Life, prologue: true, regionId: prologue.Id, bossId: d.Cheonha.PrologueBody.Boss);
            foreach (var r in d.Cheonha.Regions)
            {
                yield return new BgmState(BgmScreen.Life, regionId: r.Id, finalRegion: r.Final);
                if (r.Boss != null && !r.Final)
                    yield return new BgmState(BgmScreen.Life, regionId: r.Id, bossId: r.Boss);
            }
            for (int i = 1; i <= d.FinalBoss.Stages.Count; i++)
                yield return new BgmState(BgmScreen.Life, regionId: "bongmagok", bossId: d.FinalBoss.Boss, finalStage: i, finalRegion: true);
        }

        [Fact] public void 고를_수_있는_곡과_큐의_곡이_같다()
        {
            var picked = AllStates().Select(s => Bgm.TrackId(s)).Append(Bgm.AwakenSting).ToHashSet();
            var queue = QueueIds();
            Assert.Empty(picked.Except(queue));   // 고르는데 큐에 없는 곡
            Assert.Empty(queue.Except(picked));   // 큐에 있는데 아무 데서도 안 부르는 곡
        }

        [Fact] public void 서장_곡은_서장에만_청운진_곡은_두번째_생부터()
        {
            Assert.Equal("bgm_prologue", Bgm.TrackId(new BgmState(BgmScreen.Life, prologue: true, regionId: "cheongun")));
            Assert.Equal("bgm_region_cheongun", Bgm.TrackId(new BgmState(BgmScreen.Life, regionId: "cheongun")));
            Assert.Equal("bgm_boss_jang_sam", Bgm.TrackId(new BgmState(BgmScreen.Life, prologue: true, regionId: "cheongun", bossId: "jang_sam")));
        }

        [Fact] public void 땅의_주인_싸움은_지역_곡보다_앞선다()
        {
            Assert.Equal("bgm_region_songshan", Bgm.TrackId(new BgmState(BgmScreen.Life, regionId: "songshan")));
            Assert.Equal("bgm_boss_wongak", Bgm.TrackId(new BgmState(BgmScreen.Life, regionId: "songshan", bossId: "wongak")));
        }

        [Fact] public void 봉마곡은_혈영을_넘기는_동안_한_곡이고_혈마는_단계마다()
        {
            // 혈영 차례에는 그 땅의 주인 id 가 오지만 봉마곡 곡을 그대로 이어 간다
            Assert.Equal("bgm_region_bongmagok", Bgm.TrackId(new BgmState(BgmScreen.Life, regionId: "bongmagok", bossId: "wongak", finalRegion: true)));
            Assert.Equal("bgm_final_2", Bgm.TrackId(new BgmState(BgmScreen.Life, regionId: "bongmagok", bossId: "danmok_hyeon", finalStage: 2, finalRegion: true)));
        }

        [Fact] public void 화면_곡은_한_생_상태를_보지_않는다()
        {
            Assert.Equal("bgm_settle", Bgm.TrackId(new BgmState(BgmScreen.Settle, regionId: "songshan", bossId: "wongak")));
            Assert.Equal("bgm_ending_true", Bgm.TrackId(new BgmState(BgmScreen.Ending, endingId: "true")));
        }
    }
}
