using System;
using System.Linq;
using System.Numerics;
using BloodRain.Core.World;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>중원 지도의 지역(World.Regions). 기획서 7장 '지역과 혈운 농도', 6장 지역 고유 적·정예.</summary>
    public class RegionsTests
    {
        readonly Data.GameData _d = TestData.Load();
        Regions NewRegions() => new Regions(_d);

        [Fact] public void 지도_비율과_세계_m를_오간다()
        {
            var g = NewRegions();
            Assert.Equal(4000f, g.WidthMeters);
            Assert.Equal(2600f, g.HeightMeters);
            var songshan = g.Center("songshan"); // pos 0.60, 0.37
            Assert.Equal(2400f, songshan.X, 3);
            Assert.Equal(962f, songshan.Y, 3);
            var back = g.ToMap(songshan);
            Assert.Equal(0.60f, back.X, 5);
            Assert.Equal(0.37f, back.Y, 5);
            Assert.Equal(new Vector2(4000, 2600), g.ToWorld(1, 1));
            Assert.Equal(Vector2.Zero, g.ToWorld(0, 0));
        }

        [Fact] public void 지역_한가운데면_그_지역이고_농도도_그_값_그대로다()
        {
            var g = NewRegions();
            foreach (var r in g.All)
            {
                Assert.Equal(r.Id, g.Nearest(g.Center(r)).Id);
                Assert.Equal(r.Density, g.Density(g.Center(r)), 4);
            }
            Assert.Equal(1.0f, g.Density(g.Center("songshan"), sealStones: 0), 4);
            Assert.Equal(0.8f, g.Density(g.Center("cheongun"), sealStones: 0), 4);
        }

        [Fact] public void 두_지역_가운데점은_두_값의_평균이다()
        {
            var g = NewRegions();
            // 무당산(2.2)과 화산(2.8) 사이. 둘 다 가운데점에서 약 199m, 다른 지역은 더 멀다.
            var mid = (g.Center("wudangsan") + g.Center("huashan")) / 2f;
            Assert.Equal(2.5f, g.Density(mid), 4);
            Assert.Contains(g.Nearest(mid).Id, new[] { "wudangsan", "huashan" });

            // 가운데점의 가장 가까운 두 지역이 바로 그 둘인 쌍은 모두 평균이다
            int checkedPairs = 0;
            foreach (var a in g.All)
                foreach (var b in g.All)
                {
                    if (string.CompareOrdinal(a.Id, b.Id) >= 0) continue;
                    var m = (g.Center(a) + g.Center(b)) / 2f;
                    var two = g.All.OrderBy(r => Vector2.Distance(m, g.Center(r))).Take(3).ToList();
                    if (!(new[] { two[0].Id, two[1].Id }.OrderBy(x => x).SequenceEqual(new[] { a.Id, b.Id }.OrderBy(x => x)))) continue;
                    if (Math.Abs(Vector2.Distance(m, g.Center(two[1])) - Vector2.Distance(m, g.Center(two[2]))) < 1f) continue; // 셋째와 거의 같은 거리면 건너뛴다
                    Assert.Equal((a.Density + b.Density) / 2f, g.Density(m), 3);
                    checkedPairs++;
                }
            Assert.True(checkedPairs >= 5, $"확인한 쌍 {checkedPairs}");
        }

        [Fact] public void 지역_사이는_가까운_쪽_값에_더_가깝다()
        {
            var g = NewRegions();
            var a = g.Center("wudangsan");
            var b = g.Center("huashan");
            var nearA = a + (b - a) * 0.25f; // 무당산 쪽 1/4 지점: v = (2.2 × 3 + 2.8 × 1) / 4
            Assert.Equal((2.2f * 3 + 2.8f) / 4f, g.Density(nearA), 3);
        }

        [Fact] public void 지도_끝에서도_가장_가까운_두_지역을_섞는다()
        {
            var g = NewRegions();
            // 왼쪽 위 모서리: 곤륜 설원(3.5, 약 916m)과 당가타(1.3, 약 1,887m)
            Assert.Equal("kunlun", g.Nearest(new Vector2(0, 0)).Id);
            Assert.Equal(2.781f, g.Density(new Vector2(0, 0)), 3);
            // 오른쪽 위: 남궁세가(1.0, 약 1,658m)와 청운진(0.8, 약 1,777m)
            Assert.Equal("namgung_manor", g.Nearest(new Vector2(4000, 0)).Id);
            Assert.Equal(0.903f, g.Density(new Vector2(4000, 0)), 3);
            // 오른쪽 아래: 남궁세가(1.0)와 장강 수로(1.7)
            Assert.Equal(1.316f, g.Density(new Vector2(4000, 2600)), 3);

            // 지도 위 어디서든 농도는 가장 옅은 땅과 가장 짙은 땅 사이
            float min = g.All.Min(r => r.Density), max = g.All.Max(r => r.Density);
            for (int x = 0; x <= 4000; x += 200)
                for (int y = 0; y <= 2600; y += 200)
                    Assert.InRange(g.Density(new Vector2(x, y)), min, max);

            // 지도 밖으로는 못 나간다
            Assert.False(g.InsideWorld(new Vector2(-1, 100)));
            Assert.True(g.InsideWorld(new Vector2(4000, 2600)));
            Assert.Equal(new Vector2(0, 2600), g.ClampToWorld(new Vector2(-100, 3000)));
            Assert.Equal(new Vector2(1234, 567), g.ClampToWorld(new Vector2(1234, 567)));
        }

        [Fact] public void 봉마곡은_봉인석_일곱_전엔_서_있는_지역과_농도에서_빠진다()
        {
            var g = NewRegions();
            var bong = g.Region("bongmagok");
            Assert.Equal(7, bong.HiddenUntilSealStones);
            var c = g.Center(bong);
            Assert.Equal("bongmagok", g.Nearest(c).Id);                  // 숨은 지역까지 센다
            Assert.Equal("bongmagok", g.Nearest(c, sealStones: 7).Id);
            Assert.Equal(5.0f, g.Density(c, sealStones: 7), 4);
            Assert.Equal("wudangsan", g.Nearest(c, sealStones: 6).Id);  // 약 299m. 숭산은 약 318m
            Assert.Equal("wudangsan", g.Nearest(c, sealStones: 0).Id);
            Assert.Equal(1.618f, g.Density(c, sealStones: 0), 3);        // 무당산 2.2와 숭산 1.0을 섞은 값
            Assert.DoesNotContain(g.Visible(0), r => r.Id == "bongmagok");
            Assert.Equal(g.All.Count - 1, g.Visible(6).Count());
            Assert.Equal(g.All.Count, g.Visible(7).Count());
        }

        [Fact] public void 지역_기믹은_그_지역_데이터대로다()
        {
            var g = NewRegions();
            Assert.Equal(new[] { "bell", "pagoda_forest" }, g.Gimmicks("songshan"));
            Assert.Contains("water", g.GimmicksAt(g.Center("janggang") + new Vector2(30, 20)));
            Assert.Equal(new[] { "rising_blood" }, g.GimmicksAt(g.Center("bongmagok"), sealStones: 7));
            Assert.All(g.All, r => Assert.NotEmpty(r.Gimmicks));
            // 기획서 지역표에 지형 기믹 칸이 빈 지역이 없다
            var rows = TestData.GddTable(TestData.GddSection("지역과 혈운 농도"));
            Assert.Equal(g.All.Count, rows.Count);
            Assert.All(rows, r => Assert.False(string.IsNullOrWhiteSpace(r[5]), r[0]));
        }

        [Fact] public void 고유_적은_지역의_장_이름으로_찾는다()
        {
            var g = NewRegions();
            Assert.Equal(new[] { "mogin_nahan" }, g.LocalEnemies("songshan").Select(e => e.Id));
            Assert.Equal(new[] { "poison_toad", "golden_centipede", "dogin" }, g.LocalEnemies("danggata").Select(e => e.Id));
            Assert.Empty(g.LocalEnemies("gaebang_hq"));
            Assert.Empty(g.LocalEnemies("cheongun"));
        }

        [Fact] public void 정예는_서_있는_지역의_것이고_없는_땅은_숭산_표를_쓴다()
        {
            var g = NewRegions();
            var songshan = _d.Elites["chapter1"];
            Assert.Equal(songshan, g.Elites("songshan"));
            Assert.Equal(songshan, g.Elites("gaebang_hq"));
            Assert.Equal(songshan, g.Elites("namgung_manor"));
            Assert.Equal(songshan, g.Elites("cheongun"));                         // 서장 아닌 생의 청운진
            Assert.Equal(_d.Elites["prologue"], g.Elites("cheongun", prologueLife: true));
            // 첫째(5분)·둘째(10분)·셋째(15분), 넷째·다섯째는 셋째가 다시
            Assert.Equal(songshan[0], g.Elite("songshan", 0));
            Assert.Equal(songshan[1], g.Elite("songshan", 1));
            Assert.Equal(songshan[2], g.Elite("songshan", 2));
            Assert.Equal(songshan[2], g.Elite("songshan", 4));
            Assert.Equal(_d.Elites["prologue"][0], g.Elite("cheongun", 1, prologueLife: true));
            Assert.Contains("넷째·다섯째는 셋째가 다시 나옴", TestData.GddSection("정예(5분마다)"));
            Assert.Contains("숭산 표", TestData.GddSection("정예(5분마다)"));
            // 정예 표가 있는 장은 그 표를 그대로 쓴다
            foreach (var r in g.All.Where(r => r.Chapter != null && r.Chapter != Regions.PrologueChapter && _d.Elites.ContainsKey(r.Chapter)))
                Assert.Equal(_d.Elites[r.Chapter!], g.Elites(r.Id));
        }
    }
}
