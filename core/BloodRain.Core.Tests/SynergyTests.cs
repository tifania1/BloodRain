using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using Newtonsoft.Json.Linq;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>연환·진전·기본의 극 계산과 레벨업 카드 미리보기(S-01, 기획서 20장).</summary>
    public class SynergyTests
    {
        readonly GameData _d = TestData.Load();

        SynergyState S(Loadout l, Dictionary<string, int>? extra = null) => Synergy.Compute(_d, l, extra);
        static Loadout L(string character, params (string, int)[] items) => TestData.Build(character, items);

        // ---- 연환 ----

        [Fact] public void 처음에는_켜진_연환도_진전도_없다()
        {
            var s = S(L("jin_sowoon"));
            Assert.Empty(s.Links);
            Assert.Empty(s.ActiveSchools);
            Assert.False(s.CommonActive);
            Assert.Equal(9, s.Schools.Count);
        }

        [Fact] public void 연환은_두_무공이_모두_Lv4가_되어야_켜진다()
        {
            Assert.False(S(L("jin_sowoon", ("samjae_sword", 4), ("changcheon_qi", 3))).HasLink("ssanggeom"));
            Assert.False(S(L("jin_sowoon", ("samjae_sword", 3), ("changcheon_qi", 8))).HasLink("ssanggeom"));
            var s = S(L("jin_sowoon", ("samjae_sword", 4), ("changcheon_qi", 4)));
            Assert.Equal(new[] { "ssanggeom" }, s.Links.Select(l => l.Id));
        }

        [Fact] public void 연환은_여럿이_함께_켜지고_데이터_순서로_나온다()
        {
            var s = S(L("dang_yeonhwa", ("flying_dagger", 4), ("poison_mist", 4), ("flash_finger", 4)));
            Assert.Equal(new[] { "dokbi", "amgi" }, s.Links.Select(l => l.Id));
        }

        [Fact] public void 절기가_된_무공은_최고_단계로_치고_연환도_그대로다()
        {
            var l = L("jin_sowoon", ("samjae_sword", 4), ("changcheon_qi", 8), ("noejeong", 3));
            l.Evolve(_d.Evolutions.First(e => e.Weapon == "changcheon_qi"));
            Assert.Equal(_d.Balance.Levels.WeaponMax, BuildLevels.Of(_d, l).Weapons["changcheon_qi"]);
            Assert.True(S(l).HasLink("ssanggeom"));
        }

        // ---- 진전 ----

        [Fact] public void 진전은_같은_문파_무공과_심법을_함께_센다()
        {
            var sh = S(L("hyegong", ("geumgang", 1))).School("shaolin");
            Assert.Equal(2, sh.Members);
            Assert.Equal(2, sh.Count);
            Assert.Equal(2, sh.TierCount);
            Assert.Equal("호신 +2", sh.Tier!.Desc);
            Assert.Equal(3, sh.Next!.Count);
            Assert.Equal(1, sh.ToNext);
        }

        [Fact] public void 심법_레벨은_진전_개수와_상관없다()
        {
            Assert.Equal(2, S(L("hyegong", ("geumgang", 5))).School("shaolin").Count);
        }

        [Fact] public void 하나만_모은_문파는_단계가_없고_다음은_2개()
        {
            var sh = S(L("hyegong")).School("shaolin");
            Assert.Null(sh.Tier);
            Assert.Equal(0, sh.TierCount);
            Assert.Equal(2, sh.Next!.Count);
            Assert.Equal(1, sh.ToNext);
            Assert.Empty(S(L("hyegong")).ActiveSchools);
        }

        [Fact] public void 당가는_2_3_5_단계이고_4개에서는_5까지_하나_남는다()
        {
            var two = S(L("dang_yeonhwa", ("cheonsu", 1))).School("tang");
            Assert.Equal((2, 3, 1), (two.TierCount, two.Next!.Count, two.ToNext));
            var three = S(L("dang_yeonhwa", ("cheonsu", 1), ("poison_mist", 1))).School("tang");
            Assert.Equal((3, 5, 2), (three.TierCount, three.Next!.Count, three.ToNext));
            var four = S(L("dang_yeonhwa", ("cheonsu", 1), ("poison_mist", 1), ("mandok", 1))).School("tang");
            Assert.Equal((3, 5, 1), (four.TierCount, four.Next!.Count, four.ToNext));
            var five = S(L("dang_yeonhwa", ("cheonsu", 1), ("poison_mist", 1), ("mandok", 1), ("thunder_bomb", 1))).School("tang");
            Assert.Equal(5, five.TierCount);
            Assert.Null(five.Next);
            Assert.Equal(0, five.ToNext);
        }

        [Fact] public void 고수의_유해처럼_바깥에서_더한_수도_센다()
        {
            var sh = S(L("hyegong"), new Dictionary<string, int> { ["shaolin"] = 1 }).School("shaolin");
            Assert.Equal(1, sh.Members);
            Assert.Equal(1, sh.Bonus);
            Assert.Equal(2, sh.TierCount);
        }

        [Fact] public void 개수가_인원을_넘어도_마지막_단계에서_멈춘다()
        {
            var dm = S(L("baekya", ("hyeolhae", 1)), new Dictionary<string, int> { ["demonic"] = 2 }).School("demonic");
            Assert.Equal(4, dm.Count);
            Assert.Equal(2, dm.TierCount);
            Assert.Null(dm.Next);
            Assert.Equal(0, dm.ToNext);
        }

        [Fact] public void 없는_문파에_보너스를_주면_오류()
        {
            Assert.Throws<ArgumentException>(() => S(L("hyegong"), new Dictionary<string, int> { ["emei"] = 1 }));
        }

        // ---- 기본의 극 ----

        [Fact] public void 기본의_극은_삼재검법_Lv8에서_가장_많이_모은_문파에_하나를_더한다()
        {
            var seven = S(L("jin_sowoon", ("samjae_sword", 7), ("nahan_fist", 1), ("geumgang", 1), ("flying_dagger", 1)));
            Assert.False(seven.CommonActive);
            Assert.Equal(2, seven.TierOf("shaolin"));

            var eight = S(L("jin_sowoon", ("samjae_sword", 8), ("nahan_fist", 1), ("geumgang", 1), ("flying_dagger", 1)));
            Assert.True(eight.CommonActive);
            Assert.Equal("shaolin", eight.CommonSchool);
            Assert.Equal(3, eight.School("shaolin").Count);
            Assert.Equal(3, eight.TierOf("shaolin"));
            Assert.Equal(1, eight.School("tang").Count);
        }

        [Fact] public void 기본의_극은_절기가_된_삼재검법에도_켜진다()
        {
            var l = L("jin_sowoon", ("samjae_sword", 8), ("cheongmyeong", 3), ("nahan_fist", 1), ("geumgang", 1));
            l.Evolve(_d.Evolutions.First(e => e.Weapon == "samjae_sword"));
            Assert.Equal("shaolin", S(l).CommonSchool);
        }

        [Fact] public void 기본의_극은_동점이면_하나_더해_새_단계에_닿는_문파로()
        {
            // 당가 3(4개째는 단계 없음)과 수로채 3(4개째가 단계). 데이터 순서는 당가가 먼저다.
            var s = S(L("jin_sowoon", ("samjae_sword", 8),
                ("flying_dagger", 1), ("poison_mist", 1), ("cheonsu", 1),
                ("chain_sickle", 1), ("pear_spear", 1), ("suryong", 1)));
            Assert.Equal("waterfort", s.CommonSchool);
            Assert.Equal(4, s.TierOf("waterfort"));
            Assert.Equal(3, s.TierOf("tang"));
        }

        [Fact] public void 기본의_극은_그래도_동점이면_데이터_순서()
        {
            var s = S(L("jin_sowoon", ("samjae_sword", 8), ("nahan_fist", 1), ("geumgang", 1), ("flying_dagger", 1), ("cheonsu", 1)));
            Assert.Equal("shaolin", s.CommonSchool);
            Assert.Equal(3, s.TierOf("shaolin"));
            Assert.Equal(2, s.TierOf("tang"));
        }

        [Fact] public void 기본의_극은_마지막_단계까지_찬_문파를_건너뛴다()
        {
            // 무당 3/3은 더 채울 것이 없으니 소림 2에 더한다
            var s = S(L("jin_sowoon", ("samjae_sword", 8), ("taiji_sword", 1), ("yangui", 1), ("unryong", 1), ("nahan_fist", 1), ("geumgang", 1)));
            Assert.Equal("shaolin", s.CommonSchool);
            Assert.Equal(3, s.School("wudang").Count);
            Assert.Equal(0, s.School("wudang").Bonus);
            Assert.Equal(3, s.TierOf("shaolin"));
        }

        [Fact] public void 문파를_하나도_모으지_않았으면_기본의_극은_갈_곳이_없다()
        {
            var s = S(L("jin_sowoon", ("samjae_sword", 8)));
            Assert.True(s.CommonActive);
            Assert.Null(s.CommonSchool);
            Assert.All(s.Schools, sc => Assert.Equal(0, sc.Count));
        }

        [Fact] public void 기본의_극을_고를_때_바깥_보너스도_센다()
        {
            var l = L("jin_sowoon", ("samjae_sword", 8), ("nahan_fist", 1), ("flying_dagger", 1), ("cheonsu", 1));
            var s = S(l, new Dictionary<string, int> { ["shaolin"] = 2 });
            Assert.Equal("shaolin", s.CommonSchool);
            Assert.Equal(4, s.School("shaolin").Count);
            Assert.Equal(4, s.TierOf("shaolin"));
        }

        [Fact] public void 기본의_극_대상이_all이면_모든_문파에_하나씩()
        {
            var files = TestData.ReadFiles();
            var syn = JObject.Parse(files["synergies"]);
            syn["common"]!["target"] = "all";
            files["synergies"] = syn.ToString();
            var d = GameData.FromJson(files);

            var levels = BuildLevels.Of(d, new Loadout(d, d.Character("jin_sowoon")));
            var w = levels.Weapons.ToDictionary(kv => kv.Key, kv => kv.Value);
            w["samjae_sword"] = 8;
            w["nahan_fist"] = 1;
            var s = Synergy.Compute(d, new BuildLevels(w, new Dictionary<string, int> { ["geumgang"] = 1 }));
            Assert.Null(s.CommonSchool);
            Assert.All(s.Schools, sc => Assert.Equal(1, sc.Bonus));
            Assert.Equal(3, s.TierOf("shaolin"));
            Assert.Equal(0, s.TierOf("tang"));
        }

        // ---- 레벨업 카드 미리보기 ----

        [Fact] public void 새_심법을_고르면_닿는_진전_단계를_미리_보여준다()
        {
            var p = Synergy.Preview(_d, L("hyegong"), new Offer(OfferKind.NewPassive, "geumgang"));
            Assert.Equal("shaolin", p.School!.Id);
            Assert.Equal(2, p.SchoolCountAfter);
            var r = Assert.Single(p.Reached);
            Assert.Equal(("shaolin", 2), (r.School.Id, r.Tier.Count));
            Assert.Empty(p.Lost);
            Assert.Empty(p.Links);
        }

        [Fact] public void 단계에_닿지_않는_선택이면_개수만_오른다()
        {
            var p = Synergy.Preview(_d, L("dang_yeonhwa", ("cheonsu", 1), ("poison_mist", 1)), new Offer(OfferKind.NewPassive, "mandok"));
            Assert.Equal("tang", p.School!.Id);
            Assert.Equal(4, p.SchoolCountAfter);
            Assert.Empty(p.Reached);
        }

        [Fact] public void 이미_가진_것을_올리면_진전_개수는_그대로다()
        {
            var p = Synergy.Preview(_d, L("hyegong", ("geumgang", 1)), new Offer(OfferKind.UpgradePassive, "geumgang"));
            Assert.Equal(2, p.SchoolCountAfter);
            Assert.Empty(p.Reached);
        }

        [Fact] public void 연환_짝을_이미_가졌으면_안내하고_이걸로_완성되는지_알려준다()
        {
            var near = Synergy.Preview(_d, L("jin_sowoon", ("samjae_sword", 4), ("changcheon_qi", 2)), new Offer(OfferKind.UpgradeWeapon, "changcheon_qi"));
            var h = Assert.Single(near.Links);
            Assert.Equal(("ssanggeom", "samjae_sword", false), (h.Link.Id, h.Partner, h.Completes));
            Assert.Empty(near.CompletedLinks);

            var done = Synergy.Preview(_d, L("jin_sowoon", ("samjae_sword", 4), ("changcheon_qi", 3)), new Offer(OfferKind.UpgradeWeapon, "changcheon_qi"));
            Assert.True(Assert.Single(done.Links).Completes);
            Assert.Equal(new[] { "ssanggeom" }, done.CompletedLinks.Select(l => l.Id));
        }

        [Fact] public void 새_무공을_고를_때도_짝을_가진_연환만_안내한다()
        {
            // 창천검기의 연환은 쌍검합벽(삼재검법)과 인뢰검(뇌부). 뇌부는 없으니 쌍검합벽만.
            var p = Synergy.Preview(_d, L("jin_sowoon", ("samjae_sword", 4)), new Offer(OfferKind.NewWeapon, "changcheon_qi"));
            var h = Assert.Single(p.Links);
            Assert.Equal("ssanggeom", h.Link.Id);
            Assert.False(h.Completes);
            Assert.Equal("namgung", p.School!.Id);
        }

        [Fact] public void 이미_켜진_연환은_안내에서_빠진다()
        {
            var p = Synergy.Preview(_d, L("jin_sowoon", ("samjae_sword", 4), ("changcheon_qi", 4)), new Offer(OfferKind.UpgradeWeapon, "samjae_sword"));
            Assert.Empty(p.Links);
            Assert.Null(p.School);
        }

        [Fact] public void 삼재검법_Lv8로_오르는_선택은_기본의_극으로_닿는_단계를_보여준다()
        {
            var p = Synergy.Preview(_d, L("jin_sowoon", ("samjae_sword", 7), ("nahan_fist", 1), ("geumgang", 1)), new Offer(OfferKind.UpgradeWeapon, "samjae_sword"));
            Assert.Null(p.School);
            Assert.Equal(0, p.SchoolCountAfter);
            var r = Assert.Single(p.Reached);
            Assert.Equal(("shaolin", 3), (r.School.Id, r.Tier.Count));
        }

        [Fact] public void 기본의_극이_다른_문파로_옮겨_가면_내려가는_단계도_보여준다()
        {
            // 소림 3·당가 3 동점 → 하나 더해 4단계가 되는 소림이 기본의 극을 받는다.
            // 만독심법을 고르면 당가 4가 가장 많아져 기본의 극이 당가로 가고(5단계), 소림은 4단계를 잃는다.
            var l = L("jin_sowoon", ("samjae_sword", 8),
                ("nahan_fist", 1), ("geumgang", 1), ("cheongsim", 1),
                ("flying_dagger", 1), ("poison_mist", 1), ("cheonsu", 1));
            Assert.Equal("shaolin", S(l).CommonSchool);
            var p = Synergy.Preview(_d, l, new Offer(OfferKind.NewPassive, "mandok"));
            Assert.Equal(("tang", 5), (Assert.Single(p.Reached).School.Id, p.Reached[0].Tier.Count));
            Assert.Equal(("shaolin", 4), (Assert.Single(p.Lost).School.Id, p.Lost[0].Tier.Count));
        }

        [Fact] public void 은자_선택지는_시너지를_바꾸지_않는다()
        {
            var p = Synergy.Preview(_d, L("hyegong", ("geumgang", 1)), new Offer(OfferKind.Fallback, "silver_or_food"));
            Assert.Null(p.School);
            Assert.Empty(p.Reached);
            Assert.Empty(p.Lost);
            Assert.Empty(p.Links);
        }

        [Fact] public void 미리보기는_로드아웃을_건드리지_않는다()
        {
            var l = L("jin_sowoon", ("samjae_sword", 4), ("changcheon_qi", 3));
            var before = l.Weapons.ToDictionary(kv => kv.Key, kv => kv.Value);
            int gauge = l.Gauge.Value;
            Synergy.Preview(_d, l, new Offer(OfferKind.UpgradeWeapon, "changcheon_qi"));
            Synergy.Preview(_d, l, new Offer(OfferKind.NewPassive, "noejeong"));
            Assert.Equal(before, l.Weapons.ToDictionary(kv => kv.Key, kv => kv.Value));
            Assert.Empty(l.Passives);
            Assert.Equal(gauge, l.Gauge.Value);
        }
    }
}
