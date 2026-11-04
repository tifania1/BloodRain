using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>audio_queue/queue.jsonl(Gemini '음악 만들기'로 뽑을 배경음)이 audio_pipeline.md 규칙을 지키고,
    /// 지역·땅의 주인이 늘거나 이름이 바뀌어도 곡이 빠지지 않는지.</summary>
    public class AudioQueueTests
    {
        static readonly string[] Statuses = { "todo", "generated", "picked", "imported", "rejected" };
        static readonly string[] Milestones = { "M2", "M3", "later" };

        static List<JObject> Rows() =>
            File.ReadAllLines(Path.Combine(RepoRoot, "audio_queue", "queue.jsonl")).Where(l => l.Trim().Length > 0)
                .Select(JObject.Parse).ToList();

        static HashSet<string> Ids() => Rows().Select(o => (string)o["id"]!).ToHashSet();

        static JObject Data(string file) => JObject.Parse(File.ReadAllText(Path.Combine(DataDir, file)));

        [Fact] public void 줄마다_필수_값이_있고_id가_하나뿐이다()
        {
            var rows = Rows();
            Assert.NotEmpty(rows);
            var ids = rows.Select(o => (string)o["id"]!).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
            foreach (var o in rows)
            {
                var id = (string)o["id"]!;
                Assert.Matches("^(bgm|sting)_[a-z0-9_]+$", id);
                Assert.Contains((string)o["status"]!, Statuses);
                Assert.Contains((string)o["milestone"]!, Milestones);
                Assert.Contains((string)o["length"]!, new[] { "short", "full" });
                Assert.Equal(JTokenType.Boolean, o["loop"]!.Type);
                Assert.False(string.IsNullOrWhiteSpace((string?)o["use"]), $"{id}: use(쓰는 곳)가 비었다");
                Assert.False(string.IsNullOrWhiteSpace((string?)o["chat"]), $"{id}: chat 이 비었다");
                // 받은 MP3 는 PC art_inbox/audio 에 id 이름으로 둔다(postprocess_audio.py 가 id 로 찾는다)
                Assert.Equal($"art_inbox/audio/{id}.mp3", (string)o["save_as"]!);
            }
        }

        [Fact] public void 문구는_STYLE로_시작하고_영어만_쓴다()
        {
            foreach (var o in Rows())
            {
                var p = (string)o["prompt"]!;
                Assert.StartsWith("{STYLE} ", p);
                Assert.DoesNotMatch("[가-힣]", p);
                // 이어 붙여 도는 곡은 Gemini 에도 루프용이라고 알려 준다(이음매는 postprocess_audio.py 가 다듬음)
                if ((bool)o["loop"]!) Assert.Contains("loopable", p);
            }
        }

        [Fact] public void 공통_문구는_가사_없는_연주곡이다()
        {
            var style = ReadRepoFile("audio_queue", "STYLE.txt");
            Assert.DoesNotMatch("[가-힣]", style);
            Assert.Contains("No vocals, no lyrics", style);
            Assert.Contains("Instrumental", style);
        }

        [Fact] public void 잘라_쓰는_곡은_짧은_곡이고_루프가_아니다()
        {
            // Gemini 짧은 곡은 약 30초라 cut_s 는 그보다 짧아야 한다
            foreach (var o in Rows().Where(o => o["cut_s"] != null))
            {
                Assert.Equal("short", (string)o["length"]!);
                Assert.False((bool)o["loop"]!);
                Assert.InRange((double)o["cut_s"]!, 1, 29);
            }
        }

        [Fact] public void 탈락한_줄은_이유를_적는다()
        {
            foreach (var o in Rows().Where(o => (string)o["status"]! == "rejected"))
                Assert.False(string.IsNullOrWhiteSpace((string?)o["reason"]), $"{o["id"]}: rejected 인데 reason 이 없다");
        }

        [Fact] public void 지역마다_곡이_있다()
        {
            var ids = Ids();
            foreach (var r in ((JArray)Data("cheonha.json")["regions"]!).Cast<JObject>())
                Assert.Contains($"bgm_region_{r["id"]}", ids);
        }

        [Fact] public void 땅의_주인마다_곡이_있고_종장은_단계마다다()
        {
            var ids = Ids();
            var final = (string)Data("bosspatterns.json")["final"]!["boss"]!;
            foreach (var b in ((JArray)Data("enemies.json")["bosses"]!).Cast<JObject>().Select(b => (string)b["id"]!))
                if (b != final) Assert.Contains($"bgm_boss_{b}", ids);
            var stages = ((JArray)Data("bosspatterns.json")["final"]!["stages"]!).Count;
            for (int i = 1; i <= stages; i++) Assert.Contains($"bgm_final_{i}", ids);
        }

        [Fact] public void 거점_화면과_서장_곡이_있다()
        {
            // backlog BGM-01: 서장·객잔·침방·연무장·정산, 절기 각성 짧은 곡. 타이틀과 엔딩 셋은 기획서 14장
            var ids = Ids();
            foreach (var id in new[] { "bgm_title", "bgm_prologue", "bgm_inn", "bgm_chimbang", "bgm_yeonmu", "bgm_settle",
                                       "sting_awaken", "bgm_ending_seal", "bgm_ending_inherit", "bgm_ending_true" })
                Assert.Contains(id, ids);
        }

        [Fact] public void M2_곡은_M2_지역과_보스를_덮는다()
        {
            // 기획서 17장 M2: 서장과 숭산 일대(원각), 문파 소림·개방·남궁, 침방·연무장·정산
            var m2 = Rows().Where(o => (string)o["milestone"]! == "M2").Select(o => (string)o["id"]!).ToHashSet();
            foreach (var id in new[] { "bgm_region_cheongun", "bgm_region_songshan", "bgm_region_gaebang_hq", "bgm_region_namgung_manor",
                                       "bgm_boss_wongak", "bgm_boss_jang_sam", "bgm_prologue", "bgm_inn", "bgm_chimbang", "bgm_yeonmu", "bgm_settle" })
                Assert.Contains(id, m2);
        }
        [Fact] public void imported_줄과_bgm_json과_ogg가_서로_맞다()
        {
            // tools/postprocess_audio.py 가 picked → ogg 로 만들고 bgm.json 에 적은 뒤 imported 로 바꾼다. 한쪽만 손으로 고치면 어긋난다.
            var dir = Path.Combine(RepoRoot, "unity", "BloodRain", "Assets", "Audio", "BGM");
            var imported = Rows().Where(o => (string)o["status"]! == "imported").ToDictionary(o => (string)o["id"]!);
            var manifest = Path.Combine(dir, "bgm.json");
            if (!File.Exists(manifest)) { Assert.Empty(imported); return; }
            var tracks = ((JArray)JObject.Parse(File.ReadAllText(manifest))["tracks"]!).Cast<JObject>().ToList();
            Assert.Equal(imported.Keys.OrderBy(s => s), tracks.Select(t => (string)t["id"]!).OrderBy(s => s));
            foreach (var t in tracks)
            {
                var id = (string)t["id"]!;
                Assert.Equal($"{id}.ogg", (string)t["file"]!);
                Assert.True(File.Exists(Path.Combine(dir, $"{id}.ogg")), $"{id}.ogg 가 없다");
                Assert.Equal((bool)imported[id]["loop"]!, (bool)t["loop"]!);
                if (imported[id]["cut_s"] != null)
                    Assert.InRange((double)t["seconds"]!, 0.5, (double)imported[id]["cut_s"]! + 0.01);
            }
        }

        [Fact] public void 다듬는_도구가_큐와_같은_폴더를_쓴다()
        {
            var py = ReadRepoFile("tools", "postprocess_audio.py");
            Assert.Contains("ROOT / \"audio_queue\" / \"queue.jsonl\"", py);
            Assert.Contains("\"Assets\" / \"Audio\" / \"BGM\"", py);
        }
    }
}
