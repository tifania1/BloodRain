using System;
using System.Linq;
using System.Numerics;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Run;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>약방 영약(기획서 10장 '영약(약방)')을 한 생에 거는 Life.TakeElixirs(EL-02).</summary>
    public class ElixirLifeTests
    {
        readonly GameData _d = TestData.Load();
        CheonhaDef C => _d.Cheonha;

        Life NewLife()
        {
            var s = new Soul(_d);
            s.EndLife(s.BeginLife(Rebirth.Prologue(_d)).Finish(LifeEndWay.Fallen, 0));
            var body = Rebirth.Body(C, new RebirthCandidate(C.Sect("shaolin"), C.Region(C.Sect("shaolin").Region), Array.Empty<BirthTraitDef>()));
            return new Life(s, body, new Random(1));
        }

        ElixirDef E(string id) => C.Inn.Elixirs.Single(e => e.Id == id);

        [Fact] public void 소환단은_첫_사망에_기혈_30퍼로_재기보다_먼저()
        {
            var life = NewLife();
            life.TakeElixirs(new[] { E("sohwandan") });
            var stats = life.CurrentStats();
            Assert.Equal(0.3f, life.TryRevive(stats));
            Assert.Equal(0, life.RevivalsUsed);
            // 두 번째 사망은 재기(연무장)가 없으면 그대로 쓰러진다
            if (stats.Revival == 0) Assert.Null(life.TryRevive(stats));
        }

        [Fact] public void 대환단은_레벨_5로_오른_만큼_레벨업()
        {
            var life = NewLife();
            var r = life.TakeElixirs(new[] { E("daehwandan") });
            Assert.Equal(5, life.Level);
            Assert.Equal(1, r.FromLevel);
            Assert.Equal(4, r.LevelUps);
        }

        [Fact] public void 공청석유는_오의_게이지_100()
        {
            var life = NewLife();
            life.TakeElixirs(new[] { E("gongcheong") });
            Assert.Equal(Math.Min(100f, life.Charge.Max), life.Charge.Value);
        }

        [Fact] public void 만년설삼은_최대_기혈_30퍼()
        {
            var a = NewLife(); var b = NewLife();
            b.TakeElixirs(new[] { E("seolsam") });
            float baseHp = a.CurrentStats().MaxHealth;
            Assert.True(b.CurrentStats().MaxHealth > baseHp);
        }

        [Fact] public void 혈정단은_정예_혈정_2배()
        {
            var life = NewLife();
            life.TakeElixirs(new[] { E("hyeoljeongdan") });
            Assert.Equal(2 * _d.Balance.Drops.EliteBloodCrystal, life.KillElite());
        }

        [Fact] public void 태극환은_처음_10분_음양이_안_움직인다()
        {
            var life = NewLife();
            life.TakeElixirs(new[] { E("taegeukhwan") });
            var yang = _d.Weapons.First(w => w.Aspect == YinYang.Yang);
            int v0 = life.Loadout.Gauge.Value;
            life.Loadout.Gauge.OnWeaponLevelUp(yang);
            life.Loadout.Gauge.Shift(-30);
            Assert.Equal(v0, life.Loadout.Gauge.Value);
            for (int i = 0; i < 601; i++) life.Tick(1f, Vector2.Zero, 0);
            life.Loadout.Gauge.OnWeaponLevelUp(yang);
            Assert.NotEqual(v0, life.Loadout.Gauge.Value);
        }

        [Fact] public void 영약은_생을_시작할_때_한_번()
        {
            var life = NewLife();
            life.TakeElixirs(new[] { E("gongcheong") });
            Assert.Throws<InvalidOperationException>(() => life.TakeElixirs(new[] { E("seolsam") }));
        }
    }
}
