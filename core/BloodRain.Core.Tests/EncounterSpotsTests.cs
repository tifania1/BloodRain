using System;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Spawning;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>기연 사건을 판에 놓는 셈(기획서 20장 기연, synergies.json encounters[].spot, EN-01). 웹 한 생 판과 같은 값.</summary>
    public class EncounterSpotsTests
    {
        readonly GameData _d = TestData.Load();
        EncounterDef E(string id) => _d.Synergies.Encounter(id);

        [Fact] public void 벼랑만_빼고_다_놓는_값이_있다()
        {
            foreach (var e in _d.Synergies.Encounters)
            {
                if (e.Id == "cliff") { Assert.Null(e.Spot); continue; }
                Assert.NotNull(e.Spot);
                Assert.True(e.Spot!.AppearToMinute >= e.Spot.AppearFromMinute, e.Id);
                Assert.True(e.Spot.AppearFromMinute >= e.MinMinute, $"{e.Id} 는 minMinute 전에 나올 수 없다");
            }
        }

        [Fact] public void 웹_프로토타입에서_돌던_값과_같다()
        {
            Assert.Equal((2f, 5f, 9f, 1.3f, 0f, 1f), Tup(E("relic")));
            Assert.Equal((5f, 11f, 11f, 1.7f, 0f, 0.5f), Tup(E("hermit")));
            Assert.Equal((3f, 14f, 10f, 1.3f, 0f, 0f), Tup(E("elixir")));
            Assert.Equal((15f, 20f, 10f, 3f, 8f, 0f), Tup(E("whisper")));
            Assert.Equal((3f, 14f, 10f, 1.5f, 2.5f, 0f), Tup(E("sword_tomb")));
            Assert.False(EncounterSpots.OnGround(E("letter")));
            Assert.False(EncounterSpots.OnGround(E("spiritbeast")));
        }

        static (float, float, float, float, float, float) Tup(EncounterDef e) =>
            (e.Spot!.AppearFromMinute, e.Spot.AppearToMinute, e.Spot.RingRadius, e.Spot.ReachRadius, e.Spot.LeaveRadius, e.Spot.DecayPerSecond);

        [Fact] public void 나올_분은_창_안이고_자리는_고리_위()
        {
            var rng = new Random(3);
            var h = E("hermit");
            for (int i = 0; i < 300; i++)
            {
                Assert.InRange(EncounterSpots.AppearMinute(h, rng), 5f, 11f);
                var (x, y) = EncounterSpots.PlaceOffset(h, rng);
                Assert.Equal(11f, MathF.Sqrt(x * x + y * y), 3);
            }
            Assert.Equal((0f, 0f), EncounterSpots.PlaceOffset(E("letter"), rng));
            Assert.Equal(0f, EncounterSpots.AppearMinute(E("cliff"), rng));
        }

        [Fact] public void 닿기와_떠나기는_경계를_뺀다()
        {
            var w = E("whisper");
            Assert.True(EncounterSpots.Reached(w, 2.99f));
            Assert.False(EncounterSpots.Reached(w, 3f));
            Assert.False(EncounterSpots.Left(w, 8f));
            Assert.True(EncounterSpots.Left(w, 8.01f));
            Assert.False(EncounterSpots.Left(E("elixir"), 100f));
        }

        [Fact] public void 은거기인은_벗어나면_초당_절반씩_준다()
        {
            var h = E("hermit");
            float p = 0f;
            for (int i = 0; i < 40; i++) p = EncounterSpots.Progress(h, p, 0.1f, true);   // 4초 머묾
            Assert.Equal(4f, p, 3);
            for (int i = 0; i < 20; i++) p = EncounterSpots.Progress(h, p, 0.1f, false);  // 2초 벗어남
            Assert.Equal(3f, p, 3);
            Assert.Equal(0f, EncounterSpots.Progress(E("relic"), 0.2f, 1f, false));
        }

        [Fact] public void 웹_프로토타입이_같은_값을_읽는다()
        {
            var html = TestData.ReadRepoFile("web", "prototype", "template.html");
            Assert.Contains("o[key] = { at: rand(sp.appearFromMinute, sp.appearToMinute)", html);
            Assert.Contains("R = ENC_BY[key].spot.ringRadius;", html);
            Assert.Contains("const encProg = (key, prog, dt, inReach) =>", html);
            foreach (var id in new[] { "hermit", "relic", "elixir", "whisper", "sword_tomb" })
                Assert.Contains($"SPOT.{id}.reachRadius", html);
            Assert.DoesNotContain("key === 'hermit' ? 11 :", html);
        }
    }
}
