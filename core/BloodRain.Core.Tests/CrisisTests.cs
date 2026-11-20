using System;
using System.Linq;
using System.Numerics;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Run;
using BloodRain.Core.World;
using Newtonsoft.Json;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>GU-11m(2026-11-20 사람이 추천대로 — decisions #65): 문파의 위기를 막는 판정과 못 막았을 때.
    /// 막음 = 사건 시각에 그 자리에 있고 그 뒤 60초를 그 반경 안에서 버팀 / 강시 문파 땅 혈운 +1.0 / 무너진 위기마다 천하 +0.1 / 처음 막은 사건 윤회의 기 150.</summary>
    public class CrisisTests
    {
        readonly GameData _d = TestData.Load();

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

        Vector2 Songshan => new Regions(_d).Center("songshan");

        [Fact] public void 값은_결정대로()
        {
            var k = _d.Chronicle.Crisis;
            Assert.Equal(60f, k.HoldSeconds);
            Assert.Equal(1.0f, k.FallenRegionDensity);
            Assert.Equal(0.1f, k.FallenEachDensity);
            Assert.Equal(150, _d.Cheonha.RebirthQi.CrisisFirstBlock);
        }

        [Fact] public void 그_자리에서_60초를_버티면_막고_처음이면_윤회의_기_150()
        {
            var (soul, life) = NewLife();
            while (life.Minutes < 11.75f) life.Tick(0.5f, Songshan, 0);   // 숭산 범종 11:40
            Assert.Equal(CrisisState.Present, life.Crisis("songshan_bell"));
            while (life.Minutes < 12.60f) life.Tick(0.5f, Songshan, 0);   // 12:36 — 아직 56초
            Assert.Equal(CrisisState.Present, life.Crisis("songshan_bell"));
            while (life.Minutes < 12.70f) life.Tick(0.5f, Songshan, 0);   // 12:42 — 60초 넘김
            Assert.Equal(CrisisState.Blocked, life.Crisis("songshan_bell"));
            Assert.DoesNotContain("shaolin", life.FallenSects);
            Assert.Equal(new[] { ("songshan_bell", 150) }, life.TakeCrisesBlocked());
            Assert.Empty(life.TakeCrisesBlocked());
            Assert.Equal(150, life.Log.Qi.Settle(0).BonusOf(RebirthQiBonusKind.CrisisFirstBlock));
            Assert.Contains(life.Log.Qi.Settle(0).Lines, l => l.Kind == RebirthQiLineKind.CrisisFirstBlock && l.Count == 1 && l.Amount == 150);

            soul.EndLife(life.End(LifeEndWay.Fallen, 0));
            Assert.Contains("songshan_bell", soul.CrisesBlocked);
            var back = Soul.FromJson(_d, soul.ToJson());
            Assert.Contains("songshan_bell", back.CrisesBlocked);

            // 다음 생에 또 막으면 판정은 같지만 윤회의 기는 없다
            var (_, again) = NewLife(back);
            while (again.Minutes < 12.70f) again.Tick(0.5f, Songshan, 0);
            Assert.Equal(CrisisState.Blocked, again.Crisis("songshan_bell"));
            Assert.Equal(new[] { ("songshan_bell", 0) }, again.TakeCrisesBlocked());
            Assert.Equal(0, again.Log.Qi.Settle(0).BonusOf(RebirthQiBonusKind.CrisisFirstBlock));
        }

        [Fact] public void 버티다_반경_밖으로_나가면_무너진다()
        {
            var (_, life) = NewLife();
            while (life.Minutes < 11.75f) life.Tick(0.5f, Songshan, 0);
            Assert.Equal(CrisisState.Present, life.Crisis("songshan_bell"));
            var far = Songshan + new Vector2(life.Scale.ViewDiagonal(life.Level), 0);
            life.Tick(0.5f, far, 0);
            Assert.Equal(CrisisState.Fallen, life.Crisis("songshan_bell"));
            Assert.Contains("shaolin", life.FallenSects);
            // 돌아와도 되돌릴 수 없다
            while (life.Minutes < 13f) life.Tick(0.5f, Songshan, 0);
            Assert.Equal(CrisisState.Fallen, life.Crisis("songshan_bell"));
            Assert.Empty(life.TakeCrisesBlocked());
        }

        [Fact] public void 무너진_위기마다_천하_혈운이_짙어지고_그_땅은_더_짙다()
        {
            var (_, life) = NewLife();
            Assert.Equal(0f, life.CrisisDensity("songshan"));
            float before = life.Density;
            while (life.Minutes < 6.1f) life.Tick(0.5f, Songshan, 0);        // 개방 총타(6:00) 무너짐
            Assert.Equal(0.1f, life.CrisisDensity("songshan"), 4);
            Assert.Equal(1.1f, life.CrisisDensity("gaebang_hq"), 4);
            while (life.Minutes < 10.1f) life.Tick(0.5f, Songshan, 0);       // 무당 결계(10:00)도
            Assert.Equal(0.2f, life.CrisisDensity("songshan"), 4);
            Assert.Equal(1.2f, life.CrisisDensity("wudangsan"), 4);
            Assert.Equal(new Regions(_d).Density(life.Position, life.SealStones) + 0.2f, life.Density, 4);
            Assert.True(life.Density > before);
        }

        [Fact] public void 생_도중_저장으로_버틴_초와_막음이_돌아온다()
        {
            var (_, life) = NewLife();
            while (life.Minutes < 12.0f) life.Tick(0.5f, Songshan, 0);       // 범종 20초쯤 버팀
            float held = life.CrisisHeld["songshan_bell"];
            Assert.InRange(held, 15f, 25f);
            var snap = JsonConvert.DeserializeObject<LifeSnapshot>(JsonConvert.SerializeObject(LifeSnapshot.Capture(life)))!;
            var (_, fresh) = NewLife();
            fresh.Restore(snap);
            Assert.Equal(held, fresh.CrisisHeld["songshan_bell"], 3);
            Assert.Equal(CrisisState.Present, fresh.Crisis("songshan_bell"));
            Assert.Equal(life.FallenSects, fresh.FallenSects);
            while (fresh.Minutes < 12.70f) fresh.Tick(0.5f, Songshan, 0);
            Assert.Equal(CrisisState.Blocked, fresh.Crisis("songshan_bell"));

            var snap2 = JsonConvert.DeserializeObject<LifeSnapshot>(JsonConvert.SerializeObject(LifeSnapshot.Capture(fresh)))!;
            var (_, third) = NewLife();
            third.Restore(snap2);
            Assert.Equal(CrisisState.Blocked, third.Crisis("songshan_bell"));
            Assert.Equal(150, third.Log.Qi.Settle(0).BonusOf(RebirthQiBonusKind.CrisisFirstBlock));
        }
    }
}
