using System;
using System.Linq;
using System.Numerics;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Run;
using BloodRain.Core.World;
using Newtonsoft.Json;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>생 도중 저장 ①(backlog GU-11h1). 뽑고 JSON 으로 오가고 새 Life 에 되돌리면 같은 상태.</summary>
    public class LifeSnapshotTests
    {
        readonly GameData _d = TestData.Load();

        (Soul soul, Life life) NewLife(string sect = "shaolin")
        {
            var soul = new Soul(_d);
            soul.EndLife(soul.BeginLife(Rebirth.Prologue(_d)).Finish(LifeEndWay.Fallen, 0));
            var c = _d.Cheonha;
            var cand = new RebirthCandidate(c.Sect(sect), c.Region(c.Sect(sect).Region), Array.Empty<BirthTraitDef>());
            return (soul, new Life(soul, Rebirth.Body(c, cand), new Random(1)));
        }

        [Fact] public void 뽑고_되돌리면_같은_상태()
        {
            var (_, life) = NewLife();
            var start = new Regions(_d).Center("songshan");
            var walkTo = new Regions(_d).Center("gaebang_hq");
            var rng = new Random(5);
            for (int i = 0; i < 3000 && life.Minutes < 5; i++)
            {
                var pos = Vector2.Lerp(start, walkTo, Math.Min(1f, life.Minutes / 5f));
                life.Tick(0.1f, pos, 0);
                var a = life.Absorb(3);
                for (int n = 0; n < a.Xp.LevelUps; n++)
                {
                    var offers = life.Loadout.RollLevelUp(rng, 0, _d.Weapons.Select(w => w.Id).Concat(_d.Passives.Select(p => p.Id)).ToHashSet());
                    if (offers.Count > 0) life.Loadout.Apply(offers[0]);
                }
            }
            life.GainSilver(37);
            var snap = LifeSnapshot.Capture(life);
            var json = JsonConvert.SerializeObject(snap);
            var back = JsonConvert.DeserializeObject<LifeSnapshot>(json)!;

            var (_, fresh) = NewLife();
            fresh.Restore(back);
            Assert.Equal(life.Minutes, fresh.Minutes, 4);
            Assert.Equal(life.Level, fresh.Level);
            Assert.Equal(life.Xp.Xp, fresh.Xp.Xp, 6);
            Assert.Equal(life.Position, fresh.Position);
            Assert.Equal(life.RegionId, fresh.RegionId);
            Assert.Equal(life.Log.VisitedRegions.OrderBy(x => x), fresh.Log.VisitedRegions.OrderBy(x => x));
            Assert.Equal(life.Loadout.Weapons.OrderBy(x => x.Key), fresh.Loadout.Weapons.OrderBy(x => x.Key));
            Assert.Equal(life.Loadout.Passives.OrderBy(x => x.Key), fresh.Loadout.Passives.OrderBy(x => x.Key));
            Assert.Equal(life.Loadout.Gauge.Value, fresh.Loadout.Gauge.Value);
            Assert.Equal(life.Charge.Value, fresh.Charge.Value, 4);
            Assert.Equal(life.Silver, fresh.Silver);
            Assert.True(life.Level > 1);
        }

        [Fact] public void 다른_몸이나_흐른_생에는_되돌리지_않는다()
        {
            var (_, life) = NewLife();
            life.Tick(0.5f, new Regions(_d).Center("songshan"), 0);
            var snap = LifeSnapshot.Capture(life);
            Assert.Throws<InvalidOperationException>(() => NewLife("gaebang").life.Restore(snap));
            Assert.Throws<InvalidOperationException>(() => life.Restore(snap));
            snap.SnapshotVersion = 99;
            Assert.Throws<InvalidOperationException>(() => NewLife().life.Restore(snap));
        }
    }
}
