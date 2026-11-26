using System;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>WP-38(2026-11-20 추천대로, decisions #39): 보스 연전 — 현경 문턱까지 레벨업을 처음에 다 고름(11-26, #69), 일곱을 데이터 차례로, 처치마다 레벨업 3번, 기혈은 20분 값, 혼엔 시간만.</summary>
    public class BossRushTests
    {
        readonly GameData _d = Load();
        CheonhaDef C => _d.Cheonha;

        [Fact] public void 데이터가_정한_값()
        {
            Assert.Equal("hyeongyeong", C.BossRush.StartRealm);
            Assert.Contains(_d.Balance.Realms, r => r.Id == C.BossRush.StartRealm);
            Assert.True(C.BossRush.PickLevelsToRealm);   // 2026-11-26 사람, decisions #69
            Assert.Equal(90, BossRush.StartLevel(_d));
            Assert.Equal(89, BossRush.StartLevelUps(_d));
            Assert.Equal(3, C.BossRush.LevelUpsPerKill);
            Assert.Equal(20f, C.BossRush.HealthMinute);
            Assert.Equal(new[] { "wongak", "dang_sohye", "ma_cheonpae", "hyeolheo", "huashan_sword_soul", "seol_muhyang", "hyeokryeon_hwi" },
                BossRush.Order(C).Select(r => r.Boss));
            Assert.Contains(C.Modes, m => m.Id == BossRush.ModeId && m.Require.Ending == "seal");
            Assert.Contains("보스 연전(정해짐 2026-11-20, 시작은 2026-11-26 바뀜): 고른 몸으로 Lv1 에서 현경 문턱(Lv90)까지 오르며 그 레벨업 89번을", Gdd);
        }

        [Fact] public void 기혈은_기준_체력_그대로()
        {
            foreach (var r in BossRush.Order(C))
                Assert.Equal(_d.Bosses.First(b => b.Id == r.Boss).Health, BossRush.Health(_d, r.Id), 3);
        }

        [Fact] public void 일곱을_차례로_쓰러뜨리면_끝나고_시간이_남는다()
        {
            var rush = new BossRush(_d);
            Assert.Equal("songshan", rush.Current!.Id);
            Assert.Null(rush.ClearSeconds);
            int levelUps = 0;
            for (int i = 0; i < 7; i++) { rush.Tick(30.4f); levelUps += rush.Defeat(); }
            Assert.True(rush.Finished);
            Assert.Null(rush.Current);
            Assert.Equal(18, levelUps); // 마지막 처치는 레벨업 없이 끝
            Assert.Equal(212, rush.ClearSeconds);
            rush.Tick(10);
            Assert.Equal(212, rush.ClearSeconds); // 끝난 뒤엔 시계가 안 감
            Assert.Throws<InvalidOperationException>(() => rush.Defeat());
        }

        [Fact] public void 혼엔_가장_빠른_시간만()
        {
            var s = new Soul(_d);
            Assert.Equal(0, s.BossRushBestSeconds);
            Assert.True(s.RecordBossRush(300));
            Assert.False(s.RecordBossRush(320));
            Assert.True(s.RecordBossRush(250));
            Assert.Equal(250, Soul.FromJson(_d, s.ToJson()).BossRushBestSeconds);
        }
    }
}
