using System;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Run;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>하오문 밀서(WP-40, 기획서 20장 기연): 못 찾은 비전 하나의 조건을 보이고, 본 것은 혼이 계정에 남긴다. 웹 한 생 판 WP-30 과 같은 순서.</summary>
    public class HaomunLetterTests
    {
        readonly GameData _d = TestData.Load();

        Soul AfterPrologue()
        {
            var soul = new Soul(_d);
            soul.EndLife(soul.BeginLife(Rebirth.Prologue(_d)).Finish(LifeEndWay.Fallen, 0));
            return soul;
        }

        Life NewLife(Soul soul)
        {
            var c = _d.Cheonha;
            var cand = new RebirthCandidate(c.Sect("shaolin"), c.Region(c.Sect("shaolin").Region), Array.Empty<BirthTraitDef>());
            return new Life(soul, Rebirth.Body(c, cand), new Random(1));
        }

        [Fact] public void 데이터_순서로_못_찾은_것부터_한_생에_여러_장이면_다음_것()
        {
            var secrets = _d.Synergies.Secrets;
            Assert.True(secrets.Count >= 3);
            var life = NewLife(AfterPrologue());
            var first = life.ReadLetter(new[] { secrets[0].Id });   // 첫째는 이미 찾음
            Assert.Equal(secrets[1].Id, first!.Id);
            Assert.Equal(secrets[2].Id, life.ReadLetter(new[] { secrets[0].Id })!.Id);
            Assert.True(life.Encounters.HasMet(EncounterBoons.Letter));
        }

        [Fact] public void 본_밀서는_다음_생에도_남고_저장을_오간다()
        {
            var soul = AfterPrologue();
            var life = NewLife(soul);
            var x = life.ReadLetter(Array.Empty<string>())!;
            soul.EndLife(life.End(LifeEndWay.Fallen, 0));
            Assert.Contains(x.Id, soul.Letters);

            var loaded = Soul.FromJson(_d, soul.ToJson());
            Assert.Contains(x.Id, loaded.Letters);
            var next = NewLife(loaded).ReadLetter(Array.Empty<string>())!;
            Assert.NotEqual(x.Id, next.Id);
        }

        [Fact] public void 다_봤으면_빈_종이()
        {
            var life = NewLife(AfterPrologue());
            Assert.Null(life.ReadLetter(_d.Synergies.Secrets.Select(s => s.Id)));
        }

        [Fact] public void 속삭임을_받은_생을_혼이_세고_저장을_오간다()
        {
            var soul = AfterPrologue();
            var life = NewLife(soul);
            Assert.True(life.AcceptWhisper());
            Assert.False(life.AcceptWhisper());
            soul.EndLife(life.End(LifeEndWay.Fallen, 0));
            soul.EndLife(NewLife(soul).End(LifeEndWay.Fallen, 0));   // 안 받은 생은 안 셈
            Assert.Equal(1, soul.WhispersTotal);
            Assert.Equal(1, Soul.FromJson(_d, soul.ToJson()).WhispersTotal);
        }

        [Fact] public void 옛_저장은_밀서가_없다()
        {
            var json = AfterPrologue().ToJson().Replace("\"letters\": [],", "");
            Assert.Empty(Soul.FromJson(_d, json).Letters);
        }
    }
}
