using System;
using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>문파 병기(backlog GU-10b, sinbyeong.md). gear.json 이 설계안 표와 맞고, Build.Gear 가 칸·등급·떨어뜨리기·6단을 셈하는지.</summary>
    public class GearTests
    {
        readonly GameData _d = TestData.Load();

        [Fact]
        public void 등급은_설계안_표_넷이고_피해는_오르기만_한다()
        {
            var g = _d.Gear.Grades;
            Assert.Equal(new[] { "범병", "양병", "명병", "신병" }, g.Select(x => x.NameKo));
            Assert.Equal(new[] { 0.05, 0.10, 0.15, 0.20 }, g.Select(x => x.Damage));
            Assert.Equal(new[] { 0.0, 0.05, 0.10, 0.10 }, g.Select(x => x.Area));
            Assert.Equal(new[] { false, false, false, true }, g.Select(x => x.Sixth));
        }

        [Fact]
        public void 병기는_문파마다_하나이고_대표_무공은_그_문파_시작_무공이다()
        {
            Assert.Equal(10, _d.Gear.Gear.Count);
            Assert.Equal(_d.Gear.Gear.Count, _d.Gear.Gear.Select(g => g.Id).Distinct().Count());
            foreach (var sect in _d.Cheonha.Sects)
            {
                var gear = Gear.OfSect(_d, sect.Id);
                Assert.NotNull(gear);
                Assert.Equal(sect.StartWeapon, gear!.Weapon);
            }
            var relic = _d.Gear.Gear.Single(g => g.Sect == null);
            Assert.Equal("samjae_sword", relic.Weapon);
            Assert.Equal("relic:songshan", relic.Source);
        }

        [Fact]
        public void 떨어뜨리는_적은_있는_적이고_확정은_정예다()
        {
            foreach (var g in _d.Gear.Gear)
            {
                foreach (var id in g.Droppers) Assert.Contains(_d.Enemies, e => e.Id == id && e.Family != "elite");
                foreach (var id in g.Guaranteed) Assert.Contains(_d.Enemies, e => e.Id == id && e.Family == "elite" && e.Undead);
            }
        }

        [Fact]
        public void 같은_병기는_등급이_오르고_다른_병기는_범병으로_바꾼다()
        {
            var s = new GearSlot();
            Assert.True(s.Empty);
            Assert.Equal(0, s.Pick(_d, "vajra_beads"));
            Assert.Equal(1, s.Pick(_d, "vajra_beads"));
            Assert.Equal(0, s.Pick(_d, "changcheon_blade"));
            Assert.Equal("changcheon_blade", s.GearId);
            for (int i = 0; i < 5; i++) s.Pick(_d, "changcheon_blade");
            Assert.Equal(3, s.Grade);
            Assert.Throws<ArgumentException>(() => s.Pick(_d, "없는_병기"));
        }

        [Fact]
        public void 땅의_주인은_그_문파_병기만_올린다()
        {
            var s = new GearSlot();
            Assert.False(s.GradeUpIfSect(_d, "shaolin"));
            s.Pick(_d, "vajra_beads");
            Assert.False(s.GradeUpIfSect(_d, "gaebang"));
            Assert.True(s.GradeUpIfSect(_d, "shaolin"));
            Assert.Equal(1, s.Grade);
        }

        [Fact]
        public void 숭산_정예는_확정이고_공통_강시와_다른_땅은_안_떨어진다()
        {
            var rng = new Random(1);
            Assert.Equal("vajra_beads", Gear.RollDrop(_d, "songshan", "gyeyul_monk", 0, rng));
            Assert.Equal("vajra_beads", Gear.RollDrop(_d, "songshan", "acting_abbot", 0, rng));
            Assert.Null(Gear.RollDrop(_d, "songshan", "dongin", 0, rng));
            Assert.Null(Gear.RollDrop(_d, "songshan", "hyeol_gangsi", 0, rng));
            Assert.Null(Gear.RollDrop(_d, "cheongun", "gangsi_axeman", 0, rng));
            Assert.Null(Gear.RollDrop(_d, "danggata", "gyeyul_monk", 0, rng));
        }

        [Fact]
        public void 일반_적은_확률로_떨어지고_기연이_올린다()
        {
            Assert.Equal(_d.Gear.Drop.Chance, Gear.DropChance(_d, 0), 9);
            Assert.True(Gear.DropChance(_d, 10) > Gear.DropChance(_d, 0));
            var rng = new Random(7);
            int n = 200000, got = 0;
            for (int i = 0; i < n; i++) if (Gear.RollDrop(_d, "wudangsan", "twin_gangsi", 0, rng) != null) got++;
            double rate = got / (double)n;
            Assert.InRange(rate, _d.Gear.Drop.Chance * 0.8, _d.Gear.Drop.Chance * 1.2);
        }

        [Fact]
        public void 등급_보너스는_그_문파_무공_전부에_들고_6단은_대표_무공만()
        {
            var s = new GearSlot();
            Assert.Equal(0, Gear.DamageBonus(_d, s, "nahan_fist"));
            s.Pick(_d, "vajra_beads");
            Assert.Equal(0.05, Gear.DamageBonus(_d, s, "nahan_fist"), 9);
            Assert.Equal(0.05, Gear.DamageBonus(_d, s, "sonic_qin"), 9);   // 소림 시너지 구성원 무공
            Assert.Equal(0, Gear.DamageBonus(_d, s, "geumgang"));            // 심법은 무공이 아님
            Assert.Equal(0, Gear.DamageBonus(_d, s, "samjae_sword"));
            for (int i = 0; i < 3; i++) s.Pick(_d, "vajra_beads");
            var nahan = _d.Weapon("nahan_fist");
            Assert.Equal(6, Gear.VisualStage(_d, s, nahan, 8, awakened: true));
            Assert.Equal(5, Gear.VisualStage(_d, s, nahan, 8, awakened: false));
            Assert.False(Gear.IsSixth(_d, s, "sonic_qin", true));
        }

        [Fact]
        public void 무명_철검은_삼재검법_하나에만_든다()
        {
            var s = new GearSlot();
            s.Pick(_d, "mumyeong_iron_sword");
            Assert.Equal(0.05, Gear.DamageBonus(_d, s, "samjae_sword"), 9);
            Assert.Equal(0, Gear.DamageBonus(_d, s, "nahan_fist"));
        }
    }
}
