using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>SV-02 환혼 강화 '바라는 문파'(pin_sect): 고른 문파가 후보 한 칸에 늘 들고, 나머지 칸은 겹치지 않는다(기획서 10·21장).</summary>
    public class SectPinTests
    {
        readonly GameData _d = TestData.Load();
        CheonhaDef C => _d.Cheonha;

        /// <summary>서장을 마치고 첫 환혼도 지났고, 당가·수로채·무당까지 해금한 혼(문파 여섯).</summary>
        Soul SixSects()
        {
            var s = new Soul(_d);
            var pro = s.BeginLife(Rebirth.Prologue(_d));
            s.EndLife(pro.Finish("fallen", 0));
            var body = s.OpenRebirth(new Random(1)).Choose(0);
            var life = s.BeginLife(body);
            foreach (var region in new[] { "danggata", "janggang", "wudangsan" }) life.KillBoss(region);
            s.EndLife(life.Finish("fallen", 0));
            Assert.False(s.IsFirstRebirth);
            Assert.Equal(6, s.UnlockedSects.Count);
            return s;
        }

        [Fact] public void 데이터의_셋째_환혼_강화는_바라는_문파_4000_한_단계다()
        {
            var u = C.Rebirth.Upgrades.Single(x => x.Id == "pin_sect");
            Assert.Equal("바라는 문파", u.NameKo);
            Assert.Equal(1, u.MaxLevel);
            Assert.Equal(4000, u.BaseCost);
            Assert.False(u.CostPerLevel);
            Assert.Equal(("pin", 1), Soul.ParseEffect(u.Effect));
            Assert.Equal(C.Rebirth.Upgrades.Single(x => x.Id == "fourth_body").BaseCost * 2, u.BaseCost); // 넷째 몸의 두 배(추천값)
        }

        [Fact] public void 강화를_사야_문파를_고를_수_있고_미해금_문파와_서장_몸은_못_고른다()
        {
            var s = SixSects();
            Assert.False(s.HasSectPin);
            Assert.False(s.TryPinSect("tang"));
            Assert.Null(s.PinnedSect);

            s.AddRebirthQi(4000);
            Assert.Equal(4000, s.UpgradeCost("pin_sect"));
            Assert.True(s.TryBuyUpgrade("pin_sect"));
            Assert.Null(s.UpgradeCost("pin_sect"));
            Assert.True(s.HasSectPin);
            Assert.Null(s.ActivePinnedSect); // 사기만 하고 안 고르면 아무 일 없다

            Assert.False(s.CanPinSect("huashan"));                // 화산 땅의 주인을 아직 안 잡음
            Assert.False(s.TryPinSect("huashan"));
            Assert.False(s.TryPinSect(C.PrologueBody.Character)); // 서장 몸(진소운)은 문파가 아니다
            Assert.False(s.TryPinSect("no_such_sect"));
            Assert.Null(s.PinnedSect);

            Assert.True(s.TryPinSect("tang"));
            Assert.Equal("tang", s.ActivePinnedSect);
            Assert.True(s.TryPinSect("wudang")); // 언제든 바꾼다
            Assert.Equal("wudang", s.PinnedSect);
            Assert.False(s.TryPinSect("kunlun")); // 거절하면 앞서 고른 것이 그대로
            Assert.Equal("wudang", s.PinnedSect);
            s.ClearSectPin();
            Assert.Null(s.ActivePinnedSect);
        }

        [Fact] public void 고른_문파가_여러_시드에서_늘_한_칸에_들고_나머지는_겹치지_않는다()
        {
            var s = SixSects();
            s.AddRebirthQi(4000);
            s.TryBuyUpgrade("pin_sect");
            Assert.True(s.TryPinSect("tang"));
            var where = new HashSet<int>();
            for (int seed = 0; seed < 200; seed++)
            {
                var draw = s.OpenRebirth(new Random(seed));
                Assert.Equal("tang", draw.PinnedSect);
                Assert.Equal(3, draw.Candidates.Count);
                var ids = draw.Candidates.Select(x => x.Sect.Id).ToList();
                Assert.Single(draw.Candidates, x => x.Pinned);
                Assert.Equal("tang", draw.Candidates.Single(x => x.Pinned).Sect.Id);
                Assert.Equal(3, ids.Distinct().Count());
                Assert.All(ids, id => Assert.Contains(id, s.UnlockedSects));
                where.Add(draw.Candidates.ToList().FindIndex(x => x.Pinned));
            }
            Assert.Equal(new[] { 0, 1, 2 }, where.OrderBy(i => i)); // 고정 칸이 늘 첫 카드는 아니다
        }

        [Fact] public void 넷째_몸이면_넷_중_한_칸이고_다시_고르기에도_문파는_남고_성질은_새로_굴린다()
        {
            var s = SixSects();
            s.AddRebirthQi(10000);
            s.TryBuyUpgrade("pin_sect");
            s.TryBuyUpgrade("fourth_body");
            s.TryBuyUpgrade("reroll");
            s.TryBuyUpgrade("reroll");
            s.TryPinSect("waterfort");
            bool traitsChanged = false;
            for (int seed = 0; seed < 40; seed++)
            {
                var draw = s.OpenRebirth(new Random(seed));
                var before = draw.Candidates.Single(x => x.Pinned);
                for (int r = 0; r < 2; r++)
                {
                    draw.Reroll();
                    Assert.Equal(4, draw.Candidates.Count);
                    var pin = draw.Candidates.Single(x => x.Pinned);
                    Assert.Equal("waterfort", pin.Sect.Id);
                    Assert.Equal(4, draw.Candidates.Select(x => x.Sect.Id).Distinct().Count());
                    if (!pin.SameTraits(before)) traitsChanged = true;
                }
                // 고른 몸에도 그 문파가 그대로 간다
                int at = draw.Candidates.ToList().FindIndex(x => x.Pinned);
                Assert.Equal("waterfort", draw.Choose(at).Sect!.Id);
            }
            Assert.True(traitsChanged);
        }

        [Fact] public void 침방을_연_뒤에_바꾸면_다음_침방부터_탄다()
        {
            var s = SixSects();
            s.AddRebirthQi(4400);
            Assert.True(s.TryBuyUpgrade("pin_sect"));
            Assert.True(s.TryBuyUpgrade("reroll"));
            s.TryPinSect("tang");
            var draw = s.OpenRebirth(new Random(5));
            Assert.True(s.TryPinSect("wudang"));
            Assert.Equal("tang", draw.PinnedSect);
            Assert.Equal("tang", draw.Reroll().Single(x => x.Pinned).Sect.Id);
            Assert.Equal("wudang", s.OpenRebirth(new Random(5)).Candidates.Single(x => x.Pinned).Sect.Id);
        }

        [Fact] public void 강화를_안_샀거나_안_골랐으면_예전과_후보가_난수까지_같다()
        {
            var s = SixSects();
            string Ids(RebirthDraw d) => string.Join(",", d.Candidates.Select(x => $"{x.Sect.Id}:{string.Join("+", x.Traits.Select(t => t.Id))}:{x.Female}"));
            for (int seed = 0; seed < 30; seed++)
            {
                var plain = new RebirthDraw(C, s.UnlockedSects, 3, 0, false, new Random(seed));
                Assert.Null(plain.PinnedSect);
                Assert.DoesNotContain(plain.Candidates, x => x.Pinned);
                Assert.Equal(Ids(plain), Ids(s.OpenRebirth(new Random(seed))));
            }
            // 강화 없이 고른 문파가 저장에 남아 있어도(손으로 고친 저장) 침방은 그대로
            var json = Newtonsoft.Json.Linq.JObject.Parse(s.ToJson());
            json["pinnedSect"] = "tang";
            var hacked = Soul.FromJson(_d, json.ToString());
            Assert.Equal("tang", hacked.PinnedSect);
            Assert.Null(hacked.ActivePinnedSect);
            Assert.Equal(Ids(s.OpenRebirth(new Random(3))), Ids(hacked.OpenRebirth(new Random(3))));
        }

        [Fact] public void 첫_환혼은_고정이라_바라는_문파를_안_탄다()
        {
            var s = new Soul(_d);
            s.EndLife(s.BeginLife(Rebirth.Prologue(_d)).Finish("fallen", 0));
            s.AddRebirthQi(4000);
            Assert.True(s.TryBuyUpgrade("pin_sect"));
            Assert.True(s.TryPinSect("gaebang"));
            var first = s.OpenRebirth(new Random(2));
            Assert.True(first.IsFirst);
            Assert.Null(first.PinnedSect);
            Assert.Equal(C.Rebirth.FirstRebirth, first.Candidates.Select(x => x.Sect.Id));
            Assert.DoesNotContain(first.Candidates, x => x.Pinned);
        }

        [Fact] public void Roll_은_해금_안_된_바라는_문파를_거절하고_문파가_모자라면_성질만_달리해_채운다()
        {
            var three = new[] { "shaolin", "gaebang", "namgung" };
            Assert.Throws<ArgumentException>(() => Rebirth.Roll(C, three, 3, new Random(1), "tang"));
            Assert.Throws<ArgumentException>(() => Rebirth.Roll(C, three, 3, new Random(1), "no_such_sect"));
            Assert.Throws<ArgumentException>(() => new RebirthDraw(C, three, 3, 0, false, new Random(1), "tang"));
            for (int seed = 0; seed < 50; seed++)
            {
                var four = Rebirth.Roll(C, three, 4, new Random(seed), "gaebang");
                Assert.Equal(4, four.Count);
                Assert.Equal("gaebang", four.Single(x => x.Pinned).Sect.Id);
                // 고정 칸을 뺀 나머지 셋 가운데 고정 문파가 아닌 둘은 꼭 든다(겹침은 문파가 모자랄 때만)
                Assert.Contains(four, x => x.Sect.Id == "shaolin");
                Assert.Contains(four, x => x.Sect.Id == "namgung");
                foreach (var g in four.GroupBy(x => x.Sect.Id))
                    Assert.Equal(g.Count(), g.Select(x => string.Join("+", x.Traits.Select(t => t.Id))).Distinct().Count());
            }
        }

        [Fact] public void 저장_왕복에_고른_문파가_남고_옛_저장은_안_고른_것으로_읽는다()
        {
            var s = SixSects();
            s.AddRebirthQi(4000);
            s.TryBuyUpgrade("pin_sect");
            s.TryPinSect("wudang");
            var json = s.ToJson();
            Assert.Contains("\"pinnedSect\": \"wudang\"", json);
            var back = Soul.FromJson(_d, json);
            Assert.True(back.HasSectPin);
            Assert.Equal("wudang", back.PinnedSect);
            Assert.Equal("wudang", back.ActivePinnedSect);
            Assert.Equal(json, back.ToJson());

            // 옛 저장(칸 없음)과 안 고른 혼은 칸을 쓰지 않는다
            var old = Newtonsoft.Json.Linq.JObject.Parse(json);
            old.Remove("pinnedSect");
            var oldSoul = Soul.FromJson(_d, old.ToString());
            Assert.Null(oldSoul.PinnedSect);
            Assert.True(oldSoul.HasSectPin);
            Assert.DoesNotContain("pinnedSect", new Soul(_d).ToJson());
            // 데이터에서 없어진 문파 id 는 버린다
            old["pinnedSect"] = "gone_sect";
            Assert.Null(Soul.FromJson(_d, old.ToString()).PinnedSect);
        }
    }
}
