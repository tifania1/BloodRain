using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>기획서 표 가운데 QA-07 뒤에도 데이터와 대조하는 테스트가 없던 것(2026-11-04 QA-08):
    /// 4장 장별 줄거리, 15장 엔진과 패키지, 20장 층 표·비급 층·연환 16·비전 8·기연 8, 21장 세 겹의 고리·한 번 깨친 길.
    /// 값을 바꾸면 JSON(또는 Unity 프로젝트 설정)과 기획서 표를 같이 고쳐야 여기가 통과한다.</summary>
    public class GddTables4Tests
    {
        readonly Data.GameData _d = Load();
        Data.SynergyDefs S => _d.Synergies;

        static string ChapterLabel(string chapter) =>
            chapter == "prologue" ? "서장" : chapter == "final" ? "종장" : chapter.Replace("chapter", "") + "장";

        static string NameOf(Data.GameData d, string weaponId) => d.Weapons.Single(w => w.Id == weaponId).NameKo;

        /// <summary>표 칸에서 숫자만 뽑는다(1,000 의 쉼표는 지운다).</summary>
        static List<string> Numbers(string s) =>
            Regex.Matches(KoreanCounts(s).Replace(",", ""), @"\d+(\.\d+)?").Select(m => m.Value).ToList();

        /// <summary>데이터 글은 '두 배', '열다섯 개', '하나 늘어난다'처럼 한글 수로 적은 곳이 있어
        /// 기획서 표의 '2배', '15개', '+1'과 견주려고 숫자로 바꾼다(열아홉까지).</summary>
        static string KoreanCounts(string s)
        {
            var ones = new Dictionary<string, int>
            {
                ["한"] = 1, ["하나"] = 1, ["두"] = 2, ["세"] = 3, ["네"] = 4, ["다섯"] = 5, ["여섯"] = 6, ["일곱"] = 7, ["여덟"] = 8, ["아홉"] = 9,
            };
            return Regex.Replace(s, @"(?<![가-힣])(열)?(하나|한|두|세|네|다섯|여섯|일곱|여덟|아홉)?(?= ?(배|장|번|마리|자루|개|권|바퀴|대|늘어))",
                m => m.Length == 0 ? "" : ((m.Groups[1].Success ? 10 : 0) + (m.Groups[2].Success ? ones[m.Groups[2].Value] : 0)).ToString());
        }

        // ---- 4장 ----

        /// <summary>4장 장별 줄거리: 장 순서와 장 보스가 enemies.json bosses 의 chapter 와 같고,
        /// '얻는 것' 칸의 봉인석 조각·문파 환생·대표 인물 합류가 cheonha.json(regions·sects)과 같다.</summary>
        [Fact] public void 장별_줄거리_표의_장_보스와_얻는_것이_데이터와_같다()
        {
            var rows = GddTable(GddSection("장별 줄거리"));
            Assert.Equal(_d.Bosses.Select(b => ChapterLabel(b.Chapter)), rows.Select(r => r[0]));
            foreach (var row in rows)
            {
                var boss = _d.Bosses.Single(b => ChapterLabel(b.Chapter) == row[0]);
                Assert.StartsWith(boss.NameKo, row[3]);

                var gain = row[4];
                var region = _d.Cheonha.Regions.SingleOrDefault(r => r.Chapter == boss.Chapter && r.Boss == boss.Id);
                if (region?.SealStone != null) Assert.Contains(region.SealStone + " 조각", gain);
                else Assert.DoesNotContain("석 조각", gain);

                var sect = region?.Sect == null ? null : _d.Cheonha.Sects.Single(s => s.Id == region.Sect);
                bool reborn = sect != null && sect.Unlock == "boss:" + region!.Id;
                Assert.True(reborn == gain.Contains(" 환생"), $"{row[0]} '얻는 것'의 환생이 {sect?.Id} unlock '{sect?.Unlock}' 과 다르다");
                if (reborn) Assert.Contains(sect!.NameKo + " 환생", gain);

                bool joins = sect?.Leader != null;
                Assert.True(joins == gain.Contains(" 합류"), $"{row[0]} '얻는 것'의 합류가 {sect?.Id} leader 와 다르다");
                if (joins) Assert.Contains(_d.Character(sect!.Leader!).NameKo + " 합류", gain);
            }
        }

        // ---- 15장 ----

        /// <summary>15장 엔진과 패키지: 엔진 줄의 Unity 판이 ProjectVersion.txt 와 같고, 표에 적힌 Unity 패키지(URP·2D·Input System·Burst·
        /// Unity Test Framework)가 Packages/manifest.json 에 있다. 빌드 줄의 game-ci 는 워크플로에 있다.
        /// DOTween·Text Animator·Steamworks.NET 은 '보유 애셋'·계획이라 아직 프로젝트에 없어도 된다.</summary>
        [Fact] public void 엔진과_패키지_표가_Unity_프로젝트와_같다()
        {
            var rows = GddTable(GddSection("엔진과 패키지")).ToDictionary(r => r[0], r => r[1]);
            var unity = Path.Combine(RepoRoot, "unity", "BloodRain");
            var version = Regex.Match(ReadText(Path.Combine(unity, "ProjectSettings", "ProjectVersion.txt")), @"m_EditorVersion: (\d+\.\d+)\.").Groups[1].Value;
            Assert.StartsWith($"Unity {version} ", rows["엔진"]);
            Assert.Contains("2D URP", rows["엔진"]);

            var manifest = ReadText(Path.Combine(unity, "Packages", "manifest.json"));
            foreach (var (row, word, package) in new[]
            {
                ("엔진", "URP", "com.unity.render-pipelines.universal"),
                ("엔진", "2D", "com.unity.2d.sprite"),
                ("입력", "Input System", "com.unity.inputsystem"),
                ("성능", "Burst", "com.unity.burst"),
                ("테스트", "Unity Test Framework", "com.unity.test-framework"),
            })
            {
                Assert.Contains(word, rows[row]);
                Assert.Contains($"\"{package}\"", manifest);
            }
            Assert.Contains("`Resources/Data/strings/<lang>.json`", rows["현지화"]);
            Assert.True(Directory.Exists(Path.Combine(DataDir, "strings")));

            Assert.Contains("game-ci", rows["빌드"]);
            var workflows = Directory.GetFiles(Path.Combine(RepoRoot, ".github", "workflows"), "*.yml").Select(ReadText);
            Assert.Contains(workflows, w => w.Contains("game-ci/"));
        }

        // ---- 20장 ----

        /// <summary>20장 첫 표의 '출시 개수'가 synergies.json 과 같고, 장경각 도감 쪽수는 그 합이다.</summary>
        [Fact] public void 시너지_층_표의_출시_개수가_synergies_와_같다()
        {
            var rows = GddTable(TwentyIntro()).ToDictionary(r => r[0], r => r[3]);
            Assert.StartsWith($"{S.Links.Count} ", rows["연환(連環)"]);
            Assert.StartsWith($"{S.Schools.Count}문파", rows["진전(眞傳)"]);
            Assert.Equal($"{S.Secrets.Count}", rows["비전(秘傳)"]);
            Assert.Equal($"{S.Encounters.Count}", rows["기연(奇緣) 사건"]);

            int pages = S.Links.Count + S.Schools.Count + S.Secrets.Count + S.Encounters.Count;
            Assert.Contains($"도감은 연환 {S.Links.Count}, 진전 {S.Schools.Count}, 비전 {S.Secrets.Count}, 기연 {S.Encounters.Count}, 모두 {pages}쪽", Gdd);
        }

        /// <summary>20장 머리(## 20. 부터 첫 ### 전까지).</summary>
        static string TwentyIntro()
        {
            var gdd = Gdd;
            int start = gdd.IndexOf("## 20. 시너지와 기연");
            Assert.True(start >= 0, "기획서에 20장이 없다");
            return gdd.Substring(start, gdd.IndexOf("\n### ", start) - start);
        }

        /// <summary>20장 비급 층 표가 tier: secret 인 연환·진전 단계와 같다(이름, 종류, 몇 단계).</summary>
        [Fact] public void 비급_층_표가_tier_secret_과_같다()
        {
            var section = GddSection("비급 층과 해금");
            var rows = GddTable(section.Substring(0, section.IndexOf("| 몇 번째 복원")))
                .Select(r => (name: r[0], kind: r[1])).ToList();
            var expected = S.Links.Where(l => l.IsSecret).Select(l => (name: l.NameKo, kind: "연환"))
                .Concat(S.Schools.SelectMany(sc => sc.Tiers.Where(t => t.IsSecret).Select(t => (name: $"{sc.NameKo} {t.Count}단계", kind: "진전"))))
                .ToList();
            Assert.Equal(expected.OrderBy(x => x.name), rows.OrderBy(x => x.name));
            Assert.Contains($"'비급 층'으로 따로 두고", section);
            Assert.Contains($"연환·진전 가운데 화력을 크게 끌어올리는 {ToKoreanCount(expected.Count)}은", section);
        }

        static string ToKoreanCount(int n) => n switch { 3 => "셋", 4 => "넷", 5 => "다섯", 6 => "여섯", 7 => "일곱", _ => n.ToString() };

        /// <summary>20장 연환 16 표가 synergies.json links 와 같다: 순서, 이름(한자), 무공 둘(a + b 순서), 효과 칸의 숫자는 모두 desc 에 있다.</summary>
        [Fact] public void 연환_16_표가_links_와_같다()
        {
            var rows = GddTable(GddSection("연환(連環) 16"));
            Assert.Equal(S.Links.Count, rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                var l = S.Links[i];
                var row = rows[i];
                Assert.Equal($"{l.NameKo}({l.Hanja})", row[0]);
                Assert.Equal($"{NameOf(_d, l.A)} + {NameOf(_d, l.B)}", row[1]);
                foreach (var n in Numbers(row[2]))
                    Assert.True(Numbers(l.Desc).Contains(n), $"{l.NameKo}: 기획서 효과의 {n} 이 desc 에 없다 — {l.Desc}");
            }
        }

        /// <summary>20장 비전 8 표가 synergies.json secrets 와 같다: 순서, 이름(한자), 도감 시구 두 줄. 조건·얻는 것 칸의 숫자는 condition·reward 에 있다.</summary>
        [Fact] public void 비전_8_표가_secrets_와_같다()
        {
            var rows = GddTable(GddSection("비전(秘傳) 8"));
            Assert.Equal(S.Secrets.Count, rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                var s = S.Secrets[i];
                var row = rows[i];
                Assert.Equal($"{s.NameKo}({s.Hanja})", row[0]);
                Assert.Equal(string.Join(" / ", s.Hint), row[1]);
                foreach (var n in Numbers(row[2]))
                    Assert.True(Numbers(s.Condition).Contains(n), $"{s.NameKo}: 기획서 조건의 {n} 이 condition 에 없다 — {s.Condition}");
                foreach (var n in Numbers(row[3]))
                    Assert.True(Numbers(s.Reward).Contains(n), $"{s.NameKo}: 기획서 얻는 것의 {n} 이 reward 에 없다 — {s.Reward}");
            }
        }

        /// <summary>20장 기연 8 표의 사건 이름과 순서가 synergies.json encounters 와 같다(확률 칸은 SynergyDataTests 가 본다).</summary>
        [Fact] public void 기연_8_표의_사건이_encounters_와_같다()
        {
            var rows = GddTable(GddSection("기연(奇緣) 사건 8"));
            Assert.Equal(S.Encounters.Select(e => e.NameKo), rows.Select(r => r[0]));
        }

        // ---- 21장 ----

        /// <summary>21장 세 겹의 고리: 회귀 때 처음으로 돌아가는 것은 cheonha.json cycle.resets(봉인석)뿐이고,
        /// 윤회 줄의 '쌓이는 것'에 cycle.keeps 가 모두 적혀 있다.</summary>
        [Fact] public void 세_겹의_고리_표가_cycle_resets_keeps_와_같다()
        {
            var rows = GddTable(GddSection("세 겹의 고리")).ToDictionary(r => r[0]);
            var label = new Dictionary<string, string>
            {
                ["sealStones"] = "봉인석", ["training"] = "연무장", ["unlockedSects"] = "해금한 문파", ["unlocks"] = "무공",
                ["codex"] = "장경각 도감", ["visitedRegions"] = "처음 밟은 땅", ["mastery"] = "한 번 깨친 길",
            };
            var cycle = _d.Cheonha.Cycle;
            Assert.Equal(string.Join(", ", cycle.Resets.Select(r => label[r])), rows["회차(회귀)"][3]);
            Assert.Equal($"회귀할 때 {string.Join("·", cycle.Resets.Select(r => label[r]))}만", rows["윤회"][3]);
            foreach (var k in cycle.Keeps.Concat(cycle.Resets))
            {
                Assert.True(label.ContainsKey(k), $"cycle '{k}' 를 기획서 말로 옮기는 줄이 없다");
                Assert.Contains(label[k], rows["윤회"][2]);
            }
        }

        /// <summary>21장 한 번 깨친 길 표: 처음 오른 경지의 윤회의 기가 cheonha.json rebirthQi.firstReachRealm 과 같다.</summary>
        [Fact] public void 한_번_깨친_길_표의_윤회의_기가_데이터와_같다()
        {
            var rows = GddTable(GddSection("한 번 깨친 길")).ToDictionary(r => r[0], r => r[1]);
            Assert.EndsWith($"윤회의 기 {_d.Cheonha.RebirthQi.FirstReachRealm}", rows["처음 오른 경지"]);
        }
    }
}
