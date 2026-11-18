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
            var evo = _d.Evolutions.First(e => life.Loadout.Weapons.ContainsKey(e.Weapon));
            life.Loadout.Evolve(evo);
            life.Log.Witness("songshan_refugees");
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
            Assert.Equal(life.Log.HighestRealm, fresh.Log.HighestRealm);
            Assert.Equal(life.Loadout.Evolved.OrderBy(x => x), fresh.Loadout.Evolved.OrderBy(x => x));
            Assert.Equal(new[] { "songshan_refugees" }, fresh.Log.Chronicle);
            Assert.True(life.Level > 1);
        }

        [Fact] public void 쓰러뜨린_땅의_주인과_깨어난_땅을_되돌린다()
        {
            var (_, plain) = NewLife();
            var snap = LifeSnapshot.Capture(plain);
            snap.Level = _d.Balance.Realms.Single(r => r.Id == "jeoljeong").Level + 1;
            snap.Minutes = 12;
            snap.BossesKilled.Add("songshan");
            var (_, life) = NewLife();
            int stonesBefore = life.SealStones;
            life.Restore(snap);
            Assert.Equal(BossRegionState.Defeated, life.Bosses.State("songshan"));
            Assert.Contains("songshan", life.Log.BossesKilled);
            Assert.Equal(stonesBefore + 1, life.SealStones);
            Assert.NotEmpty(life.Bosses.Awake);                 // 절정이면 장막 안 다른 땅의 주인이 깨어 있다
            Assert.Equal(new[] { "songshan" }, LifeSnapshot.Capture(life).BossesKilled);
        }

        [Fact] public void 저장_파일에_이어_할_생이_실리고_없으면_예전_문자열과_같다()
        {
            var save = SaveGame.New(_d);
            string plain = save.ToJson();
            Assert.DoesNotContain("\"life\"", plain);
            var (_, life) = NewLife();
            life.Tick(1f, new Regions(_d).Center("songshan"), 0);
            var link = _d.Synergies.Links.First();
            save.Ongoing = LifeSnapshot.Capture(life);
            save.Ongoing.Links.Add(link.Id);
            var back = SaveGame.FromJson(_d, save.ToJson());
            Assert.NotNull(back.Ongoing);
            Assert.Equal(life.Minutes, back.Ongoing!.Minutes, 4);
            var (_, fresh) = NewLife();
            fresh.Restore(back.Ongoing);
            Assert.Contains(link.Id, fresh.Loadout.AwakenedLinks);
            back.Ongoing = null;
            Assert.Equal(plain, back.ToJson());
            // 깨진 칸은 버리고 혼은 살린다
            Assert.Contains("\"SnapshotVersion\": 1", save.ToJson());
            var broken = save.ToJson().Replace("\"SnapshotVersion\": 1", "\"SnapshotVersion\": 99");
            Assert.Null(SaveGame.FromJson(_d, broken).Ongoing);
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
