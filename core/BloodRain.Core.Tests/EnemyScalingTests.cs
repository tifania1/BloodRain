using System;
using System.Linq;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Run;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>적 기혈·피해 식(기획서 11장)을 장 이름으로 셈하든 혈운 농도로 셈하든 같은 값이 나온다.</summary>
    public class EnemyScalingTests
    {
        readonly GameData _d = TestData.Load();
        EnemyScaling S => new EnemyScaling(_d.Balance.EnemyScaling);

        [Fact] public void 장이_붙은_지역은_모두_장_배율이_그_땅_혈운_농도와_같다_종장_포함()
        {
            foreach (var r in _d.Cheonha.Regions.Where(r => r.Chapter != null))
                Assert.Equal(r.Density, S.ChapterMultiplier(r.Chapter!), 3);
            Assert.Equal(5.0f, S.ChapterMultiplier("final"), 3);
        }

        [Fact] public void 모르는_장은_1배()
        {
            Assert.Equal(1f, S.ChapterMultiplier("외전"));
        }

        [Theory]
        [InlineData("prologue", 0)]
        [InlineData("chapter3", 12.5f)]
        [InlineData("chapter7", 30)]
        [InlineData("final", 31)]
        public void 장_이름과_농도_셈은_같은_값(string chapter, float minutes)
        {
            var e = _d.Enemy("donggap");
            float m = S.ChapterMultiplier(chapter);
            Assert.Equal(S.Health(e, minutes, chapter), S.HealthAt(e.Health, minutes, m), 3);
            Assert.Equal(S.Damage(10, minutes, chapter), S.DamageAt(10, minutes, m), 3);
        }

        [Fact] public void 피해는_농도의_제곱근만큼_기혈은_농도만큼()
        {
            Assert.Equal(4f * S.HealthAt(10, 5, 1f), S.HealthAt(10, 5, 4f), 3);
            Assert.Equal(2f * S.DamageAt(10, 5, 1f), S.DamageAt(10, 5, 4f), 3);
        }

        [Fact] public void 농도가_0_아래면_0으로_본다()
        {
            Assert.Equal(0f, S.HealthAt(10, 5, -1f));
            Assert.Equal(0f, S.DamageAt(10, 5, -1f));
        }

        [Fact] public void 무공_피해는_Lv1과_Lv8_사이를_곧게_잇는다()
        {
            var w = _d.Weapons.First(x => x.DamageLv8 > x.DamageLv1);
            Assert.Equal(w.DamageLv1, WeaponMath.DamageAt(w, 0));
            Assert.Equal(w.DamageLv8, WeaponMath.DamageAt(w, 9));
            float prev = w.DamageLv1;
            for (int lv = 2; lv <= 8; lv++)
            {
                float d = WeaponMath.DamageAt(w, lv);
                Assert.True(d > prev);
                prev = d;
            }
            Assert.Equal((w.DamageLv1 + w.DamageLv8) / 2f, (WeaponMath.DamageAt(w, 4) + WeaponMath.DamageAt(w, 5)) / 2f, 3);
        }

        [Fact] public void 한_생의_Density는_서_있는_땅_농도()
        {
            var soul = new Soul(_d);
            soul.EndLife(soul.BeginLife(Rebirth.Prologue(_d)).Finish(LifeEndWay.Fallen, 0));
            var c = _d.Cheonha;
            var cand = new RebirthCandidate(c.Sect("tang"), c.Region(c.Sect("tang").Region), Array.Empty<BirthTraitDef>());
            var life = new Life(soul, Rebirth.Body(c, cand), new Random(1));
            Assert.Equal(c.Region("danggata").Density, life.Density, 3);
        }
    }
}
