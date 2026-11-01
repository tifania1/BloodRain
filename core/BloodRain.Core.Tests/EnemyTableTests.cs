using System.Linq;
using System.Text.RegularExpressions;
using BloodRain.Core.Data;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>기획서 6장 '공통 적' 표의 기혈·속도·접촉 피해 칸과 '행동' 칸에 적힌 숫자가 enemies.json 과 같은지.
    /// 처음 등장 시각은 ChapterDataTests, 지역 고유 적의 기혈·속도·피해는 EliteDataTests 가 본다.</summary>
    public class EnemyTableTests
    {
        readonly GameData _d = Load();

        EnemyDef ByName(string cell) => _d.Enemies.Single(e => e.Chapter == null && e.NameKo == Regex.Replace(cell, @"\(.*\)", "").Trim());

        [Fact] public void 공통_적_기혈_속도_접촉_피해가_데이터와_같다()
        {
            var rows = GddTable(GddSection("공통 적"));
            Assert.Equal(_d.Enemies.Count(e => e.Chapter == null && e.Family != "elite"), rows.Count);
            foreach (var r in rows)
            {
                var e = ByName(r[0]);
                Assert.Equal(Num(e.Health), r[2].Replace(",", ""));

                // '순간이동'(속도 0, 표식 뒤 순간이동), '120 (도망)'
                if (r[3] == "순간이동") { Assert.Equal(0f, e.Speed); Assert.Equal("mark_teleport", e.Behavior); }
                else Assert.Equal(Num(e.Speed), Regex.Match(r[3], @"^\d+").Value);

                // '0 (화살 8)', '0 (장판 초당 6)'
                var m = Regex.Match(r[4], @"^(\d+)(?: \((화살|장판 초당) (\d+)\))?$");
                Assert.True(m.Success, $"{e.NameKo}: 접촉 피해 칸 '{r[4]}'");
                Assert.Equal(Num(e.ContactDamage), m.Groups[1].Value);
                string kind = m.Groups[2].Value, extra = m.Groups[3].Value;
                Assert.Equal(kind == "화살" ? extra : "0", Num(e.ProjectileDamage));
                Assert.Equal(kind == "장판 초당" ? extra : "0", Num(e.ZoneDamagePerSecond));
            }
        }

        /// <summary>'행동' 칸에 숫자로 적힌 것: 넉백 저항 70%, 0.6초 예고선, 1초 뒤 순간이동, 반경 5m +20%, 은자 100.</summary>
        [Fact] public void 공통_적_행동_칸의_숫자가_데이터와_같다()
        {
            var rows = GddTable(GddSection("공통 적")).ToDictionary(r => ByName(r[0]).Id, r => r[6]);

            Assert.Contains($"넉백 저항 {Num(_d.Enemy("donggap").KnockbackResist * 100)}%", rows["donggap"]);
            Assert.Contains($"{Num(_d.Enemy("hyeol_musa").Telegraph)}초 예고선", rows["hyeol_musa"]);
            Assert.Contains($"{Num(_d.Enemy("hyeolyeong_assassin").Telegraph)}초 뒤 순간이동", rows["hyeolyeong_assassin"]);
            var gisu = _d.Enemy("gisu");
            Assert.Contains($"반경 {Num(gisu.AuraRadius)}m 적 이동속도·피해 +{Num(gisu.AuraBonus * 100)}%", rows["gisu"]);
            Assert.True(_d.Enemy("bigang").IgnoresTerrain);
            Assert.Contains("지형 무시", rows["bigang"]);
            var toad = _d.Enemy("golden_toad");
            Assert.NotNull(toad.Reward);
            Assert.Contains($"은자 {toad.Reward!.Silver}과 진기 구슬 다량", rows["golden_toad"]);
            Assert.True(toad.Reward.OrbBurst);

            // 표 아래 문단: 사라지는 초와 혈무사 돌진(2026-11-01 프로토타입에서 옮김)
            var sec = GddSection("공통 적");
            Assert.Contains($"혈인은 나온 뒤 {Num(_d.Enemy("hyeolin").LifeSeconds)}초면 사라지고, 금두꺼비는 {Num(toad.LifeSeconds)}초 안에 못 잡으면 달아납니다", sec);
            var dash = _d.Enemy("hyeol_musa").Dash;
            Assert.NotNull(dash);
            Assert.Contains($"혈무사는 {Num(dash!.Range)}m 안에 들면 예고선을 긋고, {Num(dash.Seconds)}초 동안 초당 {Num(dash.Speed)}m로 찌르며 돌진한 뒤 {Num(dash.RestMin)}~{Num(dash.RestMax)}초 쉬었다가", sec);
            Assert.True(dash.Reach < dash.Range * 1.5f, "돌진이 예고 거리보다 너무 멀리 간다");
            Assert.All(_d.Enemies.Where(e => e.Behavior == "telegraph_dash"), e => { Assert.NotNull(e.Dash); Assert.True(e.Telegraph > 0); });
            var html = ReadRepoFile("web", "prototype", "template.html");
            foreach (var use in new[] { "e.life > E_BY_ID[e.type].lifeSeconds", "d < dash.range", "e.timer = dash.seconds", "e.ax * dash.speed", "rand(dash.restMin, dash.restMax)" })
                Assert.Contains(use, html);

            // 행동 값이 없는 적은 기본값이다.
            var sigang = _d.Enemy("sigang");
            Assert.Equal(0f, sigang.Telegraph);
            Assert.Null(sigang.Reward);
            Assert.Null(sigang.OnDeath);
            Assert.Equal(0f, sigang.LifeSeconds);
            Assert.Null(sigang.Dash);
        }

        /// <summary>지역 고유 적 표의 '행동' 칸: 흑백 쌍강시 '10초 뒤 되살림', 빙강시 '2초 둔화', 수귀 '물 구역에서만', 쓰러질 때 남기는 것.</summary>
        [Fact] public void 지역_고유_적_행동_칸의_숫자가_데이터와_같다()
        {
            var rows = GddTable(GddSection("지역별 고유 적"));
            string Row(string id) => rows.Single(r => r[1].StartsWith(_d.Enemy(id).NameKo))[2];

            Assert.Contains($"{Num(_d.Enemy("twin_gangsi").ReviveSeconds)}초 뒤 다른 쪽이 되살림", Row("twin_gangsi"));
            Assert.Contains($"닿으면 {Num(_d.Enemy("ice_gangsi").OnHitSlowSeconds)}초 둔화", Row("ice_gangsi"));
            Assert.True(_d.Enemy("water_ghost").OnlyInWater);
            Assert.Contains("물 구역에서만", Row("water_ghost"));
            Assert.Equal("splinter_burst", _d.Enemy("mogin_nahan").OnDeath);
            Assert.Contains("나뭇조각이 터져", Row("mogin_nahan"));
            Assert.Equal("poison_pool", _d.Enemy("poison_toad").OnDeath);
            Assert.Contains("죽을 때 독 장판", Row("poison_toad"));
        }
    }
}
