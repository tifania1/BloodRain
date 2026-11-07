using System;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>DD-01: 일일 도전(7장 '모드' — 매일 같은 시드, 문파·타고난 성질·사건 고정).</summary>
    public class DailyChallengeTests
    {
        readonly GameData _d = Load();

        [Fact] public void 기획서_모드_표와_데이터()
        {
            Assert.Contains("| 일일 도전 | 매일 같은 시드(문파·타고난 성질·사건 고정). Steam 리더보드 | 봉인석 3개 |", Gdd);
            Assert.Contains(_d.Cheonha.Modes, m => m.Id == DailyChallenge.ModeId && m.Require.SealStones == 3);
        }

        [Fact] public void 같은_날은_시각과_상관없이_같은_시드_다른_날은_다르다()
        {
            var a = DailyChallenge.Seed(new DateTime(2026, 11, 7, 0, 0, 1, DateTimeKind.Utc));
            Assert.Equal(a, DailyChallenge.Seed(new DateTime(2026, 11, 7, 23, 59, 59, DateTimeKind.Utc)));
            var days = Enumerable.Range(0, 400).Select(i => DailyChallenge.Seed(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i))).ToList();
            Assert.Equal(days.Count, days.Distinct().Count());
            Assert.All(days, s => Assert.True(s >= 0));
        }

        [Fact] public void 시드가_고정값이다()
        {
            // 빌드·기계마다 같아야 리더보드가 같은 판이다. 이 값이 바뀌면 그날 판이 바뀐 것
            Assert.Equal(DailyChallenge.Seed(new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc)), DailyChallenge.Seed(new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc)));
            uint x = 20261107; x ^= x >> 16; x *= 0x7feb352dU; x ^= x >> 15; x *= 0x846ca68bU; x ^= x >> 16;
            Assert.Equal((int)(x & 0x7fffffff), DailyChallenge.Seed(new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc)));
        }

        [Fact] public void 같은_시드면_같은_몸_해금과_상관없이_아홉_문파에서()
        {
            var C = _d.Cheonha;
            var sects = Enumerable.Range(0, 120).Select(i => DailyChallenge.Seed(new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i)))
                .Select(s =>
                {
                    var a = DailyChallenge.Body(C, s);
                    var b = DailyChallenge.Body(C, s);
                    Assert.Equal((a.Sect.Id, a.Region.Id, a.Female), (b.Sect.Id, b.Region.Id, b.Female));
                    Assert.True(a.SameTraits(b));
                    Assert.Equal(a.Sect.Region, a.Region.Id);
                    return a.Sect.Id;
                }).Distinct().ToList();
            Assert.Contains(sects, id => C.Sect(id).Unlock != UnlockToken.Start);   // 처음부터 열린 셋 밖의 문파도 나온다
        }

        [Fact] public void 한_생_난수도_같은_시드면_같다()
        {
            int s = DailyChallenge.Seed(new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc));
            var a = DailyChallenge.LifeRandom(s); var b = DailyChallenge.LifeRandom(s);
            Assert.Equal(Enumerable.Range(0, 20).Select(_ => a.Next()), Enumerable.Range(0, 20).Select(_ => b.Next()));
        }
    }
}
