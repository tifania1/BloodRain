using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.World;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>천하 연표 시제품 ①(backlog GU-11d). chronicle.json 이 설계안 '예시: 숭산에서 태어난 생' 표와 맞고,
    /// World.Chronicle 이 그 표의 '필요한 경지'·'첫 생' 칸을 그대로 셈하는지.</summary>
    public class ChronicleTests
    {
        readonly GameData _d = TestData.Load();
        Chronicle C => new Chronicle(_d);
        int Realm(string id) => _d.Balance.Realms.OrderBy(r => r.Level).ToList().FindIndex(r => r.Id == id);

        // 설계안 표: 사건 → (숭산에서 거리, 필요한 경지). 거리는 설계안이 반올림해 적은 값이라 15m 안에서 본다.
        static readonly (string id, float distance, string realm)[] SongshanTable =
        {
            ("songshan_refugees", 60, "samryu"),
            ("gaebang_scatter", 240, "ilryu"),
            ("wudang_barrier", 610, "jeoljeong"),
            ("songshan_bell", 0, "jeoljeong"),
            ("janggang_raid", 650, "jeoljeong"),
            ("huashan_bloom", 610, "jeoljeong"),
            ("danggata_poison", 1300, "chojeoljeong"),
            ("murim_muster", 320, "ilryu"),
            ("imperial_south", 500, "jeoljeong"),
            ("magyo_north", 1460, "chojeoljeong"),
        };

        [Fact] public void 숭산_태생의_거리와_필요한_경지가_설계안_표와_같다()
        {
            var c = C;
            foreach (var (id, distance, realm) in SongshanTable)
            {
                var e = c.Event(id);
                Assert.InRange(c.Distance(e, "songshan"), distance - 15, distance + 15);
                Assert.Equal(Realm(realm), c.RequiredRealm(e, "songshan"));
            }
            Assert.Equal(Realm("hyeongyeong"), c.RequiredRealm(c.Event("bloodmoon"), "songshan"));
        }

        [Fact] public void 첫_생_도달_시각으로_판정하면_설계안_첫_생_칸이_나온다()
        {
            // 설계안: 지금 평균 빌드의 경지 도달 시각 일류 6:40, 절정 11:40, 초절정 16:40. 현경은 대개 못 감.
            var reach = new Dictionary<int, float>
            {
                [Realm("iryu")] = 200, [Realm("ilryu")] = 400, [Realm("jeoljeong")] = 700, [Realm("chojeoljeong")] = 1000,
            };
            var all = C.JudgeAll("songshan", r => reach.TryGetValue(r, out var t) ? t : (float?)null).ToDictionary(j => j.Event.Id);

            void OnTime(string id) => Assert.Equal(ChronicleReach.OnTime, all[id].Reach);
            void Late(string id, float by) { Assert.Equal(ChronicleReach.Late, all[id].Reach); Assert.Equal(by, all[id].LateBy, 3); }

            OnTime("songshan_refugees");
            Late("gaebang_scatter", 40);
            Late("wudang_barrier", 100);
            OnTime("songshan_bell");
            OnTime("janggang_raid");
            OnTime("huashan_bloom");
            Late("danggata_poison", 40);
            OnTime("murim_muster");
            OnTime("imperial_south");
            OnTime("magyo_north");
            Assert.Equal(ChronicleReach.Never, all["bloodmoon"].Reach);
            // 첫 생에 늦는 곳은 개방 총타·무당산·당가타 셋(설계안 표 아래 문단).
            Assert.Equal(new[] { "gaebang_scatter", "wudang_barrier", "danggata_poison" },
                all.Values.Where(j => j.Reach == ChronicleReach.Late).OrderBy(j => j.Event.AtSeconds).Select(j => j.Event.Id));
        }

        [Fact] public void 같은_시각에_두_곳이_터진다_장강과_화산()
        {
            var c = C;
            Assert.Equal(new[] { "huashan_bloom" }, c.SameTime(c.Event("janggang_raid")).Select(e => e.Id));
            Assert.Empty(c.SameTime(c.Event("songshan_bell")));
        }

        [Fact] public void 사건은_시각_순서이고_시각으로_나눌_수_있다()
        {
            var c = C;
            Assert.Equal(c.Events.OrderBy(e => e.AtSeconds).Select(e => e.Id), c.Events.Select(e => e.Id));
            Assert.Equal(new[] { "songshan_refugees", "gaebang_scatter" }, c.HappenedBy(360).Select(e => e.Id));
            Assert.Equal("wudang_barrier", c.Upcoming(360).First().Id);
            Assert.Equal(1800, c.Events.Last().AtSeconds);
        }

        [Fact] public void 장막_안인지는_경지로_본다()
        {
            var c = C;
            var birth = new Regions(_d).Center("songshan");
            var gaebang = c.Event("gaebang_scatter");
            Assert.False(c.InReach(gaebang, birth, Realm("iryu")));
            Assert.True(c.InReach(gaebang, birth, Realm("ilryu")));
            // 출생지가 바뀌면 필요한 경지도 바뀐다: 개방 몸이면 개방 총타는 삼류에서 닿는다.
            Assert.Equal(0, c.RequiredRealm(gaebang, "gaebang_hq"));
        }

        [Fact] public void 사건_종류는_넷이고_모두_쓰는_종류다()
        {
            Assert.Equal(new[] { "sect_crisis", "movement", "fortune", "fate" }, _d.Chronicle.Kinds.Select(k => k.Id));
            Assert.All(_d.Chronicle.Events, e => Assert.False(string.IsNullOrEmpty(e.NameKo)));
            Assert.All(_d.Chronicle.Events, e => Assert.False(string.IsNullOrEmpty(e.PlaceKo)));
        }

        /// <summary>GU-11e 보고서가 세 몸 모두 사건 열하나를 판정하고, 설계안 '첫 생' 칸을 숭산 표에 나란히 둔다.</summary>
        [Fact] public void 연표_시뮬레이션_보고서가_세_몸을_다_판정한다()
        {
            var md = BloodRain.Sim.ChronicleSim.Report(BloodRain.Sim.SimData.Load(TestData.DataDir), seeds: 2);
            foreach (var body in new[] { "## 소림 몸", "## 개방 몸", "## 남궁 몸" }) Assert.Contains(body, md);
            Assert.Equal(3 * _d.Chronicle.Events.Count, md.Split('\n').Count(l => l.StartsWith("| ") && System.Text.RegularExpressions.Regex.IsMatch(l, @"^\| \d+:\d\d \|")));
            Assert.Contains("설계안 '첫 생'", md);
            Assert.Equal(_d.Chronicle.Events.Select(e => e.Id).OrderBy(x => x), BloodRain.Sim.ChronicleSim.DesignFirstLife.Keys.OrderBy(x => x));
        }

        [Theory]
        [InlineData("\"region\": \"gaebang_hq\"", "\"region\": \"nowhere\"")]
        [InlineData("\"kind\": \"movement\", \"atSeconds\": 120", "\"kind\": \"nope\", \"atSeconds\": 120")]
        [InlineData("\"minRealm\": \"hyeongyeong\"", "\"minRealm\": \"nowhere\"")]
        [InlineData("\"id\": \"gaebang_scatter\"", "\"id\": \"songshan_refugees\"")]
        public void 잘못된_연표는_읽을_때_막는다(string from, string to)
        {
            var files = GameData.FileNames.ToDictionary(n => n, n => File.ReadAllText(Path.Combine(TestData.DataDir, n + ".json")));
            Assert.Contains(from, files["chronicle"]);
            files["chronicle"] = files["chronicle"].Replace(from, to);
            Assert.Throws<InvalidOperationException>(() => new Chronicle(GameData.FromJson(files)));
        }
    }
}
