using System;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using Newtonsoft.Json.Linq;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>EL-08: 장경각 비급 도감(20장 '장경각 비급 도감과 발견 연출')의 찾은 쪽을 혼에 남기고 도감 차례로 낸다.</summary>
    public class CodexTests
    {
        readonly GameData _d = Load();

        [Fact] public void 도감은_기획서대로_41쪽()
        {
            Assert.Contains("도감은 연환 16, 진전 9, 비전 8, 기연 8, 모두 41쪽입니다.", Gdd);
            var c = Codex.Counts(new Soul(_d));
            Assert.Equal((0, 16), c[CodexGroup.Link]);
            Assert.Equal((0, 9), c[CodexGroup.School]);
            Assert.Equal((0, 8), c[CodexGroup.Secret]);
            Assert.Equal((0, 8), c[CodexGroup.Encounter]);
            Assert.Equal(new[] { CodexGroup.Link, CodexGroup.School, CodexGroup.Secret, CodexGroup.Encounter }, Codex.Pages(new Soul(_d)).Select(p => p.Group).Distinct());
        }

        [Fact] public void 찾으면_처음만_true_없는_쪽은_예외()
        {
            var soul = new Soul(_d);
            var link = _d.Synergies.Links[0].Id;
            Assert.True(soul.Discover(CodexKey.Link(link)));
            Assert.False(soul.Discover(CodexKey.Link(link)));
            Assert.Throws<ArgumentException>(() => soul.Discover("link:없는_연환"));
            Assert.Throws<ArgumentException>(() => soul.Discover(CodexKey.School(link)));   // 연환 id 를 진전으로
            Assert.Equal((1, 16), Codex.Counts(soul)[CodexGroup.Link]);
        }

        [Fact] public void 저장에_남고_옛_저장과_없는_키는_빈다()
        {
            var soul = new Soul(_d);
            var secret = _d.Synergies.Secrets[1].Id;
            soul.Discover(CodexKey.Secret(secret));
            soul.Discover(CodexKey.Encounter(_d.Synergies.Encounters[0].Id));
            var json = soul.ToJson();
            var back = Soul.FromJson(_d, json);
            Assert.Equal(soul.Codex.OrderBy(x => x), back.Codex.OrderBy(x => x));
            Assert.Equal(new[] { secret }, Codex.FoundSecrets(back));
            Assert.Equal(json, back.ToJson());

            var old = JObject.Parse(json); old.Remove("codex");
            Assert.Empty(Soul.FromJson(_d, old.ToString()).Codex);
            var bad = JObject.Parse(json); bad["codex"] = new JArray("link:지운_연환");
            Assert.Empty(Soul.FromJson(_d, bad.ToString()).Codex);
        }

        [Fact] public void 밀서로_본_비전은_못_찾았어도_표시()
        {
            var j = JObject.Parse(new Soul(_d).ToJson());
            var id = _d.Synergies.Secrets[0].Id;
            j["letters"] = new JArray(id);
            var page = Codex.Pages(Soul.FromJson(_d, j.ToString())).Single(p => p.Group == CodexGroup.Secret && p.Id == id);
            Assert.Equal((false, true), (page.Found, page.LetterSeen));
        }

        [Fact] public void 키는_웹_도감과_같다()
        {
            var html = ReadRepoFile("web", "prototype", "template.html");
            Assert.Contains("discover('link:' + l.id)", html);
            Assert.Contains("discover('school:' + sc.id)", html);
            Assert.Contains("discover('secret:' + id)", html);
            Assert.Contains("discover('enc:hermit')", html);
            Assert.Equal(("link:a", "school:a", "secret:a", "enc:a"), (CodexKey.Link("a"), CodexKey.School("a"), CodexKey.Secret("a"), CodexKey.Encounter("a")));
            foreach (var e in _d.Synergies.Encounters.Where(e => html.Contains($"discover('enc:{e.Id}')")))
                Assert.True(CodexKey.IsValid(_d.Synergies, CodexKey.Encounter(e.Id)));
        }
    }
}
