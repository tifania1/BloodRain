using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Progression;
using BloodRain.Core.Run;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>정산 자막 고르기(DL-05, Core/Meta/InnDialogue.Settlement). 규칙은 docs/story/inn_screens.md '정산'.</summary>
    public class SettlementCaptionTests
    {
        readonly GameData _d = Load();
        CheonhaDef C => _d.Cheonha;

        SettlementCaption End(Soul s, string? sect, string end = LifeEndWay.Fallen, string realm = "iryu", string[]? bosses = null,
            float seconds = 900, bool shadow = false, int seed = 1)
        {
            var rec = new LifeRecord(sect, end, new RebirthQiLedger(C.RebirthQi).Settle(0), bosses ?? Array.Empty<string>(),
                new[] { sect == null ? "cheongun" : C.Sect(sect).Region }, realm, Array.Empty<string>());
            var summary = s.EndLife(rec);
            return InnDialogue.Settlement(_d, s, rec, summary, new ReturnFacts { LifeSeconds = seconds, FallenToShadow = shadow }, new Random(seed));
        }

        Soul AfterPrologue()
        {
            var s = new Soul(_d);
            Assert.Equal("prologue", End(s, null, realm: "ilryu", seconds: 120).Line.Id);   // 서장은 짧게 끝나도 서장 줄
            return s;
        }

        [Fact]
        public void 땅의_주인을_처음_쓰러뜨린_생은_지역과_이름을_채운다()
        {
            var s = AfterPrologue();
            var c = End(s, "shaolin", realm: "jeoljeong", bosses: new[] { "songshan" }, seconds: 100);
            Assert.Equal("lord_first", c.Line.Id);   // 짧은 생보다 먼저
            Assert.Equal("숭산의 땅의 주인, 혈나한 원각. 그 땅의 비가 옅어졌다.", c.Text);
            Assert.Equal("songshan", c.Region);
            Assert.Equal("wongak", c.Lord);
            // 다시 쓰러뜨린 땅의 주인(봉인석이 이미 있음)은 아니다
            Assert.NotEqual("lord_first", End(s, "shaolin", realm: "jeoljeong", bosses: new[] { "songshan" }).Line.Id);
        }

        [Fact]
        public void 한_생에_둘을_처음_쓰러뜨렸으면_나중_것()
        {
            var s = AfterPrologue();
            var c = End(s, "shaolin", realm: "chojeoljeong", bosses: new[] { "songshan", "danggata" });
            Assert.Equal("danggata", c.Region);
            Assert.Equal("dang_sohye", c.Lord);
        }

        [Fact]
        public void 위에서부터_맞는_하나()
        {
            var s = AfterPrologue();
            Assert.Equal("shadow", End(s, "shaolin", shadow: true, seconds: 100).Line.Id);
            Assert.Equal("zuohwa", End(s, "shaolin", end: LifeEndWay.Zuohwa, seconds: 100).Line.Id);
            Assert.Equal("short", End(s, "shaolin", seconds: 299).Line.Id);
            Assert.Equal("any", End(s, "shaolin", seconds: 300).Line.Kind);
            Assert.Equal("zuohwa", End(s, "shaolin", end: LifeEndWay.Zuohwa, shadow: true).Line.Id);   // 그림자에 쫓기다 앉으면 좌화
        }

        [Fact]
        public void 그_밖은_무작위_셋_문파와_버틴_시간을_채운다()
        {
            var s = AfterPrologue();
            var ids = new HashSet<string>();
            for (int seed = 0; seed < 60; seed++)
            {
                var c = End(s, "gaebang", seconds: 750, seed: seed);
                ids.Add(c.Line.Id);
                if (c.Line.Id == "any_3")
                {
                    Assert.Equal("개방의 이름 없는 제자가 빗속에서 12분 30초 버텼다.", c.Text);
                    Assert.Equal("gaebang", c.Sect);
                    Assert.Equal(750, c.Seconds);
                }
                else Assert.DoesNotContain("{", c.Text);
            }
            Assert.Equal(new[] { "any_1", "any_2", "any_3" }, ids.OrderBy(x => x));
        }

        [Fact]
        public void 혈월_천하에서는_바꿔_쓰는_줄()
        {
            var s = AfterPrologue();
            s.Regress();
            Assert.True(s.IsBloodMoon);
            for (int seed = 0; seed < 40; seed++)
            {
                var c = End(s, "shaolin", seed: seed);
                if (c.Line.Id != "any_2") { Assert.False(c.BloodMoon); continue; }
                Assert.True(c.BloodMoon);
                Assert.Equal("한 생이 끝났다. 혈월은 아직 지지 않았다.", c.Text);
                return;
            }
            Assert.Fail("any_2 가 나오지 않았다");
        }

        [Fact]
        public void 시간_글은_분과_초()
        {
            Assert.Equal("12분 30초", InnDialogue.KoreanDuration(750));
            Assert.Equal("5분", InnDialogue.KoreanDuration(300));
            Assert.Equal("45초", InnDialogue.KoreanDuration(45));
        }
    }
}
