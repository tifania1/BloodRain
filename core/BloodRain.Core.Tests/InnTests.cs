using System;
using System.Linq;
using System.Text.RegularExpressions;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Run;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>청운객잔 시설(기획서 10장 '객잔 시설', cheonha.json inn)과 객잔에 머무는 대표 인물(21장).</summary>
    public class InnTests
    {
        readonly GameData _d = TestData.Load();
        CheonhaDef C => _d.Cheonha;

        RebirthBody Body(string sect) =>
            Rebirth.Body(C, new RebirthCandidate(C.Sect(sect), C.Region(C.Sect(sect).Region), Array.Empty<BirthTraitDef>()));

        Soul AfterPrologue(Soul? s = null)
        {
            s ??= new Soul(_d);
            s.EndLife(s.BeginLife(Rebirth.Prologue(_d)).Finish(LifeEndWay.Fallen, 0));
            return s;
        }

        /// <summary>sect 몸으로 태어나 region 의 땅의 주인들을 잡고 쓰러진다.</summary>
        LifeSummary LiveAndKill(Soul s, string sect, params string[] regions)
        {
            var log = s.BeginLife(Body(sect));
            foreach (var r in regions) log.KillBoss(r);
            return s.EndLife(log.Finish(LifeEndWay.Fallen, 0));
        }

        [Fact] public void 기획서_표와_데이터가_같다()
        {
            var rows = TestData.GddTable(TestData.GddSection("객잔 시설"));
            var fs = C.Inn.Facilities;
            Assert.Equal(rows.Select(r => r[0]), fs.Select(f => f.NameKo));
            for (int i = 0; i < rows.Count; i++)
            {
                string cond = rows[i][1];
                var f = fs[i];
                var stone = Regex.Match(cond, @"봉인석 (\d+)개");
                Assert.Equal(stone.Success ? int.Parse(stone.Groups[1].Value) : 0, f.Require.SealStones);
                var crystal = Regex.Match(cond, @"혈정 (\d+)");
                Assert.Equal(crystal.Success ? int.Parse(crystal.Groups[1].Value) : 0, f.BuildBloodCrystal);
                Assert.Equal(cond.Contains("서장 끝"), f.Require.Prologue);
                Assert.Equal(cond == "처음부터", f.Require.SealStones == 0 && !f.Require.Prologue && f.Require.BossKilled == null
                    && f.Require.Unlock == null && f.BuildBloodCrystal == 0);
                if (f.Require.BossKilled != null)
                {
                    // '마천패 첫 처치 뒤 운낭 대화': 그 지역 땅의 주인 이름이 조건 글에 있어야 한다
                    var boss = _d.Bosses.Single(e => e.Id == C.Region(f.Require.BossKilled).Boss);
                    Assert.Contains(boss.NameKo.Split(' ').Last(), cond);
                }
            }
        }

        [Fact] public void 새_혼은_본채만_서장_뒤에_연무장()
        {
            var s = new Soul(_d);
            Assert.Equal(new[] { "main_hall" }, Inn.OpenFacilities(s));
            var summary = s.EndLife(s.BeginLife(Rebirth.Prologue(_d)).Finish(LifeEndWay.Fallen, 0));
            Assert.Equal(new[] { "training" }, summary.NewFacilities);
            Assert.Equal(new[] { "main_hall", "training" }, Inn.OpenFacilities(s));
            Assert.Empty(Inn.Residents(s));
        }

        [Fact] public void 원각을_잡으면_장경각이_열리고_혜공이_객잔에()
        {
            var s = AfterPrologue();
            var summary = LiveAndKill(s, "shaolin", "songshan");
            Assert.Equal(new[] { "library" }, summary.NewFacilities);
            Assert.True(Inn.IsOpen(s, "library"));
            Assert.Equal(new[] { C.Sect("shaolin").Leader }, Inn.Residents(s));
            Assert.Equal("hyegong", Inn.Residents(s)[0]);
        }

        [Fact] public void 약방은_봉인석_둘에_혈정_열을_써서_짓는다()
        {
            var s = AfterPrologue();
            LiveAndKill(s, "shaolin", "songshan", "danggata"); // 봉인석 둘, 혈정 10
            Assert.Equal(10, s.BloodCrystal);
            Assert.False(Inn.IsOpen(s, "apothecary"));
            Assert.Contains(Inn.Buildable(s), f => f.Id == "apothecary");
            Assert.True(Inn.TryBuild(s, "apothecary"));
            Assert.Equal(0, s.BloodCrystal);
            Assert.True(Inn.IsOpen(s, "apothecary"));
            Assert.False(Inn.TryBuild(s, "apothecary")); // 이미 있음
            Assert.DoesNotContain(Inn.Buildable(s), f => f.Id == "apothecary");
        }

        [Fact] public void 혈정이_모자라거나_조건이_안_되면_못_짓는다()
        {
            var s = AfterPrologue();
            s.AddBloodCrystal(19);
            Assert.False(Inn.TryBuild(s, "smithy"));
            Assert.Equal(19, s.BloodCrystal);
            s.AddBloodCrystal(1);
            Assert.True(Inn.TryBuild(s, "smithy"));

            s.AddBloodCrystal(50); // 혈정은 넉넉해도 봉인석이 없으면 약방은 안 된다
            Assert.False(Inn.TryBuild(s, "apothecary"));
            Assert.Equal(50, s.BloodCrystal);
            Assert.Throws<InvalidOperationException>(() => Inn.TryBuild(s, "library"));
        }

        [Fact] public void 하오문_게시판은_마천패_뒤_운낭_대화()
        {
            var s = AfterPrologue();
            LiveAndKill(s, "shaolin", "janggang");
            Assert.False(Inn.IsOpen(s, "haomun_board"));
            s.Unlock(C.Inn.Facility("haomun_board").Require.Unlock!);
            Assert.True(Inn.IsOpen(s, "haomun_board"));
            Assert.Equal(new[] { "haomun_board" }, Inn.Refresh(s));
            Assert.Empty(Inn.Refresh(s));
        }

        [Fact] public void 회귀로_봉인석이_지워져도_열린_시설은_남는다()
        {
            var s = AfterPrologue();
            LiveAndKill(s, "shaolin", "songshan", "danggata", "janggang");
            Assert.True(Inn.IsOpen(s, "cave"));
            s.Regress();
            Assert.Equal(0, s.SealStoneCount);
            Assert.True(Inn.IsOpen(s, "library"));
            Assert.True(Inn.IsOpen(s, "cave"));
            var loaded = Soul.FromJson(_d, s.ToJson());
            Assert.Equal(Inn.OpenFacilities(s), Inn.OpenFacilities(loaded));
        }

        [Fact] public void 대표_인물이_없는_땅은_합류가_없다()
        {
            var s = AfterPrologue();
            LiveAndKill(s, "shaolin", "huashan", "songshan");
            // 화산은 대표 인물이 없다(BossAwakening 의 합류 규칙과 같음)
            Assert.Equal(C.BossRegions.Where(r => r.Id is "songshan" or "huashan")
                .Select(r => C.Sects.FirstOrDefault(x => x.Id == r.Sect)?.Leader).Where(l => !string.IsNullOrEmpty(l)), Inn.Residents(s));
        }
    }
}
