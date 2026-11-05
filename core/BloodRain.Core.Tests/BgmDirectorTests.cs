using System.Text.RegularExpressions;
using BloodRain.Core.Data;
using BloodRain.Core.World;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>언제 배경음을 바꾸나(14장 '곡 바꾸기', balance.json bgm, backlog BGM-06).</summary>
    public class BgmDirectorTests
    {
        readonly BgmRulesDef R = TestData.Load().Balance.Bgm;

        static BgmState At(string region, string? boss = null) => new BgmState(BgmScreen.Life, regionId: region, bossId: boss);

        [Fact] public void 값이_14장_문장과_같다()
        {
            var m = Regex.Match(TestData.Gdd, @"지역 곡끼리는 서 있는 지역이 바뀐 뒤 (\d+)초 넘게 그대로일 때만 (\d+)초 동안 섞어 넘깁니다\. 땅의 주인 싸움, 종장 단계, 화면 전환은 바로 (\d+)초로");
            Assert.True(m.Success, "14장 '곡 바꾸기' 문장이 없다");
            Assert.Equal(R.RegionHoldSeconds, float.Parse(m.Groups[1].Value));
            Assert.Equal(R.RegionFadeSeconds, float.Parse(m.Groups[2].Value));
            Assert.Equal(R.QuickFadeSeconds, float.Parse(m.Groups[3].Value));
        }

        [Fact] public void 처음엔_바로_튼다()
        {
            var d = new BgmDirector(R);
            var c = d.Tick(0, At("songshan"));
            Assert.NotNull(c);
            Assert.Equal("bgm_region_songshan", c!.Value.TrackId);
            Assert.Null(d.Tick(0.1f, At("songshan")));
        }

        [Fact] public void 지역은_머문_시간을_넘겨야_바뀐다()
        {
            var d = new BgmDirector(R);
            d.Tick(0, At("songshan"));
            float t = 0;
            BgmChange? c = null;
            while (t < R.RegionHoldSeconds + 1 && c == null) { t += 0.1f; c = d.Tick(0.1f, At("gaebang_hq")); }
            Assert.NotNull(c);
            Assert.True(t > R.RegionHoldSeconds);
            Assert.Equal("bgm_region_gaebang_hq", c!.Value.TrackId);
            Assert.Equal(R.RegionFadeSeconds, c.Value.FadeSeconds);
        }

        [Fact] public void 경계를_넘나들면_안_바뀐다()
        {
            var d = new BgmDirector(R);
            d.Tick(0, At("songshan"));
            for (int i = 0; i < 20; i++)
            {
                float half = R.RegionHoldSeconds * 0.6f;
                Assert.Null(d.Tick(half, At("gaebang_hq")));
                Assert.Null(d.Tick(half, At("songshan"))); // 돌아오면 기다리던 시간이 없어진다
            }
            Assert.Equal("bgm_region_songshan", d.Current);
        }

        [Fact] public void 땅의_주인_싸움은_바로_넘긴다()
        {
            var d = new BgmDirector(R);
            d.Tick(0, At("songshan"));
            var c = d.Tick(0.016f, At("songshan", "wongak"));
            Assert.Equal("bgm_boss_wongak", c!.Value.TrackId);
            Assert.Equal(R.QuickFadeSeconds, c.Value.FadeSeconds);
            var back = d.Tick(0.016f, At("songshan"));
            Assert.Equal(R.QuickFadeSeconds, back!.Value.FadeSeconds);
        }

        [Fact] public void 화면_전환도_바로()
        {
            var d = new BgmDirector(R);
            d.Tick(0, At("songshan"));
            var c = d.Tick(0.016f, new BgmState(BgmScreen.Settle));
            Assert.Equal("bgm_settle", c!.Value.TrackId);
            Assert.Equal(R.QuickFadeSeconds, c.Value.FadeSeconds);
        }

        [Fact] public void 돌파_연출_동안은_바꾸지_않고_시간도_안_센다()
        {
            var d = new BgmDirector(R);
            d.Tick(0, At("songshan"));
            Assert.Null(d.Tick(R.RegionHoldSeconds * 3, At("gaebang_hq"), breakthrough: true));
            Assert.Null(d.Tick(0.016f, At("songshan", "wongak"), breakthrough: true)); // 보스도 연출 뒤에
            Assert.Equal("bgm_region_songshan", d.Current);
            Assert.NotNull(d.Tick(0.016f, At("songshan", "wongak")));
        }

        // ---- 격해진 벌(BGM-02b) ----

        static BgmState Late(string region, float sec, string? boss = null) => new BgmState(BgmScreen.Life, regionId: region, bossId: boss, lifeSeconds: sec);

        [Fact] public void 격해진_벌_값이_14장_문장과_같다()
        {
            var m = Regex.Match(TestData.Gdd, @"한 생 (\d+)분부터 격해진 벌로 (\d+)초 동안 섞어 넘깁니다");
            Assert.True(m.Success);
            Assert.Equal(R.IntenseAfterSeconds, float.Parse(m.Groups[1].Value) * 60);
            Assert.Equal(R.IntenseFadeSeconds, float.Parse(m.Groups[2].Value));
            Assert.Equal(new[] { "songshan" }, R.IntenseRegions);
        }

        [Fact] public void 십분부터_숭산은_격해진_벌로_기다리지_않고_4초()
        {
            var d = new BgmDirector(R);
            d.Tick(0, Late("songshan", 599f));
            Assert.Equal("bgm_region_songshan", d.Current);
            var c = d.Tick(1f, Late("songshan", 600f));
            Assert.NotNull(c);
            Assert.Equal("bgm_region_songshan_intense", c!.Value.TrackId);
            Assert.Equal(R.IntenseFadeSeconds, c.Value.FadeSeconds);
            // 격해진 벌이 없는 지역·땅의 주인·서장은 한 벌
            Assert.Equal("bgm_region_gaebang_hq", Bgm.TrackId(Late("gaebang_hq", 900f), R));
            Assert.Equal("bgm_boss_wongak", Bgm.TrackId(Late("songshan", 900f, "wongak"), R));
            Assert.Equal("bgm_prologue", Bgm.TrackId(new BgmState(BgmScreen.Life, prologue: true, regionId: "cheongun", lifeSeconds: 900f), R));
            Assert.Equal("bgm_region_songshan", Bgm.TrackId(Late("songshan", 900f)));   // 규칙 없이 부르면 기본 벌
        }

        [Fact] public void 격해진_벌에서_다른_지역으로는_보통처럼_기다린다()
        {
            var d = new BgmDirector(R);
            d.Tick(0, Late("songshan", 700f));
            Assert.Equal("bgm_region_songshan_intense", d.Current);
            Assert.Null(d.Tick(1f, Late("gaebang_hq", 701f)));
            BgmChange? c = null; float t = 1f;
            while (c == null && t < 10f) { t += 0.5f; c = d.Tick(0.5f, Late("gaebang_hq", 700f + t)); }
            Assert.Equal(R.RegionFadeSeconds, c!.Value.FadeSeconds);
        }
    }
}
