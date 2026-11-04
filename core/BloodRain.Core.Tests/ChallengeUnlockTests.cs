using System;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using Newtonsoft.Json.Linq;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>도전 과제 해금 셋(기획서 10장 '해금과 도전 과제'): 음공 한 생 처치 5,000, 이화창 한 생 경공 300, 뇌부 정신부 누적 10.
    /// 한 생 기록은 LifeLog.Kill·Dash·UseTalisman, 판정은 Soul.EndLife.</summary>
    public class ChallengeUnlockTests
    {
        readonly GameData _d = TestData.Load();
        CheonhaDef C => _d.Cheonha;

        RebirthBody Body(string sect) => Rebirth.Body(C, new RebirthCandidate(C.Sect(sect), C.Region(C.Sect(sect).Region), new System.Collections.Generic.List<BirthTraitDef>()));

        Soul AfterPrologue()
        {
            var s = new Soul(_d);
            s.EndLife(s.BeginLife(Rebirth.Prologue(_d)).Finish("fallen", 0));
            return s;
        }

        /// <summary>소림 몸으로 한 생: 처치·경공·정신부 수만 채워 끝낸다.</summary>
        LifeSummary Live(Soul s, int kills = 0, int dashes = 0, int talismans = 0)
        {
            var life = s.BeginLife(Body("shaolin"));
            life.Kill(kills);
            for (int i = 0; i < dashes; i++) life.Dash();
            for (int i = 0; i < talismans; i++) life.UseTalisman();
            return s.EndLife(life.Finish("fallen", 0));
        }

        [Fact] public void 데이터의_도전_과제_토큰은_기획서_10장_표와_같다()
        {
            var rows = TestData.GddTable(TestData.GddSection("해금과 도전 과제")).ToDictionary(r => r[1], r => r[0]);
            var expected = new (string weapon, ChallengeCounter counter, int count, bool oneRun, string row)[]
            {
                ("sonic_qin", ChallengeCounter.Kills, 5000, true, "한 생에서 적 5,000 처치"),
                ("pear_spear", ChallengeCounter.Dashes, 300, true, "한 생에서 경공 300회"),
                ("thunder_talisman", ChallengeCounter.Talismans, 10, false, "정신부 누적 10회 사용"),
            };
            foreach (var e in expected)
            {
                var w = _d.Weapon(e.weapon);
                var c = UnlockToken.ParseChallenge(w.Unlock);
                Assert.True(c.HasValue, $"{w.NameKo} unlock {w.Unlock} 가 도전 과제가 아님");
                Assert.Equal(e.counter, c!.Value.Counter);
                Assert.Equal(e.count, c.Value.Count);
                Assert.Equal(e.oneRun, c.Value.OneRun);
                Assert.Equal(e.row, rows[w.NameKo]);
            }
            // 데이터에 도전 과제 토큰은 이 셋뿐(새로 생기면 위 표와 Soul.UnlockChallenges 를 같이 본다)
            var all = _d.Weapons.Select(w => w.Unlock).Concat(_d.Characters.Select(c => c.Unlock)).Concat(C.Sects.Select(s => s.Unlock))
                .Where(t => UnlockToken.ParseChallenge(t) != null).Distinct().OrderBy(t => t).ToList();
            Assert.Equal(new[] { "dash_300_one_run", "kills_5000_one_run", "seal_talisman_10_total" }, all);
            Assert.Null(UnlockToken.ParseChallenge("start"));
            Assert.Null(UnlockToken.ParseChallenge("boss:songshan"));
            Assert.Null(UnlockToken.ParseChallenge("talk:unnang_haomun"));
        }

        [Fact] public void 음공은_한_생에서_5000_처치()
        {
            var s = AfterPrologue();
            Assert.DoesNotContain("sonic_qin", s.UnlockedWeapons());
            Assert.Empty(Live(s, kills: 4999).NewUnlocks);
            Assert.Empty(Live(s, kills: 4999).NewUnlocks);          // 두 생을 더해도 안 된다
            Assert.DoesNotContain("sonic_qin", s.UnlockedWeapons());
            Assert.Equal(new[] { "kills_5000_one_run" }, Live(s, kills: 5000).NewUnlocks);
            Assert.Contains("sonic_qin", s.UnlockedWeapons());
            Assert.Empty(Live(s, kills: 9000).NewUnlocks);          // 한 번 열면 다시 알리지 않는다
        }

        [Fact] public void 이화창은_한_생에서_경공_300()
        {
            var s = AfterPrologue();
            Assert.Empty(Live(s, dashes: 299).NewUnlocks);
            Assert.DoesNotContain("pear_spear", s.UnlockedWeapons());
            Assert.Equal(new[] { "dash_300_one_run" }, Live(s, dashes: 300).NewUnlocks);
            Assert.Contains("pear_spear", s.UnlockedWeapons());
        }

        [Fact] public void 뇌부는_정신부_누적_10()
        {
            var s = AfterPrologue();
            Assert.Empty(Live(s, talismans: 4).NewUnlocks);
            Assert.Equal(4, s.TalismansUsedTotal);
            Assert.Empty(Live(s, talismans: 5).NewUnlocks);
            Assert.DoesNotContain("thunder_talisman", s.UnlockedWeapons());
            Assert.Equal(new[] { "seal_talisman_10_total" }, Live(s, talismans: 1).NewUnlocks);
            Assert.Contains("thunder_talisman", s.UnlockedWeapons());
            Assert.Equal(10, s.TalismansUsedTotal);
        }

        [Fact] public void 서장_생도_센다_그리고_한_생에_셋을_함께_열_수_있다()
        {
            var s = new Soul(_d);
            var life = s.BeginLife(Rebirth.Prologue(_d));
            life.Kill(3000); life.Kill(2000);
            for (int i = 0; i < 300; i++) life.Dash();
            for (int i = 0; i < 10; i++) life.UseTalisman();
            var sum = s.EndLife(life.Finish("fallen", 0));
            Assert.Equal(new[] { "kills_5000_one_run", "dash_300_one_run", "seal_talisman_10_total" }.OrderBy(x => x), sum.NewUnlocks.OrderBy(x => x));
        }

        [Fact] public void 기록이_정산_기록에_실린다_음수는_막는다()
        {
            var s = AfterPrologue();
            var life = s.BeginLife(Body("shaolin"));
            life.Kill(); life.Kill(4); life.Dash(); life.UseTalisman(); life.UseTalisman();
            var r = life.Finish("fallen", 0);
            Assert.Equal((5, 1, 2), (r.Kills, r.Dashes, r.TalismansUsed));
            Assert.Throws<ArgumentOutOfRangeException>(() => life.Kill(-1));
            var bad = new LifeRecord("shaolin", "fallen", new RebirthQiLedger(C.RebirthQi).Settle(0), Array.Empty<string>(),
                new[] { "songshan" }, "samryu", Array.Empty<string>(), kills: -1);
            Assert.Throws<ArgumentException>(() => s.EndLife(bad));
        }

        [Fact] public void 정신부_누적은_저장_왕복하고_옛_저장은_0이다()
        {
            var s = AfterPrologue();
            Live(s, talismans: 7);
            var json = s.ToJson();
            Assert.Equal(7, (int)JObject.Parse(json)["talismansUsed"]!);
            var back = Soul.FromJson(_d, json);
            Assert.Equal(7, back.TalismansUsedTotal);
            Assert.Equal(json, back.ToJson());
            Assert.Equal(new[] { "seal_talisman_10_total" }, Live(back, talismans: 3).NewUnlocks);

            var old = JObject.Parse(json);
            old.Remove("talismansUsed");
            Assert.Equal(0, Soul.FromJson(_d, old.ToString()).TalismansUsedTotal);
        }
    }
}
