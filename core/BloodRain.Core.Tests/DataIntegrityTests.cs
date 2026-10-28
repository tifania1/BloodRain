using System.Linq;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>데이터 파일끼리 서로 어긋나지 않는지. 무공·심법·절기는 1:1:1로 짝지어진다.</summary>
    public class DataIntegrityTests
    {
        readonly Data.GameData _d = TestData.Load();

        [Fact] public void 무공_심법_절기는_각각_16종이다()
        {
            Assert.Equal(16, _d.Weapons.Count);
            Assert.Equal(16, _d.Passives.Count);
            Assert.Equal(16, _d.Evolutions.Count);
        }

        [Fact] public void 아이디는_겹치지_않는다()
        {
            Assert.Equal(_d.Weapons.Count, _d.Weapons.Select(w => w.Id).Distinct().Count());
            Assert.Equal(_d.Passives.Count, _d.Passives.Select(p => p.Id).Distinct().Count());
            Assert.Equal(_d.Evolutions.Count, _d.Evolutions.Select(e => e.Id).Distinct().Count());
            Assert.Equal(_d.Enemies.Count, _d.Enemies.Select(e => e.Id).Distinct().Count());
        }

        [Fact] public void 무공과_심법의_짝이_양쪽에서_일치한다()
        {
            foreach (var w in _d.Weapons)
            {
                var p = _d.Passives.SingleOrDefault(x => x.Id == w.PairPassive);
                Assert.True(p != null, $"{w.NameKo}의 짝 심법 {w.PairPassive}가 없음");
                Assert.Equal(w.Id, p!.PairWeapon);
            }
        }

        [Fact] public void 절기마다_무공과_심법이_실제_짝이다()
        {
            foreach (var e in _d.Evolutions)
            {
                var w = _d.Weapons.Single(x => x.Id == e.Weapon);
                Assert.Equal(w.PairPassive, e.Passive);
            }
            Assert.Equal(16, _d.Evolutions.Select(e => e.Weapon).Distinct().Count());
        }

        [Fact] public void 처음부터_쓸_수_있는_무공은_8종이다()
        {
            Assert.Equal(8, _d.Weapons.Count(w => w.Unlock == "start"));
        }

        [Fact] public void 캐릭터_8명의_시작_무공과_오의가_존재한다()
        {
            Assert.Equal(8, _d.Characters.Count);
            foreach (var c in _d.Characters)
            {
                Assert.Contains(_d.Weapons, w => w.Id == c.StartWeapon);
                Assert.Contains(_d.Ultimates, u => u.Id == c.Ultimate);
            }
        }

        [Fact] public void 무공_피해는_Lv8이_Lv1보다_크다()
        {
            foreach (var w in _d.Weapons) Assert.True(w.DamageLv8 > w.DamageLv1, w.NameKo);
        }

        [Fact] public void 음양_값은_세_가지_중_하나다()
        {
            var ok = new[] { "yang", "yin", "neutral" };
            foreach (var w in _d.Weapons) Assert.Contains(w.Yinyang, ok);
            foreach (var p in _d.Passives) Assert.Contains(p.Yinyang, ok);
        }
    }
}
