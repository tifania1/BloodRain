using System;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>WP-43(2026-11-20 추천대로, decisions #40): Steam 업적 22개. 기획서 10장 'Steam 업적' 표와 cheonha.json achievements,
    /// 판정은 Meta.Achievements 가 혼으로, 얻은 것은 혼 해금 토큰 achievement:&lt;id&gt; 로 남는다.</summary>
    public class AchievementTests
    {
        readonly GameData _d = TestData.Load();
        CheonhaDef C => _d.Cheonha;

        Soul AfterPrologue()
        {
            var s = new Soul(_d);
            s.EndLife(s.BeginLife(Rebirth.Prologue(_d)).Finish("fallen", 0));
            return s;
        }

        LifeSummary Live(Soul s, string sect, string realm = "samryu", string[]? bosses = null) =>
            s.EndLife(new LifeRecord(sect, "fallen", new RebirthQiLedger(C.RebirthQi).Settle(0), bosses ?? Array.Empty<string>(),
                new[] { C.Sect(sect).Region }, realm, Array.Empty<string>()));

        [Fact] public void 업적은_22개이고_조건이_데이터를_가리킨다()
        {
            var a = C.Achievements;
            Assert.Equal(22, a.Count);
            Assert.Equal(a.Count, a.Select(x => x.Id).Distinct().Count());
            foreach (var x in a)
            {
                Assert.False(string.IsNullOrEmpty(x.NameKo), x.Id);
                Assert.False(string.IsNullOrEmpty(x.DescKo), x.Id);
                var (kind, arg) = Achievements.Parse(x.When);
                switch (kind)
                {
                    case "realm": Assert.Contains(_d.Balance.Realms, r => r.Id == arg && r.Id != "samryu"); break;
                    case "boss": Assert.Contains(C.BossRegions, r => r.Id == arg); break;
                    case "ending": Assert.Contains(arg, C.Cycle.EndingsSecond); break;
                    case "title": Assert.Contains(C.Titles, t => t.Id == arg); break;
                }
            }
            Assert.Equal(6, a.Count(x => x.When.StartsWith("realm:")));
            Assert.Equal(C.BossRegions.Count(), a.Count(x => x.When.StartsWith("boss:")));
            Assert.Equal(3, a.Count(x => x.When.StartsWith("ending:")));
            Assert.Equal(C.Titles.Count, a.Count(x => x.When.StartsWith("title:")));
            Assert.Equal(new[] { "ending_true", "all_secrets" }, a.Where(x => x.Hidden).Select(x => x.Id));
            Assert.Throws<ArgumentException>(() => Achievements.Parse("kills:5000"));
        }

        [Fact] public void 기획서_10장_표와_같다()
        {
            var rows = TestData.GddTable(TestData.GddSection("Steam 업적")).ToList();
            Assert.Equal(C.Achievements.Select(x => x.NameKo), rows.Select(r => r[0]));
            Assert.Equal(C.Achievements.Select(x => x.DescKo), rows.Select(r => r[1]));
            Assert.Equal(C.Achievements.Select(x => x.Hidden ? "숨김" : ""), rows.Select(r => r[2]));
        }

        [Fact] public void 경지_첫_돌파는_그_아래_경지까지_같이()
        {
            var s = AfterPrologue();
            Assert.Empty(Live(s, "shaolin").NewAchievements);
            Assert.Equal(new[] { "realm_iryu", "realm_ilryu" }, Live(s, "shaolin", "ilryu").NewAchievements);
            Assert.Empty(Live(s, "shaolin", "ilryu").NewAchievements); // 한 번만
            Assert.True(Achievements.IsEarned(s, "realm_ilryu"));
        }

        [Fact] public void 봉인석_일곱은_회귀해도_남는다()
        {
            var s = AfterPrologue();
            var all = C.BossRegions.Select(r => r.Id).ToArray();
            var got = Live(s, "shaolin", "hyeongyeong", all).NewAchievements;
            Assert.Contains("all_seal_stones", got);
            Assert.Equal(7, got.Count(id => id.StartsWith("lord_")));
            s.Regress();
            Assert.Equal(0, s.SealStoneCount);
            Assert.Contains("all_seal_stones", Achievements.Earned(s));
        }

        [Fact] public void 비전_여덟은_마지막_쪽을_찾는_그_자리에서()
        {
            var s = AfterPrologue();
            var ids = _d.Synergies.Secrets.Select(x => x.Id).ToList();
            foreach (var id in ids.Take(ids.Count - 1)) s.Discover(CodexKey.Secret(id));
            Assert.False(Achievements.IsEarned(s, "all_secrets"));
            s.Discover(CodexKey.Secret(ids.Last()));
            Assert.True(Achievements.IsEarned(s, "all_secrets"));
        }

        [Fact] public void 업적이_없던_저장도_읽을_때_채운다()
        {
            var s = AfterPrologue();
            Live(s, "shaolin", "iryu");
            var json = s.ToJson().Replace("\"" + Achievements.Token("realm_iryu") + "\"", "\"x\"");
            Assert.DoesNotContain(Achievements.Token("realm_iryu"), json);
            var back = Soul.FromJson(_d, json);
            Assert.Equal(new[] { "realm_iryu" }, Achievements.Earned(back));
        }

        [Fact] public void 웹_합친_판도_데이터의_조건_갈래를_다_안다()
        {
            // LG-06(2026-11-25): 웹 achMeets 가 Core Achievements.Meets 와 같은 갈래를 본다. 데이터에 새 갈래가 생기면 웹도 같이.
            var html = TestData.ReadRepoFile("web", "prototype", "template.html");
            var build = TestData.ReadRepoFile("tools", "build_web_prototype.py");
            Assert.Contains("\"achievements\": [drop_notes(a) for a in load(\"cheonha\").get(\"achievements\", [])]", build);
            foreach (var kind in C.Achievements.Select(a => Achievements.Parse(a.When).kind).Distinct())
                Assert.Contains($"case '{kind}':", html);
            Assert.Contains("m.newAch = achRefresh(m);", html);
            Assert.Contains("'achievement:' + a.id", html);
            Assert.Equal("achievement:x", Achievements.Token("x"));
        }
    }
}
