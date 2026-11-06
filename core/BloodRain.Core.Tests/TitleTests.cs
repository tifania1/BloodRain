using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using Newtonsoft.Json.Linq;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>WP-34: 기획서 10장 해금 표의 칭호 셋 — '식객'(한 생 음식 20개), '일검무적'(무공 하나만으로 땅의 주인 처치), '구파윤회'(아홉 문파로 한 번씩 환생).
    /// 한 생 기록은 LifeLog.EatFood·SingleWeaponBoss, 판정은 Soul.EndLife(LifeSummary.NewTitles).</summary>
    public class TitleTests
    {
        readonly GameData _d = TestData.Load();
        CheonhaDef C => _d.Cheonha;

        Soul AfterPrologue()
        {
            var s = new Soul(_d);
            s.EndLife(s.BeginLife(Rebirth.Prologue(_d)).Finish("fallen", 0));
            return s;
        }

        LifeSummary Live(Soul s, string sect, int food = 0, bool single = false) =>
            s.EndLife(new LifeRecord(sect, "fallen", new RebirthQiLedger(C.RebirthQi).Settle(0), Array.Empty<string>(),
                new[] { C.Sect(sect).Region }, "samryu", Array.Empty<string>(), foodEaten: food, singleWeaponBoss: single));

        [Fact] public void 데이터가_기획서_10장_해금_표와_같다()
        {
            var rows = TestData.GddTable(TestData.GddSection("해금과 도전 과제")).ToDictionary(r => r[0], r => r[1]);
            Assert.Equal("칭호 '식객'", rows["한 생에서 음식 20개"]);
            Assert.Equal("칭호 '일검무적'", rows["무공 하나만으로 땅의 주인 처치"]);
            Assert.Equal("칭호 '구파윤회'", rows["아홉 문파로 한 번씩 환생"]);
            Assert.Equal(new[] { "식객", "일검무적", "구파윤회" }, C.Titles.Select(t => t.NameKo));
            Assert.Equal(20, C.Titles.Single(t => t.Id == "sikgaek").Count);
            Assert.Equal(9, C.Sects.Count);
        }

        [Fact] public void 식객은_한_생_음식_20개()
        {
            var s = AfterPrologue();
            Assert.Empty(Live(s, "shaolin", food: 19).NewTitles);
            Assert.Equal(new[] { "sikgaek" }, Live(s, "shaolin", food: 20).NewTitles);
            Assert.Empty(Live(s, "shaolin", food: 30).NewTitles);   // 한 번만
            Assert.Contains("sikgaek", s.Titles);
        }

        [Fact] public void 일검무적은_무공_하나로_땅의_주인을_잡은_생()
        {
            var s = AfterPrologue();
            Assert.Empty(Live(s, "gaebang").NewTitles);
            Assert.Equal(new[] { "ilgeom_mujeok" }, Live(s, "gaebang", single: true).NewTitles);
            Assert.Contains("if (Loadout.Weapons.Count == 1) Log.SingleWeaponBoss = true;",
                TestData.ReadRepoFile("unity", "BloodRain", "Assets", "Scripts", "Core", "Run", "Life.cs"));
        }

        [Fact] public void 구파윤회는_아홉_문파를_다_살아_본_뒤()
        {
            var s = AfterPrologue();
            var sects = C.Sects.Select(x => x.Id).ToList();
            foreach (var id in sects.Take(8)) Assert.Empty(Live(s, id).NewTitles);
            Assert.Empty(Live(s, sects[0]).NewTitles);   // 같은 문파를 또 살아도 안 열림
            Assert.Equal(new[] { "gupa_yunhoe" }, Live(s, sects[8]).NewTitles);
        }

        [Fact] public void 칭호는_저장_왕복하고_옛_저장은_비었다()
        {
            var s = AfterPrologue();
            Live(s, "shaolin", food: 25, single: true);
            var json = s.ToJson();
            Assert.Equal(new[] { "ilgeom_mujeok", "sikgaek" }, JObject.Parse(json)["titles"]!.Select(x => (string)x!));
            var back = Soul.FromJson(_d, json);
            Assert.Equal(s.Titles.OrderBy(x => x), back.Titles.OrderBy(x => x));
            Assert.Equal(json, back.ToJson());
            var old = JObject.Parse(json); old.Remove("titles");
            Assert.Empty(Soul.FromJson(_d, old.ToString()).Titles);
        }

        [Fact] public void 음식_수가_음수면_막는다()
        {
            var s = AfterPrologue();
            Assert.Throws<ArgumentException>(() => Live(s, "shaolin", food: -1));
        }

        [Fact] public void 웹_한_생_판도_같은_규칙()
        {
            string html = TestData.ReadRepoFile("web", "prototype", "template.html");
            Assert.Contains("if (dr.kind === 'food' || dr.kind === 'meat') G.food = (G.food || 0) + 1;", html);
            Assert.Contains("if (G.weapons.length === 1) G.singleBoss = true;", html);
            Assert.Contains("t.when === 'food_one_run' ? (G.food || 0) >= t.count : t.when === 'boss_single_weapon' ? !!G.singleBoss : t.when === 'all_sects_lived' ? (DATA.sects || []).every(x => lived.has(x.id)) : false", html);
            Assert.Contains("if (st.sect) m.sectLives[st.sect] = (m.sectLives[st.sect] || 0) + 1;", html);
            Assert.Contains("\"titles\": [drop_notes(t) for t in load(\"cheonha\").get(\"titles\", [])]", TestData.ReadRepoFile("tools", "build_web_prototype.py"));
        }
    }
}
