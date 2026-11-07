using BloodRain.Core.Data;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>11장 '성능 상한' 동시 적 수(PC 600·Steam Deck 400·모바일 400)를 생성 쪽이 묻는 판정(EN-03).</summary>
    public class EnemyCapTests
    {
        readonly LimitsDef _l = TestData.Load().Balance.Limits;

        [Fact] public void 상한_아래면_세운다()
        {
            Assert.True(_l.CanSpawn(_l.EnemiesPc - 1, mobile: false));
            Assert.True(_l.CanSpawn(_l.EnemiesMobile - 1, mobile: true));
        }

        [Fact] public void 상한에_닿으면_보통_적은_건너뛴다()
        {
            Assert.False(_l.CanSpawn(_l.EnemiesPc, mobile: false));
            Assert.False(_l.CanSpawn(_l.EnemiesMobile, mobile: true));
            Assert.True(_l.CanSpawn(_l.EnemiesMobile, mobile: false)); // PC 는 600 까지
        }

        [Fact] public void Steam_Deck_은_15장_성능_목표대로_400()
        {
            // 리눅스 PC 로 잡혀 isMobilePlatform 이 거짓이라도 Deck 은 400 에서 멈춘다(SD-01)
            Assert.Equal(400, _l.EnemyCap(PlatformTier.SteamDeck));
            Assert.False(_l.CanSpawn(_l.EnemiesSteamDeck, PlatformTier.SteamDeck));
            Assert.True(_l.CanSpawn(_l.EnemiesSteamDeck - 1, PlatformTier.SteamDeck));
            Assert.Equal(_l.EnemyCap(true), _l.EnemyCap(PlatformTier.Mobile));
            Assert.Equal(_l.EnemyCap(false), _l.EnemyCap(PlatformTier.Pc));
        }

        [Fact] public void 정예_같은_것은_넘어도_세운다()
        {
            Assert.True(_l.CanSpawn(_l.EnemiesPc + 50, mobile: false, important: true));
        }

        [Fact] public void 화면_목표_정점은_상한_안이다()
        {
            // 11장: 18분 정점 400 — 모바일 상한과 같고 PC 상한 아래. 사건 무리가 얹힐 자리가 PC 엔 남는다
            float peak = 0;
            foreach (var p in TestData.Load().Balance.SpawnTarget.Points) if (p[1] > peak) peak = p[1];
            Assert.True(peak <= _l.EnemiesMobile);
            Assert.True(peak < _l.EnemiesPc);
        }
        [Fact] public void 웹도_같은_상한()
        {
            var html = TestData.ReadRepoFile("web", "prototype", "template.html");
            Assert.Contains("matchMedia('(pointer: coarse)').matches) ? LIM.enemiesMobile : LIM.enemiesPc", html);
            Assert.Contains("const roomForFoe = () => G.enemies.length < ENEMY_CAP;", html);
            Assert.Contains("i < n && roomForFoe(); i++) { const [x, y] = ringPos()", html);
        }
    }
}
