using System;
using System.Globalization;
using System.Text.RegularExpressions;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>등롱이 서는 간격·자리·깨짐·줍는 거리(기획서 8장 '드롭', balance.json drops.lanternSpawn, LT-01).</summary>
    public class LanternSpawnTests
    {
        readonly GameData _d = TestData.Load();
        LanternSpawnDef S => _d.Balance.Drops.LanternSpawn;

        static float F(string v) => float.Parse(v, CultureInfo.InvariantCulture);

        [Fact] public void 등롱_문단이_데이터와_같다()
        {
            var m = Regex.Match(TestData.Gdd, @"등롱은 (\d+)초마다\(서 있는 등롱이 (\d+)개 아래일 때\) 플레이어 둘레 ([\d.]+)\\~([\d.]+)m 거리에 하나 섭니다\. 무공이 친 자리의 반경에 등롱 몸 ([\d.]+)m를 더해 그 안이면 깨지고, 떨어진 것은 ([\d.]+)m 안에 오면 줍습니다\. 은빛 등롱은 화면 대각선의 ([\d.]+)배");
            Assert.True(m.Success, "기획서 8장 드롭에 등롱 서는 문단이 없다");
            Assert.Equal(F(m.Groups[1].Value), S.IntervalSeconds);
            Assert.Equal(int.Parse(m.Groups[2].Value), S.MaxAlive);
            Assert.Equal(F(m.Groups[3].Value), S.RingMin);
            Assert.Equal(F(m.Groups[4].Value), S.RingMax);
            Assert.Equal(F(m.Groups[5].Value), S.HitPadding);
            Assert.Equal(F(m.Groups[6].Value), S.PickupRadius);
            Assert.Equal(F(m.Groups[7].Value), S.SilverViewRatio);
        }

        [Fact] public void 데이터가_없으면_웹_프로토타입_값이다()
        {
            var d = new LanternSpawnDef();
            Assert.Equal(33f, d.IntervalSeconds);
            Assert.Equal(4, d.MaxAlive);
            Assert.Equal(5f, d.RingMin);
            Assert.Equal(9f, d.RingMax);
            Assert.Equal(0.4f, d.HitPadding);
            Assert.Equal(0.8f, d.PickupRadius);
            Assert.Equal(0.28f, d.SilverViewRatio);
        }

        [Fact] public void 간격이_지나고_넷_아래일_때만_선다()
        {
            Assert.False(Drops.LanternDue(S, 32.9f, 0));
            Assert.True(Drops.LanternDue(S, 33f, 0));
            Assert.True(Drops.LanternDue(S, 40f, 3));
            Assert.False(Drops.LanternDue(S, 40f, 4));
        }

        [Fact] public void 새_등롱은_5에서_9m_고리_안()
        {
            var rng = new Random(7);
            for (int i = 0; i < 500; i++)
            {
                var (x, y) = Drops.LanternOffset(S, rng);
                float r = MathF.Sqrt(x * x + y * y);
                Assert.InRange(r, S.RingMin - 1e-4f, S.RingMax + 1e-4f);
            }
        }

        [Fact] public void 친_원에_몸_반경을_더해_깨지고_줍기는_경계를_뺀다()
        {
            Assert.True(Drops.LanternHit(S, 2.4f, 0f, 2f));
            Assert.False(Drops.LanternHit(S, 2.5f, 0f, 2f));
            Assert.True(Drops.InPickup(S, 0.79f));
            Assert.False(Drops.InPickup(S, 0.8f));
            Assert.Equal(20f * 0.28f, Drops.SilverLanternDistance(S, 20f), 4);
        }

        [Fact] public void 웹_프로토타입이_같은_값을_읽는다()
        {
            var html = TestData.ReadRepoFile("web", "prototype", "template.html");
            Assert.Contains("const LS = DR.lanternSpawn;", html);
            Assert.Contains("G.lanternAcc >= LS.intervalSeconds / G.timeScale && G.lanterns.filter(l => !l.dead).length < LS.maxAlive", html);
            Assert.Contains("R = rand(LS.ringMin, LS.ringMax)", html);
            Assert.Contains("<= R + LS.hitPadding) breakLantern(l)", html);
            Assert.Contains("d < LS.pickupRadius", html);
            Assert.Contains("R = diag * LS.silverViewRatio", html);
        }
    }
}
