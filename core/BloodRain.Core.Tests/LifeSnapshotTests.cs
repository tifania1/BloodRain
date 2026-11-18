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

        [Fact] public void 일지의_세던_수와_구슬_윤회의_기를_되돌린다()
        {
            var (_, life) = NewLife();
            var at = new Regions(_d).Center("songshan");
            for (int i = 0; i < 40; i++) { life.Tick(0.1f, at, 0); life.Absorb(2); }
            life.Log.Kill(57);
            life.Log.Dash(); life.Log.Dash();
            life.Log.UseTalisman();
            life.Log.CatchGoldenToad();
            life.Log.EatFood();
            life.Log.KillElite();
            life.Log.CatchSpiritBeast();
            life.Log.Whispered = true;
            life.Log.SingleWeaponBoss = true;
            var letter = life.Log.ReadLetter(Array.Empty<string>());
            var evo = _d.Evolutions.First();
            life.Log.Awaken(evo.Id);
            var json = JsonConvert.SerializeObject(LifeSnapshot.Capture(life));
            var back = JsonConvert.DeserializeObject<LifeSnapshot>(json)!;

            var (_, fresh) = NewLife();
            fresh.Restore(back);
            var a = life.Log.Finish(LifeEndWay.Fallen, 10);
            var b = fresh.Log.Finish(LifeEndWay.Fallen, 10);
            Assert.Equal(a.Kills, b.Kills);
            Assert.Equal(a.Dashes, b.Dashes);
            Assert.Equal(a.TalismansUsed, b.TalismansUsed);
            Assert.Equal(a.GoldenToads, b.GoldenToads);
            Assert.Equal(a.FoodEaten, b.FoodEaten);
            Assert.Equal(a.BloodCrystal, b.BloodCrystal);
            Assert.Equal(a.SingleWeaponBoss, b.SingleWeaponBoss);
            Assert.Equal(a.Whispered, b.Whispered);
            Assert.Equal(a.Letters, b.Letters);
            if (letter != null) Assert.Contains(letter.Id, b.Letters);
            Assert.Equal(new[] { evo.Id }, b.AwakenedEvolutions);
            Assert.Equal(life.Log.Qi.OrbCount, fresh.Log.Qi.OrbCount);
            Assert.True(a.RebirthQi.Total > 0);
            Assert.Equal(a.RebirthQi.Total, b.RebirthQi.Total);
        }

        [Fact] public void 깨달음_기연_영약_재기를_되돌린다()
        {
            var (_, life) = NewLife();
            var inn = _d.Cheonha.Inn;
            life.TakeElixirs(new[] { inn.Elixirs.First(e => e.Id == "hyeoljeongdan"), inn.Elixirs.First(e => e.Id == "taegeukhwan"), inn.Elixirs.First(e => e.Id == "sohwandan") });
            var at = new Regions(_d).Center("songshan");
            life.Tick(1f, at, 0);
            life.Log.Kill(250);
            life.TakeInsight(_d.Insight("yeokgeun"));
            life.TakeInsight(_d.Insight("salgi"), life.Log.Kills);
            life.TakeInsight(_d.Insight("geumjong"));
            Assert.True(life.InsightEffects.TryBlock());
            life.Encounters.HonorRelic("shaolin");
            life.Encounters.MeetHermit();
            Assert.NotNull(life.TryRevive(life.CurrentStats()));   // 소환단
            life.Log.Kill(130);
            life.Log.KillElite();
            var json = JsonConvert.SerializeObject(LifeSnapshot.Capture(life));
            var back = JsonConvert.DeserializeObject<LifeSnapshot>(json)!;

            var (_, fresh) = NewLife();
            fresh.Restore(back);
            Assert.Equal(life.InsightEffects.Taken.Select(i => i.Id), fresh.InsightEffects.Taken.Select(i => i.Id));
            Assert.Equal(life.InsightBonus(life.Log.Kills)[StatKind.Might], fresh.InsightBonus(fresh.Log.Kills)[StatKind.Might], 5);
            Assert.False(fresh.InsightEffects.TryBlock());   // 금종조 대기가 이어진다
            Assert.Equal(life.Encounters.Met, fresh.Encounters.Met);
            Assert.Equal(life.Encounters.SchoolExtra.OrderBy(x => x.Key), fresh.Encounters.SchoolExtra.OrderBy(x => x.Key));
            Assert.Equal(life.Elixirs.Select(e => e.Id), fresh.Elixirs.Select(e => e.Id));
            Assert.True(fresh.ElixirReviveUsed);
            Assert.True(fresh.Loadout.Gauge.Frozen);
            Assert.Equal(life.YinyangFreezeLeft, fresh.YinyangFreezeLeft, 3);
            Assert.Equal(life.Log.BloodCrystal, fresh.Log.BloodCrystal);
            Assert.Equal(life.Loadout.Gauge.Value, fresh.Loadout.Gauge.Value);
            Assert.Equal(life.Level, fresh.Level);   // 대환단 없음, 영약을 다시 걸지 않는다
            Assert.Equal(life.Charge.Value, fresh.Charge.Value, 4);
        }

        [Fact] public void 처치_때_혼에_바로_연_것은_혼_칸이_싣는다()
        {
            // 극성 처치·태극 의상처럼 처치 순간 혼에 여는 것은 스냅샷이 아니라 같은 파일의 혼(soul 칸)이 싣는다. 되돌리며 다시 열지 않는다.
            var (soul, life) = NewLife();
            soul.Unlock(UnlockToken.BossAtExtreme);
            var save = new SaveGame(soul, new DialogueProgress()) { Ongoing = LifeSnapshot.Capture(life) };
            var back = SaveGame.FromJson(_d, save.ToJson());
            Assert.True(back.Soul.IsUnlocked(UnlockToken.BossAtExtreme));
            Assert.NotNull(back.Ongoing);
        }

        [Fact] public void 저장한_몸_재료로_같은_몸을_다시_만든다()
        {
            var soul = new Soul(_d);
            soul.EndLife(soul.BeginLife(Rebirth.Prologue(_d)).Finish(LifeEndWay.Fallen, 0));
            var c = _d.Cheonha;
            var traits = c.BirthTraits.List.Take(2).ToList();
            var cand = new RebirthCandidate(c.Sect("gaebang"), c.Region(c.Sect("gaebang").Region), traits, female: true);
            var life = new Life(soul, Rebirth.Body(c, cand), new Random(1));
            life.Tick(1f, new Regions(_d).Center(c.Sect("gaebang").Region), 0);
            var back = JsonConvert.DeserializeObject<LifeSnapshot>(JsonConvert.SerializeObject(LifeSnapshot.Capture(life)))!;

            var again = back.Candidate(c)!;
            Assert.Equal(traits.Select(t => t.Id), again.Traits.Select(t => t.Id));
            Assert.True(again.Female);
            var fresh = new Life(soul, Rebirth.Body(c, again), new Random(2));
            fresh.Restore(back);
            Assert.Equal(life.Body.RebirthQiBonus, fresh.Body.RebirthQiBonus);
            Assert.Equal(life.Loadout.Gauge.Value, fresh.Loadout.Gauge.Value);
            // 성질이 다른 같은 문파 몸에는 되돌리지 않는다
            var plain = new Life(soul, Rebirth.Body(c, new RebirthCandidate(c.Sect("gaebang"), cand.Region, Array.Empty<BirthTraitDef>(), female: true)), new Random(3));
            Assert.Throws<InvalidOperationException>(() => plain.Restore(back));
        }

        [Fact] public void 장부_칸이_없는_옛_스냅샷도_읽는다()
        {
            var (_, life) = NewLife();
            life.Tick(0.5f, new Regions(_d).Center("songshan"), 0);
            var json = JsonConvert.SerializeObject(LifeSnapshot.Capture(life));
            var obj = Newtonsoft.Json.Linq.JObject.Parse(json);
            obj.Remove("Tally");
            obj.Remove("Awakened");
            obj.Remove("Boons");
            obj.Remove("Region");
            obj.Remove("Traits");
            var back = obj.ToObject<LifeSnapshot>()!;
            Assert.Null(back.Tally);
            var (_, fresh) = NewLife();
            fresh.Restore(back);
            Assert.Equal(0, fresh.Log.Kills);
            Assert.Null(back.Candidate(_d.Cheonha));   // 몸 재료가 없는 옛 저장은 문파만 맞으면 되돌린다
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
