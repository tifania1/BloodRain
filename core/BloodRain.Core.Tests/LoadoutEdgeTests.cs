using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using BloodRain.Core.Progression;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>Loadout 과 오의 게이지의 가장자리 경우. BalanceTests·RunProgressTests 가 안 보는 쪽만 모았다.</summary>
    public class LoadoutEdgeTests
    {
        readonly GameData _d = Load();
        ISet<string> StartUnlocked => new HashSet<string>(_d.Weapons.Where(w => w.Unlock == "start").Select(w => w.Id));
        ISet<string> AllUnlocked => new HashSet<string>(_d.Weapons.Select(w => w.Id));

        Loadout Jin() => new Loadout(_d, _d.Character("jin_sowoon"));

        // ---- 칸과 단계 ----

        [Fact] public void 칸이_찬_뒤_새_무공을_넣으면_터진다()
        {
            var l = Jin();
            foreach (var w in _d.Weapons.Where(w => w.Id != "samjae_sword").Take(_d.Balance.Slots.Weapons - 1))
                l.Apply(new Offer(OfferKind.NewWeapon, w.Id));
            Assert.Equal(_d.Balance.Slots.Weapons, l.Weapons.Count);
            Assert.Throws<InvalidOperationException>(() => l.Apply(new Offer(OfferKind.NewWeapon, "taiji_sword")));
        }

        [Fact] public void 칸이_찬_뒤_새_심법을_넣으면_터지고_초절정_칸을_받으면_들어간다()
        {
            var l = Jin();
            var ps = _d.Passives.Select(p => p.Id).ToList();
            for (int i = 0; i < _d.Balance.Slots.Passives; i++) l.Apply(new Offer(OfferKind.NewPassive, ps[i]));
            var extra = new Offer(OfferKind.NewPassive, ps[_d.Balance.Slots.Passives]);
            Assert.Throws<InvalidOperationException>(() => l.Apply(extra));
            Assert.DoesNotContain(l.Candidates(AllUnlocked), o => o.Kind == OfferKind.NewPassive);

            l.AddExtraSlot(weapon: false);
            Assert.Contains(l.Candidates(AllUnlocked), o => o.Kind == OfferKind.NewPassive);
            l.Apply(extra);
            Assert.Equal(_d.Balance.Slots.Passives + 1, l.Passives.Count);
        }

        [Fact] public void 최고_단계를_넘겨_올리면_터지고_선택지에서_빠진다()
        {
            var l = TestData.Build("jin_sowoon", ("samjae_sword", _d.Balance.Levels.WeaponMax), ("cheongmyeong", _d.Balance.Levels.PassiveMax));
            Assert.Throws<InvalidOperationException>(() => l.Apply(new Offer(OfferKind.UpgradeWeapon, "samjae_sword")));
            Assert.Throws<InvalidOperationException>(() => l.Apply(new Offer(OfferKind.UpgradePassive, "cheongmyeong")));
            var c = l.Candidates(StartUnlocked);
            Assert.DoesNotContain(c, o => o.Id == "samjae_sword");
            Assert.DoesNotContain(c, o => o.Id == "cheongmyeong");
        }

        [Fact] public void 은자_음식은_아무것도_바꾸지_않는다()
        {
            var l = Jin();
            float before = l.Gauge.Value;
            l.Apply(new Offer(OfferKind.Fallback, "silver_or_food"));
            Assert.Single(l.Weapons);
            Assert.Empty(l.Passives);
            Assert.Equal(before, l.Gauge.Value);
        }

        // ---- 파기 ----

        [Fact] public void 파기한_무공과_심법은_새로_나오지_않는다()
        {
            var l = Jin();
            l.Banish("nahan_fist");
            l.Banish("geumgang");
            var c = l.Candidates(StartUnlocked);
            Assert.DoesNotContain(c, o => o.Id == "nahan_fist");
            Assert.DoesNotContain(c, o => o.Id == "geumgang");
            Assert.Contains(c, o => o.Id == "flying_dagger");
            for (int seed = 0; seed < 50; seed++)
                Assert.DoesNotContain(l.RollLevelUp(new Random(seed), 100, StartUnlocked), o => o.Id == "nahan_fist" || o.Id == "geumgang");
        }

        [Fact] public void 심법은_잠금과_상관없이_나온다()
        {
            // 심법에는 unlock 이 없다. 무공 잠금 목록이 비어도 심법 후보는 다 나온다.
            var c = Jin().Candidates(new HashSet<string>());
            Assert.Equal(_d.Passives.Count, c.Count(o => o.Kind == OfferKind.NewPassive));
            Assert.DoesNotContain(c, o => o.Kind == OfferKind.NewWeapon);
        }

        // ---- 레벨업 추첨 ----

        [Fact] public void 한_번_추첨에_같은_선택지는_두_번_나오지_않는다()
        {
            var l = Jin();
            for (int seed = 0; seed < 200; seed++)
            {
                var offers = l.RollLevelUp(new Random(seed), 100, StartUnlocked);
                Assert.Equal(offers.Count, offers.Select(o => o.ToString()).Distinct().Count());
                Assert.InRange(offers.Count, _d.Balance.LevelUp.BaseChoices, _d.Balance.LevelUp.MaxChoices);
            }
        }

        [Fact] public void 같은_씨앗이면_같은_선택지()
        {
            var a = Jin().RollLevelUp(new Random(7), 25, StartUnlocked).Select(o => o.ToString());
            var b = Jin().RollLevelUp(new Random(7), 25, StartUnlocked).Select(o => o.ToString());
            Assert.Equal(a, b);
        }

        [Fact] public void 후보가_선택지_수보다_적으면_있는_만큼만()
        {
            // 무공 하나만 남기고 나머지는 다 최고 단계·칸 참: 후보는 삼재검법 강화 하나뿐
            var items = new List<(string, int)> { ("samjae_sword", _d.Balance.Levels.WeaponMax - 1) };
            foreach (var w in _d.Weapons.Where(w => w.Id != "samjae_sword").Take(_d.Balance.Slots.Weapons - 1))
                items.Add((w.Id, _d.Balance.Levels.WeaponMax));
            foreach (var p in _d.Passives.Take(_d.Balance.Slots.Passives))
                items.Add((p.Id, _d.Balance.Levels.PassiveMax));
            var l = TestData.Build("jin_sowoon", items.ToArray());

            var offers = l.RollLevelUp(new Random(1), 100, AllUnlocked);
            Assert.Single(offers);
            Assert.Equal(new Offer(OfferKind.UpgradeWeapon, "samjae_sword").ToString(), offers[0].ToString());
        }

        [Fact] public void 절기가_된_무공은_강화_선택지에서_빠진다()
        {
            var l = TestData.Build("jin_sowoon", ("samjae_sword", _d.Balance.Levels.WeaponMax), ("cheongmyeong", _d.Balance.Evolution.PairPassiveMinLevel));
            var evo = l.AwakenOnChest(35)!;
            l.Evolve(evo);
            Assert.Contains("samjae_sword", l.Evolved);
            Assert.DoesNotContain(l.Candidates(StartUnlocked), o => o.Id == "samjae_sword");
            Assert.Empty(l.AtThreshold());
        }

        // ---- 그릇 ----

        [Fact] public void 그릇은_경지_사이에서_줄지_않는다()
        {
            var l = Jin();
            int prev = 0;
            for (int lv = 1; lv <= 120; lv++)
            {
                int v = l.Vessels(lv);
                Assert.True(v >= prev, $"Lv{lv} 그릇 {v} < Lv{lv - 1} 그릇 {prev}");
                prev = v;
            }
            Assert.Equal(_d.Balance.Evolution.Vessels.Values.Max(), prev);
        }

        [Fact] public void 같은_레벨_안에서는_돌파_각성이_없다()
        {
            var l = TestData.Build("jin_sowoon", ("samjae_sword", _d.Balance.Levels.WeaponMax), ("cheongmyeong", _d.Balance.Evolution.PairPassiveMinLevel));
            Assert.Empty(l.AwakenOnBreakthrough(35, 35));
            Assert.Single(l.AwakenOnBreakthrough(1, 90));           // 한 번에 여러 경지를 넘어도 후보는 문턱 목록 그대로
        }

        // ---- 오의 게이지 ----

        UltimateCharge Charge(float bonus = 0f, float start = 0f) =>
            new UltimateCharge(_d.Balance.UltimateCharge, _d.Balance.Yinyang.Harmony, bonus, start);

        [Fact] public void 시작값은_0과_최대_사이로_자른다()
        {
            Assert.Equal(0f, Charge(start: -30f).Value);
            Assert.Equal(_d.Balance.UltimateCharge.Max, Charge(start: 999f).Value);
            Assert.True(Charge(start: 999f).IsFull);
        }

        [Fact] public void 덜_찼으면_써도_그대로다()
        {
            var c = Charge(start: 60f);
            Assert.False(c.TryUse());
            Assert.Equal(60f, c.Value);
            Assert.Equal(0.6f, c.Fraction, 3);
        }

        [Fact] public void 음수를_더해도_0_아래로_내려가지_않는다()
        {
            var c = Charge(start: 10f);
            c.AddFlat(-50f);
            Assert.Equal(0f, c.Value);
        }

        [Fact] public void 여러_마리_처치는_한_마리씩과_같다()
        {
            var a = Charge(0.2f);
            var b = Charge(0.2f);
            a.OnKill(YinYangBand.Harmony, 30);
            for (int i = 0; i < 30; i++) b.OnKill(YinYangBand.Harmony);
            Assert.Equal(b.Value, a.Value, 3);
        }

        [Fact] public void 충전_보너스는_도중에_바꿔도_다음_충전부터_든다()
        {
            var c = Charge();
            c.OnHit(YinYangBand.Harmony);
            float first = c.Value;
            c.ChargeBonus = 1f;
            c.OnHit(YinYangBand.Harmony);
            Assert.Equal(first * 3f, c.Value, 3);               // 두 번째는 두 배
        }
    }
}
