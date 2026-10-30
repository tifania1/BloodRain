using System;
using System.Linq;
using System.Numerics;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Spawning;
using BloodRain.Core.World;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>2026-10-30 기획 결정 반영(T-29). 기획서 7·10·21장.</summary>
    public class DecisionsT29Tests
    {
        readonly GameData _d = Load();

        BossAwakening Songshan() => new BossAwakening(_d, "songshan", new BossRecord());

        [Fact] public void 혈운_기둥_원은_화면_대각선_0점15배()
        {
            var b = Songshan();
            var scale = new Scale(_d);
            Assert.Equal(scale.ViewDiagonal(35) * 0.15f, b.EngageRadius(35), 2);
        }

        [Fact] public void 깨어날_때_이미_원_안이면_5초_경고_뒤에_싸운다()
        {
            var b = Songshan();
            var center = new Regions(_d).Center("songshan");
            b.UpdateRealm(35);
            Assert.Null(b.Tick(0.1f, center, 35, 12));
            Assert.Equal("songshan", b.GraceRegionId);
            float t = 0.1f;
            BossFight? fight = null;
            while (fight == null && t < 10f) { fight = b.Tick(0.1f, center, 35, 12); t += 0.1f; }
            Assert.NotNull(fight);
            Assert.InRange(t, 5f, 5.3f);
        }

        [Fact] public void 경고_중에_원_밖으로_나가면_미루고_다시_들어서면_바로_싸운다()
        {
            var b = Songshan();
            var regions = new Regions(_d);
            var center = regions.Center("songshan");
            b.UpdateRealm(35);
            b.Tick(0.1f, center, 35, 12);
            var away = center + new Vector2(b.EngageRadius(35) * 2, 0);
            Assert.Null(b.Tick(0.1f, away, 35, 12));
            Assert.Null(b.GraceRegionId);
            Assert.Equal(BossRegionState.Awake, b.State("songshan"));
            Assert.NotNull(b.Tick(0.1f, center, 35, 13));
        }

        [Fact] public void 깨어날_때_원_밖이면_들어서는_순간_싸운다()
        {
            var b = Songshan();
            var center = new Regions(_d).Center("songshan");
            b.UpdateRealm(35);
            var away = center + new Vector2(b.EngageRadius(35) * 2, 0);
            Assert.Null(b.Tick(0.1f, away, 35, 12));
            Assert.NotNull(b.Tick(0.1f, center, 35, 12.1f));
        }

        [Fact] public void 아래_경지_정예는_밟혀도_보갑_잡몹은_아니다()
        {
            var s = new Scale(_d);
            Assert.True(s.CrushedDropsChest(elite: true));
            Assert.False(s.CrushedDropsChest(elite: false));
        }

        [Fact] public void 다시_오른_경지_돌파는_0점7초에_끝나고_생성_멈춤도_같다()
        {
            var bt = new Breakthrough(_d);
            bt.Start(9.9f, 10f, revisit: true);
            Assert.Equal(0.7f, bt.DurationSeconds, 3);
            bt.Tick(0.69f);
            Assert.True(bt.IsActive);
            Assert.Equal(_d.Cheonha.Breakthrough.TimeScale, bt.WorldTimeScale);
            bt.Tick(0.02f);
            Assert.False(bt.IsActive);

            var first = new Breakthrough(_d);
            first.Start(9.9f, 10f);
            first.Tick(1.39f);
            Assert.True(first.IsActive); // 처음 오른 경지는 1.4초 내내 0.05배
            Assert.Equal(0.05f, first.WorldTimeScale, 3);

            var ld = new LifeDirector(_d, new Random(1));
            ld.OnBreakthrough(revisit: true);
            Assert.Equal(0.7f, ld.HoldRemaining, 3);
        }

        [Fact] public void 혈월_천하에선_윤회의_기와_혈정이_1점5배()
        {
            var soul = new Soul(_d);
            soul.EndLife(soul.BeginLife(Rebirth.Prologue(_d)).Finish("fallen", 0));
            soul.Regress();
            Assert.True(soul.IsBloodMoon);
            var c = _d.Cheonha;
            var body = Rebirth.Body(c, new RebirthCandidate(c.Sect("shaolin"), c.Region("songshan"), Array.Empty<BirthTraitDef>()));
            var log = soul.BeginLife(body);
            Assert.Equal(1.5f, log.RewardMultiplier);
            var kill = log.KillBoss("songshan");
            Assert.Equal((int)Math.Round(c.Bosses.FirstKill.BloodCrystal * 1.5, MidpointRounding.AwayFromZero), kill.BloodCrystal);
            var res = log.Qi.Settle(0);
            Assert.True(res.FromBloodMoon > 0);
            Assert.Equal(res.Subtotal + res.FromTraits + res.FromBloodMoon, res.Total);
            Assert.Equal((int)Math.Round((res.Subtotal + res.FromTraits) * 0.5, MidpointRounding.AwayFromZero), res.FromBloodMoon);
            Assert.Contains(res.Lines, l => l.Kind == RebirthQiLineKind.BloodMoon);
            Assert.Equal(res.Total, res.Lines.Sum(l => l.Amount));
        }

        [Fact] public void 첫_회차에선_혈월_줄이_없다()
        {
            var soul = new Soul(_d);
            var log = soul.BeginLife(Rebirth.Prologue(_d));
            Assert.Equal(1f, log.RewardMultiplier);
            var res = log.Qi.Settle(100);
            Assert.Equal(0, res.FromBloodMoon);
            Assert.DoesNotContain(res.Lines, l => l.Kind == RebirthQiLineKind.BloodMoon);
        }

        [Fact] public void 숨은_봉마곡은_봉인석이_모자라면_농도_섞기에_안_든다()
        {
            var r = new Regions(_d);
            var bong = r.Center("bongmagok");
            Assert.True(r.Density(bong, sealStones: 0) < 5f);
            Assert.Equal(5f, r.Density(bong, sealStones: 7), 3);
        }
    }
}
