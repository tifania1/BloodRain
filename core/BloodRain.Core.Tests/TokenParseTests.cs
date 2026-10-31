#nullable enable
using System.Linq;
using BloodRain.Core.Data;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>데이터 글자 토큰 읽기(해금 "boss:&lt;지역&gt;", 음양 "yang/yin")와 대표 인물 배정.
    /// 대표 인물 몸 값 대조는 CheonhaDataTests 에 있다.</summary>
    public class TokenParseTests
    {
        readonly GameData _d = TestData.Load();

        [Fact] public void 대표_인물은_한_문파에만_속한다()
        {
            var leaders = _d.Cheonha.Sects.Where(s => s.Leader != null).Select(s => s.Leader!).ToList();
            Assert.Equal(leaders.Count, leaders.Distinct().Count());
            Assert.DoesNotContain("jin_sowoon", leaders);
        }

        [Theory]
        [InlineData("start", null)]
        [InlineData("boss:huashan", "huashan")]
        [InlineData("boss:", "")]
        [InlineData("Boss:huashan", null)]
        [InlineData("kills_5000_one_run", null)]
        [InlineData("", null)]
        public void 해금_토큰_읽기(string token, string? region)
        {
            Assert.Equal(region, UnlockToken.BossRegion(token));
            Assert.Equal(region, new SectDef { Unlock = token }.UnlockBossRegion);
            Assert.Equal(region, new CharacterDef { Unlock = token }.UnlockBossRegion);
        }

        [Theory]
        [InlineData("yang", YinYang.Yang)]
        [InlineData("yin", YinYang.Yin)]
        [InlineData("neutral", YinYang.Neutral)]
        [InlineData("Yang", YinYang.Neutral)]
        [InlineData("", YinYang.Neutral)]
        public void 음양_글자_읽기(string s, YinYang expected) => Assert.Equal(expected, YinYangParse.From(s));
    }
}
