using System;
using System.Linq;
using System.Text.RegularExpressions;
using BloodRain.Core.Build;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>오의 한 방 피해 "공력 xN" = 시작 무공 한 방 × N × 오의 위력(기획서 8장 '오의 상세').</summary>
    public class UltimatesTests
    {
        readonly GameData _d = TestData.Load();

        [Fact]
        public void 진소운_일검은_삼재검법_한_방의_30배()
        {
            var jin = _d.Character("jin_sowoon");
            var l = new Loadout(_d, jin);
            // 삼재검법 Lv1 12 × 30
            Assert.Equal(360f, Ultimates.HitDamage(_d, jin, l, might: 1f), 3);
            // 공력 +20%, 절정 오의 위력 1.25
            Assert.Equal(12f * 1.2f * 30f * 1.25f, Ultimates.HitDamage(_d, jin, l, might: 1.2f, powerMultiplier: 1.25f), 2);
        }

        [Fact]
        public void 시작_무공_레벨이_오르면_오의도_따라_오른다()
        {
            var jin = _d.Character("jin_sowoon");
            var l = new Loadout(_d, jin);
            for (int i = 0; i < 7; i++) l.Apply(new Offer(OfferKind.UpgradeWeapon, "samjae_sword"));
            var sword = _d.Weapon("samjae_sword");
            Assert.Equal(sword.DamageLv8 * 30f, Ultimates.HitDamage(_d, jin, l, 1f), 3);
            // 절기 같은 무공 배율은 무공 실행기가 넘긴다
            Assert.Equal(sword.DamageLv8 * 2f * 30f, Ultimates.HitDamage(_d, jin, l, 1f, weaponMultiplier: 2f), 3);
        }

        [Fact]
        public void 배율_0인_오의는_피해가_없다()
        {
            Assert.Equal(0f, Ultimates.HitDamage(Ultimates.Find(_d, "taiji_muryang"), 100f));
            Assert.Equal(0f, Ultimates.HitDamage(Ultimates.Find(_d, "cheonma_haeche"), 100f));
        }

        [Fact]
        public void 모르는_오의는_예외()
        {
            Assert.Throws<InvalidOperationException>(() => Ultimates.Find(_d, "no_such"));
        }

        [Fact]
        public void 모든_몸과_문파의_오의가_데이터에_있다()
        {
            foreach (var c in _d.Characters) Assert.NotNull(Ultimates.Of(_d, c));
            foreach (var s in _d.Cheonha.Sects) Ultimates.Find(_d, s.Ultimate);
        }

        [Fact]
        public void 오의_배율이_기획서_오의_상세와_같다()
        {
            var rows = TestData.GddTable(TestData.GddSection("오의 상세"));
            Assert.Equal(_d.Ultimates.Count, rows.Count);
            foreach (var r in rows)
            {
                var u = _d.Ultimates.Single(x => x.NameKo == r[0]);
                var m = Regex.Match(r[1], @"공력 x(\d+)");
                // 천설부는 강시 계열에만 피해(x8), 태극무량·천마해체는 피해 배율이 없다
                Assert.Equal(m.Success ? float.Parse(m.Groups[1].Value) : 0f, u.MightMultiplier);
            }
        }
    }
}
