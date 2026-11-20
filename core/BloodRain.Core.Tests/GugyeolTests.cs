using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Run;
using Newtonsoft.Json;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>구결(口訣): 지난 생의 고른 차례를 앞부분만 그대로 자동으로 고른다(천하 확장 설계안 '회귀 가속', GU-11k).</summary>
    public class GugyeolTests
    {
        readonly GameData _d = TestData.Load();

        static readonly Offer Sword = new Offer(OfferKind.UpgradeWeapon, "samjae_sword");
        static readonly Offer Palm = new Offer(OfferKind.NewWeapon, "palm_wind");
        static readonly Offer Heart = new Offer(OfferKind.NewPassive, "heart");
        static readonly Offer Silver = new Offer(OfferKind.Fallback, "silver");

        [Fact] public void 걸음은_무공과_심법을_나누고_은자는_안_센다()
        {
            Assert.Equal("w:samjae_sword", Gugyeol.Step(Sword));
            Assert.Equal("w:palm_wind", Gugyeol.Step(Palm));
            Assert.Equal("p:heart", Gugyeol.Step(Heart));
            Assert.Null(Gugyeol.Step(Silver));
            Assert.Equal(Gugyeol.Step(Sword), Gugyeol.Step(Sword.AsGreat(2)));
        }

        [Fact] public void 앞부분은_그대로_고르고_어긋나면_그_생엔_멈춘다()
        {
            var g = new GugyeolPlayer(new[] { "w:palm_wind", "w:samjae_sword", "p:heart" });
            Assert.Equal(Palm.Id, g.Pick(new[] { Sword, Palm, Heart })!.Value.Id);
            // 같은 걸음이 둘이면 금빛 대오 카드를 먼저
            var picked = g.Pick(new[] { Sword, Sword.AsGreat(2), Heart })!.Value;
            Assert.True(picked.Great);
            Assert.Null(g.Pick(new[] { Sword, Silver }));
            Assert.True(g.Stopped);
            Assert.Equal(2, g.Done);
            Assert.Null(g.Pick(new[] { Heart })); // 멈춘 뒤엔 맞아도 안 고른다
        }

        [Fact] public void 끝까지_따르면_멈추고_빈_구결은_처음부터_멈춰_있다()
        {
            var g = new GugyeolPlayer(new[] { "p:heart" });
            Assert.NotNull(g.Pick(new[] { Heart }));
            Assert.True(g.Stopped);
            Assert.Null(g.Pick(new[] { Heart }));
            Assert.True(new GugyeolPlayer(Array.Empty<string>()).Stopped);
        }

        (Soul soul, Life life) NewLife(Soul? soul = null)
        {
            if (soul == null)
            {
                soul = new Soul(_d);
                soul.EndLife(soul.BeginLife(Rebirth.Prologue(_d)).Finish(LifeEndWay.Fallen, 0));
            }
            var c = _d.Cheonha;
            var cand = new RebirthCandidate(c.Sect("shaolin"), c.Region("songshan"), Array.Empty<BirthTraitDef>());
            return (soul, new Life(soul, Rebirth.Body(c, cand), new Random(1)));
        }

        List<Offer> PlayPicks(Life life, int n, int seed)
        {
            var rng = new Random(seed);
            var all = _d.Weapons.Select(w => w.Id).Concat(_d.Passives.Select(p => p.Id)).ToHashSet();
            var taken = new List<Offer>();
            for (int i = 0; i < n; i++)
            {
                var offers = life.Loadout.RollLevelUp(rng, 0, all);
                if (offers.Count == 0) break;
                life.Loadout.Apply(offers[0]);
                taken.Add(offers[0]);
            }
            return taken;
        }

        [Fact] public void 생이_끝나면_혼이_문파마다_마지막_차례를_남기고_저장한다()
        {
            var (soul, life) = NewLife();
            var taken = PlayPicks(life, 8, 3);
            var steps = taken.Select(Gugyeol.Step).Where(s => s != null).ToList();
            Assert.NotEmpty(steps);
            Assert.Equal(steps, life.Loadout.PickOrder);
            soul.EndLife(life.End(LifeEndWay.Fallen, 0));
            Assert.Equal(steps, soul.GugyeolOf("shaolin"));
            Assert.Empty(soul.GugyeolOf("gaebang"));
            var back = Soul.FromJson(_d, soul.ToJson());
            Assert.Equal(steps, back.GugyeolOf("shaolin"));

            // 다음 소림 생: 구결이 같은 카드가 뜨는 동안 그대로 고른다
            var (_, next) = NewLife(back);
            var g = new GugyeolPlayer(back.GugyeolOf("shaolin"));
            var first = new Offer(steps[0]!.StartsWith("w:") ? OfferKind.UpgradeWeapon : OfferKind.UpgradePassive, steps[0]!.Substring(2));
            Assert.Equal(first.Id, g.Pick(new[] { Silver, first })!.Value.Id);
        }

        /// <summary>GU-11o1(2026-11-20 추천대로 — decisions #66): 구결이 켜진 동안 카드 한 장이 다음 걸음으로 바뀌어 뜨고, 고를 수 없는 걸음이면 안 바뀌어 멈춘다.</summary>
        [Fact] public void 구결이_켜지면_다음_걸음_카드가_끼어_뜨고_못_고르는_걸음이면_멈춘다()
        {
            var (_, life) = NewLife();
            var l = life.Loadout;
            var unlocked = _d.Weapons.Where(w => w.Unlock == "start").Select(w => w.Id).Concat(_d.Passives.Select(p => p.Id)).ToHashSet();
            var cand = l.Candidates(unlocked);
            var target = cand.First(o => o.Kind == OfferKind.NewPassive);
            string step = Gugyeol.Step(target)!;
            for (int seed = 0; seed < 40; seed++)
            {
                var plain = l.RollLevelUp(new Random(seed), 0, unlocked);
                var forced = l.RollLevelUp(new Random(seed), 0, unlocked, null, step);
                Assert.Equal(plain.Count, forced.Count);                                 // 카드 수 그대로
                Assert.Contains(forced, o => Gugyeol.Step(o) == step);
                Assert.Equal(forced.Count, forced.Select(o => o.Kind + o.Id).Distinct().Count());
                if (plain.Any(o => Gugyeol.Step(o) == step)) Assert.Equal(plain.Select(o => o.Id), forced.Select(o => o.Id));
            }
            var g = new GugyeolPlayer(new[] { step, "w:no_such_weapon" });
            Assert.Equal(step, g.Next);
            var offers = l.RollLevelUp(new Random(3), 0, unlocked, null, g.Next);
            Assert.Equal(target.Id, g.Pick(offers)!.Value.Id);
            Assert.Equal("w:no_such_weapon", g.Next);
            var after = l.RollLevelUp(new Random(4), 0, unlocked, null, g.Next);   // 고를 수 없는 걸음: 카드는 그대로
            Assert.Equal(l.RollLevelUp(new Random(4), 0, unlocked).Select(o => o.Id), after.Select(o => o.Id));
            Assert.Null(g.Pick(after));
            Assert.True(g.Stopped);
            Assert.Null(g.Next);
        }

        [Fact] public void 구결_시뮬레이터_보고서가_세_몸을_낸다()
        {
            var md = BloodRain.Sim.GugyeolSim.Report(BloodRain.Sim.SimData.Load(TestData.DataDir), seeds: 2);
            foreach (var body in new[] { "| 소림 |", "| 개방 |", "| 남궁 |" }) Assert.Contains(body, md);
            var p = BloodRain.Sim.GugyeolSim.RunPair(_d, "shaolin", 1);
            Assert.InRange(p.Followed, 0, p.Steps);
            Assert.True(p.Steps > 0);
        }

        [Fact] public void 생_도중_저장을_되돌리면_고른_차례도_돌아온다()
        {
            var (_, life) = NewLife();
            PlayPicks(life, 6, 5);
            var snap = JsonConvert.DeserializeObject<LifeSnapshot>(JsonConvert.SerializeObject(LifeSnapshot.Capture(life)))!;
            var (_, fresh) = NewLife();
            fresh.Restore(snap);
            Assert.Equal(life.Loadout.PickOrder, fresh.Loadout.PickOrder);
        }
    }
}

namespace BloodRain.Core.Tests
{
    /// <summary>혼이 경지 구간을 넘은 횟수를 세고 전생의 공력을 낸다(GU-11l).</summary>
    public class SoulCrossingTests
    {
        readonly GameData _d = TestData.Load();

        [Fact] public void 생마다_오른_경지까지_구간을_세고_저장하고_공력을_낸다()
        {
            var realms = _d.Balance.Realms.OrderBy(r => r.Level).ToList();
            var soul = new Soul(_d);
            soul.EndLife(soul.BeginLife(Rebirth.Prologue(_d)).Finish(LifeEndWay.Fallen, 0));
            var before = soul.RealmCrossings.ToDictionary(kv => kv.Key, kv => kv.Value);
            var r = new LifeRecord("shaolin", LifeEndWay.Fallen, new RebirthQiLedger(_d.Cheonha.RebirthQi).Settle(0), Array.Empty<string>(), new[] { "songshan" }, realms[2].Id, Array.Empty<string>());
            soul.EndLife(r);
            int Get(Dictionary<string, int> d, string id) => d.TryGetValue(id, out var n) ? n : 0;
            Assert.Equal(Get(before, realms[1].Id) + 1, soul.RealmCrossings[realms[1].Id]);
            Assert.Equal(Get(before, realms[2].Id) + 1, soul.RealmCrossings[realms[2].Id]);
            Assert.False(soul.RealmCrossings.ContainsKey(realms[0].Id));
            Assert.Equal(0, soul.RealmCrossings.TryGetValue(realms[3].Id, out var n3) ? n3 : 0);

            var back = Soul.FromJson(_d, soul.ToJson());
            Assert.Equal(soul.RealmCrossings.OrderBy(kv => kv.Key), back.RealmCrossings.OrderBy(kv => kv.Key));
            var p = back.PastLifePower();
            Assert.Equal(soul.RealmCrossings[realms[1].Id], p.Crossings(realms[1].Id));
            // 이류 구간은 줄고, 가장 높이 오른 경지(일류)로 가는 구간은 제 속도
            Assert.True(p.CutForSegment(1) > 0);
            Assert.Equal(0f, p.CutForSegment(2));
            Assert.Empty(p.WakeRealms());
        }

        [Fact] public void 서른_번_넘은_구간까지만_그_경지로_깨어날_수_있다()
        {
            var realms = _d.Balance.Realms.OrderBy(r => r.Level).ToList();
            var cross = new Dictionary<string, int> { [realms[1].Id] = 31, [realms[2].Id] = 30, [realms[3].Id] = 29, [realms[4].Id] = 40 };
            var p = new BloodRain.Core.Progression.PastLifePower(realms, cross, realms[4].Id);
            Assert.Equal(new[] { realms[1].Id, realms[2].Id }, p.WakeRealms());
        }
    }
}

namespace BloodRain.Core.Tests
{
    /// <summary>연표 사건의 진영 갈래(GU-11n).</summary>
    public class ChronicleBranchTests
    {
        readonly GameData _d = TestData.Load();

        [Fact] public void 갈래의_진영은_있는_진영이고_무림맹_집결은_정파와_마교가_갈린다()
        {
            var camps = _d.Cheonha.Camps.List.Select(c => c.Id).ToHashSet();
            foreach (var e in _d.Chronicle.Events) foreach (var b in e.Branches) Assert.Contains(b.Camp, camps);
            var muster = _d.Chronicle.Events.Single(e => e.Id == "murim_muster");
            Assert.Equal("reinforce", BloodRain.Core.World.Chronicle.BranchFor(muster, _d.Cheonha.CampOf("shaolin").Id)!.Id);
            Assert.Equal("siege", BloodRain.Core.World.Chronicle.BranchFor(muster, _d.Cheonha.CampOf("demonic").Id)!.Id);
            Assert.Null(BloodRain.Core.World.Chronicle.BranchFor(muster, _d.Cheonha.CampOf("waterfort").Id));
        }
    }
}
