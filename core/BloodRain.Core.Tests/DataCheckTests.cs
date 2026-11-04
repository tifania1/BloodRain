using System;
using System.Linq;
using BloodRain.Core.Data;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>QA-03: 읽을 때 0 나누기·무한 반복·순서 뒤집힘을 잡는 DataCheck. 지금 데이터는 문제가 없고, 일부러 망친 값은 하나씩 잡힌다.</summary>
    public class DataCheckTests
    {
        /// <summary>망쳐 볼 사본. TestData.Load() 는 다른 테스트와 같이 쓰는 캐시라 건드리지 않는다.</summary>
        static GameData Fresh() => GameData.FromJson(TestData.ReadFiles());

        static void Broken(Action<GameData> breakIt, string expect)
        {
            var d = Fresh();
            breakIt(d);
            var problems = DataCheck.Problems(d);
            Assert.True(problems.Any(p => p.Contains(expect)), $"'{expect}' 가 안 잡힘. 잡힌 것: {string.Join(" / ", problems)}");
        }

        [Fact] public void 지금_데이터는_문제가_없다() => Assert.Empty(DataCheck.Problems(TestData.Load()));

        [Fact] public void 깨달음_killsPerStack_0() =>
            Broken(d => d.Insight("salgi").Params["killsPerStack"] = 0, "salgi.killsPerStack");

        [Fact] public void 넷째_선택지_행운_0() =>
            Broken(d => d.Balance.LevelUp.LuckForFourthChoice = 0, "luckForFourthChoice");

        [Fact] public void 오의_게이지_최대_0() =>
            Broken(d => d.Balance.UltimateCharge.Max = 0, "ultimateCharge.max");

        [Fact] public void 무공_초당_발수_0()
        {
            var w = Fresh().Weapons.First(x => x.ShotsPerSecond != null);
            Broken(d => d.Weapon(w.Id).ShotsPerSecond![0] = 0, $"weapons.{w.Id}.shotsPerSecond[0]");
        }

        /// <summary>base 0·step 0 이면 필요 진기가 0 이라 LevelFor 가 끝나지 않는다.</summary>
        [Fact] public void 경험치_구간_필요진기_0() =>
            Broken(d => { d.Balance.XpCurve.Segments[0].Base = 0; d.Balance.XpCurve.Segments[0].Step = 0; }, "balance.xpCurve 레벨 1");

        [Fact] public void 경험치_구간_비었음() =>
            Broken(d => d.Balance.XpCurve.Segments.Clear(), "segments 가 비었다");

        [Fact] public void 경험치_구간_거꾸로() =>
            Broken(d => d.Balance.XpCurve.Segments[1].ToLevel = 10, "segments[1] fromLevel");

        [Fact] public void 생성_목표_분이_겹침() =>
            Broken(d => d.Balance.SpawnTarget.Points[1][0] = d.Balance.SpawnTarget.Points[0][0], "spawnTarget.points[1]");

        [Fact] public void 생성_목표_점이_하나() =>
            Broken(d => d.Balance.SpawnTarget.Points.RemoveRange(1, d.Balance.SpawnTarget.Points.Count - 1), "spawnTarget.points 점이 1개");

        [Fact] public void 보스_체력곡선_거꾸로() =>
            Broken(d => d.Cheonha.Bosses.HealthCurve[2][0] = 1, "healthCurve[2]");

        [Fact] public void 보스_체력곡선_칸이_모자람() =>
            Broken(d => d.Cheonha.Bosses.HealthCurve[0] = new float[] { 5 }, "healthCurve[0] 는 [분, 값]");

        [Fact] public void 경지_레벨_겹침() =>
            Broken(d => d.Balance.Realms[2].Level = d.Balance.Realms[1].Level, "balance.realms[2]");

        [Fact] public void 깨지는_기믹_은자_최소가_최대보다_큼() =>
            Broken(d => { var g = d.Gimmicks.SwordRacks; g.Drop = "silver"; g.SilverMin = 50; g.SilverMax = 10; }, "gimmicks.swordRacks silverMax");
    }
}
