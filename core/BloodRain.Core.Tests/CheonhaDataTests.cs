using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BloodRain.Core.Progression;
using Newtonsoft.Json.Linq;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>cheonha.json(기획서 7·10·21장)이 기획서 표, 다른 데이터와 맞고, 지역 배치가 규칙을 지키는지.
    /// Core 타입은 아직 없어서 JSON을 직접 읽는다(backlog T-01·T-02).</summary>
    public class CheonhaDataTests
    {
        readonly Data.GameData _d = TestData.Load();
        readonly JObject _c = JObject.Parse(File.ReadAllText(Path.Combine(TestData.DataDir, "cheonha.json")));
        readonly JObject _bal = JObject.Parse(File.ReadAllText(Path.Combine(TestData.DataDir, "balance.json")));
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        JObject World => (JObject)_c["world"]!;
        float S => (float)World["scalePerRealm"]!;
        float View0 => (float)World["view0Meters"]!;
        float BoundaryMul => (float)World["boundaryViewMultiple"]!;
        List<JObject> Regions => _c["regions"]!.Children<JObject>().ToList();
        List<JObject> Sects => _c["sects"]!.Children<JObject>().ToList();
        List<JObject> Traits => _c["birthTraits"]!["list"]!.Children<JObject>().ToList();
        JObject Region(string id) => Regions.Single(r => (string)r["id"]! == id);
        int RealmIndex(string id) => _d.Balance.Realms.FindIndex(r => r.Id == id);
        double ViewAt(int realm) => View0 * Math.Pow(S, realm);
        double BoundaryAt(int realm) => ViewAt(realm) * BoundaryMul;
        (double x, double y) Meters(JObject region)
        {
            var pos = (JArray)region["pos"]!;
            return ((float)pos[0]! * (float)World["widthMeters"]!, (float)pos[1]! * (float)World["heightMeters"]!);
        }
        double Distance(JObject a, JObject b)
        {
            var (ax, ay) = Meters(a); var (bx, by) = Meters(b);
            return Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));
        }
        List<JObject> BossRegions => Regions.Where(r => r["boss"]!.Type == JTokenType.String && (bool?)r["final"] != true).ToList();

        static float Num(string s) => float.Parse(s.Replace(",", ""), Inv);

        // ---- 7장 경지와 축척 ----

        [Fact] public void 경지마다_축척_한_줄이_있고_덩이는_커진다()
        {
            var scale = _c["realmScale"]!.Children<JObject>().ToList();
            Assert.Equal(_d.Balance.Realms.Select(r => r.Id), scale.Select(s => (string)s["realm"]!));
            var sizes = scale.Select(s => (int)s["clusterSize"]!).ToList();
            Assert.Equal(1, sizes[0]);
            for (int i = 1; i < sizes.Count; i++) Assert.True(sizes[i] > sizes[i - 1]);
            Assert.Equal("hyeongyeong", (string)World["noBoundaryFrom"]!);
        }

        [Fact] public void 경지와_축척_표가_데이터와_같다()
        {
            var rows = TestData.GddTable(TestData.GddSection("경지와 축척"));
            var scale = _c["realmScale"]!.Children<JObject>().ToList();
            Assert.Equal(_d.Balance.Realms.Count, rows.Count);
            // 평균 도달: 목표 곡선(20분 Lv60)대로 분당 3레벨
            float levelsPerMinute = 60f / (float)_bal["xpCurve"]!["targets"]!["level60AtMinute"]!;
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                var realm = _d.Balance.Realms[i];
                Assert.Equal(realm.NameKo, r[0]);
                Assert.Equal(realm.Level, int.Parse(r[1]));
                float minutes = realm.Level == 1 ? 0 : realm.Level / levelsPerMinute;
                var clock = Regex.Match(r[2], @"^(\d+):(\d\d)$");
                Assert.True(clock.Success, r[2]);
                Assert.Equal(minutes, int.Parse(clock.Groups[1].Value) + int.Parse(clock.Groups[2].Value) / 60f, 2);
                Assert.Equal(Math.Round(ViewAt(i)), Num(r[3].TrimEnd('m')));
                if (realm.Id == (string)World["noBoundaryFrom"]!) Assert.Equal("없음", r[4]);
                else Assert.Equal(Math.Round(BoundaryAt(i)), Num(r[4].TrimEnd('m')));
                Assert.Equal((string)scale[i]["screenKo"]!, r[5]);
                Assert.Equal($"{(string)scale[i]["foeKo"]!} {(int)scale[i]["clusterSize"]!}", r[6]);
            }
        }

        [Fact] public void 기획서_축척_규칙의_숫자가_데이터와_같다()
        {
            var sec = TestData.GddSection("경지와 축척");
            Assert.Contains($"{TestData.Num(S)}배", sec);
            Assert.Contains($"{TestData.Num(View0)}m × {TestData.Num(S)}^(경지 순번 + {TestData.Num((float)World["viewInRealmShare"]!)} × 경지 안 진행)", sec);
            Assert.Contains($"{TestData.Num(BoundaryMul)}배", sec);
            Assert.Contains($"진기는 {TestData.Num((float)_c["crush"]!["xpPerCrushed"]!)}만", sec);
            var brk = TestData.GddSection("돌파 연출");
            Assert.Contains($"{TestData.Num((float)_c["breakthrough"]!["timeScale"]!)}배", brk);
            Assert.Contains($"{TestData.Num((float)_c["breakthrough"]!["zoomFromSeconds"]!)}~{TestData.Num((float)_c["breakthrough"]!["zoomToSeconds"]!)}초", brk);
            Assert.Contains($"{TestData.Num((float)_c["crush"]!["sweepViewMultiple"]!)}배 안", brk);
        }

        // ---- 7장 지역 ----

        [Fact] public void 지역표가_데이터와_같다()
        {
            var rows = TestData.GddTable(TestData.GddSection("지역과 혈운 농도"));
            Assert.Equal(Regions.Count, rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i]; var g = Regions[i];
                Assert.Equal((string)g["nameKo"]!, r[0]);
                var sect = (string?)g["sect"];
                if (sect == null) Assert.StartsWith("없음", r[1]);
                else Assert.Equal((string)Sects.Single(s => (string)s["id"]! == sect)["nameKo"]!, r[1]);
                Assert.Equal((float)g["density"]!, Num(r[2]), 3);
                var boss = (string?)g["boss"];
                if (boss == null) Assert.Equal("없음", r[3]);
                else Assert.Equal(_d.Bosses.Single(b => b.Id == boss).NameKo, r[3]);
                var stone = (string?)g["sealStone"];
                Assert.Equal(stone ?? "없음", r[4]);
            }
        }

        [Fact] public void 봉인석은_일곱이고_땅의_주인과_장_보스가_맞는다()
        {
            Assert.Equal(7, Regions.Count(r => r["sealStone"]!.Type == JTokenType.String));
            Assert.Equal(7, BossRegions.Count);
            Assert.All(BossRegions, r => Assert.True(r["sealStone"]!.Type == JTokenType.String));
            foreach (var r in Regions.Where(r => r["boss"]!.Type == JTokenType.String))
            {
                var boss = _d.Bosses.SingleOrDefault(b => b.Id == (string)r["boss"]!);
                Assert.True(boss != null, $"{r["id"]} 땅의 주인 {r["boss"]} 이 enemies.json 에 없음");
                Assert.Equal((string)r["chapter"]!, boss!.Chapter);
            }
            var final = Regions.Single(r => (bool?)r["final"] == true);
            Assert.Equal(7, (int)final["hiddenUntilSealStones"]!);
            Assert.Equal(7, (int)_c["bosses"]!["finalRequires"]!["sealStones"]!);
        }

        [Fact] public void 지역은_지도_안에_있고_혈운_농도는_청운진이_가장_옅고_봉마곡이_가장_짙다()
        {
            foreach (var r in Regions)
            {
                var pos = (JArray)r["pos"]!;
                Assert.InRange((float)pos[0]!, 0f, 1f);
                Assert.InRange((float)pos[1]!, 0f, 1f);
            }
            var dens = Regions.Select(r => (float)r["density"]!).ToList();
            Assert.Equal(dens.Min(), (float)Region("cheongun")["density"]!);
            Assert.Equal(dens.Max(), (float)Region("bongmagok")["density"]!);
            // 혈운 농도는 옛 장 배율을 그대로 옮겼다
            foreach (var r in Regions.Where(r => r["chapter"]!.Type == JTokenType.String && (string)r["chapter"]! != "final"))
                Assert.Equal(_d.Balance.EnemyScaling.ChapterMultiplier[(string)r["chapter"]!], (float)r["density"]!, 3);
        }

        [Fact] public void 모든_출생지에서_절정_장막_안에_땅의_주인이_있고_화경이면_일곱이_다_든다()
        {
            int peak = RealmIndex("jeoljeong"), hwa = RealmIndex("hwagyeong");
            Assert.Equal("jeoljeong", (string)_c["bosses"]!["awakenFrom"]!);
            foreach (var s in Sects)
            {
                var home = Region((string)s["region"]!);
                int atPeak = BossRegions.Count(b => Distance(home, b) <= BoundaryAt(peak));
                Assert.True(atPeak >= 1, $"{s["id"]}: 절정 장막 안 땅의 주인 없음");
                Assert.All(BossRegions, b => Assert.True(Distance(home, b) <= BoundaryAt(hwa), $"{s["id"]} → {b["id"]} 화경 장막 밖"));
            }
            // 7장 본문의 예: 숭산은 넷, 곤륜은 하나
            Assert.Equal(4, BossRegions.Count(b => Distance(Region("songshan"), b) <= BoundaryAt(peak)));
            Assert.Equal(1, BossRegions.Count(b => Distance(Region("kunlun"), b) <= BoundaryAt(peak)));
            var sec = TestData.GddSection("땅의 주인이 깨어나는 규칙");
            Assert.Contains("숭산·장강·무당·화산 넷", sec);
            Assert.Contains("곤륜 하나뿐", sec);
        }

        [Fact] public void 땅의_주인_기혈_식이_기획서와_같다()
        {
            var b = (JObject)_c["bosses"]!;
            var sec = TestData.GddSection("땅의 주인이 깨어나는 규칙");
            float k = (float)b["healthPerMinute"]!, norm = (float)b["normalizeAt20"]!;
            Assert.Contains($"(1 + {TestData.Num(k)} × 경과 분) ÷ {TestData.Num(norm)}", sec);
            Assert.Equal(1 + k * 20, norm, 3); // 20분이면 기준 체력 그대로
            Assert.Contains($"{TestData.Num((float)b["engageViewMultiple"]!)}배 안", sec);
            Assert.Contains($"{TestData.Num((float)b["spawnTargetScale"]!)}배", sec);
            Assert.Contains($"윤회의 기 {(int)b["firstKill"]!["rebirthQi"]!}", sec);
            Assert.Contains($"윤회의 기 {(int)b["repeatKill"]!["rebirthQi"]!}과 혈정 {(int)b["repeatKill"]!["bloodCrystal"]!}", sec);
            // 12분이면 0.75배라는 예
            Assert.Equal(0.75, Math.Round((1 + k * 12) / norm, 2));
        }

        // ---- 5장 문파 ----

        [Fact] public void 문파는_진전_문파_아홉과_같고_지역과_서로_가리킨다()
        {
            Assert.Equal(_d.Synergies.Schools.Select(s => s.Id).OrderBy(x => x), Sects.Select(s => (string)s["id"]!).OrderBy(x => x));
            foreach (var s in Sects)
            {
                var region = Region((string)s["region"]!);
                Assert.Equal((string)s["id"]!, (string?)region["sect"]);
            }
            Assert.Equal(Sects.Count, Regions.Count(r => r["sect"]!.Type == JTokenType.String));
        }

        [Fact] public void 대표_인물이_있는_문파는_그_인물의_몸과_같다()
        {
            foreach (var s in Sects.Where(s => s["leader"]!.Type == JTokenType.String))
            {
                var c = _d.Character((string)s["leader"]!);
                Assert.Equal(c.StartWeapon, (string)s["startWeapon"]!);
                Assert.Equal(c.Ultimate, (string)s["ultimate"]!);
                Assert.Equal(c.Trait, (string)s["trait"]!);
                Assert.Equal(c.MaxHealth, (float)s["maxHealth"]!);
                Assert.Equal(c.MoveSpeed, (float)s["moveSpeed"]!, 3);
                Assert.Equal(c.YinyangStart, (int)s["yinyangStart"]!);
                var school = _d.Synergies.Schools.Single(x => x.Id == (string)s["id"]!);
                Assert.Equal(school.Character, c.Id);
            }
            foreach (var s in Sects)
            {
                Assert.Contains(_d.Weapons, w => w.Id == (string)s["startWeapon"]!);
                Assert.Contains(_d.Ultimates, u => u.Id == (string)s["ultimate"]!);
            }
            Assert.Equal("jin_sowoon", (string)_c["prologueBody"]!["character"]!);
        }

        [Fact] public void 문파_해금은_처음_셋과_그_땅의_주인_첫_처치다()
        {
            var first = _c["rebirth"]!["firstRebirth"]!.Values<string>().ToList();
            Assert.Equal(first.OrderBy(x => x), Sects.Where(s => (string)s["unlock"]! == "start").Select(s => (string)s["id"]!).OrderBy(x => x));
            Assert.Equal((int)_c["rebirth"]!["candidates"]!, first.Count);
            foreach (var s in Sects.Where(s => (string)s["unlock"]! != "start"))
            {
                var m = Regex.Match((string)s["unlock"]!, "^boss:(.+)$");
                Assert.True(m.Success, (string)s["unlock"]!);
                Assert.Equal((string)s["region"]!, m.Groups[1].Value);
                Assert.True(Region(m.Groups[1].Value)["boss"]!.Type == JTokenType.String);
            }
        }

        [Fact] public void 문파_표가_데이터와_같다()
        {
            var rows = TestData.GddTable(TestData.GddSection("한눈에 보기"));
            foreach (var s in Sects)
            {
                var r = rows.Single(x => x[0] == (string)s["nameKo"]!);
                Assert.Equal((string)Region((string)s["region"]!)["nameKo"]!, r[1]);
                var leader = (string?)s["leader"];
                Assert.Equal(leader == null ? "없음" : _d.Character(leader).NameKo, r[2]);
                Assert.Equal(_d.Weapon((string)s["startWeapon"]!).NameKo, r[3]);
                Assert.StartsWith(_d.Ultimates.Single(u => u.Id == (string)s["ultimate"]!).NameKo, r[5]);
                int yy = (int)s["yinyangStart"]!;
                Assert.Equal(yy > 0 ? $"+{yy}" : yy.ToString(Inv), r[6]);
                Assert.Equal($"{(int)(float)s["maxHealth"]!} / {Math.Round((float)s["moveSpeed"]! * 100)}%", r[7]);
                var unlock = (string)s["unlock"]!;
                if (unlock == "start") Assert.Equal("처음부터", r[8]);
                else
                {
                    var boss = (string)Region(unlock.Substring(5))["boss"]!;
                    var name = _d.Bosses.Single(b => b.Id == boss).NameKo;
                    Assert.Contains(r[8].Replace(" 첫 처치", ""), name);
                }
            }
        }

        // ---- 21장 타고난 성질·환혼 ----

        static readonly Dictionary<string, string> StatWord = new Dictionary<string, string>
        {
            ["공력"] = "Might", ["쾌속"] = "CooldownReduction", ["기혈"] = "MaxHealth", ["심득"] = "Growth", ["신법"] = "MoveSpeed",
            ["기연"] = "Luck", ["탐욕"] = "Greed", ["경공 충전"] = "DashCharges", ["호신"] = "Armor", ["회복 초당"] = "Recovery",
            ["음식 회복"] = "FoodHeal", ["흡기"] = "Magnet", ["윤회의 기"] = "rebirthQi", ["음양 시작"] = "yinyangStart",
        };

        [Fact] public void 타고난_성질_표가_데이터와_같다()
        {
            var rows = TestData.GddTable(TestData.GddSection("타고난 성질"));
            Assert.Equal(Traits.Count, rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                var t = Traits[i];
                Assert.Equal($"{(string)t["nameKo"]!}({(string)t["hanja"]!})", rows[i][0]);
                var expected = new Dictionary<string, float>();
                foreach (var p in ((JObject)t["stats"]!).Properties()) expected[p.Name] = (float)p.Value;
                if (t["rebirthQi"] != null) expected["rebirthQi"] = (float)t["rebirthQi"]!;
                if (t["yinyangStart"] != null) expected["yinyangStart"] = (float)t["yinyangStart"]!;
                var got = new Dictionary<string, float>();
                foreach (var part in rows[i][1].Split(", "))
                {
                    var m = Regex.Match(part, @"^(.+?) ([+-][\d.]+)(%?)$");
                    Assert.True(m.Success, part);
                    Assert.True(StatWord.TryGetValue(m.Groups[1].Value, out var key), $"모르는 능력치 {m.Groups[1].Value}");
                    float v = float.Parse(m.Groups[2].Value, Inv);
                    if (m.Groups[3].Value == "%") v /= 100f;
                    got[key!] = v;
                }
                Assert.Equal(expected.Keys.OrderBy(x => x), got.Keys.OrderBy(x => x));
                foreach (var kv in expected) Assert.Equal(kv.Value, got[kv.Key], 4);
            }
        }

        [Fact] public void 성질_능력치_이름은_StatKind에_있고_서로_안_맞는_쌍은_목록에_있다()
        {
            var kinds = Enum.GetNames(typeof(Build.StatKind)).ToHashSet();
            foreach (var t in Traits)
                foreach (var p in ((JObject)t["stats"]!).Properties()) Assert.Contains(p.Name, kinds);
            var ids = Traits.Select(t => (string)t["id"]!).ToHashSet();
            Assert.Equal(ids.Count, Traits.Count);
            foreach (var pair in _c["birthTraits"]!["excludes"]!.Children<JArray>())
                foreach (var id in pair.Values<string>()) Assert.Contains(id!, ids);
            var w = _c["birthTraits"]!["countWeights"]!.Values<float>().ToList();
            Assert.Equal(1f, w.Sum(), 3);
            var sec = TestData.GddSection("환혼: 다음 몸 고르기");
            Assert.Contains($"0개 {Math.Round(w[0] * 100)}%, 1개 {Math.Round(w[1] * 100)}%, 2개 {Math.Round(w[2] * 100)}%", sec);
            // 이름이 심법·깨달음과 겹치지 않는다
            var taken = _d.Passives.Select(p => p.NameKo).Concat(_d.Insights.Select(x => x.NameKo)).ToHashSet();
            Assert.All(Traits, t => Assert.DoesNotContain((string)t["nameKo"]!, taken));
        }

        // ---- 10장 윤회의 기 ----

        [Fact] public void 윤회의_기_목표는_진기_곡선과_맞는다()
        {
            var q = (JObject)_c["rebirthQi"]!;
            var curve = new XpCurve(_d.Balance.XpCurve);
            float per = (float)q["perOrbValue"]!;
            foreach (var t in q["targets"]!.Children<JObject>())
            {
                long xp = curve.CumulativeTo((int)t["level"]!);
                float qi = xp * per;
                Assert.InRange(qi, (int)t["qi"]! * 0.9f, (int)t["qi"]! * 1.1f);
                // 목표 곡선(분당 3레벨)과 분·레벨이 맞는다
                Assert.Equal((int)t["level"]!, (int)t["minute"]! * 3);
            }
        }

        [Fact] public void 윤회의_기_표와_식이_데이터와_같다()
        {
            var q = (JObject)_c["rebirthQi"]!;
            var sec = TestData.GddSection("윤회의 기");
            Assert.Contains($"{TestData.Num((float)q["perOrbValue"]!)} \\times", sec);
            Assert.Contains($"{TestData.Num((float)q["leftoverSilverRate"]!)} \\times", sec);
            var rows = TestData.GddTable(sec).ToDictionary(r => r[0], r => int.Parse(r[1]));
            Assert.Equal((int)q["bossFirstKill"]!, rows["땅의 주인 첫 처치"]);
            Assert.Equal((int)q["bossRepeatKill"]!, rows["땅의 주인 다시 처치(혈영)"]);
            Assert.Equal((int)q["firstVisitRegion"]!, rows["처음 밟은 땅(계정 전체, 지역 열한 곳)"]);
            Assert.Equal((int)q["firstReachRealm"]!, rows["처음 오른 경지(계정 전체)"]);
            Assert.Equal(11, Regions.Count);
            foreach (var t in q["targets"]!.Children<JObject>())
                Assert.Contains($"{(int)t["minute"]!}분 약 {(int)t["qi"]!:#,0}", sec);
            // 성질 보너스 문장
            foreach (var t in Traits.Where(t => t["rebirthQi"] != null))
                Assert.Contains($"'{(string)t["nameKo"]!}' +{Math.Round((float)t["rebirthQi"]! * 100)}%", sec);
        }

        [Fact] public void 환혼_강화_표가_데이터와_같다()
        {
            var rows = TestData.GddTable(TestData.GddSection("환혼 강화"));
            var ups = _c["rebirth"]!["upgrades"]!.Children<JObject>().ToList();
            Assert.Equal(ups.Count, rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                Assert.Equal((string)ups[i]["nameKo"]!, rows[i][0]);
                Assert.Equal((int)ups[i]["maxLevel"]!, int.Parse(rows[i][2]));
                Assert.StartsWith(((int)ups[i]["baseCost"]!).ToString("#,0", Inv), rows[i][3]);
                Assert.Equal((bool)ups[i]["costPerLevel"]!, rows[i][3].Contains("다음 레벨"));
            }
        }

        [Fact] public void 회귀는_봉인석만_지우고_진엔딩은_문파_넷이다()
        {
            var cyc = (JObject)_c["cycle"]!;
            Assert.Equal(new[] { "sealStones" }, cyc["resets"]!.Values<string>());
            Assert.Contains("서로 다른 문파 4곳", TestData.Gdd);
            Assert.Equal(4, (int)cyc["trueEndingDistinctSects"]!);
            var bm = (JObject)cyc["bloodmoon"]!;
            var sec = TestData.GddSection("회귀(혈월 천하)");
            Assert.Contains($"적 기혈 {TestData.Num((float)bm["enemyHealth"]!)}배, 속도 {TestData.Num((float)bm["enemySpeed"]!)}배, 보상 {TestData.Num((float)bm["rewards"]!)}배", sec);
        }
            [Fact] public void GameData가_cheonha_json을_타입으로_읽는다()
        {
            var c = _d.Cheonha;
            Assert.Equal(2.25f, c.World.ScalePerRealm, 3);
            Assert.Equal(Regions.Count, c.Regions.Count);
            Assert.Equal(7, c.BossRegions.Count());
            Assert.Equal(9, c.Sects.Count);
            Assert.Equal("danggata", c.Sect("tang").UnlockBossRegion);
            Assert.Null(c.Sect("shaolin").UnlockBossRegion);
            Assert.Equal(0.15f, c.Trait("cheonsaeng").Stats["Might"], 3);
            Assert.Equal(-30, c.Trait("eumche").YinyangStart);
            Assert.Equal(0.3f, c.Trait("dunjae").RebirthQi, 3);
            Assert.Equal(new[] { 0.6f, 0.37f }, c.Region("songshan").Pos);
            Assert.True(c.Region("bongmagok").Final);
            Assert.Equal(7, c.Bosses.FinalRequires.SealStones);
            Assert.Equal(2, c.Rebirth.Upgrades.Count);
            Assert.Equal(2000, c.RebirthQi.Targets.Last().Qi);
            Assert.Equal(64, c.RealmScale.Last().ClusterSize);
        }
    }
}
