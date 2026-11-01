using System.Globalization;
using System.Linq;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>기획서 15장 '축척과 덩이'·'성능 목표' 와 8장 '조작'의 경공 문단에 적힌 숫자가 데이터와 같은지.
    /// 7장·11장 표는 따로 대조하지만 다른 장에 같은 숫자를 옮겨 적은 곳이 어긋나기 쉽다(2026-11-01 15장 돌파 1.2초가 남아 있었다).</summary>
    public class TechScaleTests
    {
        readonly Data.GameData _d = TestData.Load();
        static string N(float v) => v.ToString("#,0.##", CultureInfo.InvariantCulture);

        [Fact] public void 축척과_덩이_절의_숫자가_천하_데이터와_같다()
        {
            var sec = TestData.GddSection("축척과 덩이");
            var c = _d.Cheonha;
            Assert.Contains($"가로 {N(c.World.WidthMeters)} x 세로 {N(c.World.HeightMeters)}", sec);
            Assert.Contains($"2.25^경지", sec);
            Assert.Equal(2.25f, c.World.ScalePerRealm);
            Assert.Contains($"격자 크기도 경지마다 {N(c.World.ScalePerRealm)}배", sec);
            Assert.Contains($"덩이당 최대 {c.RealmScale.Max(r => r.ClusterSize)}", sec);
            Assert.Contains($"연출 {N(c.Breakthrough.ZoomToSeconds)}초(이미 올라 본 경지는 {N(c.Breakthrough.RevisitSeconds)}초)", sec);
        }

        [Fact] public void 성능_목표_표의_적_투사체_수가_성능_상한과_같다()
        {
            var l = _d.Balance.Limits;
            var rows = TestData.GddTable(TestData.GddSection("성능 목표"));
            Assert.Contains($"적 {l.EnemiesPc}, 투사체 {N(l.Projectiles)}", rows.Single(r => r[0].StartsWith("PC"))[3]);
            Assert.Equal($"적 {l.EnemiesMobile}", rows.Single(r => r[0].StartsWith("모바일"))[3]);
        }

        [Fact] public void 조작_절의_경공_문단이_데이터와_같다()
        {
            var d = _d.Balance.Dash;
            var sec = TestData.GddSection("조작");
            Assert.Contains($"{N(d.Duration)}초 동안 {N(d.Distance)}m 대시, 시작 {N(d.Iframes)}초 무적. 기본 {d.Charges}회 충전, 1회 회복에 {N(d.RechargeSeconds)}초", sec);
        }
    }
}
