using System;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Run;
using Newtonsoft.Json;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>WP-29a(2026-11-20 사람이 추천대로 — decisions #36): 마교 몸이 아닐 때 혈마의 속삭임으로 얻는 '혈공'은 마교 혈공을 그 생에만 그대로 켠다.</summary>
    public class WhisperArtTests
    {
        readonly GameData _d = TestData.Load();

        Life NewLife(string sect, string region)
        {
            var soul = new Soul(_d);
            soul.EndLife(soul.BeginLife(Rebirth.Prologue(_d)).Finish(LifeEndWay.Fallen, 0));
            var c = _d.Cheonha;
            return new Life(soul, Rebirth.Body(c, new RebirthCandidate(c.Sect(sect), c.Region(region), Array.Empty<BirthTraitDef>())), new Random(1));
        }

        [Fact] public void 소림_몸도_속삭임을_받으면_마교_혈공이_켜진다()
        {
            var life = NewLife("shaolin", "songshan");
            Assert.Null(life.BloodArt);
            Assert.True(life.AcceptWhisper());
            Assert.NotNull(life.BloodArt);
            var demonic = _d.Cheonha.Sects.First(s => s.BloodArt != null).BloodArt!;
            Assert.Equal(demonic.YinPerStep, life.BloodArt!.YinPerStep);
            Assert.Equal(new BloodRain.Core.Progression.BloodArt(demonic, new BloodRain.Core.Progression.YinYangGauge(_d.Balance.Yinyang), 0).XpMultiplier(4f),
                life.BloodArt.XpMultiplier(4f), 6);
            Assert.False(life.AcceptWhisper()); // 두 번은 없다
        }

        [Fact] public void 마교_몸은_제_혈공_그대로()
        {
            var sect = _d.Cheonha.Sects.First(s => s.BloodArt != null);
            var life = NewLife(sect.Id, sect.Region!);
            var art = life.BloodArt;
            Assert.NotNull(art);
            life.AcceptWhisper();
            Assert.Same(art, life.BloodArt);
        }

        [Fact] public void 생_도중_저장에서도_빌린_혈공이_돌아온다()
        {
            var life = NewLife("shaolin", "songshan");
            life.AcceptWhisper();
            var snap = JsonConvert.DeserializeObject<LifeSnapshot>(JsonConvert.SerializeObject(LifeSnapshot.Capture(life)))!;
            var fresh = NewLife("shaolin", "songshan");
            fresh.Restore(snap);
            Assert.NotNull(fresh.BloodArt);
        }
    }
}
