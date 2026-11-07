using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using Newtonsoft.Json.Linq;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>EL-07: 수련동 도전 과제 목록(10장 '해금과 도전 과제')을 Core 가 데이터에서 만든다. 웹 caveRows 와 같은 줄·같은 차례.</summary>
    public class CaveListTests
    {
        readonly GameData _d = Load();

        Soul With(System.Action<JObject> edit)
        {
            var j = JObject.Parse(new Soul(_d).ToJson());
            edit(j);
            return Soul.FromJson(_d, j.ToString());
        }

        [Fact] public void 새_혼은_줄이_다_있고_아무것도_안_이뤘다()
        {
            var rows = CaveList.Rows(new Soul(_d));
            Assert.All(rows, r => Assert.False(r.Done));
            var kinds = rows.Select(r => r.Kind).Distinct().ToList();
            Assert.Equal(new[] { CaveRowKind.Prologue, CaveRowKind.BossFirstKill, CaveRowKind.Challenge, CaveRowKind.Title,
                CaveRowKind.ExtremeBoss, CaveRowKind.Costume, CaveRowKind.GoldenToads, CaveRowKind.Manuscripts }, kinds);
            Assert.Equal(_d.Cheonha.BossRegions.Select(r => r.Id), rows.Where(r => r.Kind == CaveRowKind.BossFirstKill).Select(r => r.Id));
            Assert.Equal(3, rows.Count(r => r.Kind == CaveRowKind.Challenge));
            Assert.Equal(_d.Cheonha.Titles.Count, rows.Count(r => r.Kind == CaveRowKind.Title));
        }

        [Fact] public void 땅의_주인_줄의_보상이_기획서_표와_같다()
        {
            Assert.Contains("| 마천패 첫 처치 | 수로채로 환생, 하연 합류, 쇄겸술 |", Gdd);
            Assert.Contains("| 설무향 첫 처치 | 곤륜으로 환생 |", Gdd);
            var rows = CaveList.Rows(new Soul(_d)).Where(r => r.Kind == CaveRowKind.BossFirstKill).ToDictionary(r => r.Id);
            string Got(string region) => string.Join(",", rows[region].Rewards);
            Assert.Equal("wongak", rows["songshan"].Subject);
            Assert.Equal("Leader:hyegong", Got("songshan"));
            Assert.Equal("Sect:tang,Leader:dang_yeonhwa", Got("danggata"));
            Assert.Equal("Sect:waterfort,Leader:ha_yeon,Weapon:" + _d.Weapons.Single(w => w.Unlock == "boss:janggang").Id, Got("janggang"));
            Assert.Equal("Sect:kunlun", Got("kunlun"));
            Assert.StartsWith("Sect:demonic,Leader:baekya,Weapon:", Got("sipmandaesan"));
        }

        [Fact] public void 혼_기록으로_이룬_것과_셈()
        {
            var soul = With(j =>
            {
                j["prologueDone"] = true;
                j["bossesKilled"] = new JArray("songshan");
                j["unlocks"] = new JArray("dash_300_one_run", "boss_at_extreme", "costume:unhwa_harmony");
                j["talismansUsed"] = 4;
                j["goldenToads"] = 12;
                j["manuscripts"] = 9;
                j["titles"] = new JArray("sikgaek");
                j["livesBySect"] = new JObject { ["shaolin"] = 2, ["tang"] = 1 };
            });
            var rows = CaveList.Rows(soul);
            CaveRow One(CaveRowKind k, string id = "") => rows.Single(r => r.Kind == k && (id == "" || r.Id == id));
            Assert.True(One(CaveRowKind.Prologue).Done);
            Assert.True(One(CaveRowKind.BossFirstKill, "songshan").Done);
            Assert.False(One(CaveRowKind.BossFirstKill, "danggata").Done);
            Assert.True(One(CaveRowKind.Challenge, "dash_300_one_run").Done);
            var tal = One(CaveRowKind.Challenge, "seal_talisman_10_total");
            Assert.Equal((false, 10, 4), (tal.Done, tal.Target, tal.Progress));
            Assert.Null(One(CaveRowKind.Challenge, "kills_5000_one_run").Progress);   // 한 생 조건은 누적 셈이 없다
            Assert.True(One(CaveRowKind.Title, "sikgaek").Done);
            var all = One(CaveRowKind.Title, "gupa_yunhoe");
            Assert.Equal((false, 9, 2), (all.Done, all.Target, all.Progress));
            Assert.True(One(CaveRowKind.ExtremeBoss).Done);
            Assert.Equal("Insight:yeokgeun", string.Join(",", One(CaveRowKind.ExtremeBoss).Rewards));
            Assert.True(One(CaveRowKind.Costume).Done);
            var toad = One(CaveRowKind.GoldenToads);
            Assert.Equal((true, 10, 12), (toad.Done, toad.Target, toad.Progress));
            Assert.Equal(2, toad.Rewards.Single().Amount);
            var man = One(CaveRowKind.Manuscripts);
            Assert.Equal((true, 7, 7), (man.Done, man.Target, man.Progress));   // 넘쳐도 목표에서 자른다(웹 Math.min)
        }

        [Fact] public void 글_키가_네_언어에_다_있다()
        {
            var keys = CaveList.Rows(new Soul(_d)).Select(r => r.ConditionKey)
                .Concat(new[] { "ui.cave.progress", "ui.cave.reward_training_rebirth", "ui.cave.reward_sect", "ui.cave.reward_leader", "ui.cave.reward_seal_stone",
                    "ui.cave.reward_title", "ui.cave.reward_insight", "ui.cave.reward_costume", "ui.cave.reward_training_max", "ui.cave.reward_true_ending" }).Distinct();
            foreach (var lang in new[] { "ko", "en", "zh-Hans", "zh-Hant" })
            {
                var table = JObject.Parse(ReadRepoFile("unity", "BloodRain", "Assets", "Resources", "Data", "strings", lang + ".json"));
                foreach (var k in keys) Assert.True(table[k] != null, $"{lang} 에 {k} 가 없다");
            }
        }

        [Fact] public void 웹_수련동과_같은_차례()
        {
            var html = ReadRepoFile("web", "prototype", "template.html");
            int i(string s) { var at = html.IndexOf(s, System.StringComparison.Ordinal); Assert.True(at >= 0, s); return at; }
            var order = new[] { "rows.push(['서장 끝'", "첫 처치`, got.join", "CAVE_TOKEN_KO[w.unlock]) rows.push", "for (const t of DATA.titles || [])",
                "rows.push(['극성으로 땅의 주인 처치'", "c.when === 'boss_in_harmony') rows.push", "rows.push([`금두꺼비 누적", "rows.push([`의서" }.Select(i).ToList();
            Assert.Equal(order.OrderBy(x => x), order);
        }
    }
}
