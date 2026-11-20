using System;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Run;
using BloodRain.Core.World;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>DM-01(2026-11-20 사람이 추천대로 — decisions #41): 넥스트 페스트 데모 범위.</summary>
    public class DemoTests
    {
        readonly GameData _d = TestData.Load();

        [Fact] public void 데모_범위는_결정대로()
        {
            var demo = _d.Cheonha.Demo;
            Assert.Equal(new[] { "shaolin", "gaebang", "namgung" }, demo.Sects);
            Assert.Equal(new[] { "songshan" }, demo.Bosses);
            foreach (var s in demo.Sects) Assert.Contains(_d.Cheonha.Sects, x => x.Id == s);
            foreach (var r in demo.Bosses) Assert.NotNull(_d.Cheonha.Regions.First(x => x.Id == r).Boss);
            Assert.Equal(9, _d.Weapons.Count(w => w.Unlock == "start"));   // 16장 '무공 9종'
        }

        [Fact] public void 데모_혼은_세_문파와_처음_무공만_열리고_저장해도_데모다()
        {
            var soul = Soul.NewDemo(_d);
            Assert.True(soul.Demo);
            foreach (var w in _d.Weapons) soul.Unlock(w.Unlock);
            Assert.Equal(new[] { "shaolin", "gaebang", "namgung" }, soul.UnlockedSects);
            Assert.Equal(_d.Weapons.Where(w => w.Unlock == "start").Select(w => w.Id).OrderBy(x => x), soul.UnlockedWeapons().OrderBy(x => x));
            var back = Soul.FromJson(_d, soul.ToJson());
            Assert.True(back.Demo);
            Assert.False(Soul.FromJson(_d, new Soul(_d).ToJson()).Demo);

            var full = new Soul(_d);
            foreach (var w in _d.Weapons) full.Unlock(w.Unlock);
            Assert.True(full.UnlockedWeapons().Count > 9);
        }

        [Fact] public void 데모_생에서는_숭산_땅의_주인만_있다()
        {
            var soul = Soul.NewDemo(_d);
            soul.EndLife(soul.BeginLife(Rebirth.Prologue(_d)).Finish(LifeEndWay.Fallen, 0));
            var c = _d.Cheonha;
            var life = new Life(soul, Rebirth.Body(c, new RebirthCandidate(c.Sect("shaolin"), c.Region("songshan"), Array.Empty<BirthTraitDef>())), new Random(1));
            Assert.Equal(new[] { "songshan" }, life.Bosses.BossRegions.Select(r => r.Id));
            var fullSoul = new Soul(_d);
            fullSoul.EndLife(fullSoul.BeginLife(Rebirth.Prologue(_d)).Finish(LifeEndWay.Fallen, 0));
            var fullLife = new Life(fullSoul, Rebirth.Body(c, new RebirthCandidate(c.Sect("shaolin"), c.Region("songshan"), Array.Empty<BirthTraitDef>())), new Random(1));
            Assert.True(fullLife.Bosses.BossRegions.Count > 1);
        }
    }
}
