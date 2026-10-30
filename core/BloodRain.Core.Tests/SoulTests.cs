using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using Newtonsoft.Json.Linq;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>혼(계정 진행): 윤회의 기·연무장·환혼 강화·해금·봉인석·밟은 땅·한 번 깨친 길·회귀·저장(기획서 10·21장).</summary>
    public class SoulTests
    {
        readonly GameData _d = TestData.Load();
        CheonhaDef C => _d.Cheonha;

        static RebirthQiResult NoQi(GameData d) => new RebirthQiLedger(d.Cheonha.RebirthQi).Settle(0);

        RebirthBody Body(string sect, params string[] traits) =>
            Rebirth.Body(C, new RebirthCandidate(C.Sect(sect), C.Region(C.Sect(sect).Region), traits.Select(C.Trait).ToList()));

        /// <summary>서장을 마친 혼(청운진만 밟고 일류까지).</summary>
        Soul AfterPrologue()
        {
            var s = new Soul(_d);
            var life = s.BeginLife(Rebirth.Prologue(_d));
            life.Breakthrough("iryu");
            life.Breakthrough("ilryu");
            s.EndLife(life.Finish("fallen", 0));
            return s;
        }

        [Fact] public void 새_혼은_처음_세_문파만_열려_있고_서장부터다()
        {
            var s = new Soul(_d);
            Assert.Equal(new[] { "shaolin", "gaebang", "namgung" }, s.UnlockedSects);
            Assert.Equal(0, s.RebirthQi);
            Assert.Equal(0, s.Lives);
            Assert.Equal(1, s.Cycle);
            Assert.False(s.PrologueDone);
            Assert.False(s.IsFirstRebirth);
            Assert.Equal(C.Cycle.EndingsFirst, s.Endings);
            Assert.Throws<InvalidOperationException>(() => s.OpenRebirth(new Random(1)));
            Assert.Throws<InvalidOperationException>(() => s.BeginLife(Body("shaolin")));
            Assert.Equal(_d.Weapons.Where(w => w.Unlock == "start").Select(w => w.Id).OrderBy(x => x), s.UnlockedWeapons().OrderBy(x => x));
        }

        [Fact] public void 서장을_마치면_첫_환혼은_고정_셋이고_그다음은_해금한_문파에서()
        {
            var s = AfterPrologue();
            Assert.True(s.PrologueDone);
            Assert.True(s.IsFirstRebirth);
            Assert.Equal(1, s.Lives);
            Assert.Equal(new[] { "prologue" }, s.ClearedChapters);
            var first = s.OpenRebirth(new Random(4));
            Assert.True(first.IsFirst);
            Assert.Equal(C.Rebirth.FirstRebirth, first.Candidates.Select(x => x.Sect.Id));

            var body = first.Choose(2);
            var life = s.BeginLife(body);
            s.EndLife(life.Finish("zuohwa", 0));
            Assert.False(s.IsFirstRebirth);
            Assert.Equal(1, s.LivesBySect["namgung"]);
            var next = s.OpenRebirth(new Random(4));
            Assert.False(next.IsFirst);
            Assert.Equal(3, next.Count);
            Assert.Equal(0, next.RerollsLeft);
            Assert.All(next.Candidates, x => Assert.Contains(x.Sect.Id, s.UnlockedSects));
        }

        // ---- 연무장 ----

        [Fact] public void 연무장은_뒤에_살수록_비싸고_전부_환불하면_쓴_만큼_돌아온다()
        {
            var s = new Soul(_d);
            s.AddRebirthQi(5000);
            Assert.Equal(200, s.TrainingCost("Might"));
            Assert.True(s.TryBuyTraining("Might"));            // 200 × 1 × 1.0
            Assert.Equal(440, s.TrainingCost("Might"));         // 200 × 2 × 1.1
            Assert.True(s.TryBuyTraining("Might"));
            Assert.True(s.TryBuyTraining("MaxHealth"));         // 200 × 1 × 1.2
            Assert.Equal(880, s.TrainingSpent);
            Assert.Equal(5000 - 880, s.RebirthQi);
            Assert.Equal(2, s.TrainingLevel("Might"));
            Assert.Equal(0.10f, s.TrainingBonus()[StatKind.Might], 3);
            Assert.Equal(0.10f, s.TrainingBonus()[StatKind.MaxHealth], 3);

            // 모자라면 안 사지고 아무것도 안 바뀐다
            Assert.False(s.TryBuyTraining("Revival"));         // 10,000
            Assert.Equal(5000 - 880, s.RebirthQi);
            Assert.Equal(0, s.TrainingLevel("Revival"));

            Assert.Equal(880, s.RefundTraining());
            Assert.Equal(5000, s.RebirthQi);
            Assert.Empty(s.Training);
            Assert.Equal(0, s.TrainingSpent);
            Assert.Equal(200, s.TrainingCost("Might"));
        }

        [Fact] public void 연무장_최대_레벨이면_더_못_산다()
        {
            var s = new Soul(_d);
            s.AddRebirthQi(100000);
            Assert.True(s.TryBuyTraining("Amount"));
            Assert.Null(s.TrainingCost("Amount"));
            Assert.False(s.TryBuyTraining("Amount"));
            Assert.Equal(1, s.TrainingLevel("Amount"));
            Assert.Throws<InvalidOperationException>(() => s.TryBuyTraining("NoSuchStat"));
        }

        // ---- 환혼 강화 ----

        [Fact] public void 환혼_강화는_넷째_몸_2000_다시_고르기_400_곱하기_다음_레벨이다()
        {
            var s = AfterPrologue();
            Assert.Equal(50 + 100 + 100, s.RebirthQi); // 서장: 청운진, 이류, 일류
            s.AddRebirthQi(10000);
            int start = s.RebirthQi;
            Assert.Equal(3, s.CandidateCount);
            Assert.Equal(0, s.RerollsPerRebirth);
            Assert.Equal(2000, s.UpgradeCost("fourth_body"));
            Assert.True(s.TryBuyUpgrade("fourth_body"));
            Assert.Null(s.UpgradeCost("fourth_body"));
            Assert.False(s.TryBuyUpgrade("fourth_body"));
            Assert.Equal(4, s.CandidateCount);

            var costs = new List<int>();
            while (s.UpgradeCost("reroll") is int c) { costs.Add(c); Assert.True(s.TryBuyUpgrade("reroll")); }
            Assert.Equal(new[] { 400, 800, 1200 }, costs);
            Assert.Equal(3, s.RerollsPerRebirth);
            Assert.Equal(start - 2000 - 2400, s.RebirthQi);
            Assert.Throws<ArgumentException>(() => s.UpgradeCost("fifth_body"));
        }

        [Fact] public void 넷째_몸과_다시_고르기가_침방에_그대로_나온다()
        {
            var s = AfterPrologue();
            s.EndLife(s.BeginLife(Body("shaolin")).Finish("fallen", 0)); // 첫 환혼은 지났다
            s.AddRebirthQi(5000);
            s.TryBuyUpgrade("fourth_body");
            s.TryBuyUpgrade("reroll");
            s.TryBuyUpgrade("reroll");
            var draw = s.OpenRebirth(new Random(9));
            Assert.Equal(4, draw.Candidates.Count);
            Assert.Equal(2, draw.RerollsLeft);
            // 문파가 셋뿐이라 하나는 겹친다
            Assert.Equal(3, draw.Candidates.Select(x => x.Sect.Id).Distinct().Count());
            draw.Reroll();
            draw.Reroll();
            Assert.Throws<InvalidOperationException>(() => draw.Reroll());
        }

        [Fact] public void 환혼_강화_효과는_이름과_수로_읽힌다()
        {
            Assert.All(C.Rebirth.Upgrades, u => Assert.Contains(Soul.ParseEffect(u.Effect).key, Soul.UpgradeEffectKeys));
            Assert.Equal(("candidates", 1), Soul.ParseEffect("candidates+1"));
            Assert.Throws<FormatException>(() => Soul.ParseEffect("candidates"));
        }

        // ---- 한 생: 땅의 주인, 밟은 땅, 한 번 깨친 길 ----

        [Fact] public void 땅의_주인_첫_처치는_봉인석과_문파_해금과_300이고_그다음은_혈영_100과_혈정_5다()
        {
            Assert.Equal(C.Bosses.FirstKill.RebirthQi, C.RebirthQi.BossFirstKill);
            Assert.Equal(C.Bosses.RepeatKill.RebirthQi, C.RebirthQi.BossRepeatKill);

            var s = AfterPrologue();
            var life = s.BeginLife(Body("shaolin"));
            var kill = life.KillBoss("danggata");
            Assert.True(kill.FirstInCycle);
            Assert.True(kill.FirstOnAccount);
            Assert.Equal("천선석", kill.SealStone);
            Assert.Equal("tang", kill.UnlockedSect);
            Assert.Equal(300, kill.RebirthQi);
            Assert.Equal(_d.Cheonha.Bosses.FirstKill.BloodCrystal, kill.BloodCrystal); // 첫 처치에도 혈정 5(T-29)
            // 혼은 생이 끝날 때까지 그대로
            Assert.DoesNotContain("tang", s.UnlockedSects);

            // 같은 생에 다시 잡아도 혈영
            var again = life.KillBoss("danggata");
            Assert.False(again.FirstInCycle);
            Assert.Null(again.SealStone);
            Assert.Null(again.UnlockedSect);
            Assert.Equal(100, again.RebirthQi);
            Assert.Equal(5, again.BloodCrystal);

            var sum = s.EndLife(life.Finish("fallen", 0));
            Assert.Equal(new[] { "danggata" }, sum.NewSealStones);
            Assert.Equal(new[] { "tang" }, sum.NewlyUnlockedSects);
            Assert.Contains("tang", s.UnlockedSects);
            Assert.Equal(1, s.SealStoneCount);
            Assert.Contains("chapter2", s.ClearedChapters);

            // 다음 생에는 처음부터 혈영
            var next = s.BeginLife(Body("tang"));
            var k2 = next.KillBoss("danggata");
            Assert.False(k2.FirstInCycle);
            Assert.False(k2.FirstOnAccount);
            Assert.Equal(100, k2.RebirthQi);
            Assert.Throws<ArgumentException>(() => next.KillBoss("cheongun"));
            Assert.Throws<ArgumentException>(() => next.KillBoss("bongmagok"));
        }

        [Fact] public void 처음_밟은_땅과_처음_오른_경지는_계정에_한_번씩이다()
        {
            var s = new Soul(_d);
            var pro = s.BeginLife(Rebirth.Prologue(_d));
            Assert.Equal(new[] { "cheongun" }, pro.VisitedRegions);
            var b1 = pro.Breakthrough("iryu");
            Assert.True(b1.FirstTime);
            Assert.True(b1.LongCinematic);
            Assert.Equal(0, b1.ExtraInsightChoices);
            Assert.Equal(100, b1.RebirthQi);
            Assert.False(pro.Enter("cheongun"));
            var r1 = pro.Finish("fallen", 0).RebirthQi;
            Assert.Equal(50, r1.BonusOf(RebirthQiBonusKind.FirstVisitRegion));
            Assert.Equal(100, r1.BonusOf(RebirthQiBonusKind.FirstReachRealm));
            s.EndLife(pro.Finish("fallen", 0));
            Assert.Contains("iryu", s.ReachedRealms);
            Assert.DoesNotContain("samryu", s.ReachedRealms);

            var life = s.BeginLife(Body("shaolin"));
            Assert.Equal(new[] { "songshan" }, life.VisitedRegions);
            Assert.Equal(50, life.Qi.Settle(0).Total);        // 숭산은 처음
            Assert.False(life.Enter("cheongun"));               // 청운진은 서장에 밟았다
            Assert.True(life.Enter("gaebang_hq"));
            Assert.False(life.Enter("gaebang_hq"));
            var b2 = life.Breakthrough("iryu");
            Assert.False(b2.FirstTime);
            Assert.False(b2.LongCinematic);
            Assert.Equal(1, b2.ExtraInsightChoices);
            Assert.Equal(0, b2.RebirthQi);
            Assert.True(life.Breakthrough("ilryu").FirstTime);
            Assert.Equal("ilryu", life.HighestRealm);
            var rec = life.Finish("fallen", 0);
            Assert.Equal(50 + 50 + 100, rec.RebirthQi.Total);
            Assert.Equal(new[] { "songshan", "cheongun", "gaebang_hq" }, rec.VisitedRegions);
            var sum = s.EndLife(rec);
            Assert.Equal(new[] { "songshan", "gaebang_hq" }, sum.NewRegions);
            Assert.Equal(new[] { "ilryu" }, sum.NewRealms);
            Assert.Throws<ArgumentException>(() => life.Enter("emei"));
            Assert.Throws<ArgumentException>(() => life.Breakthrough("dacheng"));
        }

        [Fact] public void 절기는_처음_깨우면_긴_연출이고_다음_생부터는_짧다()
        {
            var s = AfterPrologue();
            var evo = _d.Evolutions[0].Id;
            var life = s.BeginLife(Body("shaolin"));
            var a = life.Awaken(evo);
            Assert.True(a.FirstTime);
            Assert.True(a.LongByDefault);
            Assert.True(Combat.Awakening.ChooseLong(Combat.AwakeningLength.Auto, a.LongByDefault));
            s.EndLife(life.Finish("fallen", 0));
            Assert.True(s.HasAwakened(evo));

            var b = s.BeginLife(Body("gaebang")).Awaken(evo);
            Assert.False(b.FirstTime);
            Assert.False(Combat.Awakening.ChooseLong(Combat.AwakeningLength.Auto, b.LongByDefault));
            Assert.True(Combat.Awakening.ChooseLong(Combat.AwakeningLength.Long, b.LongByDefault));
        }

        [Fact] public void 성질의_윤회의_기_배율이_한_생_정산에_들어간다()
        {
            var s = AfterPrologue();
            var life = s.BeginLife(Body("namgung", "dunjae", "danmyeong"));
            life.AbsorbOrb(5000, 1f);   // 200
            var r = life.Finish("fallen", 100).RebirthQi; // + 남궁세가 처음 밟음 50 + 은자 절반 50
            Assert.Equal(300, r.Subtotal);
            Assert.Equal(150, r.FromTraits);
            Assert.Equal(450, r.Total);
            int before = s.RebirthQi;
            Assert.Equal(450, s.EndLife(life.Finish("fallen", 100)).RebirthQi);
            Assert.Equal(before + 450, s.RebirthQi);
        }

        // ---- 생을 마치면 ----

        [Fact] public void 직접_만든_한_생_기록도_혼에_옮겨진다()
        {
            var s = AfterPrologue();
            var ledger = new RebirthQiLedger(C.RebirthQi);
            ledger.AddOrb(10000, 2.2f);
            var qi = ledger.Settle(300);
            var evo = _d.Evolutions.Take(2).Select(e => e.Id).ToList();
            var record = new LifeRecord("wudang", "fallen", qi, new[] { "wudangsan", "songshan" },
                new[] { "wudangsan", "songshan", "huashan" }, "chojeoljeong", evo, manuscripts: 1);
            int before = s.RebirthQi;
            var sum = s.EndLife(record);
            Assert.Equal(before + qi.Total, s.RebirthQi);
            Assert.Equal(new[] { "wudangsan", "songshan" }, sum.NewSealStones);
            Assert.Equal(new[] { "shaolin", "gaebang", "namgung", "wudang" }, s.UnlockedSects); // 숭산은 처음부터 열린 소림
            Assert.Equal(new[] { "wudang" }, sum.NewlyUnlockedSects);
            Assert.Equal(new[] { "jeoljeong", "chojeoljeong" }, sum.NewRealms);   // 이류·일류는 서장에 올랐다
            Assert.Equal(evo, sum.NewEvolutions);
            Assert.Equal(1, s.Manuscripts);
            Assert.Equal(2, s.Lives);
            Assert.Equal(1, s.LivesBySect["wudang"]);
            Assert.False(sum.FinalCleared);
        }

        [Fact] public void 이상한_기록은_받지_않는다()
        {
            var s = AfterPrologue();
            var q = NoQi(_d);
            LifeRecord R(string? sect = "shaolin", string way = "fallen", string[]? bosses = null, string[]? visited = null, string realm = "iryu", string[]? evo = null) =>
                new LifeRecord(sect, way, q, bosses ?? Array.Empty<string>(), visited ?? Array.Empty<string>(), realm, evo ?? Array.Empty<string>());
            Assert.Throws<ArgumentException>(() => s.EndLife(R(way: "timeout")));
            Assert.Throws<ArgumentException>(() => s.EndLife(R(sect: "emei")));
            Assert.Throws<ArgumentException>(() => s.EndLife(R(sect: null, way: "sealed")));
            Assert.Throws<ArgumentException>(() => s.EndLife(R(bosses: new[] { "gaebang_hq" })));
            Assert.Throws<ArgumentException>(() => s.EndLife(R(visited: new[] { "emei" })));
            Assert.Throws<ArgumentException>(() => s.EndLife(R(realm: "dacheng")));
            Assert.Throws<ArgumentException>(() => s.EndLife(R(evo: new[] { "nope" })));
            Assert.Equal(1, s.Lives); // 하나도 안 들어갔다
        }

        // ---- 진엔딩, 회귀 ----

        LifeRecord Sealed(string sect) =>
            new LifeRecord(sect, "sealed", NoQi(_d), Array.Empty<string>(), Array.Empty<string>(), "hyeongyeong", Array.Empty<string>());

        [Fact] public void 진엔딩은_종장을_서로_다른_문파_4곳으로_깨야_한다()
        {
            Assert.Equal(4, C.Cycle.TrueEndingDistinctSects);
            var s = AfterPrologue();
            foreach (var sect in new[] { "shaolin", "shaolin", "gaebang", "namgung" })
                Assert.True(s.EndLife(Sealed(sect)).FinalCleared);
            Assert.Equal(3, s.DistinctFinalSects);
            Assert.Equal(2, s.FinalClearsBySect["shaolin"]);
            Assert.False(s.TrueEndingSectsMet);
            Assert.Contains("final", s.ClearedChapters);
            s.Regress();
            s.EndLife(Sealed("tang"));
            Assert.True(s.TrueEndingSectsMet);  // 회차 합산
        }

        Soul Rich()
        {
            var s = AfterPrologue();
            s.AddRebirthQi(9000);
            s.TryBuyTraining("Might");
            s.TryBuyTraining("Growth");
            s.TryBuyUpgrade("reroll");
            s.Unlock("kills_5000_one_run");
            var life = s.BeginLife(Body("shaolin", "bokdeok"));
            life.Enter("wudangsan");
            life.Breakthrough("iryu");
            life.Breakthrough("ilryu");
            life.Breakthrough("jeoljeong");
            life.KillBoss("songshan");
            life.KillBoss("wudangsan");
            life.Awaken(_d.Evolutions[1].Id);
            life.FindManuscript();
            life.AbsorbOrb(20000, 1.3f);
            s.EndLife(life.Finish("fallen", 640));
            s.EndLife(Sealed("wudang"));
            return s;
        }

        [Fact] public void 회귀는_봉인석만_지우고_회차를_올린다()
        {
            Assert.Equal(new[] { "sealStones" }, C.Cycle.Resets);
            var s = Rich();
            Assert.Equal(2, s.SealStoneCount);
            var before = JObject.Parse(s.ToJson());
            s.Regress();
            var after = JObject.Parse(s.ToJson());
            Assert.Empty(s.SealStones);
            Assert.Equal(2, s.Cycle);
            Assert.True(s.IsBloodMoon);
            Assert.Equal(C.Cycle.EndingsSecond, s.Endings);
            foreach (var key in new[] { "sealStones", "cycle" }) { before.Remove(key); after.Remove(key); }
            Assert.True(JToken.DeepEquals(before, after), "회귀가 봉인석 말고 다른 것도 지웠다");

            // 회귀 뒤 첫 처치는 다시 봉인석과 300. 문파는 이미 열려 있다
            var life = s.BeginLife(Body("wudang"));
            var kill = life.KillBoss("wudangsan");
            Assert.True(kill.FirstInCycle);
            Assert.False(kill.FirstOnAccount);
            Assert.Equal("천권석", kill.SealStone);
            Assert.Null(kill.UnlockedSect);
            Assert.Equal(300, kill.RebirthQi);
            Assert.Equal(new[] { "wudangsan" }, s.EndLife(life.Finish("fallen", 0)).NewSealStones);
            // 봉인석 일곱이면 종장 조건
            Assert.False(s.HasAllSealStones);
        }

        // ---- 저장 ----

        [Fact] public void 저장_문자열로_내보내고_읽으면_같은_혼이다()
        {
            var s = Rich();
            var json = s.ToJson();
            var back = Soul.FromJson(_d, json);
            Assert.Equal(json, back.ToJson());

            Assert.Equal(s.RebirthQi, back.RebirthQi);
            Assert.Equal(s.Training.OrderBy(kv => kv.Key), back.Training.OrderBy(kv => kv.Key));
            Assert.Equal(s.TrainingSpent, back.TrainingSpent);
            Assert.Equal(1, back.UpgradeLevel("reroll"));
            Assert.Equal(s.UnlockedSects, back.UnlockedSects);
            Assert.True(back.IsUnlocked("kills_5000_one_run"));
            Assert.Contains("sonic_qin", back.UnlockedWeapons());
            Assert.Equal(s.SealStones.OrderBy(x => x), back.SealStones.OrderBy(x => x));
            Assert.Equal(s.VisitedRegions.OrderBy(x => x), back.VisitedRegions.OrderBy(x => x));
            Assert.Equal(s.ReachedRealms.OrderBy(x => x), back.ReachedRealms.OrderBy(x => x));
            Assert.Equal(s.AwakenedEvolutions.OrderBy(x => x), back.AwakenedEvolutions.OrderBy(x => x));
            Assert.Equal(s.Lives, back.Lives);
            Assert.True(back.PrologueDone);
            Assert.Equal(s.LivesBySect.OrderBy(kv => kv.Key), back.LivesBySect.OrderBy(kv => kv.Key));
            Assert.Equal(s.FinalClearsBySect.OrderBy(kv => kv.Key), back.FinalClearsBySect.OrderBy(kv => kv.Key));
            Assert.Equal(1, back.Manuscripts);
            Assert.Equal(s.Cycle, back.Cycle);

            // 읽은 혼으로 계속 진행해도 같은 답(환불도 그대로)
            Assert.Equal(s.RefundTraining(), back.RefundTraining());
            Assert.Equal(s.RebirthQi, back.RebirthQi);
        }

        [Fact] public void 저장_모양은_camelCase_이고_능력치_이름은_그대로다()
        {
            var o = JObject.Parse(Rich().ToJson());
            foreach (var key in new[] { "version", "rebirthQi", "training", "trainingSpent", "rebirthUpgrades", "unlocks", "bossesKilled",
                "sealStones", "visitedRegions", "reachedRealms", "awakenedEvolutions", "lives", "prologueDone", "livesBySect",
                "finalClearsBySect", "manuscripts", "cycle" })
                Assert.True(o.ContainsKey(key), $"저장에 {key} 가 없다");
            Assert.Equal(Soul.SaveVersion, (int)o["version"]!);
            Assert.NotNull(o["training"]!["Might"]);
            Assert.NotNull(o["rebirthUpgrades"]!["reroll"]);
            Assert.Equal(new[] { "songshan", "wudangsan" }, o["sealStones"]!.Values<string>());
        }

        [Fact] public void 빈_저장은_새_혼이고_더_새_형식은_읽지_않는다()
        {
            var empty = Soul.FromJson(_d, "{}");
            Assert.Equal(new Soul(_d).ToJson(), empty.ToJson());
            Assert.Equal(1, empty.Cycle);
            Assert.Throws<NotSupportedException>(() => Soul.FromJson(_d, "{\"version\": 99}"));
        }
    
        [Fact] public void 저장에_null_칸이_있어도_빈_것으로_읽는다()
        {
            // 손으로 고친 저장이나 옛 도구가 쓴 저장에 null 이 섞여도 혼은 읽혀야 한다.
            var json = "{\"version\":1,\"rebirthQi\":120,\"training\":null,\"rebirthUpgrades\":null,\"unlocks\":null," +
                       "\"bossesKilled\":null,\"sealStones\":null,\"visitedRegions\":null,\"reachedRealms\":null," +
                       "\"awakenedEvolutions\":null,\"livesBySect\":null,\"finalClearsBySect\":null,\"cycle\":0}";
            var s = Soul.FromJson(_d, json);
            Assert.Equal(120, s.RebirthQi);
            Assert.Empty(s.Training);
            Assert.Empty(s.SealStones);
            Assert.Equal(1, s.Cycle);
            Assert.Equal(s.ToJson(), Soul.FromJson(_d, s.ToJson()).ToJson());
        }

        [Fact] public void 저장_문자열이_null_이면_형식_오류다()
        {
            Assert.Throws<FormatException>(() => Soul.FromJson(_d, "null"));
        }
    }
}
