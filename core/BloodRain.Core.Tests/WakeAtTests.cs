using System;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Progression;
using BloodRain.Core.Run;
using Newtonsoft.Json.Linq;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>침방 '그 경지로 깨어나기'(기획서 10장 '천하 확장: 전생의 공력', WX-01U ②)를 생에 거는 Life.WakeAt.</summary>
    public class WakeAtTests
    {
        readonly GameData _d = TestData.Load();
        CheonhaDef C => _d.Cheonha;

        // 서장을 마친 혼에 구간을 넘은 횟수를 넣는다(저장 칸 RealmCrossings).
        Soul SoulWith(params (string realm, int n)[] crossings)
        {
            var s = new Soul(_d);
            s.EndLife(s.BeginLife(Rebirth.Prologue(_d)).Finish(LifeEndWay.Fallen, 0));
            var j = JObject.Parse(s.ToJson());
            var c = new JObject();
            foreach (var (realm, n) in crossings) c[realm] = n;
            j["RealmCrossings"] = c;
            return Soul.FromJson(_d, j.ToString());
        }

        Life NewLife(Soul s)
        {
            var body = Rebirth.Body(C, new RebirthCandidate(C.Sect("shaolin"), C.Region(C.Sect("shaolin").Region), Array.Empty<BirthTraitDef>()));
            return new Life(s, body, new Random(1));
        }

        [Fact] public void 일류로_깨어나면_문턱_레벨에서_건너뛴_레벨업을_돌려준다()
        {
            var life = NewLife(SoulWith(("iryu", 31), ("ilryu", 30)));
            Assert.Equal(new[] { "iryu", "ilryu" }, life.Soul.PastLifePower().WakeRealms());
            var r = life.WakeAt("ilryu");
            int ilryu = _d.Balance.Realms.Single(x => x.Id == "ilryu").Level;
            Assert.Equal(ilryu, life.Level);
            Assert.Equal(ilryu - 1, r.Xp.LevelUps);
            Assert.Equal(new[] { "iryu", "ilryu" }, r.Breakthroughs.Select(b => b.Realm).ToArray());
            Assert.Equal(0, life.Xp.Xp);
            Assert.Equal(0, life.Xp.Total);   // 진기는 먹지 않는다(윤회의 기·정산에 안 들어감)
            Assert.False(life.Breakthrough.IsActive); // 물러나는 연출 없이 처음부터 그 축척
            Assert.Equal("ilryu", life.WokeRealm);
        }

        [Fact] public void 넘은_횟수가_모자란_경지는_고를_수_없다()
        {
            var life = NewLife(SoulWith(("iryu", 30), ("ilryu", 29)));
            Assert.Throws<ArgumentException>(() => life.WakeAt("ilryu"));
            Assert.Equal(1, life.Level);
            life.WakeAt("iryu");
            Assert.Throws<InvalidOperationException>(() => life.WakeAt("iryu")); // 한 번만
        }

        [Fact] public void 절정으로_깨어나면_땅의_주인이_깨어난다()
        {
            var life = NewLife(SoulWith(("iryu", 30), ("ilryu", 30), ("jeoljeong", 30)));
            var r = life.WakeAt("jeoljeong");
            Assert.Equal(_d.Balance.Realms.Single(x => x.Id == "jeoljeong").Level, life.Level);
            Assert.NotEmpty(r.WokenBosses);
            Assert.NotEmpty(life.Bosses.Awake);
        }

        [Fact] public void 대환단과_같이_쓰면_높은_쪽()
        {
            var life = NewLife(SoulWith(("iryu", 30)));
            var elixir = C.Inn.Elixirs.Single(e => e.Id == "daehwandan");
            life.WakeAt("iryu");
            var e = life.TakeElixirs(new[] { elixir });
            Assert.Equal(_d.Balance.Realms.Single(x => x.Id == "iryu").Level, life.Level);
            Assert.Equal(0, e.LevelUps);
        }

        [Fact] public void RunXp_JumpTo_는_낮은_레벨이면_그대로()
        {
            var xp = new RunXp(new XpCurve(_d.Balance.XpCurve), new Realms(_d.Balance.Realms));
            xp.JumpTo(12);
            var r = xp.JumpTo(5);
            Assert.Equal(12, xp.Level);
            Assert.Equal(0, r.LevelUps);
        }
    }
}
