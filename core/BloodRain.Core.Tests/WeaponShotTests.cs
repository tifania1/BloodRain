using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using BloodRain.Core.Data;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>기획서 9장 '날아가는 것과 넉백' 표가 weapons.json shot·knockback·fuseSeconds·seekRange 와 같은지,
    /// 웹 프로토타입이 이 값을 데이터에서 읽는지(예전에 박아 두던 숫자가 다시 들어오지 않는지).</summary>
    public class WeaponShotTests
    {
        readonly GameData _d = TestData.Load();

        static float? Cell(string cell)
        {
            if (cell == "-" || cell == "") return null;
            var m = Regex.Match(cell, @"^([0-9.]+)(m|초)?$");
            Assert.True(m.Success, $"모르는 칸: '{cell}'");
            return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        WeaponDef ByName(string name) => _d.Weapons.Single(w => w.NameKo == name);

        [Fact]
        public void 기획서_표와_데이터가_같다()
        {
            var rows = TestData.GddTable(TestData.GddSection("날아가는 것과 넉백"));
            Assert.Equal(9, rows.Count);
            foreach (var r in rows)
            {
                var w = ByName(r[0]);
                float? speed = Cell(r[1]), radius = Cell(r[2]), life = Cell(r[3]), homing = Cell(r[4]);
                if (speed == null)
                {
                    Assert.Null(w.Shot);
                    Assert.Null(radius); Assert.Null(life); Assert.Null(homing);
                }
                else
                {
                    Assert.NotNull(w.Shot);
                    Assert.Equal(speed.Value, w.Shot!.Speed, 3);
                    Assert.Equal(radius!.Value, w.Shot.Radius, 3);
                    Assert.Equal(life!.Value, w.Shot.LifeSeconds, 3);
                    Assert.Equal(homing ?? 0f, w.Shot.Homing, 3);
                }
                Assert.Equal(Cell(r[5]), w.Knockback);
            }
        }

        [Fact]
        public void 벽력탄은_칠미터_안을_노리고_영점육초_뒤_떨어진다()
        {
            var row = TestData.GddTable(TestData.GddSection("날아가는 것과 넉백")).Single(r => r[0] == "벽력탄");
            var m = Regex.Match(row[6], @"([0-9.]+)m 안의 적 자리에 ([0-9.]+)초 뒤");
            Assert.True(m.Success, row[6]);
            var w = _d.Weapons.Single(x => x.Id == "thunder_bomb");
            Assert.Equal(float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), w.SeekRange);
            Assert.Equal(float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture), w.FuseSeconds);
            Assert.All(_d.Weapons.Where(x => x.Id != "thunder_bomb"), x => { Assert.Null(x.SeekRange); Assert.Null(x.FuseSeconds); });
        }

        [Fact]
        public void 연달아_치는_간격과_한_바퀴_시간이_표와_같다()
        {
            foreach (var r in TestData.GddTable(TestData.GddSection("날아가는 것과 넉백")))
            {
                var w = ByName(r[0]);
                var gap = Regex.Match(r[6], @"([0-9.]+)초 간격");
                var sweep = Regex.Match(r[6], @"한 바퀴 ([0-9.]+)초");
                Assert.Equal(gap.Success ? float.Parse(gap.Groups[1].Value, CultureInfo.InvariantCulture) : (float?)null, w.GapSeconds);
                Assert.Equal(sweep.Success ? float.Parse(sweep.Groups[1].Value, CultureInfo.InvariantCulture) : (float?)null, w.SweepSeconds);
            }
            // 연달아 치는 것이 있는 무공(연타·휘두르기·3연 베기·2연발)에는 간격이 있어야 프로토타입이 겹쳐 치지 않는다
            foreach (var id in new[] { "samjae_sword", "nahan_fist", "palm_wind", "drunken_staff" })
                Assert.True(_d.Weapons.Single(x => x.Id == id).GapSeconds > 0, id);
        }

        [Fact]
        public void 처음부터_쓰는_무공은_모두_넉백이_있다()
        {
            // 프로토타입이 def.knockback 을 바로 읽으니 빠지면 넉백이 사라진다(undefined → 0).
            foreach (var w in _d.Weapons.Where(x => x.Unlock == "start"))
                Assert.True(w.Knockback.HasValue, $"{w.NameKo} 넉백 없음");
        }

        [Fact]
        public void 날아가는_것을_쓰는_동작에는_shot_이_있다()
        {
            var shooters = new HashSet<string> { "homing_dagger", "slow_projectile", "random_rapid" };
            foreach (var w in _d.Weapons)
                Assert.Equal(shooters.Contains(w.Behavior), w.Shot != null);
        }

        [Fact]
        public void 날아가는_거리는_사거리와_어긋나지_않는다()
        {
            // 비도는 사거리 1.4배 안의 적을 노리는데 14.4m 날아간다(11.2m 안). 섬광지는 7m 안을 노리고 10.8m.
            foreach (var w in _d.Weapons.Where(x => x.Shot != null && x.Behavior != "slow_projectile"))
                Assert.True(w.Shot!.Reach >= w.Range, $"{w.NameKo} {w.Shot.Reach}m < 사거리 {w.Range}m");
            Assert.Equal(14.4f, _d.Weapons.Single(x => x.Id == "flying_dagger").Shot!.Reach, 3);
        }

        [Fact]
        public void 프로토타입이_숫자를_박아_두지_않는다()
        {
            var html = TestData.ReadRepoFile("web", "prototype", "template.html");
            foreach (var old in new[] { "sp = 12 * S.projSpeed", "sp = 5 * S.projSpeed", "sp = 18 * S.projSpeed", "nearestEnemies(p.x, p.y, 20, 7)",
                                        "dy / d, 7);", "ca, sa, 3);", "dy / d, 4);", "(d || 1), 2.5);",
                                        "delay: s * 0.12", "delay: i * 0.08", "delay: i * 0.15", "delay: s * 0.18", "0.36 * sweeps" })
                Assert.DoesNotContain(old, html);
            foreach (var used in new[] { "def.shot", "def.knockback", "def.fuseSeconds", "def.seekRange" })
                Assert.Contains(used, html);
        }
    }
}
