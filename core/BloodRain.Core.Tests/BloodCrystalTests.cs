using System;
using System.Linq;
using System.Text.RegularExpressions;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Run;
using Newtonsoft.Json.Linq;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>혈정: 한 생에 모으고(정예·땅의 주인, 혈월 1.5배) 정산 때 혼에 쌓여 저장에 남는다(기획서 10장 화폐, 15장 저장 목록).</summary>
    public class BloodCrystalTests
    {
        readonly GameData _d = TestData.Load();
        CheonhaDef C => _d.Cheonha;

        Soul AfterPrologue(bool bloodmoon = false)
        {
            var s = new Soul(_d);
            s.EndLife(s.BeginLife(Rebirth.Prologue(_d)).Finish(LifeEndWay.Fallen, 0));
            if (bloodmoon) s.Regress();
            return s;
        }

        RebirthBody Shaolin() =>
            Rebirth.Body(C, new RebirthCandidate(C.Sect("shaolin"), C.Region(C.Sect("shaolin").Region), Array.Empty<BirthTraitDef>()));

        [Fact] public void 정예_하나에_하나_땅의_주인_첫_처치에_다섯()
        {
            var soul = AfterPrologue();
            var log = soul.BeginLife(Shaolin());
            Assert.Equal(0, log.BloodCrystal);
            Assert.Equal(_d.Balance.Drops.EliteBloodCrystal, log.KillElite());
            var kill = log.KillBoss("songshan");
            Assert.Equal(C.Bosses.FirstKill.BloodCrystal, kill.BloodCrystal);
            Assert.Equal(_d.Balance.Drops.EliteBloodCrystal + C.Bosses.FirstKill.BloodCrystal, log.BloodCrystal);
        }

        [Fact] public void 이십분_생에_여덟개는_정예_셋과_원각()
        {
            // 10장 화폐 표 '20분 생에 8개': chapters.json life 정예가 20분 전까지 셋(5·10·15분) + 원각 첫 처치 5
            var life = _d.Chapters.Single(c => c.Id == "life");
            int elitesBefore20 = life.Events.Count(e => e.Kind == "elite" && e.At < 20f);
            var log = AfterPrologue().BeginLife(Shaolin());
            for (int i = 0; i < elitesBefore20; i++) log.KillElite();
            log.KillBoss("songshan");
            var m = Regex.Match(TestData.Gdd, @"\| 혈정\(血晶\) \|[^|]*\|[^|]*\| 20분 생에 (\d+)개 \|");
            Assert.True(m.Success, "10장 화폐 표의 혈정 줄을 못 찾음");
            Assert.Equal(int.Parse(m.Groups[1].Value), log.BloodCrystal);
        }

        [Fact] public void 혈월이면_정예_몫은_모아서_한_번에_반올림()
        {
            var log = AfterPrologue(bloodmoon: true).BeginLife(Shaolin());
            Assert.Equal(1.5f, log.RewardMultiplier);
            // 1.5 → 2, 3.0 → 3, 4.5 → 5, 6.0 → 6: 하나씩 반올림하면 넷에 8개가 되지만 모아서 하면 6개
            Assert.Equal(new[] { 2, 1, 2, 1 }, Enumerable.Range(0, 4).Select(_ => log.KillElite()).ToArray());
            Assert.Equal(6, log.BloodCrystal);
            var kill = log.KillBoss("songshan");
            Assert.Equal(8, kill.BloodCrystal); // 5 × 1.5 = 7.5 → 8
            Assert.Equal(14, log.BloodCrystal);
        }

        [Fact] public void 정산하면_혼에_쌓이고_요약에_나온다()
        {
            var soul = AfterPrologue();
            var log = soul.BeginLife(Shaolin());
            log.KillElite(); log.KillElite();
            log.KillBoss("songshan");
            var record = log.Finish(LifeEndWay.Fallen, 0);
            Assert.Equal(7, record.BloodCrystal);
            var summary = soul.EndLife(record);
            Assert.Equal(7, summary.BloodCrystal);
            Assert.Equal(7, soul.BloodCrystal);

            // 다음 생은 혈영(다시 처치)도 5
            var log2 = soul.BeginLife(Shaolin());
            Assert.Equal(C.Bosses.RepeatKill.BloodCrystal, log2.KillBoss("songshan").BloodCrystal);
            soul.EndLife(log2.Finish(LifeEndWay.Zuohwa, 0));
            Assert.Equal(7 + C.Bosses.RepeatKill.BloodCrystal, soul.BloodCrystal);
        }

        [Fact] public void 쓰기는_모자라면_그대로()
        {
            var soul = AfterPrologue();
            soul.AddBloodCrystal(10);
            Assert.False(soul.TrySpendBloodCrystal(11));
            Assert.Equal(10, soul.BloodCrystal);
            Assert.True(soul.TrySpendBloodCrystal(10));
            Assert.Equal(0, soul.BloodCrystal);
            Assert.Throws<ArgumentOutOfRangeException>(() => soul.AddBloodCrystal(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => soul.TrySpendBloodCrystal(-1));
        }

        [Fact] public void Life_가_정예_처치를_일지에_넘긴다()
        {
            var soul = AfterPrologue();
            var life = new Life(soul, Shaolin(), new Random(1));
            Assert.Equal(1, life.KillElite());
            Assert.Equal(1, life.Log.BloodCrystal);
            Assert.Equal(1, life.End(LifeEndWay.Zuohwa, 0).BloodCrystal);
        }

        [Fact] public void 저장에_남고_옛_저장은_0()
        {
            var soul = AfterPrologue();
            soul.AddBloodCrystal(23);
            var json = soul.ToJson();
            Assert.Equal(23, (int)JObject.Parse(json)["bloodCrystal"]!);
            Assert.Equal(23, Soul.FromJson(_d, json).BloodCrystal);

            var old = JObject.Parse(json);
            old.Remove("bloodCrystal");
            Assert.Equal(0, Soul.FromJson(_d, old.ToString()).BloodCrystal);
            Assert.Equal(json, Soul.FromJson(_d, json).ToJson());
        }

        [Fact] public void 음수_혈정_기록은_받지_않는다()
        {
            var soul = AfterPrologue();
            var bad = new LifeRecord("shaolin", LifeEndWay.Fallen, new RebirthQiLedger(C.RebirthQi).Settle(0),
                Array.Empty<string>(), new[] { "songshan" }, "samryu", Array.Empty<string>(), bloodCrystal: -1);
            Assert.Throws<ArgumentException>(() => soul.EndLife(bad));
        }
    }
}
