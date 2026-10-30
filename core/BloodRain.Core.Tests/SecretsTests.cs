using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>비전 8종 판정과 판 기록(S-02, 기획서 20장).</summary>
    public class SecretsTests
    {
        readonly GameData _d = TestData.Load();

        static Loadout L(string character, params (string, int)[] items) => TestData.Build(character, items);

        (SecretChecker c, RunRecord r) New(string character, IEnumerable<string>? cleared = null, int books = 0)
        {
            var r = new RunRecord(character, cleared, books);
            return (new SecretChecker(_d, r), r);
        }

        static string[] Ids(List<SecretDef> list) => list.Select(s => s.Id).ToArray();

        [Fact] public void 아무것도_안_하면_열리는_비전이_없다()
        {
            var (c, _) = New("jin_sowoon");
            Assert.Empty(c.Check(L("jin_sowoon")));
        }

        [Fact] public void 판_기록은_음양_0에_머문_연속_시간과_게임_시간을_따로_센다()
        {
            var r = new RunRecord("jin_sowoon");
            for (int i = 0; i < 3; i++) r.Tick(1, 0);
            Assert.Equal(3, r.Counters.GaugeZeroSeconds);
            r.Tick(1, 7);
            Assert.Equal(0, r.Counters.GaugeZeroSeconds);
            r.Tick(1, 0); // 방금 0으로 돌아온 순간부터 다시 센다
            Assert.Equal(0, r.Counters.GaugeZeroSeconds);
            r.Tick(1, 0);
            Assert.Equal(1, r.Counters.GaugeZeroSeconds);
            Assert.Equal(6, r.Counters.ElapsedSeconds);
        }

        [Fact] public void 매화삼롱은_세_가지가_모두_Lv5면_열리고_다시_돌려주지_않는다()
        {
            var (c, _) = New("jin_sowoon");
            Assert.Empty(c.Check(L("jin_sowoon", ("sonic_qin", 5), ("falling_petals", 5), ("maehwa", 4))));
            var full = L("jin_sowoon", ("sonic_qin", 5), ("falling_petals", 5), ("maehwa", 5));
            Assert.Equal(new[] { "maehwa_samnong" }, Ids(c.Check(full)));
            Assert.Empty(c.Check(full));
            Assert.Contains("maehwa_samnong", c.Unlocked);
        }

        [Fact] public void 낙매검선의_피는_진소운이_5장을_깬_적이_있어야_열린다()
        {
            var build = new[] { ("falling_petals", 8), ("maehwa", 5) };
            Assert.Empty(New("jin_sowoon").c.Check(L("jin_sowoon", build)));
            Assert.Empty(New("jin_sowoon", new[] { "chapter4" }).c.Check(L("jin_sowoon", build)));
            Assert.Empty(New("dang_yeonhwa", new[] { "chapter5" }).c.Check(L("dang_yeonhwa", build)));
            Assert.Equal(new[] { "nakmae_geomseon" }, Ids(New("jin_sowoon", new[] { "chapter5" }).c.Check(L("jin_sowoon", build))));
        }

        [Fact] public void 낙매검선의_피는_낙화검우가_절기가_되어도_열린다()
        {
            var l = L("jin_sowoon", ("falling_petals", 8), ("maehwa", 5));
            l.Evolve(_d.Evolutions.First(e => e.Weapon == "falling_petals"));
            Assert.Contains("nakmae_geomseon", Ids(New("jin_sowoon", new[] { "chapter5" }).c.Check(l)));
        }

        [Fact] public void 여러_비전이_한꺼번에_열리면_데이터_순서로()
        {
            var (c, _) = New("jin_sowoon", new[] { "chapter5" });
            var l = L("jin_sowoon", ("falling_petals", 8), ("maehwa", 5), ("sonic_qin", 5));
            Assert.Equal(new[] { "nakmae_geomseon", "maehwa_samnong" }, Ids(c.Check(l)));
        }

        [Fact] public void 어강술은_세_가지를_지닌_채로_정신부를_세_장_써야_열린다()
        {
            var (c, r) = New("jin_sowoon");
            var l = L("jin_sowoon", ("thunder_talisman", 1), ("chain_sickle", 1), ("domun", 1));
            r.UseTalisman(); Assert.Empty(c.Check(l));
            r.UseTalisman(); Assert.Empty(c.Check(l));
            r.UseTalisman();
            Assert.Equal(new[] { "eogang" }, Ids(c.Check(l)));
            Assert.Equal(3, r.Counters.TalismansUsed);
        }

        [Fact] public void 어강술_조건을_갖추기_전에_쓴_정신부는_세지_않는다()
        {
            var (c, r) = New("jin_sowoon");
            var l = L("jin_sowoon", ("thunder_talisman", 1), ("chain_sickle", 1));
            r.UseTalisman(); r.UseTalisman();
            Assert.Empty(c.Check(l));
            l.Apply(new Offer(OfferKind.NewPassive, "domun"));
            r.UseTalisman();
            Assert.Empty(c.Check(l)); // 갖춘 뒤로는 한 장
            r.UseTalisman(); r.UseTalisman();
            Assert.Equal(new[] { "eogang" }, Ids(c.Check(l)));
        }

        [Fact] public void 무극은_조건을_갖춘_채_음양_0을_3분_이어야_열린다()
        {
            var (c, r) = New("jin_sowoon");
            var l = L("jin_sowoon", ("taiji_sword", 8), ("yangui", 5));
            Assert.Empty(c.Check(l));
            for (int i = 0; i < 179; i++) { r.Tick(1, 0); Assert.Empty(c.Check(l)); }
            r.Tick(1, 0);
            Assert.Equal(new[] { "mugeuk" }, Ids(c.Check(l)));
        }

        [Fact] public void 무극_조건을_갖추기_전의_음양_0_시간은_세지_않는다()
        {
            var (c, r) = New("jin_sowoon");
            var l = L("jin_sowoon", ("taiji_sword", 8), ("yangui", 4));
            for (int i = 0; i < 100; i++) { r.Tick(1, 0); c.Check(l); }
            l.Apply(new Offer(OfferKind.UpgradePassive, "yangui")); // 양의심법은 중이라 게이지가 안 움직인다
            Assert.Equal(0, l.Gauge.Value);
            Assert.Empty(c.Check(l));
            for (int i = 0; i < 179; i++) { r.Tick(1, 0); Assert.Empty(c.Check(l)); }
            r.Tick(1, 0);
            Assert.Equal(new[] { "mugeuk" }, Ids(c.Check(l)));
        }

        [Fact] public void 무극은_게이지가_한_칸이라도_움직이면_처음부터_센다()
        {
            var (c, r) = New("jin_sowoon");
            var l = L("jin_sowoon", ("taiji_sword", 8), ("yangui", 5));
            c.Check(l);
            for (int i = 0; i < 170; i++) { r.Tick(1, 0); c.Check(l); }
            r.Tick(1, 5); c.Check(l);
            r.Tick(1, 0); c.Check(l);
            for (int i = 0; i < 179; i++) { r.Tick(1, 0); Assert.Empty(c.Check(l)); }
            r.Tick(1, 0);
            Assert.Equal(new[] { "mugeuk" }, Ids(c.Check(l)));
        }

        [Fact] public void 취선은_음식_열다섯_개_뒤에_기혈_20퍼센트_이하로_보갑을_열어야_열린다()
        {
            var (c, r) = New("machil");
            var l = L("machil", ("juseon", 1));
            for (int i = 0; i < 14; i++) r.EatFood();
            r.OpenChest(0.1f); Assert.Empty(c.Check(l));
            r.EatFood();
            r.OpenChest(0.3f); Assert.Empty(c.Check(l));
            r.OpenChest(0.2f);
            Assert.Equal(new[] { "chwiseon" }, Ids(c.Check(l)));
        }

        [Fact] public void 취선_보갑은_연_순간의_음식_수로_본다()
        {
            var (c, r) = New("machil");
            var l = L("machil", ("juseon", 1));
            for (int i = 0; i < 10; i++) r.EatFood();
            r.OpenChest(0.1f);
            for (int i = 0; i < 5; i++) r.EatFood();
            Assert.Empty(c.Check(l));
            Assert.Equal(15, r.Counters.FoodEaten);
        }

        [Fact] public void 취선은_주선공_없이_연_보갑을_나중에_치지_않는다()
        {
            var (c, r) = New("machil");
            var l = L("machil");
            for (int i = 0; i < 15; i++) r.EatFood();
            r.OpenChest(0.1f);
            Assert.Empty(c.Check(l));
            l.Apply(new Offer(OfferKind.NewPassive, "juseon"));
            Assert.Empty(c.Check(l));
        }

        [Fact] public void 역천은_기혈역류를_세_번_연달아_끊어야_열린다()
        {
            var (c, r) = New("jin_sowoon");
            var l = L("jin_sowoon");
            r.CutBacklash(); r.CutBacklash(); r.MissBacklash(); r.CutBacklash(); r.CutBacklash();
            Assert.Empty(c.Check(l));
            r.CutBacklash();
            Assert.Equal(new[] { "yeokcheon" }, Ids(c.Check(l)));
        }

        [Fact] public void 역천은_나중에_확인해도_가장_길었던_연속으로_본다()
        {
            var (c, r) = New("jin_sowoon");
            r.CutBacklash(); r.CutBacklash(); r.CutBacklash(); r.MissBacklash();
            Assert.Equal(0, r.Counters.BacklashCutStreak);
            Assert.Equal(new[] { "yeokcheon" }, Ids(c.Check(L("jin_sowoon"))));
        }

        [Fact] public void 혈해마공_잔편은_의서_세_권을_가진_채로_은빛_등롱을_부숴야_열린다()
        {
            var (c, r) = New("baekya", books: 2);
            var l = L("baekya", ("hyeolhae", 1));
            r.BreakSilverLantern(); Assert.Empty(c.Check(l));
            r.CollectMedicalBook(); Assert.Empty(c.Check(l)); // 등롱을 새로 부숴야 한다
            r.BreakSilverLantern();
            Assert.Equal(new[] { "hyeolhae_janpyeon" }, Ids(c.Check(l)));
            Assert.Equal(3, r.Counters.MedicalBooks);
        }

        [Fact] public void 무명검은_삼재검법_하나만_Lv8로_게임_시간_10분을_넘겨야_열린다()
        {
            var (c, r) = New("jin_sowoon");
            var solo = L("jin_sowoon", ("samjae_sword", 8));
            for (int i = 0; i < 599; i++) r.Tick(1, 3);
            Assert.Empty(c.Check(solo));
            r.Tick(1, 3);
            Assert.Equal(new[] { "mumyeong" }, Ids(c.Check(solo)));
        }

        [Fact] public void 무명검은_다른_공격_무공이_있거나_Lv7이면_안_열린다()
        {
            var (c, r) = New("jin_sowoon");
            for (int i = 0; i < 700; i++) r.Tick(1, 3);
            Assert.Empty(c.Check(L("jin_sowoon", ("samjae_sword", 8), ("nahan_fist", 1))));
            Assert.Empty(c.Check(L("jin_sowoon", ("samjae_sword", 7))));
            Assert.Equal(new[] { "mumyeong" }, Ids(c.Check(L("jin_sowoon", ("samjae_sword", 8), ("cheongmyeong", 5)))));
        }
    }
}
