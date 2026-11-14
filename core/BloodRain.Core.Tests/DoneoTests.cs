using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using Xunit;

namespace BloodRain.Core.Tests
{
    // 기획서 20장 '돈오(頓悟)'(GU-01): 연환은 조건을 갖춰도 바로 켜지지 않고, 금빛 돈오 카드를 집어야 켜진다. 둘 다 Lv8 이면 대성.
    public class DoneoTests
    {
        readonly GameData _d = TestData.Load();

        /// <summary>돈오 카드를 집지 않은 채 (id, 레벨)대로 올린 로드아웃(TestData.Build 는 다 집는다).</summary>
        Loadout Raw(params (string id, int level)[] items)
        {
            var l = new Loadout(_d, _d.Character("jin_sowoon"));
            foreach (var (id, level) in items)
            {
                bool weapon = _d.Weapons.Any(w => w.Id == id);
                var owned = weapon ? l.Weapons : l.Passives;
                int cur = owned.TryGetValue(id, out var lv) ? lv : 0;
                if (cur == 0) { l.Apply(new Offer(weapon ? OfferKind.NewWeapon : OfferKind.NewPassive, id)); cur = 1; }
                for (; cur < level; cur++) l.Apply(new Offer(weapon ? OfferKind.UpgradeWeapon : OfferKind.UpgradePassive, id));
            }
            return l;
        }

        [Fact]
        public void 값은_기획서_추천값_대성_Lv8_1_5배_짝_가중치_2배()
        {
            var r = _d.Balance.LevelUp;
            Assert.Equal(8, r.DoneoGrandLevel);
            Assert.Equal(1.5f, r.DoneoGrandMultiplier, 4);
            Assert.Equal(2f, r.KnownLinkWeight, 4);
            var gdd = TestData.Gdd;
            Assert.Contains("**돈오(頓悟)**", gdd);
            Assert.Contains("`levelUp.doneoGrandLevel`", gdd);
            Assert.Contains("효과 값 1.5배", gdd);
            Assert.Contains("가중치 2배", gdd);
        }

        [Fact]
        public void 조건을_갖춰도_돈오_카드를_집기_전엔_꺼져_있다()
        {
            var l = Raw(("samjae_sword", 4), ("changcheon_qi", 4));
            var before = Synergy.Compute(_d, l);
            Assert.False(before.HasLink("ssanggeom"));
            Assert.Contains(before.PendingLinks, x => x.Id == "ssanggeom");
            Assert.Null(before.LinkParam("ssanggeom", "damageBonus"));

            var cards = l.PendingDoneo();
            var card = Assert.Single(cards, c => c.Link.Id == "ssanggeom");
            Assert.False(card.Grand);
            l.TakeDoneo(card);

            var after = Synergy.Compute(_d, l);
            Assert.True(after.HasLink("ssanggeom"));
            Assert.Empty(after.PendingLinks);
            Assert.Equal(0.6f, after.LinkParam("ssanggeom", "damageBonus")!.Value, 4);
            Assert.DoesNotContain(l.PendingDoneo(), c => c.Link.Id == "ssanggeom");
        }

        [Fact]
        public void 레벨이_모자라면_카드가_없고_억지로_집으면_예외()
        {
            var l = Raw(("samjae_sword", 4), ("changcheon_qi", 3));
            Assert.Empty(l.PendingDoneo());
            var link = _d.Synergies.Links.First(x => x.Id == "ssanggeom");
            Assert.Throws<InvalidOperationException>(() => l.TakeDoneo(new DoneoCard(link, false)));
            Assert.Throws<InvalidOperationException>(() => l.TakeDoneo(new DoneoCard(link, true)));
        }

        [Fact]
        public void 둘_다_Lv8_이면_대성_카드가_뜨고_효과_값이_1_5배()
        {
            var l = Raw(("samjae_sword", 8), ("changcheon_qi", 8));
            // 처음엔 깨침 카드 하나만(대성은 깨친 다음 차례)
            var first = l.PendingDoneo().Where(c => c.Link.Id == "ssanggeom").ToList();
            Assert.False(Assert.Single(first).Grand);
            l.TakeDoneo(first[0]);
            var grand = Assert.Single(l.PendingDoneo(), c => c.Link.Id == "ssanggeom");
            Assert.True(grand.Grand);
            l.TakeDoneo(grand);
            Assert.Empty(l.PendingDoneo().Where(c => c.Link.Id == "ssanggeom"));

            var syn = Synergy.Compute(_d, l);
            Assert.True(syn.IsGrand("ssanggeom"));
            Assert.Equal(0.9f, syn.LinkParam("ssanggeom", "damageBonus")!.Value, 4);
        }

        [Fact]
        public void 잠긴_비급_연환은_돈오_카드로_뜨지_않는다()
        {
            var secret = _d.Synergies.Links.First(l => l.IsSecret);
            var l = Raw((secret.A, secret.MinLevel), (secret.B, secret.MinLevel));
            Assert.DoesNotContain(l.PendingDoneo(new string[0]), c => c.Link.Id == secret.Id);
            Assert.Contains(l.PendingDoneo(new[] { secret.ManualId }), c => c.Link.Id == secret.Id);
            Assert.Contains(l.PendingDoneo(), c => c.Link.Id == secret.Id);   // null 이면 다 열림
        }

        [Fact]
        public void 카드_안내는_돈오로_깨칠_수_있으면_Completes()
        {
            var l = Raw(("samjae_sword", 4), ("changcheon_qi", 3));
            var pv = Synergy.Preview(_d, l, new Offer(OfferKind.UpgradeWeapon, "changcheon_qi"));
            Assert.Contains(pv.CompletedLinks, x => x.Id == "ssanggeom");
            // 이미 조건을 갖춰 돈오를 기다리는 연환은 다시 안내하지 않는다
            l.Apply(new Offer(OfferKind.UpgradeWeapon, "changcheon_qi"));
            var pv2 = Synergy.Preview(_d, l, new Offer(OfferKind.UpgradeWeapon, "changcheon_qi"));
            Assert.DoesNotContain(pv2.Links, h => h.Link.Id == "ssanggeom");
        }

        [Fact]
        public void 도감에_오른_연환의_짝_무공_카드는_더_잘_뜬다()
        {
            var l = Raw();   // 삼재검법 Lv1
            var unlocked = new HashSet<string>(_d.Weapons.Select(w => w.Id));
            var known = new[] { "ssanggeom" };
            Assert.Equal(2.0, l.KnownLinkWeight(new Offer(OfferKind.NewWeapon, "changcheon_qi"), known));
            Assert.Equal(1.0, l.KnownLinkWeight(new Offer(OfferKind.NewWeapon, "changcheon_qi"), null));
            Assert.Equal(1.0, l.KnownLinkWeight(new Offer(OfferKind.NewPassive, "cheongmyeong"), known));

            int plain = 0, weighted = 0;
            for (int seed = 0; seed < 2000; seed++)
            {
                if (Raw().RollLevelUp(new Random(seed), 0f, unlocked).Any(o => o.Id == "changcheon_qi")) plain++;
                if (Raw().RollLevelUp(new Random(seed), 0f, unlocked, known).Any(o => o.Id == "changcheon_qi")) weighted++;
            }
            Assert.True(weighted > plain * 1.4, $"가중치 2배인데 {plain} → {weighted}");
        }

        [Fact]
        public void 가중치가_붙는_카드가_없으면_난수_순서가_그대로다()
        {
            var unlocked = new HashSet<string>(_d.Weapons.Select(w => w.Id));
            for (int seed = 0; seed < 50; seed++)
            {
                var a = Raw().RollLevelUp(new Random(seed), 10f, unlocked);
                var b = Raw().RollLevelUp(new Random(seed), 10f, unlocked, new string[0]);
                Assert.Equal(a.Select(o => o.ToString()), b.Select(o => o.ToString()));
            }
        }
    }
}
