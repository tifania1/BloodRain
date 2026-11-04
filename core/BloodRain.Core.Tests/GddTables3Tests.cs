using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>기획서 표 가운데 데이터와 대조하는 테스트가 없던 것(2026-11-04 QA-07):
    /// 2장 중원 지리와 각 장, 3장 무공 분류, 5장 인물 소개, 7장 모드의 혈월 줄, 14장 곡 목록, 16장 현지화 우선순위.
    /// 값을 바꾸면 JSON 과 기획서 표를 같이 고쳐야 여기가 통과한다.</summary>
    public class GddTables3Tests
    {
        readonly Data.GameData _d = Load();

        static string ChapterLabel(string chapter) =>
            chapter == "prologue" ? "서장" : chapter == "final" ? "종장" : chapter.Replace("chapter", "") + "장";

        /// <summary>2장 표: 봉인석을 맡은 지역 일곱과 청운진·봉마곡. 지역 이름, 봉인석, 몇 장인지가 cheonha.json regions 와 같다.</summary>
        [Fact] public void 중원_지리_표가_지역_봉인석_장과_같다()
        {
            var rows = GddTable(GddSection("중원 지리와 각 장"));
            var regions = _d.Cheonha.Regions.Where(r => r.Chapter != null).ToList();
            Assert.Equal(regions.Count, rows.Count);
            for (int i = 0; i < regions.Count; i++)
            {
                var r = regions[i];
                var row = rows[i];
                Assert.StartsWith(r.NameKo, row[0]);
                Assert.Contains(ChapterLabel(r.Chapter!), row[4]);
                if (r.SealStone != null) Assert.StartsWith(r.SealStone + "(", row[3]);
                else Assert.DoesNotContain("석(", row[3]);
            }
            Assert.Equal(7, regions.Count(r => r.SealStone != null));
        }

        /// <summary>3장 무공 분류 표: weapons.json category 를 분류 이름으로 옮기면, 무공 열여섯이 모두 제 분류 줄의 '대표 무공'에 한 번씩 있다.</summary>
        [Fact] public void 무공_분류_표에_무공_열여섯이_제_분류로_있다()
        {
            var label = new Dictionary<string, string>
            {
                ["sword"] = "검법", ["fist"] = "권장법", ["palm"] = "권장법", ["finger"] = "지법", ["hidden"] = "암기술",
                ["staff"] = "봉·창법", ["spear"] = "봉·창법", ["exotic"] = "기문병기", ["claw"] = "조법", ["sound"] = "음공",
                ["poison"] = "독공", ["talisman"] = "도술(부적)", ["orbit"] = "궤도형",
            };
            var rows = GddTable(GddSection("무공 분류")).ToDictionary(r => r[0], r => r[2].Split(',').Select(s => s.Trim()).ToList());
            foreach (var w in _d.Weapons)
            {
                Assert.True(label.ContainsKey(w.Category), $"{w.Id} 분류 '{w.Category}' 를 기획서 분류로 옮기는 줄이 없다");
                Assert.Contains(w.NameKo, rows[label[w.Category]]);
            }
            Assert.Equal(_d.Weapons.Count, rows.Values.Sum(v => v.Count));
            Assert.Equal(label.Values.Distinct().OrderBy(s => s), rows.Keys.OrderBy(s => s));
        }

        /// <summary>5장 인물 소개 표의 사람과 순서가 characters.json 과 같고, 진소운 말고는 모두 문파 대표 인물(cheonha sects leader)이다.</summary>
        [Fact] public void 인물_소개_표가_characters_와_같다()
        {
            var rows = GddTable(GddSection("인물 소개와 외형"));
            Assert.Equal(_d.Characters.Select(c => c.NameKo), rows.Select(r => r[0]));
            var leaders = _d.Cheonha.Sects.Where(s => s.Leader != null).Select(s => s.Leader!).ToHashSet();
            Assert.Equal(_d.Characters.Skip(1).Select(c => c.Id).OrderBy(s => s), leaders.OrderBy(s => s));
        }

        /// <summary>7장 모드 표의 혈월 천하 줄이 cheonha cycle.bloodmoon 과 같다(21장 '회귀' 문단은 CheonhaDataTests 가 본다).</summary>
        [Fact] public void 모드_표_혈월_천하_줄이_bloodmoon_과_같다()
        {
            var bm = _d.Cheonha.Cycle.Bloodmoon;
            static string Pct(float mul) => Num((float)Math.Round((mul - 1) * 100, 2)) + "%";
            var row = GddTable(GddSection("모드")).Single(r => r[0] == "혈월 천하(회귀)");
            Assert.StartsWith($"적 기혈 +{Pct(bm.EnemyHealth)}, 속도 +{Pct(bm.EnemySpeed)}, 보상 +{Pct(bm.Rewards)}", row[1]);
            Assert.Equal(bm.ManuscriptsOnlyHere, row[1].Contains("의서"));
        }

        /// <summary>14장 곡 목록의 곡 수와 짧은 곡이 audio_queue 와 같다. 지역 곡은 cheonha regions 순서대로 적혀 있다.</summary>
        [Fact] public void 곡_목록_표가_audio_queue_와_같다()
        {
            var queue = File.ReadAllLines(Path.Combine(RepoRoot, "audio_queue", "queue.jsonl")).Where(l => l.Trim().Length > 0)
                .Select(JObject.Parse).Where(o => ((string)o["id"]!).StartsWith("bgm_") || ((string)o["id"]!).StartsWith("sting_")).ToList();
            var ids = queue.Select(o => (string)o["id"]!).ToList();
            var rows = GddTable(GddSection("곡 목록")).ToDictionary(r => r[0]);

            int Count(string prefix) => ids.Count(i => i.StartsWith(prefix));
            Assert.Equal(_d.Cheonha.Regions.Count, Count("bgm_region_"));
            Assert.True(rows.ContainsKey($"지역 {Count("bgm_region_")}곡"), "지역 곡 수가 기획서와 다르다");
            Assert.True(rows.ContainsKey($"땅의 주인 {Count("bgm_boss_")}곡"), "땅의 주인 곡 수가 기획서와 다르다");
            Assert.Equal(_d.Cheonha.BossRegions.Count() + 1, Count("bgm_boss_")); // 일곱 + 장삼
            Assert.True(rows.ContainsKey($"혈마 {Count("bgm_final_")}곡"), "혈마 곡 수가 기획서와 다르다");
            Assert.True(rows.ContainsKey($"엔딩 {Count("bgm_ending_")}곡"), "엔딩 곡 수가 기획서와 다르다");
            // 따로 줄이 있는 곡 일곱(메인 테마·객잔·침방·연무장·정산·서장·절기 각성)과 묶음 넷
            int grouped = Count("bgm_region_") + Count("bgm_boss_") + Count("bgm_final_") + Count("bgm_ending_");
            Assert.Equal(rows.Count - 4, ids.Count - grouped);

            // 지역 곡 칸: 지역 이름 첫 낱말이 regions 순서대로
            string cell = rows[$"지역 {Count("bgm_region_")}곡"][2];
            int at = 0;
            foreach (var r in _d.Cheonha.Regions)
            {
                string word = r.NameKo.Split(' ')[0];
                int i = cell.IndexOf(word, at, StringComparison.Ordinal);
                Assert.True(i >= at, $"곡 목록 지역 칸에 '{word}' 가 순서대로 없다");
                at = i + word.Length;
            }

            // 짧은 곡: 정산·장삼·절기 각성. 기획서 길이 칸에 '짧은 곡'이 있는 줄과 같다
            var shortIds = queue.Where(o => (string?)o["length"] == "short").Select(o => (string)o["id"]!).OrderBy(s => s);
            Assert.Equal(new[] { "bgm_boss_jang_sam", "bgm_settle", "sting_awaken" }, shortIds);
            Assert.Contains("짧은 곡", rows["정산"][3]);
            Assert.Contains("장삼(짧은 곡)", rows[$"땅의 주인 {Count("bgm_boss_")}곡"][1]);
            Assert.Contains("짧은 곡", rows["절기 각성"][3]);
        }

        /// <summary>16장 현지화 우선순위의 1순위 언어 셋이 Core Strings.Languages 와 strings/ 파일과 같다.</summary>
        [Fact] public void 현지화_1순위_언어가_Strings_Languages_와_같다()
        {
            var code = new Dictionary<string, string> { ["한국어"] = "ko", ["简体中文"] = "zh-Hans", ["English"] = "en" };
            var first = GddTable(GddSection("현지화 우선순위")).Where(r => r[0] == "1").Select(r => r[1]).ToList();
            Assert.All(first, l => Assert.True(code.ContainsKey(l), $"1순위 '{l}' 의 언어 코드가 없다"));
            Assert.Equal(first.Select(l => code[l]).OrderBy(s => s), Data.Strings.Languages.OrderBy(s => s));
            var files = Directory.GetFiles(Path.Combine(DataDir, "strings"), "*.json").Select(Path.GetFileNameWithoutExtension);
            Assert.Equal(Data.Strings.Languages.OrderBy(s => s), files.OrderBy(s => s));
        }
    }
}
