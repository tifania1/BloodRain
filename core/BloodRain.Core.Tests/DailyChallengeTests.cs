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
        [Fact] public void 일일_도전은_혼의_복사본으로_돌려_진짜_혼엔_안_남는다()
        {
            var soul = new Soul(_d);
            soul.EndLife(soul.BeginLife(Rebirth.Prologue(_d)).Finish("fallen", 0));
            soul.AddRebirthQi(5000);
            Assert.True(soul.TryBuyTraining(_d.Balance.Training.Items[0].Stat));
            var before = soul.ToJson();

            var sand = DailyChallenge.Sandbox(soul);
            var stat = _d.Balance.Training.Items[0].Stat;
            Assert.Equal(1, sand.TrainingLevel(stat)); // 연무장은 받는다
            var C = _d.Cheonha;
            var body = DailyChallenge.Body(C, DailyChallenge.Seed(new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc)));
            sand.EndLife(new LifeRecord(body.Sect.Id, "fallen", new RebirthQiLedger(C.RebirthQi).Settle(0), Array.Empty<string>(),
                new[] { body.Region.Id }, "ilryu", Array.Empty<string>()));
            sand.Discover(CodexKey.Secret(_d.Synergies.Secrets[0].Id));
            Assert.Equal(before, soul.ToJson());
            Assert.NotEqual(before, sand.ToJson());
        }

        [Fact] public void 리더보드는_날마다_따로_점수는_버틴_초()
        {
            Assert.Equal("daily_20261120", DailyChallenge.LeaderboardName(new DateTime(2026, 11, 20, 23, 59, 0, DateTimeKind.Utc)));
            Assert.Equal("daily_20261107", DailyChallenge.LeaderboardName(new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc)));
            Assert.Equal(0, DailyChallenge.Score(0));
            Assert.Equal(754, DailyChallenge.Score(12.5719f));
            Assert.Contains("버틴 시간이 리더보드에 오릅니다", Gdd);
        }
    }
}
