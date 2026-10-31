#nullable enable
using System.Linq;
using BloodRain.Core.Data;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>대표 인물(characters.json)과 그 문파 몸(cheonha.json sects)은 같은 값이어야 한다.
    /// 천하에서는 문파가 몸의 바탕이고 서장·도감은 캐릭터를 읽으니, 한쪽만 고치면 둘이 갈라진다.</summary>
    public class LeaderBodyTests
    {
        readonly GameData _d = TestData.Load();

        [Fact] public void 대표_인물의_몸은_문파_몸과_같다()
        {
            foreach (var s in _d.Cheonha.Sects.Where(s => s.Leader != null))
            {
                var c = _d.Character(s.Leader!);
                Assert.True(s.StartWeapon == c.StartWeapon, $"{s.NameKo}/{c.NameKo} 시작 무공 {s.StartWeapon} ≠ {c.StartWeapon}");
                Assert.True(s.Ultimate == c.Ultimate, $"{s.NameKo}/{c.NameKo} 오의 {s.Ultimate} ≠ {c.Ultimate}");
                Assert.True(s.Trait == c.Trait, $"{s.NameKo}/{c.NameKo} 고유 특성 {s.Trait} ≠ {c.Trait}");
                Assert.True(s.MaxHealth == c.MaxHealth, $"{s.NameKo}/{c.NameKo} 기혈 {s.MaxHealth} ≠ {c.MaxHealth}");
                Assert.True(System.Math.Abs(s.MoveSpeed - c.MoveSpeed) < 1e-6f, $"{s.NameKo}/{c.NameKo} 걸음 {s.MoveSpeed} ≠ {c.MoveSpeed}");
                Assert.True(s.YinyangStart == c.YinyangStart, $"{s.NameKo}/{c.NameKo} 음양 시작 {s.YinyangStart} ≠ {c.YinyangStart}");
            }
        }

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
